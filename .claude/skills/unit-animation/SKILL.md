---
name: unit-animation
description: Workflow for changing or reviewing unit/enemy animations (the 2D Blender puppets in art/remaster/puppet) - repainting sprites with Codex, placing joints, cutting layers, rendering, the objective motion check (smear, ground, foot slip, squat, crab walk), packing into the game and proving the result. Use for any work on how units walk, attack, cast, die or stand.
---

# Unit animation: repaint, rig, render, measure, ship

Every unit is a 2D cutout puppet: a Codex painting cut into layers (body, arms, legs, weapon, off-hand), skinned to
a 2D skeleton in Blender and posed per clip by `art/remaster/puppet/build.py`. Background and history:
`art/remaster/README.md` ("2D puppets", "Walking puppets", "Motion check").

**Never sign off by eye alone.** Contact sheets hide tears and crouches. The motion check measures every frame, and
the user judged the result by the numbers plus side-by-side stills. Change → measure → look → render → measure
again → pack → test.

All Python tools run through `art/remaster/puppet/run.sh` (uv with numpy, scipy, pillow, opencv). Blender is
`/opt/homebrew/bin/blender --background --factory-startup --python-exit-code 1 --python <script> -- <args>`.
Blender's Python has numpy only: no PIL, no cv2.

## Where things live

| | |
|---|---|
| `artifacts/remaster/puppet_stride/<id>/` | people (93): `master.png`, Codex edits `body.png` `limbs.png` `held.png`, `landmarks.json`, cut layers, `layers_preview.png` |
| `artifacts/remaster/puppet/<id>/` | beasts, mounts, machines, blobs (18), and the people's old front-facing masters |
| `artifacts/remaster/puppet_units/<id>/` | build.py renders (`NNN.png`, `metadata.json`) |
| `assets/units/`, `assets/ui/models/` | what pack.py writes (committed; artifacts are not). The game ships `assets/units`; `assets/ui/models` only feeds `art/royal/figures.py` (rerun it after repacking) and is left out of exports |
| `art/remaster/puppet/review/` | the review tools below |

## The loop

1. **See it.** Whole roster, one clip at a time, ~14 units per sheet:
   `run.sh review/sheet.py walk /tmp/w.png all`. Zoom on anything odd:
   `run.sh review/frames.py /tmp/f.png enemy_shade:attack:0,.15,.27 --scale 2`. Use `--battle` to see it at true
   phone size.
2. **Measure it.** `review/check.sh all` runs `motion_check.py` and `check_report.py` and prints every failing unit
   and clip. It measures the current code and sources, not the renders, so it can test a change before rendering.
   For a quick look at a few units, use `check.sh a,b --out /tmp/mc`.
3. **Find the cause, not a symptom.**
   - `review/smear_overlay.py <id> <clip> <frame> out.png` paints where the smear is and names the two bones that
     tear it.
   - `review/rig_probe.py` (Blender) prints the posed rig at chosen frames: leg extension, knee angles, bone world
     angles. Hidden caps live in the code; the squat was a 99.2% `reachable()` slack lifting every heel.
   - Make a fix measurable first: put the value behind an env knob, compare 2–3 values with `check.sh --out`, look
     at both renders side by side, then hard-code the winner with a comment saying why. Take the knob out.
4. **Render.** `review/render.sh all` (about 30 min at `-j 3`). `--clip death` re-renders just one clip in place.
   `--out /tmp/try` renders beside the shipped version.
5. **Sign off.** `review/check.sh all` must print `MOTION_CHECK: 0 of 111 units fail`. Then look at a sheet per clip.
6. **Ship.**
   - Pack and apply:
     `python3 art/remaster/pack.py stage --only units --source puppet_units --ids <ids>`, then
     `python3 art/remaster/pack.py apply --only units --ids <ids>`.
   - Build, import and run the asset smoke test:
     ```
     export PATH="/usr/local/share/dotnet:$PATH"
     dotnet build Game.csproj
     godot --headless --path . --import
     godot --headless --path . scenes/tests/BlenderAssetSmoke.tscn -- --save-suffix=blender-review-final
     ```
     It checks the cadence of every walk.
   - Run the combat regressions:
     `godot --path . scenes/tests/CombatReviewSmoke.tscn -- --regressions --save-suffix=combat-review-final`.
     Both must report 0 failures.
   - Capture the game:
     `godot --path . --fixed-fps 30 scenes/tests/CombatMotionReview.tscn -- --save-suffix=motion-review-feel --feel-only --take=NAME --pairs=player_x:enemy_y,... [--zoom=1.6]`.
     Frames land in `artifacts/combat-feel/NAME/`. The built-in `--take=deaths` take uses fragile units, so you can
     see deaths happen.
7. **Prove it to the user.**
   - `review/stills.py OUTDIR --rev <commit before> id:clip:fracs ...` makes before/after stills.
   - `art/remaster/puppet/compare_reel.py ids --rev <rev>` makes before/after videos.
   - Put the numbers and stills on the review page (artifact "Crownroad Unit Walks"), and ask the user to judge
     the look.

## Pass criteria (check_report.py)

- **Smear.** Paint showing from mesh faces stretched past 3x their painted size. A unit fails above 3% of the figure
  if that covers more than 16 battle px², or above 100 battle px² on any unit.
