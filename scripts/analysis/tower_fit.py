#!/usr/bin/env python3
"""Turn CombatReviewSmoke --tower-fit output into ChallengeTowerCatalog's per-floor strength table.

    godot --headless --path . res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-towerfit \
        --tower-fit --floors=1,2,3 > fit.log
    python3 scripts/analysis/tower_fit.py fit.log [more logs...] [--write]

Each TOWER_FIT line is one floor's fitted strength s: the most the floor's reference player beats on both of two seeds
(health 1 + 4s, damage 1 + 2.5s on its campaign stage, its modifiers on). A later line for a floor replaces an earlier
one. The table takes s less MARGIN, so a floor sits a little inside what the reference player beat, except where the
player beat the original curve itself (the search's ceiling): that floor keeps the original value. Every floor must
have a line. --write replaces the table in scripts/core/ChallengeTowerCatalog.cs; without it the table is printed.
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CATALOG = ROOT / "scripts/core/ChallengeTowerCatalog.cs"
FLOORS = 100
MARGIN = 0.02
LOWEST = -0.2  # --tower-lowest can take a fit below the usual -0.1 for a floor the reference player can't beat there


def main():
    paths = [a for a in sys.argv[1:] if not a.startswith("--")]
    fits = {}
    for path in paths:
        for line in Path(path).read_text(encoding="utf-8", errors="replace").splitlines():
            if "TOWER_FIT: " in line:
                fit = json.loads(line.split("TOWER_FIT: ", 1)[1])
                fits[fit["floor"]] = fit
    missing = [f for f in range(1, FLOORS + 1) if f not in fits]
    if missing:
        print("No fit for floors: " + ", ".join(map(str, missing)))
        return 1
    unbeaten = [f for f, fit in fits.items() if not fit.get("beatsLowest", fit["beatsUnscaled"])]
    if unbeaten:
        print(f"The reference player loses even at s = {LOWEST} on floors: " + ", ".join(map(str, sorted(unbeaten))))
    def strength(fit):
        ceiling = fit.get("ceiling", fit.get("catalogStrength", (fit["floor"] - 1) / 99))
        return round(ceiling, 3) if fit["strength"] >= ceiling - 0.001 else max(LOWEST, round(fit["strength"] - MARGIN, 3))
    strengths = [strength(fits[f]) for f in range(1, FLOORS + 1)]
    rows = ["\t\t" + ", ".join(f"{s:.3f}f".replace("-0.000f", "0.000f") for s in strengths[i:i + 10]) + ","
            for i in range(0, FLOORS, 10)]
    table = "\n".join(rows)
    if "--write" not in sys.argv:
        print(table)
        return 0
    source = CATALOG.read_text(encoding="utf-8")
    # Only the table's own rows (two tabs deep) are replaced, never what follows it.
    updated, count = re.subn(r"(private static readonly float\[\] Strength =\n\t\{\n)((?:\t\t[^\n]*\n)*)(\t\};)",
                             lambda m: m.group(1) + table + "\n" + m.group(3), source)
    if count != 1:
        print("Could not find the Strength table in " + str(CATALOG))
        return 1
    CATALOG.write_text(updated, encoding="utf-8")
    print(f"Wrote {FLOORS} floors to {CATALOG.relative_to(ROOT)}; lowest {min(strengths):.3f}, highest {max(strengths):.3f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
