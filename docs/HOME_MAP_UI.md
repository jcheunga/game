# Map home screen

The home and campaign scenes share a full-screen atlas. Gold, food and stars sit at the top left, settings at the top right, a compact zone title at the top center, and six illustrated tabs along the bottom. Shaded enamel panels have brass rims and inset highlights; navigation icons sit in circular medallions.

Each zone presents a medieval landscape with one point of interest per irregular terrain region. Curved boundaries, uneven site spacing, natural coastlines, winding rivers and mountain ridges replace the visible square lattice. Painted terrain materials texture the ground; native forest clusters, villages, farmsteads, docks, forge structures, forts, ruined arches, towers and supply wagons use zone-specific colours. Snowy highlands, marsh reeds and ruined arches give the districts distinct scenery. Roads and stone bridges connect the landscape. Explored regions have subtle thin borders, while selected sites have a stronger outline around their dry land. Stage markers show earned stars after completion. Resource markers expose rewards through tooltips, then disappear after collection. Unopened terrain uses a continuous dark veil whose edge follows the explored territory.

A fresh zone opens on its first stage with only that tile visible. Completing a stage or gathering a resource opens one ring of surrounding tiles, and survey charts follow the same neighboring-tile rule. Travelling and preparing, losing or retreating from a stage do not reveal new tiles. Travel is free; battle entry costs food. Empty ground cannot initiate travel. Reaching a destination is immediate, with no cart or travel animation. Dragging pans the map, and scrolling or pinching zooms it.

Quiet textured water fills the surroundings on the same 2:1 ground plane, world scale and camera as the zone map. A shallow shore connects it to the terrain, and a soft vignette frames the view. Undiscovered regions use a fully opaque textured cloud layer drawn over the background and landscape scenery, keeping hidden terrain and points of interest covered until those regions open.

The polished map uses nine painted surface materials and detailed scenery sprites. Foliage, mountain ridges, gatehouses, keeps, cottages, workshops, bridges and caches share consistent painted lighting and materials. Textures follow the 2:1 ground plane across region boundaries, while mipmaps smooth distant detail. Forest clearings and restrained texture contrast preserve marker readability. Harbor docks sit at the water's edge, bridges follow their crossing angles, and terrain borders pass beneath foreground scenery. Reduced motion freezes cloud drift and water drift. Source PNGs, submitted prompts and hashes are preserved in `assets/world/overworld/polished-v3/`.

Painted world objects serve as the site markers, with earned stars above cleared forts and no labels beneath any point of interest. Per-sprite ground anchors and shared zoom scaling keep buildings and hit targets aligned. Tile borders, selected-region outlines and low elliptical selection footprints are drawn beneath roads, rivers, bridges, buildings and foliage. Gold, provisions, books, essence and survey charts each have distinct painted art.

Native tooltips and site details retain point-of-interest names, rewards and stage entry costs at every scale. The map has no persistent captions, including during hover, focus and selection.

Travel to every open destination and resource collection are free, including with zero rations. Stage entry keeps its existing food charge on deployment, with one battle entry cost shown in stage details. Reaching a tile records the destination before collection or preparation without spending food. Insufficient entry rations are explained in stage details and cannot start a battle; resources remain collectible.

Save version 46 persists open tile IDs, reached tile IDs and the current caravan tile. Existing stars, claims and zone gates remain intact. Saves from the 60-stage campaign are renumbered onto the current stages. The former Lantern Camp tile is ordinary terrain with its revealed area retained; a caravan saved at a retired camp, shrine or tower returns to the first stage, and Shrine of Resolve courage blessings no longer apply. Older known sites translate to their new tiles; victories and collected resources reveal neighbors without granting rewards again. Legacy terrain data remains available for migration.

The transparent 3 × 3 icon atlas is `assets/ui/home/painted-icons-v2.png`; runtime atlas regions preserve the generated alpha. The icon artwork was generated with the built-in image tool. Exact prompts and provenance are recorded in `assets/ui/home/generated-art-v2.json`. Terrain regions and the additional landscape scenery are drawn natively at runtime.

