# Crownroad source map

The game deliberately uses a small number of folders with clear runtime roles:

- `core/` — application state, persistence, network-facing services, catalogs, and routing.
- `data/` — typed definitions and JSON loading.
- `combat/` — simulation, spawning, units, effects, pools, and battlefield presentation.
- `combat/hud/` — reusable battle-only HUD controls and floating text.
- `ui/` — individual navigable menus and menu-specific interaction code.
- `ui/shared/` — shared UI theme, layout/backdrop, asset loading, and badge components. New reusable UI code belongs here, not in a screen class.
- `tests/` — game data, behavior and presentation reviews. Navigable screen reviews use the production router; see [live UI parity](../docs/LIVE_UI_TEST_PARITY.md).
- `tools/` — editor/development helpers, including `build_site.py` (public website). Music and sound effects are rendered by `art/audio/` (see its README).
- `platform/` — native store bridges; `GooglePlayBillingBridge.gd` adapts the Google Play Billing plug-in for `NativeIAPService`.
- `smoke/` — shell runners for headless reviews and smoke tests (combat, UI, live UI parity, typography, privacy, stage stars, adventure map, LAN races, and the HTTP multiplayer providers against a local stub). Game-driving reviews pass a unique `--save-suffix` so personal saves are untouched.
- `analysis/` — offline Python tools. `expand_campaign.py` generates the 100-stage campaign from `legacy/stages-60.json`; the review and audit scripts summarize benchmark logs into reports.
- `verify_all.sh` — the full local check: builds the game, validates data, runs the server tests and builds the website.

## Conventions

- Keep game rules out of UI classes; menus should call `GameState` or a focused service rather than reproduce game calculations.
- Put reusable controls, modal helpers, and visual primitives in `ui/shared/`.
- Keep resource paths (`res://…`) in scene files or dedicated loaders, never spread across menus.
- Keep a feature’s display-only pieces close to its runtime owner. For example, battle HUD widgets live in `combat/hud/`.

Production polish is reviewed with `smoke/production_polish_smoke.sh`. Loading-art slots are documented in [loading screens](../docs/LOADING_SCREENS.md). `tools/audit_release_package.py` checks a completed Godot ZIP export and its compiled assembly for excluded development content. `tools/bundle_size.py` breaks down an exported Android bundle against Play's size limits, and `tools/mobile_textures.py` keeps large painted textures on phone-friendly import settings (see [build size](../docs/BUILD_SIZE.md)).
