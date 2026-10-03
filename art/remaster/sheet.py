"""Contact sheet helper for preview folders (runs with system python + Pillow)."""
import sys
from pathlib import Path
from PIL import Image, ImageDraw


def sheet(folder, out=None, scale=1.0, bg=(46, 52, 58)):
    folder = Path(folder)
    frames = sorted(p for p in folder.glob('[0-9][0-9][0-9].png'))
    portrait = folder / 'portrait.png'
    ims = [Image.open(p).convert('RGBA') for p in frames]
    w, h = ims[0].size if ims else (256, 320)
    w, h = int(w * scale), int(h * scale)
    cols = min(8, len(ims)) or 1
    rows = (len(ims) + cols - 1) // cols
    pw = 512 if portrait.exists() else 0
    canvas = Image.new('RGBA', (cols * w + pw, max(rows * h, pw)), bg + (255,))
    d = ImageDraw.Draw(canvas)
    for i, (p, im) in enumerate(zip(frames, ims)):
        im = im.resize((w, h), Image.LANCZOS)
        x, y = (i % cols) * w, (i // cols) * h
        canvas.alpha_composite(im, (x, y))
        d.text((x + 4, y + 4), p.stem, fill=(230, 220, 190, 255))
    if portrait.exists():
        canvas.alpha_composite(Image.open(portrait).convert('RGBA'), (cols * w, 0))
    out = out or folder / 'sheet.png'
    canvas.save(out)
    return out


if __name__ == '__main__':
    for f in sys.argv[1:]:
        print(sheet(f))
