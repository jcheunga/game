"""Icon recipe registry and the shared material kit handed to every recipe."""
import math

import bpy
from mathutils import Matrix, Vector

from rk import geo, palette, shaders as S
from . import itemkit as P

REGISTRY = {}
ORDER = []

# rarity / faction accent colours
TEAL = '1c6e69'
BRASS = 'c89a48'
AMBER = 'ffb04a'
CRIMSON = '7a1e30'
PLAGUE = '9cf25c'
SOUL = '6ff0d2'


def icon(category, ident, **opts):
    def deco(fn):
        REGISTRY[ident] = (category, fn, opts)
        ORDER.append(ident)
        return fn
    return deco


def V(*a):
    return Vector(a)


class Kit:
    """Material shortcuts + the collection every recipe builds into."""

    def __init__(self, ident, coll):
        self.ident = ident
        self.coll = coll
        self._L = None
        self._R = None

    @property
    def L(self):
        if self._L is None:
            self._L = palette.lantern()
        return self._L

    @property
    def R(self):
        if self._R is None:
            self._R = palette.rotbound()
        return self._R

    # ---- metals
    def gilt(self, color='e3b04c', rough=0.2):
        return P.metal_tint(color, rough=rough, name='Gilt ' + color, edge=1.45)

    def old_gold(self, color='c99a45'):
        return S.gold(color, rough=0.3, name='Old gold ' + color, edge=1.5, cavity=0.34)

    def brass(self):
        return S.gold(BRASS, rough=0.3, name='Antique brass', edge=1.45, cavity=0.36)

    def silver(self, color='d4d8de', rough=0.2):
        return P.metal_tint(color, rough=rough, name='Silver ' + color, edge=1.3)

    def steel(self, color='b4bcc2', rough=0.26):
        return S.metal(color, rough=rough, name='Steel ' + color, edge=1.5, hammer=0.1, cavity=0.45)

    def iron(self, color='4a5056', rough=0.4):
        return S.metal(color, rough=rough, name='Iron ' + color, edge=2.2, hammer=0.3, cavity=0.4)

    def black_iron(self):
        return S.metal('2a2d31', rough=0.36, name='Black iron', edge=2.8, hammer=0.25, cavity=0.45)

    # ---- organics
    def leather(self, color='5a3b24'):
        return S.leather(color, name='Leather ' + color)

    def wood(self, color='6b4a2e', axis='Z'):
        return S.wood(color, name='Wood ' + color + axis, axis=axis)

    def cloth(self, color, var='', sheen=0.7):
        return S.cloth(color, name='Cloth ' + color, var=var, sheen=sheen)

    def bone(self, color='e2d6b4'):
        return S.bone(color, name='Bone ' + color, crack=0.3)

    def stone(self, color='8a8478', moss=0.0, crack=0.5):
        return P.masonry(color, name=f'Stone {color} {moss:.2f}', moss=moss, crack=crack)

    # ---- magic
    def gem(self, color, glow=1.5):
        return P.crystal(color, glow=glow, name='Gem ' + color)

    def fire(self, strength=6.0, hot='fff4cf', mid='ffb43a', outer='ff5a14', smoke='a01c06', opacity=1.0, soft=0.55,
             breakup=0.0):
        stops = ((0.0, hot, 1.0), (0.22, mid, 1.0), (0.55, outer, 0.95), (0.85, smoke, 0.5), (1.0, smoke, 0.0))
        return P.fx(stops, strength=strength, name='Fire ' + hot + outer + f'{soft:.2f}{breakup:.2f}', soft=soft,
                    opacity=opacity, noise_amt=0.25, noise_scale=4.0, noise_alpha=breakup)

    def energy(self, color, strength=5.0, hot='ffffff', soft=0.7, opacity=1.0, fade_in=0.08, fade_out=0.75):
        stops = ((0.0, hot, 0.0), (fade_in, hot, 1.0), (0.35, color, 1.0), (fade_out, color, 0.85), (1.0, color, 0.0))
        return P.fx(stops, strength=strength, name='Energy ' + color + f'{opacity:.2f}', soft=soft, opacity=opacity)

    def beam(self, color, strength=5.0, hot='ffffff', opacity=1.0, soft=0.6):
        """Uniform glow along the whole length (use for bolts, rings, runes)."""
        stops = ((0.0, hot, 1.0), (0.5, color, 1.0), (1.0, color, 1.0))
        return P.fx(stops, strength=strength, name='Beam ' + color + hot + f'{opacity:.2f}', soft=soft, opacity=opacity)

    def wave(self, color, strength=3.0, opacity=0.8):
        stops = ((0.0, color, 1.0), (1.0, color, 1.0))
        return P.fx(stops, strength=strength, name='Wave ' + color + f'{opacity:.2f}', soft=0.0, opacity=opacity,
                    width_pow=1.2)

    def halo(self, color, strength=2.0, opacity=0.5, power=2.4, core=''):
        return P.halo(color, strength=strength, power=power, opacity=opacity, core=core, name='Halo ' + color)

    def glow(self, color, strength=8.0, core='fffaf0'):
        return P.glow_solid(color, strength=strength, core=core, name='Glow ' + color)

    def spark_mat(self, color, hot='fffbe8', strength=8.0):
        stops = ((0.0, hot, 1.0), (0.4, color, 1.0), (1.0, color, 0.0))
        return P.fx(stops, strength=strength, name='Spark ' + color, soft=0.0)


