# Crownroad art remaster

A second-generation Blender pipeline for every Blender-rendered asset the game ships.
It sits next to the original library in `art/blender/`, which is untouched, so the two
can be compared and either can be rebuilt.

The game still loads rendered PNGs. Metadata fields, anchors, motion-profile names, and icon
and structure canvases match the original pipeline. Unit atlases differ: each clip's start and
length is recorded in the unit's JSON (the clips themselves are longer, see below), and every
unit renders a master large enough for about 4.5 atlas pixels per battle pixel (a battle pixel
covers about 5 Retina pixels at the battle zoom), packed into an envelope-cropped atlas.
`density.py` sets the scale per unit; `build_units.py` and `pack.py` apply it automatically.
Bosses and the Siege Tower have so many large frames that the 4096 px atlas limit holds them
to about 1.8–3; `pack.py` reports the density each unit reaches.

## What changed

| Area | Original | Remaster |
| --- | --- | --- |
| Bodies | Boxes and spheres parented to empties | One sculpted, voxel-fused body per character, skinned to a real armature with anatomy-aware weights |
| Faces | Spheres | Sculpted heads (brow, nose, jaw, cheekbones), conforming beards and hair, skulls with sockets, teeth and mandibles |
| Kit | Shared archetype with recoloured parts | Per-unit designs from a parts library: 8 helmet types, hoods, crowns, mitres, antlers; plate, mail, brigandine, gambeson; tabards, capes on their own bone chains, robes, mantles; 20+ weapon and shield builders |
| Rigs | Rigid joint hierarchy | Humanoid, quadruped (hound, horse, skeletal horse) and machine rigs; IK for two-handed weapons, bows and crossbows; mounted riders bound at the origin, then parented to the saddle |
| Materials | Flat colour with noise | Procedural PBR with cavity darkening, worn edge highlights, painted top light, cloth drape folds, creased leather, chainmail, enamel with chipped edges, wood grain, fur, mottled and veined flesh, bone, ember-cracked iron, gems and glass |
| Anatomy | Straight limbs | Shaped arm and leg stations (deltoid, biceps, forearm, calf), horse legs with hock, fetlock and hooves, a hair tail; zombies with ribs and torn wounds laid on the skin; boots on soles that follow the foot |
| Animation | Generic swings | Stances per weapon family plus a six-frame idle, an eight-frame walk (march / heavy / shamble / prowl / float), ten-frame attacks with contact on frame 4, hit, a death performance of its own for every unit (`roster/deaths.py`, solved by `rk/death.py`), deploy, and for ranged units a seven-frame close-quarters `melee` clip (contact on frame 3: kick, stock jab, staff strike, backhand, hammer smash or an engine's ram). Cleaving bosses that also shoot reuse their attack instead |
| Projectiles | Coloured dots | Side-on lit sprites for every physical shot (arrow, crossbow bolt, ballista and bone bolts, harpoon, flask, plague pot, firepot, cog, ghost skull, blight glob) from `build_projectiles.py`; the game flies them on arcs with trails, launch flashes and impacts (`scripts/combat/ProjectileStyles.cs`) |
| Camera | 15° from side | The battle camera's own angle (22° above the ground plane), with each character turned 33.5° toward it so faces, heraldry and shields read, and deaths fall onto the same ground plane as the battlefield |

## Layout

| Path | Contents |
| --- | --- |
| `rk/` | Shared kit. Characters: `core` (scene, lights, cameras, render), `shaders` (material library), `geo` (mesh builders, sculpting), `rig` (armatures, weights, posing), `body`, `heads`, `armor`, `weapons`, `undead`, `beast`, `machine`, `fur` (particle hair), `anim` (stances and clips), `character` (assembly, keying, framing, metadata), `palette`. Environments and props: `structures`, `env`, `arch`, `dressing`, `figures`, `fx`, `nodekit`, `post` |
| `roster/` | One recipe per unit: `player.py`, `enemy.py`, `bosses.py`, `beasts.py` (hound, cavalry, siege engines) |
| `icons/` | Item-icon recipes, stage and glow post-process |
| `siege/` | War wagon, skins, mounts and gatehouse recipes |
| `build_*.py` | One builder per category (see below) |
| `pack.py` | Packs renders into runtime files under `artifacts/remaster/stage/`; `apply` copies PNG/JSON into `assets/` |
| `compare.py`, `review_page.py`, `*_sheet.py`, `contact_sheets.py`, `review.py`, `ingame.py`, `sheet.py` | Comparison media, the review page and contact sheets (originals are read from git `HEAD`) |
| `blend/` | Saved, editable `.blend` scenes for every asset (about 280 MB; gitignored, so only on the machine that rendered them) |

## Commands

Run from the repository root. Every Blender builder takes `--ids all` or a comma-separated list, falls back to
the CPU if the GPU runs out of memory (force it with `RK_DEVICE=CPU`), and writes to `artifacts/remaster/<category>/`.
The exception is `build_projectiles.py`, which writes `assets/projectiles/` directly, so review its diff before
committing.

```sh
# units: preview a few (subset of frames + portrait), then final renders + .blend sources
blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_units.py -- --ids player_brawler,enemy_boss --preview --samples 32
blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_units.py -- --ids all --samples 96
# icons, structures, particles
blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_items.py -- --ids all --samples 128 --ss 2
blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_structures.py -- --ids all --samples 128 --sheet
# battle presentation bases (assets/structures/battle-v2: 1024x1024 + projected anchor/socket JSON)
blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_battle_structures.py -- --ids all --samples 128
blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_particles.py -- --ids all --samples 128
# projectile sprites: written straight to assets/projectiles/ with projectiles.json (size, tip, centre, length, spin)
blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_projectiles.py -- --ids all --samples 64
# stage runtime files (does not touch assets/), then review
python3 art/remaster/pack.py stage
python3 art/remaster/compare.py && python3 art/remaster/review_page.py
# when approved: copy staged PNG/JSON over assets/, then reimport in Godot
python3 art/remaster/pack.py apply
godot --headless --editor --path . --import
```

`pack.py apply` replaces shipped PNG/JSON files in `assets/` and never touches Godot `.import` files.
It only replaces files the game already ships (pass `--allow-new` to create new ones), so retired
assets are never brought back. Battle backdrops, campaign maps and menu screens are now painted art
(`art/royal`, see `docs/ROYAL_UI.md`), so their Blender builders were retired.

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
camera sits on the -Y side. At render time `render_character` parents every root object to a
`_Facing` empty yawed -33.5°, so recipes never rotate characters themselves. Riders and crews
must be bound (`finalize_bones` + `bind`) before their rig is moved onto a mount, or their meshes
stay behind at the build position. Pose rotations use the `LAT` axis (counter-clockwise as seen by
the camera), `YAW` and `ROLL` from `rk/rig.py`.
