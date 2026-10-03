"""In-engine-like validation of battle-v2 structures (python3 + Pillow).

python3 art/remaster/battle_sheet.py [--new artifacts/remaster/structures/battle-v2]
        [--mounts artifacts/remaster/structures] [--out .../battle-v2/contact.png] [--ids a,b]

Mimics scripts/combat (BattleStructureArt / BattleController.Lighting / BattleLighting /
DrawBaseMount): each PNG is drawn at its JSON width with its anchor on a ground line over a crop of
assets/world/battles/stage-01.png. Ground layer first (base contact, alpha-silhouette shadow
projected down-right with the default zone ShadowCast, wheel/foundation contacts, lamp pools), then
the structure, then the mount sprites in their runtime rect (-30,-59,60,75) around each socket
(mirrored on the gatehouse). Annotated panels add dots: red sockets, green contacts, orange lights,
grey smoke, white anchor. Each row: CURRENT original | NEW remaster at 2x, then NEW at true 1x.
"""
import argparse
import json
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
OLD = ROOT / 'assets/structures/battle-v2'
BG = ROOT / 'assets/world/battles/stage-01.png'
SKINS = ['iron', 'royal', 'bone', 'flame', 'shadow', 'guild', 'legendary']
ALL = ['war_wagon'] + [f'war_wagon_skin_{s}' for s in SKINS] + ['gatehouse']
SHADOW_CAST = (0.76, 0.29)          # BattleLighting default zone
SHADOW_OPACITY = 0.29
LAMP_STRENGTH = 0.4                 # default zone tint R >= .96
TINT = (1.10, 1.05, 0.96)           # FieldLighting.Tint applied to mount sprites
PANEL = (46, 52, 58, 255)
INK = (232, 222, 196, 255)
TILE = (300, 270)                   # game px
GROUND_Y = 0.84


def backdrop(w, h, seed=0):
    bg = Image.open(BG).convert('RGBA')
    W, H = bg.size
    # the game stretches the floor band (.08,.36,.84,.28) over the field: ~1.54 game px per plate px
    k = 1.54
    x0 = int(W * .2) + seed * 37 % int(W * .4)
    y0 = int(H * .42)
    return bg.crop((x0, y0, x0 + int(w / k), y0 + int(h / k))).resize((w, h), Image.LANCZOS)


def load_art(folder, ident):
    png, meta = Path(folder) / f'{ident}.png', Path(folder) / f'{ident}.json'
    if not png.exists() or not meta.exists():
        return None
    return Image.open(png).convert('RGBA'), json.loads(meta.read_text())


def ellipse_layer(size, centre, radius, rgba, layers=4, spread=.22, base_alpha=.045, step=.017, opacity=1.0):
    """BattleLighting.DrawContact: layered translucent ellipses."""
    lay = Image.new('RGBA', size, (0, 0, 0, 0))
    for layer in range(layers - 1, -1, -1):
        s = 1 + layer * spread
        a = opacity * (base_alpha + (layers - 1 - layer) * step)
        e = Image.new('RGBA', size, (0, 0, 0, 0))
        ImageDraw.Draw(e).ellipse((centre[0] - radius[0] * s, centre[1] - radius[1] * s,
                                   centre[0] + radius[0] * s, centre[1] + radius[1] * s), fill=rgba[:3] + (int(255 * a),))
        lay = Image.alpha_composite(lay, e)
    return lay


def lamp_glow(size, centre, castle, k):
    rx, ry = 36 * k, 15 * k
    col = (107, 255, 255) if castle else (255, 235, 97)
    g = Image.new('L', (int(rx * 2), int(ry * 2)), 0)
    d = ImageDraw.Draw(g)
    for i in range(20, 0, -1):
        t = i / 20
        d.ellipse((rx - rx * t, ry - ry * t, rx + rx * t, ry + ry * t), fill=int(255 * (1 - t) ** 1.3 * .13 * LAMP_STRENGTH * 2.2))
    lay = Image.new('RGBA', size, (0, 0, 0, 0))
    lay.paste(Image.new('RGBA', g.size, col + (255,)), (int(centre[0] - rx), int(centre[1] - ry)), g)
    return lay


