"""Character assembly, animation keying, framing and rendering to the game contract.

Contract (unchanged from the original pipeline):
  master frames 256x320 RGBA: idle 0-3, walk 4-9, attack 10-19 (contact 14), hit 20-21,
  then the unit's death performance (12 frames, 16 for bosses; see death.py) and deploy (4);
  portrait 512x512. res_scale renders the same
  framing at a multiple of 256x320 for units drawn large in battle (see density.py).
  metadata: drawScale = 2 * ortho / 3.5 (same world-to-screen scale as before),
  anchorX/anchorY ground projection, motion.body / motion.contact offsets.
"""
import json
import math
from pathlib import Path

import bpy
from mathutils import Euler, Matrix, Vector
from bpy_extras.object_utils import world_to_camera_view

from . import anim, body, core, geo, rig
from .death import Performance
from .rig import LAT, Poser

CAM_DIR = Vector((0.55, -0.83, 0.28)).normalized()
FRAME_W, FRAME_H = 256, 320


class Character:
    def __init__(self, ident, title, spec=None, coll=None, extras=None, rig_kind='humanoid', skeleton_fn=None):
        self.ident = ident
        self.title = title
        self.coll = coll or core.collection(title + ' • model')
        self.body_skin = None
        self.props = []      # loose kit for death performances: {'name', 'objs', 'bone'}
        if skeleton_fn is not None:
            self.s = dict(spec or {})
            self.parts = []
            self.body = None
            self.tip_rest = None
            self.release_objs = []
            self.ik = []
            self.extra_bones = []
            self._extras = extras
            self.arm, self.J = skeleton_fn(ident, self.s, self.coll)
            self.poser = Poser(self.arm)
            self.stance = {}
            return
        self.s = body.spec(**(spec or {}))
        self.J = body.joints(self.s)
        self.parts = []
        self.body = None
        self.tip_rest = None
        self.release_objs = []
        self.ik = []
        self.extra_bones = []
        self._extras = extras
        self.arm, _ = body.skeleton(ident, self.s, self.coll, extras=extras)
        self.poser = Poser(self.arm)
        self.stance = {}

    # ---------------------------------------------------------- authoring
    def add(self, items):
        for it in items:
            if isinstance(it, tuple):
                self.parts.append(it)
            else:
                raise ValueError('part must be (obj, binding)')
        return items

    def add_rigid(self, objs, bone):
        for o in objs:
            self.parts.append((o, bone))
        return objs

    def build_body(self, mats, **kw):
        self.body = body.build_body(self.ident + ' body', self.s, self.J, self.coll, mats, **kw)
        return self.body

    def set_stance(self, pose):
        self.stance = pose
        self.poser.clear()
        self.poser.apply(pose)
        bpy.context.view_layer.update()

    def _conv(self, bone):
        pb = self.arm.pose.bones[bone]
        posed = self.arm.matrix_world @ pb.matrix
        rest = self.arm.matrix_world @ pb.bone.matrix_local
        return rest @ posed.inverted(), posed

    def hold(self, wd, hand='R', pitch=60.0, yaw=0.0, roll=0.0, slide=0.0, offset=(0, 0, 0), bone=None,
             track_tip=True):
        """Place a weapon so that, in the stance pose, its axis points by pitch/yaw from the hand."""
        bone = bone or 'hand.' + hand
        conv, posed = self._conv(bone)
        pb = self.arm.pose.bones[bone]
        center = posed @ Vector((0, pb.bone.length * 0.5, 0)) + Vector(offset)
        rot = Euler((0, -math.radians(pitch), math.radians(yaw)), 'XYZ').to_matrix()
        zw = rot @ Vector((1, 0, 0))
        yw = rot @ Vector((0, 1, 0))
        xw = yw.cross(zw)
        m3 = Matrix((xw, yw, zw)).transposed()
        if roll:
            m3 = m3 @ Matrix.Rotation(math.radians(roll), 3, 'Z')
        M = Matrix.Translation(center) @ m3.to_4x4() @ Matrix.Translation((0, 0, -slide))
        Mr = conv @ M
        for o in wd['objs']:
            geo.transform(o, Mr)
            self.parts.append((o, bone))
        self.add_prop('weapon' if hand == 'R' and not bone.startswith('hand.L') else 'offhand', wd['objs'], bone)
        res = dict(wd)
        res['matrix'] = Mr
        if wd.get('tip') is not None:
            res['tip_rest'] = Mr @ wd['tip']
            if track_tip:
                self.tip_rest = res['tip_rest']
                self.tip_bone = bone
        if wd.get('grip2') is not None:
            res['grip2_rest'] = Mr @ wd['grip2']
        return res

    def strap(self, wd, side='L', forward=0.12, up=0.0, out=0.0, yaw=-10.0, pitch=0.0, bone=None):
        """Shield on the forearm: in the stance its face points forward (+X) and its long axis is vertical."""
        bone = bone or 'forearm.' + side
        conv, posed = self._conv(bone)
        pb = self.arm.pose.bones[bone]
        mid = posed @ Vector((0, pb.bone.length * 0.55, 0))
        center = mid + Vector((forward, (1 if side == 'L' else -1) * out, up))
        m3 = Euler((0, math.radians(pitch), math.radians(yaw)), 'XYZ').to_matrix()
        M = Matrix.Translation(center) @ m3.to_4x4()
        Mr = conv @ M
        for o in wd['objs']:
            geo.transform(o, Mr)
            self.parts.append((o, bone))
        self.add_prop('shield', wd['objs'], bone)
        return Mr

    def add_prop(self, name, objs, bone):
        """Register kit that can come loose in a death (second weapon -> 'weapon2', ...)."""
        taken = {p['name'] for p in self.props}
        base, i = name, 2
        while name in taken:
            name = f'{base}{i}'
            i += 1
        self.props.append({'name': name, 'objs': list(objs), 'bone': bone})
        return name

    def place_in_stance(self, objs, bone, matrix_world_stance):
        conv, _ = self._conv(bone)
        Mr = conv @ matrix_world_stance
        for o in objs:
            geo.transform(o, Mr)
            self.parts.append((o, bone))
        return Mr

    def stance_point(self, bone, local=(0, 0.5, 0)):
        _, posed = self._conv(bone)
        pb = self.arm.pose.bones[bone]
        return posed @ Vector((local[0], local[1] * pb.bone.length, local[2]))

    def two_hand(self, grip2_rest, parent='hand.R', side='L', pole=(-0.3, 0.25, -0.3)):
        """Off hand follows a grip point on the weapon via IK."""
        sy = 1 if side == 'L' else -1
        self.extra_bones.append(('grip.' + side, grip2_rest, grip2_rest + Vector((0.08, 0, 0)), parent))
        el = self.J['elbow.' + side]
        p = el + Vector((pole[0], sy * pole[1], pole[2]))
        self.extra_bones.append(('pole.' + side, p, p + Vector((0, 0, 0.1)), 'chest'))
        self.ik.append(('forearm.' + side, 'grip.' + side, 'pole.' + side))

    def finalize_bones(self):
        if not (self.extra_bones or self.tip_rest is not None):
            return
        self.poser.clear()
        view = bpy.context.view_layer
        view.objects.active = self.arm
        for o in view.objects:
            o.select_set(False)
        self.arm.select_set(True)
        bpy.ops.object.mode_set(mode='EDIT')
        eb = self.arm.data.edit_bones
        for name, head, tail, parent in self.extra_bones:
            b = eb.new(name)
            b.head, b.tail = head, tail
            b.use_deform = False
            b.parent = eb[parent]
        if self.tip_rest is not None:
            b = eb.new('weapon_tip')
            b.head, b.tail = self.tip_rest, self.tip_rest + Vector((0, 0, 0.05))
            b.use_deform = False
            b.parent = eb[getattr(self, 'tip_bone', 'hand.R')]
        bpy.ops.object.mode_set(mode='OBJECT')
        for pb in self.arm.pose.bones:
            pb.rotation_mode = 'QUATERNION'
        # World-space moves for IK targets parented under posed bones.
        fixes = {}
        self.poser = Poser(self.arm)
        if self.stance:
            self.poser.apply(self.stance)
            bpy.context.view_layer.update()
            for name, _, _, parent in self.extra_bones:
                pp = self.arm.pose.bones[parent]
                delta = (pp.matrix.to_3x3() @ pp.bone.matrix_local.to_3x3().inverted())
                fixes[name] = delta.inverted()
            self.poser.clear()
        for bone, target, pole in self.ik:
            c = self.arm.pose.bones[bone].constraints.new('IK')
            c.target = self.arm
            c.subtarget = target
            c.pole_target = self.arm
            c.pole_subtarget = pole
            c.pole_angle = math.radians(-90)
            c.chain_count = 2
            c.use_stretch = False
        self.poser = Poser(self.arm)
        self.poser.world_fix = fixes

    def bind(self):
        self.poser.clear()
        bpy.context.view_layer.update()
        if self.body is not None:
            if self.body_skin is not None:
                self.body_skin(self.body, self.arm)
            else:
                body.skin_body(self.body, self.arm, self.s, self.J)
            if 'part_tags' in self.body:
                del self.body['part_tags']
        for obj, b in self.parts:
            if isinstance(b, str):
                rig.bind_rigid(obj, self.arm, b)
            elif isinstance(b, dict):
                if b.get('fn'):
                    rig.weight_fn(obj, self.arm, b['fn'])
                elif self.body is not None:
                    allowed = b.get('skin')

                    def restrict(co, wd, allowed=allowed):
                        if not allowed:
                            return wd
                        w = {k: v for k, v in wd.items() if k in allowed}
                        return w or {allowed[-1]: 1.0}
                    rig.copy_weights(obj, self.body, self.arm, blend=restrict)
                else:
                    rig.weight_single(obj, self.arm, (b.get('skin') or ['chest'])[0])

    # ---------------------------------------------------------- animation
    def key_clips(self, clips, release=None):
        """clips: name -> list of poses, or a death Performance. Keys from frame 1 in CLIPS order;
        clip lengths come from the poses, so a longer death shifts deploy later."""
        frame = 1
        meta = {}
        P = self.poser
        pending = []
        for name, count, duration, loop in anim.CLIPS:
            poses = clips[name]
            if isinstance(poses, Performance):
                meta[name] = {'start': frame - 1, 'count': poses.count, 'duration': poses.duration, 'loop': loop}
                pending.append((name, poses, frame))
                bpy.context.scene.timeline_markers.new(name, frame=frame)
                # Placeholder keys keep later clips' frame numbers; the solver replaces them below.
                for pose in poses.poses:
                    P.clear()
                    P.apply(pose)
                    P.key(frame)
                    frame += 1
                continue
            if name != 'death':
                assert len(poses) == count, (name, len(poses))
            meta[name] = {'start': frame - 1, 'count': len(poses), 'duration': duration, 'loop': loop}
            bpy.context.scene.timeline_markers.new(name, frame=frame)
            for pose in poses:
                P.clear()
                P.apply(pose)
                P.key(frame)
                frame += 1
        meta['attack']['contactFrame'] = anim.CONTACT
        total = frame - 1
        for fc in _fcurves(self.arm):
            for kp in fc.keyframe_points:
                kp.interpolation = 'CONSTANT'
        if release:
            # Nocked projectiles vanish on the release frame and reappear on recovery.
            for obj in release:
                for f, hidden in ((1, False), (14, False), (15, True), (18, True), (19, False), (total, False)):
                    obj.hide_render = hidden
                    obj.keyframe_insert('hide_render', frame=f)
        bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, total
        for name, perf, start in pending:
            meta[name].update(perf.key(self, start))
        self.clip_meta = meta
        self.total_frames = total
        return meta


