# Battle base presentation

The caravan and all seven skins are rendered in Blender from the
remaster siege recipes in `art/remaster/siege/`. The enemy side is a painted stronghold per stage
(`assets/structures/bases`, see docs/ROYAL_UI.md "Enemy strongholds"), so the Blender gatehouse and its
outworks are no longer shipped (the recipes can still render them). The flat sprites in
`assets/structures/` remain as fallbacks and still supply the `mount_*` weapons.

`art/remaster/build_battle_structures.py` uses one orthographic camera at a
22-degree elevation and the units' battle sun from the upper left, with no baked
ground shadow. The caravan has a horizontal side view. Caravan and skin plates
are 1024 × 1024 RGBA; door strips are cropped. All import with mipmaps.

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
of its authored `width`, so the caravan is two to three soldiers tall. `centre` is the plate's alpha centroid.

Reproduce with:

```sh
blender --background --factory-startup --python-exit-code 1 \
  --python art/remaster/build_battle_structures.py -- --ids all --samples 128 --no-blend
```

`--ids=war_wagon,war_wagon_skin_bone` selects assets. Renders go to
`artifacts/remaster/structures/battle-v2/`; copy the caravan PNGs and JSON here
(`--ids all` also renders the retired gatehouse and `fort_*` outworks, which the
game no longer draws).
`--metadata-only` refreshes projected points without rendering images.
