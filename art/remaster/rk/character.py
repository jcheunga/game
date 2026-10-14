"""Character assembly, animation keying, framing and rendering to the game contract.

Contract:
  master frames 256x320 RGBA, clips in anim.CLIPS order: idle (6), walk (8), attack (10, contact on local
  frame anim.CONTACT), hit (2), the unit's death performance (12 frames, 16 for bosses; see death.py), deploy (4)
  and, for ranged units, a close-quarters melee clip (7, contact on anim.MELEE_CONTACT). Start frames are recorded
  per clip in the metadata ("animations"); never assume global frame numbers. Portrait 512x512.
  res_scale renders the same framing at a multiple of 256x320 so pack.py can reach its texel density
  (see density.py).
  metadata: drawScale = 2 * ortho / 3.5 (same world-to-screen scale as before),
  anchorX/anchorY ground projection, motion.body / motion.contact offsets.
"""
import json
import math
from pathlib import Path

import bpy
from mathutils import Euler, Matrix, Vector
from bpy_extras.object_utils import world_to_camera_view

from . import anim, body, core, geo, rig, hero, hands as HANDS, sdf as SDF, skin as SKIN, bones as BONES
import numpy as np
import os
from .death import Performance
from .rig import Poser

# The battle camera: orthographic, 22 degrees above the ground and looking straight along the field's depth
# axis, exactly like the battlefield, its structures and backdrops. The character stands turned 33.5 degrees
# toward it (FACING_YAW) for the 3/4 view, so faces and heraldry read while every fall, drop and corpse lands on the
# same ground plane the battle draws.
CAM_DIR = Vector((0.0, -math.cos(math.radians(22.0)), math.sin(math.radians(22.0))))
import os as _os
FACING_DEFAULT = math.radians(float(_os.environ.get('RK_FACING', '-45')))
FACING_YAW = FACING_DEFAULT


def set_facing(degrees=None):
    """Turn this unit toward the camera by its own angle (long-bodied mounts and engines read best nearer
    profile); None restores the roster default. Build scripts reset it before each recipe."""
    global FACING_YAW
    FACING_YAW = FACING_DEFAULT if degrees is None else math.radians(degrees)
FRAME_W, FRAME_H = 256, 320


