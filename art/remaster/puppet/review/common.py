"""Shared paths and helpers for the puppet review tools (run them with art/remaster/puppet/run.sh)."""
import json
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[4]
REVIEW = ROOT / 'artifacts/remaster'
UNITS_OUT = REVIEW / 'puppet_units'        # build.py renders: <id>/NNN.png + metadata.json
STRIDE = REVIEW / 'puppet_stride'          # people: master, Codex edits, landmarks, cut layers
PUPPET = REVIEW / 'puppet'                 # beasts, mounts, machines, blobs (and the people's old masters)
BG = (205, 200, 190)
PAGE = (240, 236, 228)
GROUND = (200, 30, 30)


def all_ids():
    return [u['Id'] for u in json.loads((ROOT / 'data/units.json').read_text())['Units']]


def ids_arg(args):
    """'all', comma lists and spaced ids, as one list."""
    out = []
    for a in args:
        out += all_ids() if a == 'all' else [x for x in a.split(',') if x]
    return out


def source_dir(ident):
    """The unit's painting sources (as build.source_dir picks them)."""
    return STRIDE / ident if (STRIDE / ident / 'layers.json').exists() else PUPPET / ident


def meta(ident, src=UNITS_OUT):
    return json.loads((Path(src) / ident / 'metadata.json').read_text())


def clip_range(ident, clip, src=UNITS_OUT):
    """Absolute frame indices of a clip (idle, walk, attack, cast, death ...) in a rendered unit."""
    a = meta(ident, src)['animations'][clip]
    return list(range(a['start'], a['start'] + a['count']))


def frame(ident, i, src=UNITS_OUT):
    return Image.open(Path(src) / ident / f'{i:03d}.png').convert('RGBA')


def on_bg(im, bg=BG):
    out = Image.new('RGBA', im.size, bg + (255,))
    out.alpha_composite(im)
    return out


def ground_y(ident, src=UNITS_OUT):
    m = meta(ident, src)
    return m['anchorY'] * m['frameHeight']


def union_box(ims, pad=4, floor=None):
    """The box holding every image's paint (and the ground line `floor`, if given)."""
    bb = None
    for im in ims:
        b = im.getchannel('A').getbbox() or (0, 0, im.width, im.height)
        bb = b if bb is None else (min(bb[0], b[0]), min(bb[1], b[1]), max(bb[2], b[2]), max(bb[3], b[3]))
    x0, y0, x1, y1 = bb
    if floor is not None:
        y1 = max(y1, int(floor) + 2)
    return x0 - pad, y0 - pad, x1 + pad, y1 + pad


def stack(rows, gap=8, bg=(255, 255, 255)):
    out = Image.new('RGB', (max(r.width for r in rows), sum(r.height + gap for r in rows)), bg)
    y = 0
    for r in rows:
        out.paste(r, (0, y))
        y += r.height + gap
    return out


def label(im, text, xy=(4, 4)):
    ImageDraw.Draw(im).text(xy, text, fill=(0, 0, 0))
    return im


def short(ident):
    return ident.replace('player_', 'p_').replace('enemy_', 'e_')