The bottom tabs open Warband, Spells, Upgrades, Achievements, Codex and More. Warband, Spells and Upgrades select their corresponding armory pages. More preserves the Adventure, Caravan and Community destinations, account entry and quit. The main scene handles challenge deep links and opens directly into the map. Optional analytics and crash reporting are opted into under Settings → Account → Privacy, with no first-run prompt.

Site details start closed. Selecting a stage opens a single floating panel with rewards, its battle entry cost and preparation action. Victory gold, supplies and the entry ration cost use painted resource icons beside their amounts. Preparation presents rewards and the active warband, without briefing tabs or star-rating explanations. Compact unit cards and painted spell buttons keep the main preparation screen free of vertical scrolling, with spells and Deploy in the fixed action bar. Collection bursts and the Deploy cost use the same painted resource icons. Shared currency badges in activity menus and achievement rewards also use this artwork. Accessibility names retain the resource names. Boss gate and other battle entry requirements appear inside these details. Closing the panel or pressing Escape restores the map. The map has no bottom message popup for travel, discoveries, rewards, food requirements or other notices. Resource balances and site details still refresh when actions complete. Escape also closes More. HUD controls use safe-area insets and anchored positions.

Selecting a discovered gold or food cache immediately reaches and gathers it in
one action. Its marker disappears after successful collection, using the existing
saved visit flag so it stays gone after reloading. Collected caches cannot be
selected again, and gathering leaves no site-details panel open. Unreached
resources can also be collected with zero rations. Tile
discoveries also disappear after collection; rival markers remain available on the map.

Only King's Road is available in a new campaign's zone navigation. Defeating a zone's boss reveals the next zone, which replaces the current map. Earlier areas remain reachable. Existing saves retain their played zones and preceding areas, based on victories, visited landmarks or recorded exploration. The highest unlocked stage number does not open a zone by itself. This gate controls home/map navigation; encounter rules and other game modes retain their existing behavior.

In editor and debug builds, Settings has a Developer tab. Developer mode starts
off. Enabling it offers +1,000/+10,000 gold and +10/+100 food in Settings,
with +1,000 gold and +100 food shortcuts below the map balances. Switching it
off hides the map shortcuts and disables grants. The mode and balances persist
with the local save. Food grants may exceed the passive recharge cap. Grants
bypass billing, purchase counts, achievements and map progression. Release
builds hide the tab and reject developer grants. The console also supports
`dev on`, `dev off`, `gold <amount>` and `food <amount>` through the same grant
path.

Developer controls are covered by native-click checks in the home review and
the focused `--developer` review. Checks cover each amount, immediate balances,
saved mode and resources, disabled grants, overflow rejection, unchanged
purchases/progression and map preservation. Both desktop and small-window home
reviews pass with zero failures (`dev-mode-home-desktop.log` and
`dev-mode-home-small.log`).

Implementation lives in `MapMenu.Home.cs`, shared by `MainMenu` through `MapMenu`. Presentation primitives live in `HomeMapUi`, `HomeMapFrame` and `HomeMapArt`; terrain presentation lives in `MapPathCanvas.Art.cs`. `AdventureTileCatalog` and `GameState.AdventureTiles.cs` own tile layout and progression. The zone access query remains in `GameState.Adventure.cs`. Scene routing carries a one-use armory page selection.

## Modal presentation

Home destinations open inside `RealmModal` over the existing map. Dismissal and
back navigation preserve the camera, exploration and resources. Campaign and
other battle starts still enter the real battle scene. Account changes and save
restores explicitly reload home. Keyboard focus remains inside an open panel;
Escape and the outer veil dismiss it.

Returning to an activity from battle creates the map shell before opening the
same modal used by home navigation. Quitting battle returns directly to the map;
Endless still banks its earned rewards once. Results can explicitly reopen
Endless, Tower, Arena, seasonal events or challenge preparation. Creating the
home shell preserves the selected battle stage even when its campaign zone is
locked. Phone modals use the shared HUD scale, a compact header, scrolling bodies
and fixed launch actions that fit inside the content area.

The panels have a separate material language from the teal map HUD: walnut and
steel for the warband, violet cloth for spells and relics, copper for wagon
upgrades, and an illustrated parchment spread for the codex. Bolted silver
bevels, muted category tabs, aged-gold primary actions and oxblood close controls
are drawn at the actual control size. Painted vignettes stage the authentic
animated unit and spell previews and decorate mode cards. Audio sliders use
amber tracks and carved handles. Achievement category headers and progress
strips distinguish progress, completion and claimable rewards.

