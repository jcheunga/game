# Map home screen

The home and campaign scenes share a full-screen atlas. Gold, food and stars sit at the top left, settings at the top right, a compact zone title at the top center, and six illustrated tabs along the bottom. Map controls float at the lower left; Map guide sits at the lower right. Shaded enamel panels have brass rims and inset highlights; navigation icons sit in circular medallions.

Each zone presents a medieval landscape with one point of interest per irregular terrain region. Curved boundaries, uneven site spacing, natural coastlines, winding rivers and mountain ridges replace the visible square lattice. Existing zone paintings texture the ground; native forest clusters, villages, farmsteads, docks, forge structures, forts, shrine arches, towers and supply wagons use zone-specific colours. Snowy highlands, marsh reeds and ruined arches give the districts distinct scenery. Roads and stone bridges connect the landscape around the lantern caravan. Explored regions have subtle thin borders, while selected sites have a stronger outline around their dry land. Stage pins show earned stars after completion. Resource markers show their reward and travel price, then disappear after collection. Unopened terrain uses a continuous dark veil whose edge follows the explored territory.

A fresh zone opens at the camp with the eight neighboring tiles revealed. Completing a stage or gathering a resource opens the eight surrounding tiles; towers and survey charts open two rings. Travelling and preparing, losing or retreating from a stage do not reveal new tiles. Empty ground no longer moves the player. Selecting a destination animates the caravan directly there, with reduced motion skipping the animation. Drag, zoom and Find Caravan remain available.

The zone painting fills the surrounding background in subdued colours. Undiscovered regions use a fully opaque fog layer drawn over the background and landscape scenery, keeping hidden terrain and points of interest covered until those regions open.

Travel to a new destination costs 1 food; returns to reached tiles are free. Stage entry keeps its existing separate food charge on deployment. The stage details show both costs, and new-stage travel requires enough food for travel and entry. Costs and arrival commit only when the animation finishes. Leaving earlier cancels without spending travel food or claiming rewards. Blocked cache selection displays the food requirement inside site details.

Save version 45 persists open tile IDs, reached tile IDs and the current caravan tile. Existing stars, claims, shrine bonuses and zone gates remain intact. Older known sites translate to their new tiles; victories and collected resources reveal neighbors without granting rewards again. Legacy terrain data remains available for migration.

The transparent 3 × 3 icon atlas is `assets/ui/home/painted-icons-v2.png`; runtime atlas regions preserve the generated alpha. Backgrounds and icon artwork were generated with the built-in image tool. Exact prompts and provenance are recorded in `assets/ui/home/generated-art-v2.json`. Terrain regions and the additional landscape scenery are drawn natively at runtime.

The bottom tabs open Warband, Spells, Upgrades, Achievements, Codex and More. Warband, Spells and Upgrades select their corresponding armory pages. More preserves the Adventure, Caravan and Community destinations, account entry and quit. Startup analytics consent and challenge deep links remain in the main scene.

Site details start closed. Selecting a landmark opens a floating panel with Overview, Intel and the existing travel/battle action. Boss gate and other battle entry requirements appear inside these details. Closing the panel or pressing Escape restores the map. The map has no bottom message popup for travel, discoveries, rewards, food requirements or other notices. Resource balances and site details still refresh when actions complete. Escape also closes More. HUD controls use safe-area insets and anchored positions.

Selecting a discovered gold or food cache sends the caravan to gather it in one
action. Its marker disappears after successful collection, using the existing
saved visit flag so it stays gone after reloading. Collected caches cannot be
selected again, and gathering leaves no site-details panel open. Unreached
rewards remain available if travel is interrupted or food runs out. Tile
discoveries also disappear after collection; camps, shrines, watchtowers and
rival markers remain available on the map.

Only King's Road is available in a new campaign's zone navigation. Defeating a zone's boss reveals the next zone, which replaces the current map. Earlier areas remain reachable. Existing saves retain their played zones and preceding areas, based on victories, visited landmarks or paid exploration. Stage numbers cross district boundaries, so a high stage number by itself does not reveal other zones. This gate controls home/map navigation; encounter rules and other game modes retain their existing behavior.

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

Add `--small-window` for a 1024 × 768 window. Captures and typography audits are written to `artifacts/tile-map/{desktop,small}`. The review exercises tile revelation through completion, travel and entry charges, native resource collection, actual drag panning, zoom, details, settings return, armory actions, codex, sequential zone access, save persistence and existing-save compatibility. A charted test fixture provides an explored-area visual review and checks all site panel text without changing the player's save or granting discovery rewards.

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
