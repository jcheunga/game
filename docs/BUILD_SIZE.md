# Build size (Android App Bundle)

How big the store download is, what fills it, and how to keep it down.
Measured on 2026-10-18 with Godot 4.6.1 Mono, the Gradle Android preset (arm64
only) and a release export.

## Google Play's limits

From [Google Play's size limits](https://support.google.com/googleplay/android-developer/answer/9859372)
(checked 2026-10-18). All are compressed download sizes:

| What | Limit |
| --- | --- |
| Base module | 500 MB |
| Each asset pack | 1.5 GB |
| Base, feature modules and install-time asset packs together | 4 GB |
| Fast-follow and on-demand asset packs together | 30 GB |

Above 200 MB, Play shows a dialog (which the player can dismiss) before
installing over mobile data. Older Play pages give 150 or 200 MB for the base
module. Ours is about 30 MB either way.

Godot's Gradle export puts the engine, the .NET runtime and the Java code in
the base module. Every exported file (textures, audio, the C# assemblies)
goes into one install-time asset pack, `assetPackInstallTime`. Godot 4.6
cannot make fast-follow or on-demand packs. Using them would need three
things: a Play Asset Delivery plugin, a Gradle module per pack, and code that
waits for a pack and loads it as a resource pack
(`ProjectSettings.LoadResourcePack`) before showing anything from it.

## Sizes

| | Before (3bca1ed7) | After |
| --- | --- | --- |
| Release .aab | 712.2 MB | 377.3 MB |
| Base module | 29.7 MB | 29.7 MB |
| Install-time asset pack | 680.7 MB | 346.1 MB |
| Play download, bundletool `get-size total` | 709.2 MB | 374.5 MB |
| Debug .aab | 714.4 MB | 379.6 MB |

The bundle now uses 6% of the base limit, 23% of the asset pack limit and 9%
of the install-time total. It is still over the 200 MB mobile-data mark (see
[Further options](#further-options)). The first measurement, a 745 MB debug
bundle, came from the main checkout, which had more art in progress.

What was saved, in compressed MB in the release bundle:

| Content | Before | After | How |
| --- | --- | --- | --- |
| Zone map paintings (40 tiles) | 209.5 | 91.6 | ETC2 instead of ASTC |
| Battle backdrop layers (50) | 68.3 | 19.7 | lossy WebP |
| Unit model previews | 53.1 | 10.7 | enemy previews excluded |
| Item pictures | 48.3 | 16.0 | lossy WebP |
| Codex portraits | 34.8 | 0 | excluded |
| Plates | 22.0 | 4.3 | lossy WebP |
| Unit and codex icons | 20.4 | 1.1 | codex copy and enemy icons excluded, lossy WebP |
| Structures | 17.9 | 2.9 | fallback wagon and gatehouse art excluded, lossy WebP |
| Mission art, store images | 10.0 | 4.1 | lossy WebP, store-listing images excluded |

Unchanged: unit battle atlases (118 MB, already lossy WebP at 0.85 since
2026-10-15), audio (36 MB, Vorbis at about 115 kbps), C# assemblies (13 MB),
the royal kit pieces and unit figures (small, crisp UI cut-outs), and effects.

`dotnet/include_debug_symbols` is on (the default). It adds `Game.pdb`: 1.4 MB,
or 0.8 MB compressed. It stays on, because crash reports (`CrashReporter`,
sent only with the player's consent) include stack traces, and the symbols
give those traces file and line numbers.

## Texture import settings

`scripts/tools/mobile_textures.py` holds the rules and rewrites the `.import`
files that differ from them:

- **Zone map paintings** (`assets/world/royal/maps/*.jpg`, 2721×1532 each):
  VRAM compressed at normal quality instead of high. Phones get ETC2 instead
  of ASTC 4×4, still with mipmaps for the zoomed-out map. A tile drops from
  5.6 MB to 2.8 MB, both in the download and in GPU memory. The map screen
  holds a zone's four tiles at once, so that is 11 MB instead of 22 MB.
  `art/royal/mapsections.py` writes new tiles with the same setting.
- **Painted pictures**: lossy WebP at quality 0.9 instead of lossless. This
  covers the battle backdrop layers (`assets/world/royal/*.png`), item
  pictures, plates, mission art, codex portraits, unit icons and structures.
  Lossy and lossless textures both decode to RGBA, so memory is unchanged.
  Alpha stays lossless, so code that reads a texture's alpha behaves the same:
  the map landmark and plate hit tests, and the trimming of card icons.

How the settings were chosen, on real tiles and layers:

- Map tile, ETC2 against the source: 31 dB PSNR (ASTC 4×4 is 39 dB). At 2×
  zoom on the most detailed part of a tile, ETC2 is hard to tell from the
  source. A 75% downscale, the other way to halve a tile, is visibly soft.
  Basis Universal (UASTC) was only 10–30% smaller than ASTC, so it was not
  used. Lossy WebP would be smaller still (1.7 MB a tile), but it would take
  four times the GPU memory.
- WebP at quality 0.9 against the PNGs: 33–43 dB, and no visible difference at
  2× zoom on item pictures, plates, backdrops and icons. Files are 5–8×
  smaller.

## Excluded files

These ship with no way for the game to load them, so the Android and Web
presets' `exclude_filter` leaves them out:

- `assets/ui/models/*`: the model preview sheets. No screen plays an animated
  preview any more; menus and the codex draw the still figures in
  `assets/ui/royal/figures`. `art/royal/figures.py` cuts those figures from
  these sheets, so they stay in the project. Until 2026-10-22 only the enemy
  sheets were left out (see [Later removals](#later-removals)).
- `assets/ui/portraits/codex/*`: the codex draws units and enemies as figures,
  and spells and relics as their item art. Raid bosses have no codex entry, so
  `RaidMenu` shows its "?" badge and never reaches these. If raid portraits
  should come back, keep the four `boss_*` raid files and fix `RaidMenu`.
- `assets/ui/icons/codex/*`: a byte-identical copy of `icons/units` that
  `art/remaster/pack.py` writes. No game code reads it.
- `assets/ui/icons/units/enemy_*`: unit icons are drawn only for player units
  (deploy cards, rewards, squad badges).
- `assets/structures/war_wagon*`: older art, used only when the `battle-v2` art
  is missing, which never happens (every wagon skin has one). `siege_atlas.png`
  stays as the last fallback, because the code would crash without it. The flat
  `gatehouse.png` was deleted on 2026-10-22 with the Blender gatehouse.
- `assets/units/warband_sprites.png` (used only when a class stand-in is
  missing; all six exist), `assets/units/_example.json`.
- `assets/branding/crownroad-play-*`: Play listing images. The app icon stays.
- `assets/ui/royal/plates/hud-battle.png` and `plates.json`: the battle HUD
  uses kit pieces, and nothing reads the JSON.

A clone of the project with exactly these files deleted ran the home map and
concept-screen reviews (below) with no new failures and no load errors.

The iOS preset needs the same filter. The editor rewrites that section of
`export_presets.cfg`, so add it in Project → Export → iOS → Resources →
Filters to exclude.

## Later removals

On 2026-10-22 UI that no screen could reach any more was removed: the animated
model preview (so all of `assets/ui/models/` is now excluded, not just the enemy
sheets), the Blender gatehouse and its five outworks (every stage has a painted
stronghold; the plates and the flat `gatehouse.png` were deleted) and the
check-box and radio icons (no screen has a check box). Measured with
`--export-pack "Android"` before and after, the pack lost 147 files and went
from 418.6 MB to 408.3 MB:

| Content | MB |
| --- | --- |
| Player model previews (37 sheets with their JSON) | 10.0 |
| Gatehouse and outwork plates | 0.3 |
| Check-box icons, C# assembly and symbols | under 0.1 |
| Total | 10.4 |

These textures are already lossy WebP, so the release bundle shrinks by about
the same amount (zipping them saves under 2%).

## Visual check

The review drivers captured screens before and after at a phone's pixel size
(`UiReviewSmoke --home-map` and `--concepts --zones=<all ten>`,
`--window=2532x1170`, so the game surface is 2080×1170). Desktop Godot loads
the desktop (BC/S3TC) copy of VRAM textures. This Mac's GPU can also sample
ETC2, so for the after captures the map imports temporarily pointed only at
their ETC2 files, the copy phones load. At 1:1 on map tiles (zoom 1.2),
backdrops, item grids, plates and the codex, before and after look the same.
Counting pixels that changed by more than 24 levels, the battle captures
differ on under 0.1% and the menus on under 0.5%, mostly animated parts. Both runs had the same nine home-map review failures
(shop purchase buttons and a codex lore check at that window size), so these
changes added none. The concept review passed both times. The 844×390 phone
preview of the shipped-file clone is described under Excluded files.

## Measuring a build

```sh
godot --headless --path . --export-release "Android" builds/android/crownroad.aab
python3 scripts/tools/bundle_size.py builds/android/crownroad.aab
```

`bundle_size.py` lists the modules, the largest asset folders (imported
textures counted under their source files) and the size by file type, and
checks them against the limits above.

A release export needs a release keystore. For a local size check only, point
`GODOT_ANDROID_KEYSTORE_RELEASE_PATH`, `_USER` and `_PASSWORD` at the debug
keystore. A fresh worktree also needs the Gradle template and plugin builds,
which are untracked, copied from the main checkout: `cp -cR <main>/android
android`, plus `cp -cR <main>/addons/<plugin>/bin addons/<plugin>/bin` for
`GodotGooglePlayBilling` and `PlayGamesSignIn`.

Play's own per-device estimate comes from bundletool. Its jar and dependencies
are in the Gradle cache, and `aapt2` is in the Android SDK's build-tools:

```sh
bundletool build-apks --bundle=crownroad.aab --output=crownroad.apks --aapt2=<sdk>/build-tools/<version>/aapt2 --ks=<keystore> --ks-key-alias=<alias> --ks-pass=pass:<password>
bundletool get-size total --apks=crownroad.apks
```

After the first upload, compare with Play Console → App size.

## Keeping it small

- After adding or regenerating art, run `python3 scripts/tools/mobile_textures.py`
  and reimport (`godot --headless --path . --import`). Godot imports a new PNG
  losslessly, and `--check` lists any texture that breaks the rules.
- A new painted folder that adds more than a few MB to the build belongs in
  the script's `RULES`.
- Content that never loads at runtime goes in the presets' `exclude_filter`.

## Further options

The build sits at about 375 MB. Getting under the 200 MB mobile-data mark
would take several of these, and each is a decision for later:

- **Fast-follow pack** for zones 2–10 (their map tiles and backdrops, about
  100 MB). This needs the Play Asset Delivery work described above.
- **Unit atlases** (118 MB): lossy quality 0.75 instead of 0.85 saves about
  15% (about 18 MB). A lower atlas density saves more but changes how close-up
  units look.
- **Four enemy atlases** that may never spawn (`enemy_boneballista`,
  `enemy_catacomb_giant`, `enemy_revenant_captain`, `enemy_plague_engine`,
  about 7 MB). They are in the codex and are named by the raise-dead and
  projectile code, so the spawn paths need checking first.
- **More lossy WebP**: unit figures, `assets/map/adventure` and
  `assets/ui/home`, about 6 MB.
- **Trimming the C# assemblies** (13 MB compressed): a few MB, but reflection
  in the JSON loading makes it risky.
- **Small leftovers** (under 0.5 MB in total): 11 unused kit pieces,
  `projectiles/hand_axe.png`, `fx/drop_4.png`, three spell particles,
  `particles/particle_trail.png`, two frame SVGs, `music/tracks.json`,
  `ui/home/generated-art-v2.json`, and four sounds that are in `sfx.json` but
  never played (`gem_*`, `hound_bark_*`, `hound_growl_*`, `notification`).
