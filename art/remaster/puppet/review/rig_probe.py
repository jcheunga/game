"""Print the posed rig at chosen frames without rendering: the way to find WHY a pose is wrong (which code moved a
bone, a hidden cap, a leg that never reaches its target). Edit `report()` to print what you need.

  /opt/homebrew/bin/blender --background --factory-startup --python art/remaster/puppet/review/rig_probe.py -- \\
      enemy_drowned_colossus idle:0 walk:3

build.build(render_hook=...) poses every frame exactly as a render would and calls the hook instead of rendering.
The hook sees build()'s locals: name (clip), i (frame in clip), anim (the Animator: legs_, split, H, rest ...),
arm (the armature), layer_obs [(layer name, mesh object)]. Found with it: reachable() lifting the heel of any leg
straighter than 99.2% (the squat), the topple applied after death limbs were aimed (capes standing up).
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
import build as B  # noqa: E402

argv = sys.argv[sys.argv.index('--') + 1:]
ident = argv[0]
want = {(c, int(i)) for c, i in (s.split(':') for s in argv[1:])}


def report(L):
    arm, anim = L['arm'], L['anim']
    M = arm.matrix_world
    print(f"== {ident} {L['name']}:{L['i']}  H={anim.H:.3f}")
    for s_ in 'RL':
        if 'thigh.' + s_ not in arm.pose.bones:
            continue
        t, sh = arm.pose.bones['thigh.' + s_], arm.pose.bones['shin.' + s_]
        hip, knee, ank = M @ t.head, M @ t.tail, M @ sh.tail
        L1, L2 = (knee - hip).length, (ank - knee).length
        print(f"  leg {s_}: hip {tuple(round(c, 3) for c in hip)} ankle {tuple(round(c, 3) for c in ank)} "
              f"extension {(ank - hip).length / (L1 + L2):.4f} knee {anim.world_angle('shin.' + s_) - anim.world_angle('thigh.' + s_):+.1f}°")
    for b in ('hips', 'spine', 'chest', 'cape', 'upper_arm.R', 'forearm.R', 'weapon'):
        if b in arm.pose.bones:
            print(f"  {b}: world angle {anim.world_angle(b):+.1f}°")


def hook():
    fr = sys._getframe(1)
    while fr and 'layer_obs' not in fr.f_locals:
        fr = fr.f_back
    L = fr.f_locals
    if (L['name'], L['i']) in want:
        report(L)


B.build(ident, None, '/tmp/rig_probe_unused', render_hook=hook)
