"""2D puppet units: a Codex painting in the roster sheet's style (output/units.png), rigged with bones placed from its
landmarks, animated per clip and rendered to the frames, metadata and icon the game loads.

blender --background --factory-startup --python-exit-code 1 --python art/remaster/puppet/build.py -- \
    --ids player_spear [--frames 1,20]

Reads  artifacts/remaster/puppet/<id>/master.png (RGBA, transparent background, facing right) and landmarks.json
       (puppet/landmarks.py), plus the unit's clip contract from artifacts/remaster/units/<id>/metadata.json (clip
       lengths, timings, contact and impact frames, motion profile), so gameplay timing is unchanged.
Writes artifacts/remaster/puppet_units/<id>/NNN.png, metadata.json, complete.json, portrait.png; pack them with
       python3 art/remaster/pack.py stage --source puppet_units.

The painting becomes a fine grid mesh (its texture is the painting itself), weighted to a 2D skeleton: each bone
pulls the part of the painting nearest to it, scaled by how thick that part is. Motions are rotations of those bones
about the view axis (positive = counter-clockwise on screen), chosen by the unit's motion profile.
"""
import json
import math
import sys
import time
import traceback
from pathlib import Path

import bpy
import numpy as np
from mathutils import Matrix, Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(HERE.parent))
sys.path.insert(0, str(HERE))
import density  # noqa: E402  (art/remaster/density.py: pure Python)
REVIEW = ROOT / 'artifacts/remaster'
PUPPET = REVIEW / 'puppet'
OUT = REVIEW / 'puppet_units'
PPU = 100.0   # painting pixels per Blender unit
DEBUG = False
STEP = 4      # mesh grid step in painting pixels


def args():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    import argparse
    ap = argparse.ArgumentParser()
    ap.add_argument('--ids', required=True)
    ap.add_argument('--frames', default=None, help='only these 1-based frames (preview)')
    ap.add_argument('--out', default=str(OUT))
    return ap.parse_args(argv)


# ------------------------------------------------------------------ scene
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in {e.identifier for e in
                                                               bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items} \
        else 'BLENDER_EEVEE_NEXT'
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.look = 'None'
    sc.view_settings.exposure = 0.0
    sc.view_settings.gamma = 1.0
    try:
        sc.eevee.taa_render_samples = 16
    except AttributeError:
        pass
    sc.render.fps = 12
    return sc


def load_rgba(path):
    img = bpy.data.images.load(str(path), check_existing=False)
    w, h = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1]   # rows top-down
    return img, px


# ------------------------------------------------------------------ geometry
class Paint:
    """The painting's frame: pixel coords (x right, y down) <-> Blender (X right, Z up, ground at 0)."""

    def __init__(self, alpha, lm):
        self.alpha = alpha
        self.h, self.w = alpha.shape
        ys, xs = np.nonzero(alpha > 0.5)
        self.x0, self.x1, self.y0, self.y1 = xs.min(), xs.max(), ys.min(), ys.max()
        feet = [lm[k] for k in ('ankle_R', 'ankle_L', 'toe_R', 'toe_L', 'fl_hoof', 'fr_hoof', 'hl_hoof', 'hr_hoof',
                                'fl_paw', 'fr_paw', 'hl_paw', 'hr_paw', 'wheel_front', 'wheel_back') if lm.get(k)]
        if feet:
            fx = [p[0] for p in feet]
            self.gx = float(np.mean(fx))
            lo, hi = int(max(0, min(fx) - 40)), int(min(self.w, max(fx) + 40))
            col = alpha[:, lo:hi] > 0.5
            rows = np.nonzero(col.any(axis=1))[0]
            self.gy = float(rows.max()) if len(rows) else float(self.y1)
        else:
            self.gx, self.gy = float((self.x0 + self.x1) / 2), float(self.y1)

    def to3(self, p):
        return Vector(((p[0] - self.gx) / PPU, 0.0, (self.gy - p[1]) / PPU))

    @property
    def fig_h(self):
        return (self.gy - self.y0) / PPU

    @property
    def fig_w(self):
        return (self.x1 - self.x0) / PPU


def build_mesh(paint, img, alpha, name='Puppet', depth=0.0):
    """A grid mesh over one layer's paint (its texture is the layer itself), `depth` toward the camera: EEVEE sorts
    blended objects by their origins, so each layer's object sits a little nearer than the one it draws over."""
    a = alpha
    h, w = a.shape
    # cells that touch the figure (dilated by one cell so edges never clip)
    gh, gw = h // STEP, w // STEP
    cell = a[:gh * STEP, :gw * STEP].reshape(gh, STEP, gw, STEP).max(axis=(1, 3)) > 0.02
    pad = np.zeros_like(cell)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            pad |= np.roll(np.roll(cell, dy, 0), dx, 1)
    cy, cx = np.nonzero(pad)
    vid = -np.ones((gh + 1, gw + 1), dtype=np.int64)
    corners = set()
    for y, x in zip(cy, cx):
        corners.update(((y, x), (y + 1, x), (y, x + 1), (y + 1, x + 1)))
    corners = sorted(corners)
    for i, (y, x) in enumerate(corners):
        vid[y, x] = i
    verts = [((x * STEP - paint.gx) / PPU, 0.0, (paint.gy - y * STEP) / PPU) for y, x in corners]
    faces = [(vid[y, x], vid[y + 1, x], vid[y + 1, x + 1], vid[y, x + 1]) for y, x in zip(cy, cx)]
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.update()
    uv = me.uv_layers.new(name='UV')
    loop_v = np.empty(len(me.loops), dtype=np.int64)
    me.loops.foreach_get('vertex_index', loop_v)
    pix = np.array([(x * STEP, y * STEP) for y, x in corners], dtype=np.float32)
    uvs = np.stack([pix[loop_v, 0] / w, 1.0 - pix[loop_v, 1] / h], axis=1)
    uv.data.foreach_set('uv', uvs.astype(np.float32).ravel())
    ob = bpy.data.objects.new(name, me)
    ob.location = (0.0, -depth, 0.0)
    bpy.context.scene.collection.objects.link(ob)
    # the painting itself, unlit
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.image = img
    tex.interpolation = 'Cubic'
    tex.extension = 'CLIP'
    em = nt.nodes.new('ShaderNodeEmission')
    tr = nt.nodes.new('ShaderNodeBsdfTransparent')
    mix = nt.nodes.new('ShaderNodeMixShader')
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(tex.outputs['Color'], em.inputs['Color'])
    nt.links.new(tex.outputs['Alpha'], mix.inputs['Fac'])
    nt.links.new(tr.outputs['BSDF'], mix.inputs[1])
    nt.links.new(em.outputs['Emission'], mix.inputs[2])
    nt.links.new(mix.outputs['Shader'], out.inputs['Surface'])
    for attr, val in (('surface_render_method', 'BLENDED'), ('blend_method', 'BLEND')):
        try:
            setattr(mat, attr, val)
        except (AttributeError, TypeError):
            pass
    me.materials.append(mat)
    return ob, pix


# ------------------------------------------------------------------ skeleton
def P(lm, k):
    v = lm.get(k)
    return None if v is None else np.array(v, dtype=np.float64)


def humanoid_bones(lm):
    """(name, head_px, tail_px, parent, radius_factor, deform) for a standing figure."""
    g = lambda k: P(lm, k)
    pel, chest, neck, chin, top = g('pelvis'), g('chest'), g('neck'), g('chin'), g('head_top')
    hips_tail = pel + np.array([0.0, 30.0])
    B = [('root', None, None, None, 0, False),
         ('hips', pel, hips_tail, 'root', 0.42, True),
         ('spine', pel, chest, 'hips', 0.45, True),
         ('chest', chest, neck, 'spine', 0.45, True),
         ('neck', neck, chin, 'chest', 0.2, True),
         ('head', chin, top, 'neck', 0.36, True)]
    for s in ('R', 'L'):
        sh, el, wr = g('shoulder_' + s), g('elbow_' + s), g('wrist_' + s)
        if sh is None or el is None or wr is None:
            continue
        hand = wr + (wr - el) * 0.35
        # the shoulder's armour and cloth stay with the chest while the arm turns under them
        B += [('shoulder.' + s, sh, sh + np.array([0.0, -40.0]), 'chest', 0.16, True),
              ('upper_arm.' + s, sh, el, 'chest', 0.22, True),
              ('forearm.' + s, el, wr, 'upper_arm.' + s, 0.2, True),
              ('hand.' + s, wr, hand, 'forearm.' + s, 0.17, True)]
        hp, kn, an, toe = g('hip_' + s), g('knee_' + s), g('ankle_' + s), g('toe_' + s)
        if hp is not None and kn is not None and an is not None:
            B += [('thigh.' + s, hp, kn, 'hips', 0.26, True), ('shin.' + s, kn, an, 'thigh.' + s, 0.21, True)]
            if toe is not None:
                B += [('foot.' + s, an, toe, 'shin.' + s, 0.17, True)]
    return B


def weapon_side(lm):
    grip = P(lm, 'weapon_grip')
    if grip is None:
        return None
    best = None
    for s in ('R', 'L'):
        w = P(lm, 'wrist_' + s)
        if w is not None:
            d = np.linalg.norm(w - grip)
            if best is None or d < best[0]:
                best = (d, s)
    return best[1] if best else 'R'