class Character:
    def __init__(self, ident, title, spec=None, coll=None, extras=None, rig_kind='humanoid', skeleton_fn=None):
        self.ident = ident
        self.title = title
        self.bound = False
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
        self.parts = []
        self.body = None
        self.body_field = None
        self.tip_rest = None
        self.release_objs = []
        self.ik = []
        self.extra_bones = []
        self._extras = extras
        self.rig_kind = rig_kind
        if rig_kind == 'hero':
            self.s = hero.spec(**(spec or {}))
            self.J = hero.joints(self.s)
            self.arm, _ = hero.skeleton(ident, self.s, self.coll, extras=extras)
        else:
            self.s = body.spec(**(spec or {}))
            self.J = body.joints(self.s)
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

    def _hand_field(self, size, pose, side):
        thumb = -1 if side == 'R' else 1
        if getattr(self, 'bone_hands', False):
            return BONES.bone_hand_field(1.12 * self.s.get('hand', 1.0) * size, pose, thumb)
        return HANDS.hand_field(1.12 * self.s.get('hand', 1.0) * size, pose, thumb=thumb,
                                claws=getattr(self, 'claws', 0.0))

    def build_bones(self, mat, thick=1.45, **kw):
        """A skeleton for the body: the bones are the visible, skinned mesh; a thin human sculpt (hidden) is
        what garments and armour are grown from."""
        ghost = hero.spec(**dict(self.s, thin=max(0.6, self.s.get('thin', 0.0))))
        self.body_field = hero.body_field(ghost, self.J)
        bf = BONES.skeleton_field(self.s, self.J, thick=thick, **kw)
        self.body = bf.mesh(0.0045, self.coll, mat, name=self.ident + ' bones', smooth=2)
        self.body_skin = lambda obj, arm: hero.skin_from_field(obj, arm, bf, sigma=0.012, smooth=1)
        self.bone_hands = True
        self.visible = dict(field=bf, mat=mat, h=0.0045, smooth=2, sigma=0.012, name=self.ident + ' bones')
        return self.body

    def build_body(self, mats, **kw):
        if self.rig_kind == 'hero':
            mat = mats['skin'] if isinstance(mats, dict) else mats
            self.body, self.body_field = hero.build_body(self.ident + ' body', self.s, self.J, self.coll, mat, **kw)
            field = self.body_field
            self.body_skin = lambda obj, arm: hero.skin_from_field(obj, arm, field)
            self.visible = dict(field=field, mat=mat, h=kw.get('h', 0.006), smooth=6, sigma=0.03,
                                name=self.ident + ' body')
            return self.body
        self.body = body.build_body(self.ident + ' body', self.s, self.J, self.coll, mats, **kw)
        return self.body

    # ---------------------------------------------------------- dressing in the stance
    JOINT_BONES = {'pelvis': ('hips', 0), 'spine': ('spine', 0), 'chest': ('chest', 0), 'neck': ('neck', 0),
                   'head': ('head', 0), 'head_top': ('head', 1), 'root': ('root', 0)}

    def bone_mats(self):
        """Armature-space deformation of every bone in the current pose (pose @ rest^-1), numpy 4x4."""
        bpy.context.view_layer.update()
        out = {}
        for pb in self.arm.pose.bones:
            M = pb.matrix @ pb.bone.matrix_local.inverted()
            out[pb.name] = np.array(M)
        return out

    def dress(self):
        """Grow garments around the body as it stands in its stance: posed joints and a posed body sculpt.
        Call after set_stance() and build_body(); garb.Kit and plate.Smith then work in the stance."""
        self.stance_mats = self.bone_mats()
        pb = self.arm.pose.bones
        Jp = {}
        for key, (bone, end) in self.JOINT_BONES.items():
            if bone in pb:
                Jp[key] = (pb[bone].tail if end else pb[bone].head).copy()
        for side in ('R', 'L'):
            for key, bone, end in (('clav', 'shoulder', 0), ('shoulder', 'upper_arm', 0), ('elbow', 'forearm', 0),
                                   ('wrist', 'hand', 0), ('hand_tip', 'hand', 1), ('hip', 'thigh', 0),
                                   ('knee', 'shin', 0), ('ankle', 'foot', 0), ('toe', 'foot', 1)):
                b = pb[bone + '.' + side]
                Jp[key + '.' + side] = (b.tail if end else b.head).copy()
        self.Jp = Jp
        self.posed_field = self.body_field.posed(self.stance_mats)
        self.posed = True
        vis = getattr(self, 'visible', None)
        if vis is not None and self.body is not None:
            # re-mesh the body in the stance so it matches its garments exactly; bind() carries it back to rest
            bpy.data.objects.remove(self.body, do_unlink=True)
            pf = vis['field'].posed(self.stance_mats)
            self.body = pf.mesh(vis['h'], self.coll, vis['mat'], name=vis['name'], smooth=vis['smooth'])
            self.visible_posed = pf
            self.body_skin = None
        return self

    def grip(self, wd_res, side='R', mat=None, pose='grip', size=1.0, toward_tip=True, at=None, glove=None):
        """A hand closed around a held weapon (result of hold()) or around a grip2 point (two-handed).
        at: 'grip2' wraps the off hand at the weapon's second grip."""
        conv_bone = 'hand.' + side
        _, posed = self._conv(conv_bone)
        wrist = posed @ Vector((0, 0, 0))
        stance_M = wd_res['stance_matrix']
        axis = wd_res['axis_world']
        if at == 'grip2' and wd_res.get('grip2') is not None:
            center = stance_M @ wd_res['grip2']
        else:
            center = wd_res['grip_world']
        f, gl = self._hand_field(size, pose, side)
        if side in getattr(self, 'hand_frames', {}):
            M = self.hand_frames[side]
        else:
            M, _ = HANDS.grip_frame(side, axis, center, wrist, gl, toward_tip)
        obj = HANDS.build(f, mat, self.coll, M, name='Hand.' + side)
        if glove is not None:
            obj.data.materials.clear()
            obj.data.materials.append(glove)
        conv_h, _ = self._conv(conv_bone)
        geo.transform(obj, conv_h)
        self.parts.append((obj, conv_bone))
        return obj

    def grip2(self, wd_res, mat, side='L', pose='grip', size=1.0, pole=(-0.3, 0.25, -0.3), toward_tip=True):
        """Off hand on a two-handed weapon: the hand closes on the weapon's second grip and rides the
        IK target bone, which sits at the hand's wrist so the forearm meets it."""
        main = wd_res.get('bone', 'hand.R')
        conv_m, _ = self._conv(main)
        stance_M = wd_res['stance_matrix']
        center = stance_M @ wd_res['grip2']
        axis = wd_res['axis_world']
        _, posed = self._conv('hand.' + side)
        wrist_now = posed @ Vector((0, 0, 0))
        f, gl = self._hand_field(size, pose, side)
        if side in getattr(self, 'hand_frames', {}):
            M = self.hand_frames[side]
        else:
            M, _ = HANDS.grip_frame(side, axis, center, wrist_now, gl, toward_tip)
        obj = HANDS.build(f, mat, self.coll, M, name='Hand.' + side)
        geo.transform(obj, conv_m)
        wrist_rest = conv_m @ M.translation
        self.two_hand(wrist_rest, parent=main, side=side, pole=pole)
        self.parts.append((obj, 'grip.' + side))
        return obj

    def rest_hand(self, side, mat, pose='relaxed', size=1.0, turn=0.0):
        """A hand on its bone in the rest pose (fists, open casting hands, claws)."""
        f, _ = self._hand_field(size, pose, side)
        if side in getattr(self, 'hand_frames', {}):
            # a solved free hand: build at the stance frame, then carry back to the rest pose
            M = self.hand_frames[side]
            obj = HANDS.build(f, mat, self.coll, M, name='Hand.' + side)
            conv_h, _ = self._conv('hand.' + side)
            geo.transform(obj, conv_h)
        else:
            M, _ = HANDS.rest_frame(self.J, side, turn)
            obj = HANDS.build(f, mat, self.coll, M, name='Hand.' + side)
        self.parts.append((obj, 'hand.' + side))
        return obj

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
        res['bone'] = bone
        res['grip_world'] = center.copy()      # stance space: where the hand closes
        res['axis_world'] = zw.normalized()
        res['stance_matrix'] = M
        if wd.get('tip') is not None:
            res['tip_rest'] = Mr @ wd['tip']
            if track_tip:
                self.tip_rest = res['tip_rest']
                self.tip_bone = bone
        if wd.get('grip2') is not None:
            res['grip2_rest'] = Mr @ wd['grip2']
        return res

    def hold_at(self, wd, M, side='R', track_tip=True, bone=None, scale=1.0):
        """Place a weapon by its stance-space matrix (rk.pose.weapon_frame) on a hand bone, optionally scaled
        about its grip."""
        bone = bone or 'hand.' + side
        conv, _ = self._conv(bone)
        if scale != 1.0:
            M = M @ Matrix.Scale(scale, 4)
        Mr = conv @ M
        for o in wd['objs']:
            geo.transform(o, Mr)
            self.parts.append((o, bone))
        self.add_prop('weapon' if side == 'R' else 'offhand', wd['objs'], bone)
        res = dict(wd)
        res.update(matrix=Mr, bone=bone, stance_matrix=M, axis_world=M.col[2].xyz.normalized(),
                   grip_world=M.translation.copy())
        if wd.get('tip') is not None:
            res['tip_rest'] = Mr @ wd['tip']
            if track_tip:
                self.tip_rest = res['tip_rest']
                self.tip_bone = bone
        if wd.get('grip2') is not None:
            res['grip2_rest'] = Mr @ wd['grip2']
        return res

    def strap_at(self, wd, M, side='L', bone=None, prop='shield'):
        """A shield (or any carried piece) by its stance-space matrix, rigid on the forearm."""
        bone = bone or 'forearm.' + side
        conv, _ = self._conv(bone)
        Mr = conv @ M
        for o in wd['objs']:
            geo.transform(o, Mr)
            self.parts.append((o, bone))
        self.add_prop(prop, wd['objs'], bone)
        return Mr

    def draw_hand(self, bow_res, mat, side='R', pose='grip', pole=(-0.6, -0.3, 1.6)):
        """The draw hand on a bow: an IK target off the bow hand at the solved wrist, carrying the hand mesh
        (and the nocked arrow); the attack clip pulls and releases it."""
        bow_bone = bow_res.get('bone', 'hand.L')
        conv_b, _ = self._conv(bow_bone)
        M = self.hand_frames[side]
        f, _ = self._hand_field(1.0, pose, side)
        obj = HANDS.build(f, mat, self.coll, M, name='Hand.' + side)
        geo.transform(obj, conv_b)
        wrist_rest = conv_b @ M.translation
        self.two_hand(wrist_rest, parent=bow_bone, side=side, pole=pole)
        self.parts.append((obj, 'grip.' + side))
        self.draw_frame = M
        return obj

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

    def _bind_posed(self, obj, field, sigma=0.03, adjust=None, smooth=2):
        V = SDF.verts(obj)
        W = field.weights(V, sigma)
        if adjust:
            W = adjust(V, W)
        quads = SDF.quads(obj) if len(obj.data.loops) == 4 * len(obj.data.polygons) else None
        bones, Wq = SDF.write_weights(obj, W, quads, smooth=smooth)
        rest = SKIN.unpose(V, Wq, bones, self.stance_mats)
        obj.data.vertices.foreach_set('co', rest.astype(np.float32).ravel())
        obj.data.update()
        rig._armature_mod(obj, self.arm)

    def mask_body(self, margin=0.004):
        """Delete body faces hidden inside solid garments in the stance (no poke-through at bent joints)."""
        import bmesh
        body = self.body
        Vp = SDF.verts(body)   # the body is meshed in the stance (Character.dress)
        inside = np.zeros(len(Vp), bool)
        for obj, b in self.parts:
            if isinstance(b, dict) and b.get('posed') and b.get('sdf') and obj.name in SDF.FIELDS:
                inside |= SDF.FIELDS[obj.name].eval(Vp) < -margin
        if not inside.any():
            return 0
        bm = bmesh.new()
        bm.from_mesh(body.data)
        bm.verts.ensure_lookup_table()
        kill = [f for f in bm.faces if all(inside[v.index] for v in f.verts)]
        bmesh.ops.delete(bm, geom=kill, context='FACES_ONLY')
        loose = [v for v in bm.verts if not v.link_faces]
        bmesh.ops.delete(bm, geom=loose, context='VERTS')
        bm.to_mesh(body.data)
        bm.free()
        body.data.update()
        geo.store_rest(body)
        return len(kill)

    def bind(self):
        self.bound = True
        self.poser.clear()
        bpy.context.view_layer.update()
        posed_body = getattr(self, 'visible_posed', None) is not None
        if posed_body and not getattr(self, 'bone_hands', False):
            self.mask_body()
        if posed_body:
            self._bind_posed(self.body, self.visible_posed, self.visible['sigma'])
        elif self.body is not None:
            if self.body_skin is not None:
                self.body_skin(self.body, self.arm)
            else:
                body.skin_body(self.body, self.arm, self.s, self.J)
            if 'part_tags' in self.body:
                del self.body['part_tags']
        for obj, b in self.parts:
            if isinstance(b, str):
                rig.bind_rigid(obj, self.arm, b)
            elif isinstance(b, dict) and b.get('sdf'):
                V = SDF.verts(obj)
                posed = b.get('posed')
                field = self.posed_field if posed else self.body_field
                W = field.weights(V, b.get('sigma') or 0.03)
                if b.get('adjust'):
                    W = b['adjust'](V, W)
                quads = SDF.quads(obj) if len(obj.data.loops) == 4 * len(obj.data.polygons) else None
                bones, Wq = SDF.write_weights(obj, W, quads, smooth=b.get('smooth', 2))
                if posed:
                    rest = SKIN.unpose(V, Wq, bones, self.stance_mats)
                    obj.data.vertices.foreach_set('co', rest.astype(np.float32).ravel())
                    obj.data.update()
                rig._armature_mod(obj, self.arm)
            elif isinstance(b, dict) and b.get('fieldw') is not None:
                V = SDF.verts(obj)
                W = b['fieldw'].weights(V, b.get('sigma') or 0.04)
                if b.get('restrict'):
                    W = {k: v for k, v in W.items() if k in b['restrict']} or {b['restrict'][0]: np.ones(len(V))}
                quads = SDF.quads(obj) if len(obj.data.loops) == 4 * len(obj.data.polygons) else None
                SDF.write_weights(obj, W, quads, smooth=2)
                rig._armature_mod(obj, self.arm)
            elif isinstance(b, dict) and b.get('rigid_posed'):
                bone = b['rigid_posed']
                M = np.linalg.inv(self.stance_mats[bone])
                V = SDF.verts(obj)
                rest = V @ M[:3, :3].T + M[:3, 3]
                obj.data.vertices.foreach_set('co', rest.astype(np.float32).ravel())
                obj.data.update()
                rig.bind_rigid(obj, self.arm, bone)
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
            if name not in clips and name in anim.OPTIONAL_CLIPS:
                continue
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
        if 'melee' in meta:
            meta['melee']['contactFrame'] = anim.MELEE_CONTACT
        total = frame - 1
        for fc in _fcurves(self.arm):
            for kp in fc.keyframe_points:
                kp.interpolation = 'CONSTANT'
        if release:
            # Nocked projectiles vanish on the release frame and reappear on recovery.
            for obj in release:
                c = meta['attack']['start'] + 1 + anim.CONTACT
                for f, hidden in ((1, False), (c - 1, False), (c, True), (c + 3, True), (c + 4, False), (total, False)):
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
def light_rig(coll, center=Vector((0, 0, 1.1)), scale=1.0, warm='fff0dc', rim='e9f0ff', key_power=None,
              rim_power=None, fill_power=None):
    """Roster-sheet studio lighting, agreeing with the battle sun (upper left; the game projects ground
    shadows down-right): a broad warm key from the camera's upper left, a cool fill from the right, a bright
    rim from behind on the right that outlines the figure, a soft sky and a warm bounce from the ground.
    Kept near-neutral because the game tints each zone."""
    c = center
    k = scale ** 2
    env = lambda key, d: float(os.environ.get(key, d))
    key_power = key_power or env('RK_KEY', 1550)
    rim_power = rim_power or env('RK_RIM', 1100)
    fill_power = fill_power or env('RK_FILL', 55)
    core.area_light('Key • upper left, camera side', c + Vector((-4.6, -5.4, 6.4)) * scale, c, key_power * k,
                    core.srgb(warm), 4.2 * scale, coll)
    core.area_light('Fill • camera right', c + Vector((5.5, -6.0, 1.4)) * scale, c, fill_power * k,
                    core.srgb('dbe6ff'), 7.0 * scale, coll)
    core.area_light('Rim • back right', c + Vector((4.2, 6.2, 4.8)) * scale, c, rim_power * k, core.srgb(rim),
                    2.6 * scale, coll)
    core.area_light('Rim • back left', c + Vector((-5.0, 5.0, 3.0)) * scale, c, rim_power * 0.35 * k,
                    core.srgb('ffe2bc'), 3.0 * scale, coll)
    core.area_light('Sky • soft top', c + Vector((0.5, -1.0, 8.0)) * scale, c, env('RK_SKY', 120) * k, core.srgb('e4ecff'),
                    8.0 * scale, coll)
    core.area_light('Bounce • warm ground', c + Vector((1.0, -5.0, -3.5)) * scale, c, 160 * k, core.srgb('ffcf9a'),
                    6.0 * scale, coll)


