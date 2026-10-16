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
  backdrop. The mouse wheel, trackpad, middle-drag, arrow keys, Home/End and F
  (follow) work too.
- Drag the field to pan. The camera holds there for four seconds after the last
  pan, then follows the fighting again; F resumes at once. Tracking centers the
  models in the uncovered field instead of behind the HUD.
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
  rows. A list too long for that (the tower's hundred floors) sets
  `RealmModal.KeepScroll`; once it reaches its end, the drag carries on into the page.
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

## Animated model previews

Battle preparation shows the victory rewards, a horizontally scrolling
row of touch-sized squad cards (portrait and name), and the **Warband** and
**Deploy** actions. Tapping a squad card opens the model inspector, on desktop
too. The armory
shows the selected unit's animated model: tapping it opens the inspector, and
dragging it does not.

The inspector shows the actual character frames, with Idle, Walk, and Attack
controls and previous/next browsing through the squad. Reduced-motion mode uses
still poses. Previews spawn no combat units and do not change progression or
combat.

All 52 units have preview atlases in `assets/ui/models/`, holding their Idle,
Walk, and Attack frames. Units with hi-res battle atlases (every unit except the
War Hound) reuse those envelope-cropped battle frames (see `ASSETS.md`); the War
Hound, whose standard 192×240 battle frames already meet the target density,
uses its native 256×320 Blender frames. Nothing is upscaled. Each preview owns
its texture and releases it when closed or changed; only crop bounds are cached.
Missing previews fall back to the battle atlas. Cropping uses a stable union of
the three animation clips so weapons remain in frame and the model does not
change scale between poses.

Previews are packed with the battle atlases (see `art/remaster/README.md`):

```sh
python3 art/remaster/pack.py stage --only units
python3 art/remaster/pack.py apply
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

`--scroll-reach` visits every home page and tab, the level track, achievements and
the hub, plus (`extras`) the stage briefing, every spell and relic inspector, the
enchantments inspector, the account dialog, the battle menu, guide, settings and a
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

Scope: this covers battle, mobile preparation, and the shared model inspector in
phone-sized landscape views. Portrait layouts and physical Android/iOS device
validation are not covered by the desktop phone-sized review.
