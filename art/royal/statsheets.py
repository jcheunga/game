#!/usr/bin/env python3
"""Paint the concept's stat tiles out of the armory plates, so screens draw their own compact stat sheet.

The warband and spells concepts paint one dark plaque per stat. The live sheet (StatSheet.cs) sizes itself to
its stats, so the plaques must go. Codex repaints a crop of the profile column without them
(art/royal/jobs/13-stat-sheets.json); this script registers the repaint onto the crop and keeps only the
plaques' footprint (grown a little for their shadow, feathered), so everything around them stays the original
painting. plates.py pastes the patch after its relayout.

  python3 art/royal/statsheets.py crop                       # crops for the Codex job (from the unpatched plates)
  python3 art/royal/codexgen.py --jobs 13-stat-sheets        # Codex repaints
  art/remaster/puppet/run.sh art/royal/statsheets.py patch   # registered, masked patches (numpy/opencv)
  python3 art/royal/plates.py warband spells                 # install the patched plates
"""
import json
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))
import plates  # noqa: E402

GEN = ROOT / "art/royal/gen/stat-sheets"
DENSITY = 1672 / 1280
# Plate-pixel origin of each profile-column crop.
CROPS = {"warband": (945, 345, 1640, 895), "spells": (945, 335, 1640, 895)}
GROW = 7       # plate px around each plaque (its drop shadow)
FEATHER = 3.0  # Gaussian sigma of the patch edge


def tiles(name):
    spec = json.loads((ROOT / f"art/royal/specs/{name}.json").read_text())
    return [e["rect"] for e in spec["elements"] if e.get("role") == "tile" and e["id"].startswith("stat.")]


def crop():
    GEN.mkdir(parents=True, exist_ok=True)
    for name, box in CROPS.items():
        image = plates.compose(name, patch=False).crop(box)
        flat = Image.new("RGBA", image.size, (0, 0, 0, 255))
        flat.alpha_composite(image)
        flat.convert("RGB").save(GEN / f"{name}-detail.png")
        print(name, image.size)


def patch():
    import numpy as np
    import cv2

    for name, box in CROPS.items():
        detail = np.array(Image.open(GEN / f"{name}-detail.png").convert("RGB"))
        clean = np.array(Image.open(GEN / f"{name}-clean.png").convert("RGB"))
        # Codex edits come back within a pixel or two of the original; SIFT matches put them back exactly.
        sift = cv2.SIFT_create(4000)
        grey = lambda a: cv2.cvtColor(a, cv2.COLOR_RGB2GRAY)
        kc, dc = sift.detectAndCompute(grey(clean), None)
        kd, dd = sift.detectAndCompute(grey(detail), None)
        good = [a for a, b in cv2.BFMatcher().knnMatch(dc, dd, k=2) if a.distance < .75 * b.distance]
        matrix, inliers = cv2.estimateAffinePartial2D(np.float32([kc[m.queryIdx].pt for m in good]),
                                                      np.float32([kd[m.trainIdx].pt for m in good]),
                                                      method=cv2.RANSAC, ransacReprojThreshold=2.0)
        h, w = detail.shape[:2]
        warped = cv2.warpAffine(clean, matrix, (w, h), flags=cv2.INTER_LANCZOS4, borderMode=cv2.BORDER_REFLECT)
        mask = np.zeros((h, w), np.float32)
        for x, y, tw, th in tiles(name):
            x0, y0 = round(x * DENSITY - box[0] - GROW), round(y * DENSITY - box[1] - GROW)
            x1, y1 = round((x + tw) * DENSITY - box[0] + GROW), round((y + th) * DENSITY - box[1] + GROW)
            mask[max(0, y0):y1, max(0, x0):x1] = 1
        mask = cv2.GaussianBlur(mask, (0, 0), FEATHER)
        out = np.dstack([warped, (mask * 255).astype(np.uint8)])
        Image.fromarray(out, "RGBA").save(GEN / f"{name}-patch.png")
        print(name, f"{int(inliers.sum())}/{len(good)} matches", "scale %.4f" % np.hypot(*matrix[0, :2]),
              "shift %.1f,%.1f" % tuple(matrix[:, 2]))


if __name__ == "__main__":
    {"crop": crop, "patch": patch}[sys.argv[1] if len(sys.argv) > 1 else "patch"]()
