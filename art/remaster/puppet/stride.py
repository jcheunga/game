#!/usr/bin/env python3
"""Walking puppet sources: every person repainted side-on and upright, so it can walk like one.

The first puppets (masters.py) were painted in the roster sheet's lineup pose: three-quarter FRONT, feet straddled,
knees bent out. A body that faces the viewer while it travels sideways crab-walks, and a straddle animated into a
stride squats, whatever the legs do. These sources repaint the same character (its master is image 2) turned to face
its walk, standing tall with the legs under the hips, and add a third edit that takes the legs off, so each leg is a
layer of its own that can swing past the other.

  python3 art/remaster/puppet/stride.py player_militia,enemy_walker [--stages master,landmarks,body,torso,hips,layers]
  python3 art/remaster/puppet/stride.py all --jobs 3

Per unit, in artifacts/remaster/puppet_stride/<id>/ (each stage skips what exists; the old sources stay untouched):
  master.png   Codex: image 2's character in the walking pose (POSE + the carry for its motion profile)
  landmarks    masters.stage_landmarks on the new master (Codex vision; check by eye)
  body.png     masters.BODY_PROMPT: what the hands hold painted out
  torso.png    masters.TORSO_PROMPT: both arms painted out
  hips.png     HIPS_PROMPT: both legs painted out below the hips and skirts (after torso.png)
  limbs.png    LIMBS_PROMPT: arms and legs painted out in one edit (instead of torso.png + hips.png)
  held.png     HELD_PROMPT: the weapon alone, painted complete where the hands covered it
  layers       layers.py --legs (weapon, items, arms, the near leg, the base)
Mounts, machines, beasts, serpents and blobs keep their sources in artifacts/remaster/puppet.
"""
import argparse
import concurrent.futures as cf
import datetime as dt
import json
import subprocess
import sys
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import masters as M  # noqa: E402

OLD = M.PUPPET
STRIDE = M.REVIEW / 'puppet_stride'

POSE = ('the figure turned to face RIGHT, toward the right edge of the image, seen from its right side in three-quarter '
        'profile (about 30 degrees from a pure side view, so a little of the chest shows): the head, chest, hips, knees '
        'and feet all point to the right. It stands tall and upright, relaxed and natural, like a soldier on an easy '
        'march caught as the feet pass: the back straight, the hips high and level above the feet, the legs long and '
        'nearly straight (only soft at the knee), the near (right) leg a short half step ahead of the far (left) leg so '
        'the two legs show apart below the knees, both feet flat on one ground line with the toes pointing right. NOT a '
        'wide or straddled stance, NOT crouching or squatting, NOT knees bent outward, NOT a lunge or fighting stance')

