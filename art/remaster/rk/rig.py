"""Armatures, skin weights, rigid binding and pose helpers.

Convention: characters face +X, their left is +Y, Z is up, ground is Z = 0.
The battle camera sits on the -Y side, so `.R` (right, -Y) is the near side.
`LAT` rotations are counter-clockwise as seen from that camera: a hanging limb
swings forward, an upright bone leans backward.
"""
import math

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.kdtree import KDTree

LAT = Vector((0, -1, 0))   # sagittal swing (ccw seen from camera)
YAW = Vector((0, 0, 1))    # turn about vertical
ROLL = Vector((1, 0, 0))   # lean sideways (top toward -Y for positive)


class Skeleton:
    """Armature builder: add bones by world head/tail, then finalize."""

    def __init__(self, name, coll):
        self.name = name
        self.data = bpy.data.armatures.new(name + ' rig')
        self.data.display_type = 'STICK'
        self.obj = bpy.data.objects.new(name + ' rig', self.data)
        coll.objects.link(self.obj)
        self.specs = []
        self.joints = {}

    def bone(self, name, head, tail, parent=None, deform=True, connect=False, roll=0.0):
        self.specs.append((name, Vector(head), Vector(tail), parent, deform, connect, roll))
        self.joints[name] = (Vector(head), Vector(tail))
        return name

    def build(self):
        view = bpy.context.view_layer
        view.objects.active = self.obj
        for o in view.objects:
            o.select_set(False)
        self.obj.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        eb = self.data.edit_bones
        for name, head, tail, parent, deform, connect, roll in self.specs:
            b = eb.new(name)
            b.head, b.tail = head, tail
            b.use_deform = deform
            # Align local X with the character's lateral axis wherever possible.
            b.align_roll(Vector((0, -1, 0)) if abs((tail - head).normalized().y) < 0.9 else Vector((0, 0, 1)))
            b.roll += roll
            if parent:
                b.parent = eb[parent]
                b.use_connect = connect
        bpy.ops.object.mode_set(mode='OBJECT')
        for pb in self.obj.pose.bones:
            pb.rotation_mode = 'QUATERNION'
        return self.obj


def bind_rigid(obj, arm, bone_name):
    """Parent geometry to a bone, keeping its current (rest) world placement."""
    bone = arm.data.bones[bone_name]
    obj.parent = arm
    obj.parent_type = 'BONE'
    obj.parent_bone = bone_name
    parent = arm.matrix_world @ bone.matrix_local @ Matrix.Translation((0, bone.length, 0))
    obj.matrix_parent_inverse = parent.inverted() @ obj.matrix_world
    obj.matrix_basis = Matrix.Identity(4)
    return obj


def bind_object(obj, parent_obj):
    obj.parent = parent_obj
    obj.matrix_parent_inverse = parent_obj.matrix_world.inverted()
    return obj


def _segments(arm, names):
    segs = []
    for n in names:
        b = arm.data.bones[n]
        segs.append((np.array(arm.matrix_world @ b.head_local), np.array(arm.matrix_world @ b.tail_local)))
    return segs


def _neighbors(me):
    n = len(me.vertices)
    nb = [[] for _ in range(n)]
    for e in me.edges:
        a, b = e.vertices
        nb[a].append(b)
        nb[b].append(a)
    return nb


