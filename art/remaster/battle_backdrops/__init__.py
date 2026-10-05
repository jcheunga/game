"""Layered battle backdrop recipes, one module per zone (see frame.py). Each module defines MOOD (sky and sun),
optional LAYERS/FOG/LOOK overrides, and far(L, scene, mood), mid(L, scene, mood), near(L, scene, mood)."""
import importlib

NAMES = ('city', 'harbor', 'foundry', 'quarantine', 'thornwall', 'basilica', 'mire', 'steppe', 'gloamwood', 'citadel')
ZONES = {}
for _name in NAMES:
    try:
        ZONES[_name] = importlib.import_module(f'{__name__}.{_name}')
    except ModuleNotFoundError as err:
        if err.name != f'{__name__}.{_name}':
            raise
