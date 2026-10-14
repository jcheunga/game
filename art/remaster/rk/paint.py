"""Paint-over projection: a unit's stance painting (artifacts/remaster/paint/<id>/painted.png, made by
art/remaster/paintover.py from the stance frame) projected onto its rigged model from the battle camera.

Every surface the camera sees in the stance pose takes its colour from the painting (emission, so the painted light
and texture come through unchanged); surfaces hidden in that pose borrow the paint of the nearest visible point on the
same piece, slightly darker. Pieces the camera cannot see at all keep their procedural materials and the studio light. The UVs are fixed at the stance frame, so the paint rides on the
animated mesh through every clip.
"""
import json
import math

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from bpy_extras.object_utils import world_to_camera_view

from . import core

PAINT = core.REVIEW / 'paint'
GREY = np.array([221, 217, 209], dtype=np.float32) / 255.0


# ------------------------------------------------------------------ images (numpy, rows top-down)
def _read(path):
    img = bpy.data.images.load(str(path), check_existing=False)
    w, h = img.size
    a = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, img.channels)[::-1]
    bpy.data.images.remove(img)
    return a


def _write(arr, name, path=None):
    h, w = arr.shape[:2]
    rgba = np.ones((h, w, 4), dtype=np.float32)
    rgba[..., :arr.shape[2]] = arr[..., :4]
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.pixels.foreach_set(rgba[::-1].ravel())
    if path is not None:
        img.filepath_raw = str(path)
        img.file_format = 'PNG'
        img.save()
    return img


def _box_blur(a, r):
    """Separable box blur (edges clamped) of an (h, w[, c]) array."""
    if r < 1:
        return a
    out = a
    for ax in (0, 1):
        pad = [(0, 0)] * out.ndim
        pad[ax] = (r + 1, r)
        p = np.pad(out, pad, mode='edge')
        c = np.cumsum(p, axis=ax)
        n = out.shape[ax]
        hi = np.take(c, np.arange(2 * r + 1, 2 * r + 1 + n), axis=ax)
        lo = np.take(c, np.arange(0, n), axis=ax)
        out = (hi - lo) / (2 * r + 1)
    return out


def _fill(rgb, mask, radii=(2, 5, 12, 30, 80)):
    """Colours bled outward from the masked pixels into the rest (so projected edges never sample background)."""
    out = rgb * mask[..., None]
    have = mask.astype(np.float32)
    for r in radii:
        w = _box_blur(have, r)
        c = _box_blur(out, r)
        fill = c / np.maximum(w, 1e-6)[..., None]
        take = (have < 0.999) & (w > 1e-4)
        out = np.where(take[..., None], out * have[..., None] + fill * (1 - have[..., None]), out)
        have = np.maximum(have, (w > 1e-4).astype(np.float32))
    return out


def _mask_of(painted):
    return (np.abs(painted[..., :3] - GREY).sum(axis=2) > 0.1).astype(np.float32)


def _warp(img, s, tx, ty):
    """img sampled so that out(x, y) = img((x - tx) / s, (y - ty) / s), bilinear."""
    h, w = img.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    sx, sy = (xs - tx) / s, (ys - ty) / s
    x0, y0 = np.clip(np.floor(sx).astype(int), 0, w - 2), np.clip(np.floor(sy).astype(int), 0, h - 2)
    fx, fy = np.clip(sx - x0, 0, 1), np.clip(sy - y0, 0, 1)
    if img.ndim == 2:
        fx2, fy2 = fx, fy
    else:
        fx2, fy2 = fx[..., None], fy[..., None]
    a = img[y0, x0] * (1 - fx2) + img[y0, x0 + 1] * fx2
    b = img[y0 + 1, x0] * (1 - fx2) + img[y0 + 1, x0 + 1] * fx2
    return a * (1 - fy2) + b * fy2


def _align(painted_mask, guide_mask):
    """Similarity (scale, shift) taking the painting's silhouette onto the model's, by moments then a small
    IoU search on downsampled masks."""
    def moments(m):
        ys, xs = np.nonzero(m > 0.5)
        return len(xs), xs.mean(), ys.mean()
    na, ax_, ay_ = moments(painted_mask)
    nb, bx_, by_ = moments(guide_mask)
    s0 = float(np.clip(np.sqrt(nb / max(na, 1)), 0.88, 1.12))
    tx0, ty0 = bx_ - s0 * ax_, by_ - s0 * ay_
    k = 4
    pm, gm = painted_mask[::k, ::k], guide_mask[::k, ::k] > 0.5
    best = (-1, s0, tx0, ty0)
    for ds in (-0.03, -0.015, 0, 0.015, 0.03):
        s = s0 * (1 + ds)
        for dx in range(-12, 13, 3):
            for dy in range(-12, 13, 3):
                tx, ty = tx0 + dx, ty0 + dy
                w = _warp(pm, s, tx / k, ty / k) > 0.5
                iou = (w & gm).sum() / max(1, (w | gm).sum())
                if iou > best[0]:
                    best = (iou, s, tx, ty)
    return best


