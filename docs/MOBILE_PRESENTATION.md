# One presentation for every screen

The game has a single presentation, sized for a landscape phone and scaled 1:1
to every other screen (since 2026-10-15; before, phones got a separate layout).
Everything is laid out on one 1280×720 canvas (`project.godot`: stretch mode
`canvas_items`, aspect `keep`), so a phone, a tablet and a monitor show exactly
the same picture at different sizes: nobody sees more or less of the field or
the menus. Screens of another shape get black bars (a 19.5:9 phone at the sides,
a 4:3 tablet above and below).

Sizes are chosen for the smallest case, a phone showing the canvas about 390 pt
tall (0.54 pt per canvas pixel):

- Text: at least 18 px for mixed case and 16 px for engraved capitals and
  figures, as drawn on the canvas (`ReadingText.PhoneBody`/`PhoneCaps`; the
  concept-screen helpers `RoyalText`, `RoyalSpec.Label`, `RoyalLabel` and
  `RoyalText.FitLines` never go below them). `UiReviewSmoke --typography`
  reports anything smaller as `SMALL_ON_PHONE`.
- Menus and modals draw on a 1/1.55 logical canvas scaled up 1.55×
  (`MobilePresentation.HudScale`), with 56-unit touch targets.
- Concept-screen buttons grow their touch area to 56 units without changing
  their art (`RoyalButton.TouchRect`).
- The battle HUD keeps the battle concept's sizes (`BattleHudScale` 1): its
  cards are about 64×71 pt on a phone and its numbers 18–20 px, and a larger HUD
  would cover the road.

`UiReviewSmoke --typography --window=WxH` captures every screen at any window
size; captures at 844×390, 1024×768, 1280×720 and 1440×900 match once scaled to
the same size.

