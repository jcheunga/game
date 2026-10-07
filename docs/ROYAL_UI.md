# Crownroad royal interface

Every screen is built from the approved concept screenshots in `output/` (the remaining eight ui-ideas and the
ten menu concepts; the battle HUD follows `02-clean-steel-battle`, the map `06-cartographers-atlas-home`). The
game canvas is a fixed 1280×720, so each concept is drawn at canvas scale and live content is placed at the
positions measured on it.

## How a concept screen is made

1. **Plate.** `art/royal/codexgen.py` asks Codex's image tool to repaint each concept as a *clean plate*: the same
   frame, panels, buttons and painted backdrop with every word, number, icon and item removed, and the area
   outside the modal transparent (`art/royal/jobs/01-plates.json`). `art/royal/plates.py` installs them in
   `assets/ui/royal/plates/`.
2. **Spec.** `art/royal/specs/<screen>.json` measures the concept: every text run (font, weight, size fitted to the
   lettering's width, pen position, baseline, colour) and every element rect. `art/royal/specview.py` overlays a
   spec on its reference to check it to the pixel; `grid.py` zooms a region with coordinates. Specs install to
   `assets/ui/royal/specs/` and screens read them at runtime by element id (`RoyalSpec`), so layout lives in one
   place.
3. **Kit.** `art/royal/cuts.py` cuts icons, selected states, card frames, buttons and ornaments from the concepts
   or plates into `assets/ui/royal/kit/` (`art/royal/cuts.json`). Light icons are keyed to alpha; painted pieces
   are nine-sliced at their source density (`SliceStyle`), so they stay sharp on Retina displays.
4. **Screen.** A `RoyalScreen` draws its plate and builds live labels (`RoyalLabel`, `RoyalText`) and controls
   (`RoyalButton`) over it. Text uses Cinzel for engraved capitals and Crimson Pro for book text
   (`assets/fonts`, OFL); large titles use the gold gradient shader `assets/shaders/royal_gold_text.gdshader`.

Screens that had no concept (tower, storehouse, arena, guild and the other activities, inspectors, dialogs) keep
their layouts and take the same chrome: `ModalSurface` draws the kit's modal frame, navy and gold buttons, tabs,
tiles and parchment, `RealmModal` uses the concept header with its castle skyline and a gold title, and
`RealmUi` uses the concept fonts.

## Screens

| Screen | Concept | Code |
| --- | --- | --- |
| Home atlas and HUD | ui 06 | `MapMenu.Home.cs`, `MapPathCanvas.Painted.cs` |
| Caravan hub (More) | menu 01 | `CaravanHub.cs` |
| Warband / Spells | ui 07, menu 02 | `ShopMenu.cs`, `ShopMenu.Roster.cs`, `ShopMenu.Profile.cs` |
| Relics | menu 03 | `ShopMenu.Relics.cs` |
| War wagon workshop | ui 08 | `ShopMenu.Wagon.cs` (an armory tab: the workshop scene is fitted inside the relics frame; three pages cover all 13 upgrades) |
| Achievements | menu 04 | `AchievementsPanel.cs` (six per page) |
| Codex | menu 05 | `CodexMenu.cs` |
| Settings | menu 06 | `SettingsMenu.cs` (every former setting is a row) |
| Endless survival | menu 07 | `EndlessMenu.cs` |
| Multiplayer challenges | menu 08 | `MultiplayerMenu.Royal.cs` (code, LAN and records in the code menu) |
| Relic forge | menu 09 | `ForgeMenu.cs` |
| Season rewards | menu 10 | `SeasonPassMenu.cs` |
| Prepare for battle | ui 09 | `LoadoutMenu.cs` |
| Battle HUD | ui 02 | `BattleController.Menu.cs`, `hud/RoyalMeter.cs`, `hud/BattleActionCard.cs` (courage on the top plate, mana below; mana's blue pieces and courage's amber fill from `art/royal/mana.py`) |
| Victory / defeat | ui 10 | `royal/RoyalResult.cs` (LAN and online rooms keep their scoreboard panel) |

Screens without a concept, and the inspectors opened over screens, share `RealmModal`'s chrome: the
`modal-header` band (cut by `art/royal/chrome.py`, with the plate's painted close button removed because the live
close button sits there) and the kit's frame, tabs and buttons. Clicking outside a royal screen's painted frame
dismisses it like a backdrop.

## Battlefields

Each zone has five painted parallax layers (`art/royal/jobs/03-backdrops.json`, assembled by
`art/royal/backdrops.py` into `assets/world/royal/`): a far sky and landmark vista (0.12), a mid row of distant
scenery (0.42), the road with its fence and verge and the row of scenery lining it (locked to the field), and a
blurred foreground drawn in front of the troops (1.35). Row layers repeat seamlessly along the field. The road
sits at the concept's screen rows (411–516), which `BattleController.Camera` holds with `BandRowFraction` .81.
The same script composes a still of each battlefield for mission and route cards (`assets/ui/royal/missions`).

## Map

`MapGuideRenderer` (`UiReviewSmoke --map-guides`) draws each zone's own geography (coast, river, bridges, roads and
site points) as a flat colour guide with `crops.json` (its world rect). A zone is too wide for one Codex image
(about 1.6 megapixels) to stay sharp when zoomed in, so `art/royal/mapsections.py run` has Codex paint it as a
mosaic of overlapping 16:9 sections (`GRID` × `GRID`, 4 × 4 by default) in diagonal waves from the north-west, each
edited from a target that already carries its finished neighbours' painted strips so the painting continues across
the joins, with the zone's previous painting of the same area as a content reference. `mapsections.py stitch`
colour-matches the sections, feathers them together and installs the map as 2 × 2 GPU-compressed texture tiles
(`assets/world/royal/maps/<zone>-<k>.jpg`, none wider than 4096 px) listed with the world rect in `<zone>.json`,
so roads, river and clearings line up with the playable tiles. A zone painted before the mosaic has one
`<zone>.jpg`. Sites use painted landmark sprites (`assets/world/royal/maps/landmarks`, installed by
`art/royal/maps.py`), stars sit above castles, hidden tiles lie under a storm-cloud bank and frontier tiles under
thin mist: `MapFogLayer` draws blurred world-space masks of both through `assets/shaders/royal_fog.gdshader`,
which frays them into lit, drifting billows using the tileable noise baked by `art/royal/fognoise.py`. Landmarks,
price tags, reward bursts and the vignette are drawn above it by `MapOverlayLayer`.

