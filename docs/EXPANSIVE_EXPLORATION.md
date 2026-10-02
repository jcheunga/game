# Expansive zone exploration

The home/campaign UI now uses completion-driven atlas tiles. See [Adventure map](ADVENTURE_MAP.md) for current behavior; the terrain and walking details below describe the version 44 implementation retained for save migration.

Implemented locally on 2026-10-01. All ten zones now contain a 32 × 24 isometric field: 768 tiles instead of 96. The world measures 3968 × 2176, with tile centers projected from `(1728, 192)` using `(64, 32)` and `(-64, 32)`. Picking, travel, terrain drawing, smoke masking and discovery markers use these same coordinates.

## Layout and discoveries

Each zone has 20 existing sites spread across the field, with at least four tile steps between sites. The boss is at least 35 steps from camp and at least five steps from any other site. Zone layouts use reflections and translations, while obstacle pockets and discoveries use a stable seed for each zone. Every site and discovery has a reachable ground route. Drawn pools and rock block movement; visible bridges cross pools where a route is needed. There are no route lines or minimap.

There are 40 tile discoveries per zone, covering about 5% of the field. They are at least four steps apart and three steps from the main sites. Each zone contains:

| Discovery | Count | Benefit |
| --- | ---: | --- |
| Hidden provisions | 16 | 4–6 food; the first nearby cache grants 6 |
| Lost coin purse | 8 | 20–80 gold, depending on zone and cache |
| Forgotten writings | 8 | 1–2 tomes |
| Ancient essence | 4 | 1–2 essence |
| Surveyor's chart | 4 | Reveals tiles within four steps |

Provisions are available within six steps of camp. Contents remain hidden under unexplored smoke. When a tile becomes charted, its unclaimed discovery icon appears. Reaching the tile collects its actual resource or survey benefit automatically, plays a sound and briefly shows the reward. Looking at a tile, climbing a watchtower or revisiting a claimed cache never grants its reward again.

## Smoke and movement

Animated fog fully conceals uncharted ground, undiscovered sites and rewards. Travel and scouting reveal the painted landscape; simply panning does not reveal it. Charted ground uses a lighter haze outside caravan sight, while the nearby caravan area is clear. Saved 32 × 24 tile knowledge supplies a continuous 496 × 272 world-space mask, with rounded reveals and flattened cloud banks matching the ground perspective. Reduced motion freezes fog and water movement.

The Explore button finds the closest unknown traversable frontier from actual exploration knowledge. It does not use hidden landmark positions. Zones open zoomed in around the caravan and span several screens; zoom-out still leaves a larger world to pan across. Manual tile travel, dragging, pinch/scroll zoom and Find Caravan remain available. Marker sizes scale with zoom so neighboring locations remain distinguishable.

Entering a new tile costs one food, paid as each step begins. A tile being visible does not waive its first-entry cost. Walked routes cost nothing, including returning when food is depleted. Travel stops at the last reached tile when the next step cannot be paid, without visiting the selected destination. Discovering provisions during a journey can fund later steps. Battle entry remains four food, and recharge remains two food per five minutes, up to 24.

## Persistence and artwork

Save revision 44 records explored cells, paid cells, stable discovery claim IDs and caravan position for each zone. Older saves relocate caravans using their stable site IDs, retain collected sites and shrine bonuses, and reveal previously known or defeated leaders. New surrounding terrain and discovery rewards begin unclaimed. Invalid cells, nonfinite positions and unknown discovery IDs are discarded. Campaign reset/prestige clears exploration and claims.

The original ten zone illustrations and their generation manifest are retained. The subsequent home-screen visual update adds a complete painted King's Road landscape and draws each zone illustration continuously across the field. Charted obstacles use rounded collision-aligned water shapes and painted scenery; the repeated ground-sample presentation has been removed. Updated artwork prompts are recorded in `assets/ui/home/generated-art-v2.json`. See `HOME_MAP_UI.md` for the home layout and visual review.

## Verification

Build/import first, then use an isolated save for each review:

```sh
dotnet build Game.csproj
/Users/jason/.local/bin/godot --headless --editor --path . --import
/Users/jason/.local/bin/godot --path . --windowed --disable-render-loop --rendering-method gl_compatibility res://scenes/tests/ExplorationReview.tscn -- --save-suffix=exploration-review-desktop --capture
```

Add `--mobile-preview` for a phone preview. The review checks all ten layouts, reachable/sparse discoveries, real resource credit, claim persistence, old-save migration, animated/reduced-motion fog, actual caravan travel, exhaustion, free returns and interrupted travel. Captures cover fresh smoke, smoke drift, provisions, watchtower exploration, a zone overview and the Citadel.

The complete repository verification and the existing FeedbackReview and WorldArtReview also cover this change. Phone previews use the desktop engine; physical-device review remains part of the production plan.

Local results on 2026-10-01: 102 exploration checks passed on each layout, 43 gameplay checks passed on each layout, and 279 desktop / 274 phone world-art checks passed. The game build had no warnings or errors; 7,492 data checks and 86 backend tests passed. Review processes exited successfully. Existing Godot shutdown resource warnings and public-site legal placeholders remain unchanged.

Screenshots are in `artifacts/exploration-review/{desktop,phone}/` and `artifacts/world-art-review/{desktop,phone}/`. The ten zone overviews and smoke/arrival previews were visually inspected. The newer home artwork and control captures are in `artifacts/home-map/{desktop,small}/`.
