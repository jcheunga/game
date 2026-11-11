#!/usr/bin/env python3
"""Turn CombatReviewSmoke --campaign-fit output into CampaignPacing's per-stage strength table.

    godot --headless --path . res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-campfit-1 \
        --campaign-fit --profiles=lag,cap --stages=1,7,13 > fit-1.log
    python3 scripts/analysis/campaign_fit.py fit-*.log [--write]

Each CAMPAIGN_FIT line is the strongest campaign strength s (enemy health ×(1 + s), damage ×(1 + s/2)) that a player
about to fight the stage beats on two seeds: "cap" has troops, spell and wagon trained to the caps their first clears
allow, "lag" is four troop levels and two wagon ranks behind with untrained spells and no relics. A later line for a
stage and profile replaces an earlier one. Each stage's strength is s_lag + share × (s_cap - s_lag) + offset: leaders
take LEADER_SHARE of the gap, mid-bosses (slot 5, where the zone has one) MIDBOSS_SHARE and zone bosses (slot 10)
BOSS_SHARE; a leader stops OUTLIER above its zone's median leader. A player who
never pays sits at about s_lag (the whole-campaign playthrough showed it), so every stage above that asks for
training they have to grind gold for; a squad trained to its caps (gold a purchase buys at once) wins them all.
The share ramps in from nothing at stage RAMP_FROM to its full value at RAMP_TO, so the first zone stays friendly.

PLAYTEST caps stages where the whole-campaign simulation (docs/PLAYTHROUGH_SIM.md) found a cliff: above some
strength the stage's enemies break the wagon in a minute and a half however well the squad is trained, so a share of
the fitted gap left the simulated free player losing dozens of times. Those stages take the strength the simulated
squad beat after a modest grind.

A stage where s_cap came out below s_lag (seed noise, or both at the search's ceiling) uses their mean for both. Every
stage needs a "cap" fit; a stage without a "lag" fit takes s_cap less the median gap of the stages that have both
(where neither hit the ceiling), and is marked "est" in the report. --write replaces the table in
scripts/core/CampaignPacing.cs; without it the table is printed with the per-stage inputs.
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PACING = ROOT / "scripts/core/CampaignPacing.cs"
STAGES = 100
# Stages before FINAL_CAP_FROM all take the zone bosses' share since 9 November 2026 (the user asked for as hard as
# possible while a free player can still finish). It was 0.4 for leaders and 0.65 for mid-bosses. Above 0.75 the target sits inside the
# fit's seed noise: bosses at 0.85 beat squads trained to their caps again and again (docs/PLAYTHROUGH_SIM.md).
LEADER_SHARE = 0.75
LEADER_OFFSET = 0.0
MIDBOSS_SHARE = 0.75
BOSS_SHARE = 0.75
# From zone 8 a grinding player's troops are at or near the last level (20), so grinding can't lift the squad any
# further (on earlier stages it can: replays raise the player level, and with it the troop cap), and a share in the
# fit's seed noise is a coin toss for a maxed squad. Those stages keep the earlier shares: at 0.75, squads at level
# 19-20 lost stage 79 15-19 times, stage 87 22 times and stage 89 14 times in a row, and the free player (swapping
# lineups) hit the 300-replay limit at stage 79.
FINAL_CAP_FROM = 71
FINAL_CAP_SHARES = {"leader": 0.4, "midboss": 0.65, "boss": 0.75}
RAMP_FROM, RAMP_TO = 3, 20
LOWEST = -0.5
OUTLIER = 0.6
# Stages that field a boss (slot 10 in every zone, slot 5 where the zone has a mid-boss).
_UNITS = {u["Id"]: u for u in json.loads((ROOT / "data/units.json").read_text())["Units"]}
BOSS_STAGES = {s["StageNumber"] for s in json.loads((ROOT / "data/stages.json").read_text())["Stages"]
               if any(_UNITS[e["UnitId"]].get("IsBoss") for w in s["Waves"] for e in w["Entries"])}
# Stage: strength cap from a playthrough that found a cliff there. The caps measured on the old waves (stages 13,
# 25, 30, 80, 85 and 100) went when the waves were redesigned on 2026-11-05. Stage 2 fitted 0.688 between
# neighbours at 0.188: the playthrough's new player (two level-1 troops) lost it three times and replayed stage 1
# eleven times, and so did the spender. At 0.188 it is won first try; 0.35 still lost once.
PLAYTEST = {2: 0.188}
# Leaders and mid-bosses stop at this strength (enemy health ×3.25): fits above it mostly hit the search ceiling, and
# the playthrough found cliffs up there (a squad fitted to win loses every time a little past it).
MAX_STRENGTH = 2.25
CEILING = 3.7  # fits at or above this hit the search ceiling (4) and are lower bounds


def main():
    paths = [a for a in sys.argv[1:] if not a.startswith("--")]
    fits = {}
    for path in paths:
        for line in Path(path).read_text(encoding="utf-8", errors="replace").splitlines():
            if "CAMPAIGN_FIT: " in line:
                fit = json.loads(line.split("CAMPAIGN_FIT: ", 1)[1])
                fits[(fit["stage"], fit["profile"])] = fit
    missing = [s for s in range(1, STAGES + 1) if (s, "cap") not in fits]
    if missing:
        print("No cap fit for stages: " + ", ".join(map(str, missing)))
        return 1
    measured = [fits[(s, "cap")]["strength"] - fits[(s, "lag")]["strength"] for s in range(1, STAGES + 1)
                if (s, "lag") in fits and fits[(s, "cap")]["strength"] < CEILING and fits[(s, "lag")]["strength"] < CEILING]
    gap = sorted(measured)[len(measured) // 2] if measured else 0.0
    estimated = [s for s in range(1, STAGES + 1) if (s, "lag") not in fits]
    if estimated:
        print(f"Estimated s_lag as s_cap - {gap:.3f} (median of {len(measured)} measured gaps) for stages: "
              + ", ".join(map(str, estimated)))
    unbeaten = [k for k, fit in fits.items() if not fit["beatsLowest"]]
    if unbeaten:
        print(f"Players lose even at s = {LOWEST}: " + ", ".join(f"{s}/{p}" for s, p in sorted(unbeaten)))
    strengths = []
    report = []
    for stage in range(1, STAGES + 1):
        cap = max(LOWEST, fits[(stage, "cap")]["strength"])
        lag = max(LOWEST, fits[(stage, "lag")]["strength"] if (stage, "lag") in fits else cap - gap)
        if cap < lag:
            lag = cap = (lag + cap) / 2
        slot = (stage - 1) % 10 + 1
        boss = stage in BOSS_STAGES
        kind = "boss" if boss and slot == 10 else "midboss" if boss else "leader"
        final_cap = stage >= FINAL_CAP_FROM
        share = FINAL_CAP_SHARES[kind] if final_cap else {"boss": BOSS_SHARE, "midboss": MIDBOSS_SHARE, "leader": LEADER_SHARE}[kind]
        ramp = min(1.0, max(0.0, (stage - RAMP_FROM) / (RAMP_TO - RAMP_FROM)))
        target = lag + share * ramp * (cap - lag) + (LEADER_OFFSET * ramp if slot not in (5, 10) else 0.0)
        if not boss:
            target = min(target, MAX_STRENGTH)
        strengths.append(round(target, 3))
        report.append(f"{stage:3d} slot {slot:2d}  lag {lag:6.3f}{' est' if stage in estimated else '    '}  cap {cap:6.3f}  "
                      f"gap {cap - lag:6.3f}  -> {target:6.3f}")
    # A leader at most OUTLIER above its zone's median leader: a fit far above its neighbours is usually two lucky
    # seeds (stage 85 fitted 1.56 against 0-0.5 around it, and a trained squad then lost it every time).
    for zone in range(STAGES // 10):
        leaders = [i for i in range(zone * 10, zone * 10 + 10) if i + 1 not in BOSS_STAGES]
        median = sorted(strengths[i] for i in leaders)[len(leaders) // 2]
        for i in leaders:
            if strengths[i] > median + OUTLIER:
                report[i] += f"  (outlier, capped at {median + OUTLIER:.3f})"
                strengths[i] = round(median + OUTLIER, 3)
    # Playtest caps go on last, so a capped stage doesn't pull its zone's median down and drag its neighbours with
    # it (capping stage 2 first would have cut stages 6, 7 and 9 from 1.288 to 1.059).
    for stage, limit in PLAYTEST.items():
        if strengths[stage - 1] > limit:
            report[stage - 1] += f"  (playtest cap {limit:.3f})"
            strengths[stage - 1] = round(limit, 3)
    rows = ["\t\t" + ", ".join(f"{s:.3f}f".replace("-0.000f", "0.000f") for s in strengths[i:i + 10]) + ","
            for i in range(0, STAGES, 10)]
    table = "\n".join(row.replace("\t\t", "        ") for row in rows)
    if "--write" not in sys.argv:
        print("\n".join(report))
        print(table)
        return 0
    source = PACING.read_text(encoding="utf-8")
    updated, count = re.subn(r"(private static readonly float\[\] Strength =\n    \{\n)((?: {8}[^\n]*\n)*)(    \};)",
                             lambda m: m.group(1) + table + "\n" + m.group(3), source)
    if count != 1:
        print("Could not find the Strength table in " + str(PACING))
        return 1
    PACING.write_text(updated, encoding="utf-8")
    print(f"Wrote {STAGES} stages to {PACING.relative_to(ROOT)}; lowest {min(strengths):.3f}, highest {max(strengths):.3f}, "
          f"mean {sum(strengths) / len(strengths):.3f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
