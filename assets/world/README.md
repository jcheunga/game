# Crownroad world artwork

The completion-driven zone map uses the terrain and scenery atlases in
[overworld/polished-v3](overworld/polished-v3/README.md). The water and terrain
share one ground plane. See [Adventure map](../../docs/ADVENTURE_MAP.md) for
rendering, discovery, and progression rules.

## Battle backgrounds

Battles are fought in front of each zone's layered Blender backdrop in
[backdrops](backdrops/README.md): far, mid and near layers that scroll in
parallax as the camera follows the fight. The field runs from x 42 to 1158 in a
world about 1200 units wide, on a shallow band from y 313 to 367; the camera
shows about 600 units at a time (`data/combat_config.json`).

The 60 painted plates `battles/stage-01.png` through `battles/stage-60.png` are
a fallback only. `WorldEnvironmentArt` loads a stage's plate by number when its
zone has no backdrop and draws it at its own proportions to cover every row the
camera can see; stages without a plate use `assets/backgrounds/{terrain_id}.png`.
The [ground material atlas](battles/polished-v2/README.md) adds ground detail to
painted plates only. [manifest.json](manifest.json) records the plates' prompts,
resolutions and hashes.

## Review

Build the game and run `scenes/tests/WorldArtReview.tscn` with a unique
`--save-suffix=world-art-review-<id>` and `--capture`. Add `--mobile-preview`
for the phone layout. The review checks that each of the ten zones has a
distinct three-layer backdrop, imported at full resolution in the proportions of
its world rects, ordered far to near with the road layer locked to the field,
and covering the whole battle world and band. It also checks the map atlas
materials and scenery, plate mapping, and shared map-tile edges, then opens each
zone's map (fresh and fully explored) and first battle to confirm the battle
draws in front of that zone's backdrop. Captures, plus a desktop backdrop
gallery, are written to `artifacts/world-art-review/` and do not use personal
progress.
