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
| Camera | 15° from side | The battle camera's own angle (22° above the ground plane), with each character turned toward it (45° by default, less for mounts and engines) so faces, heraldry and shields read, and deaths fall onto the same ground plane as the battlefield |

## Roster-sheet style (October 2026)

Every unit and enemy is rebuilt to match the stylised "royal fantasy" roster sheet (`output/units.png`;
crops of its 16 cells live in `ref/<id>.png`). The new recipes are `roster/caravan.py` (Lantern Caravan),
`host.py`, `host2.py`, `host3.py` (creatures, giants, the siege tower), `host4.py` (bosses), `mounts.py` and
`siege.py`. They register after the older recipe modules, so they win.

| Piece | What it does |
| --- | --- |
| `rk/sdf.py` | Signed-distance sculpting: primitives, smooth union/cut/keep, narrow-band surface nets, projection and smoothing, bone weights from tagged primitives, posed fields |
| `rk/hero.py`, `faces.py`, `hands.py`, `bones.py` | The hero body, sculpted heads (beards, hair, brows, lids), posed hands, skeletons and skulls |
| `rk/garb.py`, `plate.py`, `helms.py`, `arms.py`, `sigil.py` | Garments (dressed in the stance, then carried back to rest by inverse dual-quaternion skinning), plate harness, helmets, weapons and heraldry |
| `rk/pose.py`, `motion.py`, `skin.py` | Two-bone IK for grips and planted feet; idle, walk, attack (weapon-space keys), hit, deploy and melee clips |
| `rk/steed.py`, `engines.py`, `creatures.py` | Horses, hounds and tack; ballista and catapult; blobs, serpents, wisps |
| `rk/character.py` | Assembly, binding, lighting, the battle render and `render_icon` (the unit icon: an orthographic bust for people, the whole figure for beasts, mounts and machines) |

Most units face 45° toward the camera; long-bodied ones turn less (`character.set_facing`, called by
the mount, engine and siege-tower recipes, and reset before every recipe). Engines are modelled at crew
scale and grown after binding (`finish_clips(..., scale=)`), so the sheet's big engines stay big.

Lookdev and production:

```sh
# one unit's stance at battle framing (+ its icon), compared with its roster-sheet cell
blender --background --factory-startup --python art/remaster/lookdev.py -- --ids enemy_walker --samples 24 --size 600 --tag v5 --portrait
python3 art/remaster/lookdev_compare.py enemy_walker --tag v5
# every unit, a few per Blender process: lookdev, or the production masters
LOG=artifacts/remaster/lookdev/all.log art/remaster/lookdev_all.sh ids.txt v5
LOG=artifacts/remaster/build_a.log art/remaster/build_all.sh ids.txt
```

`look_stats.py` and `relight.py` calibrate the look (tone, exposure, light balance) against the sheet.

### Paint-over (the sheet's finish on every frame)

The sculpted models carry the shapes, rigs and animation; the roster sheet's painted finish comes from a paint-over.
Each unit's stance frame is repainted by Codex image generation in the sheet's style (same pose and silhouette,
richer textures), aligned back onto the model's silhouette and projected from the battle camera onto the rigged
mesh (`rk/paint.py`), so every frame of every clip carries the painting. Surfaces the camera cannot see in the
stance keep their procedural materials. Icons are cropped from the same painting.

Each unit has two paintings: the stance as the battle camera sees it, and the stance seen from behind (rendered by
turning the character 180 degrees under the same camera and lights, painted by Codex to match the front painting).
In death poses the camera sees about half the body's surface from angles the front view never saw; the back
painting covers most of it. What neither view sees in the stance (inside the legs, under shields and arms) borrows
the nearest painted colour, slightly darker; `coverage.json` records the split per unit.

