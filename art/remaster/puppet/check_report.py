#!/usr/bin/env python3
"""Pass criteria over motion_check.py's measurements of every frame: prints each failing unit and clip, a count,
and the worst values of each measure.

  python3 art/remaster/puppet/check_report.py [dir]     (default artifacts/remaster/motion_check)

  smear     paint that shows from faces stretched past 3x their painted size (a tear, a ribbon, a fan), in any frame:
            fails above SMEAR_PCT of the figure's visible paint when it covers more than SMEAR_MIN_BPX battle
            pixels² (a 4x4 patch: below that it is not seen on any unit), or above SMEAR_BPX (a 10x10 patch) on any
  ground    living clips: the lowest paint of the body and its clothes within GROUND of the road (beasts in flight
            and recoils excepted: walk, hit and deploy may rise); deaths: cloth sinks at most CLOTH_SINK into the
            road and a limp arm under the body at most ARM_SINK
  slip      walk: a planted foot moves with the ground, mean error at most SLIP of the ground's motion per frame
  gait      people (split legs) in a walk: standing and passing-leg knees bent at most KNEE (a crouch reads as a
            squat), hips sinking at most HIP_DROP below standing, and the feet passing each other by at least CROSS
            both ways (feet that never pass crab-walk). Prowl and jog run low by design
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SMEAR_PCT = 3.0
SMEAR_MIN_BPX = 16.0     # smaller than a 4x4 battle-pixel patch, a smear is not seen whatever its share
SMEAR_BPX = 100.0
GROUND = .03
CLOTH_SINK = -.045
ARM_SINK = -.11
SLIP = .2
KNEE = 25.0          # standing and passing-leg knee bend, degrees (measured hip-knee-ankle; ~5° is the knee cap)
HIP_DROP = .03       # how far the hips may sink below standing over a walk, in figure heights
CROSS = .05          # each foot reaches this far past the other along the road (both ways), in figure heights
CROUCHED = ('prowl', 'jog')   # gaits run low on purpose (a stalking ghoul, a jog's flight), and beasts' gaits
STANDING = ('idle', 'attack', 'cast', 'melee')
RIGID = ('weapon', 'offhand', 'arrow')


def failures(d):
    out = []
    for clip, s in d['clips'].items():
        rs_ = [r for r in d['frames'] if r['clip'] == clip]
        bad_ = [r for r in rs_ if (r['smear_pct'] > SMEAR_PCT and r['smear_bpx'] > SMEAR_MIN_BPX)
                or r['smear_bpx'] > SMEAR_BPX]
        if bad_:
            out.append(f"{clip}: smear {s['smear_pct_max']:.2f}% / {s['smear_bpx_max']:.0f} px² at frame "
                       f"{s['smear_at']}")
        frames = [r for r in d['frames'] if r['clip'] == clip]
        body = [min([v for k, v in r['lows'].items() if not k.startswith(('arm_',) + RIGID)] or [0]) for r in frames]
        arms = [min([v for k, v in r['lows'].items() if k.startswith('arm_')] or [0]) for r in frames]
        if clip == 'death':
            if min(body) < CLOTH_SINK:
                out.append(f"death: sinks {min(body):+.3f} of the figure into the road")
            if min(arms) < ARM_SINK:
                out.append(f"death: an arm sinks {min(arms):+.3f} into the road")
        else:
            if min(body) < -GROUND:
                out.append(f"{clip}: sinks {min(body):+.3f} into the road")
            if clip in STANDING and min(body) > GROUND:
                out.append(f"{clip}: floats {min(body):+.3f} off the road")
        g = s.get('gait')
        if clip == 'walk' and g and g.get('name') not in CROUCHED:
            if g['stand_knee'] > KNEE or (g['passing_knee'] or 0) > KNEE:
                out.append(f"walk: knees stay bent (standing {g['stand_knee']}°, passing {g['passing_knee']}°)")
            if g['hip_drop'] > HIP_DROP:
                out.append(f"walk: hips sink {g['hip_drop']:.3f} of the figure below standing (a squat)")
            if g['cross'] < CROSS:
                out.append(f"walk: the feet pass each other by only {g['cross']:.3f} of the figure (a crab walk)")
        if clip == 'walk' and s.get('slip_mean', 0) > SLIP:
            out.append(f"walk: planted feet slip {s['slip_mean']:.2f} of the ground's motion")
    return out


def main():
    d = Path(sys.argv[1]) if len(sys.argv) > 1 else ROOT / 'artifacts/remaster/motion_check'
    units = sorted(d.glob('*.json'))
    bad, worst = 0, {'smear_pct': (0, ''), 'smear_bpx': (0, ''), 'slip': (0, '')}
    frames = 0
    for p in units:
        j = json.loads(p.read_text())
        frames += sum(s['frames'] for s in j['clips'].values())
        for c, s in j['clips'].items():
            for key, val in (('smear_pct', s['smear_pct_max']), ('smear_bpx', s['smear_bpx_max']),
                             ('slip', s.get('slip_mean', 0))):
                if val > worst[key][0]:
                    worst[key] = (val, f'{p.stem} {c}')
        f = failures(j)
        if f:
            bad += 1
            print(p.stem)
            for line in f:
                print('   ', line)
    print('worst:', ', '.join(f'{k} {v:.2f} ({w})' for k, (v, w) in worst.items()))
    print(f'MOTION_CHECK: {bad} of {len(units)} units fail ({frames} frames measured)')


if __name__ == '__main__':
    main()
