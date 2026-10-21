#!/usr/bin/env python3
"""Objective checks on every frame of a puppet's clips, measured on the posed rig (nothing is rendered).

  blender --background --factory-startup --python art/remaster/puppet/motion_check.py -- --ids a,b [--out dir]
  python3 art/remaster/puppet/check_report.py [dir]          (the pass criteria)

build.build() poses every frame as for a render; at each frame, instead of rendering, this splats the deformed
layers back to front onto a grid (300 cells per figure height), keeping for every cell the topmost painted sample,
and measures:
  smear     the cells whose topmost paint comes from a mesh face stretched past 3x its painted size (a tear, a
            ribbon, a fan): a count, a share of the figure's visible paint (smear_pct) and an area in battle
            pixels (smear_bpx; the render's width is the unit's battle canvas). by_pair names the two bones whose
            pull differs most across the worst faces (what tears)
  low       the lowest painted cell of each layer (lows) and of the body and its clothes (low), in figure heights
            above the road: negative sinks, positive floats
  slip      walk only: how far a planted foot (its tip within 2% of the figure's height of the road in two frames
            running) moves against the ground, which scrolls back by the stride over the cycle; as a share of the
            ground's motion per frame
Writes <out>/<id>.json with per-clip summaries and every frame's measurements.
MC_DUMP=clip:frame:path.npz saves that frame's raster and smear cells (scratch overlay tools read it).
"""
import json
import os
import sys
import traceback
from pathlib import Path

import bpy
import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import build as B  # noqa: E402

VIS = .3            # alpha that counts as visible paint
RIGID = ('weapon', 'arrow', 'offhand')
RES = 300          # raster cells per figure height for what shows
MIN_SHOWN = 6       # a stretched face counts when this many of its cells are the topmost paint
SMEAR = 3.0        # a face stretched past this shows its paint smeared
DUMP = tuple(os.environ['MC_DUMP'].split(':')) if os.environ.get('MC_DUMP') else None   # (clip, frame, path)


