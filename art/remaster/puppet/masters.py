#!/usr/bin/env python3
"""Puppet sources for every unit: a new painting in the roster sheet's style, its joint landmarks, and the Codex edits
the cutout layers are cut from.

  python3 art/remaster/puppet/masters.py all [--jobs 3] [--stages master,landmarks,body,torso,layers] [--force master]
  python3 art/remaster/puppet/masters.py player_paladin,enemy_brute --stages master

Per unit, in artifacts/remaster/puppet/<id>/ (each stage skips what exists, so a run resumes):
  master.png      Codex: the unit painted by the sheet's artist (output/units.png), full body, facing right, on
                  transparency. Units drawn on the sheet keep their sheet design (art/remaster/ref/<id>.png); the
                  rest take only what they are from their earlier painting (artifacts/remaster/paint/<id>/painted.png)
                  and get the sheet's proportions.
  landmarks.json  puppet/landmarks.py (Codex vision on a gridded copy)
  body.png        Codex edit: the master with what the hands hold painted out (armed humanoids)
  torso.png       Codex edit: body.png with both arms painted out (humanoids); horse.png: the rider painted out
                  (mounts); nowheels.png: the wheels painted out (machines)
  layers.json     puppet/layers.py (run through puppet/run.sh for OpenCV)
  icon            artifacts/remaster/puppet_units/<id>/portrait.png, the unit's icon, from the master
When Codex reports its usage limit, the run sleeps until the time it gives and carries on.
"""
import argparse
import concurrent.futures as cf
import datetime as dt
import json
import re
import subprocess
import sys
import threading
import time
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
sys.path.insert(0, str(HERE))
import landmarks as LM  # noqa: E402

REVIEW = ROOT / 'artifacts/remaster'
PUPPET = REVIEW / 'puppet'
SHEET = ROOT / 'output/units.png'
REF = ROOT / 'art/remaster/ref'
UNITS = {u['Id']: u for u in json.loads((ROOT / 'data/units.json').read_text())['Units']}
MODEL = 'gpt-5.6-sol'

KIND = {
    'mount': {'player_raider', 'player_outrider', 'enemy_bone_lancer', 'enemy_boss_steppe'},
    'machine': {'player_ballista', 'enemy_plague_engine', 'enemy_boneballista', 'enemy_siegetower'},
    # the swamp maw and bog spore were painted as blobs, but they stand on legs: they are rigged as squat four-legged
    # beasts (landmarks placed by eye, 'stubby') so they step instead of gliding
    'quad': {'player_hound', 'enemy_grave_rat', 'enemy_ash_hound', 'enemy_bog_spore', 'enemy_swamp_maw'},
    'serpent': {'enemy_bloated_eel', 'enemy_plague_leech'},
    'blob': {'enemy_splitter', 'enemy_reliquary_eye', 'enemy_mirror_wisp'},
}
UNARMED = {'claw-rake', 'pounce', 'nest-pulse'}   # motion profiles whose hands hold nothing worth a layer

FACTION = {
    'player': ('a soldier of the Lantern Caravan, a royal human army: teal and cream heraldry with a gold crowned-lantern '
               'emblem, blued steel armour with brass and gold trim, warm brown leather, oak'),
    'enemy': ('a creature of the Rotbound Host, an undead army: ruined crimson rags and heraldry, weathered bone, '
              'rust-eaten black iron with tarnished brass, sickly pale flesh, eerie plague-green glowing eyes and lights'),
}

POSE = {
    'humanoid': ('full body, three-quarter front view facing right, standing in a natural ready combat stance: feet '
                 'apart and both flat on one ground line, the arms, hands and anything held reading clearly against '
                 'the body (not hidden behind it, not crossing the face)'),
    'mount': ('full body, side three-quarter view facing right: the rider seated on the mount, the mount standing with '
              'all four legs visible and apart, hooves or feet on one ground line, the rider\'s weapon clear of the mount\'s head'),
    'machine': 'the whole machine and its crew, side three-quarter view facing right, wheels or feet on one ground line',
    'quad': ('full body, side three-quarter view facing right, standing on all four legs with the legs clearly apart, '
             'paws on one ground line, head up, tail clear of the body'),
    'serpent': 'full body, side view facing right, the body in a gentle S-curve resting on one ground line, head raised',
    'blob': 'full body, three-quarter front view facing right, resting on or hovering just above one ground line',
}


def kind_of(ident):
    return next((k for k, ids in KIND.items() if ident in ids), 'humanoid')


