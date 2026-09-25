# Crownroad UI and code review

Reviewed and updated on 6 September 2026, using Godot 4.6.1 Mono on macOS.

## Delivered

- An illustrated camp home with a clear campaign action, three portraits, resource icons, and three compact navigation tabs. The home has no scroll containers.
- Mission preparation built around portrait cards, three essential stats, optional detail dialogs, and a permanently visible deployment action.
- A freely explorable illustrated atlas with fog of war, resource sites, independent leader encounters, and gated regional bosses. See [the adventure map guide](ADVENTURE_MAP.md) for the subsequent map overhaul.
- An armory with selectable portraits and one detail panel, plus tabs for rites, wagon upgrades, relics, and advice. Existing purchase, upgrade, equip, and progression actions remain connected to GameState.
- Compact endless preparation, challenge boards, LAN lobby, and settings tabs. Narrow store lists now use their available width.
- A battle HUD with health, courage, waves, time, icon commands, one row of unit/spell cards, optional intel, a pause card, and a full result report in a bounded reading area.
- Original generated art: camp, map, battlefield, twelve portraits, two structures, and six common unit poses. Special classes retain existing procedural rendering. Painted poses use movement bobbing and attack lunges; they are not full animation sheets.
- Twenty-five scalable navigation icons, shared colors, spacing, surfaces, headings, focus styles, and tooltips. The 1280 × 720 canvas letterboxes at other aspect ratios.

The generated files and exact prompts are listed in [generated-art.json](generated-art.json). These images were created using the built-in image generation tool. No external asset pack or downloaded font is required.

## Code cleanup and fixed defects

| Finding | Change and evidence |
| --- | --- |
| UI construction mixed into a very large battle controller | Extracted presentation methods into `BattleController.Ui.cs`. Shared primitives live in `RealmUi`, with game rules retained in their existing services. |
| Selecting an already armed unit or spell disarmed it | Selection is now idempotent. The real-input test selects and deploys a live unit; explicit cancellation remains available. |
| Desktop safe-area coordinates displaced the HUD | Desktop windows use zero display insets; mobile safe-area handling remains separate. |
| Stale children retained layout minima, and title refresh loops could spin forever while waiting for deferred deletion | Shared `TrimChildren` detaches before queueing deletion. All-menu checks repeat each available refresh twice. |
| Save rotation ignored filesystem failures and moved away the only primary save | Validate the staged JSON, copy a valid primary to backup, check copy/rename results, and retain the primary when backup creation fails. Save reload and victory persistence pass. Failure injection and power-loss durability are not covered by this pass. |
| Tower/arena/event results also changed campaign progression; losses or retreats could break campaign momentum | Restrict campaign victory, defeat, and retreat handlers to campaign mode. Mode-specific reward handlers remain responsible for their rewards. A deterministic tower victory records tower stars without changing campaign stars or the unlocked campaign stage. |
| Offline multiplayer was reported as a hosted LAN room | Require an actual ENet peer for room state; fresh-save checks assert that offline play has no LAN room. |
| A host's initial LAN snapshot overwrote a joining client's squad with an empty placeholder | Retain the local squad when applying peer snapshots. The existing two-process test now completes readiness, launch, result submission, scoreboard, and standings synchronization. |
| Boss rush led through preparation into a regular campaign battle | Its entry is disabled with an explicit in-development tooltip until that mode has a dedicated working battle flow. |
| Battle sprite mirroring shifted atlas characters away from their health bars | Mirror around the unit origin and position health bars above the painted character. |

## Readability and medieval styling follow-up

- Body text is now 18–20 px on the base canvas, with larger serif headings and numeric stats. The desktop game starts maximized; windowed and letterboxed layouts remain supported.
- Scalable enamel shields, brass laurels, engraved panel corners, and ornamental button frames add a consistent heraldic identity. These are code-native vector assets; the existing generated illustrations remain in use.
- Mission goals, enemies, field conditions, and briefing text have separate tabs. Full descriptions are retained. Deployment stays visible below the content.
- Armory lists show four portrait cards per page, with all unit and spell names wrapped in full. A larger selected portrait and three key stats lead to the complete training/traits dialog.
- Daily gifts show ten rewards per page across all thirty days. The storehouse has Gold, Rations, Bundles, and Purchase info tabs, with all products in a category visible together and duplicate reward descriptions removed.
- Multiplayer squad information now appears in the Squad tab. Wrapping prevents long unit/doctrine titles from stretching the entire board.
- Labels no longer use line-count truncation. Long button tooltips wrap to a bounded width. Codex, leaderboard, season pass, and battle HUD spacing accommodate the larger type.