def texture(ident, prefix=''):
    """The painting aligned to the model's silhouette and bled past its edges (saved as <prefix>texture.png).
    prefix '' is the front (stance) painting, 'back_' the painting of the stance seen from behind."""
    d = PAINT / ident
    painted = _read(d / f'{prefix}painted.png')[..., :3]
    guide = _read(d / f'{prefix}guide_alpha.png')[..., 0]
    if painted.shape[:2] != guide.shape:
        raise ValueError(f'{ident}: painting {painted.shape[:2]} vs guide {guide.shape}')
    iou, s, tx, ty = _align(_mask_of(painted), guide)
    aligned = _warp(painted, s, tx, ty)
    mask = _warp(_mask_of(painted), s, tx, ty) > 0.5
    filled = _fill(aligned, mask.astype(np.float32))
    if not prefix:
        # the painting cut out by the model's own silhouette (icons and figures are cropped from it)
        _write(np.concatenate([filled, guide[..., None]], axis=2), f'{ident} cutout', d / 'cutout.png')
    (d / f'{prefix}align.json').write_text(json.dumps({'iou': round(float(iou), 4), 'scale': round(float(s), 4),
                                                       'shift': [round(float(tx), 1), round(float(ty), 1)]}) + '\n')
    return _write(filled, f'{ident} {prefix}painting', d / f'{prefix}texture.png'), float(iou)


def has_back(ident):
    d = PAINT / ident
    return (d / 'back_painted.png').exists() and (d / 'back_crop.json').exists()


def back_camera(cam, coll):
    """The battle camera turned 180 degrees about the vertical through the origin: it sees the stance from behind
    exactly as the back guide was rendered (the character turned 180 degrees under the battle camera)."""
    from mathutils import Matrix
    b = cam.copy()
    b.data = cam.data.copy()
    b.name = 'Back paint camera'
    coll.objects.link(b)
    b.matrix_world = Matrix.Rotation(math.pi, 4, 'Z') @ cam.matrix_world
    return b


# ------------------------------------------------------------------ projection
def _camera_map(scene, cam):
    """Affine world -> (u, v) map of the (orthographic) camera, fitted to world_to_camera_view."""
    pts = [Vector((0, 0, 0)), Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))]
    uv = [world_to_camera_view(scene, cam, p) for p in pts]
    o = np.array([uv[0].x, uv[0].y])
    A = np.array([[uv[i].x - uv[0].x, uv[i].y - uv[0].y] for i in (1, 2, 3)])   # rows: d(u,v)/dx,dy,dz
    return lambda P: P @ A + o


def _emissive(mat):
    if not mat or not mat.use_nodes:
        return False
    for n in mat.node_tree.nodes:
        if n.type == 'EMISSION' and n.inputs['Strength'].default_value > 0.5:
            return True
        if n.type == 'BSDF_PRINCIPLED':
            st = n.inputs.get('Emission Strength')
            if st is not None and st.default_value > 0.5 and (st.is_linked or any(n.inputs['Emission Color'].default_value[:3])):
                return True
    return False


