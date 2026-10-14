#!/usr/bin/env python3
"""Joint landmarks for a puppet painting, located by Codex (vision) on a gridded copy of the painting.

  python3 art/remaster/puppet/landmarks.py <master.png> <out.json> [--kind humanoid|mount|quad|machine|serpent|blob]
"""
import argparse
import json
import re
import subprocess
import tempfile
from pathlib import Path

from PIL import Image, ImageDraw

KINDS = {
    'humanoid': ('"head_top", "chin", "neck", "chest", "pelvis", "shoulder_R", "elbow_R", "wrist_R", "shoulder_L", '
                 '"elbow_L", "wrist_L", "hip_R", "knee_R", "ankle_R", "toe_R", "hip_L", "knee_L", "ankle_L", "toe_L", '
                 '"weapon_grip", "weapon_tip", "offhand_item" (centre of a shield, lantern, flame or other item in the '
                 'other hand, or null), "facing" ("right" or "left")'),
    'mount': ('rider: "head_top", "chin", "neck", "chest", "pelvis", "shoulder_R", "elbow_R", "wrist_R", "shoulder_L", '
              '"elbow_L", "wrist_L", "weapon_grip", "weapon_tip"; horse: "horse_head" (muzzle), "horse_poll" (top of '
              'head), "horse_withers", "horse_croup", "tail_base", "tail_tip", "fl_shoulder", "fl_knee", "fl_hoof", '
              '"fr_shoulder", "fr_knee", "fr_hoof", "hl_hip", "hl_hock", "hl_hoof", "hr_hip", "hr_hock", "hr_hoof" '
              '(f/h = front/hind, l/r = the animal\'s own left/right); "facing"'),
    'quad': ('"head" (muzzle tip), "poll" (top of head), "withers", "croup", "tail_base", "tail_tip", "fl_shoulder", '
             '"fl_knee", "fl_paw", "fr_shoulder", "fr_knee", "fr_paw", "hl_hip", "hl_hock", "hl_paw", "hr_hip", '
             '"hr_hock", "hr_paw"; "facing"'),
    'machine': ('"body" (centre of the chassis), "wheel_front" (centre of the nearest front wheel), "wheel_back", '
                '"wheel_radius" (number of pixels), "arm_pivot" (where the throwing arm or bow is mounted), "arm_tip" '
                '(the bolt tip or the end of the throwing arm), "crew_head" and "crew_pelvis" (a crewman, or null); '
                '"facing"'),
    'serpent': '"head" (snout tip), "neck", "mid1", "mid2", "mid3", "tail_tip" (points along the spine); "facing"',
    'blob': '"top", "bottom", "center", "left", "right" (extremes and centre of the body); "facing"',
}


def gridded(src, dst):
    im = Image.open(src).convert('RGBA').resize((1024, 1024))
    bg = Image.new('RGBA', im.size, (221, 217, 209, 255))
    bg.alpha_composite(im)
    d = ImageDraw.Draw(bg)
    for v in range(0, 1025, 64):
        d.line([(v, 0), (v, 1024)], fill=(255, 0, 255, 255))
        d.line([(0, v), (1024, v)], fill=(255, 0, 255, 255))
        d.text((v + 2, 2), str(v), fill=(255, 0, 255, 255))
        d.text((2, v + 2), str(v), fill=(255, 0, 255, 255))
    bg.convert('RGB').save(dst)


def parse_reply(text):
    """The last JSON object in the reply, nested groups ({"rider": {...}, "horse": {...}}) flattened into one."""
    start = text.rfind('}')
    while start != -1:
        depth, i = 0, start
        while i >= 0:
            depth += {'}': 1, '{': -1}.get(text[i], 0)
            if depth == 0:
                break
            i -= 1
        try:
            obj = json.loads(text[i:start + 1])
        except (ValueError, IndexError):
            start = text.rfind('}', 0, start)
            continue
        flat = {}
        for k, v in obj.items():
            if isinstance(v, dict):
                flat.update(v)
            else:
                flat[k] = v
        return flat
    return None


def locate(master, out, kind='humanoid', model='gpt-5.6-sol'):
    with tempfile.TemporaryDirectory() as t:
        g = Path(t) / 'grid.png'
        gridded(master, g)
        prompt = (f'The image is a game character painting (1024x1024 pixels) with a magenta coordinate grid every 64 '
                  f'pixels (labels give x along the top, y down the left). Locate these points precisely and reply with '
                  f'ONLY a JSON object (no prose, no code fence), integer pixel coordinates [x, y] (null if absent): '
                  f'{KINDS[kind]}. R/L mean the character\'s own right/left. Use the visible joint centre even under '
                  f'armour, cloth or fur.')
        r = subprocess.run(['codex', 'exec', '--skip-git-repo-check', '-m', model, '-c', 'model_reasoning_effort="medium"',
                            '-s', 'read-only', '-C', t, '-i', str(g), '-'], input=prompt, capture_output=True, text=True,
                           timeout=900)
    data = parse_reply(r.stdout)
    if data is None:
        raise RuntimeError('no landmarks in Codex reply: ' + r.stdout[-500:] + r.stderr[-500:])
    data['kind'] = kind
    Path(out).write_text(json.dumps(data, indent=1) + '\n')
    return data


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('master')
    ap.add_argument('out')
    ap.add_argument('--kind', default='humanoid', choices=sorted(KINDS))
    a = ap.parse_args()
    print(json.dumps(locate(a.master, a.out, a.kind)))
