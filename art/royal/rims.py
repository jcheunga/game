"""Tidies the outside of framed kit pieces cut from the concepts.

A piece cut from a row of touching tabs or from a busy plate carries whatever lay beyond its frame: a corner of the
next tab, a plate ornament, a patch of background. Drawn on its own those show as ragged corners.

  clear_outside(image)    everything outside the gold rim becomes transparent
  symmetric(image)        the left half mirrored onto the right
"""
from PIL import Image


def _bright(pixel):
    r, g, b, a = pixel
    return a > 128 and (0.299 * r + 0.587 * g + 0.114 * b > 150 or (r - b > 45 and 0.299 * r + 0.587 * g + 0.114 * b > 85))


def _rim(image):
    """The frame's gold rim: every bright pixel joined to the middle of the top edge."""
    w, h = image.size
    pixels = image.load()
    start = next(((w // 2, y) for y in range(h // 3) if _bright(pixels[w // 2, y])), None)
    if start is None:
        raise ValueError("no rim at the top edge")
    rim, stack = set(), [start]
    while stack:
        x, y = stack.pop()
        if (x, y) in rim or not (0 <= x < w and 0 <= y < h) or not _bright(pixels[x, y]):
            continue
        rim.add((x, y))
        stack += [(x + dx, y + dy) for dx in (-1, 0, 1) for dy in (-1, 0, 1) if dx or dy]
    return rim


def clear_outside(image):
    """Clears what lies outside the rim: flooding in from the four corners, stopped only by the rim itself."""
    image = image.convert("RGBA")
    w, h = image.size
    pixels = image.load()
    rim = _rim(image)
    seen, stack = set(), [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)]
    while stack:
        x, y = stack.pop()
        if (x, y) in seen or not (0 <= x < w and 0 <= y < h) or (x, y) in rim:
            continue
        seen.add((x, y))
        stack += [(x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)]
    # Corners and slivers are a few percent of a piece; a flood that reaches far in found a gap in a dim rim.
    if len(seen) > 0.1 * w * h:
        raise ValueError(f"the rim has a gap: clearing would take {len(seen) * 100 // (w * h)}% of the piece")
    for x, y in seen:
        r, g, b, _ = pixels[x, y]
        pixels[x, y] = (r, g, b, 0)
    return image


def symmetric(image):
    """Mirrors the left half onto the right, for a piece whose right edge was cut through a shared divider."""
    image = image.convert("RGBA")
    w, h = image.size
    half = image.crop((0, 0, w // 2, h)).transpose(Image.FLIP_LEFT_RIGHT)
    image.paste(half, (w - half.width, 0))
    return image
