# Adventure map

The campaign is an isometric medieval atlas of terrain regions, each holding a stage, a resource or plain ground. Each of the ten zones is a wide painting in the cartographer's-atlas style of the concept, made over the zone's own geography so its coast, river, bridges, roads and clearings line up with the playable tiles (`art/royal/mapsections.py`, see `docs/ROYAL_UI.md`). Painted landmarks stand at the sites. There is no player cart on the map.

## Landscape

Each zone is an 18 × 14 grid of tiles (`AdventureTileCatalog`) on a world about three times as wide and tall as one screen of the atlas at its default zoom, with the 2:1 ground perspective. The ten stages are spread along roads: the first stage lies in the west, the road splits at the second stage into three lanes (stages 3–6, 4–7 and 5–8) that cross the river on bridges, and the lanes converge on the ninth stage before the boss in the east. Alternate zones mirror the lanes north to south. Each stage has a supply cache a short walk off its road, a forgotten treasury hides as far from the roads as the land allows, and twenty-eight finds (twelve food, nine gold, four essence, three survey charts) are spread evenly over the open country; no two resources share a border. The last fourteen finds were added after the first wide paintings, so they prefer open ground where the painting has no forest. The remaining tiles are plain ground.

Regions have varied sizes and curved shared boundaries around unevenly spaced sites, and the painting follows them. Two tiles are neighbours when their regions share an edge (usually six of them). Selecting a site outlines its dry land; hovering a site or a frontier tile brightens it. Clicks use the same terrain polygons, while water rejects land selection.

The painting's own sea surrounds the zone and the view never pans past the painting; a soft vignette frames it. Hidden regions lie under a thin layer of painted cloud and frontier regions under a muted, drifting ground mist (`MapFogLayer` with `assets/shaders/royal_fog.gdshader`): blurred masks of the hidden tiles (red channel), frontier tiles (green channel) and a wide blur of the hidden tiles (blue channel), over which two hand-painted cloud layers drift, fluffy cloudlets above a fainter veil; under them plain white hides the land completely, so the whole bank is white with no grey, and across its edge the white fades in over a heavy blur of the land (the shader reads the screen's blurred mipmaps), so nothing on hidden ground can be made out. The cloud spills a little way over the water beside hidden coasts and rivers. Reduced motion freezes the drift. The landmarks of revealed sites stand above the cloud.

The painted landmarks are the selectable site objects: forts and the boss's castle, and gold, food, essence and survey pickups (`assets/world/royal/maps/landmarks`). Points of interest have no labels beneath them; earned stage stars sit above the roofs. Hit targets follow each landmark's painted pixels plus a small foundation target, so transparent margins never intercept adjacent pickups.