The control palette uses weathered iron and aged brass. Inactive tabs stay
close to charcoal and brown; selected states retain subdued slate, plum, ochre,
forest and oxblood pigments. Metal highlights are soft and thin, keeping the
painted illustrations as the richest colours in the panels.

Unit and spell profiles use a compact collection sidebar, a tall painted preview
and icon stat cards. Equip and training actions stay outside the details scroller.
Traits, doctrines, talents, colour variants and next-level values are available
through an optional disclosure. Spell cards show effect-specific values from the
trained spell rather than the base definition. Battle preparation opens the same
visual unit and spell inspectors without changing the loadout.

The shared theme reserves an 18-pixel gutter beside vertical scrollbars and a
12-pixel gutter above horizontal scrollbars. This spacing is separate from panel
padding and applies to menus, overlays, inspectors, dialogs and battle UI.

The Royal Storehouse has a dedicated layout with compact pack cards. Each card
shows its name, total supplies, included bonus and a purchase button fixed at
the bottom. Descriptions and full bundle terms open through Details. Gold,
Rations and Bundles keep every purchase action visible without scrolling;
the gold-funded ration refill and recharge information share one row. A visible
footer identifies the pack and price before confirmation, offers Cancel and
shows refill results. Changing categories cancels a pending confirmation.
Purchase info retains the full payment status, and owned one-time bundles stay
disabled. The modal uses the existing shell's navigation.

`ModalUi`, `ModalSurface` and `ModalArt` own this presentation. Legacy activity
screens are reflowed into scrollable bodies with their action bars kept outside
the scroller. `ModalActivityLayout` also dresses rows rebuilt by those screens.
The versioned atlas assets and generation prompts are in `assets/ui/modal/`.

## Verification

Build and run the home review with an isolated save:

```sh
dotnet build Game.csproj
godot --path . --windowed --disable-render-loop --rendering-method gl_compatibility res://scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-home-UNIQUE --home-map
```

Add `--small-window` for a 1024 × 768 window. Captures and typography audits are written to `artifacts/tile-map/{desktop,small}`. The review exercises tile revelation through completion, free travel, entry-only charges, native resource collection with zero rations, actual drag panning, zoom, details, settings return, armory actions, codex, sequential zone access, save persistence and existing-save compatibility. A charted test fixture provides an explored-area visual review and checks all site panel text without changing the player's save or granting discovery rewards.

Use `--map-notices` in place of `--home-map` for the focused boss-warning and
message-removal regression. The full home review includes it as well. It checks
native close-button clicks, Escape, reopening settings, repeated rejected
travel and refreshes, absence of the bottom popup, and unchanged boss
progression, resources and fog knowledge.

Use `--storehouse` for the focused store layout review, also included in the
full home review. It checks all ten purchase buttons and their native first-tap
confirmations, cancellation, complete bundle details, a gold-funded food refill,
owned-bundle disabling and map preservation. Payment confirmation is never
executed by the fixture. Add `--small-window` to verify the smaller window.

Use `--map-rewards` for native cache and tile-reward selection checks, also
included in the full home review. It verifies the advertised resource amounts,
marker removal, saved claims, duplicate prevention, interrupted and blocked
travel, and permanent landmark visibility at either window size.

Use `--armory-details` for the focused profile review. It covers every unit and
spell, current trained stats, native upgrades, promotion, doctrine selection,
optional details, fixed actions and both preparation inspectors. Add
`--small-window` for the smaller layout. Captures and typography audits are in
`artifacts/armory-details/{desktop,small}`.

Add `--scroll-spacing` to a home or typography review to measure content clearance
from visible scrollbar rails. The audit checks actual laid-out content rather than
the theme settings. `--typography` covers standalone menus and their tabs;
`--home-map` covers their home overlays. Use `--small-window` for the smaller size.

The standard UI and world-art fixtures explicitly select/open sites or unlock the district under review. Full repository verification remains `./scripts/verify_all.sh`.

