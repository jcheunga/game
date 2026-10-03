"""Unit recipes. Each recipe builds a fully rigged Character and returns render options."""
REGISTRY = {}


def unit(ident):
    def deco(fn):
        REGISTRY[ident] = fn
        return fn
    return deco


def load_all():
    from . import player, enemy, bosses, beasts  # noqa: F401
    return REGISTRY