The battle shares the soldier scale and two-screen field everywhere (see
[Deployment cards](DEPLOYMENT_CARDS.md#battlefield-proportions)).

- Combat camera: the soldier scale, framing the whole battle band between the
  meters and the cards, and never showing past the bottom of the painted
  backdrop. The mouse wheel, trackpad, middle-drag, arrow keys and Home/End
  work too.
- Drag the field to pan. The camera never moves by itself: it stays where the
  player leaves it. A flick glides on and slows to a stop; a touch on the
  gliding field catches it without counting as a tap. Dragged or flung past
  either end, the field stretches a little (at most a quarter of the view) and
  springs back when let go.
- HUD: the battle concept's layout at 1:1, with no camera buttons, and icon-first unit
  and spell cards with the art whole inside the card and the courage (troops) or mana
  (magic) cost in the corner. Cards shrink to share the dock's width, so the row never scrolls.
- Pause, results, and checkpoint choices fit the smaller layout; long
  result/checkpoint reports scroll.
- Every list scrolls by finger from anywhere on it. Godot passes a wheel turn up
  through panels and buttons but stops a touch drag at the first control that
  catches the mouse, so `TouchScroll` (installed by `SceneRouter`) switches
  controls inside a vertical scroll area from Stop to Pass. Sliders, scroll bars
  and text fields keep their own drags, as do horizontal-only strips such as the
  battle card dock; mark any other control with `TouchScroll.KeepDrags`. A drag
  that starts on a button scrolls and cancels the press; the 8 px
  `gui/common/default_scroll_deadzone` lets a wobbly tap still press it.
- Activity pages scroll as one page: `RealmModal.RelaxMinimums` turns a list's own
  vertical scroll off inside the page scroll, so a panel grows to show all of its
  rows. The More hub's painted boards (`RoyalBoard`) don't scroll at all: long lists
  page (the tower's hundred floors are ten to a page).
- Touch coordinates are transformed through the camera. A unit walks out of the
  war wagon's door when its card is tapped and released, never from a field tap;
  lifting off the card, canceled touches, pause, focus loss, and resizing must
  not cause accidental deployment. Spells are dragged onto the field, or tapped
  to arm and then cast with a field tap.
- Native safe-area pixels are converted to canvas coordinates, including existing
  letterbox padding, before laying out controls.

This changes presentation, not simulation ranges, movement speeds, cooldowns, or
the existing wind-up/contact/recovery timing. Contact-timed attacks,
weapon-origin projectiles, recoil, and target lifetime guards are unchanged.

One-shot particles and projectile trails normalize their 128-pixel textures
against the intended world-pixel diameter, so hit and deployment effects do not
cover the models. This applies on desktop too.

## Unit figures

Battle preparation shows the victory rewards, a horizontally scrolling
row of touch-sized squad cards (portrait and name), and the **Warband** and
**Deploy** actions. Squad cards are display-only; the squad is edited in the
armory. The armory is two columns, the collection grid beside the selected
unit's full profile, with no large model.

Menus draw units as still figures (`UnitFigure`): one small resting-pose
texture per unit in `assets/ui/royal/figures/`. No screen plays an animated
model preview any more; the armory's large model went when it was widened.

The figures are cut from the preview sheets in `assets/ui/models/` (the first
Idle frame of each) by `art/royal/figures.py`. Those sheets hold every unit's
Idle, Walk and Attack frames and are packed with the battle atlases (see
`art/remaster/README.md`), but the game never loads them, so the export presets
leave the whole folder out (`docs/BUILD_SIZE.md`). After repacking units,
rerun the figure cut:

```sh
python3 art/remaster/pack.py stage --only units
python3 art/remaster/pack.py apply
python3 art/royal/figures.py
```

## Review

Run with a unique isolated save suffix (never use the player's normal save):

```sh
dotnet build --no-restore
godot --path . --scene res://scenes/tests/MobilePresentationReview.tscn -- --save-suffix=mobile-review-UNIQUE --capture-motion
godot --headless --path . --scene res://scenes/tests/CombatMotionReview.tscn -- --save-suffix=motion-review-UNIQUE
godot --headless --path . --scene res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-UNIQUE --regressions
godot --path . --scene res://scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-UNIQUE --typography --window=844x390
godot --path . --scene res://scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-UNIQUE --scroll-reach --mobile-preview [--late-game] [--only=ProfileMenu,extras]
```

`--scroll-reach` visits every home page and tab (`realm_tabs` rows and `royal_tab` tabs), the level track, achievements and
the hub, plus (`extras`) the stage briefing, relic equipping on the relics page, the
enchantments inspector, the Account page, the battle menu, guide, settings and a
victory. For each vertical scroll area it scrolls to the end (outer scrolls
following) and fails `SCROLL_CUT_OFF` if the last row is still below what the
screen shows, or `SCROLL_SQUASHED` if the area is under 96 px tall. Then, at nine
points across the area, a wheel turn and a finger drag (touch emulation on) must
move it or a scroll around it (`SCROLL_WHEEL_BLOCKED`, `SCROLL_DRAG_BLOCKED`), and a
button under the finger must let go of its press once the list moves. It also
checks that a wobbly tap still presses a button in a list. `--late-game` seeds a
save with everything owned, so lists are at their longest. Captures go to
`artifacts/scroll-reach`.

The presentation review uses 844×390 and 667×375 landscape windows and writes previews
under `artifacts/mobile-review`. The 844×390 window retains the game's existing
aspect-preserving letterboxing, so its captured game surface is 693×390. The
optional motion capture is an arranged in-engine duel using real attack and
damage code, not a complete recorded campaign playthrough.

Latest review (2026-09-28): build succeeded with no warnings or errors; 146 mobile
presentation checks, 429 combat-motion checks, and 122 combat-regression checks
passed. The combat drivers report ObjectDB exit warnings; the visible mobile
review reports none.

Scope: this covers battle, mobile preparation, and the armory in
phone-sized landscape views. Portrait layouts and physical Android/iOS device
validation are not covered by the desktop phone-sized review.