def profile_of(ident):
    m = REVIEW / 'units' / ident / 'metadata.json'
    return json.loads(m.read_text())['motion']['profile'] if m.exists() else ''


# ------------------------------------------------------------------ codex
LIMIT_LOCK = threading.Lock()
WAKE = [None]   # one shared wake-up time: threads that hit the limit together all wake at the reset


def wait_for_limit(text):
    """Sleep until the time a usage-limit message names (or 20 minutes), shared by every thread."""
    m = re.search(r'try again at (\d{1,2}):(\d{2})\s*([AP]M)', text)
    r = re.search(r'resets_at\\?"?:\s*(\d{9,})', text)
    now = dt.datetime.now()
    if r:
        # image generation's own (daily) limit reports the reset as an epoch time
        m = None
        when = dt.datetime.fromtimestamp(int(r.group(1))) + dt.timedelta(minutes=1)
    if r:
        pass
    elif m:
        h, mi, ap = int(m.group(1)), int(m.group(2)), m.group(3)
        h = h % 12 + (12 if ap == 'PM' else 0)
        when = now.replace(hour=h, minute=mi, second=0, microsecond=0) + dt.timedelta(minutes=1)
        if when < now:
            when += dt.timedelta(days=1)
    else:
        when = now + dt.timedelta(minutes=20)
    with LIMIT_LOCK:
        if WAKE[0] is None or WAKE[0] < now:
            WAKE[0] = when
            print(f'{now:%H:%M} LIMIT, waiting until {when:%H:%M}', flush=True)
        when = WAKE[0]
    # poll the wall clock: one long sleep stops counting while the machine sleeps, and would wake hours late
    while dt.datetime.now() < when:
        time.sleep(min(60.0, max(1.0, (when - dt.datetime.now()).total_seconds())))


def codex_image(prompt, images, dest, log, tries=4):
    """One Codex image-generation call; True once dest exists. Retries failures, waits out usage limits."""
    dest = Path(dest)
    for attempt in range(tries):
        cmd = ['codex', 'exec', '--skip-git-repo-check', '-m', MODEL, '-c', 'model_reasoning_effort="low"',
               '-s', 'workspace-write', '-C', str(dest.parent)]
        for im in images:
            cmd += ['-i', str(im)]
        p = subprocess.run(cmd + ['-'], input=prompt, capture_output=True, text=True, timeout=1800)
        Path(log).write_text(p.stdout[-6000:] + '\n--- stderr ---\n' + p.stderr[-3000:])
        if dest.exists():
            return True
        if 'usage limit' in (p.stdout + p.stderr) or 'usage_limit_reached' in (p.stdout + p.stderr):
            wait_for_limit(p.stdout + p.stderr)
    return False


# ------------------------------------------------------------------ prompts
def master_prompt(ident, dest):
    u = UNITS.get(ident, {})
    title = u.get('DisplayName', ident)
    side = 'player' if ident.startswith('player_') else 'enemy'
    kind = kind_of(ident)
    on_sheet = (REF / f'{ident}.png').exists()
    if on_sheet:
        ref = (f'Image 2 is this unit, {title}, as drawn on that sheet. Keep its design exactly: the same face, build, '
               f'armour, clothing, colours, emblems, weapon and props.')
    else:
        ref = (f'Image 2 is an older, rough painting of this unit, {title}. Take from it only WHAT the unit is: its '
               f'creature type, build, outfit, armour, colours, emblems, weapon and props. Ignore its proportions and '
               f'its rendering, which are stubby and crude: give it the natural, heroic proportions and the finish of '
               f'the figures in image 1.')
    return f"""Image 1 is the official unit sheet for the game "Crownroad: Siege of Ash". {ref}
Use your image generation tool to create ONE new image of this unit, painted by the same artist as image 1: identical \
rendering style, proportions, level of detail, materials and lighting (soft warm key light from the upper left, a cool \
rim light, rich hand-crafted texture). It is {FACTION[side]}.
Pose (the painting will be cut apart and animated): {POSE[kind]}.
The background must be TRANSPARENT (a PNG with an alpha channel, nothing behind the figure: no floor, no ground \
shadow, no backdrop, no text, no border). Square 1024x1024, the whole figure inside the frame with a small margin, \
filling most of its height.
Save the image to {dest} and reply with the path only."""


