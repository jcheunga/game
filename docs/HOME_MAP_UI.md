# Map home screen

The home and campaign scenes share a full-screen atlas. Gold, food and stars sit at the top left, settings at the top right, a compact zone title at the top center, and six illustrated tabs along the bottom. Map controls float at the lower left; Explore stays at the lower right. Shaded enamel panels have brass rims and inset highlights; navigation icons sit in circular medallions.

King's Road uses a new complete painted coastal kingdom at `assets/world/overworld/city-painted-v2.png`. The continuous illustration is drawn once across the world instead of stretching a small ground sample over repeated tiles. Other zones retain their existing illustrations. Each 3968 × 2176 zone opens close to the caravan, spanning about three screens in each direction at the standard viewport. Dragging pans across it; zoom-out is limited so exploration still requires panning. Find Caravan returns to the current caravan position.

Animated fog fully conceals uncharted terrain. Travel and scouting reveal the painted landscape through the persisted knowledge mask; merely panning never reveals terrain or spends food. Charted ground stays visible under a light haze outside caravan sight. Undiscovered sites and rewards remain hidden. Collision-aligned water and rocks appear when charted, with rounded shores and painted scenery. Food costs, discovery rules and travel coordinates remain the same.

Fog uses a continuous 496 × 272 world-space mask derived from the same saved tile knowledge. Rounded reveals merge into organic banks; their vertical scale is compressed to match the painted ground perspective. Noise and drift use that same projection, with a feathered, gently distorted outline instead of exposing tile edges. Known marker positions stay clear as the edge animates. Panning and zooming keep fog attached to the landscape.

The transparent 3 × 3 icon atlas is `assets/ui/home/painted-icons-v2.png`; runtime atlas regions preserve the generated alpha. Background, icons and scenery were generated with the built-in image tool. Exact prompts and provenance are recorded in `assets/ui/home/generated-art-v2.json`.

The bottom tabs open Warband, Spells, Upgrades, Achievements, Codex and More. Warband, Spells and Upgrades select their corresponding armory pages. More preserves the Adventure, Caravan and Community destinations, account entry and quit. Startup analytics consent and challenge deep links remain in the main scene.

Site details start closed. Selecting a site opens a floating panel with Overview, Intel and the existing travel/battle action. Closing the panel or pressing Escape restores the map. Escape also closes More. HUD controls use safe-area insets and anchored positions.

Only King's Road is available in a new campaign's zone navigation. Defeating a zone's boss reveals the next zone, which replaces the current map. Earlier areas remain reachable. Existing saves retain their played zones and preceding areas, based on victories, visited landmarks or paid exploration. Stage numbers cross district boundaries, so a high stage number by itself does not reveal other zones. This gate controls home/map navigation; encounter rules and other game modes retain their existing behavior.

Implementation lives in `MapMenu.Home.cs`, shared by `MainMenu` through `MapMenu`. Presentation primitives live in `HomeMapUi`, `HomeMapFrame` and `HomeMapArt`; terrain presentation lives in `MapPathCanvas.Art.cs`. `AdventureFogMask` builds the continuous reveal mask for `fog.gdshader`. The zone access query lives in `GameState.Adventure.cs`. Scene routing carries a one-use armory page selection.

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

Add `--small-window` for a 1024 × 768 window. Captures and typography audits are written to `artifacts/home-map/{desktop,small}`. The review exercises the fresh layout, actual drag panning, zoom limits, caravan focus, fog revelation through travel, details, More sections, settings return, armory page routing, codex, sequential zone access, selected-zone persistence and existing-save compatibility. A charted test fixture provides an explored-area visual review and checks all site panel text without changing the player's save or granting discovery rewards.

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
