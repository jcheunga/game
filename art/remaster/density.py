"""Battle-sprite texel density: how many atlas pixels cover one pixel of the battle canvas.

A battle frame is drawn Radius * 2 * VisualScale * drawScale canvas pixels wide on the
1280x720 battle canvas (scripts/combat/Unit.cs). The helpers below mirror
Unit.ResolveVisualScale and Unit.ResolveRadius; keep them in step with that file.

The regular roster packs 192x240 frames at a density of roughly 2 (Swordsman 2.5), which
stays sharp when the canvas is stretched to a 1080p-1440p window or zoomed on mobile.
Bosses and the Siege Tower are drawn three to four times larger from the same 192 px frame,
so they fall to 0.5-0.8 and look soft and blocky. Units below MIN_DENSITY are rendered
from larger masters and packed into atlases cropped to their animation envelope at
TARGET_DENSITY instead.

Pure Python (no bpy) so both build_units.py and pack.py can import it.
"""
import math

STANDARD_FRAME = (192, 240)  # regular battle-atlas frame
MASTER_FRAME = (256, 320)    # Blender master frame at res_scale 1
TARGET_DENSITY = 2.0
MIN_DENSITY = 1.25
# Masters are rendered this much larger than the packed frame, like the regular 256 -> 192 pack.
SUPERSAMPLE = 4 / 3
MAX_ATLAS = 4096  # widely supported mobile texture limit

CLASS_SCALE = {
    'boss': 1.55, 'siegetower': 1.6, 'crusher': 1.25, 'brute': 1.18, 'bloater': 1.22,
    'berserker': 1.08, 'banner': 1.06, 'mirror': 1.06, 'howler': 1.04, 'necromancer': 1.0,
    'jammer': 0.98, 'shield': 1.08, 'sniper': 0.94, 'runner': 0.92, 'saboteur': 0.92, 'hound': 0.82,
}
RADIUS_ADD = {
    'boss': 6, 'siegetower': 6, 'bloater': 3, 'crusher': 3, 'brute': 2, 'banner': 1,
    'berserker': 1, 'howler': 1, 'mirror': 1, 'sniper': -1, 'runner': -1, 'hound': -2,
}


def visual_scale(unit):
    value = unit.get('VisualScale') or 0
    if value > 0:
        return min(max(value, 0.75), 1.8)
    return CLASS_SCALE.get(unit.get('VisualClass', ''), 1.0)


def frame_canvas_width(unit, draw_scale):
    """Canvas pixels covered by the full master frame width."""
    vs = visual_scale(unit)
    radius = 14 * vs + RADIUS_ADD.get(unit.get('VisualClass', ''), 0)
    return radius * 2 * vs * draw_scale


def standard_density(unit, draw_scale):
    return STANDARD_FRAME[0] / frame_canvas_width(unit, draw_scale)


def needs_hires(unit, draw_scale):
    return standard_density(unit, draw_scale) < MIN_DENSITY


def master_scale(unit, draw_scale):
    """Render-resolution multiplier for the Blender masters (quarter steps keep 256x320 integral)."""
    if not needs_hires(unit, draw_scale):
        return 1.0
    want = TARGET_DENSITY * SUPERSAMPLE * frame_canvas_width(unit, draw_scale) / MASTER_FRAME[0]
    return max(1.0, math.ceil(want * 4) / 4)
