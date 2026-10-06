# Audio pipeline

Every piece of music, sound effect and ambience in the game is rendered by the scripts in this folder from
public-domain recordings and synthesis. Nothing is hand-edited in a DAW, so any cue can be changed in code and
re-rendered.

| Output | Count | Where |
|---|---|---|
| Music loops | 19 tracks, about 22 minutes | `assets/music/{track}.ogg`, `assets/music/tracks.json` |
| Sound-effect cues | 212 cues, 442 files with round-robin variations | `assets/sfx/{cue}[_n].ogg`, `assets/sfx/sfx.json` |
| Ambience | 15 looping beds and 29 detail one-shots (included in the cues) | `assets/sfx/amb_*`, `det_*`, `assets/sfx/ambience.json` |

## Running it

```bash
python3 art/audio/fetch_samples.py        # once: about 1.5 GB of CC0 recordings into art/audio/samples (gitignored)
art/audio/run.sh build_music.py           # every track, or name some: build_music.py title battle_road
art/audio/run.sh build_sfx.py --clean     # every cue, or a prefix: build_sfx.py hit_ spell_
godot --headless --path . --import        # let Godot import new files
godot --path . res://scenes/tests/AudioReview.tscn -- --save-suffix=audio-review-local
```

`run.sh` runs the scripts with Python 3.13, NumPy, SciPy, soundfile and matplotlib through `uv`. Each render also
writes a higher-quality review copy and a spectrogram to `artifacts/audio-review/` (gitignored). `sheet.py out.png prefix ...`
draws a grid of spectrograms for many effects at once.

## How it is built

- `ak/` is the audio kit:
  - `sampler.py` reads note, velocity layer and round robin from sample filenames. It checks the octave of every
    recording with an odd-harmonic test, measures its tuning from long-window FFT peaks, levels the keyboard, and
    plays notes with damping and seamless sustain extension.
  - `instruments.py` registers about 150 instruments and foley groups from the three libraries.
  - `song.py` is the arranging engine: parts, humanised timing and velocity, reverb sends, loop folding (reverb
    tails wrap onto the loop start), and circular mastering (filters, compressor and limiter run on a padded copy,
    so loop seams stay sample-continuous).
  - `lint.py` flags sustained semitone clashes between parts, notes outside an instrument's sampled range, and
    notes past a loop's end. Every track is lint-clean apart from the Rotbound motif's deliberate half-step sighs.
  - `reverb.py` is convolution reverb with synthesised impulse responses (room to cathedral).
  - `dsp.py` covers filters, dynamics and BS.1770 loudness.
  - `foley.py`, `voice.py` and `nature.py` design effects: recorded layers, noise-excited resonators, a formant
    voice for creatures and crowds, and wind, water, birds and insects.
- `music/` holds the score. `common.py` holds the two themes: *The Crownroad* (the caravan, D minor) and the
  *Rotbound* motif (D-E♭-D-A♭, the host). Each track is one module with a `build()`; zone battles share
  `battle_kit.py`.
- `sfx/` holds the effect catalogue. Each `@cue` declares its variants, loudness category, mix bus, cooldown, voice
  limit, pitch drift and whether it is positional on the battlefield. `sfx/ambience.py` also defines `AMBIENCE`,
  which picks a bed and its details for each place.

Loudness: music is mastered to -15.5 to -19 LUFS integrated. One-shots are normalised by category, from -27 LUFS
(quiet interface ticks) to -13.5 LUFS (stingers), as maximum momentary/short-term loudness. Beds sit at -30 LUFS.
Music is encoded as Vorbis at about 114 kbps and effects at about 128 kbps.

## In the game

- `scripts/core/AudioDirector.cs` loads `sfx.json` and plays cues on four buses (Music, Effects, Interface and
  Ambience, with a limiter on Master). Battle sounds are `AudioStreamPlayer2D`s on the battlefield, panned by the
  camera. It also runs the ambience beds, the scattered details and the battle-din layer that rises with battle
  pressure, and gives buttons a sound by role: tabs turn a page, toggles latch, back/close step away, primaries ring.
- `scripts/core/AudioCatalog.cs` decides which cue each unit, weapon profile, projectile style, death effect,
  ability and boss uses.
- `scripts/core/MusicPlayer.cs` plays scene and overlay tracks, each zone's battle track, the boss theme while a
  grave lord lives, ducking under stingers, and a fade-out for results.
- `scenes/tests/AudioReview.tscn` fights a battle in every zone and checks the track, the soundscape and the cues
  that play.

## Sources and licences

All recorded material is CC0 (public domain). No attribution is required, and commercial use is allowed.

| Library | Used for | Licence |
|---|---|---|
| [VSCO-2 Community Edition](https://github.com/sgossner/VSCO-2-CE) | Orchestral strings, brass, woodwinds, harp, timpani, percussion | CC0 1.0 |
| [Versilian Community Sample Library](https://github.com/sgossner/VCSL) | Recorders, Renaissance organ, folk harp, strumstick, psaltery, ocarina, bells, frame drums, anvil, gong, cymbals, ocean drum | CC0 1.0 |
| [Kenney audio packs](https://kenney.nl) (RPG, Impact, Interface, Casino, UI) | Foley layers: footsteps, punches, coins, cloth, pages, creaks, doors, knives, glass, wood and metal impacts, cards | CC0 1.0 |

Everything else (compositions, arrangements, synthesis and processing) is original to this project.

## Verification and what to watch

There are no human ears in the loop, so each render is checked with:

- the score lint
- loudness, peak and loop-seam statistics
- spectrogram contact sheets
- two evaluation-only models (neither generates any audio):
  - [Audiobox Aesthetics](https://github.com/facebookresearch/audiobox-aesthetics), for production quality and
    enjoyment of the music
  - [LAION CLAP](https://github.com/LAION-AI/CLAP), to check that a sound matches its description

Lessons from that process:

- **Unused recordings.** The bowed psaltery and the Renaissance organ's *Full* registration recordings scored
  clearly worse, so they are not used; the organ parts use the 8′ stop.
- **Tuning.** Sample libraries label octaves inconsistently: VCSL idiophones name C3 as middle C, and the 4′ organ
  stop sounds an octave above its label. The sampler checks rather than trusts the labels.
- **Plucked notes.** Harp, pizzicato and spiccato notes must be damped at chord changes. Otherwise their natural
  ring smears the harmony.
- **CLAP.** It is reliable for music, voices, stingers and ambience, but not for sub-second foley: almost any short
  transient reads as "tiny tick". Short effects lean on real recordings instead.
- **Synthetic sounds.** Pure-sine pings read as electronic. Noise-excited resonators and recorded layers do not.
