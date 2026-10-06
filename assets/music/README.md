Nineteen original Crownroad loops, rendered from public-domain (CC0) instrument recordings by
`art/audio/build_music.py`. Sources, licences and how to re-render are in `art/audio/README.md`;
`tracks.json` records each loop's tempo, bars and length.

- Scenes: `title` (The Crownroad), `campaign` (The Caravan Road), `shop` (The Quartermaster's Wagon),
  `loadout` (War Council), `endless_prep` (The Long Night), `multiplayer` (The Tourney Field)
- Zone battles: `battle_road`, `battle_harbor`, `battle_foundry`, `battle_quarantine`, `battle_pass`,
  `battle_basilica`, `battle_mire`, `battle_steppe`, `battle_gloamwood`, `battle_citadel`, and the general `battle`
- Bosses: `battle_boss`, `battle_boss_final`

Each loop is sample-continuous at its seam (reverb tails wrap onto the start); Godot enables looping at playback.
