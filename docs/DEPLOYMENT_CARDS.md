# Icon-first battle cards

The in-battle unit and spell buttons are portrait-led. Their ordinary ready
state shows only the artwork and a brass courage-cost badge at the top-right—no
`DEPLOY`, `CAST`, name, level, or repeated `Ready` line. Names and detailed stats
remain in hover tooltips, accessible button labels, and the mobile placement hint
after selecting a card.

## Visual states

- Ready: large authored portrait, textured price plate.
- Selected: bright frame and a check mark; unit and spell selections are mutually
  exclusive visually, matching the active targeting mode.
- Cooldown: portrait shade proportional to remaining cooldown, with no countdown
  text. The price is above the shade and remains readable.
- Unaffordable: subdued artwork and a red-tinted price badge.
- Battle ended/checkpoint: existing disabled-button rules remain in effect.

Desktop card height is unchanged. Phone cards retain a 96-logical-pixel height and
use a 112-pixel minimum width, so a three-unit/two-spell deck fits without
scrolling. Larger decks can still scroll. All decorative children ignore mouse
and touch input: tapping either the portrait or the price activates the same
card. Number keys deploy units; Q–T select magic.

## Tap to deploy, drag to cast

Tapping a ready unit card (including its cost badge) or pressing its number key
deploys the unit at once. The ramp door of the wagon's troop hold drops, the unit
steps out of the lit doorway and walks down the ramp, and it marches from the
ramp's foot on the wagon's centre line. There is no lane or position to choose, so
the decision is only *who* and *when*. Courage and cooldowns are checked on release. Lifting off the card,
Escape/right-click, focus loss, system cancellation, pause, checkpoints, battle
end and resizing cancel a held card for free.

Magic cards still drag: press, drag onto the battlefield and release, or tap to
select and then tap the target. A lifted portrait follows the pointer and the
battlefield shows the spell's area of effect. Self-centred War Cry and
last-fallen-ally Resurrect keep their special targeting. Returning to the cards,
releasing over other UI or outside the field cancels without spending. Ground
taps never deploy units.

One pointer owns the gesture, so secondary fingers and emulated mouse events
cannot create duplicate deployments or casts. Small finger jitter on a card
still counts as a tap. Horizontal swipes inside an overflowing card row scroll
it. The camera does not pan or follow units while a card is held.

## Battlefield proportions

The battle uses lane-game proportions like Dead Ahead: the wagon and stronghold
are about two soldiers tall, and the field runs about two screens from base to
base (`data/combat_config.json`: a world about 1,200 units wide with the field
from x 42 to 1158, `ViewWidth` 600 units across the screen, `StructureScale`
0.58 for the wagon and stronghold art). The camera keeps that scale (about 2.1×
in a 1280-pixel-wide window), runs the band low across the screen under the
scenery and above the card tray, and follows the fighting. Behind the band, each
zone's layered parallax backdrop (far, mid and near Blender layers, the near one
locked to the field) fills the screen; painted stage plates are only a fallback.

The walking band is about one soldier deep: 30 units walkable
(`BattlefieldTop`/`BattlefieldBottom` minus `SpawnVerticalPadding`). Every
fighter's `AggroRangeY` (15–22) lies between half and all of that depth. A unit
walking straight out of the wagon reaches an enemy on either edge, but a unit on
one edge cannot see an enemy on the other. Enemies leave the stronghold on any
line across the band. `DataIntegrityValidator` fails if a unit's `AggroRangeY` or
the band breaks this rule.

Distances are tuned for the two-screen field. A Swordsman crosses in about 30 seconds.
Ranged reach is two to four soldiers, and aggro is about two soldiers past
weapon reach. Auras, rallies, spell areas, campaign effects and base weapons
reach part of the field, not most of it.

The stronghold straddles the band, its mass on the centre line, with outworks
on its flanks: a curtain wall, a beacon tower, a brazier, a palisade and a bone
totem. They are scenery only; troops walk past them and nothing targets them.

Battle text is numbers only: damage, healing and repairs.

