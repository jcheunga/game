# Mobile battle and model presentation

The battle scene has a phone-specific presentation, enabled on Android/iOS,
touch-enabled web builds, or with the `--mobile-preview` user argument. Desktop
and phone share the soldier scale and two-screen field (see
[Deployment cards](DEPLOYMENT_CARDS.md#battlefield-proportions)).

- Combat camera: the same soldier scale as desktop, framing the whole battle
  band between the HUD and the cards. The zoom button steps to 1.25× and 1.5×
  closer, then back.
- Drag the field to pan. The view button then shows a sword: tap it to resume
  tracking. Otherwise the view button fits the whole field, and tapping it again
  restores the selected zoom and previous close-view position.
- The clear-view button hides the cards to expose more of the field; tapping it
  again restores them. Field taps cannot cast spells in this view-only mode.
  Both zoom and card visibility preserve the visible focal point. Tracking
  centers the models in the uncovered field instead of behind the HUD.
- HUD: 1.55× scale, 56-unit minimum top-row touch targets, icon-first unit and
  spell cards with a large portrait and the courage cost in the corner, and
  horizontal scrolling for larger decks.
- Pause, results, and checkpoint choices fit the smaller layout; long
  result/checkpoint reports scroll.
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

Mobile battle preparation shows the victory rewards, a horizontally scrolling
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
godot --headless --path . --scene res://scenes/tests/CombatMotionReview.tscn -- --save-suffix=motion-review-UNIQUE --mobile-preview
godot --headless --path . --scene res://scenes/tests/CombatReviewSmoke.tscn -- --save-suffix=combat-review-UNIQUE --regressions --mobile-preview
```

The mobile review uses 844×390 and 667×375 landscape windows and writes previews
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