The added typography suite inspects 25 menu scenes and their tabs, battle HUD text, every armory selection, populated results, and preparation/map text for all 60 campaign stages. It checks font sizes, glyph bounds, hidden lines, button text fit, fixed-panel overlap, and horizontal clipping inside vertical scroll areas. It also repeats the menu sweep at 1024 × 768. Intentional vertical scrolling remains available for complete reports and extended descriptions. This is layout coverage at the default text settings, not localization or screen-reader certification.

Run `scripts/smoke/typography_smoke.sh`; screenshots and structured text audits are written to ignored `artifacts/typography`, `artifacts/typography-advanced`, and `artifacts/typography-small` directories. All runs require isolated test saves.

## Validation

- `scripts/smoke/typography_smoke.sh`: zero layout/text failures in the menu and tab sweep (1,358 labels), extended armory/campaign sweep (2,929 labels), and smaller-window sweep. The driver explicitly sets and asserts 1280 × 720 or 1024 × 768 window dimensions; the latter letterboxes the game content.
- `scripts/verify_all.sh`: game builds with zero warnings/errors; **6,437 data checks** and **71 server tests** pass.
- `scripts/smoke/ui_review_smoke.sh`: opens the main journey screens and all 18 additional menus, captures rendered screenshots, and checks visible buttons against the viewport. Deliberately scrollable content is excluded from the viewport assertion.
- Core interaction checks cover map → preparation → battle, a single food charge, unit upgrade cost/level, unequip/re-equip, detail dialogs, keyboard/mouse deployment, pause/resume, intel, retreat, and save reload.
- The playthrough uses ordinary keyboard/mouse input and normal combat rules to win stage 1 and verifies stars on disk and return to the map. Its separate tower result regression invokes the actual result handler deterministically; it does not claim a tower combat playthrough.
- `scripts/smoke/lan_race_smoke.sh`: host and client pass with synchronized scoreboard and standings.
- All automated game runs use unique save suffixes. The user's normal campaign save was not reset or used for testing. The project application name remains unchanged to preserve the existing save directory.

Run the visual suite with Godot, .NET, and ripgrep on PATH. `GODOT_BIN` and `DOTNET_BIN` can override executable locations. Screenshots and run logs go to ignored `artifacts/ui-review/`.

## Remaining findings

These are existing product/release gaps found during review, not claims that the local campaign is broken:

1. **P1 — Online identity and cloud-save authorization.** `server/Endpoints.cs`, `PlayerProfile`, returns a predictable session string and marks a caller-supplied profile ID verified. `CloudSaveUpload` and `CloudSaveDownload` accept a profile ID without authenticating ownership. A caller who supplies another player's identifier can target their save. Replace this with authenticated sessions and enforce ownership on every player resource before a public online release.
2. **P1 — Purchase receipts are not verified with the platform.** `PurchaseValidate` checks presence, catalog membership, duplication, and request rate, then credits the purchase without validating the receipt with Apple/Google. Platform receipt verification and a required unique transaction identifier remain release work. This pass did not change payment handling or perform any purchase.
3. **P2 — Administration endpoints lack access control.** `server/Program.cs` exposes `/stats`, `/analytics/summary`, and `/admin/balance` without authentication. Restrict these to authorized operators before exposing the server publicly.
4. **P2 — Accessibility coverage remains incomplete.** The existing font offset changes fallback sizes, while many menus use explicit font sizes. High contrast currently affects combat characters/health bars rather than the entire interface. Maximum font size, screen readers, localization, and physical mobile devices need their own validation pass.
5. **P2 — Engine shutdown leaks.** Repeated scene smoke runs still report ObjectDB and occasional CanvasItem RID leaks at process exit. They do not fail the gameplay assertions, but their ownership should be traced with verbose engine diagnostics before long-session certification.

This review covered scene navigation/layout, rendering and asset loading, battle input/results, persistence, LAN readiness, and backend endpoint code alongside the existing data/server tests. It is not exhaustive balance testing of all sixty campaign stages or certification of all online services. Further extraction of the battle simulation should follow focused subsystem tests rather than a wholesale rewrite during a presentation change.