Visual-update verification on 2026-10-01: the build passed without warnings or errors. Desktop and smaller-window home reviews each passed 38 behavior checks and 480 text checks with no failures or text issues. The exploration review passed 96 checks, including persistent fog knowledge, reduced motion, resource discovery, exhaustion, free returns and interrupted travel. Fresh, panned, first-discovery, explored and site-panel captures were visually inspected. As in earlier reviews, Godot reports existing resource cleanup warnings when the test process exits.

Fog-shape follow-up verified on 2026-10-02: the build passed without warnings or errors. Both window sizes passed the same 38 behavior and 480 text checks. All 99 exploration checks passed, including a rounded reveal with concealed corners, visible known ground centers, a concealed distant boss and a retained travel route. Fresh and first-discovery fog edges were visually inspected. A stable preview is saved at `artifacts/home-map/fog-rounded-preview.png`.

Modal material redesign verified on 2026-10-02: the build passed without warnings
or errors. Desktop and smaller-window reviews each passed 79 behavior checks
and 1,859 text checks with zero failures. Checks cover actual recruit/equip/
upgrade actions, spell training, reward claims, all home activity destinations,
map preservation, keyboard focus, settings persistence and native campaign and
endless battle launches. Painted warband, spells, upgrades, settings,
achievements, codex and mode panels were visually inspected. Godot still emits
the previously observed resource cleanup messages when these fixtures exit.

Boss-warning dismissal verified on 2026-10-02: the new regression reproduced
the warning returning after the details were closed. The fix passed the full
home review at both window sizes, including native dismissal clicks, Escape,
expiry and replacement-message timing. Boss requirements remain enforced and
dismissal does not change resources or exploration knowledge.

Storehouse cleanup verified on 2026-10-02: the build passed without warnings or
errors. Desktop and smaller-window home reviews each passed 134 behavior checks
and 2,312 text checks with zero failures. All ten pack actions and confirmation
states fit without scrolling, and Gold, Rations and Bundles were visually
inspected. The review also confirmed cancellation, full bundle details, refill
feedback, one-time ownership and unchanged map exploration.

Reward removal verified on 2026-10-02: the build passed without warnings or
errors. Both home reviews passed 189 behavior checks and 2,194 text checks with
zero failures, and all 99 exploration checks passed. Native selections covered
gold and food caches, the forgotten treasury and all five tile reward types.
Collected markers stayed removed after saving and rebuilding the map; blocked
or interrupted travel kept unreached rewards available. Before/after cache
captures were visually inspected.

Bottom message popup removed on 2026-10-02: the map no longer creates the
notification panel, dismiss control or expiry timer. The build passed without
warnings or errors. Both home reviews passed 190 behavior checks and 2,193 text
checks with zero failures, including reward collection, blocked travel, boss
details, map navigation and modal actions. The collected-reward map capture
was visually inspected with the bottom area clear.

Tile atlas verified on 2026-10-02: the final build passed without warnings or errors. Desktop and smaller-window home reviews each passed 245 behavior checks and 1,580 text checks with zero failures or text issues. All ten zone themes, the fresh foothold, resource reveals and compact stage costs were visually inspected. Checks cover completion-driven revelation, separate travel and entry costs, actual deployment, native resource taps, blocked claims, duplicate prevention, cancellation, reduced motion, full disk reload, version 44 landmark migration, boss and zone gates, storehouse, developer supplies and home overlays. The repository verification also passed, including all 86 server tests and the public website build. Captures and logs are in `artifacts/tile-map/`.

Medieval landscape follow-up verified on 2026-10-02: the build passed without warnings or errors. Desktop and smaller-window reviews each passed 295 behavior checks and 1,580 text checks with zero failures, text issues or runtime errors. The additional checks validate curved terrain polygons and all 630 site positions across the ten zones, including coastline containment, dry land and matching hit tests. Fresh exploration, the fully charted King's Road, Saltwake docks, Emberforge and snowy Thornwall were visually inspected, along with smaller-window stage details. Captures remain in `artifacts/tile-map/`; review logs are in `artifacts/landscape-map/`.

Background and opaque fog follow-up verified on 2026-10-02: the build passed without warnings or errors. The desktop tile review passed 119 behavior checks and 241 text checks. The full smaller-window home review passed 295 behavior checks and 1,580 text checks, with no failures, text issues or runtime errors. Fresh-map captures at both sizes and the Saltwake background were visually inspected. The shorter smaller-window run completed its behavior checks but reported resources still in use during immediate shutdown after battle; the full home review returned to the map and exited cleanly. Logs are in `artifacts/map-background/`.

