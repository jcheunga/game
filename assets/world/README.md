# Crownroad stage and zone artwork

The current completion-driven home map uses the painted material and scenery
atlases in [overworld/polished-v3](overworld/polished-v3/README.md), with quiet
water surroundings on the same ground plane as the playable terrain. See
[Adventure map](../../docs/ADVENTURE_MAP.md) for the current rendering and
progression rules. The 32 × 24 walking layout described below is retained as
the historical version 44 implementation used by save migration.

The campaign has 60 individual battle backgrounds in `battles/stage-01.png`
through `battles/stage-60.png`, and 10 main-map backgrounds in `zones/`:
city, harbor, foundry, quarantine, thornwall, basilica, mire, steppe,
gloamwood and citadel. Every image is generated separately for its location.

## Runtime layout

`WorldEnvironmentArt` selects a battle image by stage number and a main-map
image by route ID. Textures load on demand and leave the screen when it closes.
The original PNGs are preserved. Battle imports now use lossless compression
with mipmaps. Each panorama keeps its original proportions and places the start
of its clear floor at `(84, 96)` in the `2560 × 720` world; perimeter scenery
is cropped naturally. The movement rectangle still ends at `(2476, 584)`.
The stage image is neither mirrored nor repeated across the field. Fine,
zone-specific ground detail comes from the new
[battle material atlas](battles/polished-v2/README.md), blended into the stage's
floor without stretching its texture over the full battlefield.

Each zone uses its own illustrated backdrop and ground material. The source
ground quadrilateral has normalized top/right/bottom/left points
`(0.46, 0.29)`, `(0.81, 0.47)`, `(0.64, 0.64)` and `(0.28, 0.44)`,
inset from the decorative perimeter to keep cliffs and buildings off walkable tiles.
It is mapped continuously across the actual 32 × 24 isometric grid in a 3968 × 2176 world, preserving
more of the original ground detail than a small rectangular crop. Drawing,
discovery, touch picking and travel share the
same diamond coordinates. Animated, world-aligned smoke hides unseen ground. Charted ground remains
visible through a light haze outside caravan sight. Discovered water and rock
stay impassable; drawn bridges provide crossings. The artwork contains no route lines, labels or interaction markers;
the game draws those separately. The source illustrations and original prompts
are retained; revision 44 expands the runtime tile layout, with crisp native
ground detail, terrain obstacles and sparse discovery icons.

## Source and reproduction

Created on 2026-10-01 with the built-in `image_gen` tool. No external stock
images were used. [manifest.json](manifest.json) records every final path,
stage/zone title and exact submitted prompt, plus source resolution and SHA-256
when generation is complete. The source PNGs are the editable replacement
assets; visual and rights approval for a public release remains part of the
production launch runbook.

Prompts specify medieval dark-fantasy dioramas, one palette per zone, quiet
unobstructed central ground, perimeter landmarks, and no characters or UI.
Replace one PNG to revise one location without changing any other stage.

## Review

Build the game and import assets, then run `scenes/tests/WorldArtReview.tscn`
with a unique `--save-suffix=world-art-review-<id>` and `--capture`.
Add `--mobile-preview` to review the phone layout. The scene checks all 70
files, distinct image hashes, usable imported resolution, battle bounds,
shared tile edges and the artwork loaded by each zone's actual game screens.
It captures fresh/explored maps, normal/overview battles and five stage sheets
in `artifacts/world-art-review/`. Personal progress is not used by this review.

Local verification on 2026-10-01: 279 desktop checks and 274 phone-preview
checks passed, with 45 desktop and 40 phone captures. All 60 battle floor crops
and all 10 explored zone maps were visually reviewed. The game build, 7,492
data checks, 86 backend tests and the 42-check gameplay review on both layouts
also passed before the zone expansion. `ExplorationReview.tscn` now checks the
768-tile layouts, 40 spaced discoveries per zone, progressive entry costs,
smoke behavior and old-save migration. After the expansion, 102 exploration
checks and 43 gameplay checks passed on each layout; the 279/274 world-art
checks passed again. See `docs/EXPANSIVE_EXPLORATION.md`.
Phone preview is a desktop simulation; signed mobile builds still
need physical-device review before release.
