"""Unit recipes. Each recipe builds a fully rigged Character and returns render options."""
REGISTRY = {}


def unit(ident):
    def deco(fn):
        REGISTRY[ident] = fn
        return fn
    return deco


def load_all():
    from . import player, enemy, bosses, beasts  # noqa: F401
    from . import caravan, host, host2, host3, host4, mounts, siege  # noqa: F401  (the roster-sheet style; overrides the older recipes)
    return REGISTRY