```sh
art/remaster/guide_all.sh ids.txt                       # stance guides only (build_units.py --guide)
EXTRA="--back-guide" art/remaster/build_all.sh ids.txt  # back guides (units/<id>/back000.png)
python3 art/remaster/paintover.py paint-back all        # Codex: back_painted.png, matched to the front painting
python3 art/remaster/paintover.py paint all --jobs 3    # Codex: artifacts/remaster/paint/<id>/painted.png
python3 art/remaster/paintover.py sheet all             # guide | painting review sheet
EXTRA="--paint" SAMPLES=32 art/remaster/build_all.sh ids.txt   # painted masters (all clips)
python3 art/remaster/paintover.py icons all             # units/<id>/portrait.png from the painting
python3 art/remaster/tone.py apply all                  # the sheet's tone on every frame and icon (once per render)
python3 art/remaster/look_stats.py ref --frames         # measured match to the 16 reference cells
python3 art/remaster/qa_frames.py all && python3 art/remaster/unit_review.py <ids>
python3 art/remaster/paint_drift.py all                 # how far each painting strayed from its model's colours
# in the engine: the art contract for every unit, then every unit on a real battlefield, 8 per screenshot
godot --path . scenes/tests/BlenderAssetSmoke.tscn -- --save-suffix=blender-review-x --screenshots --lineups
```

Codex runs as `codex exec -m gpt-5.6-sol` (the default model in `~/.codex/config.toml` is not available on a
ChatGPT account) and its image generation has a usage limit: a full roster of 111 paintings fits, a second full pass
does not. Units that turn little toward the camera (mounts, engines, serpents) call `character.set_facing`; long
low bodies need it or their near end draws below the ground anchor (BlenderAssetSmoke checks `AnchorY > 0.5`).

`tone.py` holds one curve (`tone_curve.json`: gamma 0.65, contrast 1.55, saturation 0.7) fitted so the reference
units' stance frames match the sheet's mean luminance and contrast; the paintings keep their models' darker values,
so without it the roster sits about 29 luminance levels under the sheet. A second step warms bright, low-colour areas
(linen, bone, pale cloth) toward the sheet's cream; strongly coloured glows are masked out and keep their brightness.
Compressing highlights to the sheet's level instead was tried and rejected: it greys the whites and costs contrast. A fresh render clears `tone.json`, so the
new frames are toned again; icons made after toning get the same curve.

After changing a model, re-render its guide and repaint it (`paintover.py prep <id>` keeps the first guide unless
forced; delete `artifacts/remaster/paint/<id>/` to start over), or the painting no longer fits the mesh.

### 2D puppets (the sheet's art, cut out and animated)

The paint-over keeps each sculpted model's silhouette and proportions, so units still read as the old models with
new texture. The units now ship as 2D cutout puppets instead (`puppet/`): every unit is painted afresh by Codex in
the sheet's style, with its proportions, cut into layers, rigged with a 2D skeleton and animated in Blender.

| Step | What it does |
| --- | --- |
| `puppet/masters.py` | Per unit, resumable: `master.png` (Codex: the unit by the sheet's artist, full body, facing right, on transparency; sheet units keep their sheet cell's design, the rest take only *what* they are from `paint/<id>/painted.png`), `landmarks.json` (`puppet/landmarks.py`: Codex vision on a gridded copy; a master painted facing left is mirrored), then Codex edits: `body.png` (what the hands hold painted out) and `torso.png` (both arms painted out), `horse.png` (a mount's rider painted out) or `nowheels.png` (a machine's wheels painted out); `layers`; `icon`. A usage-limit message makes it sleep until the time Codex gives |
| `puppet/layers.py` | Registers each edit onto the master (SIFT + RANSAC similarity; the arms edit rescales) and cuts what it removed into layers: weapon and off-hand item (a straight weapon is a band along the line through the grip with the most removed paint, as far as that paint runs, so a haft crossing a belt of its own colour is kept and a shield behind the pommel is not; the shaft is painted in under the fists), each arm, the rider, each wheel; the base is the body with the edit's paint where the arms were. `layers.json` holds the draw order and the weapon line and grips. Runs under `puppet/run.sh` (uv: numpy, scipy, OpenCV) |
| `puppet/build.py` | Blender: one grid mesh per layer (its texture is the layer), skinned only to its own bones; hands are IK targets (a `wield` bone carries the weapon, between the hands for two-handed weapons), so attacks say where the hand goes around the shoulder and which way the weapon points (`HAND_PROFILES`), whatever the painted pose. Clip lengths, timings, contact and impact frames come from the unit's `units/<id>/metadata.json`; deaths follow `roster/deaths.py` (motion, and when the weapon leaves the hand, its toss and spin: it is thrown, lands, tips flat and skids) |
| `puppet/creatures.py` | Rigs and motion for four-legged beasts (trot, crouch-and-pounce, sprawl), serpents (travelling wave, rear and strike), blobs and floaters (squash and stretch, hover), mounts (canter, couched-lance charge, the mount pitching onto its chest) and machines (wheels that turn as it rolls, draw-and-loose recoil, a lurching wreck) |

