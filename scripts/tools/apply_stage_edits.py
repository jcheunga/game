#!/usr/bin/env python3
"""Applies stage edits saved in the Stage Explorer page (scripts/tools/stage_explorer.py) to the game data.

    python3 scripts/tools/apply_stage_edits.py <dir-or-json-files...> [--write]

Each edit is one JSON document as the page saves it (collection "edits", one per stage):
    {"stage": 12, "name": "...", "s": 0.9, "note": "...", "waves": [{"i": 0, "label": "...", "t": 3, "gap": 0.8,
     "e": [{"id": "enemy_walker", "c": 2}]}]}
Claude exports them with the ArtifactData tool (list, out_dir) and passes the directory here. Without --write it only
prints what would change. With it, data/stages.json gets the waves and name, and the Strength table in
scripts/core/CampaignPacing.cs gets s.

A wave keeps the fields the page doesn't edit (Area, SpawnXRatio, AdvanceTriggerXRatio) from the shipped wave it
came from ("i", its index there); a new wave copies them from the wave before it.

Hand edits don't survive the generators: design_waves.py --write rewrites every stage's waves and campaign_fit.py
rewrites the Strength table. Keep the exported edits and re-apply them after either.
"""
import glob, json, os, re, sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
STAGES = os.path.join(ROOT, "data/stages.json")
PACING = os.path.join(ROOT, "scripts/core/CampaignPacing.cs")


def load_edits(paths):
    edits = {}
    for path in paths:
        files = sorted(glob.glob(os.path.join(path, "**", "*.json"), recursive=True)) if os.path.isdir(path) else [path]
        for name in files:
            doc = json.load(open(name))
            doc = doc.get("data", doc)  # an exported document may wrap its fields
            if isinstance(doc.get("stage"), (int, float)) and doc["stage"] >= 1:
                edits[int(doc["stage"])] = doc
    return edits


def main():
    write = "--write" in sys.argv
    paths = [a for a in sys.argv[1:] if not a.startswith("--")]
    if not paths:
        sys.exit(__doc__)
    edits = load_edits(paths)
    raw = json.load(open(STAGES))
    stages = raw if isinstance(raw, list) else next(v for v in raw.values() if isinstance(v, list))
    units = json.load(open(os.path.join(ROOT, "data/units.json")))
    units = units if isinstance(units, list) else next(v for v in units.values() if isinstance(v, list))
    enemy_ids = {u["Id"] for u in units if u.get("Side") == "Enemy"}

    text = open(PACING).read()
    start = text.index("{", text.index("float[] Strength")) + 1
    end = text.index("};", start)
    values = re.findall(r"-?\d+\.\d+", text[start:end])
    changed_strength = False

    for n in sorted(edits):
        edit, stage = edits[n], next(s for s in stages if s["StageNumber"] == n)
        old = stage["Waves"]
        waves = []
        for w in sorted(edit.get("waves", []), key=lambda w: float(w["t"])):
            entries = [{"UnitId": e["id"], "Count": int(e["c"])} for e in w.get("e", []) if int(e["c"]) > 0]
            unknown = [e["UnitId"] for e in entries if e["UnitId"] not in enemy_ids]
            if unknown:
                sys.exit(f"stage {n}: unknown enemy {unknown}")
            if not entries:
                continue
            i = w.get("i", -1)
            source = old[i] if isinstance(i, int) and 0 <= i < len(old) else (waves[-1] if waves else old[0])
            wave = dict(source)
            wave.update({"Label": w["label"], "TriggerTime": float(w["t"]), "SpawnInterval": float(w["gap"]), "Entries": entries})
            waves.append(wave)
        if not waves:
            sys.exit(f"stage {n}: the edit has no waves")
        notes = []
        if waves != old:
            before = sum(e["Count"] for w in old for e in w["Entries"])
            after = sum(e["Count"] for w in waves for e in w["Entries"])
            notes.append(f"waves {len(old)} -> {len(waves)}, bodies {before} -> {after}")
            stage["Waves"] = waves
        name = (edit.get("name") or "").strip()
        if name and name != stage["StageName"]:
            notes.append(f"name '{stage['StageName']}' -> '{name}'")
            stage["StageName"] = name
        if isinstance(edit.get("s"), (int, float)) and abs(float(values[n - 1]) - edit["s"]) > 0.0005:
            notes.append(f"strength {values[n - 1]} -> {edit['s']:.3f}")
            values[n - 1] = f"{edit['s']:.3f}"
            changed_strength = True
        print(f"stage {n}: {'; '.join(notes) or 'no change'}")
        if edit.get("note"):
            print(f"    note: {edit['note']}")

    if not write:
        print("Dry run. Pass --write to change data/stages.json and CampaignPacing.cs.")
        return
    json.dump(raw, open(STAGES, "w"), indent=2, ensure_ascii=False)
    open(STAGES, "a").write("\n")
    if changed_strength:
        rows = ["        " + ", ".join(f"{v}f" for v in values[i:i + 10]) + "," for i in range(0, len(values), 10)]
        open(PACING, "w").write(text[:start] + "\n" + "\n".join(rows) + "\n    " + text[end:])
    print("Written.")


if __name__ == "__main__":
    main()