class Probe:
    def __init__(self):
        self.cache = {}
        self.pairs = {}
        self.rows = []
        self.last_feet = {}

    def layer_data(self, ob):
        """Rest coordinates, faces, and each vertex's alpha in the layer's own texture."""
        if ob.name in self.cache:
            return self.cache[ob.name]
        me = ob.data
        rest = np.array([v.co[:] for v in me.vertices])
        faces = np.array([list(p.vertices) for p in me.polygons], dtype=np.int64)
        img = next(n.image for n in ob.active_material.node_tree.nodes if n.type == 'TEX_IMAGE')
        w, h = img.size
        px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[..., 3]
        uv = np.zeros((len(me.vertices), 2))
        luv = np.empty(len(me.loops) * 2)
        me.uv_layers[0].data.foreach_get('uv', luv)
        lv = np.empty(len(me.loops), dtype=np.int64)
        me.loops.foreach_get('vertex_index', lv)
        uv[lv] = luv.reshape(-1, 2)
        xs = np.clip((uv[:, 0] * w).astype(int), 0, w - 1)
        ys = np.clip((uv[:, 1] * h).astype(int), 0, h - 1)
        # a vertex's visibility: the most opaque texel in the 4 px cell around it
        a = np.zeros(len(xs))
        for dx in (-2, 0, 2):
            for dy in (-2, 0, 2):
                a = np.maximum(a, px[np.clip(ys + dy, 0, h - 1), np.clip(xs + dx, 0, w - 1)])
        self.cache[ob.name] = (rest, faces, a, np.stack([uv[:, 0] * w, uv[:, 1] * h], axis=1), px)
        return self.cache[ob.name]

    def measure(self, ctx):
        name, i, anim, arm, layer_obs = ctx['name'], ctx['i'], ctx['anim'], ctx['arm'], ctx['layer_obs']
        H = float(anim.H)
        dg = bpy.context.evaluated_depsgraph_get()
        posed = []
        for k, (lname, ob) in enumerate(layer_obs):
            if ob.hide_render:
                continue
            rest, faces, a, txy, px = self.layer_data(ob)
            ev = ob.evaluated_get(dg)
            me = ev.to_mesh()
            co = np.empty(len(me.vertices) * 3)
            me.vertices.foreach_get('co', co)
            ev.to_mesh_clear()
            co = co.reshape(-1, 3)
            M = np.array(ob.matrix_world)
            wco = co @ M[:3, :3].T + M[:3, 3]
            if not len(faces):
                continue
            fvis = (a[faces] > VIS).sum(axis=1) >= 3
            posed.append((k, lname, rest, faces[fvis], wco, co, txy, px))
        # what shows: the layers splatted back to front (draw order is layer order) onto a grid; each cell keeps the
        # topmost layer. Faces are sampled bilinearly from their four corners
        res = H / RES
        allx = np.concatenate([w[f.ravel(), 0] for _, _, _, f, w, _, _, _ in posed if len(f)])
        allz = np.concatenate([w[f.ravel(), 2] for _, _, _, f, w, _, _, _ in posed if len(f)])
        x0, z1 = allx.min() - res, allz.max() + res
        gw, gh = int((allx.max() - x0) / res) + 3, int((z1 - allz.min()) / res) + 3
        top = np.full((gh, gw), -1, np.int32)
        topf = np.full((gh, gw), -1, np.int64)       # the face on top: layers in draw order, faces in index order
        base_id, ids = 0, {}

        def cells(w, f, n, txy, px):
            """The grid cells a face's painted (opaque) samples land on: n x n samples per face, placed bilinearly
            between its posed corners and read from its texture between its painted corners."""
            u = (np.arange(n) + .5) / n
            U, V = np.meshgrid(u, u)
            U, V = U.ravel()[None, :, None], V.ravel()[None, :, None]

            def lerp(q):
                return (q[:, None, 0] * (1 - U) * (1 - V) + q[:, None, 1] * U * (1 - V) + q[:, None, 2] * U * V
                        + q[:, None, 3] * (1 - U) * V)
            pts = lerp(w[f][..., [0, 2]])
            tex = lerp(txy[f])
            hh, ww = px.shape
            opaque = px[np.clip(tex[..., 1].astype(int), 0, hh - 1), np.clip(tex[..., 0].astype(int), 0, ww - 1)] > VIS
            cx = np.clip(((pts[..., 0] - x0) / res).astype(int), 0, gw - 1)
            cz = np.clip(((z1 - pts[..., 1]) / res).astype(int), 0, gh - 1)
            return cz, cx, opaque
        paint_low = {}
        for k, lname, rest, f, w, co, txy, px in posed:
            ids[k] = base_id
            if len(f):
                cz, cx, op = cells(w, f, 6, txy, px)
                if op.any():
                    paint_low[lname] = round(float(z1 - (cz[op].max() + 1) * res) / H, 4)
                fid = np.broadcast_to(base_id + np.arange(len(f))[:, None], cz.shape)
                top[cz[op], cx[op]] = k
                topf[cz[op], cx[op]] = fid[op]
            base_id += len(f)
        worst, worst_layer, where = 0.0, None, None
        self.layer_smear, self.pair_smear = {}, {}
        smear = set()
        for k, lname, rest, f, w, co, txy, px in posed:
            if lname in RIGID or not len(f):
                continue
            r0, p1 = rest[f], co[f]
            l0 = np.linalg.norm(r0 - np.roll(r0, 1, axis=1), axis=2)
            l1 = np.linalg.norm(p1 - np.roll(p1, 1, axis=1), axis=2)
            ratio = (l1 / np.maximum(l0, 1e-9)).max(axis=1)
            cand = np.nonzero(ratio > SMEAR)[0]
            for c0 in range(0, len(cand), 2000):
                cc = cand[c0:c0 + 2000]
                cz, cx, op = cells(w, f[cc], 12, txy, px)
                own = op & (topf[cz, cx] == (ids[k] + cc)[:, None])
                for j in range(len(cc)):
                    if own[j].any():
                        got = set(zip(cz[j][own[j]].tolist(), cx[j][own[j]].tolist()))
                        smear |= got
                        if len(got) >= MIN_SHOWN:
                            self.layer_smear[lname] = self.layer_smear.get(lname, 0) + len(got)
                            pair = self.face_pair(dict(layer_obs)[lname], f[cc[j]])
                            self.pair_smear[pair] = self.pair_smear.get(pair, 0) + len(got)
                        if ratio[cc[j]] > worst and len(got) >= MIN_SHOWN:
                            worst, worst_layer = float(ratio[cc[j]]), lname
                            where = {'rest': [round(float(v), 2) for v in (r0[cc[j]][0][0], r0[cc[j]][0][2])],
                                     'shown_cells': len(got)}
        figure = int((top >= 0).sum())
        # in battle pixels: the render's width W (armature units) is the unit's canvas in battle pixels
        bpx = B.density.frame_canvas_width(ctx['unit'], ctx['draw_scale']) / ctx['W']
        cell_bpx2 = (res * bpx) ** 2
        fig_bpx = H * bpx
        if DUMP and name == DUMP[0] and i == int(DUMP[1]):
            sm = np.array(sorted(smear)) if smear else np.zeros((0, 2), int)
            np.savez(DUMP[2], top=top, smear=sm, x0=x0, z1=z1, res=res, W=ctx['W'], Hc=ctx['H'], cx=ctx['cx'], cz=ctx['cz'])
        top_pairs = sorted(self.pair_smear.items(), key=lambda kv: -kv[1])[:3]
        # the ground contact: the lowest painted sample that shows (a single vertex near paint can be dragged far
        # off while its faces carry only a sliver), of the body and its clothes (a dropped weapon lies apart)
        body_lows = [v for k_, v in paint_low.items() if k_ not in RIGID]
        low = min(body_lows) if body_lows else 0.0
        row = {'clip': name, 'i': i, 'stretch': round(worst, 2), 'layer': worst_layer, 'low': round(low, 4),
               'lows': paint_low,
               'by_layer': self.layer_smear, 'by_pair': [[a, b, n] for (a, b), n in top_pairs],
               'where': where, 'smear': len(smear), 'smear_pct': round(100.0 * len(smear) / max(1, figure), 2),
               'smear_bpx': round(len(smear) * cell_bpx2, 1), 'fig_bpx': round(fig_bpx, 1)}
        if name == 'walk':
            row.update(self.feet(anim, arm, H, ctx['c']))
        self.rows.append(row)

    def face_pair(self, ob, verts):
        """The two bones whose pull differs most across a face (what stretches it)."""
        key = (ob.name, tuple(int(v) for v in verts))
        if key in self.pairs:
            return self.pairs[key]
        ws = []
        for v in verts:
            ws.append({ob.vertex_groups[g.group].name: g.weight for g in ob.data.vertices[int(v)].groups})
        names = set().union(*ws)
        span = {n: max(w.get(n, 0) for w in ws) - min(w.get(n, 0) for w in ws) for n in names}
        two = sorted(span, key=lambda n: -span[n])[:2]
        pair = (ob.name + ':' + two[0], two[1] if len(two) > 1 else '')
        self.pairs[key] = pair
        return pair

    def feet(self, anim, arm, H, c):
        out = {}
        stride = getattr(anim, 'walk_stride', None)
        if not stride:
            return out
        per = stride / c['count']           # the ground scrolls back this far per frame
        for b in ('foot.R', 'foot.L', 'fl.lower', 'fr.lower', 'hl.lower', 'hr.lower'):
            if b not in arm.pose.bones:
                continue
            pb = arm.pose.bones[b]
            tip = arm.matrix_world @ pb.tail
            head = arm.matrix_world @ pb.head
            z = min(tip.z, head.z)
            x = tip.x
            prev = self.last_feet.get(b)
            if prev is not None and z < .02 * H and prev[1] < .02 * H:
                out.setdefault('slip', []).append(round(abs((x - prev[0]) - (-per)) / max(1e-9, per), 3))
            self.last_feet[b] = (x, z)
        return out