- **Ground.** Living clips keep the body within 3% of the figure of the road. In deaths, cloth may sink at most 4.5%
  and a limp arm under the body at most 11%. Grounding uses visible paint only (`build.VISIBLE`).
- **Slip.** A planted foot moves with the ground: mean error at most 0.2 of the ground's motion per frame.
- **Gait (people).**
  - Standing and passing-leg knee bend at most 25°; about 5–8° of the reading is the knee mark on the kneecap.
  - Hips sink at most 3% of the figure below standing.
  - The feet pass each other by at least 5% both ways; feet that never cross is a crab walk.
  - Prowl and jog gaits are exempt.

Do not loosen a criterion to pass a unit. Find out why it fails; if the measurement is wrong, fix the measurement
and say so.

## New sprites (Codex)

`art/remaster/puppet/stride.py` repaints people side-on:
`python3 art/remaster/puppet/stride.py <ids> --stages master,landmarks,body,limbs,held [--force master]`.

- **Spell out the pose.** The master prompt states the limb count and asks for open elbows. When a unit's carry
  still comes back wrong (an arm folded with its fist against its own shoulder), give that unit its pose in
  `CARRY_UNIT`.
- **Check the repaint against the original.** `run.sh review/masters_sheet.py /tmp/m.png <ids>`. One repaint gave
  the Drowned Colossus four arms.
- **Joints by eye, always.** View them with `run.sh review/landmarks_view.py <id> /tmp/l.png [--legs]`, which shows
  true coordinates. Codex errors to correct:
  - knees on the boot cuffs;
  - hips at the hem;
  - a staff's tip on its foot;
  - a belt lantern marked as the off-hand item.

  Set them with `python3 art/remaster/puppet/setlm.py <dir> knee_R=x,y ...`, and use `--anatomy` to solve the hips
  from the knees. Wide straddles need hips by eye.
- **Other landmarks:**
  - `want_held=true` asks for `held.png` (the weapon painted alone) when the cut weapon is incomplete
    (`weapon_whole` < 0.9).
  - `arm_cloth=RL` keeps crimson tatters on the body.
  - `offhand_item=null` stops a carried item being cut as held.
  - `weapon_butt` / `weapon_end` give an exact two-point weapon fit.
- **Cut and inspect.** Re-cut with `review/recut.sh <ids>` and look at `layers_preview.png`.
- **Codex limits.** About 40 images per 5 hours, plus a daily image cap. HTTP 429 "exceeded retry limit" is burst
  throttling and has cleared in 15 minutes to a few hours. Retry in a loop with 10-minute sleeps rather than
  hammering. Check usage in `~/.codex/sessions/**/*.jsonl` (`rate_limits`). Kill Codex calls hung for more than
  30 minutes.

## Rules learned the hard way

- **Death code.** Death poses aim bones before the topple (`place(rot=...)` comes last). Anything relative to the
  ground goes in `lie_on_ground()`, which runs after it.
- **Skinning.**
  - Weights are a log-domain softmax; far paint follows its nearest bone.
  - Arm layers gate the chest to the shoulder's end, with soft weights (`sharp` 1.5).
  - Beasts use `sharp` 1.0. Crisper weights always measured worse.
- **Capes.** A cape follows the hips' turn (`cape_hang`). Face down it is pressed flat, never swung.
- **Folded arms.** `open_reach` (0.25 past the painted reach), `limit_elbows` (40°) and `two_high` (80°) respect
  arms painted folded.
- **Legs.** Standing legs reach 99.3% of their length, and the passing and landing legs 99.9%. Walking strides are
  at most 0.85–1.0 of the leg, the planted foot shifted back by `STEP_BACK` (0.15 of the step) so it lands only a
  little ahead of the hips. Centred under the hips, strides over 0.75–0.81 dipped the hips. Changing any of these
  changes the squat: re-run the gait check.
- **Cadence.** Troops walk a 1.28 s cycle, about 1.6 steps a second; until October 29 it was 0.95 s, 2.1 steps a
  second, which the user found far too busy. Don't shorten the cycles, and keep each walk frame near 0.09 s
  (14 frames).
- **Re-render after any speed change.** The walk is sized to `data/units.json` speeds, and the cadence check in
  `BlenderAssetSmoke` catches a mismatch.
- **Environment.**
  - The Mac sleeping (lid closed) stalls renders for hours; keep it awake.
  - zsh aborts a whole command on an unmatched glob (`rm -f x/*.bak`).
  - In a zsh script, `$(jobs -r | wc -l)` is always 0 because a subshell sees no jobs. Throttle by counting
    `$jobstates` inline, as `render.sh` does. On Oct 29 the old throttle started all 111 Blenders (about 1.6 GB
    each) and the Mac rebooted. After starting a render, check `pgrep -f 'Blender.*build.py' | wc -l` equals `-j`.
  - `(a & b).sum()` on 255-valued masks sums 255s. Use `((a & b) > 0).sum()`.
- **Commits.** Commit only your files: art/remaster sources, `assets/units`, `assets/ui/models`. Other sessions
  leave unrelated changes in the tree (for example `export_presets.cfg`). "Push" means commit and push main
  directly.