BODY_PROMPT = """The attached image is a game character painting with a transparent background, used for 2D cutout \
animation. Use your image generation tool to EDIT it: remove everything the figure holds or carries in its hands - \
weapons, shafts, blades, staves, bows, shields, flasks, lanterns, banners - completely. Where those covered the body, \
paint the body that was behind them. The hands stay exactly where they are, now empty and loosely closed. Everything \
else must stay exactly the same: same character, same pose, same position in the frame, same size, same framing, \
same painting style, same lighting. Keep the background TRANSPARENT (PNG with alpha, no floor, no shadow). Output the \
same size as the input (1024x1024).
Save the image to {dest} and reply with the path only."""

TORSO_PROMPT = """The attached image is a game character painting with a transparent background, used for 2D cutout \
animation. Use your image generation tool to EDIT it: remove BOTH arms COMPLETELY - each whole arm from the top of the \
shoulder down to the hand: the entire sleeve, pauldron, upper arm, elbow, forearm, bracer or gauntlet, and the hand. \
Leave NO sleeve stubs or shoulder caps. Where an arm covered the body, paint the body that was behind it (the side of \
the torso, its clothing, armour, belt, straps and pouches), so the torso is complete and its sides are clean where the \
arms joined. Keep the head, any cloak or collar, the torso, the belt, the skirts and the legs exactly the same: same \
character, same pose, same position in the frame, same size, same framing, same painting style, same lighting. Keep \
the background TRANSPARENT (PNG with alpha, no floor, no shadow). Output 1024x1024.
Save the image to {dest} and reply with the path only."""


# ------------------------------------------------------------------ stages
def stage_master(ident, force=False):
    d = PUPPET / ident
    d.mkdir(parents=True, exist_ok=True)
    dest = d / 'master.png'
    if dest.exists() and not force:
        return 'exists'
    if dest.exists():
        n = 1
        while (d / f'master_v{n}.png').exists():
            n += 1
        dest.rename(d / f'master_v{n}.png')
        for f in ('landmarks.json', 'body.png', 'torso.png', 'layers.json'):
            (d / f).unlink(missing_ok=True)
    ref = REF / f'{ident}.png'
    if not ref.exists():
        ref = REVIEW / 'paint' / ident / 'painted.png'
    t0 = time.time()
    ok = codex_image(master_prompt(ident, dest), [SHEET, ref], dest, d / 'codex_master.log')
    if ok:
        normalise(dest)
    return f'ok {time.time() - t0:.0f}s' if ok else 'FAILED'


def normalise(path):
    """1024x1024 RGBA; a figure painted on an opaque backdrop is reported, not guessed at."""
    from PIL import Image
    im = Image.open(path).convert('RGBA')
    if im.size != (1024, 1024):
        im = im.resize((1024, 1024), Image.LANCZOS)
    im.save(path)
    lo, hi = im.getchannel('A').getextrema()
    if lo > 200:
        print(f'WARNING {path} has no transparency', flush=True)


def blob_landmarks(path):
    """A blob's extremes and centre straight from the painting's alpha (no Codex call needed)."""
    from PIL import Image
    a = Image.open(path).convert('RGBA').getchannel('A').point(lambda v: 255 if v > 100 else 0)
    w, h = a.size
    px = a.load()
    pts = [(x, y) for y in range(0, h, 2) for x in range(0, w, 2) if px[x, y]]
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    cx, cy = sum(xs) / len(xs), sum(ys) / len(ys)
    y0, y1 = min(ys), max(ys)
    top = [x for x, y in pts if y <= y0 + 6]
    bot = [x for x, y in pts if y >= y1 - 6]
    row = [x for x, y in pts if abs(y - cy) <= 4]
    return {'top': [round(sum(top) / len(top)), y0], 'bottom': [round(sum(bot) / len(bot)), y1],
            'center': [round(cx), round(cy)], 'left': [min(row), round(cy)], 'right': [max(row), round(cy)],
            'facing': 'right', 'kind': 'blob'}


def serpent_landmarks(path):
    """A side-on serpent's spine from the painting: the middle of each column's paint, tail on the left, head on
    the right (it was painted facing right), sampled at the points the rig wants."""
    from PIL import Image
    a = Image.open(path).convert('RGBA').getchannel('A').point(lambda v: 255 if v > 100 else 0)
    w, h = a.size
    px = a.load()
    spine = []
    for x in range(0, w, 4):
        col = [y for y in range(0, h, 2) if px[x, y]]
        if len(col) > 6:
            spine.append((x, (min(col) + max(col)) / 2))
    L = [0.0]
    for (x0, y0), (x1, y1) in zip(spine, spine[1:]):
        L.append(L[-1] + ((x1 - x0) ** 2 + (y1 - y0) ** 2) ** 0.5)

    def at(f):
        t = f * L[-1]
        k = next(i for i, v in enumerate(L) if v >= t)
        return [round(spine[k][0]), round(spine[k][1])]
    return {'tail_tip': at(0.02), 'mid3': at(0.25), 'mid2': at(0.48), 'mid1': at(0.68), 'neck': at(0.84),
            'head': at(0.98), 'facing': 'right', 'kind': 'serpent'}


