# ASSETS

Accurate drop-in asset manifest for the current runtime pipeline.

The game already supports incremental art/audio replacement. Missing files do not break the build or the game. They fall back to procedural visuals or generated audio until you drop authored assets into `assets/`.

## Blender source assets

The shipped unit sprites, model previews, unit icons and Codex portraits, item icons, caravan skins,
battle-v2 bases, weapon mounts, gatehouse, particles, projectiles, zone battle backdrops and fallback
battle backgrounds come from the remaster pipeline in [`art/remaster/README.md`](art/remaster/README.md):
sculpted, skinned characters, modelled equipment and lighting matched to the battle renderer. It writes
the same runtime contracts described below; `python3 art/remaster/pack.py stage` then `apply` refreshes
`assets/` and only ever replaces files the game already ships (backdrops and projectiles have their own
build scripts). The original library below remains the source for older art.

The original Blender visual asset pass covers the current unit roster, environments, structures, wagon cosmetics, weapon mounts, icons, portraits, and particle textures. Editable scenes and reproducible build instructions live in [`art/blender/README.md`](art/blender/README.md); [`art/blender/coverage.json`](art/blender/coverage.json) records required-file coverage. The game loads rendered PNGs, not live 3D models. The authoring folder is excluded from Godot import. Vector UI, the painted icon and map-piece atlases, fonts, and procedural music/SFX are retained.

The material/lighting finish is defined in [`art/blender/ART_DIRECTION.md`](art/blender/ART_DIRECTION.md). `polish_assets.py` applies editable procedural surfaces and a consistent light rig to saved models, preserving their geometry and animation contracts. Preview first, publish explicitly, then repack the complete animation atlases. The heavier Blender shaders are baked into the images; they add no runtime shader work.

## Audit

### Combat animation

All character atlases contain idle 0–5 (6 frames), walk 6–13 (8), attack 14–23 (10),
hit 24–25 (2), then a 12-frame death from 26 (16 for bosses, see Death performances) and a 4-frame
deploy (from 38, or 42 for bosses). Ranged units add a 7-frame `melee` clip after the deploy (from 42,
or 46 for bosses) to strike at point-blank; royal-cleave bosses reuse their attack instead. Attacks
use 20 shared weapon/creature motion profiles, with contact/release at local frame
4 (frame 3 of `melee`). The metadata includes `anchorX`, `motion.profile`, normalized `motion.body`
and `motion.contact` points, and `animations.attack.contactFrame`.

Attack contact and recovery follow the simulation clock. Projectile release,
body-height impacts, target-facing, recoil and directional hit marks are wired
into combat. These are gameplay-timing changes as well as visual changes; unit
damage values and catalog cooldowns were not rebalanced. Native sources remain
editable through `art/blender/animate_combat.py`; the existing surface finish is
preserved. See `CombatMotionReview.tscn` for isolated timing/pooled-unit checks.

Routine impacts now use a restrained, blended pose reaction instead of selecting
the larger authored Hit clip or physically nudging either combatant. Multiple
melee hits can land together while the defender keeps its own attack. Native Hit
frames remain available in the source/atlas. See
[`docs/COMBAT_HIT_REACTIONS.md`](docs/COMBAT_HIT_REACTIONS.md) for bounds and tests.

### Death performances

Every unit has its own death, defined in [`art/remaster/roster/deaths.py`](art/remaster/roster/deaths.py) and
solved in Blender by [`art/remaster/rk/death.py`](art/remaster/rk/death.py). There are 19 choreographies (falls
back and forward, kneel-and-topple, crumple, spin, stagger, sit-and-slump, last roar, rise-and-collapse for
casters, prayer, braced, rigid topple, dive-and-skid, burst, hound, horse with a thrown rider, machine wreck,
nest burst). Each unit's parameters, kit and timing make it distinct. The solver keys each frame so that:

- the deformed body rests on the ground, pivoting at the pelvis or a planted foot/knee, so nothing floats or skates;
- capes hang under gravity, drape over the back and legs, and bend at the hem instead of propping the body up;
  scabbards and quivers swing about their straps;
- weapons, shields, crowns, helmets and pustules come loose and fall with gravity, topple about their contact
  point and settle flat before the clip ends; a planted standard or sword stays standing;
