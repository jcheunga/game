# Adventure map

The campaign is an isometric medieval atlas with one stage, resource or landmark per terrain region. The ten zones keep their own ground materials, foliage and atmosphere. Painted scenery sprites provide forests, mountains, villages, forts, shrines, supply wagons and the lantern caravan.

## Landscape

Regions have varied sizes and curved shared boundaries around unevenly spaced sites. An irregular coastline, winding riverbanks and mountain ridges break up the map footprint. Explored regions have subtle thin borders that stay the same screen width when zooming; selecting a site adds a stronger outline around its dry land. Unexplored borders remain beneath opaque fog, whose edge follows the explored territory. Clicks use the same terrain polygons as the drawing, while water rejects land selection.

Each zone's painting fills the backdrop with aspect-preserving, screen-sized framing and gentle camera parallax. Subdued colours and a soft vignette keep active sites readable. Undiscovered regions receive a fully opaque, textured cloud layer after landscape scenery is drawn; neither the background nor neighboring tree crowns can show through those areas. Reduced motion freezes the cloud and water drift.

World-aligned painted materials cover meadow, forest floor, earth, coast, marsh, ash, snow and cobbles. Roads use worn earth, banks use finer shoreline textures, and rivers have layered water detail. Mipmaps keep textures and scenery smooth at wider zoom levels. Scenery and buildings sort by ground height, with clearings around sites; harbor docks sit at riverbanks. Terrain borders remain beneath foliage, while selected sites keep their stronger outline.

Source artwork, exact generation prompts and hashes live in `assets/world/overworld/polished-v3/`. `AdventureAtlasArt` fits each scenery sprite to its source silhouette and caches the material textures; `MapPathCanvas.Materials` applies theme tints, animated surfaces and atmosphere.

Forest clusters, boulders and grasses fill the landscape around clearings for landmarks. The main stages follow a winding road, with stone bridges across the river. King's Road has farmsteads and windmills; Saltwake has docks and sails; Emberforge has furnaces, chimneys and ash ridges; Thornwall has snowy pines and peaks. The remaining zones add graves, ruined arches, reeds, fields and citadel outworks in their own palettes.

Geography is deterministic and cosmetic. Stable tile IDs and their logical neighborhood relationships preserve existing saves, completion routes, food prices and revelation rules.

## Playing

- Tap a stage or landmark to open its details. Tap a resource to travel and collect it. Drag to pan; pinch, scroll or use the zoom controls to zoom.
- Travelling to a new destination costs 1 food. Returning to a reached tile is free. Stage entry costs additional food, shown alongside travel before preparation; entry is charged when deploying.
- Each zone starts with a camp and its eight surrounding tiles. Clearing a stage or collecting a resource opens the eight tiles around that destination. Preparing, travelling, defeat and retreat do not reveal additional tiles.
- Watchtowers and survey charts open two rings. Shrines open their neighbors and grant three starting courage per shrine in their own district.
- Open regular encounters can be challenged in any order. Defeat the five regular leaders to open the boss gate, then defeat that boss to reveal the next zone.
- Claimed resources disappear; completed stages retain earned stars. Sites beneath the dark tile veil remain hidden.
- The caravan animates directly to the selected destination. Reduced motion skips this animation. Empty terrain cannot move the caravan.
- Cancelling before arrival spends no travel food and collects no reward. Food checks reserve enough for both new-stage travel and subsequent entry, while entry is still charged only on deployment.

## Saves

Save version 45 records open and reached tile IDs and the current caravan tile, alongside existing stars, claims, shrine benefits and zone access. Version 44 and earlier saves translate known legacy points to the corresponding atlas tiles. Cleared stages and collected resources open their surroundings; old rewards never pay again. Legacy terrain coordinates remain available for migration. Reset and prestige clear tile progress.

`AdventureTileCatalog` owns stable point IDs and logical neighbors. `AdventureAtlasLandscape` owns site positions, curved regions, coastline, rivers and roads. `GameState.AdventureTiles` owns costs, arrival, claims, revelation and migration. `MapPathCanvas` owns camera, selection and cosmetic travel; `MapPathCanvas.Landscape` and `MapPathCanvas.Art` draw the landscape and landmarks.

## Validation

Run `bash scripts/smoke/adventure_map_smoke.sh` with Godot Mono and .NET on the path. The real-engine suite uses an isolated save and covers all ten layouts, completion routes, native cache taps, one-time rewards, independent travel and entry charges, fog gating, defeat/retreat, interrupted travel, reduced motion, full disk reload, legacy migration, boss and zone gates, actual battle entry and menu preservation. Screenshots and text audits are saved under `artifacts/tile-map/`.

The `--home-map` review includes these tile checks plus notice dismissal, resource types, developer controls, storehouse and modal regression checks. Add `--small-window` to review 1024 × 768.

The geometry checks cover all ten zones: valid curved polygons, matching hit tests, sites on dry land inside the coastline, varied region shapes and connected completion routes. Use `--atlas-geometry` for a headless geometry-only review.
