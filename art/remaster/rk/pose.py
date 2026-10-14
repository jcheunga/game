"""Pose authoring: put hands exactly where a weapon (or a gesture) needs them.

`stance(ch, base, R=..., L=...)` applies a base pose (legs, torso, head), then solves each listed arm
with analytic two-bone IK so its wrist lands on a target, its elbow bends toward a pole, and its hand
takes a given orientation. The solved upper arm, forearm and hand rotations are returned in the stance
as '@quat' entries, so clips and death performances layer on top exactly as with authored angles.

Targets are world points in the character's own frame (before the turn toward the camera).
"""
import math

import bpy
from mathutils import Matrix, Quaternion, Vector

from . import hands as HANDS
from .rig import add_poses


class Grip:
    """A hand closed on a weapon: grip centre, weapon axis (toward the business end), elbow pole."""

    def __init__(self, point, axis, pole=None, pose='grip', toward_tip=True, size=1.0):
        self.point, self.axis = Vector(point), Vector(axis).normalized()
        self.pole, self.pose, self.toward_tip, self.size = pole, pose, toward_tip, size


class Reach:
    """A free hand: wrist target, elbow pole and an optional hand frame (palm direction, finger direction)."""

    def __init__(self, wrist, pole=None, fingers=None, palm=None, pose='relaxed'):
        self.wrist, self.pole = Vector(wrist), pole
        self.fingers = Vector(fingers).normalized() if fingers is not None else None
        self.palm = Vector(palm).normalized() if palm is not None else None
        self.pose = pose


def _hand_bone_frame(side, R3):
    """Hand bone rotation (armature space) from a hand-local frame (columns X', Y', Z' of rk.hands)."""
    Xp, Yp, Zp = R3.col[0].copy(), R3.col[1].copy(), R3.col[2].copy()
    sg = 1 if side == 'R' else -1
    X = Zp * sg
    Y = Yp
    Z = X.cross(Y).normalized()
    return Matrix((X, Y, Z)).transposed()


def _set_bone(pb, R3, head):
    M = R3.to_4x4()
    M.translation = head
    pb.matrix = M
    bpy.context.view_layer.update()


class Leg:
    """A planted foot: ankle target (world), knee pole and the toe direction (on the ground plane)."""

    def __init__(self, ankle, pole=None, toe=(1, 0, 0), lift=0.0):
        self.ankle, self.pole = Vector(ankle), pole
        self.toe = Vector(toe).normalized()
        self.lift = lift


def solve_leg(ch, side, ankle, pole=None, toe=Vector((1, 0, 0))):
    """Two-bone IK for a leg; the foot bone lies along `toe` (flat on the ground)."""
    arm = ch.arm
    bpy.context.view_layer.update()
    inv = arm.matrix_world.inverted()
    pbt = arm.pose.bones['thigh.' + side]
    pbs = arm.pose.bones['shin.' + side]
    pbf = arm.pose.bones['foot.' + side]
    S = pbt.head.copy()
    a, b = pbt.bone.length, pbs.bone.length
    T = inv @ Vector(ankle)
    d = T - S
    L = max(abs(a - b) * 1.01, min(d.length, (a + b) * 0.999))
    dn = d.normalized()
    T = S + dn * L
    if pole is None:
        pole = (S + T) * 0.5 + Vector((0.5, 0, 0))
    pd = (inv @ Vector(pole)) - S
    pd = pd - dn * pd.dot(dn)
    pd.normalize()
    cosA = max(-1.0, min(1.0, (a * a + L * L - b * b) / (2 * a * L)))
    A = math.acos(cosA)
    K = S + dn * (a * math.cos(A)) + pd * (a * math.sin(A))
    u = (K - S).normalized()
    f = (T - K).normalized()
    # knees flex the other way to elbows: keep the bone's X on the -Y side like the rest pose
    X = f.cross(u)
    if X.length < 1e-6:
        X = pd.cross(u)
    X.normalize()
    _set_bone(pbt, Matrix((X, u, X.cross(u).normalized())).transposed(), S)
    K2 = pbs.head.copy()
    f = (T - K2).normalized()
    Xs = X - f * X.dot(f)
    Xs.normalize()
    _set_bone(pbs, Matrix((Xs, f, Xs.cross(f).normalized())).transposed(), K2)
    # foot: along the toe direction, slightly pitched down to the ground
    toe = Vector(toe)
    toe.z = 0
    toe.normalize()
    fy = (toe + Vector((0, 0, -0.18))).normalized()
    fx = fy.cross(Vector((0, 0, 1))).normalized()
    if fx.dot(Xs) < 0:
        fx = -fx
    _set_bone(pbf, Matrix((fx, fy, fx.cross(fy).normalized())).transposed(), pbf.head.copy())
    return {pb.name: pb.rotation_quaternion.copy() for pb in (pbt, pbs, pbf)}