- rigid skeletons and machine parts (skulls, hands, wheels, ballista arms, the plague barrel) break away and tumble;
- glowing eyes, soul cores, lanterns and forge embers go dark.

The death clip's metadata adds `impactFrame` (the body lands), `impactPoint` (where the torso lands) and `fx` (the
effect family). In game, `UnitDeathVisual` plays the clip, fires the thud, dust and (for heavy units and bosses) a
camera accent on the impact frame, drops the body beneath the living, lets it rest, then dissolves it in its
family's style (`DeathPresentation.cs`, `BattleDeathEffects.cs`). Lantern Caravan dead go out as warm lantern
motes; the Rotbound Host burns to ash, scatters as soul-light, or rots into gas, water, embers or glass.
Bosses rest longer and play the boss-death sting. Reduced motion keeps the clip but drops particles and the
dissolve; crowded fights shorten the rest.

Preview one unit's death without touching the shipped art:

```sh
blender --background --factory-startup --python-exit-code 1 --python art/remaster/build_units.py -- \
    --ids player_brawler --preview --clip death --no-portrait --samples 24
```

### Hi-res battle atlases

At the battle zoom (`ViewWidth` 600) one battle pixel covers several screen pixels, so units
render larger Blender masters and pack into atlases cropped to their animation envelope at
4.5 atlas pixels per battle pixel. Every unit but the War Hound, which already exceeds that in
the standard 192×240 frame, is packed this way. Bosses and the Siege Tower are drawn larger
still, so the 4096 px atlas limit caps them lower, at about 1.8–3.1. Frame size, column count,
`drawScale` and the normalised anchors differ per unit, and the atlases import with mipmaps.
[`art/remaster/density.py`](art/remaster/density.py) sets the target and `pack.py` picks each
unit's scale. `BlenderAssetSmoke` checks that every unit keeps at least 1.25 atlas pixels per
battle pixel.

### Model-inspection previews

Preparation and armory model viewers use sheets packed separately from the battle
sprites. All 52 characters have Idle, Walk, and Attack clips in
`assets/ui/models/{unit_id}.png` with matching metadata. Hi-res units (above) reuse
their cropped battle frames, which are sharper than a 256×320 master; the War Hound
uses its native 256×320 frames. `python3 art/remaster/pack.py stage --only units`
then `apply` refreshes them with the battle atlases. Textures are loaded per
preview and released when their viewer closes or changes character.
See [`docs/MOBILE_PRESENTATION.md`](docs/MOBILE_PRESENTATION.md) for zoom controls,
the mobile preparation layout, and review instructions.

### Health-bar artwork

Unit, boss and structure bars share editable vector art in `assets/ui/bars/health_frame.svg` and `health_fill.svg`. `HealthBarPainter` slices the frame so its beveled end caps retain their proportions; the fill is cropped to the actual health ratio. Unit frames are oxidized steel; bosses and bases use aged-gold trim. These are UI assets, so no Blender rebuild is needed to edit them.

Actual health changes immediately. A short amber trailing segment shows recent damage; reduced-motion mode removes that animation. High-contrast mode uses brighter frames, larger bars and cyan/orange team colors. `HealthBarReview.tscn` checks the presentation and captures normal/high-contrast battle previews with an isolated `--save-suffix=healthbar-review-<unique-id>`.

### Catalog coverage