```sh
python3 art/remaster/puppet/masters.py all --jobs 3                 # sources (resumes; waits out Codex limits)
art/remaster/puppet/follow.sh                                     # or: later stages as masters land
blender --background --factory-startup --python-exit-code 1 --python art/remaster/puppet/build.py -- \
    --ids $(python3 art/remaster/puppet/ready.py)                   # frames, metadata, complete.json
python3 art/remaster/pack.py stage --source puppet_units && python3 art/remaster/pack.py apply
```

Frames render at the density `pack.py` packs to (`density.TARGET_DENSITY`, supersampled), under a camera wide
enough for the body lying either way from its feet; `drawScale` keeps each unit's on-screen height. The paintings
carry the sheet's lighting; the battle adds each unit's cast and contact shadows from its silhouette
(`BattleLighting`). Icons (`puppet_units/<id>/portrait.png`) come from the master: a head-and-shoulders crop on the
head landmarks for people, the whole figure for beasts, mounts and machines.

### Walking puppets (people repainted side-on)

The first puppets were painted in the roster sheet's lineup pose: three-quarter front, feet straddled, knees bent
out. A body that faces the viewer while it travels sideways crab-walks, and a straddle animated into a stride
squats, whatever the legs do. So every person (not the beasts, mounts, machines, serpents or blobs) is repainted
from its master by `puppet/stride.py` into `artifacts/remaster/puppet_stride/<id>/`: the same character turned to
face its walk in three-quarter profile, standing tall with the legs under the hips mid-step, carrying its weapon the
way its attack profile needs (`CARRY`). `build.py` uses these sources wherever `layers.json` exists there.

