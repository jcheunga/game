# Concept screen specs

Each `<screen>.json` measures one concept screen on its 1280x720 reference in `art/royal/ref/`
(canvas units, the game's fixed canvas). Screens are built from these numbers; captures are compared
against the same references.

- `python3 art/royal/grid.py <ref> x y w h [scale] [out.png]` zooms a region with a labelled grid.
- `python3 art/royal/specview.py <spec> [out.png] [--crop x,y,w,h] [--scale 3] [--write]` overlays the spec:
  text in magenta on the concept lettering, rects as cyan outlines. `--write` stores width-fitted sizes.

Text elements: `text` (casing as rendered: Cinzel draws lowercase as small caps, so "Warband" gives a tall W
and small-cap ARBAND, while "SQUAD 5 / 6" is all full caps), `font` (`cinzel` engraved capitals, `crimson`
book serif), `weight`, `width` (measured ink width; the size is fitted to it), `x` (ink left edge, centre for
`align: center`, right edge for `align: right`), `baseline`, `color` (sampled letter fill).
Rect elements: `rect` [x, y, w, h] tight on the visible frame edge, `role`.