def solve_arm(ch, side, wrist, pole=None, hand_R3=None):
    """Rotate the arm so the wrist reaches `wrist`; returns {bone: Quaternion} (also left applied)."""
    arm = ch.arm
    bpy.context.view_layer.update()
    inv = arm.matrix_world.inverted()
    pbu = arm.pose.bones['upper_arm.' + side]
    pbf = arm.pose.bones['forearm.' + side]
    pbh = arm.pose.bones['hand.' + side]
    S = pbu.head.copy()
    a, b = pbu.bone.length, pbf.bone.length
    T = inv @ Vector(wrist)
    d = T - S
    L = max(abs(a - b) * 1.01, min(d.length, (a + b) * 0.995))
    dn = d.normalized()
    T = S + dn * L
    if pole is None:
        sy = -1 if side == 'R' else 1
        pole = S + Vector((-0.35, sy * 0.35, -0.35))
    pd = (inv @ Vector(pole)) - S
    pd = pd - dn * pd.dot(dn)
    if pd.length < 1e-6:
        pd = Vector((-1, 0, 0)) - dn * Vector((-1, 0, 0)).dot(dn)
    pd.normalize()
    cosA = max(-1.0, min(1.0, (a * a + L * L - b * b) / (2 * a * L)))
    A = math.acos(cosA)
    E = S + dn * (a * math.cos(A)) + pd * (a * math.sin(A))
    u = (E - S).normalized()
    f = (T - E).normalized()
    X = u.cross(f)
    if X.length < 1e-6:
        X = u.cross(pd)
    X.normalize()
    _set_bone(pbu, Matrix((X, u, X.cross(u).normalized())).transposed(), S)
    E2 = pbf.head.copy()
    f = (T - E2).normalized()
    Xf = X - f * X.dot(f)
    Xf.normalize()
    _set_bone(pbf, Matrix((Xf, f, Xf.cross(f).normalized())).transposed(), E2)
    if hand_R3 is not None:
        _set_bone(pbh, hand_R3, pbh.head.copy())
    return {pb.name: pb.rotation_quaternion.copy() for pb in (pbu, pbf, pbh)}


def _strip_arm(pose, side):
    out = {}
    for k, v in pose.items():
        if k.startswith('@'):
            if k == '@quat':
                v = {b: q for b, q in v.items() if not b.endswith('.' + side) or b.split('.')[0] not in
                     ('upper_arm', 'forearm', 'hand')}
            out[k] = v
            continue
        if k.split('.')[0] in ('upper_arm', 'forearm', 'hand') and k.endswith('.' + side):
            continue
        out[k] = v
    return out


def grip_frame(ch, side, g, wrist_hint=None):
    """World 4x4 of a grip hand (rk.hands local frame) for Grip g."""
    f, gl = HANDS.hand_field(1.12 * ch.s.get('hand', 1.0) * g.size, g.pose, thumb=-1 if side == 'R' else 1)
    if wrist_hint is None:
        pb = ch.arm.pose.bones['hand.' + side]
        wrist_hint = ch.arm.matrix_world @ pb.head
    M, _ = HANDS.grip_frame(side, g.axis, g.point, wrist_hint, gl, g.toward_tip)
    return M


def _strip_leg(pose, side):
    return {k: v for k, v in pose.items()
            if not (k.split('.')[0] in ('thigh', 'shin', 'foot') and k.endswith('.' + side))}


