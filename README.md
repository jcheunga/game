# Crownroad (Godot + C#)

Medieval fantasy lane-battle game built with Godot 4.6 + C#. The **Lantern Caravan** defends its war wagon against the **Rotbound Host** across 100 campaign stages in ten zones, endless roguelite runs, and async/LAN multiplayer challenges.

## Quick start

```bash
godot --editor --path .         # open in editor
godot --path .                  # run directly
```

## Server

```bash
./scripts/verify_all.sh           # build game, validate data, run server tests, build website
cd server
dotnet run                      # start backend on port 5000
dotnet run -- --test            # run backend endpoint tests
dotnet run -- --test-data ../data  # run game-data, locale, and store-config checks
docker compose up -d            # local HTTP-only Docker run
```

See `server/.env.example` for local configuration. For a public release, use
the HTTPS deployment workflow in [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md), not
the local Compose file.

## Website

The public site (home, support, privacy and terms pages) lives in `site/` and
is deployed with the API. Preview it locally:

```bash
python3 scripts/tools/build_site.py --serve   # http://localhost:8000
```

Fill in `site/site.json` before launch. See [docs/WEBSITE.md](docs/WEBSITE.md)
for the go-live checklist and store-listing URLs.

## Current status

- Repo-side roadmap work is complete.
- Primary local verification command: `./scripts/verify_all.sh`
- Current verified state: game build `0 warnings / 0 errors`, server tests `86 passed`, data checks `11992 passed`
- New saves start with one Swordsman; a squad holds six units and five spells. Adventure travel is free, each battle costs 4 food, and food recharges 2 every 5 minutes up to 24. Account sign-in setup is in [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md#account-sign-in). Older reports are in [docs/archive/](docs/archive/README.md).
- Production still requires translations, deployment secrets, email/Google credentials, store signing/credentials, and manual device playtesting.

## Tests

| Command | What | Count |
|---------|------|-------|
| `cd server && dotnet run -- --test` | Server endpoint tests (happy path + validation) | 86 |
| `cd server && dotnet run -- --test-data ../data` | Game data, locale, and store-config checks | 11992 |
| `./scripts/verify_all.sh` | Full local repo verification | game build + data checks + server tests + website build |

GitHub Actions also runs the game build plus the server/data validation workflow on `server/`, `data/`, `scripts/`, `scenes/`, and project/workflow changes, supports manual dispatch, and cancels stale in-progress runs per ref.

## Adding art assets (no code changes needed)

| Asset Type | Drop Location | Format |
|-----------|---------------|--------|
| Unit sprites | `assets/units/{unit_id}.png` + `.json` | Sprite sheet and frame metadata; `{visual_class}.png` is the fallback |
| Screens and menus | `assets/ui/royal/` (plates, specs, kit, figures, missions) | Concept plates, measured layouts and cut interface pieces; see `docs/ROYAL_UI.md` |
| Battle backdrops | `assets/world/royal/{zone}.json` + `{zone}_far/_mid/_ground/_near/_front.png` | Five painted parallax layers per zone (`art/royal/backdrops.py`) |
| Map art | `assets/world/royal/maps/{zone}.png` + `.json`, `maps/landmarks/` | One painted map per zone and the site landmarks (`art/royal/maps.py`) |
| Spell and relic pictures | `assets/ui/royal/items/{spell_or_relic_id}.png` | Painted item images (`art/royal/items.py`) |
| Structures | `assets/structures/battle-v2/{id}.png` + `.json` | Wagon, gatehouse and fort plates with anchor, mounts and door strip; `assets/structures/{id}.png` is the fallback |
| Particle textures | `assets/particles/{particle_id}.png` | Battle burst/trail sprites for deploy, impact, spell, and boss VFX |
| Projectile sprites | `assets/projectiles/{sprite_id}.png` | Blender-rendered arrows, bolts, flasks, pots and other shots, flown on arcs by `ProjectileStyles` |
| Unit icons | `assets/ui/icons/units/{unit_id}.png` | Optional shared fallback: `{visual_class}.png` |
| Reward icons | `assets/ui/icons/rewards/{reward_type}.png` | Badges for sigils, shards, relics, season experience, units and spells; gold, food, tomes and essence use the home map's resource icons |
| Meta icons | `assets/ui/icons/meta/{meta_id}.png` | Social, leaderboard, arena, and challenge-status badges |
| Codex portraits | `assets/ui/portraits/codex/{entry_id}.png` | Only for codex foes without a battle figure (legacy foes and raid bosses); units show their figure, spells and relics their painted picture |
| Music | `assets/music/{track_id}.ogg` | 19 original loops rendered by `art/audio/build_music.py` |
| Sound effects | `assets/sfx/{cue_id}[_n].ogg` + `sfx.json` | 212 cues rendered by `art/audio/build_sfx.py` |

Audit coverage with the in-game debug console command `assets` or `cd server && dotnet run -- --test-data ../data`. See `ASSETS.md` for the full ID lists and format specs.

## Adding translations

Place a JSON file at `data/locale/{language_code}.json` with the same keys as `data/locale/en.json`. It appears in the Settings language selector automatically. `dotnet run -- --test-data ../data` now checks for missing keys, empty strings, extra keys, and broken `{0}`-style format placeholders before release.

## Export presets

`export_presets.cfg` includes Web (PWA), Android (arm64, Google Play Billing), and iOS (StoreKit IAP). Build with:

```bash
godot --headless --path . --export-release "Web" builds/web/index.html
godot --headless --path . --export-release "Android" builds/android/crownroad.apk
godot --headless --path . --export-release "iOS" builds/ios/crownroad.ipa
```

## Key documents

| File | Contents |
|------|----------|
| `ROADMAP.md` | Full milestone plan, sprint log, bugs/hardening status |
| `ASSETS.md` | Art production manifest (units, backgrounds, structures, audio, particles) |
| `THEME_BIBLE.md` | Fiction, factions, and setting reference |
| `CAMPAIGN_PLAN.md` | 10-district campaign structure: ten stages per district, 100 in all |
| `docs/` | Current system notes: adventure map, deployment cards, combat review, launch and deployment |
| `docs/archive/` | Dated reports kept for reference (legacy 60-stage numbering) |

## Architecture

- **SceneRouter** (autoload) — handles all scene transitions with fade + loading tips
- **GameState** (autoload) — centralized progression, economy, and settings, persisted via SaveSystem
- **AudioDirector** (autoload) — manifest-driven sound effects on four mix buses, positional battle sounds, ambience beds and details
- **MusicPlayer** (autoload) — crossfading music tracks mapped to scene/route context
- **NativeIAPService** (autoload) — platform-detected IAP (Apple StoreKit / Google Play / Stripe)
- **SafeAreaService** (autoload) — mobile notch/island display inset handling
- All game data is JSON-driven from `data/` and loaded through `GameData`
- Unit and projectile object pools reduce GC pressure on mobile
- Analytics are off until the player opts in under Settings → Account → Privacy

## Prototype controls

- Battlefield: one shallow band about two screens long (world 1200 wide, 600 visible). The camera
  follows the fighting; scroll the mouse wheel or trackpad, middle-drag, or use Left/Right arrows to pan,
  then the sword button or **F** to follow again. **Home / End** jump to either base. On mobile, drag to
  explore and use the map button to see the whole battlefield.
- Deploying: tap a unit card (or press its number key). The unit walks out of the war wagon's door onto
  the battle line; there is no lane choice. Spell cards drag onto the field to aim.
- Victory: destroy the enemy stronghold. This wins immediately, whatever waves remain. Only melee troops
  and siege units (such as the Ballista Crew) damage bases; archers and casters fight troops only. You lose
  when the war wagon's hull reaches 0.
- Encounters: advancing troops can trigger the next authored pack early; enemy caps and time fallbacks
  still apply. Cursed stages mark patches of cursed ground.
- Home map:
  - Opens directly onto the active zone map, with gold, food and stars at the top left and settings at the top right
  - Each zone has irregular medieval terrain with forests, mountains, rivers, villages, forts and resource caches
  - Clearing a stage or collecting a resource opens the surrounding tiles. Travel is free; each battle costs 4 food
  - Select a site to open its floating panel; close it to see the unobstructed map
  - Defeat the zone's nine leaders to open its boss, and the boss to reveal the next zone. The pager shows one zone at a time
  - Bottom tabs: `Warband`, `Spells`, `Upgrades`, `Achievements`, `Codex` and `More`
  - `More` holds the Adventure, Caravan and Community destinations, `Account`, `Player profile` and `Quit game`
  - See [home map layout and verification](docs/HOME_MAP_UI.md) and [adventure map](docs/ADVENTURE_MAP.md)
- Settings: tabs for `Sound` (music, effects, ambience, mute), `Gameplay` (caravan name, tutorial hints,
  reduced motion, high contrast, FPS counter, language, difficulty), `Online` (provider, endpoint, profile
  refresh) and `Account` (account, cloud save, privacy, payments). It returns to the screen you opened it from.
- Armory (`Warband`, `Spells`, `War wagon`, `Relics` tabs):
  - Recruit units with gold once their stage is reached, scribe spells, and upgrade owned units with a stat preview
  - Equip up to six unit cards and five spell cards; one unit is enough to deploy
  - Each unit has a squad role (`Frontline`, `Recon`, `Support`, `Breach`); two cards of one role activate
    `Frontline Drill`, `Recon Link`, `Support Mesh` or `Breach Line`
  - War wagon: upgrade plating, stores, march drum and rune beacon. The wagon starts with an archer crew;
    upgrade its damage and range, install a mounted ballista and firepot launcher, learn Arrow Volley and
    Emergency Repairs, or fit Reinforced Axles. Installed mounts fire automatically
  - Enemy strongholds have no weapons: the gate never fires at your troops
- Stage preparation: the leader, victory rewards, your warband with levels and courage costs, and equipped
  spells. `Edit squad` opens the armory; `Deploy` spends the entry food and starts the battle.
- Battle:
  - 19 unit cards: Swordsman, Archer, Shield Knight, Spearman, Crossbowman, Cavalry Rider, Siege Engineer,
    Mage, Halberdier, Alchemist, Battle Monk, War Hound, Banner Knight, Necromancer, Rogue, Berserker,
    Lantern Guard, Ballista Crew and Stormcaller, each unlocked by stage
  - 10 spell cards: Fireball, Heal, Frost Burst, Lightning Strike, Barrier Ward, Stone Barricade, War Cry,
    Earthquake, Polymorph and Resurrect
  - Unit deploys and spell casts consume courage and enter cooldown
  - Units fight when enemies enter their aggro box. Ranged units fire Blender-rendered projectiles on arcs
    and strike in melee at point-blank
  - Enemy roles include Blight Casters (ranged), Sappers (dive for the wagon), Dread Heralds (buff nearby
    undead), Hexers (disrupt courage), Rot Hulks (explode on death) and Bone Nests (split on death); each
    zone ends with its own boss
  - Later stages add route modifiers, timed hazards and battlefield events such as ritual sites, relic
    escorts and gate breaches, with rewards for success and penalties for failure
  - Only combat numbers float over the field; there is no other battle text
  - Mission stars are evaluated from stage-authored objective rules; fully clearing a district grants a one-time reward
- Endless: choose any of the ten zones, pick an opening boon, then survive escalating waves. Every fifth wave
  is a checkpoint with a draft upgrade or route fork; every 15th wave is a boss checkpoint. Retreat banks the
  spoils; defeat ends the run with a result screen.
- Multiplayer challenge:
  - Build, import, roll or share a seeded challenge code like `CH-04-PRS-4821`; pin codes to keep boards
  - Boards: `Rooms`, `Daily`, `Featured`, `Saved` and `Squad`. Featured boards can lock everyone to the same squad
  - `Challenge briefing` explains scoring and medals; `Records & replay` shows local attempts and the best
    deployment tape, which replays as a ghost benchmark
  - Online rooms: `Refresh` pulls rooms, leaderboards, featured boards and the player profile; `Match` finds a
    seat; `Host` publishes the selected board; listings offer `Request join`. Joined rooms add ready, launch,
    reset, leave, report and seat-recovery controls, auto refresh, a room monitor and a scoreboard
  - `Sync` flushes queued challenge results through the selected sync provider
  - `LAN` opens LAN races over local ENet: host or join a board, ready up, launch together after a shared
    load barrier, and compare results on a live scoreboard with session standings and rematches
  - `scripts/smoke/lan_race_smoke.sh` runs a two-instance headless LAN race; `scripts/smoke/http_*_smoke.sh`
    drive each HTTP provider against a local stub server, and the `*_suite.sh` scripts run them together

## Project layout

- `project.godot`: project config, startup scene, and autoload registration
- `Game.csproj`: C# project for Godot
- `Game.sln`: solution file for Rider/Visual Studio
- `data/units.json`: unit definitions and costs
- `data/stages.json`: stage progression/map/combat tuning
- `data/combat_config.json`: global combat/battlefield/economy tuning
- `scenes/MainMenu.tscn`: startup scene · the home map (shares `MapMenu`)
- `scenes/EndlessMenu.tscn`: endless-mode prep and route selection
- `scenes/MultiplayerMenu.tscn`: async multiplayer challenge prep and code entry
- `scenes/SettingsMenu.tscn`: shared audio/interface settings screen
- `scenes/MapMenu.tscn`: zone map
- `scenes/ShopMenu.tscn`: dedicated unit/base shop screen
- `scenes/LoadoutMenu.tscn`: stage preparation
- `scenes/Battle.tscn`: freefield combat scene
- `scenes/autoload/`: autoload root scenes
- `scripts/core/`: app services (`SaveSystem`, `GameState`, `SceneRouter`)
- `scripts/data/`: data models and loader (`GameData`)
- `scripts/ui/`: menu/map UI controllers and components
- `scripts/combat/`: battle loop, unit model, and combat runtime stats

## Architecture notes

- Game flow is routed through `SceneRouter` (autoload).
- Progression/resources are centralized in `GameState` (autoload) and persisted by `SaveSystem` to `user://savegame.json`.
- UI/battle audio is centralized in `AudioDirector` (autoload), which plays the cues in `assets/sfx/sfx.json` and the zone soundscapes; `AudioCatalog` maps units, weapons and spells to cues, and the audio pipeline lives in `art/audio/`.
- Shared audio/interface options are surfaced through `SettingsMenu` and persisted in `GameState`.
- Unit and stage tuning are data-driven from JSON files in `data/` and loaded through `GameData`.
- Global combat pacing/limits (spawn pressure, enemy cap, base approach distance, courage economy, etc.) is in `data/combat_config.json`.
- Endless-mode best wave/time, selected route, owned units, base upgrades, and the gold/food economy are also persisted in `GameState`.