def _fcurves(obj):
    ad = obj.animation_data
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


# ------------------------------------------------------------------ rendering
def light_rig(coll, center=Vector((0, 0, 1.1)), scale=1.0, warm='ffecd6', rim='aecbff', key_power=1150,
              rim_power=1100, fill_power=320):
    """Battle lighting: sun from the upper left and slightly behind (matching the game's projected
    ground shadows, which fall down-right), a soft camera-side fill so faces read, a cool back rim
    and a broad sky. Neutral-warm key, because the game adds its own per-zone tint."""
    c = center
    core.area_light('Key • sun upper left', c + Vector((-6.5, 1.0, 7.5)) * scale, c, key_power * scale ** 2, core.srgb(warm),
                    3.5 * scale, coll)
    core.area_light('Fill • camera side', c + Vector((5.5, -6.5, 2.5)) * scale, c, fill_power * scale ** 2,
                    core.srgb('fff1e2'), 6.0 * scale, coll)
    core.area_light('Rim • cool back right', c + Vector((4.5, 5.5, 4.5)) * scale, c, rim_power * scale ** 2, core.srgb(rim),
                    3.0 * scale, coll)
    core.area_light('Sky • soft top', c + Vector((0.5, -1.0, 8.0)) * scale, c, 520 * scale ** 2, core.srgb('dfe9ff'),
                    8.0 * scale, coll)


