# Painted atlas materials and scenery

Created on 2026-10-02 using the built-in `image_gen` tool. The original PNGs are
preserved without recolouring, background removal or raster alterations.
Exact submitted prompts, dimensions and SHA-256 hashes are recorded in
[manifest.json](manifest.json) and [utility-manifest.json](utility-manifest.json).

- `terrain-materials.png`: nine opaque surface swatches, row-major in a 3 × 3
  atlas: meadow, forest floor, earth, coast, marsh, ash, snow, cobbles and water.
- `medieval-scenery.png`: twenty transparent painted scenery and landmark
  sprites: foliage, ridges, settlements, ruins, forts, shrine and camp.
- `utility-scenery.png`: six transparent painted utility sprites: chest,
  supply wagon, stone bridge, wooden bridge, caravan and farm.

`AdventureAtlasArt` uses fitted source rectangles for each scenery silhouette,
since the generated sprite spacing is organic rather than mechanically equal.
It extracts the nine material swatches into shared GPU textures at runtime.
Imported textures use lossless compression, preserved alpha and mipmaps.
No generated image remains an external runtime dependency.

`MapPathCanvas.Materials` tints scenery for each district, sizes sprites to the
map's world scale, draws continuous surface UVs, and supplies opaque cloud fog
and the screen vignette. The existing zone paintings use aspect-preserving
screen framing with gentle parallax, avoiding world-sized enlargement.

Native fallback drawings remain available when an asset is missing. No
gameplay identities, save fields, tile shapes, food prices or completion
relationships are changed by these visual assets.