Painted map polish verified on 2026-10-02: the build passed without warnings or errors. Desktop and smaller-window home reviews each passed 295 behavior checks and 1,580 text checks with zero failures, text issues or runtime errors. The review exercised all ten zones; fresh exploration, Saltwake, Emberforge, snowy Thornwall and smaller-window stage costs were visually inspected. Terrain materials, fitted scenery, textured waterways, opaque cloud fog and the sharper parallax background preserve existing exploration and food costs. Captures are in `artifacts/tile-map/`; final logs are in `artifacts/polished-map/`.

Grounded site markers verified on 2026-10-02: the build passed without warnings or errors. Desktop and smaller-window home reviews each passed 297 behavior checks and 1,468 text checks with zero failures, text issues or runtime errors. Checks include the removed Map guide, native selection of a painted fort after zooming and panning, every pickup type, duplicate prevention and battle launch. The first review exposed a transparent landmark margin intercepting a neighboring gold pickup; fitted alpha hit masks fixed that interaction, and both final reviews passed. Fresh exploration, earned stage stars, Saltwake, snowy Thornwall, overview label density and smaller-window stage costs were visually inspected. Final logs are in `artifacts/grounded-map/`; captures remain in `artifacts/tile-map/`.

Shared background plane verified on 2026-10-02: the build passed without warnings or errors. Desktop and smaller-window home reviews each passed 297 behavior checks and 1,468 text checks with zero failures, text issues or runtime errors. The scenic backdrop, separate parallax and offset coastal shadows are removed. Surrounding water uses the zone's world transform and the river material's 2:1 projection and scale. Fresh exploration, Saltwake, Emberforge, snowy Thornwall and smaller-window stage details were visually inspected. Final logs and the stable preview are in `artifacts/map-plane/`; all ten zone captures remain in `artifacts/tile-map/`.

Resource icons and single-page details verified on 2026-10-02: the build passed without warnings or errors. Desktop and smaller-window home reviews each passed 303 behavior checks and 1,521 text checks with zero failures, text issues, runtime errors or shutdown warnings in the final runs. Native checks cover the removed Intel tab, configured victory rewards, separate ration costs, every pickup type, blocked claims, achievement claims and real battle deployment. Stage details, blocked supplies, the achievement reward cards and the deployment button were visually inspected. Final logs and a stable stage-panel preview are in `artifacts/resource-icons/`.

Unit and spell profiles redesigned on 2026-10-02: the build passed without warnings or errors. Desktop and smaller-window home reviews each passed 303 behavior checks and 1,543 text checks. The focused profile reviews each passed 42 behavior checks and 1,613 text checks, with no failures, text issues, runtime errors or shutdown warnings. Native checks cover every unit and spell, current trained values, upgrades, promotion, doctrine selection, optional training details, fixed actions and visual battle-preparation inspectors. Unit, healing spell, trained Fireball and both preparation inspectors were visually inspected. Final logs, focused captures and stable unit/spell previews are in `artifacts/armory-details/`.

Scroll spacing verified on 2026-10-02: the build passed without warnings or errors.
At each window size, 56 standalone menu states and 59 home-overlay states covered
130 scrolling regions and 62 visible rails, with no spacing failures. Standalone
reviews each passed 1,528 text checks; home reviews each passed 303 behavior checks
and 1,543 text checks, with no layout failures or runtime errors. The wider review
also fixed undersized armory portrait captions and a daily-challenge heading that
forced its scrolling panel outside the screen. Player profile and daily challenge
captures were visually inspected. The standalone review retains existing
CanvasItem cleanup warnings at process exit; home reviews exit cleanly. Logs and
stable profile previews are in `artifacts/scroll-spacing/`.

Startup analytics modal removed on 2026-10-02. A fresh save opens directly into
the map, while optional upload choices stay disabled until enabled in Settings.
The build passed without warnings or errors, and the native privacy review passed
all 27 checks, including startup and Settings captures. The capture fixture retains
its existing resource-cleanup diagnostics at process exit. Evidence is in
`artifacts/privacy-review/`.
