"""Death performances: per-unit choreography solved against the ground.

Each unit dies its own way (see roster/deaths.py). A performance is a short track of
key poses with timing, layered on the unit's stance. The solver in this module keys
it frame by frame:

* ground contact: the root moves so the deformed body rests on Z = 0, with no floating
  corpses and no limbs through the floor;
* planted contacts: a foot or knee can stay pinned while the body pivots over it, so
  nobody skates;
* props: weapons, shields and helmets are released and fall under gravity, bounce,
  and settle flat. A planted prop, such as a banner, stays standing;
* scatter: rigid skeletons and machine parts break apart and tumble;
* lights out: emissive materials (eyes, soul cores, lanterns, flames) dim to nothing.

It also reports the frame where the body hits the ground (`impactFrame`) so the game can
time dust and the thud.
"""
import math

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

from .rig import LAT, YAW, ROLL, add_poses, blend_poses

# Stylised gravity: real-time drops read as floaty at sprite frame rates.
GRAVITY = 9.81 * 2.2
FRAMES = 12
DURATION = 0.075
# Thin dangling kit may brush this far into the ground instead of propping the body up.
SOFT = ('Scabbard', 'Chape', 'Quiver', 'Sheath', 'Pouch', 'Arrow', 'Plume', 'Strap', 'Rein', 'Tassel', 'Feather')
SOFT_DEPTH = 0.1
CAPE_BONES = ('cape.0', 'cape.1', 'cape.2')


# ------------------------------------------------------------------ timing
def ease(kind, t):
    t = min(1.0, max(0.0, t))
    if kind == 'in':        # gravity: slow start, fast finish
        return t * t
    if kind == 'in3':
        return t * t * t
    if kind == 'out':       # struck: fast start, slow finish
        return 1 - (1 - t) ** 2
    if kind == 'out3':
        return 1 - (1 - t) ** 3
    if kind == 'lin':
        return t
    if kind == 'hold':
        return 0.0 if t < 1 else 1.0
    return t * t * (3 - 2 * t)  # 'io'


class Key:
    """A pose reached at normalised time `t`.

    pose:  delta pose layered on the (weighted) stance
    stance: how much of the fighting stance remains (1 = alert, 0 = limp)
    ease:  curve used to arrive at this key from the previous one
    """
    def __init__(self, t, pose=None, stance=1.0, ease='io'):
        self.t, self.pose, self.stance, self.ease = t, pose or {}, stance, ease


def build_track(stance, keys, n):
    """Interpolate keys to n frames. Returns a list of full poses."""
    keys = sorted(keys, key=lambda k: k.t)
    out = []
    for i in range(n):
        u = i / (n - 1)
        a = keys[0]
        b = keys[-1]
        for ka, kb in zip(keys, keys[1:]):
            if ka.t <= u <= kb.t:
                a, b = ka, kb
                break
        s = 0.0 if b.t == a.t else (u - a.t) / (b.t - a.t)
        e = ease(b.ease, s) if u >= a.t else 0.0
        if u > keys[-1].t:
            a = b = keys[-1]
            e = 1.0
        delta = blend_poses(a.pose, b.pose, e)
        w = a.stance + (b.stance - a.stance) * e
        out.append(add_poses(blend_poses(stance, {}, 1 - w), delta))
    return out


def frame_of(u, n):
    return max(0, min(n - 1, round(u * (n - 1))))