Tapping a stage that can be entered goes straight in: a battle opens its preparation, which shows the rewards and the entry cost. Tapping a frontier tile opens it at once for 1 food; every frontier tile but a stage shows that price on a small tag (red when the caravan can't afford it). The site panel only appears to explain why something is blocked, such as a sealed boss gate or missing rations.

Point-of-interest names, rewards and stage entry costs remain available in tooltips and site details at every zoom. The map itself shows the painted objects and earned stage stars without persistent captions.

Hovering a stage, resource or frontier tile gently brightens its ground and adds a warm gold
outline just inside the tile's curved dry-land boundary so neighboring fog cannot
hide it. The outline keeps a constant screen width while zooming and lies beneath the
landmarks. It clears when the pointer leaves, when the pickup is collected, or while panning.

The paintings carry each zone's character: King's Road has farmsteads and windmills; Saltwake has docks and sails; Emberforge has furnaces, chimneys and lava; Thornwall has snowy pines and peaks; the remaining zones add graves, ruined arches, reeds, fields and citadel outworks in their own palettes. `UiReviewSmoke --map-guides` draws the flat geography guides the paintings were made over.

Geography is deterministic and cosmetic: it never depends on saved progress.

## Playing

- A tile is **opened** (charted, clear), on the **frontier** (touching opened ground: seen through mist and ready to open) or **hidden** under the storm cloud. Each zone starts with its first stage opened, along with the plain ground around it: the stage is already discovered, so no tile beside it asks for food and the frontier begins one ring out. Every zone rings its first stage with plain ground (`ExplorationReview` checks it).
- Opening a frontier tile costs 1 food (`GameState.AdventureTileFoodCost`) and only reveals the tiles touching it. A cache or find on the tile is gathered as it opens; a survey chart also charts the plain ground around it for free. Tapping a supply wagon or hidden provisions that borders a stage does the same: the frontier tiles around it open without their 1 food (food elsewhere opens like any tile). That reaches one ring only. Resources never share a border, so the ring is plain ground, which gathers nothing and opens nothing further; stages in it are still won. Without 1 food the tile explains the cost instead and nothing is charged.
- A stage on the frontier is challenged rather than bought: its battle costs its entry rations, charged when deploying, and winning it charts its tile and opens every tile around it for free, gathering any cache or find there. What a victory gathers opens nothing further, so only the land beyond joins the frontier. A stage's first win also pays 6 food (`GameState.CampaignFirstClearFood`). With every road opened and every food source gathered, a zone leaves the caravan about 95–130 food ahead (`ExplorationReview` prints `EXPLORATION_ECONOMY` per zone, and `EXPLORATION_STAGE_RINGS` lists the few resources beside a stage). Preparing, defeat and retreat chart nothing.
- Open regular encounters can be challenged in any order. Defeat the zone's nine regular leaders to open the boss gate, then defeat that boss to reveal the next zone.
- Gathered resources disappear; completed stages retain earned stars. Sites beneath the storm cloud remain hidden.
- Tile travel completes immediately, without a moving cart or travel animation.

## Saves

Saves (version 47) record opened and reached tile IDs and the current caravan tile, alongside existing stars, claims and zone access. Saves from the smaller atlas (version 46 and earlier) keep their stars and gathered caches: on loading, the roads between the stages they won (from each zone's first stage) are charted, a zone whose boss fell is charted apart from its resources, and the caravan returns to the first stage. Finds from the smaller atlas are gone, along with their claims. Saves from the 60-stage campaign have their stage-numbered site IDs moved onto the current stages by `CampaignRenumbering` first. Tile progress loads after the stars, since won stages are charted. Reset and prestige clear tile progress.

Old scout tower, shrine and camp IDs remain in `AdventureMapCatalog` as retired sites so older saves' visits stay valid; they have no atlas tile or interaction. The former 32 × 24 walking terrain and its saved hero positions, explored and travelled cells are gone; older saves' copies are ignored.

`AdventureTileCatalog` owns the grid, stable tile IDs and placement. `AdventureAtlasLandscape` owns site positions, curved regions, adjacency, coastline, river, roads, bridges and the painting's sections. `GameState.AdventureTiles` owns opening costs, the frontier, claims and migration. `MapPathCanvas` owns camera, selection and immediate travel; `MapPathCanvas.Painted` draws the painting, fog, price tags and landmarks and `MapPathCanvas.Landscape` the selection and hover outlines. The saved caravan tile remains the current destination for save compatibility; no caravan is rendered.

## Validation

Run `bash scripts/smoke/adventure_map_smoke.sh` with Godot Mono and .NET on the path. The real-engine suite uses an isolated save and covers all ten layouts, exploration routes, native cache taps, one-time rewards, opening costs, the zero-ration explanation, entry-only battle charges, fog gating, defeat/retreat, immediate travel, reduced motion, full disk reload, legacy migration, boss and zone gates, actual battle entry and menu preservation. `ExplorationReview` checks each zone's layout (stage spacing, lanes, bridges, sparse resources, adjacency) and the exploration rules. Screenshots and text audits are saved under `artifacts/tile-map/`.

The `--home-map` review includes these tile checks plus notice dismissal, resource types, developer controls, storehouse and modal regression checks. Add `--small-window` to review 1024 × 768.

The geometry checks cover all ten zones: valid curved polygons, matching hit tests, sites on dry land inside the coastline, varied region shapes and every tile reachable from the first stage. Use `--atlas-geometry` for a headless geometry-only review.

First-stage exploration verified on 2026-10-02: the build passed without warnings
or errors. Desktop and smaller-window home reviews each passed 326 behavior
checks and 1,543 text checks with zero failures, text issues or runtime
diagnostics. All ten zones begin with their first stage alone, contain no scout
tower destinations and retain connected completion routes. Checks cover the
eight-neighbor first victory, one-ring survey collection, defeat and retreat,
separate travel and entry costs, full disk reload and old tower save migration.
Starting terrain and the first reveal were visually inspected; stable previews
and final logs are in `artifacts/stage-first-map/`.

Compact atlas verified on 2026-10-02: the build passed without warnings or
errors. Desktop and smaller-window tile reviews each passed 149 behavior checks
and 224 text checks with zero failures, text issues or runtime diagnostics.
The reviews cover all ten zones, native landmark and resource selection,
completion reveals, travel and entry costs, save reload and legacy migration.
Before/after overviews and the smaller-window first reveal were visually
inspected. Stable captures and review logs are in `artifacts/compact-map/`.