# how the hands carry things on the march, by attack profile (the rig moves the hands from here)
CARRY = {
    'sword-cut': ('the weapon in the near (right) hand, held at the side a little forward, the blade or head sloped up '
                  'and ahead; a shield or anything else in the far (left) hand held forward in front of the chest; an '
                  'empty far hand relaxed in front of the belt'),
    'guard-cut': ('the shield on the far (left) arm, held up in front of the chest and hips toward the right, its face '
                  'turned toward the viewer; the weapon in the near (right) hand at the side, point or head forward and '
                  'slightly up, below the shield\'s top edge and clear of the face'),
    'spear-thrust': 'the spear in both hands at waist height, sloped gently up, its point well ahead of the body',
    'blade-stab': 'the blade(s) held low in front at the side(s), points forward, relaxed and ready',
    'axe-cleave': ('the great weapon in both hands, carried sloped across the front of the body, its head up and ahead '
                   'at shoulder height, clear of the face'),
    'royal-cleave': ('the great weapon in both hands, carried sloped across the front of the body, its head up and ahead '
                     'at shoulder height, clear of the face'),
    'heavy-smash': ('the heavy weapon carried in both hands, sloped across the front of the body with its head up and '
                    'ahead at shoulder height, clear of the face (or, with no weapon, the great fists hanging forward, '
                    'ready)'),
    'staff-strike': 'the staff, horn or standard in the near (right) hand, upright and a little ahead, planted on the march',
    'hammer-command': ('the hammer, mallet or drumstick in the near (right) hand at the side, head up; anything else in '
                       'the far (left) hand or carried as before, in front of the body'),
    'staff-cast': ('a staff in the near (right) hand, upright beside and a little ahead of the body, its foot near the '
                   'ground; the other hand forward at chest height with its spell, flame or item; robes falling '
                   'straight so the feet show below the hem'),
    'flask-toss': ('what is thrown (bomb, flask, harpoon, boulder) held as image 2 holds it, in the near (right) hand at '
                   'the side or overhead; the other hand forward'),
    'bow-draw': ('the bow in the far (left) hand, held forward and low with the arm relaxed, NOT drawn, an arrow loose '
                 'along it; the near (right) hand relaxed at the hip'),
    'crossbow': 'the crossbow held in both hands at waist height, level, pointing right',
    'claw-rake': ('the arms hanging forward of the body, the claws or fists ready; a hunched creature may lean its '
                  'chest forward, but it stands on its legs, not in a crouch'),
}
CARRY_DEFAULT = ('everything held as image 2 holds it, adjusted to the new view: the main item in the near (right) hand, '
                 'anything else in the far (left) hand in front of the body')


def humanoid(ident):
    return M.kind_of(ident) == 'humanoid'


def master_prompt(ident, dest):
    u = M.UNITS.get(ident, {})
    title = u.get('DisplayName', ident)
    side = 'player' if ident.startswith('player_') else 'enemy'
    carry = CARRY.get(M.profile_of(ident) or old_profile(ident), CARRY_DEFAULT)
    return f"""Image 1 is the official unit sheet for the game "Crownroad: Siege of Ash": take from it only the \
painting style. Image 2 is this unit, {title} ({M.FACTION[side]}), painted by that artist. Keep image 2's design \
EXACTLY: the same face, head, build and proportions, the same armour, clothing, colours, emblems, weapon and props, and \
the same rendering, materials and lighting (soft warm key light from the upper left, a cool rim light).
Use your image generation tool to create ONE new image: this same character in a NEW POSE, for a side-scrolling game \
where it will be cut into parts and animated walking to the right.
Pose: {POSE}.
Hands: {carry}. The arms, hands and anything held read clearly against the body and stay clear of the face and of \
the legs; both legs, from the hem or hips down to the feet, are unobstructed.
Anatomy: exactly the limbs image 2 has - TWO arms with one hand each and TWO legs (a far arm or leg partly hidden \
behind the body is still the same one arm or leg). Never add, double or mirror an arm, hand, leg or head.
The background must be TRANSPARENT (a PNG with an alpha channel, nothing behind the figure: no floor, no ground \
shadow, no backdrop, no text, no border). Square 1024x1024, the whole figure inside the frame with a small margin, \
filling most of its height.
Save the image to {dest} and reply with the path only."""


def old_profile(ident):
    m = M.REVIEW / 'puppet_units' / ident / 'metadata.json'
    return json.loads(m.read_text()).get('motion', {}).get('profile', '') if m.exists() else ''


HIPS_PROMPT = """The attached image is a game character painting with a transparent background, used for 2D cutout \
animation. Use your image generation tool to EDIT it: remove BOTH LEGS COMPLETELY - each whole leg from the hip down: \
the thigh, knee, shin, ankle and the boot or foot, and any greave, knee guard or leg wrapping. Keep everything that \
hangs from the waist - tunic skirts, tabards, coat tails, robes, loincloths, tattered cloth, pouches, scabbards - \
exactly as it is, and paint the inside and underside of those skirts where the legs were, so the figure ends cleanly \
at its hips and hems with NOTHING below them. Keep the head, torso, belt and skirts exactly the same: same character, \
same pose, same position in the frame, same size, same framing, same painting style, same lighting. Keep the \
background TRANSPARENT (PNG with alpha, no floor, no shadow). Output 1024x1024.
Save the image to {dest} and reply with the path only."""


