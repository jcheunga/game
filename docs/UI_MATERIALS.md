# Shared UI materials

The game UI now uses one native vector material library: dark, lightly grained
leather/steel faces, aged-brass edges, recessed wells, and enamel meter fills.
The texture stays quiet behind text; primary actions use a warmer brass face.
Selected buttons have a lower accent line as well as a depressed surface, and
keyboard focus has a separate bright outline.

## Editable sources

- `art/ui/build_surfaces.py` generates the 32 SVGs in `assets/ui/frames/`.
- `scripts/ui/shared/MedievalUi.cs` supplies the shared control theme and slicing.
- `scripts/ui/shared/UiSurfaceStyle.cs` layers a tintable body and rim for cards
  and badges, preserving their existing selection/reward colours.
- `scripts/combat/hud/BattleHudBar.cs` uses the shared meter track and fill for
  courage and wave progress, including outlined values and high-contrast edges.

Regenerate with `python3 art/ui/build_surfaces.py`, then let Godot import the SVGs.
No external artwork, bitmap-generation service, shader, or SVG filter is needed.
Godot's importer did not render SVG pattern fills in the review, so grain is
expanded into ordinary vector paths. The material test checks decoded texture
pixels to prevent an apparently successful import from silently losing grain.

Large panel and inset centres tile at a fixed scale. Edge lighting is confined to
the fixed slices so resizing does not repeat large lighting gradients. Button
faces tile horizontally and stretch vertically to follow the existing controls.
ResourceLoader caches the imported textures; no textures are generated per frame.

## Coverage and boundaries

The shared finish covers menu and battle panels, ordinary/primary/icon buttons,
tabs, focus/hover/pressed/disabled states, selected cards, badges, input fields,
drop-downs and popups, checks/radios, scrollbars, separators, tooltips, dialogs,
progress bars, custom battle meters, the field minimap, boss banners, and
placement/objective label backings. Existing illustrated icons,
backgrounds, map tokens, and the previously textured unit health bars remain.

Content padding and font sizing are retained. Mobile touch-button styling keeps
the primary-action material instead of replacing it with the ordinary surface.
No combat rules, touch targets, camera settings, or save progression are changed
by this material pass. Platform-owned window chrome and development/debug UI are
outside the shared game theme.

## Verification

Use isolated saves for all review commands; the review scenes modify test state.

```sh
dotnet build Game.csproj --no-restore
godot --path . --scene res://scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-materials-UNIQUE --materials
godot --path . --scene res://scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-textures-UNIQUE --typography
godot --path . --scene res://scenes/tests/MobilePresentationReview.tscn -- --save-suffix=mobile-review-textures-UNIQUE
```

The material review checks imports, visible grain, tile modes, retained padding,
mobile primary styling, focus, and shared control styles. It also captures normal
and high-contrast material sheets in `artifacts/ui-material-review/`.
The full typography review visits 25 menus plus battle and their available tabs;
screenshots and measured text/layout results are in `artifacts/typography/`.
Phone-sized rendering is captured in `artifacts/mobile-review/`; it is desktop
simulation, not a physical iOS/Android device certification.

### Shared-material baseline review — 29 September 2026

These baseline counts precede the icon-first deployment-card follow-up. See
`docs/DEPLOYMENT_CARDS.md` for its updated layout and verification.

- Build: zero errors and warnings.
- Material review: 51 checks passed, including all 31 imported surfaces and
  decoded panel grain; normal and high-contrast material sheets reviewed.
- Shared menu typography: 58 screen/tab captures, 1,321 text entries, no detected
  layout failures across 25 menus and battle.
- Advanced typography: all armory pages plus map/preparation for all 60 campaign
  stages, 2,741 text entries, no detected layout failures.
- Phone-size review: 146 checks passed, including model visibility, cards,
  scrolling, touch input, reports, pause/results and inspection.
- Functional UI review: 12 checks passed, including details dialogs, upgrades,
  equip/unequip, deployment charges, battle input, and isolated save reload.

The all-menu review emitted a shutdown-only CanvasItem/ObjectDB leak warning;
the material, phone-size, functional and advanced reviews exited without that
warning. Editor import also reports the existing Android shutdown-setting error
after importing successfully. Physical-device rendering/performance remains to
be checked before release.
