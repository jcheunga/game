# Battle base presentation

The caravan, all seven skins and the gatehouse are rendered in Blender from the
remaster siege recipes in `art/remaster/siege/`. The flat sprites in
`assets/structures/` remain as fallbacks and still supply the `mount_*` weapons.

`art/remaster/build_battle_structures.py` uses one orthographic camera at a
22-degree elevation and the units' battle sun from the upper left, with no baked
ground shadow. The caravan has a horizontal side view; the gatehouse turns
toward approaching troops on its left. Caravan, skin and gatehouse plates are
1024 × 1024 RGBA; door strips and outworks are cropped. All import lossless
with mipmaps.

Each JSON stores camera-projected ground anchors, wheel/foundation contacts,
roof weapon sockets, lamp ground positions and the damage-smoke position.
Runtime drawing preserves proportions and uses these points instead of
centering transparent image rectangles on the battle ground.

Animated troop and structure silhouettes cast soft shadows on a separate
ground layer above terrain and below all actors. Contact shadows, per-zone
light color and restrained lantern/torch pools tie them to the scene. Bases
and troops sort by their ground positions; airborne projectiles sit above
them. Health bars keep their original UI colors.

The caravan carries a troop hold slung between its wheels, closed by a ramp door
(`art/remaster/siege/wagon.py`, `troop_hold`). Each wagon renders shut, half-open
and lowered; `<id>_door.png` holds the opening frames, cropped to the region that
changes, and the JSON `door` block gives that rect, the frame count, the doorway
point where a troop appears (`exit`) and the ramp foot (`foot`). At runtime the
wagon stands so the ramp foot lands on the battle's centre line.

In battle every plate is drawn at `StructureScale` (data/combat_config.json, 0.58)
of its authored `width`, so the bases are two to three soldiers tall. `centre` is the plate's alpha centroid. The gatehouse is drawn so its visual
mass straddles the battle band rather than standing on its centre line.

`fort_tower`, `fort_wall`, `fort_palisade`, `fort_brazier` and `fort_totem`
(`art/remaster/siege/fortifications.py`) are outworks flanking the gatehouse.
They share its camera and scale and are cropped to their silhouettes (`width` is
the cropped image's world width). They are scenery only: never targets, never
obstacles.

Reproduce with:

```sh
blender --background --factory-startup --python-exit-code 1 \
  --python art/remaster/build_battle_structures.py -- --ids all --samples 128 --no-blend
```

`--ids=gatehouse,war_wagon,fort_tower` selects assets. Renders go to
`artifacts/remaster/structures/battle-v2/`; copy the PNGs and JSON here.
`--metadata-only` refreshes projected points without rendering images.