HELD_PROMPT = """The attached image is a game character painting with a transparent background, used for 2D cutout \
animation. Use your image generation tool to EDIT it: keep ONLY the weapon the figure fights with - whatever it \
holds to strike or shoot with: a sword, axe, mace, hammer, club, spear, polearm, staff (with anything fixed to or \
hanging from it), bow (with its string and arrow), crossbow, or the bomb, flask or rock it throws - and remove \
EVERYTHING else: the whole figure (head, body, clothes, armour, arms and hands) and anything else it holds or carries \
(a shield, lantern, flame or item in the other hand, sheathed weapons, quivers). Paint the weapon COMPLETE where the \
hands, the body or anything else covered it - its whole grip, haft or shaft continuing straight through, as if the \
hands had let go of it in mid-air. The weapon stays exactly where it is: same position in the frame, same angle, same \
size, same painting style and lighting. Everything else TRANSPARENT (PNG with alpha). Output 1024x1024.
Save the image to {dest} and reply with the path only."""


def stage_held(ident, force=False):
    """The weapon alone, complete under the hands (its layer, instead of a diff that loses what the fists cover)."""
    d = STRIDE / ident
    lm = json.loads((d / 'landmarks.json').read_text()) if (d / 'landmarks.json').exists() else {}
    if M.kind_of(ident) != 'humanoid' or not lm.get('weapon_tip'):
        return 'no weapon'
    dest = d / 'held.png'
    if dest.exists() and not force:
        return 'exists'
    ok = M.codex_image(HELD_PROMPT.format(dest=dest), [d / 'master.png'], dest, d / 'codex_held.log')
    if ok:
        M.normalise(dest)
    return 'ok' if ok else 'FAILED'


LIMBS_PROMPT = """The attached image is a game character painting with a transparent background, used for 2D cutout \
animation. Use your image generation tool to EDIT it: remove BOTH ARMS and BOTH LEGS COMPLETELY.
Arms: each whole arm from the top of the shoulder down to the hand - the entire sleeve, pauldron, upper arm, elbow, \
forearm, bracer or gauntlet, and the hand; leave NO sleeve stubs or shoulder caps, and paint the sides of the torso, \
its clothing, armour, belt, straps and pouches where the arms covered them.
Legs: each whole leg from the hip down - the thigh, knee, shin, ankle and the boot or foot, and any greave, knee guard \
or leg wrapping. Keep everything that hangs from the waist (tunic skirts, tabards, coat tails, robes, loincloths, \
tattered cloth, pouches, scabbards) exactly as it is, and paint the inside and underside of those skirts where the \
legs were, so the figure ends cleanly at its hips and hems with NOTHING below them.
Keep the head, any cloak or collar, the torso, the belt and the skirts exactly the same: same character, same pose, \
same position in the frame, same size, same framing, same painting style, same lighting. Keep the background \
TRANSPARENT (PNG with alpha, no floor, no shadow). Output 1024x1024.
Save the image to {dest} and reply with the path only."""


def stage_limbs(ident, force=False):
    """One edit for arms and legs together (a unit started on torso.png + hips.png finishes that way)."""
    d = STRIDE / ident
    if (d / 'torso.png').exists():
        return 'torso route'
    dest = d / 'limbs.png'
    if dest.exists() and not force:
        return 'exists'
    src = d / 'body.png' if (d / 'body.png').exists() else d / 'master.png'
    ok = M.codex_image(LIMBS_PROMPT.format(dest=dest), [src], dest, d / 'codex_limbs.log')
    if ok:
        M.normalise(dest)
    return 'ok' if ok else 'FAILED'