def stage_landmarks(ident, force=False):
    d = PUPPET / ident
    if (d / 'landmarks.json').exists() and not force:
        return 'exists'
    if kind_of(ident) in ('blob', 'serpent'):
        f = blob_landmarks if kind_of(ident) == 'blob' else serpent_landmarks
        (d / 'landmarks.json').write_text(json.dumps(f(d / 'master.png'), indent=1) + '\n')
        return 'ok (from alpha)'
    flipped = False
    for attempt in range(4):
        try:
            lm = LM.locate(d / 'master.png', d / 'landmarks.json', kind_of(ident), MODEL)
        except Exception as e:  # noqa: BLE001
            msg = str(e)
            if 'usage limit' in msg:
                wait_for_limit(msg)
            continue
        if str(lm.get('facing', 'right')).lower().startswith('l') and not flipped:
            # painted facing left despite the prompt: mirror it (and anything made from it) and look again
            from PIL import Image, ImageOps
            for f in ('master.png', 'body.png', 'torso.png', 'horse.png', 'nowheels.png'):
                if (d / f).exists():
                    ImageOps.mirror(Image.open(d / f)).save(d / f)
            (d / 'layers.json').unlink(missing_ok=True)
            flipped = True
            continue
        return 'ok' + (' (mirrored to face right)' if flipped else '')
    return 'FAILED'


RIDER_PROMPT = """The attached image is a game painting of a mounted unit with a transparent background, used for 2D \
cutout animation. Use your image generation tool to EDIT it: remove the RIDER completely - the whole person or \
creature riding, with their clothes, armour and anything they hold. Keep the mount (the horse or beast) with its \
saddle, harness and barding exactly the same, and paint the saddle and the mount's back, flank and neck where the \
rider covered them. Same pose, same position in the frame, same size, same framing, same painting style, same \
lighting. Keep the background TRANSPARENT (PNG with alpha, no floor, no shadow). Output 1024x1024.
Save the image to {dest} and reply with the path only."""

WHEELS_PROMPT = """The attached image is a game painting of a war machine with a transparent background, used for 2D \
cutout animation. Use your image generation tool to EDIT it: remove ALL of its wheels completely (rims, spokes, hubs \
and axle caps). Paint the machine's frame, axles and anything else that the wheels covered, so nothing is missing \
behind them. Everything else must stay exactly the same: same machine, same crew, same position in the frame, same \
size, same framing, same painting style, same lighting. Keep the background TRANSPARENT (PNG with alpha, no floor, \
no shadow). Output 1024x1024.
Save the image to {dest} and reply with the path only."""

# the second edit per kind: what it paints out, the file it writes, its prompt
SECOND = {'humanoid': ('torso.png', TORSO_PROMPT), 'mount': ('horse.png', RIDER_PROMPT),
          'machine': ('nowheels.png', WHEELS_PROMPT)}
LAYERED = {'humanoid', 'mount', 'machine'}


def armed(ident):
    d = PUPPET / ident
    if kind_of(ident) not in ('humanoid', 'mount'):
        return False
    lm = json.loads((d / 'landmarks.json').read_text()) if (d / 'landmarks.json').exists() else {}
    # a claw unit's painting may still hold something (a miner's pick): landmarks placed by hand say so; Codex's
    # are not trusted to tell claws from weapons
    if profile_of(ident) in UNARMED and lm.get('located_by') != 'claude':
        return False
    return bool(lm.get('weapon_tip') or lm.get('offhand_item'))


def stage_body(ident, force=False):
    d = PUPPET / ident
    if not armed(ident):
        return 'unarmed'
    dest = d / 'body.png'
    if dest.exists() and not force:
        return 'exists'
    ok = codex_image(BODY_PROMPT.format(dest=dest), [d / 'master.png'], dest, d / 'codex_body.log')
    if ok:
        normalise(dest)
    return 'ok' if ok else 'FAILED'


