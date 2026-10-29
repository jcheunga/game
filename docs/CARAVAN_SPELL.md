# Caravan spell

The war wagon has a spell of its own, separate from the loadout. When the mana bar is full, a slot the size of a
unit card slides out on the right of the card dock. Tap it (or press Space) to cast **Arcane Barrage**, which spends
the whole bar. The spell is a first version and may change.

## What it does

1. **Wind-up (0.45 s).** An arcane rune circle opens under the wagon, light gathers into the lantern on its roof
   and the field falls into a violet dusk.
2. **Barrage (3 s).** The lantern looses 30 arcane missiles a second in a wide fan. Each missile weaves as it
   homes in on a living enemy anywhere on the field. Targets are spread so that every enemy is struck again and
   again: each launch picks the enemy hit least so far, up to 10 hits per enemy (140 magical damage before
   resistance). When a target dies mid-flight, the missile turns to the nearest enemy that still has hits left.
   Missiles left over once every enemy has its share burst on open ground, mostly in view. The barrage never
   hits troops, the wagon or the stronghold.
3. **Cooldown (60 s).** The slot stays shut until the bar is full again. If the bar refills before the cooldown
   ends, the slot opens with the usual cooldown shade.

Kills from the barrage pay mana as normal (7 per enemy), so it partly refills the bar.

## The mana scale

The bar holds **100**. To make room for the barrage, every mana figure was scaled by 3.5 on 2026-10-15, so
existing spells balance exactly as before:

| | Before | Now |
|---|---|---|
| Mana bar | 30 | 100 |
| Mana per enemy kill | 2 | 7 |
| Spell and tactic costs | 4–18 | 14–63 |
| Soul Harvest, per corpse | 4 | 14 |
| Crownroad Charter, per supply objective | 2 | 7 |

The bar fills in 15 kills (100 / 7). In the tactical benchmark, quick early wins (stages 5–30) took 9–11 kills
(63–77 mana), and later battles (stages 50–99) took 18–20 kills (126–140 mana). So a player who saves mana can
open the barrage in longer and later battles, but not yet in the quickest early wins. Every ordinary spell cast
pushes it further away. The benchmark's casts, kills and times were identical before and after the rescale. Spell gold
prices still use the old scale (5 gold per old mana point, `GameState.GetSpellPurchaseCost`), so nothing in the
shop changed price. The server's balance dashboard grades power per mana on the new scale.

## Tuning

Everything lives in `data/combat_config.json` under `Combat.CaravanSpell`:

| Key | Value | Meaning |
|---|---|---|
| `ManaCost` | 100 | Mana spent; the slot opens at this much. Must not exceed `ManaMax`. |
| `Cooldown` | 60 | Seconds before it can be cast again. |
| `Duration` | 3 | Seconds the lantern fires. |
| `MissilesPerSecond` | 30 | Launch rate. |
| `MissileDamage` | 14 | Magical damage per hit. |
| `MaxHitsPerEnemy` | 10 | Hits one enemy can take from one barrage. |

`DataIntegrityValidator` checks the block, and that `assets/ui/royal/items/<Id>.png` exists.

## Where it lives

- `scripts/combat/BattleController.CaravanSpell.cs`: the rules (open, cast, cooldown, targeting, damage), the
  slot's layout beside the dock, and the wind-up, volley and fade effects built from `SpellVfx`.
- `scripts/combat/CaravanBarrageFx.cs`: the missiles. Flight is stepped on the battle clock, so hits keep the
  simulation's timing. Ribbons, halos and ground rings are drawn in normal blend, because added light alone
  washes out to white over the sunlit backdrops. Hot cores, stars, sparks and flashes are added light on a
  child layer. A veil dims the field under the missiles.
- `scripts/combat/hud/CaravanSpellButton.cs`: the slot. It springs out from behind the dock with a burst and the
  ready chime. While ready, it has a violet halo, heartbeat outlines, comets running round the frame and the
  gold frame of a playable card.
- Art: `assets/ui/royal/items/caravan_arcane_barrage.png`, painted with Codex
  (`art/royal/jobs/11-caravan-spell.json`). It shows the wagon's own lantern cupola, which is where the missiles
  launch from (`WagonLanternUv`, the same on every wagon skin).
- Sound: `spell_caravan_barrage` (charge, ignition and a three-second shimmering hum) and `caravan_spell_ready`
  (harp and bell) come from `art/audio/sfx/magic.py` and `ui.py`. Launches and impacts reuse `cast_arcane` and
  `impact_arcane`, spaced out so thirty missiles a second stay listenable.

## Review

```sh
godot --path . res://scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-caravan-UNIQUE --spell-gallery --only=caravan
godot --headless --path . --fixed-fps 60 res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-caravan-UNIQUE --caravan-spell
```

The gallery saves full frames to `artifacts/spell-gallery/caravan_*.png`: the slot opening, the slot ready, and
the barrage from 0.2 s to 4.6 s at quarter speed. `--caravan-spell` covers opening, cost, cooldown, missile count,
spread, the per-enemy cap and friendly safety. It also runs inside `--regressions`.