def stance(ch, base, R=None, L=None, feet=None):
    """Base pose plus solved arms (R/L: Grip or Reach) and planted feet (feet: {'R': Leg, 'L': Leg}).
    Returns the stance dict (with '@quat')."""
    pose = dict(base)
    for side, spec in (('R', R), ('L', L)):
        if spec is not None:
            pose = _strip_arm(pose, side)
    for side in (feet or {}):
        pose = _strip_leg(pose, side)
    P = ch.poser
    P.clear()
    P.apply(pose)
    bpy.context.view_layer.update()
    quats = dict(pose.get('@quat', {}))
    for side, leg in (feet or {}).items():
        quats.update(solve_leg(ch, side, leg.ankle, leg.pole, leg.toe))
    ch.hand_frames = getattr(ch, 'hand_frames', {})
    for side, spec in (('R', R), ('L', L)):
        if spec is None:
            continue
        if isinstance(spec, Grip):
            # the wrist sits where a hand closed on the grip puts it; first guess from a neutral reach
            sh = ch.arm.matrix_world @ ch.arm.pose.bones['upper_arm.' + side].head
            hint = sh if spec.pole is None else sh.lerp(Vector(spec.pole), 0.5)
            M = grip_frame(ch, side, spec, wrist_hint=hint)
            q = solve_arm(ch, side, M.translation, spec.pole, _hand_bone_frame(side, M.to_3x3()))
            ch.hand_frames[side] = M
        else:
            hand_R3 = None
            if spec.fingers is not None:
                Yp = spec.fingers
                palm = spec.palm if spec.palm is not None else Vector((0, 1 if side == 'R' else -1, 0))
                Zp = -(palm - Yp * palm.dot(Yp)).normalized()   # back of the hand away from the palm
                Xp = Yp.cross(Zp).normalized()
                R3 = Matrix((Xp, Yp, Zp)).transposed()
                hand_R3 = _hand_bone_frame(side, R3)
                M = R3.to_4x4()
                M.translation = spec.wrist
                ch.hand_frames[side] = M
            q = solve_arm(ch, side, spec.wrist, spec.pole, hand_R3)
        quats.update(q)
    pose['@quat'] = quats
    P.clear()
    P.apply(pose)
    bpy.context.view_layer.update()
    return pose


def weapon_frame(point, axis, flat=(0, -1, 0)):
    """World 4x4 for a weapon (grip at origin, +Z toward the business end, flat facing `flat`)."""
    Z = Vector(axis).normalized()
    Y = Vector(flat) - Z * Vector(flat).dot(Z)
    if Y.length < 1e-6:
        Y = Vector((0, 0, 1)) - Z * Z.z
    Y.normalize()
    X = Y.cross(Z).normalized()
    M = Matrix((X, Y, Z)).transposed().to_4x4()
    M.translation = Vector(point)
    return M


def frame(origin, x, z):
    """World 4x4 with columns X = x, Z = z (orthogonalised), Y = Z x X."""
    X = Vector(x).normalized()
    Z = Vector(z)
    Z = (Z - X * Z.dot(X)).normalized()
    Y = Z.cross(X).normalized()
    M = Matrix((X, Y, Z)).transposed().to_4x4()
    M.translation = Vector(origin)
    return M


def planted(ch, drop, spread=0.34, stride=0.32, toe_out=18.0, front='L', knees_out=0.35, lean=0.0, ankle_z=None):
    """Feet planted wide on the ground for a fighting stance: `stride` front/back, `spread` side to side,
    knees pushed outward. Returns {'R': Leg, 'L': Leg}."""
    J = ch.J
    az = J['ankle.R'].z if ankle_z is None else ankle_z
    out = {}
    for side, sy in (('R', -1), ('L', 1)):
        fwd = stride * (0.5 if side == front else -0.5)
        ank = Vector((J['ankle.' + side].x + fwd, sy * spread, az))
        a = math.radians(sy * toe_out + (8 if side == front else -6) * sy)
        toe = Vector((math.cos(a), math.sin(a), 0))
        hip = J['hip.' + side] + Vector((0, 0, -drop))
        knee_dir = Vector((0.6, sy * knees_out, 0)).normalized()
        pole = (hip + ank) * 0.5 + knee_dir * 0.6
        out[side] = Leg(ank, pole, toe)
    return out


def feet_from_pose(ch):
    """Planted-foot targets where the current pose puts the feet (ankles, knee poles, toe directions)."""
    bpy.context.view_layer.update()
    mw = ch.arm.matrix_world
    out = {}
    for side in ('R', 'L'):
        shin = ch.arm.pose.bones['shin.' + side]
        foot = ch.arm.pose.bones['foot.' + side]
        ank = mw @ foot.head
        knee = mw @ shin.head
        toe = (mw @ foot.tail) - ank
        toe.z = 0
        hip = mw @ ch.arm.pose.bones['thigh.' + side].head
        mid = (hip + ank) * 0.5
        kdir = knee - mid
        kdir = kdir.normalized() if kdir.length > 1e-4 else Vector((1, 0, 0))
        out[side] = Leg(ank, knee + kdir * 0.5, toe.normalized() if toe.length > 1e-4 else Vector((1, 0, 0)))
    return out