def mesh_objects(root):
    return [o for o in root.children_recursive if o.type == 'MESH' and not o.hide_render]


def world_for(scene):
    """The studio environment every unit renders in: a dim neutral dome for soft ambient light, and the
    roster sheet's exposure and contrast."""
    scene.view_settings.view_transform = os.environ.get('RK_VIEW', 'Khronos PBR Neutral')
    scene.view_settings.exposure = float(os.environ.get('RK_EXPOSURE', '0.0'))
    try:
        scene.view_settings.look = 'None'
    except TypeError:
        pass
    return core.studio_world(scene, top='8a96a6', bottom='4a3e32', strength=float(os.environ.get('RK_AMBIENT', '0.15')),
                             reflect=4.5, band='fff4e2')


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
                     min_scale=3.6, frames=None, scale_override=None, res_scale=1.0, paint=None, back_guide=False):
    scene = bpy.context.scene
    core.setup_render(scene, samples)
    world_for(scene)
    turn_toward_camera(scene)
    rc = core.collection('Camera and lighting')
    target = Vector((0.12, 0, cam_target_z))
    cam = core.ortho_camera('Battle camera • 3/4 facing right', target + CAM_DIR * 30, target, 4.0, rc)
    scene.camera = cam
    scene.render.resolution_x, scene.render.resolution_y = FRAME_W, FRAME_H
    scene.render.fps = 12
    light_rig(rc, Vector((0, 0, 1.1)))
    total = ch.total_frames
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
    if back_guide:
        # the stance seen from behind, under the same camera and lights (the character turned 180 degrees): the
        # guide for the back painting (art/remaster/paintover.py back), which covers what the front cannot see
        root = bpy.data.objects['_Facing']
        root.rotation_euler.z += math.pi
        scene.frame_set(1)
        bpy.context.view_layer.update()
        core.render_to(scene, out / 'back000.png')
        root.rotation_euler.z -= math.pi
        bpy.context.view_layer.update()
        return {'back_guide': str(out / 'back000.png'), 'drawScale': draw_scale,
                'frameWidth': scene.render.resolution_x, 'frameHeight': scene.render.resolution_y}
    if paint:
        # the roster-sheet paintings of this unit, projected at the stance (rk/paint.py): the front from this camera,
        # the back (when painted) from the same camera turned 180 degrees about the character
        from . import paint as PT
        PT.project(scene, cam, paint, back_cam=PT.back_camera(cam, rc) if PT.has_back(paint) else None)
    scene.frame_set(1)
    bpy.context.view_layer.update()
    ground = world_to_camera_view(scene, cam, Vector((0, 0, 0)))

    def projected(p):
        q = world_to_camera_view(scene, cam, p)
        return [round(q.x - ground.x, 6), round(ground.y - q.y, 6)]
    bz = body_z if body_z is not None else (ch.arm.matrix_world @ ch.arm.pose.bones['spine'].head).z if 'spine' in ch.arm.pose.bones else 1.2
    body_pt = projected(Vector((0, 0, bz)))
    scene.frame_set(ch.clip_meta['attack']['start'] + 1 + anim.CONTACT)
    bpy.context.view_layer.update()
    tip_arm = getattr(ch, 'tip_bone_owner', ch).arm
    if 'weapon_tip' in tip_arm.pose.bones:
        tip = tip_arm.matrix_world @ tip_arm.pose.bones['weapon_tip'].head
    else:
        tip = Matrix.Rotation(FACING_YAW, 3, 'Z') @ Vector(contact_fallback)
    contact_pt = projected(tip)
    death = ch.clip_meta.get('death', {})
    if '_impactWorld' in death:
        # the solver works before the turn toward the camera
        death['impactPoint'] = projected(Matrix.Rotation(FACING_YAW, 3, 'Z') @ Vector(death.pop('_impactWorld')))
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
        # the roster-sheet icon (bust or whole figure); the legacy portrait camera remains for reference renders
        if render_icon(ch, out / 'portrait.png', samples=max(samples, 128)) is None:
            render_portrait(ch, out / 'portrait.png', samples=max(samples, 128), **(portrait_cfg or {}))
    (out / 'metadata.json').write_text(json.dumps(meta, indent=2) + '\n')
    (out / 'complete.json').write_text(json.dumps({'id': ch.ident, 'frames': total, 'revision': 'remaster-v2'}, indent=2) + '\n')
    return meta