- In game: open the debug console with `` ` `` and run `assets`
- Repo-side: `cd server && dotnet run -- --test-data ../data`

Both paths print the current asset coverage and the exact IDs still missing.

## Drop Locations

| Asset Type | Drop Location | Notes |
|-----------|---------------|-------|
| Unit sprite sheet | `assets/units/{unit_id}.png` | Matching `.json`; falls back to `{visual_class}.png` |
| Animated model preview | `assets/ui/models/{unit_id}.png` | Idle/Walk/Attack sheet and `.json`; falls back to the battle sheet |
| Zone battle backdrop | `assets/world/backdrops/{route_id}.json` + `{route_id}_far.png`, `{route_id}_mid.png`, `{route_id}.png` | Far, mid and near (road) parallax layers; the JSON gives each layer's world rect and parallax. See `assets/world/backdrops/README.md` |
| Fallback stage plate | `assets/world/battles/stage-{number:00}.png` | Painted plates for stages 1–60, used only when a zone has no backdrop |
| Painted home icons | `assets/ui/home/painted-icons-v2.png` | Transparent 3 × 3 atlas for tabs and stats |
| Home artwork prompts | `assets/ui/home/generated-art-v2.json` | Built-in image tool prompts and provenance for the icon atlas |
| World art prompts | `assets/world/manifest.json` | Prompts and provenance for the 60 stage plates; notes in `assets/world/README.md` |
| Fallback battle terrain | `assets/backgrounds/{terrain_id}.png` | Used when a stage has neither a zone backdrop nor a stage plate |
| Battle structures | `assets/structures/battle-v2/{structure_id}.png` + `.json` | Caravan, skins (with `_door.png` strips), gatehouse and `fort_*` outworks; see `assets/structures/battle-v2/README.md` |
| Structures | `assets/structures/{structure_id}.png` | `mount_*` weapon mounts; flat `war_wagon`, `gatehouse` and `war_wagon_skin_*` fallbacks |
| Particle texture | `assets/particles/{particle_id}.png` | Battle VFX sprite used by CPU particle bursts/trails |
| Projectile sprite | `assets/projectiles/{sprite_id}.png` + `projectiles.json` | Side-on lit shot (arrow, bolt, flask, pot…); the JSON gives each sprite's normalised tip and centre and its real length. Styles and flight live in `scripts/combat/ProjectileStyles.cs` |
| Unit icon | `assets/ui/icons/units/{unit_id}.png` | Optional fallback: `{visual_class}.png` |
| Spell icon | `assets/ui/icons/spells/{spell_id}.png` | Optional fallback: `{effect_type}.png` |
| Relic icon | `assets/ui/icons/relics/{relic_id}.png` | Armory/loadout card art |
| Reward icon | `assets/ui/icons/rewards/{reward_type}.png` | Currency/reward badge art used across reward screens, main-menu summary chips, and economy HUD strips |
| Meta icon | `assets/ui/icons/meta/{meta_id}.png` | Social, leaderboard, and challenge-status badge art |
| Codex icon | `assets/ui/icons/codex/{entry_id}.png` | Optional fallback if no portrait exists |
| Codex portrait | `assets/ui/portraits/codex/{entry_id}.png` | Detail-screen portrait or splash art |
| Music | `assets/music/{track_id}.ogg` | `.ogg`, `.mp3`, and `.wav` all load |
| SFX override | `assets/sfx/{cue_id}.ogg` | `.ogg`, `.mp3`, and `.wav` all load |

## Fallback Rules

- Missing unit sprites fall back to the procedural silhouettes already used in battle.
- Battles draw the zone backdrop, then the stage plate, then the terrain background, then a procedural ground.
- Missing structure images fall back to the current color-block/procedural presentation.
- Missing particle textures fall back to the existing built-in Godot particle quads.
- Missing unit/spell/relic/codex/reward images fall back to generated badges with initials, so the UI still stays readable.
- Missing music and SFX fall back to the procedural audio already shipped in the repo.
- You can replace assets incrementally. There is no requirement to finish a whole category in one pass.

## Sizes And Formats

- Unit sheets: PNG, authored facing right
- Unit metadata: JSON, see `assets/units/_example.json`
- Zone backdrops: three PNG layers in the proportions of their world rects; the near layer is at least `4096` px wide. Layout rules are in `assets/world/backdrops/README.md`.
- Fallback battle backgrounds: PNG, target `1280x720`
- Fallback stage plates: panoramic PNG, at least `1280x600`, drawn at their own proportions to cover the visible battle world
- Structures: PNG, authored against transparent background
- Particle textures: PNG with transparency, target `64x64` to `256x256`
- Unit/spell/relic icons: PNG, target `128x128`
- Reward icons: PNG, target `128x128`
- Codex portraits: PNG, target `512x512` or larger portrait crop
- Music/SFX: loopable `ogg` preferred, `mp3`/`wav` also supported

## Menu and map presentation

Menus use the shared modal materials in `assets/ui/modal/` and vector surfaces
in `assets/ui/frames/`. The completion-driven map uses the four texture atlases
in `assets/world/overworld/polished-v3/`.

## Fonts

Headings (screen and modal titles, section titles, card titles, battle result
titles and the boss banner) use Grenze Gotisch, the blackletter display face the
website also uses: `assets/fonts/GrenzeGotisch-Variable.ttf`, SIL Open Font
License 1.1 (`assets/fonts/GrenzeGotisch-OFL.txt`), from
[google/fonts](https://github.com/google/fonts/tree/main/ofl/grenzegotisch).
Body text, buttons, tabs and numbers keep the system text faces. Use
`RealmUi.Heading`, `RealmUi.SectionTitle` or `RealmUi.Display` rather than
setting the font directly: they size it up a tenth, trim its tall line box to
match the text faces, and switch headings with an all-caps word to the font's
roman stylistic set.

## Route IDs

- `city` = King's Road
- `harbor` = Saltwake Docks
- `foundry` = Emberforge March
- `quarantine` = Ashen Ward
- `thornwall` = Thornwall Pass
- `basilica` = Hollow Basilica
- `mire` = Mire of Saints
- `steppe` = Sunfall Steppe
- `gloamwood` = Gloamwood Verge
- `citadel` = Crownfall Citadel

## Terrain IDs

Current campaign coverage is 100 stages (ten per zone) across 31 terrain IDs; battles draw each zone's layered Blender backdrop (`assets/world/backdrops`).

- `city`: `highway`, `night`, `urban`
- `harbor`: `industrial`, `shipyard`, `swamp`
- `foundry`: `foundry`, `railyard`, `smelter`
- `quarantine`: `blacksite`, `checkpoint`, `decon`, `lab`
- `thornwall`: `pass`, `shrine`, `watchfort`
- `basilica`: `cathedral`, `ossuary`, `reliquary`
- `mire`: `chapel`, `ferry`, `marsh`
- `steppe`: `grassland`, `siegecamp`, `waystation`
- `gloamwood`: `grove`, `timberroad`, `witchcircle`
- `citadel`: `breachyard`, `bridgefort`, `innerkeep`

## Structure IDs

- `war_wagon`
- `gatehouse`

## Particle IDs

- `particle_soft`
- `particle_deploy`
- `particle_smoke`
- `particle_spark`
- `particle_fire`
- `particle_heal`
- `particle_frost`
- `particle_lightning`
- `particle_arcane`
- `particle_stone`
- `particle_trail`

## Unit Visual Classes

The current roster uses 23 unique `visual_class` IDs. A single sprite sheet can serve multiple units that share the same class.

- `banner`: Banner Knight
- `berserker`: Berserker
- `bloater`: Rot Hulk
- `boss`: Ashen Regent, Bone Pontiff, Dread Sovereign, Gloamwood Witch, Grave Lord, Harrow Tidemaster, Iron Warden, Mire Behemoth, Plague Archon, Plague Monarch, Reliquary Tyrant, Steppe Warlord, Thornwall Chieftain, Tidecaller
- `brute`: Grave Brute
- `crusher`: Bone Juggernaut, Catacomb Giant
- `fighter`: Halberdier, Spearman, Swordsman
- `gunner`: Alchemist, Archer, Crossbowman
- `hound`: War Hound
- `howler`: Dread Herald, Revenant Captain
- `jammer`: Hexer
- `mirror`: Mirror Knight
- `necromancer`: Lich, Necromancer
- `runner`: Ghoul, Tunneler
- `saboteur`: Sapper
- `shield`: Lantern Guard, Shield Knight, Shield Wall
- `siegetower`: Siege Tower
- `skirmisher`: Cavalry Rider, Rogue
- `sniper`: Ballista Crew, Mage
- `spitter`: Blight Caster, Bone Ballista, Plague Engine
- `splitter`: Bone Nest
- `support`: Battle Monk, Siege Engineer, Stormcaller
- `walker`: Risen

## Unit Sprite Metadata

If you add `assets/units/{unit_id}.json` (or the shared-class fallback), the loader reads:

- `frameWidth`
- `frameHeight`
- `drawScale` (sprite canvas scale relative to collision radius)
- `anchorX`, `anchorY` (normalized ground position within a frame)
- `healthBarY` (normalized standing silhouette height above the ground)
- `motion.profile`, `motion.body`, `motion.contact`
- `animations.idle`
- `animations.walk`
- `animations.attack`
- `animations.hit`
- `animations.death`
- `animations.deploy`
- `animations.melee` (optional; ranged units only)

If no metadata exists, the runtime uses the default row order above.

## UI Icon Slots

Unit icons can be authored either per unit or per shared class:

- `assets/ui/icons/units/{unit_id}.png`
- `assets/ui/icons/units/{visual_class}.png`

Spell icons can be authored either per spell or per shared effect type:

- `assets/ui/icons/spells/{spell_id}.png`
- `assets/ui/icons/spells/{effect_type}.png`

Relic icons are per relic:

- `assets/ui/icons/relics/{relic_id}.png`

Reward icons are per reward type:

- `assets/ui/icons/rewards/{reward_type}.png`

Current reward icon IDs:

- `gold`
- `food`
- `tomes`
- `essence`
- `sigils`
- `shards`
- `relic`
- `season_xp`
- `unit`
- `spell`

Meta icons are per social/competitive status type:

- `assets/ui/icons/meta/{meta_id}.png`

Current meta icon IDs:

- `arena_rating`
- `tower_floor`
- `endless_wave`
- `daily_streak`
- `guild`
- `friends`
- `challenge`
- `members`

Codex detail portraits are per codex entry:

- `assets/ui/portraits/codex/{entry_id}.png`

Optional codex icons can also be authored per entry:

- `assets/ui/icons/codex/{entry_id}.png`

## Music Track IDs

Scene tracks:

- `title`
- `campaign`
- `shop`
- `loadout`
- `endless_prep`
- `multiplayer`

Battle tracks:

- `battle`
- `battle_road`
- `battle_harbor`
- `battle_foundry`
- `battle_quarantine`
- `battle_pass`
- `battle_basilica`
- `battle_mire`
- `battle_steppe`
- `battle_gloamwood`
- `battle_citadel`

## SFX Cue IDs

Core interaction:

- `ui_hover`, `ui_confirm`, `scene_change`, `deploy`

Combat:

- `impact_light`, `impact_heavy`, `bus_hit`, `barricade_hit`, `repair`
- `hazard_warning`, `hazard_strike`, `spell_cast`
- `boss_spawn`, `boss_death`, `victory`, `defeat`

Progression:

- `upgrade_confirm`, `achievement_unlock`, `relic_pickup`

Ambience:

- `ambience_menu`, `ambience_battle`, `ambience_endless`, `ambience_multiplayer`, `ambience_shop`
- `ambience_route_road`, `ambience_route_harbor`, `ambience_route_foundry`, `ambience_route_quarantine`
- `ambience_route_thornwall`, `ambience_route_basilica`, `ambience_route_mire`, `ambience_route_steppe`
- `ambience_route_gloamwood`, `ambience_route_citadel`

## Recommended Handoff Order

1. `assets/units`
2. `assets/world/backdrops`
3. `assets/structures/battle-v2`
4. `assets/particles` and `assets/projectiles`
5. `assets/ui/modal` and `assets/ui/frames`
6. `assets/world/overworld/polished-v3`
7. `assets/music`
8. `assets/sfx`

That order covers the main campaign loop first: units, battle spaces, combat VFX, title/map/loadout/results, then audio polish.

## Shared UI material library

The editable UI textures are the 32 native SVG surfaces in `assets/ui/frames/`,
generated by `art/ui/build_surfaces.py`. They provide dark grained panels,
aged-brass button states, recessed inputs, tinted cards, meter enamel, scroll
thumbs, and selection/focus details. The shared theme applies them throughout the
menus and battle HUD while preserving existing content padding and mobile touch
sizes. See `docs/UI_MATERIALS.md` for regeneration and visual-review commands.

Battle deployment cards reuse the authored unit/spell PNG icons at a larger size,
with the new `cost_badge.svg` brass plate in the top-right. Transparent padding is
cropped only in the UI; the original artwork is unchanged.

## Painted home modal assets

`assets/ui/modal/materials-v1.png` is a 2 × 2 atlas containing walnut planks,
violet arcane cloth, golden parchment, and burnished copper. The 3 × 2
`illustrations-v1.png` atlas supplies warband, spell, forge, adventure, caravan,
and community vignettes. Both were generated with the built-in image tool;
the complete prompts and provenance are in `generated-art-v1.json` in the same
folder. `ModalArt` selects regions at runtime without altering the originals.

`ModalSurface` draws scalable silver bevels, rivets, coloured tabs and action
plates around these materials. Text and actions remain native controls. The
editable `slider-thumb-v1.svg` supplies the carved amber audio handle. Existing
animated units and spell icons remain the source of the actual roster previews.