def stage_master(ident, force=False):
    d = STRIDE / ident
    d.mkdir(parents=True, exist_ok=True)
    dest = d / 'master.png'
    if dest.exists() and not force:
        return 'exists'
    if dest.exists():
        n = 1
        while (d / f'master_v{n}.png').exists():
            n += 1
        dest.rename(d / f'master_v{n}.png')
        for f in ('landmarks.json', 'body.png', 'torso.png', 'hips.png', 'limbs.png', 'held.png', 'layers.json'):
            (d / f).unlink(missing_ok=True)
    t0 = time.time()
    ok = M.codex_image(master_prompt(ident, dest), [M.SHEET, OLD / ident / 'master.png'], dest, d / 'codex_master.log')
    if ok:
        M.normalise(dest)
    return f'ok {time.time() - t0:.0f}s' if ok else 'FAILED'


def stage_hips(ident, force=False):
    d = STRIDE / ident
    dest = d / 'hips.png'
    if (d / 'limbs.png').exists() or not (d / 'torso.png').exists():
        return 'limbs route'
    if dest.exists() and not force:
        return 'exists'
    ok = M.codex_image(HIPS_PROMPT.format(dest=dest), [d / 'torso.png'], dest, d / 'codex_hips.log')
    if ok:
        M.normalise(dest)
    return 'ok' if ok else 'FAILED'


def stage_layers(ident, force=False):
    d = STRIDE / ident
    if (d / 'layers.json').exists() and not force:
        return 'exists'
    p = subprocess.run([str(HERE / 'run.sh'), str(HERE / 'layers.py'), str(d), '--legs', '--preview',
                        str(d / 'layers_preview.png')], capture_output=True, text=True)
    (d / 'layers.log').write_text(p.stdout[-4000:] + p.stderr[-4000:])
    return 'ok' if p.returncode == 0 else 'FAILED (layers.log)'


def wrap(fn):
    """Run one of masters.py's stages against the stride sources."""
    def go(ident, force=False):
        M.PUPPET = STRIDE
        return fn(ident, force)
    return go


STAGES = {'master': stage_master, 'landmarks': wrap(M.stage_landmarks), 'body': wrap(M.stage_body),
          'torso': wrap(M.stage_torso), 'hips': stage_hips, 'limbs': stage_limbs, 'held': stage_held, 'layers': stage_layers}


def run(ident, stages, force):
    fails_p = STRIDE / ident / 'failures.json'
    fails = json.loads(fails_p.read_text()) if fails_p.exists() else {}
    for s in stages:
        if fails.get(s, 0) >= 2 and s not in force:
            print(f'{dt.datetime.now():%H:%M} {s.upper()} {ident} SKIPPED (failed {fails[s]}x)', flush=True)
            return ident, f'{s} skipped'
        t0 = time.time()
        try:
            r = STAGES[s](ident, s in force)
        except Exception as e:  # noqa: BLE001
            r = f'ERROR {e}'
        print(f'{dt.datetime.now():%H:%M} {s.upper()} {ident} {r} ({time.time() - t0:.0f}s)', flush=True)
        if 'FAIL' in r or 'ERROR' in r:
            fails[s] = fails.get(s, 0) + 1
            fails_p.parent.mkdir(parents=True, exist_ok=True)
            fails_p.write_text(json.dumps(fails) + '\n')
            return ident, f'{s} {r}'
    return ident, 'done'


UNARMED_PROFILES = M.UNARMED | {'claw-rake'}


def needs(ident, stage):
    """Whether a unit still wants this edit (follow mode)."""
    d = STRIDE / ident
    lm = json.loads((d / 'landmarks.json').read_text()) if (d / 'landmarks.json').exists() else {}
    armed = bool(lm.get('weapon_tip') or lm.get('offhand_item'))
    if stage == 'body':
        return armed and not (d / 'body.png').exists()
    if stage == 'limbs':
        return not (d / 'torso.png').exists() and not (d / 'limbs.png').exists() and \
            ((d / 'body.png').exists() or not armed)
    if stage == 'hips':
        return (d / 'torso.png').exists() and not (d / 'hips.png').exists()
    if stage == 'held':
        # only where the weapon cut from the body edit is broken: set by eye on review (setlm want_held=true), or
        # measured by layers.py (weapon_whole, the share of the weapon's line with paint on it after the fill)
        if not lm.get('weapon_tip') or (d / 'held.png').exists():
            return False
        if lm.get('want_held'):
            return True
        info = json.loads((d / 'layers.json').read_text()) if (d / 'layers.json').exists() else {}
        return info.get('weapon_whole', 1.0) < 0.9
    return False


