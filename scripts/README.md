# Crownroad source map

The game deliberately uses a small number of folders with clear runtime roles:

- `core/` — application state, persistence, network-facing services, catalogs, and routing.
- `data/` — typed definitions and JSON loading.
- `combat/` — simulation, spawning, units, effects, pools, and battlefield presentation.
- `combat/hud/` — reusable battle-only HUD controls and floating text.
- `ui/` — individual navigable menus and menu-specific interaction code.
- `ui/shared/` — shared UI theme, layout/backdrop, asset loading, and badge components. New reusable UI code belongs here, not in a screen class.
- `tests/` — game data validation.
- `tools/` — editor/development helpers.

## Conventions

- Keep game rules out of UI classes; menus should call `GameState` or a focused service rather than reproduce game calculations.
- Put reusable controls, modal helpers, and visual primitives in `ui/shared/`.
- Keep resource paths (`res://…`) in scene files or dedicated loaders, never spread across menus.
- Keep a feature’s display-only pieces close to its runtime owner. For example, battle HUD widgets live in `combat/hud/`.