# ---------------------------------------------------------------------- shared helpers
def wrap_cylinder(obj, radius, phase=0.0, flare=0.0, z0=0.0):
    """Bend a flat strip authored in XZ (x = arc length, y = radial offset outwards = -y) around Z.
    x=0 lands at the front (-Y). flare widens the radius with height (cone)."""
    me = obj.data
    for v in me.vertices:
        x, y, z = v.co
        r = radius - y + flare * max(0.0, z - z0)
        a = x / radius + phase
        v.co = V(r * math.sin(a), -r * math.cos(a), z)
    me.update()
    geo.store_rest(obj)
    return obj


def objects_in(coll):
    out = []
    for o in coll.all_objects:
        out.append(o)
    return out


def tilt_all(coll, rot_deg=(0, 0, 0), loc=(0, 0, 0), scale=1.0):
    """Rotate/translate every mesh in the collection by baking into mesh data (keeps `rest`)."""
    m = Matrix.LocRotScale(Vector(loc), geo._euler(rot_deg).to_quaternion(), Vector((scale, scale, scale)))
    for o in coll.all_objects:
        if o.type == 'MESH':
            o.data.transform(m)
            o.data.update()
    return m


def root_transform(coll, rot_deg=(0, 0, 0), loc=(0, 0, 0), scale=1.0, name='Icon root'):
    """Parent everything to an empty and transform the empty (rest attributes stay put)."""
    root = bpy.data.objects.new(name, None)
    coll.objects.link(root)
    for o in list(coll.objects):
        if o is root or o.parent is not None:
            continue
        o.parent = root
    root.location = loc
    root.rotation_euler = geo._euler(rot_deg)
    root.scale = (scale, scale, scale)
    bpy.context.view_layer.update()
    return root


def xform(objs, loc=(0, 0, 0), rot=(0, 0, 0), scale=1.0):
    """Bake a transform into a list of objects' mesh data (rotation in degrees, XYZ)."""
    m = Matrix.LocRotScale(Vector(loc), geo._euler(rot).to_quaternion(), Vector((scale, scale, scale)))
    for o in objs:
        if o.type == 'MESH':
            geo.transform(o, m)
    return objs


def aim_rot(direction, up=(0, 0, 1)):
    """Euler (degrees) that turns +Z towards `direction`."""
    q = Vector((0, 0, 1)).rotation_difference(Vector(direction).normalized())
    return tuple(math.degrees(a) for a in q.to_euler())
