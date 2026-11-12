#!/usr/bin/env python3
"""Builds the campaign stage explorer: one HTML page showing every stage's waves and enemies, its fitted strength,
how alike it is to other stages, and where the free playthrough sim had to grind.

    python3 scripts/tools/stage_explorer.py [--out artifacts/stage-explorer.html] [--sim farm-free1,farm-free2]

Reads data/stages.json, data/units.json, the Strength table in scripts/core/CampaignPacing.cs, the enemy portraits in
assets/ui/portraits/codex and (when present) artifacts/playthrough/<name>.json sim results. The page is published as
a claude.ai artifact; rerun this after design_waves.py or campaign_fit.py and republish to refresh it.
"""
import argparse, base64, io, json, math, os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
RECIPES = {
    "Scouting party": "Vanguard", "First swarm": "Swarm", "Outriders": "Raiders", "Shield line": "Shield line",
    "Sappers": "Siege", "Restless dead": "Necromancy", "Quiet road": "Ambush", "Picked men": "Elite",
    "Road column": "Long march", "Skirmishers": "Commander", "Road patrol": "Opening",
}


RULES = {
    "swarm_density": "more foes at once", "cursed_ground": "burning strip", "drained_courage": "slower courage",
    "mirror_pressure": "foes reflect damage", "lich_graveyard": "fallen foes rise", "strained_caravan": "weaker wagon hull",
    "rapid_assault": "faster waves", "reinforced_barricade": "tougher gate", "elite_vanguard": "stronger foes",
    "surging_courage": "breather: faster courage", "fortified_deploy": "breather: protected fresh troops",
    "armored_convoy": "breather: tougher wagon hull",
}


def load_list(path):
    data = json.load(open(os.path.join(ROOT, path)))
    return data if isinstance(data, list) else next(v for v in data.values() if isinstance(v, list))


def strengths():
    text = open(os.path.join(ROOT, "scripts/core/CampaignPacing.cs")).read()
    block = text[text.index("float[] Strength"):]
    block = block[block.index("{") + 1:block.index("};")]
    return [float(v) for v in re.findall(r"-?\d+\.\d+", block)]


def portrait(unit_id):
    path = os.path.join(ROOT, "assets/ui/portraits/codex", unit_id + ".png")
    if not os.path.exists(path):
        return None
    from PIL import Image
    image = Image.open(path).convert("RGBA").resize((64, 64), Image.LANCZOS)
    out = io.BytesIO()
    image.save(out, "WEBP", quality=70)
    return "data:image/webp;base64," + base64.b64encode(out.getvalue()).decode()


def main():
    args = argparse.ArgumentParser()
    args.add_argument("--out", default="artifacts/stage-explorer.html")
    args.add_argument("--sim", default="fun-free1,fun-free2")
    args = args.parse_args()

    units = {u["Id"]: u for u in load_list("data/units.json")}
    table = strengths()
    sims = []
    for name in filter(None, args.sim.split(",")):
        path = os.path.join(ROOT, "artifacts/playthrough", name + ".json")
        if os.path.exists(path):
            sims.append({row["stage"]: row for row in json.load(open(path))["stages"]})

    stages, vectors = [], []
    for s in load_list("data/stages.json"):
        n = s["StageNumber"]
        counts = {}
        waves = []
        for w in s["Waves"]:
            entries = [[e["UnitId"], e["Count"]] for e in w["Entries"]]
            for unit_id, count in entries:
                counts[unit_id] = counts.get(unit_id, 0) + count
            waves.append({"label": w["Label"], "t": w["TriggerTime"], "gap": w["SpawnInterval"], "e": entries})
        strength = table[n - 1] if n <= len(table) else 0
        boss = next((i for i in counts if units[i].get("IsBoss")), "")
        health = sum(units[i]["MaxHealth"] * c for i, c in counts.items()) * (1 + strength)
        runs = [sim[n] for sim in sims if n in sim]
        stages.append({
            "n": n, "name": s["StageName"], "zone": s["MapName"], "desc": s["Description"],
            "recipe": "Commander" if boss else RECIPES.get(s["Waves"][0]["Label"], "Other"), "s": strength,
            "rule": ", ".join(RULES.get(m["Type"], m["Type"]) for m in s.get("Modifiers") or []), "level": s.get("EnemyLevel", 1),
            "debut": s.get("NewEnemyId") or "", "boss": boss,
            "bodies": sum(counts.values()), "health": round(health), "gate": s["EnemyBaseHealth"], "waves": waves,
            "tries": max((r["attempts"] for r in runs), default=0), "grind": max((r["grind"] for r in runs), default=0),
        })
        vectors.append(counts)

    # How alike two stages are: the cosine of their enemy-count vectors (1 = the same host in the same proportions).
    def alike(a, b):
        dot = sum(c * b.get(i, 0) for i, c in a.items())
        return dot / (math.sqrt(sum(c * c for c in a.values())) * math.sqrt(sum(c * c for c in b.values())) or 1)
    for i, stage in enumerate(stages):
        best = max((j for j in range(len(stages)) if j != i), key=lambda j: alike(vectors[i], vectors[j]))
        stage["twin"] = stages[best]["n"]
        stage["twinAlike"] = round(alike(vectors[i], vectors[best]), 2)

    used = sorted({i for v in vectors for i in v}, key=lambda i: (min(s["n"] for s, v in zip(stages, vectors) if i in v), i))
    enemies = {}
    for unit_id in used:
        u = units[unit_id]
        enemies[unit_id] = {
            "name": u["DisplayName"], "boss": bool(u.get("IsBoss")), "hp": u["MaxHealth"], "dmg": u["AttackDamage"],
            "speed": u["Speed"], "ranged": bool(u.get("UsesProjectile")), "desc": u.get("Description", ""),
            "weak": u.get("Weakness", ""), "color": u.get("ColorHex", "888888"), "img": portrait(unit_id),
        }

    data = {"stages": stages, "enemies": enemies, "simRuns": len(sims)}
    template = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "stage_explorer.template.html")).read()
    out = os.path.join(ROOT, args.out)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    open(out, "w").write(template.replace("/*DATA*/null", json.dumps(data, separators=(",", ":"))))
    print(f"{out}: {len(stages)} stages, {len(enemies)} enemies, {len(sims)} sim runs, {os.path.getsize(out) // 1024} KB")


if __name__ == "__main__":
    sys.exit(main())