def turn_toward_camera(scene):
    """Parent everything the character is made of to one root turned FACING_YAW about the ground's vertical, so the
    animation, ground contact, death physics and world-locked riders keep their relationships exactly."""
    root = bpy.data.objects.get('_Facing')
    if root is not None:
        return root
    root = bpy.data.objects.new('_Facing', None)
    scene.collection.objects.link(root)
    for obj in list(scene.objects):
        if obj is root or obj.parent is not None or obj.type in ('CAMERA', 'LIGHT'):
            continue
        obj.parent = root
    root.rotation_euler = (0.0, 0.0, FACING_YAW)
    bpy.context.view_layer.update()
    return root


def render_portrait(ch, path, samples=128, target=None, distance=5.4, lens=85, frame=1, height=None, side=0.0,
                    elevation=0.1, drop=0.32):
    scene = bpy.context.scene
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    turn = Matrix.Rotation(FACING_YAW, 3, 'Z')
    if target is None:
        hb = ch.arm.pose.bones.get('head')
        t = (ch.arm.matrix_world @ hb.head) if hb else Vector((0, 0, 1.4))
        target = Vector((t.x + 0.05, t.y, t.z - drop if height is None else height))
    else:
        # Recipes give targets in the character's own frame, before the turn toward the camera.
        target = turn @ Vector(target)
    d = (turn @ Vector((0.86, -0.5 + side, elevation))).normalized()
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


