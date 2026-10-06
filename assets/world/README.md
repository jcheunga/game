# Crownroad world artwork

Everything in `royal/` is painted art made with the pipeline in `art/royal` (see
[docs/ROYAL_UI.md](../../docs/ROYAL_UI.md)).

## Battle backdrops

Battles are fought in front of each zone's painted backdrop: five layers per zone
(`royal/<zone>_far.png`, `_mid`, `_ground`, `_near`, `_front` and `royal/<zone>.json`).
The far vista scrolls at 0.12 of the camera's speed and the mid row of scenery at
0.42; the road and the row of scenery lining it are locked to the field and repeat
along it; a blurred foreground is drawn in front of the troops at 1.35. The road
sits at the concept's screen rows 411–516. `art/royal/backdrops.py` assembles the
layers from the Codex generations and writes each layer's world rect and parallax
to the zone's JSON.

## Campaign maps

Each zone's map is one painting (`royal/maps/<zone>.png`) made over the zone's own
geography, drawn at the world rect in `royal/maps/<zone>.json` (which also records
the sea colour around it). The site landmarks are in `royal/maps/landmarks/`.
`art/royal/maps.py` installs both. See [Adventure map](../../docs/ADVENTURE_MAP.md)
for rendering, discovery and progression rules.

## Review

Build the game and run `scenes/tests/WorldArtReview.tscn` with a unique
`--save-suffix=world-art-review-<id>` and `--capture` (add `--zones=city,harbor`
to capture only some zones, and `--mobile-preview` for the phone layout). The
review checks that each of the ten zones has a distinct five-layer backdrop in
the proportions of its world rects, ordered far to near with the road layer
locked to the field and the foreground in front, and a painted map; it then opens
each zone's map (fresh and fully explored) and first battle to confirm both are
connected. Captures, plus a desktop backdrop gallery, are written to
`artifacts/world-art-review/` and do not use personal progress.
