#!/usr/bin/env python3
"""Rebuild the progression audit from authored encounters and exported runtime prices.

Run scripts/smoke/progression_review.sh first. This is a design model, not live tuning.
"""
import collections
import hashlib
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "docs/progression"
ART = ROOT / "artifacts/progression"
STARTERS = {"player_brawler", "player_shooter", "player_defender"}
SUPPORT = {"player_marksman", "player_coordinator", "player_grenadier", "player_breacher"}
CORE_WAGON = {"hull_plating", "convoy_pantry", "dispatch_console", "signal_relay", "projectile_ward"}


def read(path):
    return json.loads(path.read_text())


def unit_target(stage):
    return 1 if stage < 4 else 2 if stage < 9 else 3 if stage < 16 else 4 if stage < 37 else 5


def wagon_targets(stage):
    level = 0 if stage < 9 else 1 if stage < 21 else 2 if stage < 37 else 3
    targets = {key: level for key in CORE_WAGON}
    for key, milestones in {
        "wagon_archers": [6, 25, 45], "wagon_ballista": [12, 26, 47],
        "wagon_firepot": [15, 29, 49], "wagon_emergency_repair": [19, 35, 51],
        "wagon_armor": [27, 43, 55], "wagon_volley": [33, 46, 57],
    }.items():
        targets[key] = sum(stage >= n for n in milestones)
    return targets


def investment(economy, stage, candidate=False, flexible=False, collector=False):
    level = unit_target(stage)
    multipliers = [1, 2, 3, 5] if candidate else [1, 1, 1, 1]
    unit_ids = STARTERS | (SUPPORT if flexible else set())
    unit_cost = 0
    # Reserve units join the representative route at 18, 23, 28 and 33, trained to the main squad.
    contracts = dict(zip(["player_marksman", "player_coordinator", "player_grenadier", "player_breacher"], [18, 23, 28, 33]))
    for unit in economy["units"]:
        if unit["id"] not in unit_ids or stage < contracts.get(unit["id"], 1):
            continue
        trained_level = level if unit["id"] in STARTERS or collector else min(level, 3 if stage < 45 else 4)
        unit_cost += unit["recruit"] + sum(c * m for c, m in zip(unit["upgrades"][:trained_level-1], multipliers))
    spell_level = 1 if stage < 16 else 2 if stage < 31 else 3
    spell_cost = sum(sum(s["upgrades"][:spell_level-1]) for s in economy["spells"]
                     if s["id"] in {"spell_heal", "spell_fireball"})
    targets = wagon_targets(stage)
    base_cost = sum(sum(b["upgrades"][:targets.get(b["id"], 0)]) for b in economy["wagon"])
    # Promotions of starter units, whose exact runtime costs are exported. Sigils tracked separately.
    promotion_ids = ["player_defender", "player_shooter", "player_brawler"][:sum(stage >= n for n in [40, 48, 56])]
    promoted = [u["promotion"] for u in economy["units"] if u["id"] in promotion_ids]
    promotion_cost = sum(p["GoldCost"] for p in promoted)
    return {"gold": unit_cost + spell_cost + base_cost + promotion_cost,
            "units": unit_cost, "spells": spell_cost, "wagon": base_cost,
            "promotions": promotion_cost, "sigils": sum(p["SigilCost"] for p in promoted)}


