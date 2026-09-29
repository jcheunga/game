#!/usr/bin/env python3
"""Summarize matched real-engine campaign runs; a loss is balance evidence, not a test error."""
import hashlib
import json
import statistics
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / "artifacts/stage-length-review"
OUT = ROOT / "docs/STAGE_LENGTH_REVIEW.md"


def records(path, prefix):
    text = path.read_text()
    if "COMBAT_REVIEW_RESULT: 0 failures" not in text or "ERROR:" in text or "COMBAT_CHECK: FAIL" in text:
        raise ValueError(f"Incomplete or failed engine run: {path}")
    return [json.loads(line.split(": ", 1)[1]) for line in text.splitlines() if line.startswith(prefix + ": ")]


def main():
    layout = {s["stage"]: s for s in records(ART / "layout.log", "STAGE_LAYOUT")}
    samples = {width: {} for width in ("short", "long")}
    for width in samples:
        for start in (1, 31):
            batch = records(ART / f"{width}-{start}.log", "COMBAT_SAMPLE")
            assert sorted(s["stage"] for s in batch) == list(range(start, start + 30))
            for sample in batch:
                samples[width].setdefault(sample["stage"], []).append(sample)
        for seed in (1000, 2000):
            batch = records(ART / f"repeat-{width}-{seed}.log", "COMBAT_SAMPLE")
            assert len(batch) == 19 and len({s["stage"] for s in batch}) == 19
            for sample in batch:
                samples[width][sample["stage"]].append(sample)
    counters = {f.stem: records(f, "COMBAT_SAMPLE")[0] for f in sorted(ART.glob("counter-*.log"))}
    assert len(counters) == 4 and len(layout) == 60
    records(ART / "regressions.log", "COMBAT_SAMPLE")
    short, long = samples["short"], samples["long"]
    both_won = [n for n in long if short[n][0]["won"] and long[n][0]["won"]]
    median_delta = statistics.median(long[n][0]["seconds"] - short[n][0]["seconds"] for n in both_won)
    notes = {
        7: "Pacing: all wins, but clears rise from 81–89s to 127–155s. Review convoy travel and held waves.",
        8: "Priority: 105s target missed in all three long runs; all three short runs earned it.",
        12: "Priority: reference squad wins fall from 3/3 to 1/3; investigate Forge escorts and reinforcement travel.",
        13: "Monitor jam/hazard exposure; initial star loss did not repeat consistently.",
        15: "Initial loss becomes a win; the larger field can also favor player buildup. Still misses deploy/jam stars.",
        16: "Pacing: clears increase to 140–205s; no side mission secured in the long repeats.",
        19: "Shrine completion varies at both lengths (1/3 each); shifted position needs lane-directed playtesting.",
        25: "Priority: 150s target missed in 2/3 long runs, versus 0/3 short runs; graveyard pressure lasts longer.",
        26: "Reliability: wins drop 3/3 → 2/3; longer Grave Toll encounter.",
        29: "Pacing/attrition: clear grows 109s → 169s; this stage also has the expanded cursed strip.",
        43: "Side mission completion falls 3/3 → 1/3; check marked lane access and hazard overlap.",
        44: "Breach crew moves from x916 to x1914; success falls 3/3 → 2/3. Retest approach and hold position.",
        49: "Initial reference loss becomes a win, despite longer cursed ground. Do not assume all maps become harder.",
        50: "Priority: Purge Seal completion falls 2/3 → 0/3; original 36s activation and hold rule need review.",
        51: "Pacing: 80–88s becomes 115–177s. Review siege reinforcement travel; mission remains attainable.",
        52: "Priority: 1/3 → 0/3 reference wins; cursed ground spans 2.66× as far. Baseline already difficult.",
        53: "Priority: 1/3 → 0/3 reference wins. Check curse attrition and late pressure; baseline already difficult.",
        55: "Priority: 3/3 → 2/3 wins, mission 2/3 → 0/3; expanded curse strip plus tunnel pressure.",
        56: "No reference wins at either length; longer defeats alone do not prove a new blocker. Needs a stronger squad review.",
        57: "Pacing: 96–103s becomes 111–181s; escort remains attainable in every sample.",
        58: "Priority: all three long reference runs reach the 300s audit cap. Specialist siege squad also times out.",
        59: "Initial reference loss becomes a long-map win (239s); expanded curse exposure still warrants specialist review.",
        60: "Priority: specialist support squad changes from a win to a loss; normal reference fails at both lengths.",
    }
    def result(s):
        status = "Win" if s["won"] else "Cap" if s["seconds"] >= s["timeLimit"] else "Loss"
        return f"{status} {s['seconds']:.1f}s · {s['stars']}★"

    lines = [
        "# Campaign review after doubling battlefield length",
        "",
        "Reviewed 2026-09-28. **The longer map changes stage balance; the existing campaign has not been individually redesigned for it.**",
        "",
        "## What changed",
        "",
        "Every stage uses the same combat layout: world width grew from 1280 to 2560, and the enemy spawn/gate moved 1280 units right. The base gap is now 2368 instead of 1088, and the playable horizontal span is 2392 instead of 1112. Units retain their movement speeds, attack ranges, costs, health, and damage. Waves, star targets, mission start times, hold requirements, hazard intervals/radii, and summon cooldowns retain their authored values. Map-menu node positions and progression requirements did not change.",
        "",
        "Victory still requires an intact wagon, a breached gate, and every scripted wave/pending spawn/living defender cleared. Star time targets are optional scoring conditions; they are not automatic defeat timers. Mission TargetSeconds measures accumulated objective progress, not a deadline to reach the site.",
        "",
        "## Coverage and limitations",
        "",
        "- Inspected all **60 stages, 330 waves, 56 resolved side missions, and 108 hazards**, including generated campaign side missions and runtime star-objective overrides.",
        "- Ran every stage at both widths using the existing tactical benchmark: **120 engine battles**. Repeated 19 flagged stages at both widths with two additional battle RNG seeds: **76 more battles**. Compared specialist squads on stages 58 and 60 at both widths: **4 additional battles**, for **200 total**.",
        "- The reference squad is Swordsman, Archer, and Shield Knight, at the harness's stage-scaled upgrade levels, with its standard tactical spells/base upgrades. The two specialist checks additionally use the armaments profile and stage-unlocked counter squads. Each run uses an isolated save; the player's save is untouched.",
        "- This is a consistent automated strategy, not optimal play or proof a stage is impossible. The bot does not deliberately secure mission lanes, dodge every hazard, or select the late adaptive-wave directive. Its star misses on those objectives must not be interpreted as engine failures. Matching battle seeds reduce variation; they do not establish exhaustive balance coverage.",
        "- **300 seconds is the audit cutoff, not a gameplay timeout.** All 200 runs finished without a combat-check failure; losses and cutoffs are recorded as balance outcomes. The independent combat regression suite passed 122 checks, and the layout audit passed 180 checks. Headless runs report the previously known ObjectDB exit warning.",
        "",
        "## Findings",
        "",
        f"The initial full sweep won **{sum(v[0]['won'] for v in short.values())}/60** at the old width and **{sum(v[0]['won'] for v in long.values())}/60** at the new width. Among stages won at both widths, median clear time increased **{median_delta:.1f}s**. Longer travel sometimes gives the player more buildup time, so difficulty does not increase uniformly.",
        "",
        "1. **Retune stage 8's speed objective first.** Its unchanged 105s target was met in 3/3 short-map samples and missed in 3/3 long-map samples (105.6–182.2s). Stage 25's 150s target also fell from 3/3 to 1/3 successes (148.8–233.7s on the long field). Tune individual targets alongside encounter pacing; doubling every timer would hide the larger outliers.",
        "2. **Review summoner and boss cleanup before declaring the campaign balanced.** Stage 12 falls from 3/3 to 1/3 wins. Stage 26 falls from 3/3 to 2/3. Stages 52 and 53 fall from 1/3 to 0/3. Existing limits on simultaneous summons still apply, but the longer approach provides more time for summons, jamming, and attrition before reinforcements arrive.",
        "3. **Cursed ground expands much more than a simple camera change.** Its damaging strip grows from 772 to 2052 units (2.66×), with unchanged damage per second. This affects stages 29, 43, 49, 52, 53, 55, and 59. Safe upper/lower lanes still exist. Consider authored curse patches or a capped strip length rather than scaling the damage zone across almost the whole map.",
        "4. **Review mission placement and activation together.** Stage 50's Purge Seal succeeds in 2/3 short runs and 0/3 long runs. Stage 44's Breach Crew moves from x916 to x1914 with its 32s activation unchanged; completion falls 3/3 → 2/3. Stage 19's shrine moves from x719 to x1461, but repeat success is 1/3 at both lengths, so that initial miss alone is not a confirmed regression.",
        "5. **Late encounters require focused manual/specialist tuning.** Stage 58 reaches the audit cap in every long reference run. The specialist comparisons below also regress at the larger width, strengthening the case beyond the basic squad's known limitations.",
        "",
        "### Specialist comparisons (one matched seed each)",
        "",
        "| Stage | Squad | Old map | Long map |",
        "|---|---|---|---|",
    ]
    for label, n in (("siege", 58), ("support", 60)):
        a, b = counters[f"counter-{label}-short"], counters[f"counter-{label}-long"]
        lines.append(f"| {n} · {layout[n]['name']} | {a['squad'].replace('player_', '').replace(',', ', ')} | {result(a)} | {result(b)} |")
    lines += [
        "",
        "## Every stage",
        "",
        "The two result columns show the same initial seed. Repeat wins include all three tested seeds where available. ‘No specific regression observed’ means no isolated issue in this limited strategy; it does not certify optimal difficulty or three-star attainability.",
        "",
        "| # | Stage | Old map | Long map | Repeat wins, old → long | Review |",
        "|---:|---|---|---|---|---|",
    ]
    for n in range(1, 61):
        a, b = short[n], long[n]
        repeat = f"{sum(s['won'] for s in a)}/{len(a)} → {sum(s['won'] for s in b)}/{len(b)}" if len(a) > 1 else "One seed"
        note = notes.get(n, "No specific regression observed; retain for a general pacing playtest.")
        lines.append(f"| {n} | {layout[n]['name']} | {result(a[0])} | {result(b[0])} | {repeat} | {note} |")
    lines += [
        "",
        "## Recommended implementation order",
        "",
        "1. Separate hazard coverage from arena width, especially cursed ground; keep clearly usable safe lanes.",
        "2. Tune the Forge and late summoner encounters around reinforcement travel and post-breach cleanup. Avoid raising enemy caps globally, which could amplify the same problem.",
        "3. Reposition key mission sites and align activation with when their lane can be contested; preserve existing hold durations unless hands-on tests show they are inappropriate.",
        "4. Recalibrate stage 8 and 25 speed targets after those encounter adjustments, then retest all timed/deployment-limit objectives.",
        "5. Replay the full 60-stage sweep and the repeat/specialist cases, then manually verify objective-directed play, adaptive branches, mobile scrolling, and travel downtime. No stage-balance values were changed during this review.",
        "",
        "## Reproduction and evidence",
        "",
        "Build with `dotnet build Game.csproj`. Run `CombatReviewSmoke.tscn` with `--headless --fixed-fps 60`, an isolated `--save-suffix=combat-review-<unique-name>`, and `--tactical --time-limit=300`. Omit `--stages` for all 60. Add `--reference-map` for the original width; this changes only that test process's in-memory tuning. Use `--seed-offset=1000` or `2000` for repeats. `--stage-layout` exports resolved layouts, and `--regressions` runs the independent rule checks.",
        "",
        "Raw logs and the complete per-stage objective/layout/result JSON are under `artifacts/stage-length-review/`. Regenerate this report with `python3 scripts/analysis/stage_length_review.py`. The JSON records SHA-256 hashes for the logs and reviewed game sources.",
    ]
    OUT.write_text("\n".join(lines) + "\n")
    sources = ["data/combat_config.json", "data/stages.json", "data/units.json", "scripts/combat/BattleController.cs",
               "scripts/combat/BattleController.BossPacing.cs", "scripts/combat/BattleSpawnDirector.cs", "scripts/core/StageObjectives.cs",
               "scripts/core/StageMissionEvents.cs", "scripts/tests/CombatReviewSmoke.cs"]
    payload = {"date": "2026-09-28", "battles": 200, "medianMatchedWinDelta": median_delta,
               "stages": [{"layout": layout[n], "short": short[n], "long": long[n], "review": notes.get(n, "No specific regression observed.")}
                          for n in range(1, 61)], "specialists": counters,
               "sourceHashes": {p: hashlib.sha256((ROOT / p).read_bytes()).hexdigest() for p in sources},
               "logHashes": {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(ART.glob("*.log"))}}
    (ART / "review.json").write_text(json.dumps(payload, indent=2) + "\n")
    print(f"Reviewed 60 stages / 200 battles. Report: {OUT}")


if __name__ == "__main__":
    main()