def stage_torso(ident, force=False):
    """The second edit: arms off a figure, the rider off a mount, the wheels off a machine."""
    d = PUPPET / ident
    kind = kind_of(ident)
    if kind not in SECOND:
        return 'no layers for ' + kind
    name, prompt = SECOND[kind]
    dest = d / name
    if dest.exists() and not force:
        return 'exists'
    src = d / 'body.png' if (d / 'body.png').exists() else d / 'master.png'
    ok = codex_image(prompt.format(dest=dest), [src], dest, d / f'codex_{name[:-4]}.log')
    if ok:
        normalise(dest)
    return 'ok' if ok else 'FAILED'


def stage_layers(ident, force=False):
    d = PUPPET / ident
    if kind_of(ident) not in LAYERED:
        return 'one mesh'
    if (d / 'layers.json').exists() and not force:
        return 'exists'
    p = subprocess.run([str(HERE / 'run.sh'), str(HERE / 'layers.py'), str(d), '--preview', str(d / 'layers_preview.png')],
                       capture_output=True, text=True)
    (d / 'layers.log').write_text(p.stdout[-4000:] + p.stderr[-4000:])
    return 'ok' if p.returncode == 0 else 'FAILED (layers.log)'


FULL_FIGURE_CLASSES = {'hound', 'ballista', 'siegetower', 'splitter'}


def stage_icon(ident, force=False):
    """The unit icon from its master painting: head and shoulders for people (framed on the head landmarks, the
    bottom edge faded so cards frame it cleanly), the whole figure for beasts, mounts, machines and blobs."""
    from PIL import Image, ImageChops
    d = PUPPET / ident
    dest = REVIEW / 'puppet_units' / ident / 'portrait.png'
    if dest.exists() and not force:
        return 'exists'
    fig = Image.open(d / 'master.png').convert('RGBA')
    x0, y0, x1, y1 = fig.getchannel('A').point(lambda a: 255 if a > 40 else 0).getbbox()
    lm = json.loads((d / 'landmarks.json').read_text()) if (d / 'landmarks.json').exists() else {}
    full = kind_of(ident) != 'humanoid' or UNITS.get(ident, {}).get('VisualClass') in FULL_FIGURE_CLASSES \
        or not (lm.get('head_top') and lm.get('chin'))
    size = 1024
    if full:
        side = max(x1 - x0, y1 - y0) * 1.06
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    else:
        top, chin = lm['head_top'], lm['chin']
        feet = [lm[k][1] for k in ('ankle_R', 'ankle_L', 'toe_R', 'toe_L') if lm.get(k)]
        tall = (max(feet) if feet else y1) - top[1]
        side = tall * 0.5
        cx = (top[0] + chin[0]) / 2
        cy = (top[1] + chin[1]) / 2 + side * 0.24
    crop = fig.crop((round(cx - side / 2), round(cy - side / 2), round(cx + side / 2), round(cy + side / 2)))
    crop = crop.resize((size, size), Image.LANCZOS)
    if not full:
        fade = Image.linear_gradient('L').resize((size, size))
        cut = fade.point(lambda v: 255 if v < 205 else max(0, 255 - (v - 205) * 5))
        crop.putalpha(ImageChops.multiply(crop.getchannel('A'), cut))
    dest.parent.mkdir(parents=True, exist_ok=True)
    crop.save(dest)
    return 'ok'


STAGES = {'master': stage_master, 'landmarks': stage_landmarks, 'body': stage_body, 'torso': stage_torso,
          'layers': stage_layers, 'icon': stage_icon}


def run(ident, stages, force):
    """Stages in order; a stage that has failed twice for a unit is not tried again unless forced (a follow loop must
    not spend the image budget on a unit that keeps failing; failures.json records them)."""
    fails_p = PUPPET / ident / 'failures.json'
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


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('ids')
    ap.add_argument('--stages', default='master,landmarks,body,torso,layers')
    ap.add_argument('--force', default='', help='stages to redo even if their output exists')
    ap.add_argument('--jobs', type=int, default=3)
    a = ap.parse_args()
    ids = [i for i in UNITS if (REVIEW / 'units' / i / 'metadata.json').exists()] if a.ids == 'all' else a.ids.split(',')
    stages = a.stages.split(',')
    force = set(filter(None, a.force.split(',')))
    PUPPET.mkdir(parents=True, exist_ok=True)
    (PUPPET / '.gdignore').touch()
    with cf.ThreadPoolExecutor(a.jobs) as ex:
        results = list(ex.map(lambda i: run(i, stages, force), ids))
    bad = [r for r in results if r[1] != 'done']
    print('PUPPET_SOURCES', len(results) - len(bad), 'done;', 'failed:', bad, flush=True)


if __name__ == '__main__':
    main()