def compose(art, mounts_dir, ident, k=2, annotate=True, seed=0):
    tex, meta = art
    W, H = TILE[0] * k, TILE[1] * k
    canvas = backdrop(W, H, seed)
    castle = ident == 'gatehouse'
    ground = (W / 2, H * GROUND_Y)
    width = meta['width'] * k
    scale = width / tex.width
    size = (int(round(tex.width * scale)), int(round(tex.height * scale)))
    sprite = tex.resize(size, Image.LANCZOS)
    ax, ay = meta['anchor']
    x0, y0 = ground[0] - ax * size[0], ground[1] - ay * size[1]

    def point(uv):
        return ground[0] + (uv[0] - ax) * size[0], ground[1] + (uv[1] - ay) * size[1]

    # ---- ground layer (BattleController.DrawStructureGround)
    canvas = Image.alpha_composite(canvas, ellipse_layer(canvas.size, (ground[0], ground[1] - 5 * k),
                                                         ((66 if castle else 67) * k, (12 if castle else 8) * k),
                                                         (6, 9, 13), opacity=.55))
    cx, cy = SHADOW_CAST
    rx0, ry0 = x0 - ground[0], y0 - ground[1]          # sprite rect relative to the feet
    fx, fy = ground
    data = (1, -cx / cy, -fx + cx / cy * fy - rx0, 0, -1 / cy, fy / cy - ry0)
    sil = sprite.split()[3].transform(canvas.size, Image.AFFINE, data, resample=Image.BILINEAR)
    sil = sil.filter(ImageFilter.GaussianBlur(1.6 * k))
    shadow = Image.new('RGBA', canvas.size, (5, 7, 11, 0))
    shadow.putalpha(sil.point(lambda v: int(v * SHADOW_OPACITY * .95)))
    canvas = Image.alpha_composite(canvas, shadow)
    for c in meta['contacts']:
        canvas = Image.alpha_composite(canvas, ellipse_layer(canvas.size, point(c), ((19 if castle else 7) * k, (5 if castle else 2.8) * k),
                                                             (6, 9, 13), opacity=.8))
    for l in meta['lights']:
        canvas = ImageChops.add(canvas.convert('RGB'), lamp_glow(canvas.size, point(l), castle, k).convert('RGB')).convert('RGBA')
    # ---- structure, then mounts on their sockets
    canvas.alpha_composite(sprite, (int(round(x0)), int(round(y0))))
    kinds = ['ballista'] if castle else ['arrows', 'ballista', 'firepot']
    for kind, socket in zip(kinds, meta['mounts']):
        p = Path(mounts_dir) / f'mount_{kind}.png'
        if not p.exists():
            continue
        m = Image.open(p).convert('RGBA').resize((60 * k, 75 * k), Image.LANCZOS)
        r, g, b, a = m.split()
        m = Image.merge('RGBA', [ch.point(lambda v, t=t: min(255, int(v * t))) for ch, t in zip((r, g, b), TINT)] + [a])
        if castle:
            m = m.transpose(Image.FLIP_LEFT_RIGHT)
        sx, sy = point(socket)
        canvas.alpha_composite(m, (int(round(sx - 30 * k)), int(round(sy - 59 * k))))
    if annotate:
        d = ImageDraw.Draw(canvas)
        r = 2.5 * k / 2

        def dot(p, col):
            d.ellipse((p[0] - r, p[1] - r, p[0] + r, p[1] + r), fill=col, outline=(0, 0, 0, 255))
        for s in meta['mounts']:
            dot(point(s), (235, 60, 60, 255))
        for c in meta['contacts']:
            dot(point(c), (70, 220, 90, 255))
        for l in meta['lights']:
            dot(point(l), (255, 170, 40, 255))
        dot(point(meta['smoke']), (170, 170, 170, 255))
        d.line((ground[0] - 6 * k, ground[1], ground[0] + 6 * k, ground[1]), fill=(255, 255, 255, 255), width=k)
        d.line((ground[0], ground[1] - 6 * k, ground[0], ground[1] + 6 * k), fill=(255, 255, 255, 255), width=k)
        d.rectangle((x0, y0, x0 + size[0], y0 + size[1]), outline=(255, 255, 255, 70))
    return canvas


def row(ident, new_dir, mounts_dir):
    old, new = load_art(OLD, ident), load_art(new_dir, ident)
    tiles = []
    for label, art, md in (('CURRENT (assets/structures/battle-v2)', old, ROOT / 'assets/structures'),
                           ('NEW remaster', new, mounts_dir)):
        if art is None:
            t = Image.new('RGBA', (TILE[0] * 2, TILE[1] * 2), PANEL)
            ImageDraw.Draw(t).text((8, 8), label + ': missing', fill=INK)
        else:
            t = compose(art, md, ident, 2, True, seed=sum(map(ord, ident)) % 7)
            ImageDraw.Draw(t).text((6, 4), label, fill=INK)
        tiles.append(t)
    if new is not None:
        one = compose(new, mounts_dir, ident, 1, False, seed=sum(map(ord, ident)) % 7)
        tiles.append(one)
    w = sum(t.width for t in tiles) + 12 * len(tiles)
    h = max(t.height for t in tiles) + 22
    c = Image.new('RGBA', (w, h), PANEL)
    ImageDraw.Draw(c).text((6, 4), f'{ident}   current | new (2x, annotated) | new (1x, as drawn)', fill=INK)
    x = 0
    for t in tiles:
        c.alpha_composite(t, (x, 20))
        x += t.width + 12
    return c


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--new', default=str(ROOT / 'artifacts/remaster/structures/battle-v2'))
    ap.add_argument('--mounts', default=str(ROOT / 'artifacts/remaster/structures'))
    ap.add_argument('--out', default=None)
    ap.add_argument('--ids', default='all')
    a = ap.parse_args()
    ids = ALL if a.ids == 'all' else a.ids.split(',')
    rows = [row(i, a.new, a.mounts) for i in ids]
    W = max(r.width for r in rows)
    sheet = Image.new('RGBA', (W, sum(r.height for r in rows)), PANEL)
    y = 0
    for r in rows:
        sheet.alpha_composite(r, (0, y))
        y += r.height
    out = Path(a.out) if a.out else Path(a.new) / 'contact.png'
    sheet.convert('RGB').save(out)
    print(out)


if __name__ == '__main__':
    main()
