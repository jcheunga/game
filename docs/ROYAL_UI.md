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

The More hub's activities that had no concept (tower, bounties, boss rush, raid, event, expeditions, daily
gifts, progression, guild, friends, arena, rankings, and the LAN race reached from Challenges) are painted **boards** (`royal/RoyalBoard.cs`,
below). Inspectors, dialogs and the few remaining screens without a concept keep their layouts and take the same
chrome: `ModalSurface` draws the kit's modal frame, navy and gold buttons, tabs, tiles and parchment, `RealmModal`
uses the concept header with its castle skyline and a gold title, and `RealmUi` uses the concept fonts.

## Boards

A board is a concept plate repainted by Codex into a plain, reusable one (`art/royal/jobs/15-boards.json`): the
frame, the navy header with its castle skyline and the props over its corners stay, the screen's own panels go.
`board-banners` comes from the achievements plate (teal banners down both sides, lanterns and crates in the
bottom corners); `board-camp` from the season plate (lantern, coins, scroll and crates over the bottom-left).
`RoyalBoard` knows each board's geometry: the body, the footer row clear of the props, the corner the props cover
and, on the banners board, the top-left corner its hanging banner covers (row captions sit beside it, panels
start below it). A screen subclasses it with a title and a header emblem and composes its sections in `Compose`
from kit pieces at canvas positions, as the concept screens do: `Section` (gold-rimmed panel with an engraved
heading), `Picture` (a painting in a gold rim), `Plaque`, `Meter`, `Tab`, `Card`, `Action` (gold, navy, slate,
claim and claimed buttons with an optional price), `Pager`, `RewardArt` and header `Chip`s. Everything fits the
canvas; long lists page instead of scrolling. `Refresh` rebuilds the live layer; `Notify` shows a result on the
screen's notice line and keeps it through a refresh.

The art behind them, all Codex-painted and installed by `art/royal/boards.py` (`boards`, `emblems`, `sheets`,
`scenes`, `kit`):
- header emblems in the forge anvil's gold (`kit/emblem-<name>`, two 3 x 3 sheets);
- arena rank, guild perk and bounty objective pictures (`items/arena-*`, `perk-*`, `bounty-*`),
  cut from 3 x 3 sheets by their opaque blobs (a neighbour reaching into a cell stays with its own object);