def _painted_material(mat, tex, cache, large=False, back_tex=None):
    """The material with the painting mixed over it. Small glowing parts (eyes, flames, gems) keep their own glow;
    a large glowing body (a wisp) takes the painting, or it renders as a blown-out white shape."""
    if mat is None or (_emissive(mat) and not large):
        return mat
    key = (mat.name, large and _emissive(mat))
    if key in cache:
        return cache[key]
    pm = mat.copy()
    pm.name = mat.name + ' (painted)'
    nt = pm.node_tree
    out = next((n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL' and n.is_active_output), None) or \
        next((n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL'), None)
    if out is None or not out.inputs['Surface'].is_linked:
        cache[key] = mat
        return mat
    orig = out.inputs['Surface'].links[0].from_socket
    uvn = nt.nodes.new('ShaderNodeUVMap')
    uvn.uv_map = 'Paint'
    im = nt.nodes.new('ShaderNodeTexImage')
    im.image = tex
    im.interpolation = 'Linear'
    im.extension = 'EXTEND'
    nt.links.new(uvn.outputs['UV'], im.inputs['Vector'])
    em = nt.nodes.new('ShaderNodeEmission')
    if back_tex is None:
        nt.links.new(im.outputs['Color'], em.inputs['Color'])
    else:
        buv = nt.nodes.new('ShaderNodeUVMap')
        buv.uv_map = 'PaintBack'
        bim = nt.nodes.new('ShaderNodeTexImage')
        bim.image = back_tex
        bim.interpolation = 'Linear'
        bim.extension = 'EXTEND'
        nt.links.new(buv.outputs['UV'], bim.inputs['Vector'])
        side = nt.nodes.new('ShaderNodeAttribute')
        side.attribute_name = 'paint_back'
        mixc = nt.nodes.new('ShaderNodeMix')
        mixc.data_type = 'RGBA'
        nt.links.new(side.outputs['Fac'], mixc.inputs['Factor'])
        nt.links.new(im.outputs['Color'], mixc.inputs[6])
        nt.links.new(bim.outputs['Color'], mixc.inputs[7])
        nt.links.new(mixc.outputs[2], em.inputs['Color'])
    shade = nt.nodes.new('ShaderNodeAttribute')
    shade.attribute_name = 'paint_shade'
    nt.links.new(shade.outputs['Fac'], em.inputs['Strength'])
    vis = nt.nodes.new('ShaderNodeAttribute')
    vis.attribute_name = 'paint_vis'
    mix = nt.nodes.new('ShaderNodeMixShader')
    nt.links.new(vis.outputs['Fac'], mix.inputs['Fac'])
    nt.links.new(orig, mix.inputs[1])
    nt.links.new(em.outputs['Emission'], mix.inputs[2])
    nt.links.new(mix.outputs['Shader'], out.inputs['Surface'])
    # painted surfaces carry their own light: no displacement wobble under the paint
    if out.inputs.get('Displacement') is not None and out.inputs['Displacement'].is_linked:
        nt.links.remove(out.inputs['Displacement'].links[0])
    cache[key] = pm
    return pm


