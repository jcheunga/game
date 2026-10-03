# Painted atlas materials and scenery

Created on 2026-10-02 using the built-in `image_gen` tool. The original PNGs are
preserved without recolouring, background removal or raster alterations.
Exact submitted prompts, dimensions and SHA-256 hashes are recorded in
[manifest.json](manifest.json), [utility-manifest.json](utility-manifest.json)
and [resource-manifest.json](resource-manifest.json).

- `terrain-materials.png`: nine opaque surface swatches, row-major in a 3 × 3
  atlas: meadow, forest floor, earth, coast, marsh, ash, snow, cobbles and water.
- `medieval-scenery.png`: twenty transparent painted scenery and landmark
  sprites: foliage, ridges, settlements, ruins, forts, shrine and camp.
- `utility-scenery.png`: six transparent painted utility sprites: chest,
  supply wagon, stone bridge, wooden bridge, caravan and farm.
- `resource-scenery.png`: three transparent world pickups: forgotten books,
  essence reliquary and survey chart, painted for the map's 2:1 ground plane.

`AdventureAtlasArt` uses fitted source rectangles for each scenery silhouette,
since the generated sprite spacing is organic rather than mechanically equal.
It extracts the nine material swatches into shared GPU textures at runtime.
Imported textures use lossless compression, preserved alpha and mipmaps.
No generated image remains an external runtime dependency.

Per-sprite ground anchors align visible foundations rather than source-image
centres or cast shadows. Landmarks and pickups serve as the main site markers;
compact captions sit below the objects and scale with their world positions.
Shared alpha hit masks keep transparent sprite margins from intercepting clicks
on neighboring landmarks or resources.

`MapPathCanvas.Materials` tints scenery for each district, sizes sprites to the
map's world scale, projects surface UVs onto the 2:1 terrain plane, and supplies opaque cloud fog
and the screen vignette. The surrounding water uses the same material scale,
ground projection and camera as the rivers and terrain. The home map no longer
places the playable zone over a separate scenic painting or raised-map shadow.

Native fallback drawings remain available when an asset is missing. No
gameplay identities, save fields, tile shapes, food prices or completion
relationships are changed by these visual assets.
