"""Scan and pitch-verify every registered instrument; prints a report."""
import sys
from ak import instruments

names = sys.argv[1:] or list(instruments.SPEC)
for name in names:
    try:
        print(instruments.get(name).report(), flush=True)
    except Exception as exc:  # noqa: BLE001
        print(f"{name:24s} ERROR {exc}", flush=True)
