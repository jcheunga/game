# Crownroad — Blender visual asset library

An editable, stylized miniature-style **first complete visual asset pass** for the current game catalog. This is rendered 3D artwork used in a 2D game, not pixel art. Native scenes retain geometry, materials, cameras and lights. Character rigs use named rigid joints and keyframes, not skinned armatures.

This library establishes usable coverage and consistent rendering, with the material/lighting finish described below. Characters still share archetypes with different equipment and colors. Further silhouette and animation refinement is appropriate before calling the entire art direction final production art.

## Material and lighting finish

The [art direction guide](ART_DIRECTION.md) defines the road-worn miniature finish:
separate wood, cloth, leather, paint, metal, bone, stone and earth responses;
part-local procedural textures; restrained contact shading; a warm key and cool
rim; and clearer environmental depth. These are editable Blender node materials,
not flattened textures or externally linked image files.

`polish_assets.py` upgrades **saved scenes without rebuilding their geometry**.
It defaults to preview-only. Add `--publish` to save the finished scenes and all
affected animation frames, then repack the game assets. Particles and the existing
painted main-menu/map illustrations are intentionally outside this material pass.

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python-exit-code 1 --python art/blender/polish_assets.py -- --ids lantern_caravan,player_brawler --samples 64
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python-exit-code 1 --python art/blender/polish_assets.py -- --category all --samples 64 --publish --resume
python3 art/blender/pack_assets.py all
python3 art/blender/verify_assets.py
```

Finish previews and per-asset publish records are in `artifacts/blender/polish-v2/`.
Resume skips only matching finish-code and sample-count records. Normal geometry
generators must be followed by this finish pass to reproduce the upgraded look.

The completed finish covers **158 scenes**: 52 characters, 8 caravans, 5 mounts,
the gatehouse, 31 fallback battlefields and 61 item/icon scenes.
All 1,484 character animation frames were re-rendered and packed. The original
203-source library remains intact, including the 11 unchanged particle sources.
See [verification](VERIFICATION.md) for the final image, native-scene and engine checks.

## Contact-driven combat animation

`animate_combat.py` edits the finished named-joint rigs in place. It adds ten-pose
attacks across the 52 characters, grouped into equipment/creature motion profiles:
blade cuts/stabs, guarded cuts, heavy cleaves, downward boss strikes, spear/lance
thrusts, bow draw, crossbow recoil, staff casting/striking, flask toss, hammer
command, hound pounce, claw rake, ballista/bombard recoil and siege/nest motion.
These are shared fighting-style families, not 53 entirely independent sets.

Contact/release is attack frame 4 (zero-based). The simulation advances both the
attack pose and its damage/release callback; rendering cannot make a hit happen
early. Attacks commit through recovery, targets can escape the short melee
tolerance, and dying/pooling cancels pending hits. Offensive player abilities use
the release beat; existing boss-area telegraphs remain separate. Damage amounts
and catalog cooldowns are retained, but anticipation/recovery intentionally change
combat timing. A fresh campaign balance review is still appropriate.

Materials, lighting, geometry, portraits and non-attack poses are retained. When a
long weapon needs more framing space, all poses are re-rendered with compensated
draw scale so its apparent size stays consistent. Never rerun `build_units.py`
just to edit an attack: it reconstructs the older base models/animation layout.

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python-exit-code 1 --python art/blender/animate_combat.py -- --ids player_spear --publish
python3 art/blender/pack_assets.py units
python3 art/blender/verify_assets.py
godot --headless --path . scenes/tests/CombatMotionReview.tscn -- --save-suffix=motion-review-unique-run
```

Omit `--publish` for a staged preview. Animation masters and publish records are
under `artifacts/blender/combat-motion/`. Source scenes include the recoverable
original joint poses in `motion_baseline`; manual pose edits should be saved as
a separate source before rerunning the authoring script.

## Scope

52 characters (including 14 bosses), six animation clips per character, 31 fallback battle terrains, 60 stage illustrations, four map atlases, 33 relics, 10 spells, 10 reward badges, 8 meta badges, 83 Codex entries, 11 particle textures, 8 caravan looks, 5 weapon types and an enemy gatehouse.

Current map atlases, modal illustrations, vector UI and fonts remain in place. Existing procedural music and sound effects remain active; Blender is not the audio pipeline. Collision sizes, combat stats, cooldowns and progression rules are unchanged.

## Editable sources