def mesh_objects(root):
    return [o for o in root.children_recursive if o.type == 'MESH' and not o.hide_render]


def frame_envelope(scene, cam, objs, frames, margin=0.92):
    env = 0.0
    dg = bpy.context.evaluated_depsgraph_get()
    for f in frames:
        scene.frame_set(f)
        dg.update()
        for o in objs:
            ev = o.evaluated_get(dg)
            if o.hide_render:
                continue
            mw = ev.matrix_world
            for corner in ev.bound_box:
                p = world_to_camera_view(scene, cam, mw @ Vector(corner))
                env = max(env, abs(p.x - .5) * 2 / margin, abs(p.y - .5) * 2 / margin)
    return env


def render_character(ch, out_dir, samples=96, portrait=True, cam_target_z=1.15, portrait_cfg=None,
                     body_z=None, contact_fallback=(1.12, 0, 1.1), profile='sword-cut', save_blend=None,
                     min_scale=3.6, frames=None, scale_override=None, res_scale=1.0):
    scene = bpy.context.scene
    core.setup_render(scene, samples)
    core.studio_world(scene, top='7a8a9c', bottom='3a3128', strength=0.75)
    rc = core.collection('Camera and lighting')
    target = Vector((0.12, 0, cam_target_z))
    cam = core.ortho_camera('Battle camera • 3/4 facing right', target + CAM_DIR * 30, target, 4.0, rc)
    scene.camera = cam
    scene.render.resolution_x, scene.render.resolution_y = FRAME_W, FRAME_H
    scene.render.fps = 12
    light_rig(rc, Vector((0, 0, 1.1)))
    total = getattr(ch, 'total_frames', 32)
    scene.frame_start, scene.frame_end = 1, total
    objs = [o for o in bpy.data.objects if o.type == 'MESH' and not o.name.startswith('_')]
    env = frame_envelope(scene, cam, objs, range(1, total + 1))
    scale = max(min_scale, 4.0 * env) if scale_override is None else scale_override
    cam.data.ortho_scale = scale
    draw_scale = 2.0 * scale / 3.5
    # Same framing, more pixels: anchors and motion points are normalised, so only the size changes.
    k = res_scale(draw_scale) if callable(res_scale) else res_scale
    scene.render.resolution_x, scene.render.resolution_y = round(FRAME_W * k), round(FRAME_H * k)
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    scene.frame_set(1)
    bpy.context.view_layer.update()
    ground = world_to_camera_view(scene, cam, Vector((0, 0, 0)))

    def projected(p):
        q = world_to_camera_view(scene, cam, p)
        return [round(q.x - ground.x, 6), round(ground.y - q.y, 6)]
    bz = body_z if body_z is not None else (ch.arm.matrix_world @ ch.arm.pose.bones['spine'].head).z if 'spine' in ch.arm.pose.bones else 1.2
    body_pt = projected(Vector((0, 0, bz)))
    scene.frame_set(15)
    bpy.context.view_layer.update()
    tip_arm = getattr(ch, 'tip_bone_owner', ch).arm
    if 'weapon_tip' in tip_arm.pose.bones:
        tip = tip_arm.matrix_world @ tip_arm.pose.bones['weapon_tip'].head
    else:
        tip = Vector(contact_fallback)
    contact_pt = projected(tip)
    death = ch.clip_meta.get('death', {})
    if '_impactWorld' in death:
        death['impactPoint'] = projected(Vector(death.pop('_impactWorld')))
    meta = {
        'frameWidth': scene.render.resolution_x, 'frameHeight': scene.render.resolution_y,
        'drawScale': draw_scale,
        'animations': ch.clip_meta,
        'assetId': ch.ident, 'title': ch.title,
        'source': f'art/remaster/units/{ch.ident}.py',
        'anchorY': 1 - ground.y, 'anchorX': round(ground.x, 6),
        'motion': {'revision': 'remaster-v1', 'profile': profile, 'body': body_pt, 'contact': contact_pt},
    }
    if save_blend:
        scene['asset_id'] = ch.ident
        scene['animations'] = json.dumps(ch.clip_meta)
        scene.frame_set(1)
        core.save_blend(save_blend)
    for f in (frames or range(1, total + 1)):
        scene.frame_set(f)
        scene.render.filepath = str(out / f'{f - 1:03}.png')
        bpy.ops.render.render(write_still=True)
    if portrait:
        render_portrait(ch, out / 'portrait.png', samples=max(samples, 128), **(portrait_cfg or {}))
    (out / 'metadata.json').write_text(json.dumps(meta, indent=2) + '\n')
    (out / 'complete.json').write_text(json.dumps({'id': ch.ident, 'frames': total, 'revision': 'remaster-v2'}, indent=2) + '\n')
    return meta


def render_portrait(ch, path, samples=128, target=None, distance=5.4, lens=85, frame=1, height=None, side=0.0,
                    elevation=0.1, drop=0.32):
    scene = bpy.context.scene
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    if target is None:
        hb = ch.arm.pose.bones.get('head')
        t = (ch.arm.matrix_world @ hb.head) if hb else Vector((0, 0, 1.4))
        target = Vector((t.x + 0.05, t.y, t.z - drop if height is None else height))
    target = Vector(target)
    d = Vector((0.86, -0.5 + side, elevation)).normalized()
    cam = core.persp_camera('Portrait camera', target + d * distance, target, lens, bpy.data.collections['Camera and lighting'])
    old = scene.camera
    scene.camera = cam
    rx, ry = scene.render.resolution_x, scene.render.resolution_y
    scene.render.resolution_x = scene.render.resolution_y = 512
    old_samples = scene.cycles.samples
    scene.cycles.samples = samples
    core.render_to(scene, path)
    scene.cycles.samples = old_samples
    scene.render.resolution_x, scene.render.resolution_y = rx, ry
    scene.camera = old
