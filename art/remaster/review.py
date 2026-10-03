"""Multi-unit review grid: idle, walk, wind-up, contact, death and portrait per row."""
import sys
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]


def grid(ids, folder='preview', out='review.png', frames=('000', '004', '011', '014', '024'), bg=(52, 58, 62)):
    base = ROOT / 'artifacts/remaster' / folder
    w, h = 256, 320
    rows = [i for i in ids if (base / i / '000.png').exists()]
    canvas = Image.new('RGBA', (w * len(frames) + 320, h * len(rows)), bg + (255,))
    d = ImageDraw.Draw(canvas)
    for r, ident in enumerate(rows):
        for c, f in enumerate(frames):
            p = base / ident / f'{f}.png'
            if p.exists():
                canvas.alpha_composite(Image.open(p).convert('RGBA'), (c * w, r * h))
        p = base / ident / 'portrait.png'
        if p.exists():
            im = Image.open(p).convert('RGBA').resize((320, 320), Image.LANCZOS)
            canvas.alpha_composite(im, (len(frames) * w, r * h))
        d.text((6, r * h + 6), ident, fill=(240, 225, 190, 255))
    path = ROOT / 'artifacts/remaster' / out
    canvas.save(path)
    return path


if __name__ == '__main__':
    out = sys.argv[1]
    print(grid(sys.argv[2].split(','), out=out))
