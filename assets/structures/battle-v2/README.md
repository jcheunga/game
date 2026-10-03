# Battle base presentation

The caravan, all seven skins and the gatehouse are rendered from the existing
editable Blender models. Source models and earlier sprites remain intact.

`art/blender/render_battle_structures.py` uses one orthographic camera at a
22-degree elevation, a warm upper-left key and soft sky fill. The caravan has
a horizontal side view; the gatehouse turns toward approaching troops on its
left. PNGs are 1024 × 1024 RGBA with lossless imports and mipmaps.

Each JSON stores camera-projected ground anchors, wheel/foundation contacts,
roof weapon sockets, lamp ground positions and the damage-smoke position.
Runtime drawing preserves proportions and uses these points instead of
centering transparent image rectangles on the battle ground.

Animated troop and structure silhouettes cast soft shadows on a separate
ground layer above terrain and below all actors. Contact shadows, per-zone
light color and restrained lantern/torch pools tie them to the scene. Bases
and troops sort by their ground positions; airborne projectiles sit above
them. Health bars keep their original UI colors.

Reproduce with:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background \
  --python art/blender/render_battle_structures.py -- --samples 48
```

`--ids=gatehouse,war_wagon` selects assets. `--metadata-only` refreshes projected
points without rendering images or changing model files.