- scenes: the tower, the three expedition destinations and the four raid bosses (`items/scene-*`), and the hub's
  adventure and progression activities (`items/activity-*`, `jobs/16-adventure-activities.json`, installed by
  `pictures.py`; the hub prefers them to the concept's card cuts, which have their labels painted in);
- `kit/paper-clean`, the codex parchment without its page buttons, for the bounty posters.

The More hub lays its cards out to fit too: three to a row for up to six activities, four beyond that, a short
last row centred.

Buttons have no hover highlight: hovering looks exactly like resting. Every button in the tree presses the same
way (`ButtonFeel.Install`, from `SceneRouter`). While held, it shrinks a few pixels, drops 2 px (`ButtonFeel.Depth`)
and darkens, caption, icon and price included. Sliding off or starting a scroll lets it rise. Letting go over it
runs its action (on release, as Godot buttons act) and springs it up a little past rest before it settles.
- **Own surface.** A button that draws its own surface (theme, `ModalUi`, `HomeMapUi`, kit `RoyalButton`, battle
  cards) moves as a whole. Only its drawn transform changes, never its layout or hit area.
- **Painted art.** A `RoyalButton` over plate art, or any button marked `ButtonFeel.PaintBelow` (hotspots, the
  challenge selectors), gets `PressedArt`. It copies the screen under the button and redraws it as a soft dent
  (`assets/shaders/button_press.gdshader`). The dent is feathered to nothing at the rect's edge, so a hotspot larger
  than its art never shows as a box or a doubled edge.
- **Own touch handling.** The battle card dock and the map's landmarks read touches themselves and drive the press
  with `ButtonFeel.Hold`. The map squashes the painted landmark toward its foot, and its stars follow.

`UiReviewSmoke --button-press` holds one of each kind and captures it at rest and held. Keyboard focus keeps its
outline, which Godot shows only for keyboard navigation (`gui/common/show_focus_state_on_pointer_event`).

A `RoyalButton` is exactly the rect it is placed at. Its caption is a `RoyalLabel` centred in that rect; the
button's own `Text` is never drawn (it names the button for tests and accessibility), so neither that text nor a
surface's slice margins may give it a minimum size. If they did, Godot would stretch the surface past the rect and
the caption would sit high. The UI review's `ROYAL_BUTTON_STRETCHED` audit fails any button held at its minimum.

## Screens

| Screen | Concept | Code |
| --- | --- | --- |
| Home atlas and HUD | ui 06 | `MapMenu.Home.cs`, `MapPathCanvas.Painted.cs` |
| Caravan hub (More) | menu 01 | `CaravanHub.cs` (its activities below are boards unless listed) |
| Tower, bounties, boss rush, raid, event | board-banners | `TowerMenu.cs`, `BountyMenu.cs`, `BossRushMenu.cs`, `RaidMenu.cs`, `EventMenu.cs` |
| Expeditions, progression, guild, friends, arena, rankings, LAN race | board-banners | `ExpeditionMenu.cs`, `ProgressionMenu.cs`, `GuildMenu.cs`, `FriendsMenu.cs`, `ArenaMenu.cs`, `LeaderboardMenu.cs`, `LanRaceMenu.cs` (runner cards from `LanChallengeService.BuildRoomSnapshot`, standings from `SessionStandings`) |
| Daily gifts | board-camp | `LoginCalendarMenu.cs` |
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

### Stat sheets

Every stat list (warband and spell profiles, the codex, the spell and relic inspectors) is one `StatSheet`
(`scripts/ui/shared/StatSheet.cs`, built from `ArmoryDetailUi.Stats`): one kit `tile` frame holding 34-unit rows of
icon, name (book serif 18) and right-aligned value (book serif bold 20), with hairlines between rows and columns.
Columns drop until every name and value fits its cell. A single column too narrow for a long pair closes up instead
(values at the 18 px floor, then slightly tighter letter spacing in the name), so text never shrinks or overflows.
Names are kept short enough for two columns ("Durability", "Mana gain", "Repair"). Icons come from
`StatSheet.Glyph`: the concept's painted stat glyphs, plus `assets/ui/icons/stats/*.svg` drawn to match for mana,
duration, speed and siege. The concepts painted one plaque per stat on the warband and spells plates; those are
painted out (`art/royal/statsheets.py`, Codex job `13-stat-sheets.json`, pasted by `plates.py`), so the sheet is only
as tall as its rows and the space below stays free. `UiReviewSmoke --armory-details` and
`--concepts --only=codex-stats` check that every stat of every unit, spell and relic fits its cell.

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

## Enemy strongholds

Every stage has its own painted stronghold (`art/royal/bases.py`, briefs in `art/royal/bases_briefs.py`). Each
zone's ten share an architecture (`ZONE_LOOK`); each stage's form comes from its name and theme, and they grow
from outposts (stages 1–3 of a zone) to the boss stage's grand fortress. The Rotbound Host's marks (skull
heraldry, bone trophies, green witch-fire) are accents only.

They are painted *into* the scene so they share its light and brushwork. `bases.py guide` composes each zone's
backdrop layers over the world rect around the enemy end (`EnemyBaseX` −216 to +104, y 120–440) as the battle
camera at the end of the field draws them (the first 100 were painted for the two-screen field, x 936–1256). Codex paints the stronghold into that guide (standing along the far edge of the
road, its gate at road level), then a second edit keys it onto flat magenta; the key stays pixel-aligned with
the painting. `bases.py cut` takes the alpha from the key and the colour from the painting, bakes a soft contact
shadow onto the ground below it, finds the witch-fire lanterns and the tallest roofs, shifts the plate so its
gate (measured by eye into `GATES` with `bases.py gates`) stands where troops strike it, and writes
`assets/structures/bases/stage_NNN.{png,json}`, recording the `EnemyBaseX` it was laid out against. Key edits that come back rescaled are registered onto the
painting (SIFT + RANSAC) before cutting. Plates import as lossy WebP (q0.85) with mipmaps (~27 MB imported
for all 100); keep those `.import` settings when adding plates.

At runtime `EnemyBaseArt` loads the stage's plate (endless runs and boss rushes borrow the zone's final one) and
shifts it with any later change to `EnemyBaseX` (plates without the field default to 1152), so the field's length
can change without a recut.
It is drawn untinted behind the band, so every troop on the road passes in front of it, with an additive flicker
over each lantern, damage smoke from its roofs, and its health meter at world row 270. Stages without a plate
fall back to the Blender gatehouse and its outworks. Review with `bases.py preview`, then
`CombatReviewSmoke --bases [--stages=…] --screenshots` (`artifacts/enemy-bases`).

