# Live UI test parity

The presentation reviews use `LiveUiReview.Open` to enter activities through
`SceneRouter`. Home activities must be children of the live map's `RealmModal`,
with `home_modal` set and the expected destination scene loaded. A standalone
activity cannot pass this check.

The old direct scene loader bypassed `MapMenu.OpenHomeDestination`, modal themes,
activity reflow, and the mobile canvas. Its screenshots could show different
menus from those reached by a player. This affected the main UI, typography,
mobile preparation, feedback, privacy, world/exploration, and Codex art reviews.

## Coverage

| Review | What it now covers |
| --- | --- |
| `--live-parity` | Native clicks on home tabs and Settings, More → Community → Challenges → LAN, modal back/close, all 22 routed activities, and preservation of map/resources. Runs on desktop, smaller windows, and phones. |
| `--typography`, `--all-menus` | All public activity routes discovered from `SceneRouter`, their live overlays, and the active activity's controls. Typography visits every visible tab. Add `--only=ShopMenu,ArenaMenu` to re-capture just the screens being changed. |
| `--typography-advanced` | Every unit/spell detail and all 60 campaign stages. Fixtures explicitly open the tiles; the review selects the actual tile and verifies preparation's stage number. |
| `--core`, `--playthrough` | Preparation, armory, challenges, settings, deployment, battle menus, and result/return flows through production navigation. |
| Home, rewards, armory, storehouse, developer, progression and star reviews | Shared live screen loader; existing state fixtures and behavior checks remain in the isolated test save. |
| `MobilePresentationReview` | Production preparation overlays on both desktop and phone, production armory overlay, full model inspector, and controlled battle presentation. |
| `FeedbackReview`, `PrivacyReview` | Production home, armory, preparation and Settings; screenshots include the real map/overlay hierarchy. |
| `ExplorationReview`, `WorldArtReview` | Production home maps. World art battle captures also use the router. |
| `BlenderAssetSmoke` Codex screenshot | Production Codex overlay, with the Bosses tab and a discovered entry selected through the controls. |

The uncalled free-roam/watchtower/shrine adventure review and the uncalled old
home review were removed. Current tile-map coverage remains in
`UiReviewSmoke.Tiles.cs`; `--adventure` is an alias for that current review.
The shared site helper focuses the current tile coordinates, rather than the
retired adventure-layout coordinates.

## Checks and fixtures

Text audits use the current viewport and active overlay. They check text width,
glyph bounds, label/button containment, scroll clipping, and modal body bounds.
Scrollable content may extend along its enabled axis; the containing scroll
area must still fit. A modal's clipping flag does not exempt unreachable text
or buttons from the body-bound check. Independent `CanvasLayer` inspectors are
checked in their dedicated review rather than against the underlying modal.

Late-game, full-roster and deterministic battle fixtures are intentional. They
seed only isolated saves and exercise the production controls and components;
they do not build replacement versions of activity menus.

Material/health-bar galleries, combat motion/camera/weapon tests and sprite
asset tests intentionally instantiate individual battle components or controlled
battles. They verify rendering or simulation, rather than claim navigation
coverage. Data validators and HTTP/LAN service smoke tests do not render menu
screens and are not evidence of visual parity.

`CombatReviewSmoke --reference-map` is an explicit historical battlefield
comparison. It changes only the test process's geometry and is not used in
the parity suite. Art-generation contact sheets and galleries are also asset
reviews, not evidence of current screen navigation.

The corrected phone checks exposed cramped multiplayer footer actions, narrow
armory actions, fixed-height activity descriptions, and inaccessible storehouse
purchase buttons. The layouts now reserve icon/text widths, scroll long action
bars, use a narrower model column and one upgrade column, grow activity cards,
and make phone catalogs scrollable. Tiny bevels no longer produce invalid
polygons.
Daily gift cards also use the modal's available width when rebuilt.

Visual game processes must run sequentially. Concurrent native windows can
send focus-loss events during card gestures, which correctly cancel a live
drag and create misleading input-test failures. Headless component tests may
run independently.
The health-bar, combat, deployment-card, and battle-polish reviews now stop active audio before quitting,
allowing playback references to drain instead of producing shutdown errors.

## Verified on 4 October 2026

All ten engine review drivers and the data validator were inspected. Current
results on Godot 4.6.1 Mono/macOS:

- Live route checks: all 22 activities, on desktop, smaller windows, and phone
  preview, with zero failures and no engine errors.
- Typography: all activity tabs at those three sizes, plus the extended armory
  review and all 60 campaign stage selections, with zero failures.
- UI interactions: core, all menus/repeated refresh, playthrough, current home
  tile map and its home subreviews, armory details, material/deployment galleries,
  mouse/touch card dragging, progression, stars, and battle cleanup/lighting/polish
  reviews, with zero failures.
- Dedicated presentation: Feedback (desktop and phone), Privacy, Mobile,
  Exploration and World Art (desktop and phone), and Codex art capture, with
  zero failures.
- Controlled combat regressions, motion, and health-bar checks: zero failures.
- Game build: zero warnings/errors. Data validator: 7,483 passed, zero failed.
  Server tests: 86 passed, zero failed.

Runs use isolated saves. Phone preview is desktop emulation; physical-device
rendering and real online account/room data were not part of this visual pass.

## Run

```sh
./scripts/smoke/live_ui_parity_smoke.sh
./scripts/smoke/typography_smoke.sh
./scripts/smoke/ui_review_smoke.sh
```

The parity run writes screenshots and route logs to `artifacts/live-ui-parity`.
The live multiplayer screenshot reached through actual navigation is
`artifacts/live-ui-parity/desktop/click-Multiplayer.png`.
