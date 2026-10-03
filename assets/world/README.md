# Crownroad world artwork

The completion-driven zone map uses the terrain and scenery atlases in
[overworld/polished-v3](overworld/polished-v3/README.md). The water and terrain
share one ground plane. See [Adventure map](../../docs/ADVENTURE_MAP.md) for
rendering, discovery, and progression rules.

The campaign has 60 individual battle backgrounds, `battles/stage-01.png`
through `battles/stage-60.png`. `WorldEnvironmentArt` loads the appropriate
image by stage number. Each panorama retains its proportions and places its
clear floor at `(84, 96)` in the `2560 × 720` battlefield. The movement bounds
end at `(2476, 584)`. Lossless imports with mipmaps preserve fine detail.
The [battle material atlas](battles/polished-v2/README.md) adds ground detail.

The previous zone paintings and standalone menu backgrounds have been removed.
The old walking-map coordinates remain in save migration code where required.

## Source and review

The stage illustrations were created with the built-in image generator.
[manifest.json](manifest.json) records their final paths, titles, submitted
prompts, resolutions, and hashes. Each location can be replaced independently.

Build the game and run `scenes/tests/WorldArtReview.tscn` with a unique
`--save-suffix=world-art-review-<id>` and `--capture`. Add `--mobile-preview`
for the phone layout. The review checks all 60 stage images, current map atlas
materials and scenery, imported resolution, battle bounds, shared tile edges,
and each zone's actual game screens. Captures are written to
`artifacts/world-art-review/` and do not use personal progress.
