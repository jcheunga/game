# Adventure map

The campaign is an isometric medieval atlas with one stage or resource per terrain region. The ten zones keep their own ground materials, foliage and atmosphere. Painted scenery sprites provide forests, mountains, villages, forts and stationary supply wagons. There is no player cart on the map.

## Landscape

The atlas footprint and site spacing are 20% smaller than the original layout.
The ground retains its 2:1 perspective, and all stages, resources, tile identities
and neighboring-tile reveals remain intact. Camera bounds follow the compact
atlas while legacy terrain coordinates remain available for older saves.

Regions have varied sizes and curved shared boundaries around unevenly spaced sites. An irregular coastline, winding riverbanks and mountain ridges break up the map footprint. Explored regions have subtle thin borders that stay the same screen width when zooming; selecting a site adds a stronger outline around its dry land. Unexplored borders remain beneath opaque fog, whose edge follows the explored territory. Clicks use the same terrain polygons as the drawing, while water rejects land selection.

Quiet textured water surrounds the playable coast on the same world-aligned 2:1 ground plane as the zone terrain. It shares the river material scale and follows the same pan and zoom, with a shallow shore and no raised-map shadow or separate scenic backdrop. Zone colours and a soft vignette keep active sites readable. Undiscovered regions receive a fully opaque, textured cloud layer after landscape scenery is drawn; neither the background nor neighboring tree crowns can show through those areas. Reduced motion freezes the cloud and water drift.

World-aligned painted materials cover meadow, forest floor, earth, coast, marsh, ash, snow and cobbles. Material UVs follow the map's 2:1 ground projection. Roads use worn earth, banks use finer shoreline textures, and rivers have layered water detail. Mipmaps keep textures and scenery smooth at wider zoom levels. Scenery and buildings sort by ground height, with clearings around sites; harbor docks sit at riverbanks. Bridges follow their local crossing angle while vertical posts remain upright. Terrain borders remain beneath foliage, while selected sites keep their stronger outline.

The painted forts and pickups are the selectable site objects. Labels beneath every point of interest are removed, while earned stage ratings remain above the roofs. Hit targets and world art share the same ground position and zoom scale. Individual ground anchors account for each sprite's footprint and cast shadow. Click regions use sprite alpha and a small foundation target, preventing transparent marker margins from intercepting adjacent pickups. Tile borders, selected-region outlines and hover footprints are drawn beneath roads, rivers, bridges, buildings and foliage. Gold, food, books, essence and survey charts have distinct painted pickup art. The Map guide, its activity-panel link and the persistent tutorial sentence are removed.

Site details have one page, with no Intel tab. Painted resource icons identify reward amounts and battle entry costs. Travel and resource collection are free. The same icons appear in collection bursts and deployment costs. Mission information remains in battle preparation; unlocked heroic directives can be toggled directly in stage details, refreshing the displayed victory reward.

Point-of-interest names, rewards and stage entry costs remain available in tooltips and site details at every zoom. The map itself shows the painted objects and earned stage stars without persistent captions.

Hovering a stage or resource gently brightens its ground and adds a warm gold
outline just inside the tile's curved dry-land boundary so neighboring fog cannot
hide it. The outline keeps a constant screen width while zooming and remains
beneath roads, rivers, buildings and trees.
It clears when the pointer leaves, when the pickup is collected, or while panning.

Source artwork, exact generation prompts and hashes live in `assets/world/overworld/polished-v3/`. `AdventureAtlasArt` fits each scenery sprite to its source silhouette and caches the material textures; `MapPathCanvas.Materials` applies theme tints, animated surfaces and atmosphere.

Forest clusters, boulders and grasses fill the landscape around clearings for landmarks. The main stages follow a winding road, with stone bridges across the river. King's Road has farmsteads and windmills; Saltwake has docks and sails; Emberforge has furnaces, chimneys and ash ridges; Thornwall has snowy pines and peaks. The remaining zones add graves, ruined arches, reeds, fields and citadel outworks in their own palettes.

Geography is deterministic and cosmetic. Stable tile IDs and their logical neighborhood relationships preserve existing saves, completion routes, food prices and revelation rules.

## Playing

- Tap a stage or landmark to open its details. Tap a resource to travel and collect it. Drag to pan; pinch or scroll to zoom. The left-side zoom and current-position buttons are removed.
- Travelling to any open destination and collecting resources are free, including with zero rations. Only battle entry costs food, shown in stage details and preparation; entry is charged when deploying.
- Each zone starts on its first stage, with only that tile visible. Clearing a stage or collecting a resource opens the eight tiles around that destination. Preparing, travelling, defeat and retreat do not reveal additional tiles.
- Scout towers, Lantern Camp and Shrines of Resolve are removed. Former camp and shrine tiles are ordinary terrain; nearby discoveries keep their positions. Saves with a retired destination return to the first stage and retain explored surroundings. Shrine courage bonuses are removed, including previously collected blessings. Every completion, including a survey chart, opens one neighboring ring. Forgotten treasuries are available when their tile opens.
- Open regular encounters can be challenged in any order. Defeat the five regular leaders to open the boss gate, then defeat that boss to reveal the next zone.
- Claimed resources disappear; completed stages retain earned stars. Sites beneath the dark tile veil remain hidden.
- Tile travel completes immediately, without a moving cart or travel animation. Empty terrain cannot initiate travel.
- Food checks apply only to battle entry and restart. Reaching a stage does not reserve or spend rations. Failed entry leaves the food balance unchanged.

## Saves

Save version 45 records open and reached tile IDs and the current caravan tile, alongside existing stars, claims and zone access. Version 44 and earlier saves translate known legacy points to the corresponding atlas tiles. Retired camp and shrine IDs map to their former terrain regions; visited sites retain explored surroundings. Cleared stages and collected resources open their surroundings; old rewards never pay again. Legacy terrain coordinates remain available for migration. Reset and prestige clear tile progress.

Old tower IDs and coordinates remain in the legacy catalog to preserve discovery
placement, visit history and access to previously played zones. They have no atlas
tile or interaction. A saved caravan at a removed tower returns to the starting
stage; known tiles, balances, stage stars and collected resources are retained.

`AdventureTileCatalog` owns stable point IDs and logical neighbors. `AdventureAtlasLandscape` owns site positions, curved regions, coastline, rivers and roads. `GameState.AdventureTiles` owns costs, arrival, claims, revelation and migration. `MapPathCanvas` owns camera, selection and immediate travel; `MapPathCanvas.Landscape` and `MapPathCanvas.Art` draw the landscape and landmarks. The saved caravan tile remains the current destination for save compatibility; no caravan is rendered.

## Validation

Run `bash scripts/smoke/adventure_map_smoke.sh` with Godot Mono and .NET on the path. The real-engine suite uses an isolated save and covers all ten layouts, completion routes, native cache taps, one-time rewards, free travel, zero-ration collection, entry-only charges, fog gating, defeat/retreat, immediate travel, reduced motion, full disk reload, legacy migration, boss and zone gates, actual battle entry and menu preservation. Screenshots and text audits are saved under `artifacts/tile-map/`.

The `--home-map` review includes these tile checks plus notice dismissal, resource types, developer controls, storehouse and modal regression checks. Add `--small-window` to review 1024 × 768.

The geometry checks cover all ten zones: valid curved polygons, matching hit tests, sites on dry land inside the coastline, varied region shapes and connected completion routes. Use `--atlas-geometry` for a headless geometry-only review.

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
