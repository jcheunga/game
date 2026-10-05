# Zone battle backdrops

Each zone battles in front of three Blender-rendered layers that stand in for one perspective view:

| Layer | File | Scrolls | What it shows |
| --- | --- | --- | --- |
| far | `<zone>_far.png` | 0.15 × camera | A true perspective vista: the horizon, the painted sky, distant ranges and the zone's landmark |
| mid | `<zone>_mid.png` | 0.45 × camera | Mid-distance scenery at 0.45 scale, seen about 10° above the ground (transparent sky) |
| near | `<zone>.png` | locked to the field | The road the troops walk, its verges and the low scenery lining it, from the battle camera (22°, the angle of every unit and structure sprite) |

`<zone>.json` lists the layers far to near with the battle-world rect each covers while the camera is centred on
the field (`rect`, game units) and its `parallax`. `sky` fills anything above the far layer, and `fog` is the
horizon colour that distant scenery fades toward. `BattleTerrainCanvas` draws the stack and offsets each layer by
`(1 − parallax) × (camera x − field centre)`, so the distant layers drift behind the fight as the camera follows it.

Composition rules the recipes follow, so units stay readable and the sky stays visible:

- The near scenery stands a few metres behind the road and stays low (walls, stalls, carts, hedges; accents to
  about 5 m). The near ground dissolves a few metres further back into the mid layer.
- In front of the road, every prop is scaled so it never rises over the band on screen.
- Mid scenery stays under about 9 m (accents to about 14 m) so the far vista shows over it.
- The far landmark sits 1.5–2.5 km out and peaks just below the top of the desktop frame.

Rebuild (renders go to `artifacts/remaster/backdrops/`, with preview composites in `preview/`; copy the PNG and
JSON files here, then reimport):

```sh
blender --background --factory-startup --python-exit-code 1 \
  --python art/remaster/build_backdrops.py -- --zones all --samples 64
```

Recipes live in `art/remaster/battle_backdrops/<zone>.py`; `--scale 0.5 --samples 16` renders quick drafts.
The painted per-stage plates in `assets/world/battles/` are only used if a zone has no backdrop.
