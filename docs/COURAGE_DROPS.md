# Clickable courage packs

Defeating an authored enemy has a 10% chance to leave a physical courage pack.
Click or tap it to gain up to 20 courage. Holding the pointer does not collect
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

## Tuning and code

The drop chance, amount, lifetime, fade duration, and guarantee threshold live
in `data/combat_config.json`, loaded through `CombatTuning`. A zero chance
disables drops; a zero guarantee threshold disables bad-luck protection.

- `BattleController.CourageDrops.cs`: drops, collection, input ownership, seeded randomness, and lifetime updates.
- `CouragePickup.cs`: brass flame-token drawing, expiry fade, and collection flight.
- `Unit.CanDropCourage`: runtime provenance, reset by `Setup` and assigned by authored spawning.

Quartermaster and Supply Crate remain expansion designs; this change implements
the ordinary enemy drop system only.

## Verification

The combat review's `--courage-drops` profile exercises real enemy cleanup,
summons/splits/carriers, pool reuse, seeded reward sequences, expiry and pause,
capacity clamping, and viewport mouse/touch input. It is also included in
`--regressions`. Runs use an isolated `--save-suffix=combat-review-...`.

With `--screenshots`, the profile saves desktop and phone battle captures to
`artifacts/courage-drops/`. Phone-size captures are desktop emulation, not a
physical-device certification.

Verified 8 October 2026: game build with zero warnings/errors; 57 focused
checks including desktop/phone captures; 1,001 combat regression checks; 79
card-input checks; 12,008 data checks; and 86 server tests, all passing. The
repository verification also completed its website preview build. Native
desktop and phone-size pack captures were visually inspected.
