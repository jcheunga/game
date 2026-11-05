#!/usr/bin/env python3
"""Summarise CombatReviewSmoke --playthrough-sim runs.

    python3 scripts/analysis/playthrough_report.py artifacts/playthrough/free.json artifacts/playthrough/spender.json

Prints one summary row per run, then a per-zone table for each run: battles, losses, grind replays, play time,
the day the zone was finished, the player's level and unit cap, the squad's training, and money spent. Stages that
needed a grind or three or more losses are listed as walls. --markdown prints the same tables as Markdown.
"""
import argparse
import json
import sys
from collections import defaultdict


def load(path):
    with open(path) as handle:
        data = json.load(handle)
    data["name"] = path.rsplit("/", 1)[-1].removesuffix(".json")
    return data


def table(headers, rows, markdown):
    cells = [[str(c) for c in row] for row in rows]
    if markdown:
        lines = ["| " + " | ".join(headers) + " |", "|" + "|".join(" --- " for _ in headers) + "|"]
        lines += ["| " + " | ".join(row) + " |" for row in cells]
        return "\n".join(lines)
    widths = [max(len(h), *(len(r[i]) for r in cells)) if cells else len(h) for i, h in enumerate(headers)]
    fmt = "  ".join("{:>%d}" % w for w in widths)
    return "\n".join([fmt.format(*headers), fmt.format(*("-" * w for w in widths))] + [fmt.format(*r) for r in cells])


def squad_level(deck):
    levels = [int(entry.rsplit(":", 1)[1]) for entry in deck]
    return sum(levels) / len(levels) if levels else 0


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("runs", nargs="+")
    parser.add_argument("--markdown", action="store_true")
    args = parser.parse_args()
    runs = [load(path) for path in args.runs]

    rows = []
    for run in runs:
        s = run["summary"]
        spent = s["spent"]
        rows.append([
            run["name"], f"{s['cleared']}/{s['target']}", s["battles"], s["losses"], s["replays"],
            s["days"], s["playHours"], s["foodBreaks"], s["refills"], s["goldFromBattles"], s["goldBought"],
            spent["units"] + spent["recruits"] + spent["spells"] + spent["wagon"], f"${s['dollars']:.2f}", s["level"],
        ])
    print(table(["run", "cleared", "battles", "losses", "grind", "days", "play h", "food waits", "refills",
                 "gold earned", "gold bought", "gold spent", "money", "level"], rows, args.markdown))

    for run in runs:
        print(f"\n{run['name']}: by zone")
        battles = defaultdict(lambda: {"battles": 0, "losses": 0, "grind": 0, "seconds": 0.0})
        stage_zone = {}
        for row in run["stages"]:
            stage_zone[row["stage"]] = (row["stage"] - 1) // 10 + 1
        # Grind battles replay an earlier stage; count them against the wall that sent the player back.
        wall_of = {}
        target = 1
        for battle in run["battles"]:
            if battle["kind"] != "grind":
                target = battle["stage"]
            zone = (target - 1) // 10 + 1
            entry = battles[zone]
            entry["battles"] += 1
            entry["losses"] += 0 if battle["won"] else 1
            entry["grind"] += 1 if battle["kind"] == "grind" else 0
            entry["seconds"] += battle["seconds"]
        zone_rows = []
        stages = {row["stage"]: row for row in run["stages"]}
        for zone in sorted(battles):
            last = max((s for s in stages if (s - 1) // 10 + 1 == zone), default=None)
            end = stages.get(last)
            b = battles[zone]
            zone_rows.append([
                zone, f"{(zone - 1) * 10 + 1}-{zone * 10}", b["battles"], b["losses"], b["grind"],
                round(b["seconds"] / 3600, 2), end["day"] + 1 if end else "-", end["level"] if end else "-",
                end["unitCap"] if end else "-", f"{squad_level(end['deck']):.1f}" if end else "-",
                end["wagon"] if end else "-", f"${end['dollars']:.2f}" if end else "-",
            ])
        print(table(["zone", "stages", "battles", "losses", "grind", "fight h", "done day", "level", "unit cap",
                     "squad lv", "wagon", "money"], zone_rows, args.markdown))
        walls = [row for row in run["stages"] if row["grind"] > 0 or row["losses"] >= 3]
        if walls:
            print(f"\n{run['name']}: walls (stages that needed a grind or 3+ losses)")
            print(table(["stage", "attempts", "losses", "grind", "level", "unit cap", "squad lv", "money"],
                        [[w["stage"], w["attempts"], w["losses"], w["grind"], w["level"], w["unitCap"],
                          f"{squad_level(w['deck']):.1f}", f"${w['dollars']:.2f}"] for w in walls], args.markdown))
        if run["summary"].get("wall"):
            print(f"\n{run['name']}: STOPPED at stage {run['summary']['wall']} (grind limit reached)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
