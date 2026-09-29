# Icon-first battle cards

The in-battle unit and spell buttons are now portrait-led. Their ordinary ready
state shows only the artwork and a brass courage-cost badge at the top-right—no
`DEPLOY`, `CAST`, name, level, or repeated `Ready` line. Names and detailed stats
remain in hover tooltips, accessible button labels, and the mobile placement hint
after selecting a card.

## Visual states

- Ready: large authored portrait, textured price plate.
- Selected: bright frame and a check mark; unit and spell selections are mutually
  exclusive visually, matching the active targeting mode.
- Cooldown: portrait shade proportional to remaining cooldown, plus a small
  seconds label. The price is above the shade and remains readable.
- Unaffordable: subdued artwork, warmer price plate, explicit `N short` label.
- Battle ended/checkpoint: existing disabled-button rules remain in effect.

Desktop card height is unchanged. Phone cards retain a 96-logical-pixel height and
use a 112-pixel minimum width, so the normal three-unit/two-spell deck fits without
scrolling. Larger decks can still scroll. All decorative children ignore mouse
and touch input: tapping either the portrait or the price activates the same
card. Keyboard shortcuts and the select-then-place flow remain available as
alternatives to dragging.

## Drag to deploy and cast

Press a ready unit or magic card (including its cost badge), drag onto the
battlefield, and release. Mouse and touch use the same targeting and spending
paths. A lifted portrait follows the pointer; the battlefield shows the unit's
entry lane and model ghost, or the spell's area of effect. Instructions stay in
screen space so they remain readable at mobile camera zoom.

Unit drops choose the lane, not a new unrestricted spawn position. Units still
enter from the caravan or an available captured forward post, with existing
frontline snapping, forward-post charges and recovery. Magic uses the drop
position; self-centered War Cry and last-fallen-ally Resurrect retain their
existing special targeting rules.

Returning to the cards, releasing over other UI or outside the field cancels
without spending. Escape/right-click, focus loss, system cancellation, pause,
checkpoints, battle end and resizing also cancel safely. Courage and cooldowns
are checked again on release. One pointer owns the gesture, so secondary fingers
and emulated mouse events cannot create duplicate deployments or casts.

Small finger jitter does not deploy. Horizontal swipes inside an overflowing
card row scroll it; lifting out of the row starts a card drag. The camera does
not pan or follow units while a card is being aimed. Dragging terrain still pans
normally when no card is held.

## Editable parts

- `scripts/combat/hud/BattleActionCard.cs`: portrait, badge, status and selection.
- `scripts/combat/BattleController.Ui.cs`: unit/spell cost and cooldown binding.
- `scripts/combat/BattleController.Mobile.cs`: touch layout and placement hint.
- `scripts/combat/BattleController.CardDragging.cs`: gesture capture, validation,
  cancellation and drag preview.
- `art/ui/build_surfaces.py`: source for `assets/ui/frames/cost_badge.svg`.
- `assets/ui/icons/units/` and `assets/ui/icons/spells/`: existing portrait artwork.

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
```

The card-specific review exercises desktop and phone sizes, actual badge clicks,
resolved prices, selection and keyboard input, low courage, unit/spell cooldowns,
recovery and disabled states. Screenshots are written to
`artifacts/deployment-card-review/`. The mobile review also verifies bounds,
touch targets, the larger model view, resizing, pause/results and inspection.
These are desktop-rendered phone-size checks, not physical-device certification.

The drag review sends actual viewport mouse/touch events and checks deployment,
damage and healing at the drop position, spending and cooldowns, invalid and
interrupted gestures, duplicate releases, multiple fingers, emulated mouse
events, overflowing card-row scrolling, and forward-post rules. Its desktop and
phone preview screenshots are written to `artifacts/card-drag-review/`.

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
