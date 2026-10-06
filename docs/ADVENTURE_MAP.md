# Adventure map

The campaign is an isometric medieval atlas with one stage or resource per terrain region. Each of the ten zones is one painting in the cartographer's-atlas style of the concept, made over the zone's own geography so its coast, river, bridges, road and clearings line up with the playable tiles (`art/royal/maps.py`, see `docs/ROYAL_UI.md`). Painted landmarks stand at the sites. There is no player cart on the map.

## Landscape

The atlas footprint and site spacing are 20% smaller than the original layout.
The ground retains its 2:1 perspective, and all stages, resources, tile identities
and neighboring-tile reveals remain intact. Camera bounds follow the compact
atlas while legacy terrain coordinates remain available for older saves.

Regions have varied sizes and curved shared boundaries around unevenly spaced sites, and the painting follows them. Selecting a site outlines its dry land; hovering one brightens it. Clicks use the same terrain polygons, while water rejects land selection.

The painting's own sea surrounds the zone and the view never pans past the painting; a soft vignette frames it. Undiscovered regions lie under a bank of dark storm cloud (`MapFogLayer` with `assets/shaders/royal_fog.gdshader`): a blurred mask of the hidden tiles that tileable noise frays into lit, slowly drifting billows, whose edge stays inside the hidden tiles so explored ground is clear. Reduced motion freezes the drift. The landmarks of explored sites stand above the cloud.

The painted landmarks are the selectable site objects: forts and the boss's castle, camps, watchtowers, shrines, and gold, food, book, essence and survey pickups (`assets/world/royal/maps/landmarks`). Points of interest have no labels beneath them; earned stage stars sit above the roofs. Hit targets follow each landmark's painted pixels plus a small foundation target, so transparent margins never intercept adjacent pickups.

Tapping a site that can be entered goes straight in: the caravan reaches it and a battle opens its preparation, which shows the rewards and the entry cost. The site panel only appears to explain why a site is blocked, such as a sealed boss gate or missing rations. Travel and resource collection are free.

Point-of-interest names, rewards and stage entry costs remain available in tooltips and site details at every zoom. The map itself shows the painted objects and earned stage stars without persistent captions.

Hovering a stage or resource gently brightens its ground and adds a warm gold
outline just inside the tile's curved dry-land boundary so neighboring fog cannot
hide it. The outline keeps a constant screen width while zooming and lies beneath the
landmarks. It clears when the pointer leaves, when the pickup is collected, or while panning.

The paintings carry each zone's character: King's Road has farmsteads and windmills; Saltwake has docks and sails; Emberforge has furnaces, chimneys and lava; Thornwall has snowy pines and peaks; the remaining zones add graves, ruined arches, reeds, fields and citadel outworks in their own palettes. `UiReviewSmoke --map-guides` draws the flat geography guides the paintings were made over.

Geography is deterministic and cosmetic. Stable tile IDs and their logical neighborhood relationships preserve existing saves, completion routes, entry costs and revelation rules.

## Playing

- Tap a stage to open its battle preparation, or a landmark or resource to travel there and collect it; a blocked site explains why instead. Drag to pan; pinch or scroll to zoom.
- Travelling to any open destination and collecting resources are free, including with zero rations. Only battle entry costs food, shown in stage details and preparation; entry is charged when deploying.
- Each zone starts on its first stage, with only that tile visible. Clearing a stage or collecting a resource opens the eight tiles around that destination. Preparing, travelling, defeat and retreat do not reveal additional tiles.
- Every completion, including a survey chart, opens one neighboring ring. Forgotten treasuries are available when their tile opens.
- Open regular encounters can be challenged in any order. Defeat the zone's nine regular leaders to open the boss gate, then defeat that boss to reveal the next zone.
- Claimed resources disappear; completed stages retain earned stars. Sites beneath the dark tile veil remain hidden.
- Tile travel completes immediately, without a moving cart or travel animation. Empty terrain cannot initiate travel.
- Food checks apply only to battle entry and restart. Reaching a stage does not reserve or spend rations. Failed entry leaves the food balance unchanged.

## Saves

Saves (version 46) record open and reached tile IDs and the current caravan tile, alongside existing stars, claims and zone access. Version 44 and earlier saves translate known legacy points to the corresponding atlas tiles. Saves from the 60-stage campaign have their stage-numbered site and tile IDs moved onto the current stages by `CampaignRenumbering`. The former Lantern Camp tile is ordinary terrain that keeps the camp's revealed state; nearby discoveries keep their positions and visited sites retain explored surroundings. A saved caravan at the retired camp returns to the first stage, and Shrine of Resolve courage blessings no longer apply. Cleared stages and collected resources open their surroundings; old rewards never pay again. Legacy terrain coordinates remain available for migration. Reset and prestige clear tile progress.

Old scout tower and shrine IDs and coordinates remain in the legacy catalog to
preserve discovery placement, visit history and access to previously played zones.
They have no atlas tile or interaction. A saved caravan at one of them returns to
the starting stage; known tiles, balances, stage stars and collected resources are
retained.

`AdventureTileCatalog` owns stable point IDs and logical neighbors. `AdventureAtlasLandscape` owns site positions, curved regions, coastline, rivers and roads. `GameState.AdventureTiles` owns costs, arrival, claims, revelation and migration. `MapPathCanvas` owns camera, selection and immediate travel; `MapPathCanvas.Painted` draws the painting, fog and landmarks and `MapPathCanvas.Landscape` the selection and hover outlines. The saved caravan tile remains the current destination for save compatibility; no caravan is rendered.

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