def render_icon(ch, path, size=1024, samples=128, mode=None, frame=1, turn_deg=16.0, elevation=9.0, rim_boost=1.5,
                pad=1.1, fill=420.0):
    """Unit icon: a three-quarter front bust (humanoids) or the whole figure (beasts, mounts, machines),
    framed from the posed geometry, transparent background."""
    from bpy_extras.object_utils import world_to_camera_view
    scene = bpy.context.scene
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    turn = Matrix.Rotation(FACING_YAW, 3, 'Z')
    hero_rig = getattr(ch, 'rig_kind', None) == 'hero'
    mode = mode or ('bust' if hero_rig else 'full')
    dg = bpy.context.evaluated_depsgraph_get()
    pts = []
    body_names = ('body', 'Head', 'Hand', 'bones', 'Skull', 'Mandible', 'Hair', 'Beard', 'Helm', 'Hood', 'Cuirass',
                  'Pauldron', 'Gambeson', 'Tabard', 'Kettle', 'Great helm', 'Armet', 'Cap', 'Crown', 'Bandana', 'Scarf',
                  'Mitre', 'Collar', 'Shirt', 'Cape')
    z_cut = None
    if mode == 'bust':
        # head and shoulders down to the lower chest: the face carries the card at small sizes
        pb = ch.arm.pose.bones
        z_cut = (ch.arm.matrix_world @ pb['chest'].head).z - 0.12 if 'chest' in pb else None
    for o in bpy.data.objects:
        if o.type != 'MESH' or o.hide_render or o.name.startswith('_'):
            continue
        if mode == 'bust' and not (o.name.startswith(body_names) or ' body' in o.name or ' bones' in o.name):
            continue
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        mw = ev.matrix_world
        step = max(1, len(me.vertices) // 1500)
        verts = me.vertices
        for i in range(0, len(verts), step):
            p = mw @ verts[i].co
            if z_cut is None or p.z > z_cut:
                pts.append(p)
        ev.to_mesh_clear()
    if not pts:
        return None
    # Orthographic, from the battle camera's side turned a little toward the unit's front: faces catch the
    # key light, mounts and engines show their length. Framed exactly from the posed geometry.
    a, e = math.radians(turn_deg), math.radians(elevation)
    d = Vector((math.sin(a), -math.cos(a), math.tan(e))).normalized()
    fwd = -d
    right = fwd.cross(Vector((0, 0, 1))).normalized()
    up = right.cross(fwd).normalized()
    xs = [p.dot(right) for p in pts]
    ys = [p.dot(up) for p in pts]
    zs = [p.dot(d) for p in pts]
    cx, cy = (min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2
    span = max(max(xs) - min(xs), max(ys) - min(ys)) * pad
    center = right * cx + up * cy + d * (sum(zs) / len(zs))
    rc = bpy.data.collections.get('Camera and lighting') or core.collection('Camera and lighting')
    data = bpy.data.cameras.new('Icon camera')
    data.type = 'ORTHO'
    data.ortho_scale = span
    data.clip_start, data.clip_end = 0.05, 200
    cam = bpy.data.objects.new('Icon camera', data)
    rc.objects.link(cam)
    cam.location = center + d * 30.0
    cam.rotation_euler = fwd.to_track_quat('-Z', 'Y').to_euler()
    fill_light = core.area_light('Icon fill', center + d * 8.0 + up * 2.5 - right * 1.5, center, fill,
                                 core.srgb('fff3e4'), 5.0, rc)
    old_cam = scene.camera
    scene.camera = cam
    rx, ry = scene.render.resolution_x, scene.render.resolution_y
    scene.render.resolution_x = scene.render.resolution_y = size
    bpy.context.view_layer.update()
    lights = [o for o in rc.objects if o.type == 'LIGHT' and o.name.startswith('Rim')]
    saved = [(l, l.data.energy) for l in lights]
    for l in lights:
        l.data.energy *= rim_boost
    old_samples = scene.cycles.samples
    scene.cycles.samples = samples
    core.render_to(scene, path)
    scene.cycles.samples = old_samples
    for l, e_ in saved:
        l.data.energy = e_
    scene.render.resolution_x, scene.render.resolution_y = rx, ry
    scene.camera = old_cam
    bpy.data.objects.remove(cam, do_unlink=True)
    bpy.data.objects.remove(fill_light, do_unlink=True)
    return path