```sh
python3 art/royal/bases.py guide                                  # zone guides
python3 art/royal/bases.py run [stage ...]                        # Codex paint + key; resumes, waits out limits
python3 art/royal/bases.py sheet <stage ...>                      # read gate centres into GATES
art/remaster/puppet/run.sh art/royal/bases.py cut [stage ...]     # plates + JSON (needs numpy/opencv)
godot --headless --path . --import
```

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
drifting ground mist: `MapFogLayer` draws blurred world-space masks of both (and a much wider blur of the hidden
tiles) through `assets/shaders/royal_fog.gdshader`. The cloud is two hand-painted layers, white on black
(Codex job `18-map-clouds`, made to tile and packed into `fog-clouds.png` by `art/royal/clouds.py`): fluffy
cloudlets lit from the upper left, and below them a fainter veil that drifts more slowly on its own heading.
Toward the edge of hidden land the faintest cloud goes first, so the outline frays into cloudlets; a small pocket
of hidden land gets a denser little cloud. Between the clouds a haze of the same blue-grey hides a heavy blur
of the land (the blurred mipmaps of the screen, about a thirtieth of its width), so the layer is one even tone,
the cloud shows only as soft relief with faint shadows, and nothing on hidden ground can be made out. Smooth noise (`art/royal/fognoise.py`) bends the
layers so their tiles never line up and varies the cloud's thickness a little from place to place. The cloud
reaches a little way out over the sea beside hidden coasts. The
frontier's colours are muted under the mist (the shader reads the screen behind it). Landmarks, price tags,
reward bursts and the vignette are drawn above it by `MapOverlayLayer`. `UiReviewSmoke --fog` captures the fog
fresh, zoomed out, close up, mid-campaign and in other zones into `artifacts/map-fog/`.

Tapping a site that can be entered goes straight in: the caravan travels there and a battle opens its
preparation (which shows the rewards and entry cost). The site panel only appears to explain why a site is
blocked, such as a sealed boss gate or missing rations.

## Items

Relics and spells are painted images (`art/royal/jobs/02-items.json`, and `08-items-expansion.json` for the
expansion spells, tactics and relics, installed by `art/royal/items.py`), not model renders, and so are the hub
activities and wagon upgrades (`06-activities.json`, `07-upgrades.json`, installed by `art/royal/pictures.py`).
`UiArtLoader` uses them wherever an item is shown; the codex shows units as their battle figures and keeps
portraits only for foes without one.

The small cream glyphs in card corners and detail headers are kit pieces. The first ten spells and the relic
kinds (`relicicon-boots`, `-crown`, ...) are cut from the concepts; the expansion's are painted as 3x3 sheets
(`09-glyphs.json`) and cut by `art/royal/glyphs.py` into `kit/spellicon-<id>` and `kit/relicicon-<id>`. A relic
with a glyph of its own uses it; the rest share one by the kind of object (`ShopMenu.RelicIcon`).

## Royal storehouse

The storehouse (`CashShopMenu.cs`, `StorePackCard.cs`) has no concept of its own, so it is built from the concept
screens' pieces inside the modal frame: the achievements screen's tabs (`ach-tab`, cut from its plate, and
`ach-tab-selected`) with cream glyphs, the home strip's parchment balances, and `ach-card` pack cards that light up as
`ach-card-ready` while a purchase waits for its Confirm tap. Each pack has a painted picture
(`art/royal/jobs/14-storehouse.json`, installed by `art/royal/storehouse.py` as `items/store_<product id>`), so the four
gold and four ration packs grow from a purse to the king's treasury and from a ration bundle to the royal granary.
The pack giving the most for its price is marked with the painted ribbon (`kit/store-ribbon`), which also marks the
once-per-account starter kit. The tab glyphs and the small `store-*` glyphs are glyph sheet 12.

The modal draws at `MobilePresentation.HudScale`, so its kit pieces use `RoyalKit.Scaled` (rims and corners at their
concept-screen size, margins in modal units) and its text is sized in modal units against the phone floors as drawn
(12 units of book serif and 11 of capitals clear 18 and 16 px). Review with `UiReviewSmoke --storehouse`
(`artifacts/home-map/desktop/storehouse-*`).

## Spell effects

Spells in battle are built from `scripts/combat/SpellVfx.cs` pieces: ground rune circles, glows, shockwaves,
light pillars, lightning, chains, road decals, painted elements that rise out of the ground and painted particle
bursts, all on the battlefield's shallow ground plane and pausing with the battle. Each spell's recipe (wind-up,
impact, aftermath) is in `BattleController.SpellVfx.cs`; the Apply*Spell methods call it, and placed tactics stand
on the road as their painted item pictures (`Unit.TacticalArt.cs`). Lingering states draw on the body
(`Unit.StatusVisuals.cs`): flames, thorns, chains, shield shimmer, stun stars, a silence rune. Lasting areas
(`BattleContentField`) turn their school's rune circle and drift motes; a meteor's circle fills until it lands.

The textures are painted by Codex (`10-spell-vfx.json`) and installed by `art/royal/vfx.py` into
`assets/fx/spells` (sigils keyed white from black and tinted in code, decals, eruptions, and particle sheets cut
into `p_*` sprites). `UiReviewSmoke --spell-gallery [--only=meteor,...]` casts every spell on a staged field and
saves a strip of timed frames per spell to `artifacts/spell-gallery`.

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