# ------------------------------------------------------------------ geometry
class Geometry:
    """World-space vertex access for every mesh driven by an armature."""

    def __init__(self, arms):
        self.arms = list(arms)
        self.rigid = []      # (obj, local verts)
        self.deformed = []
        names = {a.name for a in self.arms}
        dg = bpy.context.evaluated_depsgraph_get()
        for o in bpy.data.objects:
            if o.type != 'MESH' or o.name.startswith('_'):
                continue
            mods = [m for m in o.modifiers if m.type == 'ARMATURE' and m.object is not None]
            arm_mod = any(m.object.name in names for m in mods)
            if mods and not arm_mod:
                continue  # skinned to another rig
            owner = o
            # Walk up through plain objects only: a rider's meshes belong to the rider's rig,
            # even though that rig rides on this one.
            while (owner.parent is not None and owner.parent.name not in names
                   and owner.parent.type != 'ARMATURE'):
                owner = owner.parent
            parented = owner.parent is not None and owner.parent.name in names
            if arm_mod:
                self.deformed.append(o)
            elif parented:
                ev = o.evaluated_get(dg)
                me = ev.to_mesh()
                co = np.empty(len(me.vertices) * 3, dtype=np.float32)
                me.vertices.foreach_get('co', co)
                ev.to_mesh_clear()
                co = co.reshape(-1, 3)
                if len(co) > 400:
                    co = co[:: max(1, len(co) // 400)]
                self.rigid.append((o, co))

    @staticmethod
    def _world(co, mw):
        m = np.array(mw, dtype=np.float32)
        return co @ m[:3, :3].T + m[:3, 3]

    def points(self, objs=None, exclude=()):
        dg = bpy.context.evaluated_depsgraph_get()
        out = []
        for o, co in self.rigid:
            if o in exclude or o.hide_render or (objs is not None and o not in objs):
                continue
            out.append(self._world(co, o.matrix_world))
        for o in self.deformed:
            if o in exclude or o.hide_render or (objs is not None and o not in objs):
                continue
            ev = o.evaluated_get(dg)
            me = ev.data
            co = np.empty(len(me.vertices) * 3, dtype=np.float32)
            me.vertices.foreach_get('co', co)
            co = co.reshape(-1, 3)
            if len(co) > 4000:
                co = co[:: max(1, len(co) // 4000)]
            out.append(self._world(co, ev.matrix_world))
        return np.concatenate(out) if out else np.zeros((0, 3), dtype=np.float32)

    def min_z(self, exclude=()):
        pts = self.points(exclude=exclude)
        return float(pts[:, 2].min()) if len(pts) else 0.0

    def all_objects(self):
        return [o for o, _ in self.rigid] + list(self.deformed)

    def ground_height(self, exclude=(), capes=()):
        """Lowest load-bearing point. Capes and dangling kit are excluded (they drape and swing
        clear afterwards); other soft kit counts only once it sinks past SOFT_DEPTH."""
        exclude = set(exclude) | set(capes)
        soft = {o for o in self.all_objects() if o.name.startswith(SOFT) and o not in exclude}
        hard = self.min_z(exclude=exclude | soft)
        if not soft:
            return hard
        low_soft = self.min_z(exclude=set(self.all_objects()) - soft)
        return min(hard, low_soft + SOFT_DEPTH)


def update():
    bpy.context.view_layer.update()


# ------------------------------------------------------------------ rigid bodies
def _principal_thin_axis(rel):
    if len(rel) < 3:
        return Vector((0, 0, 1))
    cov = np.cov((rel - rel.mean(axis=0)).T)
    w, v = np.linalg.eigh(cov)
    a = v[:, 0]
    return Vector((float(a[0]), float(a[1]), float(a[2]))).normalized()


class Body:
    """A rigid piece (a prop or a skeleton segment) tumbling under gravity.

    State is the world transform relative to its pose at release: rotation `q` about the
    centroid `c`. Ground contact uses the piece's own vertices.
    """

    def __init__(self, pts, velocity, spin, restitution=0.28, friction=0.55, settle=0.6, roll=0.0):
        self.c0 = Vector(pts.mean(axis=0).tolist())
        self.rel = pts - np.array(self.c0, dtype=np.float32)
        self.c = self.c0.copy()
        self.q = Quaternion()
        self.v = Vector(velocity)
        self.w = Vector(spin)                       # rad/s, world axis * rate
        self.e, self.f, self.settle_rate, self.roll = restitution, friction, settle, roll
        self.thin = _principal_thin_axis(self.rel)
        self.grounded = 0

    def _lowest(self, q=None, c=None):
        q = q or self.q
        c = c or self.c
        m = np.array(q.to_matrix(), dtype=np.float32)
        return float((self.rel @ m.T)[:, 2].min() + c.z)

    def step(self, dt, substeps=6, remaining=None):
        h = dt / substeps
        if remaining is not None and not self.grounded and remaining <= 3:
            # Everything is at rest by the last frame: steer still-airborne pieces down in time.
            height = self._lowest()
            if height > 0:
                self.v.z = min(self.v.z, -height / max(dt * 0.6, remaining * dt * 0.8))
        for _ in range(substeps):
            self.v.z -= GRAVITY * h
            self.c += self.v * h
            if self.w.length > 1e-6:
                self.q = Quaternion(self.w.normalized(), self.w.length * h) @ self.q
            low = self._lowest()
            if low < 0:
                self.c.z -= low
                if self.v.z < 0:
                    self.v.z = -self.v.z * self.e
                    if self.v.z < 0.35:
                        self.v.z = 0.0
                self.v.x *= self.f
                self.v.y *= self.f
                if not self.grounded:
                    # The spin it lands with carries into the topple: a dropped spear doesn't stand on its point.
                    self.topple = max(getattr(self, 'topple', 0.0), self.w.length * 0.8 + 2.5)
                self.w *= 0.45 if not self.roll else 0.9
                self.grounded += 1
            if self.grounded and not self.roll:
                # Topple onto the widest face (thinnest principal axis to +/-Z), pivoting about the
                # contact point and speeding up as it goes, like a pole or a shield falling over.
                axis = self.q @ self.thin
                target = Vector((0, 0, 1 if axis.z >= 0 else -1))
                fix = axis.rotation_difference(target)
                angle = fix.angle
                if angle > math.radians(1.5):
                    self.topple = getattr(self, 'topple', 0.0) + 26.0 * self.settle_rate * h
                    step = min(1.0, self.topple * h / angle)
                    m = np.array(self.q.to_matrix(), dtype=np.float32)
                    pts = self.rel @ m.T
                    pivot = Vector(pts[int(np.argmin(pts[:, 2]))].tolist()) + self.c
                    turn = Quaternion().slerp(fix, step)
                    self.c = pivot + turn @ (self.c - pivot)
                    self.q = turn @ self.q
                    low = self._lowest()
                    self.c.z -= low
            if self.grounded and self.roll:
                self.v.x = self.roll * self.w.length * (1 if self.w.dot(Vector(LAT)) >= 0 else -1) * 0.12
                self.c.x += self.v.x * h
                low = self._lowest()
                self.c.z -= low

    def delta(self):
        """World matrix moving the piece from its release pose to its current pose."""
        return Matrix.Translation(self.c) @ self.q.to_matrix().to_4x4() @ Matrix.Translation(-self.c0)


# ------------------------------------------------------------------ performance
class Performance:
    """Keyed by Character.key_clips in place of a pose list."""

    def __init__(self, stance, keys, frames=FRAMES, duration=DURATION, pins=(), air=(), drops=None,
                 scatter=None, dim=None, fx='dust', plant=None, release_ik=0.2, scale_keys=False,
                 hide=None, ground=True, impact_bone=None, motion='', plant_lean=7.0, world_lock=False):
        self.count = frames
        self.duration = duration
        self.poses = build_track(stance, keys, frames)
        self.pins = list(pins)              # (u0, u1, bone, along)
        # air: times (0-1) or one (start, end) range during which the body may be off the ground
        if isinstance(air, tuple) and len(air) == 2:
            self.air = set(range(frame_of(air[0], frames), frame_of(air[1], frames) + 1))
        else:
            self.air = set(frame_of(u, frames) for u in air)
        self.drops = drops or {}            # prop name -> dict(release, toss, spin, ...)
        self.scatter = scatter or {}        # bone name -> dict(release, toss, spin, ...)
        self.dim = dim                      # None (all emissive) | [] (none) | list of material-name prefixes
        self.fx = fx
        self.plant = plant
        self.plant_lean = plant_lean
        self.world_lock = world_lock   # riders: detach from the mount's saddle for the fall
        self.release_ik = release_ik
        self.scale_keys = scale_keys
        self.hide = hide or {}              # object-name prefix -> u at which it vanishes
        self.ground = ground
        self.impact_bone = impact_bone
        self.motion = motion
        self.impact = None

    # -- helpers -------------------------------------------------------
    def _props(self, ch):
        """Registered kit plus '@Prefix' groups of named parts (crowns, helmets, pustules).
        A spec with each=True gives every matching part its own tumbling body."""
        props = {p['name']: p for p in getattr(ch, 'props', [])}
        names = {ch.arm.name}
        for name, spec in list(self.drops.items()):
            if not name.startswith('@'):
                continue
            objs = [o for o in bpy.data.objects if o.type == 'MESH' and o.name.startswith(name[1:])
                    and o.parent is not None and o.parent.name in names]
            if spec.get('each'):
                del self.drops[name]
                for i, o in enumerate(sorted(objs, key=lambda o: o.name)):
                    key = f'{name}#{i}'
                    jitter = _jitter(i)
                    toss = Vector(spec.get('toss', (0, 0, 1.0)))
                    self.drops[key] = dict(spec, toss=tuple(toss + Vector((jitter[0], jitter[1], abs(jitter[2]))) * spec.get('spread', 0.8)),
                                           spin=[(jitter[0] * 900, LAT), (jitter[1] * 600, ROLL)])
                    props[key] = {'name': key, 'objs': [o], 'bone': o.parent_bone}
            elif objs:
                props[name] = {'name': name, 'objs': objs, 'bone': objs[0].parent_bone}
        return props

    def key(self, ch, frame0, scene=None):
        """Key the performance starting at timeline frame `frame0`. Returns clip metadata."""
        scene = scene or bpy.context.scene
        arm, P = ch.arm, ch.poser
        n = self.count
        geo = Geometry([arm])
        props = self._props(ch)
        released, pieces = {}, {}
        prev_centroids = {}
        ik = [c for pb in arm.pose.bones for c in pb.constraints if c.type == 'IK']
        dims = _dim_targets(ch, self.dim)
        hides = [(o, frame_of(u, n)) for pref, u in self.hide.items() for o in bpy.data.objects
                 if o.type == 'MESH' and o.name.startswith(pref)]
        pin_ref = {}
        impact_track, torso = [], []
        capes = [o for o in geo.deformed if any(vg.name.startswith('cape.') for vg in o.vertex_groups)]
        cape_chain = [b for b in CAPE_BONES if b in arm.pose.bones]
        cape_lag = None
        drop_objs = set(o for name in self.drops for o in props.get(name, {}).get('objs', []))
        if self.plant and self.plant in props:
            drop_objs |= set(props[self.plant]['objs'])
        dangles = {}
        for o, _ in geo.rigid:
            if (o.name.startswith(SOFT) and o.parent == arm and o.parent_type == 'BONE'
                    and o not in drop_objs):
                dangles.setdefault(o.parent_bone, []).append(o)
        for group in dangles.values():
            for o in group:
                o.rotation_mode = 'QUATERNION'
                _key_identity(o, frame0 - 1 if frame0 > 1 else 1)
        for o in drop_objs:
            o.rotation_mode = 'QUATERNION'
        scatter_bones = [b for b in self.scatter if b in arm.pose.bones]
        depth = {b: _depth(arm.pose.bones[b]) for b in scatter_bones}
        scatter_bones.sort(key=lambda b: depth[b])
        bone_objs = _bone_objects(arm)
        ib = self.impact_bone or next((b for b in ('chest', 'body', 'spine', 'hips') if b in arm.pose.bones), 'root')

        # Identity before the clip, so other clips are unaffected.
        for o in drop_objs:
            _key_identity(o, frame0 - 1 if frame0 > 1 else 1)
        for f_local in range(n):
            frame = frame0 + f_local
            scene.frame_set(frame)
            if f_local == 0:
                lock0 = arm.matrix_world.copy()
            P.clear()
            P.apply(self.poses[f_local])
            update()
            if self.world_lock:
                # Keep the root where the mount was at the start of the clip, not where it goes.
                pb = arm.pose.bones['root']
                pb.matrix = arm.matrix_world.inverted() @ lock0 @ pb.matrix
                update()
            # planted contacts
            for i, (u0, u1, bone, along) in enumerate(self.pins):
                a, b = frame_of(u0, n), frame_of(u1, n)
                if not (a <= f_local <= b) or bone not in arm.pose.bones:
                    continue
                p = _bone_point(arm, bone, along)
                if f_local == a:
                    pin_ref[i] = (p, None)
                else:
                    ref = pin_ref.get(i, (p, None))[0]
                    _move_root_world(ch, Vector((ref.x - p.x, ref.y - p.y, 0)))
                    update()
                    pin_ref[i] = (ref, Vector((ref.x - p.x, ref.y - p.y, 0)))
            # carry the last pin correction after its window closes
            for i, (u0, u1, bone, along) in enumerate(self.pins):
                if f_local > frame_of(u1, n) and i in pin_ref and pin_ref[i][1] is not None:
                    _move_root_world(ch, pin_ref[i][1] * 1.0)
            update()
            # ground
            if self.ground:
                loose = set(o for _, _, objs in pieces.values() for o in objs)
                dangling = set(o for g in dangles.values() for o in g)
                low = geo.ground_height(exclude=drop_objs | loose | dangling, capes=capes)
                if f_local not in self.air or low < 0:
                    _move_root_world(ch, Vector((0, 0, -low)))
                    update()
            # capes hang under gravity in the body's frame, trail behind fast turns and rest on the
            # ground (solved after the body is grounded, which ignores them)
            if cape_chain:
                cape_lag = _drape(ch, cape_chain, cape_lag)
                if self.ground and capes:
                    # a hem that still digs in bends along the ground, lowest segment first
                    _lift_hem(ch, cape_chain, geo, capes)
            # scabbards and quivers swing about their strap instead of digging in
            for group in dangles.values():
                for o in group:
                    _key_identity(o, frame)
                update()
                _dangle(geo, group)
                for o in group:
                    o.keyframe_insert('location', frame=frame)
                    o.keyframe_insert('rotation_quaternion', frame=frame)
                    o.keyframe_insert('scale', frame=frame)
            impact_track.append(_bone_point(arm, ib, 0.0).z)
            torso.append((_bone_point(arm, ib, 0.0) + _bone_point(arm, 'hips' if 'hips' in arm.pose.bones else ib, 0.0)) / 2)
            # scatter: rigid skeleton segments and machine parts
            dt = self.duration
            for bone in scatter_bones:
                spec = self.scatter[bone]
                rf = frame_of(spec.get('release', 0.3), n)
                pb = arm.pose.bones[bone]
                if f_local < rf:
                    continue
                if bone not in pieces:
                    objs = _subtree_objects(arm, bone, bone_objs, scatter_bones)
                    pts = geo.points(objs=set(objs))
                    if not len(pts):
                        pts = np.array([list(arm.matrix_world @ pb.head)], dtype=np.float32)
                    mw0 = arm.matrix_world @ pb.matrix
                    vel = Vector(spec.get('toss', (0, 0, 0)))
                    spin = _spin(spec)
                    pieces[bone] = (Body(pts, vel, spin, restitution=spec.get('bounce', 0.3),
                                         roll=spec.get('roll', 0.0)), mw0, objs)
                else:
                    pieces[bone][0].step(dt, remaining=n - 1 - f_local)
                body_, mw0, _ = pieces[bone]
                pb.matrix = arm.matrix_world.inverted() @ body_.delta() @ mw0
                update()
            # props
            for name, spec in list(self.drops.items()) + ([(self.plant, {'plant': True, 'release': 0.0, 'lean': self.plant_lean})]
                                                          if self.plant and self.plant not in self.drops else []):
                prop = props.get(name)
                if not prop:
                    continue
                rf = frame_of(spec.get('release', 0.25), n)
                objs = prop['objs']
                pts = geo.points(objs=set(objs))
                if not len(pts):
                    continue
                if f_local < rf:
                    prev_centroids[name] = Vector(pts.mean(axis=0).tolist())
                    for o in objs:
                        _key_identity(o, frame)
                    continue
                if name not in released:
                    c = Vector(pts.mean(axis=0).tolist())
                    inherited = (c - prev_centroids.get(name, c)) / dt * spec.get('inherit', 0.6)
                    vel = inherited + Vector(spec.get('toss', (0, 0, 0.4)))
                    worlds = {o: o.matrix_world.copy() for o in objs}
                    if spec.get('plant'):
                        low = float(pts[:, 2].min())
                        base = Vector(pts[int(np.argmin(pts[:, 2]))].tolist())
                        released[name] = ('plant', worlds, (base, low, spec))
                    else:
                        released[name] = ('drop', worlds, Body(pts, vel, _spin(spec), restitution=spec.get('bounce', 0.25),
                                                               friction=spec.get('friction', 0.5),
                                                               settle=spec.get('settle', 0.6),
                                                               roll=spec.get('roll', 0.0)))
                else:
                    kind, worlds, state = released[name]
                    if kind == 'drop':
                        state.step(dt, remaining=n - 1 - f_local)
                kind, worlds, state = released[name]
                if kind == 'drop':
                    d = state.delta()
                else:
                    base, low, spec_ = state
                    k = (f_local - rf) / max(1, n - 1 - rf)
                    lean = math.radians(spec_.get('lean', 8.0)) * (k ** 1.5)
                    sink = -low * min(1.0, (f_local - rf + 1) / 2)
                    d = (Matrix.Translation(Vector((base.x, base.y, 0))) @ Matrix.Rotation(lean, 4, 'Y')
                         @ Matrix.Translation(-Vector((base.x, base.y, 0))) @ Matrix.Translation((0, 0, sink)))
                for o in objs:
                    o.matrix_world = d @ worlds[o]
                    o.keyframe_insert('location', frame=frame)
                    o.keyframe_insert('rotation_quaternion', frame=frame)
                    o.keyframe_insert('scale', frame=frame)
                update()
            # IK hands let go as the body goes limp
            for c in ik:
                c.influence = 1.0 if f_local < frame_of(self.release_ik, n) else max(
                    0.0, 1 - (f_local - frame_of(self.release_ik, n) + 1) / 3)
                c.keyframe_insert('influence', frame=frame)
            # dim lights
            for sock, base in dims:
                k = f_local / max(1, n - 1)
                sock.default_value = base * max(0.0, 1 - k * 1.25) ** 1.6
                sock.keyframe_insert('default_value', frame=frame)
            for o, hf in hides:
                o.hide_render = f_local >= hf
                o.keyframe_insert('hide_render', frame=frame)
            P.key(frame)
            if self.scale_keys:
                for pb in arm.pose.bones:
                    pb.keyframe_insert('scale', frame=frame)
        # Restore everything on the next frame.
        after = frame0 + n
        dangle_objs = [o for g in dangles.values() for o in g]
        for o in list(drop_objs) + dangle_objs:
            _key_identity(o, after)
        for c in ik:
            c.influence = 1.0
            c.keyframe_insert('influence', frame=after)
            c.keyframe_insert('influence', frame=1) if frame0 > 1 else None
        for sock, base in dims:
            sock.default_value = base
            sock.keyframe_insert('default_value', frame=after)
            sock.keyframe_insert('default_value', frame=1)
        for o, _ in hides:
            o.hide_render = False
            o.keyframe_insert('hide_render', frame=after)
            o.keyframe_insert('hide_render', frame=1)
        if self.scale_keys:
            P.clear()
            for pb in arm.pose.bones:
                pb.keyframe_insert('scale', frame=after)
                pb.keyframe_insert('scale', frame=1)
        _constant(arm, list(drop_objs) + dangle_objs, dims, hides)
        self.impact = _impact_frame(impact_track)
        land = torso[self.impact].copy()
        land.z = 0.0
        # World point under the torso at impact; render_character projects it to frame space.
        meta = {'impactFrame': self.impact, 'fx': self.fx, '_impactWorld': tuple(land)}
        if self.motion:
            meta['motion'] = self.motion
        return meta


# ------------------------------------------------------------------ internals
def _jitter(i):
    """Deterministic pseudo-random vector in [-1, 1]^3 per index (renders are reproducible)."""
    return (math.sin(i * 12.9898 + 1.3) * 0.999, math.sin(i * 78.233 + 4.1) * 0.999, math.sin(i * 37.719 + 2.7) * 0.999)


def _spin(spec):
    s = spec.get('spin', 0.0)
    if isinstance(s, (int, float)):
        return Vector(LAT) * math.radians(s)
    total = Vector()
    for deg, axis in s:
        total += Vector(axis) * math.radians(deg)
    return total


def _depth(pb):
    d = 0
    while pb.parent is not None:
        pb = pb.parent
        d += 1
    return d


def _dangle(geo, group):
    """Swing a hanging group about its highest point until it rests on the ground."""
    pts = geo.points(objs=set(group))
    if not len(pts) or pts[:, 2].min() > -0.005:
        return
    top = pts[pts[:, 2] >= np.percentile(pts[:, 2], 90)].mean(axis=0)
    pivot = Vector(top.tolist())
    low = Vector(pts[int(np.argmin(pts[:, 2]))].tolist())
    d = low - pivot
    axis = d.cross(Vector((0, 0, 1)))
    if axis.length < 1e-6:
        axis = Vector((0, 1, 0))
    axis.normalize()
    base = {o: o.matrix_world.copy() for o in group}

    def lowest(angle):
        R = Matrix.Translation(pivot) @ Matrix.Rotation(angle, 4, axis) @ Matrix.Translation(-pivot)
        m = np.array(R, dtype=np.float32)
        return float((pts @ m[:3, :3].T + m[:3, 3])[:, 2].min()), R
    sign = 1 if lowest(0.05)[0] > lowest(-0.05)[0] else -1
    best = None
    for k in range(1, 16):
        z, R = lowest(sign * math.radians(6 * k))
        best = R
        if z >= 0:
            break
    for o in group:
        o.matrix_world = best @ base[o]
    update()


def _body_frame(arm, bone):
    """Rotation taking rest-space (character) axes to their posed world directions."""
    pb = arm.pose.bones[bone]
    return arm.matrix_world.to_3x3() @ pb.matrix.to_3x3() @ pb.bone.matrix_local.to_3x3().inverted()


def _plane_weight(m):
    """0 when gravity barely projects onto a drape plane (angle unstable), 1 when it lies in it."""
    t = max(0.0, min(1.0, (m - 0.35) / 0.4))
    return t * t * (3 - 2 * t)


def _lateral(v):
    """Angle of a direction in the character's front plane: 0 = straight down, + = toward its left."""
    return math.degrees(math.atan2(v.y, -v.z))


def _sagittal(v):
    """Angle of a direction in the character's side plane: 0 = straight down, + = forward."""
    return math.degrees(math.atan2(v.x, -v.z))


def _seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(1e-9, ab.length_squared)))
    return (a + ab * t - p).length


def _drape(ch, chain, lag):
    """Capes hang toward gravity and trail behind fast turns, draping over the back and legs.

    Each segment swings (about the body's side axis) toward gravity until its points would pass
    into the torso or legs. Positions are rotated analytically, so one depsgraph update per frame.
    """
    arm, P = ch.arm, ch.poser
    mw = arm.matrix_world
    pbs = arm.pose.bones
    parent = pbs[chain[0]].parent.name if pbs[chain[0]].parent else 'root'
    R = _body_frame(arm, parent).normalized()
    Ri = R.inverted()
    axis = (R @ Vector(LAT)).normalized()
    gb = Ri @ Vector((0, 0, -1))
    g = _sagittal(gb)
    # Each swing only means something when gravity lies in that plane: no sideways drape on a
    # body lying flat on its back or front, no fore-aft drape on one lying on its side.
    w_sag = _plane_weight(math.hypot(gb.x, gb.z))
    w_lat = _plane_weight(math.hypot(gb.y, gb.z))
    trail = 0.0 if lag is None else max(-14.0, min(14.0, (lag - g) * 0.45)) * w_sag

    def wpt(bone, end):
        b = pbs[bone]
        return mw @ (b.tail if end else b.head)
    capsules = []
    if 'hips' in pbs and 'neck' in pbs:
        capsules.append((wpt('hips', 0), wpt('neck', 0), 0.17))
    for side in ('R', 'L'):
        for bone, r in (('thigh.' + side, 0.09), ('shin.' + side, 0.07)):
            if bone in pbs:
                capsules.append((wpt(bone, 0), wpt(bone, 1), r))
    pts = [wpt(chain[0], 0)] + [wpt(b, 1) for b in chain]   # chain is connected: head, tails

    def clearance(points):
        body = min((_seg_dist(q, a, b) - r for q in points for a, b, r in capsules), default=1.0)
        return min(body, min(q.z - 0.07 for q in points))   # the cloth's bones stay off the floor
    side_axis = (R @ Vector(ROLL)).normalized()
    gl = _lateral(Ri @ Vector((0, 0, -1)))

    def swing(i, ax, delta, guard):
        head, rest = pts[i], pts[i + 1:]
        floor = min(0.0, clearance(rest)) - 0.01
        chosen, step, k = 0.0, (5.0 if delta > 0 else -5.0), 0.0
        while abs(k) < abs(delta) - 1e-6:
            k = k + step if abs(k + step) <= abs(delta) else delta
            rot = Matrix.Rotation(math.radians(k), 3, ax)
            cand = [head + rot @ (q - head) for q in rest]
            if guard and clearance(cand[:1] + [head.lerp(cand[0], 0.5)]) < floor:
                break
            chosen = k
        if abs(chosen) > 1e-6:
            rot = Matrix.Rotation(math.radians(chosen), 3, ax)
            pts[i + 1:] = [head + rot @ (q - head) for q in rest]
        return chosen

    for i, bname in enumerate(chain):
        d = (pts[i + 1] - pts[i]).normalized()
        want = g - 6.0 + trail * (1 + 0.4 * i)
        lat = swing(i, axis, max(-12.0, min(75.0, (want - _sagittal(Ri @ d)) * 0.85 * w_sag)), True)
        # On a body lying on its side, the cloth also falls sideways to the ground.
        d = (pts[i + 1] - pts[i]).normalized()
        roll = swing(i, side_axis, max(-70.0, min(70.0, (gl - _lateral(Ri @ d)) * 0.8 * w_lat)), True)
        if abs(lat) > 1e-6:
            P.r(bname, lat)
        if abs(roll) > 1e-6:
            P.r(bname, roll, ROLL)
    update()
    return g


def _lift_hem(ch, chain, geo, capes):
    P = ch.poser
    others = set(geo.all_objects()) - set(capes)
    # Cloth lying under the body has nowhere else to be: allow it a few centimetres, and only
    # bend a hem that truly digs in, a few small steps at most.
    for b in reversed(chain):
        for _ in range(4):
            if geo.min_z(exclude=others) > -0.035:
                return
            best = None
            for deg, axis in ((7, LAT), (-7, LAT), (7, ROLL), (-7, ROLL)):
                P.r(b, deg, axis)
                update()
                z = geo.min_z(exclude=others)
                P.r(b, -deg, axis)
                if best is None or z > best[0]:
                    best = (z, deg, axis)
            P.r(b, best[1], best[2])
            update()


def _lie_factor(arm):
    """0 while the torso is upright, 1 once it lies flat."""
    b = 'chest' if 'chest' in arm.pose.bones else 'root'
    pb = arm.pose.bones[b]
    d = (arm.matrix_world.to_3x3() @ (pb.matrix.to_3x3() @ Vector((0, 1, 0)))).normalized()
    tilt = math.degrees(math.acos(max(-1.0, min(1.0, d.z))))
    return max(0.0, min(1.0, (tilt - 30) / 55))


def _bone_point(arm, bone, along):
    pb = arm.pose.bones[bone]
    return arm.matrix_world @ (pb.matrix @ Vector((0, pb.bone.length * along, 0)))


def _move_root_world(ch, world_offset):
    arm = ch.arm
    local = arm.matrix_world.inverted().to_3x3() @ world_offset
    pb = arm.pose.bones['root']
    pb.location = Vector(pb.location) + ch.poser.inv['root'] @ local


def _bone_objects(arm):
    out = {}
    for o in bpy.data.objects:
        if o.parent == arm and o.parent_type == 'BONE':
            out.setdefault(o.parent_bone, []).append(o)
    return out


def _subtree_objects(arm, bone, bone_objs, scattered):
    """Objects carried by `bone` and by descendants that do not break away themselves."""
    out = list(bone_objs.get(bone, []))
    stack = [c for c in arm.pose.bones[bone].children]
    while stack:
        c = stack.pop()
        if c.name in scattered:
            continue
        out.extend(bone_objs.get(c.name, []))
        stack.extend(c.children)
    return out


def _key_identity(o, frame):
    o.matrix_basis = Matrix.Identity(4)
    o.keyframe_insert('location', frame=frame)
    o.keyframe_insert('rotation_quaternion', frame=frame)
    o.keyframe_insert('scale', frame=frame)


def _dim_targets(ch, dim):
    """Emission-strength sockets to fade: (socket, original value)."""
    if dim == []:
        return []
    seen, out = set(), []
    for o in bpy.data.objects:
        if o.type != 'MESH':
            continue
        for slot in o.material_slots:
            m = slot.material
            if m is None or not m.use_nodes or m.name in seen:
                continue
            if dim is not None and not any(m.name.startswith(p) for p in dim):
                continue
            seen.add(m.name)
            for node in m.node_tree.nodes:
                if node.type != 'BSDF_PRINCIPLED':
                    continue
                sock = node.inputs.get('Emission Strength')
                if sock is None:
                    continue
                if sock.is_linked:
                    src = sock.links[0].from_node
                    if src.type == 'MATH':
                        # strength * mask: fade whichever factor is the constant
                        for inp in src.inputs[:2]:
                            if not inp.is_linked and inp.default_value > 0.5:
                                out.append((inp, inp.default_value))
                                break
                elif sock.default_value > 0.5:
                    out.append((sock, sock.default_value))
    return out


def _constant(arm, objs, dims, hides):
    blocks = [arm] + list(objs) + [o for o, _ in hides]
    for b in blocks:
        ad = b.animation_data
        for fc in _fcurves(ad):
            for kp in fc.keyframe_points:
                kp.interpolation = 'CONSTANT'
    for sock, _ in dims:
        tree = sock.id_data
        for fc in _fcurves(tree.animation_data):
            for kp in fc.keyframe_points:
                kp.interpolation = 'CONSTANT'


def _fcurves(ad):
    if not ad or not ad.action:
        return []
    act = ad.action
    if hasattr(act, 'fcurves') and len(getattr(act, 'fcurves', [])):
        return list(act.fcurves)
    out = []
    for layer in getattr(act, 'layers', []):
        for strip in layer.strips:
            for bag in getattr(strip, 'channelbags', []):
                out.extend(bag.fcurves)
    return out


def _impact_frame(zs):
    if not zs:
        return 0
    top, low = zs[0], min(zs)
    if top - low < 0.05:
        return len(zs) // 2
    for i, z in enumerate(zs):
        if z <= low + max(0.05, 0.25 * (top - low)):
            return i
    return len(zs) - 1


# ------------------------------------------------------------------ motion library
# Bone conventions (rig.py): characters face +X, the camera is on -Y. Positive LAT leans an
# upright bone back and swings a hanging limb forward; positive ROLL tips the top toward the
# camera; positive YAW turns the character to its left (away from the camera).
def P(**kw):
    out = {}
    for k, v in kw.items():
        if k in ('move', 'scale'):
            out['@' + k] = {b.replace('_R', '.R').replace('_L', '.L'): val for b, val in v.items()}
            continue
        out[k.replace('_R', '.R').replace('_L', '.L')] = v
    return out


def _arms(r, l, fr=0.0, fl=0.0, rr=0.0, rl=0.0):
    """Upper-arm swing (LAT) and splay (ROLL, outward positive) with elbow bend."""
    return P(upper_arm_R=[(r, LAT), (-rr, ROLL)], upper_arm_L=[(l, LAT), (rl, ROLL)], forearm_R=fr, forearm_L=fl)


def _legs(tr, tl, sr, sl, fr=0.0, fl=0.0):
    return P(thigh_R=tr, thigh_L=tl, shin_R=sr, shin_L=sl, foot_R=fr, foot_L=fl)


def _m(*poses):
    return add_poses(*poses)


def fall_back(side=0.0, slide=0.08, sprawl=1.0, stagger=1.0, head_turn=30, arms=1.0):
    """Struck in the chest: head snaps back, a stagger step, the knees buckle into a sitting
    fall, the seat and then the back hit the ground, a small bounce, and the limbs go slack."""
    s = side
    keys = [
        Key(0.0, _m(P(spine=[(14, LAT)], chest=[(6, LAT)], head=[(24, LAT)]),
                    _arms(-40 * arms, -26 * arms, 24, 14, 26, 18)), 0.7, 'out'),
        Key(0.18, _m(P(spine=[(20 * stagger, LAT)], chest=[(8, LAT)], head=[(30, LAT)],
                       move={'root': (-0.07 * stagger, 0, 0)}),
                     _legs(14, -26, -6, -30, 4, 24), _arms(10 * arms, 26 * arms, 12, 10, 52, 44)), 0.4, 'out'),
        Key(0.40, _m(P(hips=[(22, LAT), (4 * s, ROLL)], spine=[(10, LAT)], head=[(8, LAT)],
                       move={'root': (-0.08 * stagger - slide * 0.3, 0, 0)}),
                     _legs(74, 62, -112, -100, 30, 26), _arms(54 * arms, 70 * arms, 24, 20, 30, 26)), 0.15, 'in'),
        Key(0.55, _m(P(hips=[(52, LAT), (6 * s, ROLL)], spine=[(14, LAT)], head=[(-12, LAT)],
                       move={'root': (-0.08 * stagger - slide * 0.6, 0, 0)}),
                     _legs(84, 72, -74, -92, 26, 22), _arms(62 * arms, 80 * arms, 30, 26, 36, 30)), 0.0, 'in3'),
        Key(0.68, _m(P(hips=[(88, LAT), (8 * s, ROLL)], spine=[(0, LAT)], head=[(18, LAT)],
                       move={'root': (-0.08 * stagger - slide, 0, 0)}),
                     _legs(44 * sprawl, 62 * sprawl, -58, -84, 20, 16), _arms(58 * arms, 40 * arms, 18, 26, 46, 50)),
            0.0, 'in3'),
        Key(0.78, _m(P(hips=[(83, LAT), (8 * s, ROLL)], spine=[(2, LAT)], head=[(0, LAT)],
                       move={'root': (-0.08 * stagger - slide, 0, 0)}),
                     _legs(50 * sprawl, 68 * sprawl, -66, -92, 22, 18), _arms(64 * arms, 48 * arms, 28, 34, 50, 54)),
            0.0, 'out'),
        Key(1.0, _m(P(hips=[(89, LAT), (10 * s, ROLL)], spine=[(-2, LAT)], head=[(8, LAT), (head_turn, YAW)],
                      move={'root': (-0.08 * stagger - slide, 0, 0)}),
                    P(thigh_R=[(12 * sprawl, LAT)], shin_R=-12, foot_R=26,
                      thigh_L=[(34 * sprawl, LAT), (-22, ROLL)], shin_L=-56, foot_L=16),
                    _arms(174 * arms, 2, 6, 8, 24, 58)), 0.0, 'io'),
    ]
    return dict(keys=keys, pins=[(0.0, 0.55, 'foot.R', 0.4)], motion='fall-back')


def fall_forward(side=0.0, reach=1.0, step=0.1, head_turn=-35, clutch=1.0):
    """Doubles over, stumbles a step, drops to the knees and falls on the face."""
    s = side
    keys = [
        Key(0.0, _m(P(spine=[(-16, LAT)], chest=[(-6, LAT)], head=[(-10, LAT)]),
                    _arms(12, 18, 70 * clutch, 80 * clutch, -6, -6)), 0.8, 'out'),
        Key(0.24, _m(P(hips=[(-8, LAT)], spine=[(-24, LAT)], head=[(-14, LAT)], move={'root': (step, 0, 0)}),
                     _legs(38, -14, -24, -40, 8, 18), _arms(18, 22, 74 * clutch, 84 * clutch, -4, -4)), 0.5, 'out'),
        Key(0.44, _m(P(hips=[(-16, LAT), (4 * s, ROLL)], spine=[(-30, LAT)], head=[(-16, LAT)],
                       move={'root': (step, 0, 0)}),
                     _legs(78, 66, -100, -108, 30, 34), _arms(34, 40, 40, 50, 6, 8)), 0.25, 'in'),
        Key(0.68, _m(P(hips=[(-84, LAT), (8 * s, ROLL)], spine=[(-4, LAT)], head=[(24, LAT), (head_turn, YAW)],
                       move={'root': (step + 0.04, 0, 0)}),
                     _legs(12, 4, -30, -16, 34, 30), _arms(140 * reach, 40, 10, 30, 16, 30)), 0.0, 'in3'),
        Key(0.78, _m(P(hips=[(-79, LAT), (8 * s, ROLL)], spine=[(-6, LAT)], head=[(16, LAT), (head_turn, YAW)],
                       move={'root': (step + 0.04, 0, 0)}),
                     _legs(18, 8, -42, -24, 30, 30), _arms(150 * reach, 46, 20, 38, 20, 34)), 0.0, 'out'),
        Key(1.0, _m(P(hips=[(-87, LAT), (10 * s, ROLL)], spine=[(-2, LAT)], head=[(28, LAT), (head_turn * 1.2, YAW)],
                      move={'root': (step + 0.04, 0, 0)}),
                    _legs(8, 14, -24, -40, 40, 34), _arms(146 * reach, 22, 14, 30, 24, 30)), 0.0, 'io'),
    ]
    return dict(keys=keys, pins=[(0.44, 0.68, 'shin.R', 0.0)], motion='fall-forward')


def kneel_topple(side=1.0, pause=1.0, bow=1.0, arm_raise=0.0):
    """Drops to one knee, holds a beat with the head bowed, then topples sideways."""
    s = side
    kneel = _m(_legs(84, 6, -88, -112, 4, 40), P(spine=[(-10 * bow, LAT)], head=[(-18 * bow, LAT)]))
    keys = [
        Key(0.0, _m(P(spine=[(12, LAT)], head=[(14, LAT)]), _arms(-18, -10, 24, 14)), 0.85, 'out'),
        Key(0.30, _m(kneel, _arms(-4 + arm_raise, 6, 22, 26, 4, 4)), 0.4, 'out'),
        Key(0.30 + 0.22 * pause, _m(kneel, P(spine=[(-16 * bow, LAT), (4 * s, ROLL)], head=[(-28 * bow, LAT), (8 * s, ROLL)]),
                                    _arms(-8 + arm_raise * 0.8, 2, 16, 22, 2, 2)), 0.15, 'io'),
        Key(0.80, _m(_legs(70, 30, -96, -110, 10, 30), P(hips=[(84 * s, ROLL), (-10, LAT)], spine=[(-14, LAT), (6 * s, ROLL)],
                                                         head=[(-10, LAT), (16 * s, ROLL)]),
                     _arms(18, 30, 30, 40, 30, 10)), 0.0, 'in3'),
        Key(0.89, _m(_legs(72, 34, -100, -114, 10, 30), P(hips=[(78 * s, ROLL), (-10, LAT)], spine=[(-16, LAT)],
                                                          head=[(-4, LAT), (6 * s, ROLL)]),
                     _arms(26, 40, 40, 48, 36, 14)), 0.0, 'out'),
        Key(1.0, _m(_legs(66, 40, -92, -104, 12, 28), P(hips=[(88 * s, ROLL), (-12, LAT)], spine=[(-18, LAT)],
                                                        head=[(-12, LAT), (14 * s, ROLL)]),
                    _arms(40, 22, 36, 44, 30, 8)), 0.0, 'io'),
    ]
    return dict(keys=keys, pins=[(0.30, 0.76, 'shin.L', 0.0)], motion='kneel-topple')


def crumple(side=1.0, jolt=1.0, forward=1.0):
    """The legs simply stop: a jolt, a vertical collapse onto the heels, then a heap."""
    s = side
    keys = [
        Key(0.0, _m(P(spine=[(6 * jolt, LAT)], head=[(12 * jolt, LAT)]), _arms(-10, -6, 10, 6, 10, 10)), 0.7, 'out'),
        Key(0.34, _m(_legs(76, 84, -134, -142, 46, 50), P(spine=[(-18, LAT)], head=[(-22, LAT)]),
                     _arms(-14, -10, 8, 6, 6, 6)), 0.18, 'in'),
        Key(0.54, _m(_legs(80, 88, -140, -146, 50, 54), P(spine=[(-36 * forward, LAT)], chest=[(-12, LAT)],
                                                          head=[(-30, LAT)]),
                     _arms(-6, -2, 14, 10, 4, 4)), 0.0, 'in'),
        Key(0.80, _m(_legs(66, 80, -120, -136, 40, 46), P(hips=[(-48 * forward, LAT), (30 * s, ROLL)], spine=[(-28, LAT)],
                                                          head=[(-10, LAT), (30 * s, YAW)]),
                     _arms(20, 10, 20, 20, 20, 10)), 0.0, 'in3'),
        Key(0.90, _m(_legs(66, 80, -118, -132, 40, 46), P(hips=[(-44 * forward, LAT), (27 * s, ROLL)], spine=[(-26, LAT)],
                                                          head=[(-6, LAT), (30 * s, YAW)]),
                     _arms(26, 16, 26, 24, 24, 12)), 0.0, 'out'),
        Key(1.0, _m(_legs(62, 78, -114, -130, 40, 46), P(hips=[(-52 * forward, LAT), (36 * s, ROLL)], spine=[(-30, LAT)],
                                                         head=[(-12, LAT), (34 * s, YAW)]),
                    _arms(24, 8, 22, 18, 22, 8)), 0.0, 'io'),
    ]
    return dict(keys=keys, pins=[], motion='crumple')


def spin_fall(side=1.0, turn=1.0):
    """Spun around by the blow: twists, arms flung wide, and lands on the back half-turned."""
    s = side
    keys = [
        Key(0.0, _m(P(spine=[(22 * turn, YAW), (6, LAT)], head=[(30 * turn, YAW)]), _arms(-30, 20, 30, 10, 30, 20)),
            0.7, 'out'),
        Key(0.22, _m(P(root=[(84 * turn, YAW)], spine=[(14, LAT), (10, YAW)], head=[(16, LAT)]),
                     _legs(20, 40, -40, -60, 10, 14), _arms(30, 60, 10, 20, 70, 60)), 0.35, 'io'),
        Key(0.44, _m(P(root=[(124 * turn, YAW)], hips=[(40, LAT), (8 * s, ROLL)], spine=[(10, LAT)], head=[(16, LAT)]),
                     _legs(40, 30, -70, -50, 14, 10), _arms(70, 90, 20, 20, 50, 50)), 0.15, 'in'),
        Key(0.62, _m(P(root=[(150 * turn, YAW)], hips=[(86, LAT), (10 * s, ROLL)], spine=[(-4, LAT)], head=[(20, LAT)]),
                     _legs(14, 30, -24, -46, 16, 12), _arms(54, 70, 16, 24, 50, 50)), 0.0, 'in3'),
        Key(0.72, _m(P(root=[(150 * turn, YAW)], hips=[(80, LAT), (10 * s, ROLL)], spine=[(-2, LAT)], head=[(-2, LAT)]),
                     _legs(16, 34, -30, -56, 18, 12), _arms(62, 78, 26, 34, 54, 54)), 0.0, 'out'),
        Key(1.0, _m(P(root=[(152 * turn, YAW)], hips=[(88, LAT), (12 * s, ROLL)], spine=[(-2, LAT)], head=[(4, LAT), (-30, YAW)]),
                    _legs(6, 26, -8, -46, 20, 14), _arms(0, 174, 4, 6, 0, 24)), 0.0, 'io'),
    ]
    return dict(keys=keys, pins=[(0.0, 0.22, 'foot.L', 0.4)], motion='spin-fall')


def stagger_back(side=0.0, steps=1.0, weight=1.0):
    """A big body reels back two heavy steps, sinks to its knees, then slams down on its face."""
    s = side
    keys = [
        Key(0.0, _m(P(spine=[(18, LAT)], head=[(22, LAT)]), _arms(-30, -24, 26, 20, 16, 16)), 0.8, 'out'),
        Key(0.14, _m(P(spine=[(20, LAT)], head=[(18, LAT)], move={'root': (-0.12 * steps, 0, 0)}),
                     _legs(30, -26, -20, -50, 4, 30), _arms(-34, -20, 30, 24, 20, 18)), 0.7, 'out'),
        Key(0.28, _m(P(spine=[(14, LAT)], head=[(10, LAT)], move={'root': (-0.24 * steps, 0, 0)}),
                     _legs(-20, 28, -46, -18, 30, 4), _arms(-20, -30, 26, 30, 14, 20)), 0.6, 'io'),
        Key(0.46, _m(_legs(84, 78, -112, -116, 40, 40), P(spine=[(-8, LAT)], head=[(-10, LAT)],
                                                          move={'root': (-0.26 * steps, 0, 0)}),
                     _arms(-6, -2, 20, 16, 6, 6)), 0.3, 'in'),
        Key(0.62, _m(_legs(86, 80, -114, -118, 40, 40), P(spine=[(-16, LAT), (5 * s, ROLL)], head=[(-26, LAT)],
                                                          move={'root': (-0.26 * steps, 0, 0)}),
                     _arms(-10, -4, 14, 12, 4, 4)), 0.1, 'io'),
        Key(0.84, _m(_legs(14, 10, -34, -28, 34, 30), P(hips=[(-86, LAT), (6 * s, ROLL)], spine=[(-4, LAT)],
                                                        head=[(20, LAT), (-30, YAW)], move={'root': (-0.22 * steps, 0, 0)}),
                     _arms(110, 30, 12, 24, 30, 30)), 0.0, 'in3'),
        Key(0.92, _m(_legs(18, 14, -44, -36, 30, 30), P(hips=[(-82 * (1 - 0.02 * weight), LAT), (6 * s, ROLL)], spine=[(-6, LAT)],
                                                        head=[(14, LAT), (-30, YAW)], move={'root': (-0.22 * steps, 0, 0)}),
                     _arms(116, 34, 20, 30, 34, 34)), 0.0, 'out'),
        Key(1.0, _m(_legs(10, 16, -30, -40, 36, 32), P(hips=[(-88, LAT), (8 * s, ROLL)], spine=[(-2, LAT)],
                                                       head=[(24, LAT), (-36, YAW)], move={'root': (-0.22 * steps, 0, 0)}),
                    _arms(112, 18, 14, 26, 30, 30)), 0.0, 'io'),
    ]
    return dict(keys=keys, pins=[(0.46, 0.84, 'shin.R', 0.0)], motion='stagger-back')


def rise_collapse(side=1.0, lift=0.12, arms_up=1.0, spin=0.0):
    """Casters: the magic leaves them. Lifted onto the toes, arms rising, then a dead drop."""
    s = side
    keys = [
        Key(0.0, _m(P(spine=[(14, LAT)], head=[(20, LAT)]), _arms(24 * arms_up, 30 * arms_up, 10, 10, 30, 30)), 0.6, 'out'),
        Key(0.34, _m(P(spine=[(20, LAT)], head=[(32, LAT)], root=[(30 * spin, YAW)], move={'root': (0, 0, lift)}),
                     _legs(4, -6, -8, -12, 46, 50), _arms(110 * arms_up, 120 * arms_up, 4, 6, 40, 40)), 0.15, 'out'),
        Key(0.50, _m(P(spine=[(22, LAT)], head=[(36, LAT)], root=[(45 * spin, YAW)], move={'root': (0, 0, lift * 1.15)}),
                     _legs(6, -4, -10, -14, 50, 54), _arms(122 * arms_up, 132 * arms_up, 2, 4, 44, 44)), 0.05, 'io'),
        Key(0.66, _m(P(spine=[(-38, LAT)], chest=[(-12, LAT)], head=[(-40, LAT)], root=[(50 * spin, YAW)]),
                     _legs(82, 88, -140, -146, 50, 54), _arms(-8, -4, 10, 8, 6, 6)), 0.0, 'in3'),
        Key(0.86, _m(P(root=[(50 * spin, YAW)], hips=[(58 * s, ROLL), (-26, LAT)], spine=[(-30, LAT)], head=[(-14, LAT), (30 * s, YAW)]),
                     _legs(70, 82, -124, -138, 40, 46), _arms(24, 14, 24, 20, 24, 10)), 0.0, 'in'),
        Key(1.0, _m(P(root=[(50 * spin, YAW)], hips=[(64 * s, ROLL), (-30, LAT)], spine=[(-30, LAT)], head=[(-16, LAT), (34 * s, YAW)]),
                    _legs(66, 80, -120, -134, 40, 46), _arms(28, 10, 26, 18, 26, 8)), 0.0, 'io'),
    ]
    return dict(keys=keys, pins=[], air=[0.17, 0.34, 0.42, 0.5, 0.58], motion='rise-collapse')


def pray_slump(side=0.0):
    """Sinks to both knees, hands meeting in prayer, bows, and folds forward."""
    s = side
    kneel = _legs(6, 2, -108, -112, 30, 30)
    pray = P(upper_arm_R=[(46, LAT), (-14, ROLL)], upper_arm_L=[(46, LAT), (14, ROLL)], forearm_R=96, forearm_L=96,
             hand_R=[(20, ROLL)], hand_L=[(-20, ROLL)])
    keys = [
        Key(0.0, _m(P(spine=[(10, LAT)], head=[(14, LAT)]), _arms(-14, -10, 20, 14)), 0.8, 'out'),
        Key(0.30, _m(kneel, pray, P(spine=[(-2, LAT)], head=[(-6, LAT)])), 0.2, 'out'),
        Key(0.54, _m(kneel, pray, P(spine=[(-14, LAT)], head=[(-34, LAT)])), 0.05, 'io'),
        Key(0.80, _m(_legs(40, 36, -130, -134, 40, 40), P(hips=[(-70, LAT), (6 * s, ROLL)], spine=[(-20, LAT)],
                                                          head=[(-10, LAT), (-30, YAW)]),
                     _arms(70, 76, 60, 64, 10, 10)), 0.0, 'in3'),
        Key(0.90, _m(_legs(40, 36, -128, -132, 40, 40), P(hips=[(-66, LAT), (6 * s, ROLL)], spine=[(-20, LAT)],
                                                          head=[(-8, LAT), (-30, YAW)]),
                     _arms(76, 80, 54, 60, 12, 12)), 0.0, 'out'),
        Key(1.0, _m(_legs(44, 40, -132, -136, 40, 40), P(hips=[(-74, LAT), (8 * s, ROLL)], spine=[(-22, LAT)],
                                                         head=[(-6, LAT), (-36, YAW)]),
                    _arms(90, 84, 40, 50, 16, 14)), 0.0, 'io'),
    ]
    return dict(keys=keys, pins=[(0.30, 0.80, 'shin.R', 0.0)], motion='pray-slump')


def brace_slide(side=-1.0, hold_arm='R'):
    """Holds on to a planted weapon: sinks to a knee against it, then slides down and rolls off."""
    s = side
    grip = P(**{('upper_arm_' + hold_arm): [(70, LAT)], ('forearm_' + hold_arm): 20})
    kneel = _legs(80, 4, -92, -112, 6, 40)
    keys = [
        Key(0.0, _m(P(spine=[(10, LAT)], head=[(14, LAT)]), grip), 0.8, 'out'),
        Key(0.32, _m(kneel, grip, P(spine=[(-8, LAT), (-6 * s, ROLL)], head=[(-14, LAT)])), 0.3, 'out'),
        Key(0.56, _m(kneel, grip, P(spine=[(-14, LAT), (-8 * s, ROLL)], head=[(-30, LAT), (-8 * s, ROLL)])), 0.12, 'io'),
        Key(0.82, _m(_legs(64, 34, -96, -108, 12, 30), P(hips=[(80 * s, ROLL), (-10, LAT)], spine=[(-12, LAT)],
                                                         head=[(-8, LAT), (12 * s, ROLL)]),
                     _arms(40, 24, 30, 36, 20, 10)), 0.0, 'in3'),
        Key(0.91, _m(_legs(64, 36, -98, -110, 12, 30), P(hips=[(75 * s, ROLL), (-10, LAT)], spine=[(-12, LAT)],
                                                         head=[(-4, LAT), (8 * s, ROLL)]),
                     _arms(48, 30, 36, 42, 24, 12)), 0.0, 'out'),
        Key(1.0, _m(_legs(60, 40, -92, -104, 14, 28), P(hips=[(88 * s, ROLL), (-12, LAT)], spine=[(-14, LAT)],
                                                        head=[(-12, LAT), (14 * s, ROLL)]),
                    _arms(56, 20, 30, 40, 26, 8)), 0.0, 'io'),
    ]
    return dict(keys=keys, pins=[(0.32, 0.78, 'shin.L', 0.0)], motion='brace-slide')


def topple_rigid(side=0.0, sway=1.0):
    """Armoured and stiff: sways back, tips past balance and falls like a felled tree."""
    s = side
    keys = [
        Key(0.0, _m(P(root=[(4 * sway, LAT)], head=[(10, LAT)]), _arms(-8, -6, 0, 0)), 0.95, 'out'),
        Key(0.22, _m(P(root=[(7 * sway, LAT)], head=[(14, LAT)]), _arms(-14, -8, 0, 0)), 0.85, 'io'),
        Key(0.46, _m(P(root=[(-24, LAT), (3 * s, ROLL)], head=[(-4, LAT)]), _legs(0, -6, -4, -8, 18, 24),
                     _arms(10, 6, 0, 0, 4, 4)), 0.7, 'in'),
        Key(0.70, _m(P(root=[(-89, LAT), (5 * s, ROLL)], head=[(20, LAT), (-20, YAW)]), _legs(0, -4, -6, -10, 40, 44),
                     _arms(124, 14, 10, 10, 20, 18)), 0.55, 'in3'),
        Key(0.80, _m(P(root=[(-83, LAT), (5 * s, ROLL)], head=[(12, LAT), (-20, YAW)]), _legs(4, 0, -12, -16, 38, 40),
                     _arms(130, 20, 14, 14, 24, 22)), 0.5, 'out'),
        Key(1.0, _m(P(root=[(-89, LAT), (6 * s, ROLL)], head=[(24, LAT), (-26, YAW)]), _legs(0, -2, -10, -14, 42, 46),
                    _arms(128, 10, 12, 12, 22, 18)), 0.45, 'io'),
    ]
    return dict(keys=keys, pins=[(0.0, 0.70, 'foot.R', 1.0)], motion='topple-rigid')


def dive_skid(side=0.0, dist=0.5, twitch=0.0):
    """Dies mid-lunge: momentum throws it forward and it skids to a stop on its chest."""
    s = side
    keys = [
        Key(0.0, _m(P(hips=[(-14, LAT)], spine=[(-12, LAT)], head=[(14, LAT)], move={'root': (0.05, 0, 0)}),
                    _legs(30, -30, -30, -40, 10, 30), _arms(60, 40, 20, 20)), 0.7, 'out'),
        Key(0.28, _m(P(hips=[(-48, LAT)], spine=[(-6, LAT)], head=[(20, LAT)], move={'root': (dist * 0.45, 0, 0)}),
                     _legs(10, -24, -40, -30, 30, 30), _arms(110, 90, 10, 14, 20, 20)), 0.3, 'in'),
        Key(0.50, _m(P(hips=[(-86, LAT), (6 * s, ROLL)], spine=[(-2, LAT)], head=[(26, LAT), (-20, YAW)],
                       move={'root': (dist * 0.75, 0, 0)}),
                     _legs(4, -10, -30, -20, 40, 40), _arms(150, 40, 10, 20, 20, 30)), 0.0, 'in3'),
        Key(0.74, _m(P(hips=[(-84, LAT), (6 * s, ROLL)], spine=[(-4, LAT)], head=[(20, LAT), (-26, YAW)],
                       move={'root': (dist * 0.95, 0, 0)}),
                     _legs(8, -4, -40, -30, 40, 40), _arms(156, 44, 20, 26, 24, 32)), 0.0, 'out'),
        Key(0.87, _m(P(hips=[(-87, LAT), (8 * s, ROLL)], spine=[(-4, LAT)], head=[(30 + 10 * twitch, LAT), (-30, YAW)],
                       move={'root': (dist, 0, 0)}),
                     _legs(6 + 30 * twitch, 12, -26 - 50 * twitch, -44, 44, 36), _arms(146 + 14 * twitch, 20, 12 + 40 * twitch, 30, 22, 30)),
            0.0, 'io'),
        Key(1.0, _m(P(hips=[(-88, LAT), (8 * s, ROLL)], spine=[(-2, LAT)], head=[(28, LAT), (-34, YAW)],
                      move={'root': (dist, 0, 0)}),
                    _legs(6, 12, -26, -44, 44, 36), _arms(146, 20, 12, 30, 22, 30)), 0.0, 'io'),
    ]
    return dict(keys=keys, pins=[], air=[0.28], motion='dive-skid')


def burst(side=1.0, swell=1.35):
    """Bloated dead: swells, shudders, bursts and collapses deflated."""
    s = side
    keys = [
        Key(0.0, _m(P(spine=[(8, LAT)], head=[(16, LAT)], scale={'chest': 1.06, 'spine': 1.04}), _arms(-10, -6, 20, 20, 20, 20)),
            0.8, 'out'),
        Key(0.20, _m(P(spine=[(10, LAT), (6, ROLL)], head=[(24, LAT), (-10, ROLL)], scale={'chest': swell * 0.9, 'spine': 1.15}),
                     _arms(10, 20, 30, 30, 50, 50)), 0.6, 'io'),
        Key(0.34, _m(P(spine=[(12, LAT), (-6, ROLL)], head=[(30, LAT), (10, ROLL)], scale={'chest': swell, 'spine': 1.22}),
                     _arms(20, 30, 20, 20, 60, 60)), 0.5, 'io'),
        Key(0.44, _m(P(spine=[(20, LAT)], head=[(36, LAT)], scale={'chest': 0.82, 'spine': 0.92}, move={'root': (-0.06, 0, 0)}),
                     _legs(20, 26, -40, -50, 10, 10), _arms(40, 50, 10, 10, 70, 70)), 0.3, 'out3'),
        Key(0.72, _m(P(hips=[(56, LAT), (10 * s, ROLL)], spine=[(10, LAT)], head=[(20, LAT)], scale={'chest': 0.8, 'spine': 0.9},
                       move={'root': (-0.08, 0, 0)}),
                     _legs(50, 60, -100, -110, 30, 30), _arms(60, 80, 20, 20, 40, 40)), 0.0, 'in3'),
        Key(0.84, _m(P(hips=[(52, LAT), (10 * s, ROLL)], spine=[(8, LAT)], head=[(14, LAT)], scale={'chest': 0.8, 'spine': 0.9},
                       move={'root': (-0.08, 0, 0)}),
                     _legs(52, 62, -104, -114, 30, 30), _arms(40, 60, 24, 24, 56, 56)), 0.0, 'out'),
        Key(1.0, _m(P(hips=[(60, LAT), (14 * s, ROLL)], spine=[(10, LAT)], head=[(24, LAT), (30, YAW)],
                      scale={'chest': 0.78, 'spine': 0.9}, move={'root': (-0.08, 0, 0)}),
                    _legs(48, 62, -100, -116, 30, 30), _arms(24, 40, 20, 22, 60, 58)), 0.0, 'io'),
    ]
    return dict(keys=keys, pins=[(0.0, 0.44, 'foot.L', 0.4)], motion='burst', scale_keys=True)


def last_roar(side=0.0, roar=1.0):
    """Refuses to fall: drops to both knees, throws the head back in a last roar with arms
    flung wide, then pitches forward onto the face."""
    s = side
    kneel = _legs(6, 2, -108, -112, 30, 30)
    keys = [
        Key(0.0, _m(P(spine=[(10, LAT)], head=[(12, LAT)]), _arms(-20, -16, 30, 24, 20, 20)), 0.8, 'out'),
        Key(0.24, _m(kneel, P(spine=[(6, LAT)], head=[(4, LAT)]), _arms(10, 14, 30, 30, 40, 40)), 0.35, 'out'),
        Key(0.44, _m(kneel, P(spine=[(18 * roar, LAT)], chest=[(10 * roar, LAT)], head=[(40 * roar, LAT)]),
                     _arms(60, 66, 16, 20, 70, 70)), 0.1, 'out'),
        Key(0.60, _m(kneel, P(spine=[(20 * roar, LAT)], chest=[(12 * roar, LAT)], head=[(44 * roar, LAT), (4 * s, ROLL)]),
                     _arms(64, 70, 12, 16, 74, 74)), 0.05, 'io'),
        Key(0.82, _m(_legs(28, 24, -70, -74, 34, 34), P(hips=[(-84, LAT), (6 * s, ROLL)], spine=[(-4, LAT)],
                                                        head=[(22, LAT), (-34, YAW)]),
                     _arms(124, 36, 14, 24, 30, 32)), 0.0, 'in3'),
        Key(0.90, _m(_legs(30, 26, -76, -80, 34, 34), P(hips=[(-80, LAT), (6 * s, ROLL)], spine=[(-6, LAT)],
                                                        head=[(16, LAT), (-34, YAW)]),
                     _arms(132, 40, 24, 30, 34, 36)), 0.0, 'out'),
        Key(1.0, _m(_legs(22, 30, -60, -80, 36, 34), P(hips=[(-87, LAT), (8 * s, ROLL)], spine=[(-2, LAT)],
                                                       head=[(26, LAT), (-40, YAW)]),
                    _arms(130, 20, 16, 26, 30, 30)), 0.0, 'io'),
    ]
    return dict(keys=keys, pins=[(0.24, 0.82, 'shin.R', 0.0)], motion='last-roar')


def sit_slump(side=1.0, step=1.0):
    """Shot: staggers back a step, sits down hard, slumps over the knees and tips onto a side."""
    s = side
    keys = [
        Key(0.0, _m(P(spine=[(12, LAT)], head=[(16, LAT)]), _arms(-16, -10, 10, 10, 10, 10)), 0.75, 'out'),
        Key(0.22, _m(P(spine=[(14, LAT)], head=[(14, LAT)], move={'root': (-0.06 * step, 0, 0)}),
                     _legs(10, -24, -10, -26, 4, 22), _arms(-12, -6, 14, 12, 16, 16)), 0.45, 'out'),
        Key(0.45, _m(P(hips=[(12, LAT)], spine=[(-6, LAT)], head=[(-10, LAT)], move={'root': (-0.08 * step, 0, 0)}),
                     _legs(80, 72, -6, -18, 16, 12), _arms(-10, -6, 20, 16, 16, 16)), 0.1, 'in3'),
        Key(0.58, _m(P(hips=[(10, LAT)], spine=[(-2, LAT)], head=[(0, LAT)], move={'root': (-0.08 * step, 0, 0)}),
                     _legs(78, 70, -6, -16, 16, 12), _arms(-6, -2, 24, 20, 18, 18)), 0.05, 'out'),
        Key(0.78, _m(P(hips=[(8, LAT)], spine=[(-34, LAT)], chest=[(-14, LAT)], head=[(-40, LAT)],
                       move={'root': (-0.08 * step, 0, 0)}),
                     _legs(80, 72, -8, -20, 16, 12), _arms(30, 26, 20, 18, 8, 8)), 0.0, 'io'),
        Key(1.0, _m(P(hips=[(8, LAT), (66 * s, ROLL)], spine=[(-24, LAT)], head=[(-8, LAT), (24 * s, YAW)],
                      move={'root': (-0.08 * step, 0, 0)}),
                    _legs(76, 66, -20, -44, 20, 16), _arms(34, 20, 26, 22, 24, 10)), 0.0, 'in'),
    ]
    return dict(keys=keys, pins=[(0.0, 0.45, 'foot.R', 0.4)], motion='sit-slump')


# ------------------------------------------------------------------ beasts and machines
def _paws(fu=0.0, fl=0.0, bu=0.0, bl=0.0, sides=('R', 'L'), twitch=0.0, ff=0.0, bf=0.0):
    out = {}
    for i, side in enumerate(sides):
        k = 1 + twitch * (1 if i == 0 else -0.6)
        out['fl.%s.upper' % side] = fu * k
        out['fl.%s.lower' % side] = fl * k
        out['bl.%s.upper' % side] = bu * k
        out['bl.%s.lower' % side] = bl * k
        if ff:
            out['fl.%s.foot' % side] = ff * k
        if bf:
            out['bl.%s.foot' % side] = bf * k
    return out


def hound_fall(side=-1.0):
    """Yelps and rears, legs buckle, rolls onto its side, a last kick, then still."""
    s = side
    keys = [
        Key(0.0, _m(P(neck=[(22, LAT)], head=[(16, LAT)], jaw=[(-28, LAT)], spine=[(6, LAT)]),
                    _paws(-24, 10, 6, -6)), 0.8, 'out'),
        Key(0.24, _m(P(hips=[(16 * s, ROLL)], neck=[(-6, LAT)], head=[(-8, LAT)], jaw=[(-14, LAT)]),
                     _paws(16, -46, -10, 40)), 0.4, 'io'),
        Key(0.48, _m(P(hips=[(60 * s, ROLL)], neck=[(-18, LAT)], head=[(-14, LAT)], jaw=[(-8, LAT)]), {'tail.0': [(-20, LAT)]},
                     _paws(24, -20, -24, 20)), 0.15, 'in'),
        Key(0.64, _m(P(hips=[(88 * s, ROLL)], neck=[(-24, LAT), (10 * s, ROLL)], head=[(-18, LAT)], jaw=[(-6, LAT)],
                       ), {'tail.0': [(-30, LAT)]}, _paws(34, -12, -34, 12)), 0.0, 'in3'),
        Key(0.74, _m(P(hips=[(82 * s, ROLL)], neck=[(-20, LAT), (8 * s, ROLL)], head=[(-12, LAT)], jaw=[(-10, LAT)],
                       ), {'tail.0': [(-26, LAT)]}, _paws(38, -16, -38, 16)), 0.0, 'out'),
        Key(0.86, _m(P(hips=[(88 * s, ROLL)], neck=[(-22, LAT), (10 * s, ROLL)], head=[(-16, LAT)], jaw=[(-8, LAT)],
                       ), {'tail.0': [(-30, LAT)]}, _paws(30, -30, -50, 34, twitch=0.4)), 0.0, 'io'),
        Key(1.0, _m(P(hips=[(89 * s, ROLL)], neck=[(-24, LAT), (10 * s, ROLL)], head=[(-18, LAT)], jaw=[(-12, LAT)],
                      ), {'tail.0': [(-32, LAT)]}, _paws(32, -14, -32, 14)), 0.0, 'io'),
    ]
    return dict(keys=keys, motion='hound-fall')


def horse_collapse(side=-1.0, roll=84.0):
    """Rears with a scream, the forelegs buckle and the chest drops, then it rolls onto its side."""
    s = side
    keys = [
        Key(0.0, _m(P(hips=[(8, LAT)], neck=[(18, LAT)], head=[(12, LAT)], jaw=[(-20, LAT)]), _paws(-20, 10, 4, -4)), 0.8, 'out'),
        Key(0.28, _m(P(hips=[(-14, LAT)], neck=[(-10, LAT)], head=[(-8, LAT)], jaw=[(-10, LAT)]),
                     _paws(30, -70, 6, -10, ff=-60)), 0.4, 'in'),
        Key(0.46, _m(P(hips=[(-10, LAT), (roll * 0.45 * s, ROLL)], neck=[(-20, LAT)], head=[(-14, LAT)]),
                     _paws(36, -80, 20, -40, ff=-60, bf=30)), 0.15, 'in'),
        Key(0.64, _m(P(hips=[(-4, LAT), (roll * s, ROLL)], neck=[(-30, LAT), (14 * s, ROLL)], head=[(-12, LAT)]),
                     _paws(20, -30, -10, 16, ff=-20, bf=10)), 0.0, 'in3'),
        Key(0.74, _m(P(hips=[(-4, LAT), (roll * 0.93 * s, ROLL)], neck=[(-26, LAT), (12 * s, ROLL)], head=[(-8, LAT)]),
                     _paws(24, -34, -14, 20, ff=-24, bf=12)), 0.0, 'out'),
        Key(0.86, _m(P(hips=[(-4, LAT), (roll * s, ROLL)], neck=[(-30, LAT), (14 * s, ROLL)], head=[(-12, LAT)]),
                     _paws(16, -40, -30, 34, ff=-20, bf=14, twitch=0.4)), 0.0, 'io'),
        Key(1.0, _m(P(hips=[(-4, LAT), (roll * 1.02 * s, ROLL)], neck=[(-34, LAT), (16 * s, ROLL)], head=[(-14, LAT)],
                      jaw=[(-8, LAT)]), _paws(18, -26, -16, 20, ff=-16, bf=10)), 0.0, 'io'),
    ]
    return dict(keys=keys, motion='horse-collapse')


def thrown_rider(side=0.0, dist=0.9):
    """Pitched over the horse's neck as it goes down; lands face first ahead of it."""
    s = side
    keys = [
        Key(0.0, _m(P(spine=[(10, LAT)], head=[(12, LAT)]), _arms(-20, -16, 20, 20)), 0.85, 'out'),
        Key(0.30, _m(P(spine=[(-40, LAT)], head=[(-10, LAT)], move={'root': (0.18 * dist, 0, 0.06)}),
                     _arms(90, 80, 10, 10, 20, 20)), 0.5, 'in'),
        Key(0.50, _m(P(hips=[(-62, LAT), (6 * s, ROLL)], spine=[(-10, LAT)], head=[(14, LAT)],
                       move={'root': (0.62 * dist, -0.05, -0.2)}),
                     _legs(30, 20, -40, -30, 20, 20), _arms(130, 110, 10, 16, 24, 24)), 0.2, 'in'),
        Key(0.66, _m(P(hips=[(-88, LAT), (8 * s, ROLL)], spine=[(-2, LAT)], head=[(24, LAT), (-30, YAW)],
                       move={'root': (dist, -0.08, -0.6)}),
                     _legs(10, 4, -24, -14, 34, 30), _arms(150, 40, 12, 22, 20, 30)), 0.0, 'in3'),
        Key(0.78, _m(P(hips=[(-82, LAT), (8 * s, ROLL)], spine=[(-4, LAT)], head=[(18, LAT), (-30, YAW)],
                       move={'root': (dist + 0.06, -0.08, -0.6)}),
                     _legs(14, 8, -34, -22, 30, 30), _arms(156, 44, 22, 30, 24, 32)), 0.0, 'out'),
        Key(1.0, _m(P(hips=[(-88, LAT), (10 * s, ROLL)], spine=[(-2, LAT)], head=[(28, LAT), (-36, YAW)],
                      move={'root': (dist + 0.08, -0.08, -0.6)}),
                    _legs(8, 14, -24, -40, 40, 34), _arms(146, 20, 14, 30, 24, 30)), 0.0, 'io'),
    ]
    # Still in the saddle, then flying: only the landing touches the ground.
    return dict(keys=keys, air=(0.0, 0.6), motion='thrown')


def machine_wreck(lean=14.0, pitch=-10.0, arm=0.0, squash=1.0, ramp=0.0, jolt=0.04):
    """Siege engines: a jolt, something gives (arm, ramp), the frame drops onto a broken corner."""
    keys = [
        Key(0.0, P(body=[(3, LAT)], move={'body': (0, 0, jolt)}, arm=[(arm * 0.6, LAT)]), 1.0, 'out'),
        Key(0.22, P(body=[(-3, LAT), (-3, ROLL)], move={'body': (0, 0, jolt * 1.5)}, arm=[(arm, LAT)], ramp=[(ramp * 0.3, LAT)]),
            1.0, 'out'),
        Key(0.46, P(body=[(pitch * 0.6, LAT), (lean * 0.6, ROLL)], arm=[(arm * 0.8, LAT)], ramp=[(ramp * 0.7, LAT)],
                    scale={'body': (1, 1, 1 - 0.06 * squash)}), 1.0, 'in'),
        Key(0.64, P(body=[(pitch, LAT), (lean, ROLL)], arm=[(arm * 0.5, LAT)], ramp=[(ramp, LAT)],
                    scale={'body': (1.02, 1.02, 1 - 0.12 * squash)}), 1.0, 'in3'),
        Key(0.76, P(body=[(pitch * 0.85, LAT), (lean * 0.9, ROLL)], arm=[(arm * 0.55, LAT)], ramp=[(ramp * 0.92, LAT)],
                    scale={'body': (1.01, 1.01, 1 - 0.1 * squash)}), 1.0, 'out'),
        Key(1.0, P(body=[(pitch, LAT), (lean, ROLL)], arm=[(arm * 0.5, LAT)], ramp=[(ramp, LAT)],
                   scale={'body': (1.02, 1.02, 1 - 0.12 * squash)}), 1.0, 'io'),
    ]
    return dict(keys=keys, motion='wreck', scale_keys=True)


def nest_burst(swell=1.7):
    """The heart swells and bursts; the nest sags and splits."""
    keys = [
        Key(0.0, P(scale={'heart': 1.15, 'body': (1.04, 1.04, 1.02)}), 1.0, 'out'),
        Key(0.30, P(scale={'heart': swell, 'body': (1.1, 1.1, 1.06)}), 1.0, 'io'),
        Key(0.40, P(scale={'heart': 0.02, 'body': (1.22, 1.22, 0.74)}), 1.0, 'out3'),
        Key(0.70, P(scale={'heart': 0.02, 'body': (1.42, 1.42, 0.36)}, body=[(6, ROLL)], move={'body': (0, 0, -0.1)}), 1.0, 'in3'),
        Key(0.82, P(scale={'heart': 0.02, 'body': (1.38, 1.38, 0.42)}, body=[(5, ROLL)], move={'body': (0, 0, -0.09)}), 1.0, 'out'),
        Key(1.0, P(scale={'heart': 0.02, 'body': (1.42, 1.42, 0.38)}, body=[(6, ROLL)], move={'body': (0, 0, -0.1)}), 1.0, 'io'),
    ]
    return dict(keys=keys, motion='nest-burst', scale_keys=True)


MOTIONS = {
    'hound_fall': hound_fall, 'horse_collapse': horse_collapse, 'thrown_rider': thrown_rider,
    'machine_wreck': machine_wreck, 'nest_burst': nest_burst,
    'last_roar': last_roar, 'sit_slump': sit_slump,
    'fall_back': fall_back, 'fall_forward': fall_forward, 'kneel_topple': kneel_topple, 'crumple': crumple,
    'spin_fall': spin_fall, 'stagger_back': stagger_back, 'rise_collapse': rise_collapse, 'pray_slump': pray_slump,
    'brace_slide': brace_slide, 'topple_rigid': topple_rigid, 'dive_skid': dive_skid, 'burst': burst,
}


def performance(stance, spec, motions=None):
    """Build a Performance from a roster/deaths.py spec."""
    spec = dict(spec)
    lib = dict(MOTIONS, **(motions or {}))
    fn = lib[spec.pop('motion')]
    params = spec.pop('params', {})
    m = fn(**params)
    keys = spec.pop('keys', None) or m['keys']
    return Performance(stance, keys, frames=spec.pop('frames', FRAMES), duration=spec.pop('duration', DURATION),
                       pins=spec.pop('pins', m.get('pins', ())), air=spec.pop('air', m.get('air', ())),
                       scale_keys=spec.pop('scale_keys', m.get('scale_keys', False)),
                       motion=spec.pop('label', m.get('motion', '')), **spec)