| Folder/file | Contents |
| --- | --- |
| `lantern_caravan.blend` | Timber/iron/teal caravan, independent wheel pivots, studio and battle cameras |
| `caravans/` | Seven additional cosmetic variants, including material and geometry details |
| `units/` | One source scene per unit ID, six named animation ranges |
| `gatehouse/` | Enemy gatehouse |
| `mounts/` | Archers, ballista, firepot, frost and hex sentries |
| `battlefields/` | Every campaign terrain |
| `items/` | Relic, spell, reward and meta-icon models |
| `particles/` | Eleven tintable effect-texture scenes |
| `coverage.json` | Catalog-derived required visual coverage |

`.gdignore` keeps the authoring library out of Godot imports/builds. Models and shaders are constructed locally; no downloaded third-party models or textures are needed. UI and audio outside this folder retain their existing provenance.

## Rebuild

Run from the repository root. These scripts use Blender's bundled Python. Replace the executable path on other platforms.

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python art/blender/build_caravan.py -- --samples 96 --width 1440 --preview
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python art/blender/build_units.py -- --samples 24
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python art/blender/build_worlds.py -- --samples 24
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python art/blender/build_items.py -- --samples 32
/Applications/Blender.app/Contents/MacOS/Blender --background --factory-startup --python art/blender/build_caravan_variants.py -- --samples 48
python3 art/blender/pack_assets.py all
python3 art/blender/verify_assets.py
```

The pack/verify steps require Pillow. They only pack, resize and validate native renders. Rendering uses Cycles with Metal when available and CPU otherwise. Rebuilding **overwrites generated scenes and PNGs**: save manual Blender edits under a different filename first.

Units, worlds and items support `--ids id_a,id_b` for targeted revisions and `--resume` to skip existing complete work. Do not use resume after changing a generator. Units additionally support `--portrait-only`. Worlds accept `--category battlefields|gatehouse`; items accept `--category icons|particles`; caravan variants accept `--category skins|mounts`.

## Runtime contracts

- Unit master frames: 256 × 320, transparent. Packed atlas: 1536 × 960, 8 columns of 192 × 240 frames. Portraits are separately rendered at 512 × 512; icons are 256 × 256.
- Model-inspection atlases: 1280 × 1280, 5 columns of original 256 × 320 frames; idle, walk and attack only. `pack_assets.py previews` rebuilds these from existing masters without rerendering or upscaling. The `units` and `all` actions also refresh them. Files live in `assets/ui/models/` and fall back to battle atlases when absent.
- Frames: idle 0–3, walk 4–9, attack 10–19, hit 20–21, death 22–27, deploy 28–31. Contact/release is atlas frame 14. Matching JSON stores timing/looping, contact frame, profile, weapon/body attachment points, draw scale, ground anchors and health-bar height.
- Individual atlases take priority over 23 shared-class fallbacks. Routine damage now uses a small blended pose reaction over idle/walk/attack; the larger authored Hit clip is retained in the atlas but not selected for normal hits. Death is a short-lived visual independent of the pooled combat unit. See [hit reactions](../../docs/COMBAT_HIT_REACTIONS.md).
- Caravan: 1440 × 1120 RGBA (9:7), matching the 180 × 140 battle slot. Cosmetics share a fixed camera. +X faces forward; −Y is the near side; Z is up; ground is Z = 0.
- Gatehouse: 1440 × 1280. Mounts: 384 × 480. Icons: 512 × 512. Particles: 128 × 128, white/tintable. Battle/menu backgrounds: 1280 × 720. Maps: 1280 × 960.
- Sprites retain transparent margins. Health bars use measured standing silhouettes. Background architecture stays behind playable lanes. High-resolution masters remain separate from compact game atlases.

## Verify and review

```sh
godot --headless --editor --path . --import
dotnet build Game.csproj --nologo
godot --headless --path . scenes/tests/BlenderAssetSmoke.tscn -- --save-suffix=blender-review-unique-run
godot --path . --windowed --rendering-method gl_compatibility --resolution 1280x720 scenes/tests/BlenderAssetSmoke.tscn -- --save-suffix=blender-review-unique-capture --screenshots
godot --headless --path . scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-blender-unique --regressions
```

Use unique review save suffixes; tests must not touch normal player saves. The art check validates imported character metadata, clip ranges, coverage, skins/mounts and animation transitions. Captures use the real battle scene with a representative display roster, not a normal encounter or balance test.

`artifacts/blender/` contains 1,696 character master frames, portrait masters, contact sheets, battle captures, framing/image-validation reports and test logs. This review folder is ignored by Git; source scenes and shipped assets are not. `pack_assets.py all` regenerates contact sheets and coverage. `verify_assets.py` checks decoded image dimensions, opacity, transparent margins and source files.
