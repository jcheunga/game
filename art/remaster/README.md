# Crownroad art remaster

A second-generation Blender pipeline for every Blender-rendered asset the game ships.
It sits next to the original library in `art/blender/`, which is untouched, so the two
can be compared and either can be rebuilt.

The game still loads rendered PNGs. Runtime contracts (frame sizes, atlas layouts,
metadata fields, anchors, contact frame, motion-profile names, icon and structure
canvases) match the original pipeline, so the remastered files are drop-in. One exception:
units drawn large in battle (bosses, Siege Tower) render bigger masters and pack into
envelope-cropped atlases so they stay as sharp as the regular roster. `density.py` decides
which units qualify and at what scale; `build_units.py` and `pack.py` apply it automatically.

## What changed

| Area | Original | Remaster |
| --- | --- | --- |
| Bodies | Boxes and spheres parented to empties | One sculpted, voxel-fused body per character, skinned to a real armature with anatomy-aware weights |
| Faces | Spheres | Sculpted heads (brow, nose, jaw, cheekbones), conforming beards and hair, skulls with sockets, teeth and mandibles |
| Kit | Shared archetype with recoloured parts | Per-unit designs from a parts library: 8 helmet types, hoods, crowns, mitres, antlers; plate, mail, brigandine, gambeson; tabards, capes on their own bone chains, robes, mantles; 20+ weapon and shield builders |
| Rigs | Rigid joint hierarchy | Humanoid, quadruped (hound, horse, skeletal horse) and machine rigs; IK for two-handed weapons, bows and crossbows; mounted riders parented to the saddle |
| Materials | Flat colour with noise | Procedural PBR with cavity darkening, worn edge highlights, painted top light, chainmail, enamel with chipped edges, wood grain, fur, flesh, bone, ember-cracked iron, gems and glass |
| Animation | Generic swings | Stances per weapon family plus idle, walk (march / heavy / shamble / prowl / float), ten-frame attacks with contact on frame 4, hit, death (back / forward / crumble) and deploy |
| Camera | 15° from side | 3/4 view (about 34° from side) so faces, heraldry and shields read; same world-to-screen scale as before |

## Layout

| Path | Contents |
| --- | --- |
| `rk/` | Shared kit. Characters: `core` (scene, lights, cameras, render), `shaders` (material library), `geo` (mesh builders, sculpting), `rig` (armatures, weights, posing), `body`, `heads`, `armor`, `weapons`, `undead`, `beast`, `machine`, `fur` (particle hair), `anim` (stances and clips), `character` (assembly, keying, framing, metadata), `palette`. Environments and props: `structures`, `env`, `arch`, `dressing`, `figures`, `fx`, `nodekit`, `post` |
| `roster/` | One recipe per unit: `player.py`, `enemy.py`, `bosses.py`, `beasts.py` (hound, cavalry, siege engines) |
| `icons/` | Item-icon recipes, stage and glow post-process |
| `siege/` | War wagon, skins, mounts and gatehouse recipes |
| `menu_scenes/`, `battle_scenes/`, `map_scenes/` | Menu backgrounds, battlefield fallbacks and district maps |
| `build_*.py` | One builder per category (see below) |
| `pack.py` | Packs renders into runtime files under `artifacts/remaster/stage/`; `apply` copies PNG/JSON into `assets/` |
| `compare.py`, `review_page.py`, `*_sheet.py`, `contact_sheets.py`, `review.py`, `ingame.py`, `sheet.py` | Comparison media, the review page and contact sheets (originals are read from git `HEAD`) |
| `blend/` | Saved, editable `.blend` scenes for every asset (about 280 MB) |

## Commands

Run from the repository root. Every Blender builder takes `--ids all` or a comma-separated list, falls back to
the CPU if the GPU runs out of memory (force it with `RK_DEVICE=CPU`), and writes to `artifacts/remaster/<category>/`.

```sh
# units: preview a few (subset of frames + portrait), then final renders + .blend sources
blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_units.py -- --ids player_brawler,enemy_boss --preview --samples 32
blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_units.py -- --ids all --samples 96
# icons, structures, particles, menus, battlefield fallbacks, district maps
blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_items.py -- --ids all --samples 128 --ss 2
blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_structures.py -- --ids all --samples 128 --sheet
# battle presentation bases (assets/structures/battle-v2: 1024x1024 + projected anchor/socket JSON)
blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_battle_structures.py -- --ids all --samples 128
blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_particles.py -- --ids all --samples 128
blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_menus.py -- --ids all --samples 128
blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_battlefields.py -- --ids all --samples 128
blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_maps.py -- --ids all --samples 128
# stage runtime files (does not touch assets/), then review
python3 art/remaster/pack.py stage
python3 art/remaster/compare.py && python3 art/remaster/review_page.py
# when approved: copy staged PNG/JSON over assets/, then reimport in Godot
python3 art/remaster/pack.py apply
godot --headless --editor --path . --import
```

`pack.py apply` replaces shipped PNG/JSON files in `assets/` and never touches Godot `.import` files.
It only replaces files the game already ships (pass `--allow-new` to create new ones), so retired
assets are never brought back. The game has since retired menu backgrounds and district-map panels;
their builders (`build_menus.py`, `build_maps.py`) remain for reference and `pack.py` skips them.

## Editing a unit

Each unit is a short recipe in `roster/`. Change the parts, colours, weapon or stance and
re-run `build_units.py --ids <id> --preview`. Shared changes (a helmet shape, a material,
an attack family) live in `rk/` and apply to every unit that uses them. The saved `.blend`
in `blend/units/` is fully rigged and keyed if you prefer to tweak by hand; render from it
directly and pack with `pack.py`.

Lighting matches the battle renderer (`scripts/combat/BattleLighting.cs`): sun from the upper left
and slightly behind, so the silhouette shadows the game projects (down-right) agree with the shading.
Keys are neutral-warm because the game applies its own per-zone tint. Nothing that the game shadows
itself (units, battle-v2 structures, mounts) has a baked ground shadow.

Conventions: characters face +X, their left is +Y, Z is up, ground is Z = 0; the battle
camera sits on the -Y side. Pose rotations use the `LAT` axis (counter-clockwise as seen by
the camera), `YAW` and `ROLL` from `rk/rig.py`.