def follow(ids, jobs):
    """Keep Codex busy: a unit's edits as soon as its joints have been placed by eye (landmarks.json located_by
    claude: setlm.py), otherwise the next master still to paint. Runs until every unit has its sources."""
    import threading
    lock = threading.Lock()
    busy = set()

    def failed(i, st):
        fp = STRIDE / i / 'failures.json'
        return (json.loads(fp.read_text()) if fp.exists() else {}).get(st, 0) >= 2

    def take():
        with lock:
            for i in ids:
                if i in busy:
                    continue
                lmp = STRIDE / i / 'landmarks.json'
                if lmp.exists() and json.loads(lmp.read_text()).get('located_by') == 'claude':
                    for st in ('body', 'limbs', 'hips', 'held'):
                        if needs(i, st) and not failed(i, st):
                            busy.add(i)
                            return i, st
            for i in ids:
                # a master without joints yet: Codex places them (checked and corrected by eye before the edits)
                if i not in busy and (STRIDE / i / 'master.png').exists() and \
                        not (STRIDE / i / 'landmarks.json').exists() and not failed(i, 'landmarks'):
                    busy.add(i)
                    return i, 'landmarks'
            for i in ids:
                if i not in busy and not (STRIDE / i / 'master.png').exists() and not failed(i, 'master'):
                    busy.add(i)
                    return i, 'master'
        return None

    def pending():
        for i in ids:
            if not (STRIDE / i / 'master.png').exists() and not failed(i, 'master'):
                return True
            lmp = STRIDE / i / 'landmarks.json'
            if not lmp.exists() or json.loads(lmp.read_text()).get('located_by') != 'claude':
                return True
            if any(needs(i, st) and not failed(i, st) for st in ('body', 'limbs', 'hips', 'held')):
                return True
        return False

    def worker():
        while True:
            t = take()
            if t is None:
                if not pending():
                    return
                time.sleep(60)
                continue
            i, st = t
            try:
                run(i, [st, 'landmarks'] if st == 'master' else [st], set())
            finally:
                with lock:
                    busy.discard(i)

    threads = [threading.Thread(target=worker) for _ in range(jobs)]
    for th in threads:
        th.start()
    for th in threads:
        th.join()
    print('STRIDE_FOLLOW done', flush=True)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('ids')
    ap.add_argument('--stages', default='master,landmarks,body,limbs,hips,held')
    ap.add_argument('--force', default='')
    ap.add_argument('--jobs', type=int, default=3)
    ap.add_argument('--follow', action='store_true', help='paint masters, and edits once joints are placed by eye')
    a = ap.parse_args()
    ids = [i for i in M.UNITS if (OLD / i / 'master.png').exists() and humanoid(i)] if a.ids == 'all' \
        else a.ids.split(',')
    stages = a.stages.split(',')
    force = set(filter(None, a.force.split(',')))
    STRIDE.mkdir(parents=True, exist_ok=True)
    (STRIDE / '.gdignore').touch()
    M.PUPPET = STRIDE
    if a.follow:
        return follow(ids, a.jobs)
    with cf.ThreadPoolExecutor(a.jobs) as ex:
        results = list(ex.map(lambda i: run(i, stages, force), ids))
    bad = [r for r in results if r[1] != 'done']
    print('STRIDE_SOURCES', len(results) - len(bad), 'done;', 'failed:', bad, flush=True)


if __name__ == '__main__':
    main()
