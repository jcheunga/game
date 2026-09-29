# Crownroad — surface and lighting direction

## Identity

**Road-worn royal miniatures.** Readable, crafted shapes with travel-worn materials,
not photographic grime or a plastic toy finish. The caravan is the material anchor:
oak, blue-black iron, antique brass, teal cloth, and small amber lanterns.

This finish pass preserves the existing silhouettes, palettes, cameras, animations,
and game sizes. It does not claim that shared character archetypes have become
unique designs. Distinctive boss silhouettes remain a separate modeling pass.

## Material rules

| Family | Visible character | Restraint |
| --- | --- | --- |
| Iron / brass | Broken, broad highlights; fine hammering; contact shading | No chrome, uniform glitter, or orange rust everywhere |
| Flat cut-steel weapons | Broad satin response with a readable face | Avoid fully metallic black silhouettes between reflections |
| Cloth | Soft woven relief, subtle dye variation, grazing-angle sheen | Preserve faction color; weave must not dominate the silhouette |
| Painted heraldry | Satin finish distinct from matching cloth | Shield paint must not inherit fabric weave |
| Wood | Directional grain, warm tonal variation, shallow cut fibers | No deep noisy grooves or random grain between frames |
| Leather | Fine pores, varied roughness, restrained worn sheen | Never as shiny as armor |
| Bone / skin | Quiet mottling, softer highlights, minimal relief | Keep faces and hands readable |
| Stone / earth | Layered grain and roughness; readable masonry planes | Ground detail stays quieter than units |
| Magic / lanterns | Small emissive focal points | No clipping away the crystal/lantern shape |

All textures are native, editable Blender nodes. Generated coordinates remain
attached to each animated part. Color variation and roughness are separate;
bump does not change the silhouette. No external texture files need relinking.

## Lighting

- A broad warm-neutral key from the upper left/front establishes form.
- A weaker cool fill preserves shadow detail without flattening it.
- A cool rear light separates edges and armor from warm ground.
- Local amber lanterns and ghost lights retain their authored colors.
- AgX highlight handling and a small exposure lift keep metal bright, not white.
- Environment haze starts behind the readable midground; night scenes keep a
  cooler key and lower ambient illumination rather than just a dark overlay.
- Subject cameras and framing stay fixed so before/after comparisons are valid.

## Review gates

1. Compare identical before/after cameras, not a flattering new camera angle.
2. Review portraits **and actual-sized battle sprites**. Fine details that vanish
   at game size should still improve the broad material response, not add noise.
3. Check at least cloth, armor, bone, wood, an environment, and a large structure.
4. Render every animation frame after changing a character's material or light rig.
5. Verify transparency, edge padding, atlas dimensions, anchors, animation clips,
   and engine imports. Use isolated test saves, never normal player progress.

## Updating a saved model

Edit the `.blend` scene, then run `polish_assets.py` for its ID. The finishing tool
opens the saved geometry instead of recreating it. Its default is preview-only;
`--publish` updates the source scene and render outputs. Run `pack_assets.py all`
after publishing to rebuild game atlases, icons, and Codex aliases.

This tool deliberately reapplies the shared material recipes and light rig. For
hand-edited shader nodes or bespoke lighting, render directly from the saved
scene instead; do not reapply the finish over those edits. Original material
colors and the chosen surface family are stored in `crownroad_surface_recipe`.

Example: `--category units --ids player_brawler --samples 64 --publish`.
For the caravan use `--category caravan --ids lantern_caravan`.

The geometry-building scripts still recreate models from their recipes. After a
full generator rebuild, run the finishing pass again. Do not run a generator over
a manually edited scene unless its changes have been incorporated or backed up.