def project(scene, cam, ident, frame=1, eps=0.01, back_cam=None):
    """Project the unit's paintings onto every mesh at `frame` (the stance): the front painting from `cam`, and the
    back painting (when given `back_cam`) onto what the front cannot see. Points neither camera sees borrow the
    paint of the nearest point that one of them does see on the same piece, a little darker."""
    crop = json.loads((PAINT / ident / 'crop.json').read_text())
    x0, y0, x1, y1 = crop['box']
    W, H = crop['frame']
    tex, iou = texture(ident)
    layers = [(cam, crop)]
    back_tex = None
    if back_cam is not None:
        bcrop = json.loads((PAINT / ident / 'back_crop.json').read_text())
        back_tex, biou = texture(ident, 'back_')
        layers.append((back_cam, bcrop))
    scene.frame_set(frame)
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    cmap = _camera_map(scene, cam)
    maps = [_camera_map(scene, c) for c, _ in layers]
    fwds = []
    for c, _ in layers:
        f_ = np.array(c.matrix_world.to_3x3() @ Vector((0, 0, -1)))
        fwds.append(f_ / np.linalg.norm(f_))
    objs = [o for o in scene.objects if o.type == 'MESH' and not o.hide_render and not o.name.startswith('_')
            and not o.particle_systems]
    world, verts, faces, off = {}, [], [], 0
    for o in objs:
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        n = len(me.vertices)
        co = np.empty(n * 3)
        me.vertices.foreach_get('co', co)
        M = np.array(ev.matrix_world)
        wco = co.reshape(-1, 3) @ M[:3, :3].T + M[:3, 3]
        if n == len(o.data.vertices):
            world[o] = wco
        loops = np.empty(len(me.loops), dtype=np.int64)
        me.loops.foreach_get('vertex_index', loops)
        starts = np.empty(len(me.polygons), dtype=np.int64)
        me.polygons.foreach_get('loop_start', starts)
        totals = np.empty(len(me.polygons), dtype=np.int64)
        me.polygons.foreach_get('loop_total', totals)
        for s_, t_ in zip(starts, totals):
            faces.append([int(i) + off for i in loops[s_:s_ + t_]])
        verts.append(wco)
        off += n
        ev.to_mesh_clear()
    allv = np.concatenate(verts)
    fin = allv[np.isfinite(allv).all(axis=1)]
    figure_h = float(fin[:, 2].max() - fin[:, 2].min()) if len(fin) else 1.0
    tree = BVHTree.FromPolygons(allv.tolist(), faces, all_triangles=False)
    reach = 20.0
    cache, painted_objs = {}, 0
    covered = [0, 0, 0]   # seen from the front, from the back, borrowed
    for o, wco in world.items():
        n = len(wco)
        ok = np.isfinite(wco).all(axis=1)
        # visibility per layer: does a ray from that camera's side reach this vertex first?
        seen_by = []
        for fwd in fwds:
            vis = np.zeros(n, dtype=bool)
            F = Vector(fwd.tolist())
            for i in np.nonzero(ok)[0]:
                p = wco[i]
                hit = tree.ray_cast(Vector((p - fwd * reach).tolist()), F, reach + 1.0)
                vis[i] = hit[0] is not None and hit[3] >= reach - eps
            seen_by.append(vis)
        layer = np.full(n, -1)
        for li_ in range(len(seen_by) - 1, -1, -1):   # the front wins where both see a point
            layer[seen_by[li_]] = li_
        shade = np.ones(n, dtype=np.float32)
        src = np.arange(n)
        seen = np.nonzero(layer >= 0)[0]
        hidden = np.nonzero((layer < 0) & ok)[0]
        paint_on = np.ones(n, dtype=np.float32)
        if len(seen) >= max(8, 0.02 * n):
            if len(hidden):
                from mathutils.kdtree import KDTree
                kd = KDTree(len(seen))
                for j, i in enumerate(seen):
                    kd.insert(Vector(wco[i].tolist()), j)
                kd.balance()
                for i in hidden:
                    _, j, _ = kd.find(Vector(wco[i].tolist()))
                    if j is not None:
                        src[i] = seen[j]
                shade[hidden] = 0.72
            layer = layer[src]
            layer[layer < 0] = 0
        else:
            paint_on[:] = 0.0          # a piece no camera sees keeps its own material
            layer[:] = 0
        covered[0] += int((layer[seen] == 0).sum())
        covered[1] += int((layer[seen] == 1).sum())
        covered[2] += len(hidden)
        me = o.data
        li = np.empty(len(me.loops), dtype=np.int64)
        me.loops.foreach_get('vertex_index', li)
        for k, (c, cr) in enumerate(layers):
            bx0, by0, bx1, by1 = cr['box']
            bw, bh = cr['frame']
            uv = maps[k](np.nan_to_num(wco[src]))
            tu = (uv[:, 0] * bw - bx0) / (bx1 - bx0)
            tv = 1 - ((1 - uv[:, 1]) * bh - by0) / (by1 - by0)
            name = 'Paint' if k == 0 else 'PaintBack'
            uvl = me.uv_layers.get(name) or me.uv_layers.new(name=name)
            uvl.data.foreach_set('uv', np.stack([tu[li], tv[li]], axis=1).astype(np.float32).ravel())
        for name, vals in (('paint_vis', paint_on), ('paint_shade', shade), ('paint_back', (layer == 1).astype(np.float32))):
            attr = me.attributes.get(name) or me.attributes.new(name, 'FLOAT', 'POINT')
            attr.data.foreach_set('value', vals)
        span = float(np.linalg.norm(wco[ok].max(0) - wco[ok].min(0))) if ok.any() else 0.0
        large = span > 0.3 * figure_h
        for slot in o.material_slots:
            slot.material = _painted_material(slot.material, tex, cache, large=large, back_tex=back_tex)
        painted_objs += 1
    tot = max(1, sum(covered))
    (PAINT / ident / 'coverage.json').write_text(json.dumps({'front': round(covered[0] / tot, 4),
                                                             'back': round(covered[1] / tot, 4),
                                                             'borrowed': round(covered[2] / tot, 4)}) + '\n')
    # where the head sits in the painting (paintover.py centres bust icons on it)
    size = crop.get('size', 1024)
    for arm in sorted((o for o in scene.objects if o.type == 'ARMATURE' and 'head' in o.pose.bones),
                      key=lambda o: (not o.name.startswith(ident + ' '), o.name)):
        pb = arm.pose.bones['head']
        c = np.array(arm.matrix_world @ ((pb.head + pb.tail) * 0.5))
        u, v = cmap(c[None, :])[0]
        hx, hy = (u * W - x0) / (x1 - x0) * size, ((1 - v) * H - y0) / (y1 - y0) * size
        (PAINT / ident / 'head.json').write_text(json.dumps({'x': round(float(hx), 1), 'y': round(float(hy), 1),
                                                            'armature': arm.name}) + '\n')
        break
    # the painting already carries its light: show its colours as painted
    scene.view_settings.view_transform = 'Standard'
    scene.view_settings.look = 'None'
    scene.view_settings.exposure = 0.0
    print(f'PAINT_PROJECTED {ident} objects={painted_objs} iou={iou:.3f} back={back_cam is not None} '
          f'borrowed={covered[2] / tot:.1%}', flush=True)
    return iou