| Step | What it does |
| --- | --- |
| `master.png` | Codex: image 2 (the old master) repainted in the walking pose, image 1 (the sheet) for style only |
| joints | Codex places them, then they are checked by eye on a gridded copy: knees on the knee cap or cop (Codex puts them on the boot cuffs), then `puppet/setlm.py dir knee_R=x,y ... --anatomy` solves the hips (thigh as long as the shin, under the pelvis, 0.33-0.5 of the height above the ground). A hip at the hem makes a leg that pivots at the knee guard: it walks squatting |
| `body.png` | Codex: what the hands hold painted out |
| `limbs.png` | Codex: arms and legs painted out in one edit (older units: `torso.png` then `hips.png`) |
| `held.png` | Codex: the weapon alone, painted whole where the hands covered it (the weapon layer; a bow's arrow is cut from it) |
| `layers.py --legs` | the near leg as a layer (the far leg is a shaded copy moved onto its hip, so neither is ever half hidden), a skirt overlay (what covered the near thigh: tabards draw over it, while it passes in front of a cape), robes keep both legs behind |

`stride.py ... --follow` keeps Codex busy: a unit's edits as soon as its joints are placed, otherwise the next master.
Codex's 5-hour window allows about 40 images; landmarks by Codex cost as much as an image, so they are placed by eye.

In `build.py` the split legs change every clip: the walk is a side-on stride (heel strike, roll, toe-off, a swing
past the other leg, the knee bending forward by IK), sized to the unit's own battle speed so the cycle keeps a natural
rhythm (`GAITS`: about 0.7-1 s a cycle for troops, slow heavy steps for bosses; faster than `JOG_ABOVE` legs a second
there is no natural walk and the unit jogs, with a moment in the air). Idle, attacks, hits and casts move the hips
over planted feet (a lunge steps the leading foot), standing in a compact guard; deploy lands on bent knees; archers
raise the bow, draw the arrow to the cheek and loose it. `WEAPON_MOTION` overrides a profile where the new painting
carries its weapon differently (a one-handed spear thrusts; a mace or axe held level is still swung, so this is set by
eye). Deaths on split legs: face down, the feet point back along the legs; the legs set the ground contact (cloth and
limp arms may sink a little rather than prop a body up); a cape settles over the back, or flattens under a body on its
back. The swamp maw and bog spore stand on legs, so they are rigged as squat four-legged beasts (`stubby` in their
landmarks: they lunge and slump onto their bellies rather than fling their legs).

Off-hand items (shields, lanterns, a flame in the palm) keep the angle to the torso they were painted at in every
living clip, so a shield stays upright through casts and swings. In deaths the arms come to rest across the body
(aimed from the shoulder at the hips: a side-on shoulder sits on the back's edge), a shield keeps its angle to the
torso and so lies flat as the body does, the legs settle nearly straight, and a dropped weapon lies on its own long
axis (a bow's bone runs along its arrow). One-handed blows drive the shoulder through the target; a polearm's chop
stops level. Units still on the old paintings get a stride sized to the game's frame-time limit at their speed, and
machines roll their wheels in each wheel's own plane (the painted ellipse squashed to a circle, turned, stretched
back) by whole spoke steps per cycle, matched to the ground (`creatures.spoke_symmetry`, wheel layers cut to the
near wheel's rim ellipse in `layers.rim_ellipse`).

Bosses walk slowest of all, so they take long, unhurried strides: a cycle of about 3 s over 24 walk frames (a short
cycle at their speed is a shuffle of tiny steps). Hand flames (a spell burning in the palm) are cut from the arm into
the off-hand layer, which moves rigidly on its own bone, and go out when the caster dies; lanterns, vials and drums
fall with the body (`offhand_kind` in layers.json, `offhand_flame` in landmarks to override). Winged units (`wings`
in landmarks) fold their wings into the back as they fall. The walk is sized to `data/units.json` speeds: after the
shortened field scaled every speed by 580/1060 (2026-10-18) every unit was rebuilt, since a cycle sized to the old
speed plays in slow motion.

`pack.py` keeps the walk's `stride` in the packed frame's widths (it is cropped narrower than the render, so left in
render widths every walk played too fast and the feet slid); `BlenderAssetSmoke` checks every walking puppet's cycle
time at its battle speed.

A frame-by-frame audit of all 111 units (walk, attack and death) found and fixed a last set of faults.
- **Skin weights.** Paint several radii from every bone (claw tips, tatters, spell wisps) underflowed to zero
  weight for all of them, and was then shared evenly by every bone. As the limbs swung apart it stretched into
  lines and ribbons. Weights are now a log-domain softmax, so that paint follows its nearest bone. In arm layers,
  the chest bone only holds the shoulder's end, so a hand resting on the torso swings with its arm.
- **Repaint check.** One repaint gave the Drowned Colossus four arms. The master prompt now states the limb count,
  and every master was checked against its original.
- **Shields and held weapons.** When a weapon painted alone replaces one held across a shield, the shield's band
  is mended along its own long axis (`mend_shield`).
- **Arm layer clean-up.**
  - Crimson tatters swept into a bare arm go back to the body (`arm_cloth` in landmarks).
  - An arm never takes the head.
  - A green or blue spell's faint halo goes with its flame, so it no longer hangs over the corpse.
- **Deaths.** Death poses aim the limbs in the body's own upright frame, and the topple is applied last. A pass
  after the topple (`lie_on_ground`) lays the legs level or sloping down to the ground, never more than a few
  degrees up, and a limp arm no more than about 12° below level. Grounding uses visible paint only (`VISIBLE`):
  the legs set contact, cloth may sink at most 2.5% of the figure into the road and a limp arm under the body 10%.
  On its back the cape is turned under the body and flattened; face down it is pressed flat onto the back (swung
  to lie along the back, it tore). On its back a dead foot falls slack.

#### Motion check (objective pass criteria)

`puppet/motion_check.py` poses every frame of every clip exactly as a render would, but measures it instead (9,000+
frames for the roster in about ten minutes). `puppet/check_report.py` applies the pass criteria.
- **Smear.** The deformed layers are splatted back to front onto a grid, and the check counts the painted cells
  that show from mesh faces stretched past 3x their painted size: tears, ribbons and fans. A unit fails above 3% of
  the figure when that covers more than 16 battle px², or above 100 battle px² on any unit.
- **Ground.** In living clips the body's lowest paint stays within 3% of the figure of the road. Deaths have the
  cloth and arm limits above.
- **Slip.** A planted foot keeps pace with the ground, to within 20% of its motion per frame.

The check also measures the gait itself, aimed at the original complaint that units looked squatting and
crab-walking. For people walking it records:
- **Knee bend** of the standing pose and of the supporting leg as the body passes over it.
- **Hip sink** below the standing height.
- **Leg crossing:** how far the feet pass each other.

It found every unit standing with its knees bent about 35°, and the supporting leg never straighter than about 30°.
The rig had held standing legs to 95.5% of their length, the passing leg to 97.5%, and the landing leg to 99%, then
sank the hips a further 1.2% of the leg: a permanent half crouch. Now:
- **Legs.** Standing legs reach 99.3% of their length, and the passing and landing legs 99.9%, with no extra sink.
- **Strides.** Walking strides are at most 0.68 to 0.81 of the leg. Longer, the hips had to dip to land the heel.

One more cap was hiding behind those: `reachable` treated any leg past 99.2% of its length as out of reach. It
lifted the heel in 2° steps, which bent the knee back to about 97.5%, so the straighter targets never took effect.
Its slack now matches the passing extension. Knees now measure about 20° standing (roughly 5–8° of that is the knee
landmark sitting on the kneecap), and the supporting leg passes nearly straight. Every walk must:
- keep both standing and passing knees under 25°;
- sink its hips no more than 3% of the figure below standing;
- pass its feet at least 5% of the figure both ways.

Prowl and jog gaits run low by design.

`by_pair` in the output names the two bones that tear. It led to these changes:
- **Arms.** The chest holds only the shoulder's end of an arm layer, and arms take soft weights (`sharp` 1.5).
- **Capes.** A cape keeps the hips' turn and pivots at its hinge (`cape_hang`), with half the walk's sway and a
  wide blend into the body.
- **Beasts and mounts.** Soft weights, wider tail bones, a death splay shared between shoulder and knee, and a
  ground step for living clips (no planted feet).
- **Machines.** The arm drops only 10° on the frame.
- **Wings.** They fold to 40%.

Wider changes tried and dropped because they measured worse:
- crisper weights;
- distance measured through the paint;
- per-bone spans along the arm chain;
- a hand gate;
- swinging face-down capes.

Two units were painted with an arm folded, the fist against its own shoulder: the Reliquary's staff arm and the
Furnace Knight's far arm. The paint between that fist and shoulder does not exist, so straightening the arm smeared
it. A general repaint prompt asking for open elbows came back folded again. Both are now repainted with the pose
spelled out (`CARRY_UNIT` in `stride.py`):
- **Reliquary:** the staff arm hangs at full length.
- **Furnace Knight:** the hammer is carried low in both hands, with both arms long.

Their worst frames measure 15 and 0 px². The rig also respects any arm that is still painted folded:
- **Reach.** A hand reaches at most 0.25 of the arm's length past its painted reach (`open_reach`).
- **Elbows in deaths.** A dead elbow opens at most 40° from its painted bend (`limit_elbows`).
- **Two-handed swings.** With either arm folded, the swing raises the haft to 80° rather than 110° (`two_high`).

### Combat feel

The puppet clips (`build.py new_contract`) are sized for a phone: idle 8 frames, walk 10, attack 16 with contact on
6 at the same contact time as before, melee 12, hit 3, deploy 6, death 20 (24 for the big), and a 12-frame cast whose
frames from 6 on are held (`holdFrom`) and looped while the unit channels. Segments are eased (windups decelerate and
hold a beat, strikes accelerate into contact, follow-throughs settle) with a motion-blur trail of the weapon alone on
the frames around contact. Deaths add a jolt from the killing blow, a fall that accelerates into the ground, one bounce
and a settle (`dramatize_death`); the weapon is thrown with the table's toss and spin and lies where it lands.

The walk is driven by the feet, not swung from the hips: each foot follows its own path (planted and sliding back
for 60% of the cycle, lifted and carried forward for the rest) in its own lane of the 3/4 view, so the legs never
cross, and planar two-bone IK bends the knee forward to reach it. The hips ride at the height the legs allow (nearly
straight at passing, lower at double support), which gives the bob. Four-legged beasts and mounts trot on diagonal
pairs with the same paw IK (`creatures.QuadAnim.step_legs`). The walk clip records `stride` (ground covered per
cycle), and the game plays the cycle at the speed that keeps the planted foot still. Casts lift a staff high, or
raise a weapon overhead; beasts rear and howl, blobs swell, engines crank back.

Size: atlases are rendered at `density.PUPPET_DENSITY` (3 pixels per battle pixel; 2 for the siege tower),
trimmed per frame and shelf-packed on as many 4096 px pages as needed (`pack.py trimmed_pack`).

In the battle: `UnitLook` lights each unit with its zone's colours (key, ambient, sky rim, road bounce, depth haze,
measured from the painted backdrop) and gives a true white hit flash; blows freeze both fighters for a few frames
(hit-stop) and shake the camera's offset (the old shake moved the cameras with the world, so nothing shook);
`BattleGore` sprays and stains light blood coloured by what the creature is made of (painted sprites from
`fx_cut.py`), chips bone and wood, and pools blood under the fallen, who are knocked back along the killing blow;
arrows stay in a target while it fights and stick in the road when they miss. A kill throws a heavier spray, a gush
of droplets and spatter ahead of the body, and the body leaves a smear where it slides.

Abilities (`BattleController.Content.cs`, `AbilityVfx.cs`, `BattleCastFx.cs`): a caster plays its cast clip over a
ground sigil of its school that fills as the cast charges, with light gathering at its chest; the effect then lands
as a real shot, bolt, chain, heal (with the amount), charge run or mark. Passive abilities show on the blows that
carry them: slash arcs when a cleave carries through, sparks for parries and set pikes, a chain for hooks, plate
shards for armour breaks, a streak for piercing hits. Statuses show on the body: `Unit.StatusVisuals` paints flames,
brambles or chains, the shield ring, stars and the silence rune on the sprite, and `UnitStatusFx` adds the light and
motion (a burning glow and rising flame, drifting z's for sleep, embers for battle frenzy); the text chips are left
for what has no mark (armour broken, wounded, marked, casting, hidden), or for everything in high contrast.

Raise Dead (`BattleController.RaiseDead.cs`): the fallen of both armies are remembered for 14 s. The Gloamwood Witch
(and the Lich and the other necromancers) sends a soul-wisp to each body near her, and it gets back up, its death
played backwards and lit grave-green, as an undead of the Host: Caravan soldiers come back at 60% health and 80%
damage. With no bodies near, the dead claw up out of the ground. Bosses, engines and the already raised stay down.

Review it on a real battlefield (takes: melee, ranged, beasts, deaths, abilities, witch):

```sh
godot --path . --fixed-fps 30 scenes/tests/CombatMotionReview.tscn -- --save-suffix=motion-review-feel --feel-only [--zoom=2.2] [--take=deaths]
godot --path . scenes/tests/CombatMotionReview.tscn -- --save-suffix=motion-review-feel --feel-only --projectiles
```

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
`_Facing` empty yawed toward the camera (`character.FACING_YAW`, -45° unless the recipe called
`set_facing`), so recipes never rotate characters themselves. Riders and crews
must be bound (`finalize_bones` + `bind`) before their rig is moved onto a mount, or their meshes
stay behind at the build position. Pose rotations use the `LAT` axis (counter-clockwise as seen by
the camera), `YAW` and `ROLL` from `rk/rig.py`.
