# Mobile battle and model presentation

The battle scene now has a phone-specific presentation, enabled on Android/iOS,
touch-enabled web builds, or with the `--mobile-preview` user argument. Desktop
and phone share the soldier scale and two-screen field (see
[Deployment cards](DEPLOYMENT_CARDS.md#battlefield-proportions)).

- Combat camera: the same soldier scale as desktop, tracking the action between
  the HUD and the cards. The zoom button steps to 1.25× and 1.5× closer.
- Drag the field to pan. The view button then shows a sword: tap it to resume
  tracking. **Map** fits the whole field; **Fight** restores the selected zoom
  and previous close-view position.
- **View** hides cards and convoy orders to expose more of the field; **Cards**
  restores them. Field taps cannot deploy units or cast spells while in this
  view-only mode. Both zoom and card visibility preserve the visible focal point.
  Tracking centers the models in the uncovered field instead of behind the HUD.
- HUD: 1.55× scale, 56-unit minimum top-row touch targets, compact name/portrait/
  cost/readiness cards, and horizontal scrolling for larger decks.
- Long reports live in the expandable intel panel. Pause, results, and checkpoint
  choices fit the smaller layout; long result/checkpoint reports scroll.
- Touch coordinates are transformed through the camera. A unit deploys when its
  card is released, never from a field tap; lifting off the card, canceled
  touches, pause, focus loss, and resizing must not cause accidental deployment.
- Native safe-area pixels are converted to canvas coordinates, including existing
  letterbox padding, before laying out controls.

This changes presentation, not simulation ranges, movement speeds, cooldowns, or
the existing wind-up/contact/recovery timing. The prior contact-timed attacks,
weapon-origin projectiles, recoil, and target lifetime guards remain in use.

The close-view review also exposed a particle sizing bug: 128-pixel textures were
being multiplied by sizes intended as world-pixel diameters. One-shot particles
and projectile trails now normalize against texture size, so hit/deployment
effects no longer cover the models. This correction applies on desktop too.

## Animated model previews

Mobile battle preparation uses touch-sized controls and large animated squad
cards. Mission details move into a scrollable **Briefing** panel. Larger decks
scroll horizontally; dragging a preview does not open its inspector.

Both desktop and mobile preparation and the armory offer **Inspect model**.
The full-screen inspector shows the actual character frames, with Idle, Walk,
and Attack controls and previous/next browsing. Reduced-motion mode uses still
poses. Previews spawn no combat units and do not change progression or combat.

All 53 characters have separate preview atlases in `assets/ui/models/`. They use
the original 256×320 Blender frames rather than the 192×240 battle frames: 20
poses packed into a 1280×1280 atlas. This preserves source detail without
upscaling. Bosses and the Siege Tower instead reuse their hi-res, envelope-cropped battle
frames (see `ASSETS.md`), which hold more detail. Each preview owns its texture and releases it when closed or changed;
only crop bounds are cached. Missing previews fall back to the battle atlas.
Cropping uses a stable union of the three animation clips so weapons remain in
frame and the model does not change scale between poses.

Repack previews after changing the Blender renders:

```sh
python3 art/blender/pack_assets.py previews
```

The `units` and `all` packing actions also refresh these previews.

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
passed. The advanced text/layout audit passed across armory pages and all 60
campaign map/preparation screens. The combat drivers and advanced menu audit
report ObjectDB exit warnings; the visible mobile review reports none.

Scope: battle, mobile preparation, and the shared model inspector are adapted
for phone-sized landscape views. Other menus, including the overall armory
layout around its new preview, retain their existing layouts. Portrait layouts
and physical Android/iOS device validation are not covered by the desktop
phone-sized review.
