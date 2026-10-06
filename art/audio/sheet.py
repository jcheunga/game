"""Contact sheet of rendered review files: python sheet.py out.png prefix ..."""
import sys
from pathlib import Path

from ak import io

REVIEW = Path(__file__).resolve().parents[2] / "artifacts" / "audio-review" / "sfx"
out, prefixes = sys.argv[1], sys.argv[2:]
paths = sorted(p for p in REVIEW.glob("*.ogg") if any(p.stem.startswith(x) for x in prefixes))
io.contact_sheet(paths, out, cols=8)
print(len(paths), "files ->", out)