def weight_by_bones(obj, arm, bones, power=6.0, smooth=6, smooth_factor=0.5, allow=None, radius_bias=None):
    """Distance-to-segment skin weights with topological smoothing.

    allow(co) -> iterable of permitted bone names for a vertex (optional).
    radius_bias: {bone: r} subtracted from distances so thick segments win their volume.
    """
    me = obj.data
    co = np.array([obj.matrix_world @ v.co for v in me.vertices])
    segs = _segments(arm, bones)
    d = np.zeros((len(co), len(bones)))
    for j, (a, b) in enumerate(segs):
        ab = b - a
        t = np.clip(((co - a) @ ab) / max(1e-9, ab @ ab), 0, 1)
        p = a + t[:, None] * ab
        d[:, j] = np.linalg.norm(co - p, axis=1)
        if radius_bias and bones[j] in radius_bias:
            d[:, j] = np.maximum(1e-3, d[:, j] - radius_bias[bones[j]])
    d = np.maximum(d, 1e-4)
    w = 1.0 / d ** power
    if allow is not None:
        mask = np.zeros_like(w)
        for i, c in enumerate(co):
            ok = set(allow[i]) if isinstance(allow, list) else set(allow(Vector(c)))
            for j, bn in enumerate(bones):
                if bn in ok:
                    mask[i, j] = 1
        w *= mask
        w[w.sum(1) == 0] = 1.0 / d[w.sum(1) == 0] ** power
    w /= w.sum(1, keepdims=True)
    if smooth:
        nb = _neighbors(me)
        for _ in range(smooth):
            avg = np.array([w[n].mean(0) if n else w[i] for i, n in enumerate(nb)])
            w = w * (1 - smooth_factor) + avg * smooth_factor
    # keep the strongest four influences
    idx = np.argsort(-w, axis=1)[:, :4]
    groups = {bn: obj.vertex_groups.get(bn) or obj.vertex_groups.new(name=bn) for bn in bones}
    for i in range(len(co)):
        row = w[i, idx[i]]
        row = row / row.sum()
        for j, val in zip(idx[i], row):
            if val > 0.01:
                groups[bones[j]].add([i], float(val), 'REPLACE')
    _armature_mod(obj, arm)
    return obj


def weight_single(obj, arm, bone):
    g = obj.vertex_groups.get(bone) or obj.vertex_groups.new(name=bone)
    g.add(list(range(len(obj.data.vertices))), 1.0, 'REPLACE')
    _armature_mod(obj, arm)
    return obj


def copy_weights(target, source, arm, k=6, blend=None):
    """Garment weights from nearest body vertices. blend(co, weights)->weights optional."""
    sme = source.data
    kd = KDTree(len(sme.vertices))
    for i, v in enumerate(sme.vertices):
        kd.insert(source.matrix_world @ v.co, i)
    kd.balance()
    names = [g.name for g in source.vertex_groups]
    sw = np.zeros((len(sme.vertices), len(names)))
    for v in sme.vertices:
        for g in v.groups:
            sw[v.index, g.group] = g.weight
    groups = [target.vertex_groups.get(n) or target.vertex_groups.new(name=n) for n in names]
    for v in target.data.vertices:
        co = target.matrix_world @ v.co
        found = kd.find_n(co, k)
        acc = np.zeros(len(names))
        tot = 0
        for _, idx, dist in found:
            wt = 1.0 / max(dist, 1e-4)
            acc += sw[idx] * wt
            tot += wt
        acc /= max(tot, 1e-9)
        wd = {names[j]: acc[j] for j in range(len(names)) if acc[j] > 0.01}
        if blend:
            wd = blend(co, wd)
        s = sum(wd.values()) or 1
        for n, val in wd.items():
            g = target.vertex_groups.get(n) or target.vertex_groups.new(name=n)
            g.add([v.index], float(val / s), 'REPLACE')
    _armature_mod(target, arm)
    return target


def weight_fn(obj, arm, fn):
    """Explicit weights: fn(world co) -> {bone: weight}."""
    for v in obj.data.vertices:
        wd = fn(obj.matrix_world @ v.co)
        s = sum(wd.values()) or 1
        for n, val in wd.items():
            if val <= 0:
                continue
            g = obj.vertex_groups.get(n) or obj.vertex_groups.new(name=n)
            g.add([v.index], float(val / s), 'REPLACE')
    _armature_mod(obj, arm)
    return obj


def _armature_mod(obj, arm):
    if not any(m.type == 'ARMATURE' for m in obj.modifiers):
        mod = obj.modifiers.new('Rig', 'ARMATURE')
        mod.object = arm
        mod.use_deform_preserve_volume = True
        # Armature first so later smoothing/subdivision follows the posed shape.
        if obj.modifiers.find('Rig') > 0:
            obj.modifiers.move(obj.modifiers.find('Rig'), 0)
    obj.parent = arm
    obj.parent_type = 'OBJECT'
    obj.matrix_parent_inverse = arm.matrix_world.inverted()