Tapping a site that can be entered goes straight in: the caravan travels there and a battle opens its
preparation (which shows the rewards and entry cost). The site panel only appears to explain why a site is
blocked, such as a sealed boss gate or missing rations.

## Items

Relics and spells are painted images (`art/royal/jobs/02-items.json`, installed by `art/royal/items.py`),
not model renders, and so are the hub activities and wagon upgrades (`06-activities.json`, `07-upgrades.json`,
installed by `art/royal/pictures.py`). `UiArtLoader` uses them wherever an item is shown; the codex shows units as
their battle figures and keeps portraits only for foes without one.

## Review

```sh
dotnet build Game.csproj --no-restore
godot --path . --windowed --rendering-method gl_compatibility res://scenes/tests/UiReviewSmoke.tscn -- --save-suffix=ui-review-concepts --concepts
python3 art/royal/compare.py warband spells relics hub
```

`--concepts` seeds a save shaped like the concept mock-ups and captures every concept screen through production
navigation into `artifacts/royal-ui/capture`; `--zones=city,harbor,…` adds a battle capture per zone.
`compare.py` writes side-by-side and blended comparisons with the references to `artifacts/royal-ui/compare`.
The other drivers (`--royal-ui`, `--typography`, `--core`, `--live-parity`, `MobilePresentationReview`,
`CombatReviewSmoke --regressions`) cover behaviour and layout audits; run them one at a time.

The raw Codex outputs live in `art/royal/gen/` (git-ignored, about 300 MB); the trimmed, installed copies are in
`assets/`.