def build_armature(paint, bones, extra=()):
    arm_data = bpy.data.armatures.new('Rig')
    arm = bpy.data.objects.new('Rig', arm_data)
    bpy.context.scene.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    eb = arm_data.edit_bones
    for name, head, tail, parent, _, deform in list(bones) + list(extra):
        b = eb.new(name)
        if head is None:
            b.head, b.tail = Vector((0, 0, 0)), Vector((0, 0, 0.3))
        else:
            h3, t3 = paint.to3(head), paint.to3(tail)
            if (t3 - h3).length < 1e-3:
                t3 = h3 + Vector((0, 0, 0.05))
            b.head, b.tail = h3, t3
        b.align_roll(Vector((0, -1, 0)))      # local Z toward the camera: rotation about Z is in the picture plane
        b.use_deform = deform
        if parent:
            b.parent = eb[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    for pb in arm.pose.bones:
        pb.rotation_mode = 'XYZ'
    return arm


def seg_dist(P_, a, b):
    ab = b - a
    t = np.clip(((P_ - a) @ ab) / max(1e-9, ab @ ab), 0, 1)
    return np.linalg.norm(P_ - (a + t[:, None] * ab), axis=1)


def skin(ob, arm, pix, bones, torso_len, allowed=None, sharp=4.0):
    """Weights by distance to each bone, scaled by how thick that part is. `allowed` limits a layer to its own bones
    (an arm layer never follows the hips; a weapon follows only its bone)."""
    deform = [(n, h, t, r) for n, h, t, _, r, d in bones if d and h is not None and (allowed is None or n in allowed)]
    if len(deform) == 1:
        vg = ob.vertex_groups.new(name=deform[0][0])
        vg.add(list(range(len(pix))), 1.0, 'REPLACE')
    else:
        D = np.stack([seg_dist(pix, h, t) / max(6.0, r * torso_len) for n, h, t, r in deform], axis=1)
        W = np.exp(-(D ** 2)) ** sharp
        W += 1e-12
        W /= W.sum(axis=1, keepdims=True)
        for k, (n, *_r) in enumerate(deform):
            vg = ob.vertex_groups.new(name=n)
            idx = np.nonzero(W[:, k] > 0.003)[0]
            for i in idx:
                vg.add([int(i)], float(W[i, k]), 'REPLACE')
    mod = ob.modifiers.new('Rig', 'ARMATURE')
    mod.object = arm
    ob.parent = arm


LAYER_BONES = {
    'base': ('hips', 'spine', 'chest', 'neck', 'head', 'thigh.R', 'shin.R', 'foot.R', 'thigh.L', 'shin.L', 'foot.L'),
    'arm_R': ('chest', 'shoulder.R', 'upper_arm.R', 'forearm.R', 'hand.R'),
    'arm_L': ('chest', 'shoulder.L', 'upper_arm.L', 'forearm.L', 'hand.L'),
    'weapon': ('weapon',),
}


# ------------------------------------------------------------------ motion
def sm(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


EASES = {
    'smooth': lambda u: u * u * (3 - 2 * u),
    'in': lambda u: u * u * u,                      # accelerating: a strike driven home, a body falling
    'out': lambda u: 1 - (1 - u) ** 3,              # decelerating: a windup reaching its height, a recoil settling
    'snap': lambda u: u ** 1.6,
    'linear': lambda u: u,
}


def ease_keys(keys, t):
    """keys: [(t0, value) or (t0, value, ease), ...] sorted; the ease named on a key shapes the segment that ends
    at it (smoothstep when none)."""
    if t <= keys[0][0]:
        return dict(keys[0][1])
    for ka, kb in zip(keys, keys[1:]):
        a, va, b, vb = ka[0], ka[1], kb[0], kb[1]
        if a <= t <= b:
            f = EASES[kb[2] if len(kb) > 2 else 'smooth']
            u = f(max(0.0, min(1.0, (t - a) / max(1e-9, b - a))))
            return {k: va.get(k, 0.0) * (1 - u) + vb.get(k, 0.0) * u for k in set(va) | set(vb)}
    return dict(keys[-1][1])


ARM_PROFILES = {
    # windup/strike rotations of the weapon arm (deg, + = counter-clockwise = up and back for a right-facing unit)
    'sword-cut': dict(wind=(55, 30, 10), strike=(-50, -30, -15), lean=(6, -9), lunge=0.04),
    'guard-cut': dict(wind=(40, 25, 5), strike=(-40, -25, -10), lean=(4, -7), lunge=0.03),
    'royal-cleave': dict(wind=(65, 35, 10), strike=(-60, -35, -20), lean=(8, -12), lunge=0.05),
    'axe-cleave': dict(wind=(65, 35, 10), strike=(-60, -35, -20), lean=(8, -12), lunge=0.05),
    'heavy-smash': dict(wind=(70, 35, 10), strike=(-65, -40, -20), lean=(9, -13), lunge=0.04),
    'staff-strike': dict(wind=(40, 20, 0), strike=(-45, -25, -10), lean=(5, -8), lunge=0.03),
    'hammer-command': dict(wind=(55, 30, 5), strike=(-35, -20, -5), lean=(6, -6), lunge=0.02),
    'claw-rake': dict(wind=(55, 35, 15), strike=(-55, -40, -20), lean=(6, -12), lunge=0.05, both=True),
    'flask-toss': dict(wind=(80, 45, 15), strike=(-40, -30, -10), lean=(8, -8), lunge=0.02),
    'staff-cast': dict(wind=(30, 15, 0), strike=(-25, -15, -5), lean=(5, -5), lunge=0.015),
}


# Hand-target attacks. Each key: the wielding hand's angle around the shoulder 'a' (0 forward, 90 up, 180 back) and
# reach 'r' (fraction of the arm), the weapon's angle to that line 'rel' (or its absolute angle 'wa' when the profile
# says so), the other hand's 'oa'/'or' or offsets 'odx'/'odz', body 'lean' and 'lunge'. Keys run windup, contact,
# follow-through; the painted pose is the start and the end.
def _swing(wind, strike, follow, lean, lunge, **kw):
    return dict(keys=[(0.7, dict(wind, lean=lean[0], lunge=-0.01)), (1.0, dict(strike, lean=lean[1], lunge=lunge)),
                      (0.22, dict(follow, lean=lean[1] * 0.6, lunge=lunge * 0.7))], **kw)


HAND_PROFILES = {
    'sword-cut': _swing({'a': 130, 'r': .85, 'rel': 25}, {'a': -10, 'r': .95, 'rel': -10}, {'a': -35, 'r': .88, 'rel': -20},
                        (6, -9), .04),
    'guard-cut': _swing({'a': 115, 'r': .8, 'rel': 15}, {'a': 0, 'r': .95, 'rel': -10, 'odx': .04},
                        {'a': -30, 'r': .85, 'rel': -18, 'odx': .02}, (4, -7), .03),
    'royal-cleave': _swing({'a': 150, 'r': .9, 'rel': 30}, {'a': -20, 'r': .95, 'rel': -15}, {'a': -40, 'r': .85, 'rel': -10},
                           (9, -13), .05),
    'staff-strike': _swing({'a': 120, 'r': .8, 'rel': 15}, {'a': -5, 'r': .95, 'rel': -10}, {'a': -30, 'r': .85, 'rel': -15},
                           (5, -8), .03),
    'hammer-command': _swing({'a': 120, 'r': .85, 'rel': 10}, {'a': 25, 'r': .95, 'rel': 0}, {'a': 15, 'r': .9, 'rel': 0},
                             (5, -5), .02),
    'flask-toss': _swing({'a': -160, 'r': .8, 'rel': 0}, {'a': 50, 'r': .95, 'rel': 0}, {'a': 10, 'r': .85, 'rel': 0},
                         (8, -8), .02),
    # casters raise the staff high beside the head, then drive it forward at chest height (below the face, which
    # a near arm crossing higher would cover), the free hand thrust out with it
    'staff-cast': _swing({'a': 100, 'r': .85, 'wa': 95}, {'a': -5, 'r': .95, 'wa': 50, 'oa': 0, 'or': 1.0},
                         {'a': -10, 'r': .9, 'wa': 60, 'oa': 5, 'or': .95}, (5, -5), .015, absolute=True),
    'claw-rake': dict(keys=[(0.7, {'a': 130, 'r': .9, 'rel': 0, 'lean': 6, 'lunge': -0.01}),
                            (1.0, {'a': -35, 'r': 1.0, 'rel': 0, 'oa': 125, 'or': .9, 'lean': -12, 'lunge': .05}),
                            (0.2, {'a': -50, 'r': .9, 'rel': 0, 'oa': -35, 'or': 1.0, 'lean': -10, 'lunge': .05})]),
    'thrust': dict(keys=[(0.65, {'a': -160, 'r': .55, 'wa': 5, 'lean': 5, 'lunge': -0.02}),
                         (1.0, {'a': -5, 'r': 1.0, 'wa': -3, 'lean': -9, 'lunge': .06}),
                         (0.25, {'a': -15, 'r': .9, 'wa': -8, 'lean': -5, 'lunge': .04})], absolute=True),
}
HAND_PROFILES['staff-jab'] = dict(keys=[(0.65, {'a': -150, 'r': .6, 'wa': 70, 'lean': 4, 'lunge': -0.01}),
                                        (1.0, {'a': -15, 'r': 1.0, 'wa': 25, 'lean': -8, 'lunge': .04}),
                                        (0.25, {'a': -20, 'r': .9, 'wa': 35, 'lean': -4, 'lunge': .025})], absolute=True)
HAND_PROFILES['axe-cleave'] = HAND_PROFILES['heavy-smash'] = HAND_PROFILES['royal-cleave']
HAND_PROFILES['blade-stab'] = HAND_PROFILES['spear-thrust'] = HAND_PROFILES['thrust']


def hand_track(profile, t, c, n, rest, near=False, two=False):
    """Pose at local frame t of an n-frame attack with contact at c: keys eased in turn from the painted pose
    (`rest`), each angle unwrapped toward the one before so every move takes the short way round."""
    spec = HAND_PROFILES.get(profile, HAND_PROFILES['sword-cut'])
    tc = c / max(1, n - 1)
    # windup reached early and held a beat (anticipation), then the strike snaps through to contact
    times = [tc * min(0.62, spec['keys'][0][0]), tc, min(1.0, tc + spec['keys'][2][0])]
    keys = [(0.0, dict(rest))]
    seg_ease = ('out', 'snap', 'out')
    for j, (tt, (_, k)) in enumerate(zip(times, spec['keys'])):
        full = dict(rest)
        full.update(k)
        if two and j == 0 and 'r' in k:
            # two hands on one haft cannot reach as high or as far back as one arm
            full['a'] = min(k['a'], 110.0)
            full['r'] = min(k['r'], 0.72)
        if near and j > 0 and 'rel' in k and not spec.get('absolute'):
            # the near arm (facing right, the character's right) crosses in front of the body to strike: ending
            # lower keeps it from sweeping the whole chest, and the weapon still points where it would
            full['a'] = k['a'] - 20
            full['rel'] = k['rel'] + 20
        keys.append((tt, full, seg_ease[j]))
    keys.append((1.0, dict(rest), 'smooth'))
    # the weapon's own angle is what reads: interpolate it directly (a swing keyed by its angle to the arm sets it
    # from the hand's angle at each key)
    for key in keys:
        k = key[1]
        if not spec.get('absolute') or 'wa' not in k:
            k['wa'] = k['a'] + k['rel'] if not spec.get('absolute') else rest['wa']
    for j, (kp, kc) in enumerate(zip(keys, keys[1:])):
        prev, cur = kp[1], kc[1]
        for f in ('a', 'oa', 'wa'):
            if f in cur and f in prev:
                d = (cur[f] - prev[f] + 180) % 360 - 180
                if j < 2 and f in ('a', 'wa'):
                    # windup and strike go over the top: never through straight down (a hand under the knees, a
                    # blade swept through the body or the ground)
                    d = over_the_top(prev[f], d)
                cur[f] = prev[f] + d
    p = ease_keys(keys, t / max(1, n - 1))
    return p


def over_the_top(start, d):
    """Of the two ways round (d, d -/+ 360), the one whose path from `start` does not pass straight down (-90)."""
    def crosses_down(a0, dd):
        lo, hi = sorted((a0, a0 + dd))
        k = math.ceil((lo + 90) / 360)
        return -90 + 360 * k <= hi
    if not crosses_down(start, d):
        return d
    alt = d - 360 if d > 0 else d + 360
    return alt if not crosses_down(start, alt) else d


def attack_pose(profile, t, c, side, n):
    """Pose for local frame t of an n-frame attack whose contact frame is c."""
    tc = c / max(1, n - 1)
    if profile in ('spear-thrust', 'blade-stab', 'mounted-lance'):
        keys = [(0, {}), (tc * 0.62, {'lean': 6, 'lunge': -0.03, 'ua': 10, 'thrust': -0.05}, 'out'),
                (tc, {'lean': -10, 'lunge': 0.07, 'ua': -12, 'fa': -8, 'thrust': 0.10}, 'in'),
                (min(1, tc + 0.25), {'lean': -6, 'lunge': 0.05, 'ua': -6, 'thrust': 0.06}), (1, {})]
    elif profile in ('bow-draw', 'crossbow'):
        keys = [(0, {}), (tc * 0.85, {'draw': 1.0, 'lean': 3}), (tc, {'draw': -0.3, 'lean': 5, 'lunge': -0.015}),
                (min(1, tc + 0.3), {'draw': 0.0, 'lean': 1}), (1, {})]
    else:
        p = ARM_PROFILES.get(profile, ARM_PROFILES['sword-cut'])
        w, s = p['wind'], p['strike']
        keys = [(0, {}), (tc * 0.62, {'ua': w[0], 'fa': w[1], 'hd': w[2], 'lean': p['lean'][0], 'lunge': -0.01}, 'out'),
                (tc, {'ua': s[0], 'fa': s[1], 'hd': s[2], 'lean': p['lean'][1], 'lunge': p['lunge']}, 'in'),
                (min(1, tc + 0.22), {'ua': s[0] * 0.6, 'fa': s[1] * 0.6, 'hd': s[2] * 0.5, 'lean': p['lean'][1] * 0.6,
                                     'lunge': p['lunge'] * 0.7}), (1, {})]
        if p.get('both'):
            for k in keys:
                k[1]['both'] = 1.0
    return ease_keys(keys, t / max(1, n - 1))


class Animator:
    def __init__(self, arm, paint, lm, profile, fig_h_px, side=None, two=False):
        self.arm, self.paint, self.lm, self.profile = arm, paint, lm, profile
        self.side = side or weapon_side(lm) or 'R'
        self.two = two
        self.death_motion, self.death_release, self.air = None, None, 0.0
        self.vclass = ''
        self.toss, self.spin = (0.4, 0.0, 0.2), 0.0
        self.off = 'L' if self.side == 'R' else 'R'
        self.H = fig_h_px / PPU
        self.pb = arm.pose.bones
        self.drop = 0.0
        # each bone's rest direction in the picture plane, as an angle from straight down (+ = toward the front)
        self.rest = {}
        for b in arm.data.bones:
            d = b.tail_local - b.head_local
            self.rest[b.name] = math.degrees(math.atan2(d.x, -d.z))
        B = arm.data.bones
        ang = lambda v: math.degrees(math.atan2(v.z, v.x))
        armlen = lambda s_: (B['upper_arm.' + s_].length + B['forearm.' + s_].length) if 'forearm.' + s_ in B else 1.0
        self.hand_rig = None
        if 'wield' in B and 'upper_arm.' + self.side in B:
            sh = B['upper_arm.' + self.side].head_local.copy()
            L = armlen(self.side)
            if two and 'upper_arm.' + self.off in B:
                sh = (sh + B['upper_arm.' + self.off].head_local) / 2
                L = min(L, armlen(self.off))
            g0 = B['wield'].head_local.copy()
            if 'weapon' in B:
                wa0 = ang(B['weapon'].tail_local - B['weapon'].head_local)
            else:
                wa0 = ang(B['forearm.' + self.side].tail_local - B['forearm.' + self.side].head_local)
            a0 = ang(g0 - sh)
            self.hand_rig = dict(anchor=sh, L=L, g0=g0, wa0=wa0, a0=a0, r0=(g0 - sh).length / L,
                                 rel0=(wa0 - a0 + 180) % 360 - 180)
        self.off_rig = None
        rb = 'reach.' + self.off
        if rb in B and 'upper_arm.' + self.off in B:
            sh = B['upper_arm.' + self.off].head_local.copy()
            r0p = B[rb].head_local.copy()
            self.off_rig = dict(anchor=sh, L=armlen(self.off), p0=r0p, a0=ang(r0p - sh),
                                r0=(r0p - sh).length / armlen(self.off))

    def clear(self):
        self.drop = 0.0
        self.wfall = 0.0
        self.release = 0.0
        self.air = 0.0
        for pb in self.pb:
            for con in pb.constraints:
                if con.type == 'IK':
                    con.influence = 1.0
            pb.rotation_euler = (0, 0, 0)
            pb.location = (0, 0, 0)
            pb.scale = (1, 1, 1)

    def rot(self, name, deg):
        if name in self.pb:
            self.pb[name].rotation_euler.z += math.radians(deg)

    def wield(self, rot=0.0, dx=0.0, dz=0.0, slide=0.0):
        """Move a two-handed weapon: turn it about the point between the hands, shift that point (in figure heights,
        on the chest's frame), slide the weapon along its own length; the arms follow by IK."""
        if 'wield' not in self.pb:
            return
        w = self.pb['wield']
        w.rotation_euler.z += math.radians(rot)
        w.location = (w.location[0] + dx * self.H, w.location[1] + dz * self.H, 0.0)   # the bone points up
        if slide:
            self.pb['weapon'].location = (0.0, slide * self.H, 0.0)

    def hands(self, p):
        """Absolute hand targets: the wielding hand (or the point between two) at angle p['a'] around its shoulder,
        p['r'] of the arm's reach out, the weapon pointing p['wa'] degrees; the other hand at p['oa'] / p['or']
        (+ p['odx'], p['odz'] in figure heights). Angles: 0 = forward, 90 = up, on the chest's frame."""
        h = self.hand_rig
        if h and 'a' in p:
            a = math.radians(p['a'])
            g = h['anchor'] + Vector((math.cos(a), 0.0, math.sin(a))) * (h['L'] * p['r'])
            w = self.pb['wield']
            d = g - h['g0']
            w.location = (d.x, d.z, 0.0)            # the wield bone points up: local X, Y = world X, Z
            w.rotation_euler.z = math.radians(p['wa'] - h['wa0'])
        o = self.off_rig
        if o and 'oa' in p:
            a = math.radians(p['oa'])
            q = o['anchor'] + Vector((math.cos(a), 0.0, math.sin(a))) * (o['L'] * p['or'])
            d = q - o['p0']
            self.pb['reach.' + self.off].location = (d.x + p.get('odx', 0.0) * self.H, d.z + p.get('odz', 0.0) * self.H, 0.0)
        elif o and ('odx' in p or 'odz' in p):
            self.pb['reach.' + self.off].location = (p.get('odx', 0.0) * self.H, p.get('odz', 0.0) * self.H, 0.0)

    def reach(self, dx=0.0, dz=0.0):
        rb = 'reach.' + self.off
        if rb in self.pb:
            r = self.pb[rb]
            r.location = (r.location[0] + dx * self.H, r.location[1] + dz * self.H, 0.0)

    def place(self, dx=0.0, dz=0.0, rot=0.0, sy=1.0, sx=1.0):
        r = self.pb['root']
        r.location = (dx * self.H, dz * self.H, 0.0)   # root's local axes: X right, Y up (the bone points up)
        r.rotation_euler = (0, 0, math.radians(rot))
        r.scale = (sx, sy, 1.0)

    # clip poses ---------------------------------------------------------
    def idle(self, i, n):
        ph = math.tau * i / n
        s = math.sin(ph)
        self.place(dz=-0.004 * (1 - math.cos(ph)))
        if 'chest' in self.pb:
            self.pb['chest'].scale = (1.0 + 0.008 * s, 1.0 + 0.012 * s, 1.0)
        self.rot('head', 1.5 * math.sin(ph + 0.6))
        self.rot('upper_arm.' + self.side, 2.0 * s)
        self.rot('upper_arm.' + self.off, -1.5 * s)
        self.rot('spine', 0.8 * s)
        self.wield(1.2 * s, dz=0.003 * math.sin(ph + 0.8))
        self.reach(dz=0.003 * math.sin(ph + 1.6))

    WALKS = {
        # stride (fraction of leg length), foot lift (fraction of leg), hip bob (fraction of leg), lean (deg, - =
        # forward), stance narrowing (0 keeps the painted stance, 1 puts the feet under the hips), crouch (fraction of
        # leg the hips sink), per-leg stride/lift scale (R, L), head nod (deg), arm swing (deg)
        'march': dict(stride=.55, lift=.17, bob=.04, lean=-4, narrow=.45, crouch=.02, legs=((1, 1), (1, 1)), nod=1.5, arms=4),
        'heavy': dict(stride=.46, lift=.12, bob=.055, lean=-6, narrow=.4, crouch=.05, legs=((1, 1), (1, 1)), nod=2.5, arms=2.5),
        'shamble': dict(stride=.4, lift=.1, bob=.03, lean=-10, narrow=.4, crouch=.06, legs=((1, 1), (.75, .35)), nod=4, arms=1.5),
        'prowl': dict(stride=.62, lift=.2, bob=.035, lean=-14, narrow=.35, crouch=.12, legs=((1, 1), (1, 1)), nod=1, arms=3),
    }
    STYLE_OF = {'brute': 'heavy', 'crusher': 'heavy', 'boss': 'heavy', 'bloater': 'heavy', 'berserker': 'heavy',
                'walker': 'shamble', 'necromancer': 'march', 'runner': 'prowl', 'rogue': 'prowl',
                'saboteur': 'prowl'}
    STANCE = .6        # share of the cycle a foot is planted

    def leg_geometry(self):
        """Rest positions and lengths for both legs (armature space, X right, Z up), read once."""
        if getattr(self, '_legs', None) is not None:
            return self._legs
        B = self.arm.data.bones
        legs = {}
        for s_ in ('R', 'L'):
            tb, sb = 'thigh.' + s_, 'shin.' + s_
            if tb not in B or sb not in B:
                continue
            hip = B[tb].head_local.copy()
            knee = B[sb].head_local.copy()
            ankle = B[sb].tail_local.copy()
            legs[s_] = dict(hip=hip, ankle=ankle, L1=(knee - hip).length, L2=(ankle - knee).length,
                            knee_fwd=(knee - (hip + ankle) / 2).x)
        self._legs = legs
        return legs

    def leg_to(self, s_, target, hip_shift):
        """Planar two-bone IK: turn thigh and shin so the ankle reaches `target` (armature space) from the hip
        moved by `hip_shift`; the knee bends forward (toward the facing), the foot stays level."""
        g = self.legs_[s_]
        hip = g['hip'] + hip_shift
        d = target - hip
        L1, L2 = g['L1'], g['L2']
        dist = max(1e-6, min(d.length, (L1 + L2) * .999))
        dirv = d.normalized()
        # angle at the hip between the hip-ankle line and the thigh (law of cosines)
        cos_a = max(-1.0, min(1.0, (L1 * L1 + dist * dist - L2 * L2) / (2 * L1 * dist)))
        a = math.acos(cos_a)
        base = math.atan2(dirv.x, -dirv.z)          # from straight down, + toward the front
        thigh_abs = math.degrees(base + a)           # the knee forward of the hip-ankle line
        knee = hip + Vector((math.sin(math.radians(thigh_abs)), 0, -math.cos(math.radians(thigh_abs)))) * L1
        sv = (hip + dirv * dist) - knee
        shin_abs = math.degrees(math.atan2(sv.x, -sv.z))
        tb, sb = 'thigh.' + s_, 'shin.' + s_
        d_t = thigh_abs - self.rest[tb]
        d_s = (shin_abs - thigh_abs) - (self.rest[sb] - self.rest[tb])
        self.rot(tb, d_t)
        self.rot(sb, d_s)
        return d_t + d_s

    def walk(self, i, n):
        """A real stride, driven by the feet: each foot is planted and slides back under the body (so on the ground
        it stays still while the unit moves forward), then lifts in an arc and swings forward to plant again; the
        knees follow by two-bone IK, bending forward. The hips dip as the leading foot lands and rise as the body
        passes over the planted leg; the feet close in under the hips from the painted fighting stance; the body
        leans into the walk and the arms swing against the legs. The unit's class sets the gait."""
        g = self.WALKS[self.STYLE_OF.get(self.vclass, 'march')]
        self.legs_ = self.leg_geometry()
        if len(self.legs_) < 2:
            return
        leg_len = sum(v['L1'] + v['L2'] for v in self.legs_.values()) / len(self.legs_)
        lanes = {s_: v['ankle'].x + (v['hip'].x - v['ankle'].x) * g['narrow'] for s_, v in self.legs_.items()}
        sep = abs(lanes['L'] - lanes['R'])
        g = dict(g, stride=min(g['stride'], .8 * sep / leg_len))
        cyc = i / n
        mid_x = sum(v['hip'].x for v in self.legs_.values()) / 2
        # hip height: the leg nearly straight as the body passes over the planted foot, and lowered just enough at
        # double support for both feet to reach the ground (the painted fighting stance is a crouch or a wide
        # straddle; walking, the body stands over its legs), less the gait's crouch
        mid_need, ext_need = [], []
        for s_ in ('R', 'L'):
            v = self.legs_[s_]
            nx = v['ankle'].x + (v['hip'].x - v['ankle'].x) * g['narrow']
            dx = abs(nx - v['hip'].x)
            half = .5 * g['stride'] * leg_len * max(g['legs'][0][0], g['legs'][1][0])
            full = v['L1'] + v['L2']
            rest_vert = v['hip'].z - v['ankle'].z
            mid_need.append(math.sqrt(max(1e-6, (full * .975) ** 2 - dx * dx)) - rest_vert)
            ext_need.append(math.sqrt(max(1e-6, (full * .995) ** 2 - (dx + half) ** 2)) - rest_vert)
        hi, lo = min(mid_need), min(ext_need)
        k = 0.5 - 0.5 * math.cos(math.tau * 2 * (cyc - .05))          # 0 at double support, 1 passing
        stand = lo + (hi - lo) * k - g['crouch'] * leg_len
        bob = 0.0
        sway = .012 * leg_len * math.sin(math.tau * cyc)
        hip_shift = Vector((sway, 0, stand + bob))
        foot_sum = 0.0
        for s_, off in (('R', 0.0), ('L', 0.5)):
            v = self.legs_[s_]
            k_stride, k_lift = g['legs'][0 if s_ == 'R' else 1]
            S = g['stride'] * leg_len * k_stride
            u = (cyc + off) % 1.0
            # the neutral foot: the painted ankle, pulled in toward under the body
            nx = v['ankle'].x + (v['hip'].x - v['ankle'].x) * g['narrow']
            if u < self.STANCE:            # planted: moves back at constant speed
                q = u / self.STANCE
                fx = S * (.5 - q)
                lift = 0.0
            else:                          # swing: lifts and travels forward, fastest in the middle
                q = (u - self.STANCE) / (1 - self.STANCE)
                e = q * q * (3 - 2 * q)
                fx = S * (-.5 + e)
                lift = g['lift'] * leg_len * k_lift * math.sin(math.pi * q) ** .8
            target = Vector((nx + fx, 0, v['ankle'].z + lift))
            dt = self.leg_to(s_, target, hip_shift)
            if DEBUG:
                hp_ = v['hip'] + hip_shift
                print('WALKDBG', i, s_, 'u', round(u, 2), 'stand', round(stand, 3), 'leg', round(v['L1'] + v['L2'], 3),
                      'dist', round((target - hp_).length, 3), 'restdist', round((v['ankle'] - v['hip']).length, 3),
                      'dx', round(nx - v['hip'].x, 3), flush=True)
            # the foot stays level on the ground, heel lifting at toe-off and toe up just before the heel strikes
            toe = 0.0
            if u < self.STANCE and u > self.STANCE * .7:
                toe = -14 * (u - self.STANCE * .7) / (self.STANCE * .3)
            elif u >= self.STANCE:
                q = (u - self.STANCE) / (1 - self.STANCE)
                toe = -14 * (1 - q) + 10 * max(0.0, q - .6) / .4
            self.rot('foot.' + s_, -dt + toe)
            foot_sum += fx
        # legs are children of the hips: move the hips (not the root) so the solved feet stay where they were put.
        # The hips bone points down, so its local X and Y are world -X and -Z.
        if 'hips' in self.pb:
            self.pb['hips'].location = (-hip_shift.x, -hip_shift.z, 0.0)
        swing = math.sin(math.tau * cyc)
        self.rot('spine', g['lean'] + 1.2 * math.sin(math.tau * 2 * cyc))
        self.rot('chest', -.8 * swing)
        self.rot('head', g['nod'] * math.sin(math.tau * 2 * cyc + .6) - g['lean'] * .4)
        self.wield(g['arms'] * swing, dz=0.006 * math.sin(math.tau * 2 * cyc))
        self.reach(dx=-0.016 * swing * g['arms'] / 4, dz=0.004 * math.sin(math.tau * 2 * cyc))
        self.walk_stride = g['stride'] * leg_len / self.STANCE     # ground covered per cycle, armature units

    def attack(self, i, n, c, profile=None):
        if self.two:
            return self.attack_two(i, n, c, profile or self.profile)
        if 'string' in self.pb and (profile or self.profile) == 'bow-draw':
            return self.attack_bow(i, n, c)
        prof = profile or self.profile
        if prof == 'flask-toss' and self.long_weapon():
            prof = 'spear-thrust'            # a harpoon or javelin is hurled from a draw-back, not lobbed
        if prof == 'crossbow' and 'wield' in self.pb:
            return self.attack_recoil(i, n, c)
        if self.hand_rig:
            return self.attack_hands(i, n, c, prof)
        p = attack_pose(prof, i, c, self.side, n)
        self.place(dx=p.get('lunge', 0.0))
        self.rot('spine', p.get('lean', 0.0) * 0.6)
        self.rot('chest', p.get('lean', 0.0) * 0.4)
        sd = self.side
        self.rot('upper_arm.' + sd, p.get('ua', 0.0))
        self.rot('forearm.' + sd, p.get('fa', 0.0))
        self.rot('hand.' + sd, p.get('hd', 0.0))
        if p.get('both'):
            self.rot('upper_arm.' + self.off, p.get('ua', 0.0) * 0.8)
            self.rot('forearm.' + self.off, p.get('fa', 0.0) * 0.8)
        if 'weapon' in self.pb and p.get('thrust'):
            self.pb['weapon'].location = (0.0, p['thrust'] * self.H, 0.0)   # along the weapon's own axis
        if p.get('draw'):
            self.rot('forearm.' + sd, 14 * p['draw'])
            self.rot('upper_arm.' + sd, 6 * p['draw'])
        # the front leg braces
        self.rot('thigh.L', -4 * p.get('lean', 0.0) / 10)

    def long_weapon(self):
        B_ = self.arm.data.bones
        return 'weapon' in B_ and (B_['weapon'].tail_local - B_['weapon'].head_local).length > 0.4 * self.H

    def attack_recoil(self, i, n, c):
        """A crossbow held at aim: steady, the shot kicks the stock back and the muzzle up, then it settles."""
        tc = c / max(1, n - 1)
        keys = [(0, {}), (tc * 0.8, {'r': -1.5, 'lean': 1}),
                (tc, {'r': 10, 'x': -0.04, 'z': 0.01, 'lean': 5, 'lunge': -0.016}, 'snap'),
                (min(1, tc + 0.3), {'r': 2, 'x': -0.008, 'lean': 1}, 'out'), (1, {})]
        p = ease_keys(keys, i / max(1, n - 1))
        self.place(dx=p.get('lunge', 0.0))
        self.rot('spine', p.get('lean', 0.0) * 0.6)
        self.wield(p.get('r', 0.0), p.get('x', 0.0), p.get('z', 0.0))

    def long_level(self):
        """A long weapon held within 30 degrees of level, pointing forward: a spear or pike."""
        B_ = self.arm.data.bones
        if 'weapon' not in B_:
            return False
        d = B_['weapon'].tail_local - B_['weapon'].head_local
        return d.length > 0.45 * self.H and d.x > 0 and abs(math.degrees(math.atan2(d.z, d.x))) < 30

    def rest_hands(self):
        h, o = self.hand_rig, self.off_rig
        r = {'a': h['a0'], 'r': h['r0'], 'rel': h['rel0'], 'wa': h['wa0'], 'lean': 0.0, 'lunge': 0.0,
             'odx': 0.0, 'odz': 0.0}
        if o:
            r.update(oa=o['a0'], **{'or': o['r0']})
        return r

    def attack_hands(self, i, n, c, profile):
        p = hand_track(profile, i, c, n, self.rest_hands(), near=(self.side == 'R' and not self.two),
                       two=self.two)
        if not self.off_rig:
            for k in ('oa', 'or', 'odx', 'odz'):
                p.pop(k, None)
        self.place(dx=p.get('lunge', 0.0))
        self.rot('spine', p.get('lean', 0.0) * 0.6)
        self.rot('chest', p.get('lean', 0.0) * 0.4)
        self.hands(p)
        self.rot('thigh.L', -4 * p.get('lean', 0.0) / 10)

    def attack_bow(self, i, n, c):
        """Painted at full draw: hold, pull the last inch, loose (the string hand flies back, the bow kicks), then
        reach forward to the string for the next arrow and draw again."""
        tc = c / max(1, n - 1)
        keys = [(0, {}), (tc * 0.8, {'pull': 0.03, 'lean': 2}, 'out'),
                (tc, {'pull': 0.09, 'kick': 7, 'lean': 4}, 'snap'),
                (min(1, tc + 0.3), {'pull': -0.14, 'kick': 1}, 'out'), (1, {})]
        p = ease_keys(keys, i / max(1, n - 1))
        g = self.arm.data.bones['weapon'].head_local
        h = self.arm.data.bones['string'].head_local
        d = (h - g).normalized()
        self.pb['string'].location = (d.x * p.get('pull', 0.0) * self.H, d.z * p.get('pull', 0.0) * self.H, 0.0)
        self.wield(p.get('kick', 0.0))
        self.rot('spine', p.get('lean', 0.0) * 0.5)

    def attack_two(self, i, n, c, profile):
        """Two-handed: the weapon leads and the arms follow. Thrusts draw back, then drive the point forward and
        level; everything else is a two-handed swing: up and back over the shoulder, then down through the target."""
        tc = c / max(1, n - 1)
        if profile in ('claw-rake', 'pounce'):
            profile = 'heavy-smash'          # a claw unit painted holding a tool two-handed swings it
        if profile == 'flask-toss' and self.long_weapon():
            profile = 'spear-thrust'
        if profile in ('sword-cut', 'guard-cut') and self.long_level():
            profile = 'spear-thrust'         # a light unit painted with a spear or pike held level thrusts with it
            #                                  (heavy smashes and cleaves keep their swing, however low the haft)
        if profile == 'crossbow':
            keys = [(0, {}), (tc * 0.8, {'r': -1.5, 'lean': 1}), (tc, {'r': 9, 'x': -0.045, 'z': 0.012, 'lean': 5}, 'snap'),
                    (min(1, tc + 0.3), {'r': 2, 'x': -0.01, 'lean': 1}, 'out'), (1, {})]
        elif profile in ('spear-thrust', 'blade-stab', 'mounted-lance'):
            keys = [(0, {}),
                    (tc * 0.6, {'r': 6, 'x': -0.07, 'z': 0.014, 'lean': 6, 'lunge': -0.03}, 'out'),
                    (tc, {'r': -9, 'x': 0.12, 'z': -0.012, 'lean': -10, 'lunge': 0.07, 'sl': 0.04}, 'in'),
                    (min(1, tc + 0.12), {'r': -10, 'x': 0.13, 'lean': -11, 'lunge': 0.075, 'sl': 0.045}, 'out'),
                    (min(1, tc + 0.3), {'r': -5, 'x': 0.06, 'lean': -5, 'lunge': 0.04, 'sl': 0.02}), (1, {})]
        elif self.hand_rig:
            return self.attack_hands(i, n, c, profile)
        else:
            keys = [(0, {}),
                    (tc * 0.62, {'r': 60, 'x': -0.06, 'z': 0.13, 'lean': 9, 'lunge': -0.02}, 'out'),
                    (tc, {'r': -50, 'x': 0.07, 'z': -0.05, 'lean': -12, 'lunge': 0.05}, 'in'),
                    (min(1, tc + 0.12), {'r': -58, 'x': 0.075, 'z': -0.06, 'lean': -13, 'lunge': 0.055}, 'out'),
                    (min(1, tc + 0.3), {'r': -35, 'x': 0.05, 'z': -0.03, 'lean': -7, 'lunge': 0.035}), (1, {})]
        p = ease_keys(keys, i / max(1, n - 1))
        self.place(dx=p.get('lunge', 0.0))
        self.rot('spine', p.get('lean', 0.0) * 0.6)
        self.rot('chest', p.get('lean', 0.0) * 0.4)
        self.wield(p.get('r', 0.0), p.get('x', 0.0), p.get('z', 0.0), p.get('sl', 0.0))
        self.rot('thigh.L', -4 * p.get('lean', 0.0) / 10)

    def melee(self, i, n, c):
        """Point-blank: casters jab with the staff end, crossbowmen ram the stock, everyone else a short guard cut."""
        prof = {'staff-cast': 'staff-jab', 'crossbow': 'spear-thrust'}.get(self.profile, 'guard-cut')
        self.attack(i, n, c, profile=prof)

    def hit(self, i, n):
        k = 1.0 if i == 0 else 0.45
        self.place(dx=-0.02 * k)
        self.rot('spine', 7 * k)
        self.rot('chest', 4 * k)
        self.rot('head', 8 * k)
        self.rot('upper_arm.' + self.side, 8 * k)
        self.wield(6 * k, dx=-0.01 * k, dz=0.01 * k)

    def deploy(self, i, n):
        t = i / max(1, n - 1)
        self.place(sy=0.86 + 0.14 * sm(t), sx=1.06 - 0.06 * sm(t), dz=0.0)
        self.rot('spine', -6 * (1 - t))

    CASTERS = ('staff-cast', 'staff-strike', 'hammer-command', 'flask-toss')

    def cast(self, i, n, hold_from=6):
        """The ability pose: casters lift the staff high and thrust the free hand out, channelling; fighters raise
        the weapon overhead in a rallying shout; archers draw and aim high. Rises into the pose, then breathes."""
        t = min(1.0, i / max(1, hold_from))
        e = 1 - (1 - t) ** 3
        breathe = math.sin(math.tau * max(0, i - hold_from) / max(1, n - hold_from)) if i >= hold_from else 0.0
        caster = self.profile in self.CASTERS
        lean = (8 if caster else 5) * e + 1.5 * breathe
        self.rot('spine', lean * .6)
        self.rot('chest', lean * .4)
        self.rot('head', (10 if caster else 14) * e)
        if self.two:
            self.wield(70 * e + 3 * breathe, dx=-.02 * e, dz=.16 * e + .006 * breathe)
        elif 'string' in self.pb:
            self.wield(28 * e, dz=.03 * e)
        elif self.hand_rig:
            rest = self.rest_hands()
            if caster:
                pose = {'a': 92, 'r': .95, 'wa': 95, 'oa': 8, 'or': 1.0}
            else:
                pose = {'a': 105, 'r': .95, 'wa': 100, 'oa': -40, 'or': .7}
            p = dict(rest)
            for k_, v_ in pose.items():
                if k_ in rest:
                    d = (v_ - rest[k_] + 180) % 360 - 180 if k_ in ('a', 'oa', 'wa') else v_ - rest[k_]
                    p[k_] = rest[k_] + d * e
            p['a'] += 2.5 * breathe
            if not self.off_rig:
                for k_ in ('oa', 'or'):
                    p.pop(k_, None)
            self.hands(p)
        self.place(dz=.004 * breathe)

    def legs_to(self, thigh_abs, shin_abs, w):
        """Blend both legs toward a pose given as absolute angles from straight down (+ = toward the front) in the
        body's own frame: kneeling, sitting, folded under, straight along a fallen body."""
        for s_, k_ in (('R', 1.0), ('L', 0.92)):
            th, sh = 'thigh.' + s_, 'shin.' + s_
            if th in self.rest:
                self.rot(th, (thigh_abs - self.rest[th]) * w * k_)
            if sh in self.rest and th in self.rest:
                bend = (shin_abs - thigh_abs) - (self.rest[sh] - self.rest[th])
                self.rot(sh, bend * w * k_)

    def arms_limp(self, target_abs, w):
        for s_ in ('R', 'L'):
            ua, fa = 'upper_arm.' + s_, 'forearm.' + s_
            if ua in self.rest:
                want = max(-50.0, min(50.0, target_abs - self.rest[ua]))   # big turns tear a painting
                self.rot(ua, want * w)
            if fa in self.rest and ua in self.rest:
                self.rot(fa, -0.5 * (self.rest[fa] - self.rest[ua]) * w)

    def death(self, i, n, impact, forward):
        """The unit's own death (its clip's motion, as roster/deaths.py chose it), in the picture plane: a topple
        about the feet, legs that kneel, sit or fold, a lift or a spin, then a bounce and stillness. The hands let
        go of the weapon at the table's release time; build.drop_weapon throws it."""
        t = i / max(1, n - 1)
        ti = max(0.2, impact / max(1, n - 1))
        self.t_death, self.ti_death = t, ti
        keys = death_keys(self.death_motion, ti, forward)
        p = ease_keys(keys, t)
        self.legs_to(p.get('kt', 0.0), p.get('ks', 0.0), p.get('kw', 0.0))
        self.rot('spine', p.get('sp', 0.0))
        self.rot('chest', p.get('ch', 0.0))
        self.rot('head', p.get('hd', 0.0))
        self.wfall = min(1.0, abs(p.get('top', 0.0)) / 90.0)
        rel = self.death_release if self.death_release is not None else ti * 0.5
        self.release = sm(min(1.0, max(0.0, (t - rel) / 0.08)))
        for s_ in ('R', 'L'):
            fa = self.pb.get('forearm.' + s_)
            for con in (fa.constraints if fa else ()):
                if con.type == 'IK':
                    con.influence = 1.0 - self.release
        fl = p.get('fl', 0.0)
        # the blow flings the arms (and whatever they hold) up and back, before the hands let go
        self.wield(26 * fl, dx=-0.02 * fl, dz=0.05 * fl)
        self.arms_limp(p.get('ua', 0.0) - 10.0 + 60 * fl, max(self.release * 0.8, 0.6 * fl))
        flip = p.get('flip', 0.0)
        sq = p.get('sq', 0.0)
        self.place(rot=p.get('top', 0.0), dx=p.get('dx', 0.0), sx=math.cos(math.pi * flip) * (1 + sq * 0.6),
                   sy=1.0 + sq)
        if abs(self.pb['root'].scale[0]) < 0.35:      # a spin turns the body, never down to an edge-on sliver
            self.pb['root'].scale[0] = math.copysign(0.35, self.pb['root'].scale[0] or 1.0)
        self.air = p.get('air', 0.0)


def dramatize_death(keys, ti, motion):
    """Weight for any death motion's keys: the killing blow jolts the body back (head whips, the weapon and arms
    jerk up and out), the fall then accelerates like a body under gravity and lands hard at ti, bounces once off
    the ground and settles into the motion's final pose."""
    keys = [tuple(k) for k in keys]
    final = dict(keys[-1][1])
    at_ti = ease_keys(keys, ti)
    t_j = ti * 0.16
    jolt = ease_keys(keys, t_j)
    if motion not in ('burst', 'nest-burst', 'rise-collapse'):
        jolt['top'] = jolt.get('top', 0.0) + 7.0
        jolt['hd'] = jolt.get('hd', 0.0) + 22.0
        jolt['sp'] = jolt.get('sp', 0.0) + 6.0
        jolt['dx'] = jolt.get('dx', 0.0) - 0.025
        jolt['fl'] = 1.0
    pre = [k for k in keys if k[0] < ti - 1e-6 and k[0] > t_j + 1e-6]
    out = [(0.0, dict(keys[0][1])), (t_j, jolt, 'out')]
    for k in pre:
        out.append((k[0], dict(k[1]), 'smooth'))
    out.append((ti, dict(at_ti, fl=0.0), 'in'))           # the fall accelerates into the ground
    span = 1.0 - ti
    top_i = at_ti.get('top', 0.0)
    bounce = dict(ease_keys([(ti, at_ti), (1.0, final)], ti + span * 0.1))
    if abs(top_i) > 40:
        bounce['top'] = top_i * 0.9                        # rebounds off the ground...
    bounce['hd'] = bounce.get('hd', 0.0) + 6.0
    out.append((ti + span * 0.1, bounce, 'out'))
    land = dict(ease_keys([(ti, at_ti), (1.0, final)], ti + span * 0.26))
    land['top'] = top_i + (final.get('top', top_i) - top_i) * 0.3 if abs(top_i) > 40 else land.get('top', 0.0)
    out.append((ti + span * 0.26, land, 'in'))             # ...and drops back
    settle = dict(ease_keys([(ti, at_ti), (1.0, final)], ti + span * 0.42))
    if abs(top_i) > 40:
        settle['top'] = land['top'] * 0.985 + final.get('top', 0.0) * 0.015
    out.append((ti + span * 0.42, settle, 'out'))
    out.append((1.0, final, 'smooth'))
    return out


def death_keys(motion, ti, forward):
    """Key poses for a death motion (see raw_death_keys), with dramatize_death's weight added."""
    m = (motion or 'fall-back').replace('_', '-')
    return dramatize_death(raw_death_keys(motion, ti, forward), ti, m)


def raw_death_keys(motion, ti, forward):
    """Key poses for a death motion, over the clip (0-1) with the body landing at ti. Fields: top (topple, degrees,
    + = backward), dx (slide, figure heights), air (lift), kt/ks/kw (legs: thigh and shin absolute angles, weight),
    sp/ch/hd (spine, chest, head), ua (where the limp arms hang), flip (a spin, 0-1), sq (swell or squash)."""
    m = (motion or 'fall-back').replace('_', '-')
    after = lambda f: ti + (1 - ti) * f
    flat = {'kw': 0.9, 'kt': 0.0, 'ks': 0.0}
    if m in ('fall-forward', 'dive-skid', 'last-roar', 'hound-fall'):
        skid = 0.1 if m == 'dive-skid' else 0.0
        lead = [(ti * 0.45, {'sp': 15, 'ch': 10, 'hd': 25, 'ua': 40, 'top': 6, 'dx': -0.01})] if m == 'last-roar' else \
               [(ti * 0.3, {'top': -8, 'dx': 0.03, 'sp': -10, 'hd': -10, 'kw': 0.4, 'kt': 20, 'ks': -25})]
        return [(0, {})] + lead + [
            (ti, dict(flat, top=-90, dx=0.08, hd=-12, sp=-4)),
            (after(0.25), dict(flat, top=-84, dx=0.08 + skid * 0.7, hd=-6)),
            (1, dict(flat, top=-90, dx=0.08 + skid, hd=-10))]
    if m == 'stagger-back':
        return [(0, {}), (ti * 0.25, {'dx': -0.04, 'top': 6, 'kw': 0.5, 'kt': -20, 'ks': -10, 'hd': 10}),
                (ti * 0.55, {'dx': -0.09, 'top': 16, 'kw': 0.5, 'kt': 15, 'ks': -30, 'hd': 15, 'sp': 8}),
                (ti, dict(flat, top=90, dx=-0.13, hd=15)), (after(0.25), dict(flat, top=84, dx=-0.13, hd=8)),
                (1, dict(flat, top=90, dx=-0.13, hd=12))]
    if m in ('crumple', 'burst', 'nest-burst'):
        swell = [(ti * 0.45, {'sq': 0.14}), (ti * 0.62, {'sq': 0.17})] if 'burst' in m else \
                [(ti * 0.35, {'kw': 0.7, 'kt': 40, 'ks': -60, 'sp': -12, 'hd': -12, 'top': -4})]
        return [(0, {})] + swell + [
            (ti * 0.8, {'kw': 1, 'kt': 75, 'ks': -125, 'sp': -30, 'hd': -20, 'top': -40,
                        'sq': -0.04 if 'burst' in m else 0}),
            # the knees give, then the body goes on over onto its front, legs sliding out behind: prone, not a bundle
            (ti, {'kw': 1, 'kt': 45, 'ks': -80, 'sp': -18, 'hd': -20, 'top': -82}),
            (after(0.3), {'kw': 1, 'kt': 32, 'ks': -55, 'sp': -10, 'hd': -15, 'top': -86}),
            (1, {'kw': 1, 'kt': 30, 'ks': -50, 'sp': -10, 'hd': -18, 'top': -88})]
    if m == 'sit-slump':
        sit = {'kw': 1, 'kt': 85, 'ks': 0}
        return [(0, {}), (ti * 0.4, dict(sit, sp=5)), (ti * 0.6, dict(sit, sp=8, hd=10, top=8)),
                (ti, dict(sit, top=55, sp=15, hd=20)), (1, dict(sit, top=80, sp=10, hd=15))]
    if m in ('kneel-topple', 'pray-slump'):
        kneel = {'kw': 1, 'kt': 40, 'ks': -95}
        bow = {'sp': -35, 'ch': -20, 'hd': -25} if m == 'pray-slump' else {'sp': -8, 'hd': -15}
        lie = {'kw': 1, 'kt': 28, 'ks': -55, 'sp': -10, 'hd': -15}
        return [(0, {}), (ti * 0.35, dict(kneel, sp=-8, hd=-15)), (ti * 0.65, dict(kneel, **bow)),
                (ti * 0.85, dict(kneel, top=-45, **bow)), (ti, dict(lie, top=-84)),
                (after(0.25), dict(lie, top=-80)), (1, dict(lie, top=-88))]
    if m == 'rise-collapse':
        return [(0, {}), (ti * 0.4, {'air': 0.08, 'sp': 10, 'hd': 15, 'ua': 50}),
                (ti * 0.6, {'air': 0.09, 'sp': 12, 'hd': 18, 'ua': 55}),
                (ti * 0.85, {'air': 0, 'kw': 1, 'kt': 70, 'ks': -120, 'top': -45, 'sp': -20, 'hd': -15}),
                (ti, {'kw': 1, 'kt': 42, 'ks': -75, 'top': -82, 'sp': -12, 'hd': -18}),
                (1, {'kw': 1, 'kt': 30, 'ks': -50, 'top': -88, 'sp': -10, 'hd': -18})]
    if m == 'spin-fall':
        return [(0, {}), (ti * 0.45, {'flip': 1.0, 'top': 8, 'dx': -0.02}),
                (ti, dict(flat, flip=1.0, top=90, dx=-0.05)), (after(0.25), dict(flat, flip=1.0, top=84, dx=-0.05)),
                (1, dict(flat, flip=1.0, top=90, dx=-0.05))]
    if m == 'topple-rigid':
        s_ = -1 if forward else 1
        return [(0, {}), (ti * 0.3, {'top': 6 * s_}), (ti * 0.75, {'top': 45 * s_}), (ti, {'top': 90 * s_}),
                (after(0.25), {'top': 84 * s_}), (1, {'top': 90 * s_})]
    # fall-back (and any motion without a 2D version)
    return [(0, {}), (ti * 0.35, {'top': 12, 'dx': -0.03, 'sp': 8, 'hd': 12, 'kw': 0.35, 'kt': 10, 'ks': -25}),
            (ti, dict(flat, top=90, dx=-0.06, sp=4, hd=18)), (after(0.25), dict(flat, top=84, dx=-0.065, hd=10)),
            (1, dict(flat, top=90, dx=-0.065, hd=14))]


def ground_fix(ob, arm):
    """Raise or lower the root so the deformed painting rests on the ground (z = 0)."""
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    me = ev.to_mesh()
    co = np.empty(len(me.vertices) * 3)
    me.vertices.foreach_get('co', co)
    ev.to_mesh_clear()
    co = co.reshape(-1, 3) @ np.array(ob.matrix_world)[:3, :3].T + np.array(ob.matrix_world)[:3, 3]
    return co


def lie_matrix(d, head):
    """World matrix for a weapon bone pointing along d (picture plane) from head, its local Z toward the camera."""
    z = Vector((0.0, -1.0, 0.0))
    y = d.normalized()
    x = y.cross(z)
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = head
    return m


def drop_weapon(arm, anim, layer_obs, center, forward, state, dt=0.075):
    """Let go in the death, the weapon is thrown: from the pose it was released in, with the table's toss (metres
    per second forward and up, a figure being ~1.8 m) and spin, under gravity, until it strikes the ground; then it
    tips flat, skids a little, and lies there."""
    if anim.release <= 0:
        state.clear()
        return
    mw = arm.matrix_world
    wb = arm.pose.bones['weapon']
    wob = next((o for n_, o in layer_obs if n_ == 'weapon'), None)
    m_per = anim.H / 1.8
    if 'c0' not in state:
        # long weapons turn slowly in the air (a spear does not cartwheel like a dagger)
        state['spin_k'] = min(1.0, 0.35 * anim.H / max(1e-6, wb.length))
        h = mw @ wb.head
        d = ((mw @ wb.tail) - h).normalized()
        tx, _, tz = anim.toss
        state.update(c0=h + d * center, a0=math.degrees(math.atan2(d.z, d.x)), t0=anim.t_death,
                     v=Vector((tx * m_per, 0.0, tz * m_per)), w=anim.spin * state['spin_k'])
        # it lands about when the body does: in a short clip real gravity would leave it hanging in the air
        t_land = max(0.1, (anim.ti_death + 0.08 - anim.t_death)) * state.get('clip_s', 1.0)
        h0 = max(0.05, state['c0'].z)
        g_real = 9.8 * m_per
        state['g'] = max(g_real, 2 * (h0 + state['v'].z * t_land) / (t_land * t_land))
        state['t_land'] = t_land
        a0 = state['a0']
        flat0 = min((0.0, 180.0, -180.0), key=lambda f: abs(((a0 - f + 180) % 360) - 180))
        state['flat'] = a0 + (((flat0 - a0) + 180) % 360 - 180)
    T = (anim.t_death - state['t0']) * state.get('clip_s', 1.0)
    g = state['g']

    def put(c, a):
        dd = Vector((math.cos(math.radians(a)), 0.0, math.sin(math.radians(a))))
        wb.matrix = mw.inverted() @ lie_matrix(dd, c - dd * center)
        bpy.context.view_layer.update()
        return float(ground_fix(wob, arm)[:, 2].min()) if wob else 0.0

    if 'land' not in state:
        c = state['c0'] + state['v'] * T + Vector((0.0, 0.0, -0.5 * g * T * T))
        q = min(1.0, T / state['t_land'])
        a = state['a0'] + state['w'] * T * (1 - q) + (state['flat'] - state['a0']) * sm(q) * 0.85
        low = put(c, a)
        if low >= 0:
            return
        state['land'] = (T, c.copy(), a, state['v'].x)
    t_land, c_l, a_l, vx = state['land']
    u = T - t_land
    flat_a = min((0.0, 180.0, -180.0, 360.0), key=lambda f: abs(((a_l - f + 180) % 360) - 180))
    flat_a = a_l + (((flat_a - a_l) + 180) % 360 - 180)
    k = sm(min(1.0, u / 0.18))
    bounce = math.sin(min(1.0, u / 0.22) * math.pi) * 0.03 * anim.H * (1 - min(1.0, u / 0.22))
    slide = vx * 0.35 * min(u, 0.25)
    a = a_l + (flat_a - a_l) * k
    c = Vector((c_l.x + slide, 0.0, c_l.z))
    low = put(c, a)
    put(c + Vector((0.0, 0.0, -low + bounce)), a)


# ------------------------------------------------------------------ render
def camera(paint, sc):
    figH, figW = paint.fig_h, paint.fig_w
    # wide enough for the standing figure and for the body lying either way from its feet
    lo = min((paint.x0 - paint.gx) / PPU, -1.4 * figH)
    hi = max((paint.x1 - paint.gx) / PPU, 1.4 * figH)
    W = 1.08 * max(hi - lo, figH)
    H = W * 1.25
    cam_d = bpy.data.cameras.new('Cam')
    cam_d.type = 'ORTHO'
    cam_d.sensor_fit = 'HORIZONTAL'
    cam_d.ortho_scale = W
    cam = bpy.data.objects.new('Cam', cam_d)
    sc.collection.objects.link(cam)
    cx = (lo + hi) / 2
    cz = H / 2 - 0.14 * H
    cam.location = (cx, -20.0, cz)
    cam.rotation_euler = (math.radians(90), 0, 0)
    sc.camera = cam
    px_w = int(round(W * PPU))
    sc.render.resolution_x, sc.render.resolution_y = px_w, int(round(px_w * 1.25))
    sc.render.resolution_percentage = 100
    return cam, W, H, cx, cz


def norm_point(p3, W, H, cx, cz):
    """Blender point -> the metadata convention: offset from the ground anchor in frame fractions, y negative up."""
    gxn = (0.0 - (cx - W / 2)) / W
    gyn = (cz + H / 2 - 0.0) / H
    xn = (p3.x - (cx - W / 2)) / W
    yn = (cz + H / 2 - p3.z) / H
    return [round(xn - gxn, 6), round(yn - gyn, 6)]


def new_contract(old, big):
    """The clip set rendered now: about twice the old frames (smooth at the battle's 60 Hz), the same contact time
    so gameplay timing is unchanged, and the death given room to fall, land and settle. Old starts are reassigned
    in the old order; other per-clip fields (fx, motion) carry over."""
    order = sorted(old.items(), key=lambda kv: kv[1]['start'])
    out, start = {}, 0
    for name, o in order:
        c = {k: v for k, v in o.items() if k not in ('start', 'count', 'duration', 'contactFrame', 'impactFrame',
                                                       'impactPoint')}
        if name == 'idle':
            c.update(count=8, duration=0.13, loop=True)
        elif name == 'walk':
            c.update(count=10, duration=0.07, loop=True)
        elif name == 'attack':
            t_contact = o.get('contactFrame', 4) * o['duration']
            c.update(count=16, contactFrame=6, duration=round(t_contact / 6, 4), loop=False)
        elif name == 'melee':
            t_contact = o.get('contactFrame', 3) * o['duration']
            c.update(count=12, contactFrame=5, duration=round(t_contact / 5, 4), loop=False)
        elif name == 'hit':
            c.update(count=3, duration=0.07, loop=False)
        elif name == 'death':
            n = 24 if big else 20
            c.update(count=n, duration=0.055 if big else 0.05, impactFrame=round(0.5 * (n - 1)), loop=False)
        elif name == 'deploy':
            c.update(count=6, duration=0.065, loop=False)
        else:
            c.update(count=o['count'], duration=o['duration'])
        c['start'] = start
        start += c['count']
        out[name] = c
    # a channel or rally pose for abilities and spells: rises into it over the first half, then holds (the game
    # loops the held frames, from holdFrom, while the cast lasts)
    out['cast'] = {'start': start, 'count': 12, 'duration': 0.06, 'loop': False, 'holdFrom': 6}
    return out


BLUR_TRAIL = tuple(-0.55 + 0.55 * k / 16 for k in range(16))   # instants in the frame before, oldest first


def render_blurred(sc, path, pose_at, i, trail_objs=None, all_objs=()):
    """Motion blur that keeps the pose readable: the pose at frame i rendered crisp over a soft trail, the average
    of the pose at several instants just before it (premultiplied), faded. A fast weapon leaves a smear behind it
    the way a camera sees one, not a row of copies."""
    tmp = str(path) + '.sub.png'

    def shot(t):
        pose_at(t)
        sc.render.filepath = tmp
        bpy.ops.render.render(write_still=True)
        img = bpy.data.images.load(tmp, check_existing=False)
        w, h = img.size
        px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
        bpy.data.images.remove(img)
        px[..., :3] *= px[..., 3:4]
        return px
    # only what swings leaves a trail: the weapon (or, for claws and fists, the arms); the body stays crisp
    hidden = [o for o in all_objs if trail_objs and o not in trail_objs]
    for o in hidden:
        o.hide_render = True
    trail = None
    for t in BLUR_TRAIL:
        px = shot(i + t)
        trail = px if trail is None else trail + px
    for o in hidden:
        o.hide_render = False
    # a weapon smears strongly; a limb (claws, fists) only faintly, or it reads as ghost arms
    weapon_trail = bool(trail_objs) and any(getattr(o, 'name', '') == 'weapon' for o in trail_objs)
    trail *= (0.6 if weapon_trail else 0.32) / len(BLUR_TRAIL)
    top = shot(i)
    acc = top + trail * (1.0 - top[..., 3:4])       # the crisp pose over the faded trail
    h, w = acc.shape[:2]
    a = acc[..., 3:4]
    acc[..., :3] = np.where(a > 1e-4, acc[..., :3] / np.maximum(a, 1e-4), 0.0)
    out = bpy.data.images.new('blur', w, h, alpha=True)
    out.pixels.foreach_set(np.clip(acc, 0, 1).ravel())
    out.filepath_raw = str(path)
    out.file_format = 'PNG'
    out.save()
    bpy.data.images.remove(out)
    Path(tmp).unlink(missing_ok=True)


def density_units():
    return json.loads((ROOT / 'data/units.json').read_text())['Units']


def old_contract(ident):
    meta = json.loads((REVIEW / 'units' / ident / 'metadata.json').read_text())
    return meta


def old_figure_ratio(ident, meta):
    from PIL import Image  # noqa: F401  (Blender ships no PIL; read with bpy)
    return None


def figure_ratio_bpy(path, area=False):
    """The figure's height in the frame, as a fraction of the frame width; with area, the square root of its
    opaque area as a fraction of the width squared (for wide, low bodies, whose height says little)."""
    img = bpy.data.images.load(str(path), check_existing=False)
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1, :, 3]
    bpy.data.images.remove(img)
    if area:
        return float(np.sqrt((a > 0.5).sum())) / w
    rows = np.nonzero((a > 0.5).any(axis=1))[0]
    return (rows.max() - rows.min()) / w if len(rows) else None


def rig_humanoid(ident, d, lm, paint, img, info):
    """Bones, layer meshes and IK for a standing figure; returns what the clip loop needs."""
    bones = humanoid_bones(lm)
    extra = []
    torso = float(np.linalg.norm(P(lm, 'chin') - P(lm, 'pelvis')))
    line = (info or {}).get('weapon_line')
    side = weapon_side(lm) or 'R'
    if line:
        # the hand nearest the weapon's grip holds it
        g_ = np.array(line['grip'], float)
        side = min((s_ for s_ in 'RL' if P(lm, 'wrist_' + s_) is not None),
                   key=lambda s_: np.linalg.norm(P(lm, 'wrist_' + s_) - g_), default=side)
        wpn = (g_, np.array(line['tip'], float))
    elif side and P(lm, 'weapon_tip') is not None:
        wpn = (P(lm, 'weapon_grip'), P(lm, 'weapon_tip'))
    else:
        wpn = None
    off = 'L' if side == 'R' else 'R'
    two = False
    if wpn is not None and P(lm, 'wrist_' + off) is not None:
        if info and 'grips' in info:
            two = off in info['grips']
        else:
            two = seg_dist(P(lm, 'wrist_' + off)[None, :], wpn[0], wpn[1])[0] < 0.12 * torso
    up = np.array([0.0, -30.0])
    has_arms = P(lm, 'wrist_' + side) is not None and P(lm, 'elbow_' + side) is not None
    bow = wpn is not None and not two and old_contract(ident)['motion']['profile'] == 'bow-draw' and \
        P(lm, 'wrist_' + off) is not None
    if has_arms:
        # Hands are driven, not arms: the weapon hangs from a 'wield' bone on the chest (between the hands if it
        # takes two) and each arm reaches its grip on the weapon by IK; the other hand reaches for a target of its
        # own. Motions then say where the hands go and which way the weapon points, whatever the painted pose.
        w1 = P(lm, 'wrist_' + side)
        w2 = P(lm, 'wrist_' + off)
        if wpn is not None and two:
            pivot = (w1 + w2) / 2
        elif wpn is not None:
            pivot = wpn[0]
        else:
            pivot = w1
        extra.append(('wield', pivot, pivot + up, 'chest', 0, False))
        holder = 'wield'
        if wpn is not None:
            extra.append(('weapon', wpn[0], wpn[1], 'wield', 0.09, True))
            holder = 'weapon'
        extra.append(('grip.' + side, w1, w1 + up / 3, holder, 0, False))
        if w2 is not None:
            if two:
                extra.append(('grip.' + off, w2, w2 + up / 3, 'weapon', 0, False))
            elif bow:
                extra.append(('string', w2, w2 + up, 'chest', 0, False))
            else:
                extra.append(('reach.' + off, w2, w2 + up, 'chest', 0, False))
    elif wpn is not None:
        extra.append(('weapon', wpn[0], wpn[1], 'hand.' + side, 0.09, True))
    if P(lm, 'offhand_item') is not None and P(lm, 'wrist_' + off) is not None and not two:
        extra.append(('offhand', P(lm, 'wrist_' + off), P(lm, 'offhand_item'), 'hand.' + off, 0.3, True))
    arm = build_armature(paint, bones, extra)
    layer_obs = []
    if info:
        for k, name in enumerate(info['order']):
            li, lpx = load_rgba(d / f'{name}.png')
            ob, pix = build_mesh(paint, li, lpx[..., 3], name, depth=0.01 * k)
            allowed = LAYER_BONES.get(name) or (('offhand', 'hand.' + off) if name == 'offhand' else None)
            skin(ob, arm, pix, bones + extra, torso, allowed)
            layer_obs.append((name, ob))
    else:
        ob, pix = build_mesh(paint, img, paint.alpha)
        skin(ob, arm, pix, bones + extra, torso)
        layer_obs.append(('master', ob))
    targets = [(s_, t_) for s_, t_ in ((side, 'grip.' + side), (off, 'grip.' + off), (off, 'string'), (off, 'reach.' + off))
               if t_ in arm.pose.bones and 'forearm.' + s_ in arm.pose.bones]
    for s_, tgt_ in targets:
        ik = arm.pose.bones['forearm.' + s_].constraints.new('IK')
        ik.target, ik.subtarget, ik.chain_count = arm, tgt_, 2
        for b_ in ('forearm.' + s_, 'upper_arm.' + s_):
            pb = arm.pose.bones[b_]
            pb.lock_ik_x = pb.lock_ik_y = True      # bend only in the picture plane
            pb.ik_stretch = 0.08                     # a painted arm may give a little rather than let go
    return dict(arm=arm, layer_obs=layer_obs, side=side, two=two, wpn=wpn, line=line,
                make_anim=lambda profile: Animator(arm, paint, lm, profile, paint.gy - paint.y0, side, two))


def build(ident, frames=None, out_root=OUT):
    t0 = time.time()
    sc = reset()
    d = PUPPET / ident
    lm = json.loads((d / 'landmarks.json').read_text())
    img, px = load_rgba(d / 'master.png')
    paint = Paint(px[..., 3], lm)
    info = json.loads((d / 'layers.json').read_text()) if (d / 'layers.json').exists() else None
    if lm.get('kind', 'humanoid') == 'humanoid':
        rig = rig_humanoid(ident, d, lm, paint, img, info)
    else:
        import creatures
        rig = creatures.rig(ident, d, lm, paint, img, info)
    arm, layer_obs = rig['arm'], rig['layer_obs']
    side, two, wpn, line = rig.get('side'), rig.get('two', False), rig.get('wpn'), rig.get('line')
    cam, W, H, cx, cz = camera(paint, sc)
    meta = old_contract(ident)
    # same on-screen size as the unit had: drawScale scales the frame; keep the figure's height on screen
    # people keep their height on screen; beasts, mounts, machines and blobs their bulk (painted afresh, a nest or
    # a serpent can be far wider or flatter than the old model, and matching its height would shrink it)
    by_area = lm.get('kind', 'humanoid') != 'humanoid'
    old_ratio = figure_ratio_bpy(REVIEW / 'units' / ident / f"{meta['animations']['idle']['start']:03}.png", by_area)
    new_ratio = (float(np.sqrt((paint.alpha > 0.5).sum())) / PPU if by_area else paint.fig_h) / W
    draw_scale = meta['drawScale'] * (old_ratio / new_ratio) if old_ratio else meta['drawScale']
    # render no finer than pack.py keeps (the battle's target density, supersampled): same framing, fewer pixels
    unit = next((u for u in density_units() if u['Id'] == ident), {})
    want = density.puppet_density(unit) * density.SUPERSAMPLE * density.frame_canvas_width(unit, draw_scale) * 1.1
    px_w = int(min(sc.render.resolution_x, max(384, math.ceil(want / 4) * 4)))
    sc.render.resolution_x, sc.render.resolution_y = px_w, int(round(px_w * 1.25))
    big = unit.get('VisualClass') in ('boss', 'siegetower') or meta['animations'].get('death', {}).get('count', 0) >= 16
    clips = new_contract(meta['animations'], big)
    profile = meta['motion']['profile']
    anim = rig['make_anim'](profile)
    anim.vclass = unit.get('VisualClass', '')
    body_bone = getattr(anim, 'body_bone', 'chest')
    dclip = clips.get('death', {})
    anim.death_motion = dclip.get('motion')
    try:
        from roster.deaths import DEATHS
        wd = DEATHS.get(ident, {}).get('drops', {}).get('weapon')
    except Exception:  # noqa: BLE001
        wd = None
    if wd:
        anim.death_release = float(wd.get('release', 0.3)) * 0.9 + 0.05
        anim.toss = tuple(wd.get('toss', (0.4, 0.0, 0.2)))
        sp = wd.get('spin', 0)
        anim.spin = float(sp[0][0] if isinstance(sp, (list, tuple)) and sp and isinstance(sp[0], (list, tuple))
                          else sp if isinstance(sp, (int, float)) else 0) * 0.5
    dm = (clips.get('death', {}).get('motion', '') or '')
    forward = any(k in dm for k in ('forward', 'dive', 'roar'))
    order = sorted(clips.items(), key=lambda kv: kv[1]['start'])
    out = Path(out_root) / ident
    out.mkdir(parents=True, exist_ok=True)
    total = sum(c['count'] for _, c in order)
    want = None if frames is None else set(frames)
    contact_pt = None
    impact_pt = None
    drop_state = {}
    # the weapon's centre, as a distance along it from its grip (the butt runs on behind the hand)
    wpn_center = 0.0
    if 'weapon' in arm.pose.bones:
        Lb = arm.data.bones['weapon'].length
        bt = np.array(line['butt'], float) if line else wpn[0]
        wpn_center = (Lb - float(np.linalg.norm(bt - wpn[0])) / PPU) / 2
    stride_x = {}

    def pose(name, c, i):
        n = c['count']
        anim.clear()
        if name == 'idle':
            anim.idle(i, n)
        elif name == 'walk':
            anim.walk(i, n)
        elif name == 'attack':
            anim.attack(i, n, c.get('contactFrame', 4))
        elif name == 'melee':
            anim.melee(i, n, c.get('contactFrame', 3))
        elif name == 'hit':
            anim.hit(i, n)
        elif name == 'deploy':
            anim.deploy(i, n)
        elif name == 'cast':
            anim.cast(i, n, c.get('holdFrom', 6))
        elif name == 'death':
            anim.death(i, n, c.get('impactFrame', max(1, n // 2)), forward)
        bpy.context.view_layer.update()

    foot = next((b for b in ('foot.R', 'shin.R', 'fl.lower', 'hl.lower') if b in arm.pose.bones), None)
    names_ = {n_: o for n_, o in layer_obs}
    trail_objs = [names_[k] for k in ('weapon',) if k in names_] or \
        [o for n_, o in layer_obs if n_.startswith('arm_')] or None
    for name, c in order:
        n = c['count']
        for i in range(n):
            f = c['start'] + i
            pose(name, c, i)
            if name == 'walk' and foot and i in (0, n // 2):
                stride_x[i] = (arm.matrix_world @ arm.pose.bones[foot].tail).x
            if name == 'death':
                low = min(float(ground_fix(o, arm)[:, 2].min()) for n_, o in layer_obs if n_ not in ('weapon', 'offhand'))
                r = arm.pose.bones['root']
                # world z offset -> root local (the root bone points up: local Y = world Z)
                r.location = (r.location[0], r.location[1] - low + getattr(anim, 'air', 0.0) * anim.H, 0)
                bpy.context.view_layer.update()
                if 'weapon' in arm.pose.bones:     # after the body is grounded: the weapon is placed in the world
                    drop_state['clip_s'] = c['duration'] * (n - 1)
                    drop_weapon(arm, anim, layer_obs, wpn_center, forward, drop_state)
            if name == 'attack' and i == c.get('contactFrame', 4) and 'weapon' in arm.pose.bones:
                tip = arm.matrix_world @ arm.pose.bones['weapon'].tail
                contact_pt = norm_point(tip, W, H, cx, cz)
            if name == 'death' and i == c.get('impactFrame', 0):
                ch = arm.matrix_world @ arm.pose.bones[body_bone].head
                impact_pt = norm_point(ch, W, H, cx, cz)
            if want is None or (f + 1) in want:
                cf = c.get('contactFrame', -99)
                if name in ('attack', 'melee') and cf - 1 <= i <= cf + 1:
                    render_blurred(sc, out / f'{f:03}.png', lambda x, name=name, c=c: pose(name, c, x), i,
                                   trail_objs, [o for _, o in layer_obs])
                    pose(name, c, i)
                else:
                    sc.render.filepath = str(out / f'{f:03}.png')
                    bpy.ops.render.render(write_still=True)
    anim.clear()
    bpy.context.view_layer.update()
    body = norm_point(arm.matrix_world @ arm.pose.bones[body_bone].head, W, H, cx, cz)
    if contact_pt is None:
        cb = getattr(anim, 'contact_bone', None)
        hand = arm.pose.bones.get(cb) if cb else arm.pose.bones.get('hand.' + (side or 'R'))
        contact_pt = norm_point(arm.matrix_world @ (hand.tail if hand else arm.pose.bones[body_bone].tail), W, H, cx, cz)
    gx_n = (0.0 - (cx - W / 2)) / W
    gy_n = (cz + H / 2) / H
    new_clips = json.loads(json.dumps(clips))
    if 'death' in new_clips and impact_pt is not None:
        new_clips['death']['impactPoint'] = impact_pt
    if 'walk' in new_clips and getattr(anim, 'walk_stride', 0):
        # ground covered in one walk cycle, as a fraction of the frame width: the game plays the cycle at the speed
        # that keeps the planted foot still on the ground
        new_clips['walk']['stride'] = round(anim.walk_stride / W, 5)
    elif 'walk' in new_clips and len(stride_x) == 2:
        new_clips['walk']['stride'] = round(abs(stride_x[0] - stride_x[new_clips['walk']['count'] // 2]) * 2 / W, 5)
    out_meta = {
        'frameWidth': sc.render.resolution_x, 'frameHeight': sc.render.resolution_y,
        'drawScale': draw_scale, 'animations': new_clips, 'assetId': ident, 'title': meta.get('title', ident),
        'source': f'art/remaster/puppet/build.py ({ident})',
        'anchorY': round(gy_n, 6), 'anchorX': round(gx_n, 6),
        'motion': {'revision': 'puppet-v2', 'profile': profile, 'body': body, 'contact': contact_pt},
    }
    (out / 'metadata.json').write_text(json.dumps(out_meta, indent=2) + '\n')
    if want is None:
        (out / 'complete.json').write_text(json.dumps({'id': ident, 'frames': total, 'revision': 'puppet-v2'}) + '\n')
    print(f'PUPPET_COMPLETE {ident} {time.time() - t0:.1f}s frames={total} drawScale={draw_scale:.3f} '
          f'two_handed={two}', flush=True)


if __name__ == '__main__':
    a = args()
    frames = [int(x) for x in a.frames.split(',')] if a.frames else None
    for ident in a.ids.split(','):
        try:
            build(ident, frames, a.out)
        except Exception:
            traceback.print_exc()
            print('PUPPET_FAILED ' + ident, flush=True)