class Poser:
    """Compose bone rotations in armature rest space; keyframe whole poses."""

    def __init__(self, arm):
        self.arm = arm
        self.rest = {b.name: b.matrix_local.to_3x3() for b in arm.data.bones}
        self.inv = {k: m.inverted() for k, m in self.rest.items()}
        self.world_fix = {}   # bone -> 3x3 inverse of the parent's stance rotation (for world-space moves)

    def has(self, bone):
        return bone in self.arm.pose.bones

    def clear(self):
        for pb in self.arm.pose.bones:
            pb.rotation_quaternion = (1, 0, 0, 0)
            pb.location = (0, 0, 0)
            pb.scale = (1, 1, 1)

    def r(self, bone, deg, axis=LAT):
        if bone not in self.arm.pose.bones or not deg:
            return
        local = self.inv[bone] @ Vector(axis)
        q = Quaternion(local.normalized(), math.radians(deg))
        pb = self.arm.pose.bones[bone]
        pb.rotation_quaternion = q @ pb.rotation_quaternion

    def move(self, bone, offset):
        if bone not in self.arm.pose.bones:
            return
        pb = self.arm.pose.bones[bone]
        off = Vector(offset)
        if bone in self.world_fix:
            off = self.world_fix[bone] @ off
        pb.location = Vector(pb.location) + self.inv[bone] @ off

    def scale(self, bone, s):
        if bone not in self.arm.pose.bones:
            return
        self.arm.pose.bones[bone].scale = s if not isinstance(s, (int, float)) else (s, s, s)

    def apply(self, pose):
        """pose: {bone: [(deg, axis), ...] | deg} plus optional '@move': {bone: vec}."""
        for bone, val in pose.items():
            if bone == '@move':
                for b, off in val.items():
                    self.move(b, off)
                continue
            if bone == '@scale':
                for b, s in val.items():
                    self.scale(b, s)
                continue
            if isinstance(val, (int, float)):
                self.r(bone, val)
            else:
                for deg, axis in val:
                    self.r(bone, deg, axis)

    def key(self, frame):
        for pb in self.arm.pose.bones:
            pb.keyframe_insert('rotation_quaternion', frame=frame)
            pb.keyframe_insert('location', frame=frame)
            pb.keyframe_insert('scale', frame=frame)


def blend_poses(a, b, t):
    """Linear blend of two pose dictionaries (degrees/offsets)."""
    out = {}
    keys = set(a) | set(b)
    for k in keys:
        if k in ('@move', '@scale'):
            da, db = a.get(k, {}), b.get(k, {})
            sub = {}
            for bk in set(da) | set(db):
                default = (0, 0, 0) if k == '@move' else (1, 1, 1)
                va = Vector(_vec3(da.get(bk, default)))
                vb = Vector(_vec3(db.get(bk, default)))
                sub[bk] = tuple(va.lerp(vb, t))
            out[k] = sub
            continue
        va, vb = _norm(a.get(k, 0)), _norm(b.get(k, 0))
        axes = {}
        for deg, ax in va:
            axes.setdefault(tuple(ax), [0, 0])[0] += deg
        for deg, ax in vb:
            axes.setdefault(tuple(ax), [0, 0])[1] += deg
        out[k] = [(x + (y - x) * t, Vector(ax)) for ax, (x, y) in axes.items()]
    return out


def add_poses(*poses):
    out = {}
    for p in poses:
        for k, v in p.items():
            if k in ('@move', '@scale'):
                d = out.setdefault(k, {})
                for bk, bv in v.items():
                    if k == '@move':
                        d[bk] = tuple(Vector(_vec3(d.get(bk, (0, 0, 0)))) + Vector(_vec3(bv)))
                    else:
                        cur = _vec3(d.get(bk, (1, 1, 1)))
                        d[bk] = tuple(c * s for c, s in zip(cur, _vec3(bv)))
                continue
            out.setdefault(k, [])
            out[k] = list(_norm(out[k])) + list(_norm(v))
    return out


def _vec3(v):
    return (v, v, v) if isinstance(v, (int, float)) else tuple(v)


def _norm(v):
    if isinstance(v, (int, float)):
        return [(v, LAT)] if v else []
    return [(d, Vector(a)) for d, a in v]


def smooth(t):
    return t * t * (3 - 2 * t)