def main():
    argv = sys.argv[sys.argv.index('--') + 1:]
    ids = argv[argv.index('--ids') + 1].split(',')
    out = Path(argv[argv.index('--out') + 1]) if '--out' in argv else B.ROOT / 'artifacts/remaster/motion_check'
    out.mkdir(parents=True, exist_ok=True)
    for ident in ids:
        probe = Probe()

        def hook(*a, **k):
            fr = sys._getframe(1)
            while fr and 'layer_obs' not in fr.f_locals:
                fr = fr.f_back
            L = fr.f_locals
            if L.get('name') == 'walk' and L.get('i') == 0:
                probe.last_feet = {}
            probe.measure(L)
        try:
            B.build(ident, None, str(out / '_frames'), render_hook=hook)
        except Exception:  # noqa: BLE001
            traceback.print_exc()
            continue
        rows = probe.rows
        summary = {}
        for clip in sorted({r['clip'] for r in rows}):
            rs = [r for r in rows if r['clip'] == clip]
            s = {'frames': len(rs), 'smear_max': max(r['smear'] for r in rs),
                 'smear_bpx_max': max(r['smear_bpx'] for r in rs), 'fig_bpx': rs[0]['fig_bpx'],
                 'smear_at': max(rs, key=lambda r: r['smear'])['i'],
                 'smear_pct_max': max(r['smear_pct'] for r in rs),
                 'stretch_max': max(r['stretch'] for r in rs),
                 'stretch_at': max(rs, key=lambda r: r['stretch'])['i'],
                 'stretch_layer': max(rs, key=lambda r: r['stretch'])['layer'],
                 'low_min': min(r['low'] for r in rs), 'low_max': max(r['low'] for r in rs),
                 'low_last': rs[-1]['low']}
            sl = [x for r in rs for x in r.get('slip', [])]
            if sl:
                s['slip_mean'] = round(float(np.mean(sl)), 3)
                s['slip_p90'] = round(float(np.percentile(sl, 90)), 3)
            summary[clip] = s
        (out / f'{ident}.json').write_text(json.dumps({'id': ident, 'clips': summary, 'frames': rows}) + '\n')
        print('CHECKED', ident, json.dumps({k: (v['smear_max'], v['stretch_max'], v['low_min'], v['low_max'], v.get('slip_mean'))
                                             for k, v in summary.items()}), flush=True)


main()
