# Clickable courage packs

Defeating an authored enemy has a 10% chance to leave a physical courage pack.
Enemies are the only source of packs. Courage from Supply Crates, Quartermasters,
the Umbral Coin and Caravan Token relics, field objectives and the Ash Pilgrim
goes straight into the meter (see "Direct grants" below).
Click or tap a pack to gain up to 20 courage. Holding the pointer does not collect
until release, and dragging away cancels collection. Uncollected packs expire
after 8 seconds of active battle time, fading during the final 2 seconds. There
is no countdown ring; the fade is the only expiry warning.

Pause, endless checkpoints, and multiplayer start barriers stop the lifetime.
At full courage, a pickup stays available until expiry so the player can spend
first. Collection clamps to the current meter capacity. Nothing is carried
between battles.

After 15 eligible kills without a drop, the next eligible kill guarantees a
pack. The miss counter resets when the pack appears, even if it is left to
expire. A dedicated random sequence preserves seeded challenge rolls without
consuming the combat/cosmetic random generator.

Bosses and ordinary authored enemies qualify. Summoned escorts, split bodies,
graveyard reanimations, player-created objects, and self-destroying siege
carriers do not produce additional rolls. Spawn provenance resets when a unit
is reused from the pool. Existing mana, priority-threat courage, and
clean-defense rewards retain their behavior.

Pickup taps work through desktop and mobile camera transforms, including the phone view with cards hidden, and have a
generous minimum hit area. They take priority over a simple ground-targeted
spell tap, preserve the armed spell, and cost no mana. Explicit spell drags can
land over a pack without collecting it. Camera pans, secondary fingers,
synthetic mouse events following touch, canceled gestures, and stale releases
after focus loss cannot claim a pack. Battle end clears all packs.

## Look and feel

A pack is a painted ember gem: a faceted amber teardrop with a flame inside, in a
gilded filigree setting. It tumbles out of the fallen enemy's chest in a short arc,
lands with a puff of dust and a spray of sparks, then hovers over the road. While it
waits it bobs and sways, casts a warm pool of light and a soft shadow on the road,
gives off rising embers and catches a glint across its facets every couple of
seconds. The embers die down as it fades out.

Collecting it plays a warm rising chime, flashes, rings across the road and throws
sparks. The gem swells, then streaks on a comet trail into the flame on the courage
meter, which flares as it lands. Reduced motion keeps the gem still and fades it in
place. High contrast adds a cream ring around it.

## Direct grants

`GrantCourage(from, amount)` adds courage at once, clamped to the meter. A loose
flame with a spark trail flies from the source into the meter, with a `+N` number at
the source. It can never be tapped. At a full meter nothing is granted and nothing
flies. Quartermaster and Umbral Coin budgets count only the courage actually
granted.

## Tuning and code

The drop chance, amount, lifetime, fade duration, and guarantee threshold live
in `data/combat_config.json`, loaded through `CombatTuning`. A zero chance
disables drops; a zero guarantee threshold disables bad-luck protection.

- `BattleController.CourageDrops.cs`: drops, collection, input ownership, seeded randomness, and lifetime updates.
- `CouragePickup.cs`: the gem's pop, hover, glow, embers and glint, its expiry fade, and the collection or grant flight.
- `RoyalMeter.Flare`: the courage meter's flare when a pack or grant lands.
- Art: `art/royal/jobs/12-courage-pack.json` (Codex painting) → `art/royal/pickups.py` → `assets/fx/pickups/courage_ember.png`. Sounds: `courage_drop` and `courage_pickup` in `art/audio/sfx/ui.py`.
- `Unit.CanDropCourage`: runtime provenance, reset by `Setup` and assigned by authored spawning.


## Verification

The combat review's `--courage-drops` profile exercises real enemy cleanup,
summons/splits/carriers, pool reuse, seeded reward sequences, expiry and pause,
capacity clamping, and viewport mouse/touch input. It is also included in
`--regressions`. Runs use an isolated `--save-suffix=combat-review-...`.

It also checks that direct grants add courage at once, clamp at capacity and leave
no packs, and the content review checks that a Supply Crate leaves no packs.

With `--screenshots`, the profile saves desktop and phone battle captures to
`artifacts/courage-drops/`: `*-packs` (a settled and a fading pack) and `*-motion`
(a pack tumbling out and one flying to the meter) and `*-arrival` (the meter's flare). Phone-size captures are desktop emulation, not a
physical-device certification.

Verified 8 October 2026: game build with zero warnings/errors; 57 focused
checks including desktop/phone captures; 1,001 combat regression checks; 79
card-input checks; 12,008 data checks; and 86 server tests, all passing. The
repository verification also completed its website preview build. Native
desktop and phone-size pack captures were visually inspected.