Support troops wait for a leader ahead of them but never fall back to one
behind. Out in front, they keep marching. Only melee troops and units flagged
`DamagesStructures` (Ballista Crew, Bone Ballista, Plague Engine, ranged
bosses) can damage the wagon or the stronghold. Other ranged troops advance to
firing range of a base and hold there. Tooltips show "Hits bases" or
"Can't hit bases" for ranged units.

## Editable parts

- `scripts/combat/hud/BattleActionCard.cs`: portrait, badge, status and selection.
- `scripts/combat/BattleController.Ui.cs`: unit/spell cost and cooldown binding.
- `scripts/combat/BattleController.Mobile.cs`: touch layout and placement hint.
- `scripts/combat/BattleController.CardDragging.cs`: gesture capture, unit taps,
  magic validation, cancellation and drag preview.
- `scripts/combat/BattleController.Camera.cs`: soldier-scale zoom, band placement, follow and panning.
- `scripts/combat/hud/BattleActionCard.cs`: the card art, round cost badge (bronze `hud-cost` for courage on troops, blue `hud-cost-mana` for mana on magic) and cooldown.
- `assets/ui/icons/units/` and `assets/ui/royal/items/`: unit portraits and painted spell pictures.

The visual crops transparent icon padding at runtime; no source PNG is modified.
Crop bounds are cached, and each card releases its owned atlas wrapper when it
leaves the scene. Artwork is neither generated nor decoded every frame.

## Review

Use a fresh isolated save suffix for each run:

```sh
dotnet build Game.csproj --no-restore
godot --path . --scene res://scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-cards-UNIQUE --deployment-cards
godot --path . --scene res://scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-drag-UNIQUE --drag-cards
godot --path . --scene res://scenes/tests/MobilePresentationReview.tscn -- --save-suffix=mobile-review-cards-UNIQUE
godot --path . --scene res://scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-card-flow-UNIQUE
godot --path . --scene res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-drag-camera-UNIQUE --camera
godot --headless --path . --scene res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-lanes-UNIQUE --lanes
```

The card-specific review exercises desktop and phone sizes, actual badge clicks,
resolved prices, selection and keyboard input, low courage, unit/spell cooldowns,
recovery and disabled states. Screenshots are written to
`artifacts/deployment-card-review/`. The mobile review also verifies bounds,
touch targets, the larger model view, resizing, pause/results and inspection.
These are desktop-rendered phone-size checks, not physical-device certification.

The drag review sends actual viewport mouse/touch events and checks unit taps
deploying at the wagon, magic damage and healing at the drop position, spending
and cooldowns, invalid and interrupted gestures, duplicate releases, multiple
fingers, emulated mouse events and overflowing card-row scrolling. Its desktop
and phone screenshots are written to `artifacts/card-drag-review/`. The `--lanes`
review checks the aggro rule against real units, enemy entry lines, the
no-retreat rule for support troops and which troops can hit a base.

Icon-card baseline verified on 29 September 2026: build completed with zero errors/warnings;
89 card-specific checks, 162 mobile-presentation checks, 12 functional UI checks,
and 52 shared-material checks passed. Actual desktop and phone-sized ready,
low-courage and cooldown screenshots were inspected. The dedicated card review
reported a shutdown-only ObjectDB warning in that baseline; the other three reviews exited
without it. No combat costs, cooldown durations, deployment rules, or save data
outside the isolated review saves were changed.

Drag follow-up verified on 29 September 2026: build completed with zero
errors/warnings. All 81 drag checks, 89 icon-card checks, 162 mobile-presentation
checks, 31 camera checks and 12 functional UI checks passed (375 total), with no
engine errors or warnings in these runs. Desktop and phone-sized drag previews
were visually inspected. Physical-device touch testing remains outstanding.

Tap-to-deploy and shallow battlefield verified on 5 October 2026: build with zero
errors/warnings; 7,535 data checks, 86 server tests, 612 combat regressions,
78 card-gesture checks, 34 camera checks and 150 mobile-presentation checks
passed. Desktop and phone battle screenshots were inspected.
