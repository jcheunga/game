# Restrained, shared hit reactions

Normal melee and projectile hits do not offset either combatant's simulation
position. Each attack still resolves its own damage at the authored contact
frame, with the existing range, lifetime and death guards. This lets multiple
fighters hit a shared target without earlier hits shoving it beyond later
swings. Real movement can still evade a swing. Explicit ability/hazard pushes
remain in `PushUnitsFromPoint`; their displacement is not removed.

`HitReactionMotion` groups impacts arriving within a fixed 70 ms window. It uses
their strength-weighted direction and the strongest amplitude, not the sum of
amplitudes. Equal opposite hits balance out. New impacts do not reset the current
pose or extend an open window. A critically damped response smoothly leans toward
the combined impulse, then returns to neutral; the normalized pose is capped at
one even under sustained fire or long presentation frames.

The rendered translation is capped at 1.1 world pixels and the rotation at
0.022 radians (about 1.3 degrees); normal hits are substantially below those
caps. Damage influences strength, heavy target classes resist the reaction, and
ranged impacts use a smaller weight. The authored Hit clip stays in the unit
atlases but is not played for routine damage. Idle, walking
and committed attack poses keep playing under the flinch. There is no hit stun
or attack cancellation added by this presentation layer.

Hit flashes and impact rings are short and small. Combat numbers keep a steady
on-screen size as the camera zoom changes (HUD-sized on phones), so they do not
grow over the models. View-only mode also suppresses the spell targeting preview.
Routine hits below 24 applied damage
do not shake the battlefield; heavier impacts use a capped 0.65-pixel melee or
0.35-pixel ranged accent. The existing brief movement slow remains. Reduced
motion suppresses the flinch, and pausing freezes it. Pool reuse clears all
blended reaction state.

This intentionally changes routine pushback, not damage values, cooldowns or
contact timing. Less displacement may affect how long crowds stay engaged;
automated correctness checks do not replace a campaign balance playthrough.

## Verification

Verified 2026-09-28: clean build; 482 motion/crowd checks (including capture),
122 combat regressions, 119 unit-asset checks and 146 mobile-presentation checks
passed. The regression driver retains its ObjectDB exit warning; the visible
motion and mobile runs reported no warnings. Physical-device and campaign
balance playthroughs are not covered by these checks.

Use unique isolated save suffixes, never the player's normal save:

```sh
dotnet build --no-restore
godot --headless --path . --scene res://scenes/tests/CombatMotionReview.tscn -- --save-suffix=motion-review-crowd-UNIQUE --mobile-preview
godot --headless --path . --scene res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-crowd-UNIQUE --regressions --mobile-preview
godot --headless --path . --scene res://scenes/tests/BlenderAssetSmoke.tscn -- --save-suffix=blender-review-crowd-UNIQUE --units-only
```

The motion review tests six attackers at the edge of melee reach, reversed
processing order, staggered contacts, opposing sides, exact damage totals, the
defender's ability to strike back, motion bounds, frame-rate consistency, pause,
reduced motion, pooling and preservation of explicit knockback.

Add `--capture-crowd` to a visible motion review to capture an arranged three-on-one
exchange at phone size. Frames are written to
`artifacts/mobile-review/crowd-hit-frames/`. This is a deterministic in-engine
demonstration using real attack/damage code, not a campaign balance recording.
The defender is held idle for two exchanges to isolate incoming reactions,
then counters during the third exchange.