def main():
    stages = read(ROOT / "data/stages.json")["Stages"]
    definitions = {u["Id"]: u for u in read(ROOT / "data/units.json")["Units"]}
    economy = read(ART / "economy-runtime.json")
    products = read(ROOT / "data/shop_products.json")
    results = collections.defaultdict(lambda: collections.defaultdict(list))
    log_hashes = {}
    for profile in ["core", "lean", "equipped"]:
        for suffix in ["", "-seed-1000", "-seed-2000"]:
            path = ART / f"{profile}{suffix}.log"
            log = path.read_text()
            if "COMBAT_REVIEW_RESULT: 0 failures" not in log or "ERROR:" in log:
                raise ValueError(f"Incomplete or failed benchmark: {path}")
            samples = [json.loads(line.split(": ", 1)[1]) for line in log.splitlines() if line.startswith("COMBAT_SAMPLE:")]
            expected = 60 if not suffix else 13
            if len(samples) != expected or len({s['stage'] for s in samples}) != expected:
                raise ValueError(f"Expected {expected} distinct stages: {path}")
            for sample in samples:
                sample["seedOffset"] = int(sample.get("seedOffset", 0))
                results[sample["stage"]][profile].append(sample)
            log_hashes[path.name] = hashlib.sha256(path.read_bytes()).hexdigest()

    for profile, expected in [("armed", 13), ("siege-counter", 1), ("support-counter", 1)]:
        for seed in [0, 1000, 2000]:
            path = ART / f"{profile}-seed-{seed}.log"
            log = path.read_text()
            if "COMBAT_REVIEW_RESULT: 0 failures" not in log or "ERROR:" in log:
                raise ValueError(f"Incomplete or failed purchase benchmark: {path}")
            samples = [json.loads(line.split(": ", 1)[1]) for line in log.splitlines() if line.startswith("COMBAT_SAMPLE:")]
            if len(samples) != expected or len({s['stage'] for s in samples}) != expected:
                raise ValueError(f"Expected {expected} purchase benchmark stages: {path}")
            for sample in samples:
                sample["seedOffset"] = int(sample.get("seedOffset", seed))
                results[sample["stage"]][profile].append(sample)
            log_hashes[path.name] = hashlib.sha256(path.read_bytes()).hexdigest()

    maps = collections.defaultdict(list)
    for s in stages:
        maps[s["MapId"]].append(s["StageNumber"])
    district_bosses = {max(v) for v in maps.values()}
    paths = {name: {"balance": 120, "spent": 0, "replays": 0, "largest_shortfall": 0}
             for name in ["current_focused", "candidate_focused", "candidate_flexible", "candidate_collector"]}
    current_gold, candidate_gold, food, sigils = 120, 120, 12, 0
    rows, prior_candidate, prior_current = [], [], []
    for stage in stages:
        n = stage["StageNumber"]
        counts = collections.Counter()
        for wave in stage["Waves"]:
            for entry in wave["Entries"]:
                counts[entry["UnitId"]] += entry["Count"]
        bosses = [definitions[k]["DisplayName"] for k in counts if definitions[k].get("VisualClass") == "boss"]
        boss_count = sum(v for k, v in counts.items() if definitions[k].get("VisualClass") == "boss")
        candidate_reward = 90 + 12 * (n - 1) + (60 if bosses else 0)
        target = investment(economy, n, candidate=True)
        ledger = {}
        for name, track in paths.items():
            candidate = name.startswith("candidate")
            cost = investment(economy, n, candidate=candidate,
                              flexible=name.endswith(("flexible", "collector")), collector=name.endswith("collector"))
            due = cost["gold"] - track["spent"]
            shortfall = max(0, due - track["balance"])
            # Replays only use previously modeled cleared stages. No future victory finances its own entry loadout.
            rewards = prior_candidate if candidate else prior_current
            best_replay = max(rewards, default=0)
            repeats = math.ceil(shortfall / best_replay) if shortfall and best_replay else 0
            if shortfall and not best_replay:
                raise ValueError(f"Unfunded initial loadout: {name}")
            track["balance"] += repeats * best_replay - due
            track["spent"] = cost["gold"]
            track["replays"] += repeats
            track["largest_shortfall"] = max(track["largest_shortfall"], shortfall)
            ledger[name] = {"cost": cost, "balance_before_reward": track["balance"],
                            "replays_here": repeats, "replays_total": track["replays"],
                            "sigil_shortfall": max(0, cost["sigils"] - sigils)}
            track["balance"] += candidate_reward if candidate else stage["RewardGold"]
        modifiers = [m["Type"] for m in stage.get("Modifiers", [])]
        priorities = []
        if any(k in counts for k in ["enemy_jammer", "enemy_revenant_captain"]):
            priorities.append("Rune Beacon; remove support casters")
        if "cursed_ground" in modifiers:
            priorities.append("safe lanes before more health")
        if any(k in counts for k in ["enemy_boneballista", "enemy_plague_engine", "enemy_catacomb_giant"]):
            priorities.append("Mounted Ballista; protected ranged damage")
        if any(k in counts for k in ["enemy_splitter", "enemy_bloater"]):
            priorities.append("Firepot/area damage; spread reinforcements")
        if "tunnel_invasion" in modifiers or "enemy_saboteur" in counts:
            priorities.append("Archer Crew; reserve courage for dives")
        if not priorities:
            priorities.append("frontline then ranged; damage level before collection breadth")
        row = {"stage": n, "name": stage["StageName"], "map": stage["MapId"],
               "map_boss": n in district_bosses, "combat_bosses": bosses,
               "authored_enemies": dict(counts), "authored_hp_budget": round(sum(
                   definitions[k]["MaxHealth"] * v for k, v in counts.items()) * stage["EnemyHealthScale"]),
               "modifiers": modifiers, "current_reward_gold": stage["RewardGold"],
               "current_gross_gold_before": current_gold, "food_before_no_failures": food,
               "entry_food": stage["EntryFoodCost"], "authored_boss_sigils_before": sigils,
               "candidate_reward_gold": candidate_reward, "candidate_gross_gold_before": candidate_gold,
               "target_unit_level": unit_target(n), "target_wagon": wagon_targets(n),
               "priority": priorities[:3], "authored_advice": stage["Description"],
               "new_recruits": [u["name"] for u in economy["units"] if u["unlock"] == n and u["recruit"] > 0],
               "candidate_investment": target, "ledgers": ledger, "benchmarks": results[n]}
        rows.append(row)
        current_gold += stage["RewardGold"]
        candidate_gold += candidate_reward
        food += stage["RewardFood"] - stage["EntryFoodCost"]
        sigils += boss_count
        prior_current.append(stage["RewardGold"])
        prior_candidate.append(math.floor(candidate_reward * .35))

    primary_cost = sum(u["recruit"] + sum(u["upgrades"]) for u in economy["units"]) + sum(
        s["recruit"] + sum(s["upgrades"]) for s in economy["spells"]) + sum(sum(b["upgrades"]) for b in economy["wagon"])
    source_paths = ["data/stages.json", "data/units.json", "data/spells.json", "data/equipment.json", "data/shop_products.json",
                    "scripts/core/GameState.cs", "scripts/core/GameState.Adventure.cs", "scripts/core/GameState.Progression.cs",
                    "scripts/core/AdventureMapCatalog.cs", "scripts/core/BaseUpgradeDefinition.cs",
                    "scripts/core/RelicForgeCatalog.cs", "scripts/core/UnitPromotionCatalog.cs", "scripts/core/UnitSkillTreeCatalog.cs",
                    "scripts/combat/Unit.cs", "scripts/combat/BattleController.cs",
                    "scripts/tests/CombatReviewSmoke.cs", "scripts/tests/CombatReviewSmoke.Progression.cs"]
    audit = {"date": "2026-09-11", "status": "Analysis and candidate design; candidate prices/rewards are not applied",
             "scope": "Normal difficulty; numeric stage itinerary; excludes optional income, collection bonuses and starting mastery",
             "candidate": {"reward_base": 90, "reward_per_stage": 12, "combat_boss_bonus": 60,
                           "replay_fraction": .35, "unit_upgrade_multipliers": [1, 2, 3, 5]},
             "totals": {"current_stage_gold": current_gold - 120, "candidate_stage_gold": candidate_gold - 120,
                        "primary_roster_spell_wagon_cost": primary_cost, "net_food": food - 12,
                        "authored_boss_sigils": sigils}, "economy": economy, "products": products,
             "maps": maps, "paths": paths, "stages": rows,
             "source_sha256": {p: hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in source_paths},
             "benchmark_sha256": log_hashes}
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / "audit.json").write_text(json.dumps(audit, indent=2) + "\n")

    lines = ["# Stage progression matrix", "", "Generated by `scripts/analysis/progression_audit.py`, 2026-09-11.", "",
             "Read PROGRESSION_DESIGN.md for assumptions and the proposed economy. These are target builds and measured attempts, not proven minimum requirements. Numeric order is an analytical itinerary; the adventure map permits ordinary encounters out of order.", "",
             "Core: original tactical reference. Lean: one unit level lower, same wagon/spells. Equipped: lean plus three fixed common relics, assumed owned. All use the starter trio and the same controller. Results are clears/attempts; 13 selected boundaries have three seeds, other stages one. Gear acquisition is not assumed guaranteed by the current game.", "",
             "| Stage | Fight | Core | Lean | Equipped | Proposed level | Current gold before spending | Proposed gold before spending | Next investment / tactic |", "|---|---|---|---|---|---|---|---|---|"]
    for row in rows:
        measured = []
        for p in ["core", "lean", "equipped"]:
            samples = row["benchmarks"][p]
            measured.append(f"{sum(s['won'] for s in samples)}/{len(samples)} (L{samples[0]['level']})")
        lines.append(f"| {row['stage']} | {row['name']} | {' | '.join(measured)} | {row['target_unit_level']} | {row['current_gross_gold_before']:,} | {row['candidate_gross_gold_before']:,} | {'; '.join(row['priority'])} |")
    lines += ["", "## Targeted purchases", "", "Armed adds all six weapon/skill/armor wagon upgrades at the reference wagon level (1 at stages 9–16, 2 at 17–24, 3 thereafter). Counter squads additionally replace the starter trio. All three seeds; no starting gear, doctrines, promotion or mastery. These are complete purchase packages, not proof that every component is required.", "",
              "| Stage | Purchase profile | Squad | Clears / attempts | Current total gold investment |", "|---|---|---|---|---|"]
    for row in rows:
        for profile in ["armed", "siege-counter", "support-counter"]:
            samples = row["benchmarks"].get(profile, [])
            if samples:
                names = [definitions[key]["DisplayName"] for key in samples[0]["squad"].split(",")]
                lines.append(f"| {row['stage']} | {profile} | {' / '.join(names)} | {sum(s['won'] for s in samples)}/{len(samples)} | {samples[0]['investment']['gold']:,} |")
    lines += ["", "## Proposed purchase ledger", "", "Start with 120 gold. Gold below is already net of prior modeled purchases. Replay counts assume victories at the highest previously modeled reward, 35% payout in the candidate. No daily rewards, exploration caches, purchased gold or failed attempts. The flexible route also trains Mage, Monk, Alchemist and Halberdier on missions 18/23/28/33. Promotions require 10 earned sigils across three starter units; assumed authored boss kills are checked separately. Equipment choice rewards in the design are not charged again as forged items.", "",
              "Reserve units stay at L3 through stage 44, then L4. The collector sensitivity instead levels all seven units equally; see audit.json for its extra replay demand.", "",
              "| Before stage | Focused total investment | Focused remaining gold | Flexible total investment | Flexible remaining gold | Flexible replays so far |", "|---|---|---|---|---|---|"]
    for row in rows:
        if row["stage"] not in [4, 8, 12, 16, 21, 26, 31, 36, 41, 46, 52, 58, 60]:
            continue
        f, v = row["ledgers"]["candidate_focused"], row["ledgers"]["candidate_flexible"]
        lines.append(f"| {row['stage']} | {f['cost']['gold']:,} | {f['balance_before_reward']:,} | {v['cost']['gold']:,} | {v['balance_before_reward']:,} | {v['replays_total']} |")
    lines += ["", "## Recruit cost and power", "", "Prices below are the current runtime values. Basic DPS is damage divided by attack cooldown, excluding armor, splash, targeting, spells, synergy and other permanent power. Unit attack cooldown upgrades subtract seconds; they are not percentage reductions.", "",
              "| Unit | Unlock | Recruit gold | L1 to L5 gold | L5 health / L1 | L5 basic DPS / L1 | Promotion gold / sigils |", "|---|---|---|---|---|---|---|"]
    for u in economy["units"]:
        low, high = u["levels"][0], u["levels"][-1]
        promo = u["promotion"]
        lines.append(f"| {u['name']} | {u['unlock']} | {u['recruit']} | {sum(u['upgrades'])} | {high['hp']/low['hp']:.2f}× | {(high['damage']/high['cooldown'])/(low['damage']/low['cooldown']):.2f}× | {str(promo['GoldCost'])+' / '+str(promo['SigilCost']) if promo else 'No path'} |")
    (OUT / "STAGE_MATRIX.md").write_text("\n".join(lines) + "\n")
    print(json.dumps({"totals": audit["totals"], "paths": paths, "samples": sum(len(s) for p in results.values() for s in p.values())}, indent=2))


if __name__ == "__main__":
    main()
