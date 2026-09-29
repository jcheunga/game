"""Build the Lantern Caravan source model and render the game's 9:7 sprite.

Run with Blender --background --factory-startup --python this_file -- [options].
Everything is modeled here; there are no downloaded models, fonts, or textures.
"""

import argparse
import json
import math
from pathlib import Path
import random
import sys

import bpy
from mathutils import Vector

SOURCE = Path(__file__).resolve().parent
ROOT = SOURCE.parent.parent
sys.path.insert(0, str(SOURCE))
from crownroad_art import (area_light, beam, box, camera, collection, cylinder,
                          material, mesh, ring, setup_render, tube)


parser = argparse.ArgumentParser()
parser.add_argument('--samples', type=int, default=96)
parser.add_argument('--width', type=int, default=1440)
parser.add_argument('--preview', action='store_true', help='Also render the studio presentation.')
parser.add_argument('--no-render', action='store_true')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
if args.width % 9:
    parser.error('--width must be divisible by 9 (the battle slot has a 9:7 aspect ratio).')

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for datablocks in (bpy.data.materials, bpy.data.meshes, bpy.data.curves, bpy.data.cameras, bpy.data.lights):
    for block in list(datablocks):
        if block.users == 0:
            datablocks.remove(block)
bpy.context.preferences.filepaths.save_version = 0
random.seed(41)

chassis = collection('01 • Chassis and suspension')
hull = collection('02 • Oak cabin and armor')
wheels = collection('03 • Four independent wheels')
deck = collection('04 • Roof gallery and mounts')
cloth = collection('05 • Teal canvas and heraldry')
supplies = collection('06 • Cargo and tools')
lanterns = collection('07 • Lanterns')
rig = collection('08 • Cameras and light rig')
studio = collection('09 • Presentation only — hidden in sprite')

oak = material('Oak • warm weathered grain', '302013', variation='674a2e', grain=(1, 23, 23))
oak_light = material('Oak • cut ends and worn edges', '493425', variation='806044', grain=(1, 18, 18))
oak_dark = material('Oak • tarred undercarriage', '261b14', variation='493b2a', grain=(1, 14, 14))
iron = material('Iron • blue black hammered', '20272b', 0.58, 0.72, '444e52')
iron_light = material('Iron • polished contact edges', '697478', 0.38, 0.82)
brass = material('Brass • aged ochre', '947135', 0.48, 0.72, 'b89756')
brass_dark = material('Brass • tarnished recesses', '72552b', 0.53, 0.65)
teal = material('Canvas • Lantern Caravan teal', '0e3432', 0.9, variation='26544d', grain=(6, 6, 8))
teal_dark = material('Paint • weathered teal shield', '153f3e', 0.7, variation='31645a')
linen = material('Rope • waxed flax', '988165', 0.94, variation='c3aa81')
leather = material('Leather • oxblood harness', '38251d', 0.78, variation='63412d')
void = material('Recess • deep warm shadow', '121b1b', 0.95)
amber = material('Glass • amber lantern core', 'ffb14c', 0.34, emission=1.4)


def rivet(x, y, z, size=0.035, mat=brass_dark, coll=hull, axis='Y'):
    return cylinder('Hand-forged rivet', (x, y, z), size, 0.027, mat, coll, axis, vertices=8, bevel=0.004)


def finial(x, y, z, coll=deck):
    cylinder('Finial collar', (x, y, z + 0.035), 0.071, 0.07, brass_dark, coll)
    verts = [(x, y, z + 0.32), (x - 0.085, y, z + 0.13), (x, y - 0.07, z + 0.13),
             (x + 0.085, y, z + 0.13), (x, y + 0.07, z + 0.13), (x, y, z + 0.02)]
    faces = [(0, 1, 2), (0, 2, 3), (0, 3, 4), (0, 4, 1), (5, 2, 1), (5, 3, 2), (5, 4, 3), (5, 1, 4)]
    mesh('Spearhead finial', verts, faces, brass, coll)


# Heavy box-section frame, visible through the open spokes.
for y in (-0.68, 0.68):
    box('Long oak chassis rail', (0, y, 1.0), (5.1, 0.24, 0.28), oak_dark, chassis)
for x in (-1.76, 1.66):
    cylinder('Forged axle', (x, 0, 0.86), 0.12, 2.85, iron, chassis, 'Y')
    for y in (-0.72, 0.72):
        for layer in range(3):
            coords = [(x + (i / 12 - .5) * (1.35 - layer * .18), y,
                       0.91 + layer * .055 + .17 * math.sin(math.pi * i / 12)) for i in range(13)]
            tube('Laminated spring', coords, 0.028, iron, chassis)
for x in (-2.35, -1.2, 0, 1.2, 2.35):
    box('Transverse bed support', (x, 0, 1.2), (.16, 2.3, .18), oak_dark, chassis)
for i in range(12):
    box('Bed floor board', (-2.35 + i * .425, 0, 1.36), (.407, 2.3, .16), oak_light, chassis, .018)
for y in (-1.16, 1.16):
    box('Bed iron sill', (0, y, 1.4), (5.35, .1, .16), iron, chassis)
    for i in range(18):
        rivet(-2.5 + i * .294, y * 1.045, 1.4, coll=chassis)


# Four spoked wheels. Each wheel is parented to a named hub for later animation.
for x, label in ((-1.76, 'Rear'), (1.66, 'Front')):
    for side, y in (('Near', -1.25), ('Far', 1.25)):
        previous = set(wheels.objects)
        center = (x, y, .87)
        ring(label + ' wheel oak felloe', center, .835, .64, .26, oak, wheels)
        ring(label + ' wheel outer iron tire', center, .89, .818, .29, iron, wheels)
        for f in (-1, 1):
            ring('Bright worn wheel lip', (x, y + f * .137, .87), .873, .837, .027, iron_light, wheels)
        for i in range(10):
            a = i * math.tau / 10
            p = (x + math.cos(a) * .67, y, .87 + math.sin(a) * .67)
            beam('Tapered oak spoke', center, p, .115, .16, oak_light, wheels, .02)
        cylinder('Wheel hub oak barrel', center, .2, .43, oak_dark, wheels, 'Y', 20)
        front_y = y + (-.25 if y < 0 else .25)
        cylinder('Wheel hub iron cap', (x, front_y, .87), .18, .11, iron, wheels, 'Y', 20)
        ring('Hub brass inlay', (x, front_y + (-.062 if y < 0 else .062), .87), .137, .118, .025, brass, wheels)
        rivet(x, front_y + (-.08 if y < 0 else .08), .87, .082, brass, wheels)
        for i in range(20):
            a = i * math.tau / 20
            rivet(x + math.cos(a) * .765, y + (-.15 if y < 0 else .15), .87 + math.sin(a) * .765,
                  .027, iron_light, wheels)
        pivot = bpy.data.objects.new(f'WHEEL_{label.upper()}_{side.upper()} • rotate local Y', None)
        wheels.objects.link(pivot)
        pivot.location = center
        pivot.empty_display_size = .3
        for obj in set(wheels.objects) - previous - {pivot}:
            obj.parent = pivot
            obj.matrix_parent_inverse = pivot.matrix_world.inverted()
            # Empty's world transform needs an explicit update before parenting.
            obj.matrix_parent_inverse.translation = -Vector(center)


# Cabin planks: horizontal grain and subtle variations in weathering.
box('Shadow core behind cabin boards', (-.22, 0, 2.24), (4.45, 1.89, 1.75), oak_dark, hull)
for side in (-1, 1):
    y = side * 1.005
    for row in range(7):
        z = 1.61 + row * .225
        for segment in range(3):
            x = -1.75 + segment * 1.51
            box('Individual side oak board', (x, y, z), (1.484, .12, .214),
                oak_light if (row + segment) % 6 == 0 else oak, hull, .012)
    for x in (-2.48, -.87, .82, 2.12):
        box('Cabin vertical timber', (x, y + side * .06, 2.32), (.16, .18, 1.88), oak_dark, hull)
        box('Protective forged upright', (x, y + side * .16, 2.3), (.12, .055, 1.83), iron, hull, .013)
        for z in (1.52, 1.87, 2.25, 2.62, 3.06):
            rivet(x, y + side * .203, z, .032)
    for z in (1.5, 2.04, 3.11):
        box('Long brass-edged iron strap', (-.17, y + side * .105, z), (4.9, .08, .11), iron, hull)
        for x in (-2.25, -1.5, -.6, .3, 1.25, 1.92):
            rivet(x, y + side * .156, z, .027)

for x in (-2.5, 2.14):
    for row in range(7):
        box('End-wall board', (x, 0, 1.61 + row * .225), (.12, 1.94, .211), oak, hull, .012)
    for y in (-.85, 0, .85):
        box('End-wall armored upright', (x + (.085 if x > 0 else -.085), y, 2.26), (.065, .11, 1.84), iron, hull)

# Hand-cut checks, scrapes, repair plates, and reinforced corners make the wood
# feel road-worn while keeping the broad silhouette legible at 180px wide.
for side in (-1, 1):
    for row in range(7):
        for segment in range(3):
            if (row + segment) % 3 == 1:
                continue
            x = -2.45 + segment * 1.51 + random.uniform(.08, .26)
            z = 1.61 + row * .225 + random.uniform(-.065, .065)
            length = random.uniform(.12, .43)
            y = side * 1.07
            tube('Weather check along oak grain', [(x, y, z), (x + length * .45, y + side * .003, z + .012),
                                                  (x + length, y, z - .007)], .0055, oak_dark, hull)
            if row % 2 == 0:
                tube('Pale worn wood fiber', [(x + .025, y + side * .003, z - .018),
                                            (x + length * .7, y + side * .003, z - .012)], .003, oak_light, hull)
    for x in (-2.39, 2.03):
        direction = 1 if x < 0 else -1
        verts = [(x, side * 1.22, 1.5), (x + direction * .38, side * 1.22, 1.5), (x, side * 1.22, 1.9)]
        mesh('Triangular lower armor gusset', verts, [(0, 1, 2)], iron, hull)
        for dx, dz in ((0, .04), (.25, .04), (0, .27)):
            rivet(x + direction * dx, side * 1.235, 1.54 + dz, .029, brass_dark)
for x, z in ((-1.19, 1.87), (.49, 2.98)):
    box('Old board repair strap', (x, -1.087, z), (.33, .027, .085), iron, hull, .008)
    for dx in (-.115, .115):
        rivet(x + dx, -1.11, z, .021, iron_light)


def gothic_panel(name, cx, y, bottom, width, height, mat, coll):
    outline = [(-.5, 0), (.5, 0), (.5, .72), (.31, .88), (0, 1), (-.31, .88), (-.5, .72)]
    coords = [(cx + a * width, y, bottom + b * height) for a, b in outline]
    mesh(name, coords, [tuple(range(len(coords)))], mat, coll)
    return coords


# Deep arrow slits and carved surrounds: readable broad motifs at battle size.
for x in (-1.64, 1.42):
    coords = gothic_panel('Pointed arrow-slit recess', x, -1.08, 2.18, .53, .68, void, hull)
    tube('Forged gothic window rim', [(p[0], -1.125, p[2]) for p in coords], .045, iron, hull, True)
    tube('Window gold accent', [(p[0], -1.177, p[2]) for p in coords[2:]], .013, brass, hull)
    for dx in (-.12, .12):
        beam('Vertical slit grille', (x + dx, -1.14, 2.22), (x + dx, -1.14, 2.72), .034, .04, iron_light, hull, .007)
    box('Arrow-slit sill', (x, -1.15, 2.15), (.7, .19, .09), oak_light, hull)

# Central armored service door and step.
gothic_panel('Side service door backing', -.18, -1.11, 1.56, .86, 1.46, oak_dark, hull)
for i in range(4):
    box('Service door vertical board', (-.485 + i * .205, -1.13, 2.14), (.193, .065, 1.12), oak, hull, .01)
for z in (1.76, 2.46):
    box('Door hinge strap', (-.18, -1.18, z), (.84, .06, .08), iron, hull)
    for x in (-.5, .14):
        rivet(x, -1.221, z, .033, brass)
ring('Door pull', (.075, -1.235, 2.08), .066, .041, .025, brass, hull)
box('Fold-out running step', (-.18, -1.39, 1.26), (1.04, .46, .12), oak_dark, chassis)
for x in (-.57, .23):
    beam('Step iron support', (x, -1, 1.45), (x, -1.57, 1.2), .07, .07, iron, chassis)

# Front towing assembly, with a heavy hitch and drooping chain.
for y in (-.58, .58):
    beam('Tow drawbar', (1.75, y, 1.02), (3.1, y * .55, .82), .18, .16, oak_dark, chassis)
beam('Tow crossbar', (3.06, -.4, .82), (3.06, .4, .82), .14, .14, iron, chassis)
ring('Tow ring', (3.29, 0, .82), .17, .105, .08, iron, chassis, 'Z')
beam('Tow collar', (3.03, 0, .82), (3.28, 0, .82), .11, .1, iron, chassis)
for i in range(11):
    t = i / 10
    p = (2.48 + .58 * t, -.45, .87 - .26 * math.sin(t * math.pi))
    ring('Safety chain link', p, .053, .035, .025, iron_light, chassis, 'Y' if i % 2 else 'Z', 16)


# Wooden roof deck and crenellated rail: mounting space for future upgrades.
for i in range(13):
    box('Roof floor plank', (-2.55 + i * .4, 0, 3.23), (.385, 2.32, .14), oak, deck, .015)
for side in (-1, 1):
    y = side * 1.13
    for z in (3.28, 3.73):
        box('Gallery horizontal rail', (-.16, y, z), (5.12, .12, .11), oak_dark, deck)
        box('Rail brass beading', (-.16, y + side * .073, z + .02), (5.1, .025, .024), brass_dark, deck, .006)
    for x in (-2.64, -1.6, -.55, .52, 1.56, 2.35):
        box('Gallery upright', (x, y, 3.57), (.095, .095, .66), iron, deck, .02)
        cylinder('Gallery collar', (x, y, 3.77), .075, .065, brass, deck)
        finial(x, y, 3.8)
    for x in (-2.15, -1.08, -.02, 1.04, 1.95):
        box('Gallery defensive panel', (x, y, 3.49), (.66, .07, .29), oak, deck, .015)
        for dx in (-.25, .25):
            rivet(x + dx, y + side * .06, 3.5, .024, brass, deck)
for x in (-2.64, 2.35):
    for z in (3.28, 3.73):
        box('Gallery end rail', (x, 0, z), (.12, 2.34, .11), oak_dark, deck)


def shield(cx, y, z, scale=1):
    points = [(-.34, .37), (.34, .37), (.31, -.02), (.2, -.24), (0, -.43), (-.2, -.24), (-.31, -.02)]
    coords = [(cx + a * scale, y - .03 * (1 - abs(a) / .34), z + b * scale) for a, b in points]
    mesh('Teal heraldic shield', coords, [tuple(range(7))], teal_dark, cloth)
    tube('Shield bound brass rim', [(a, b - .025, c) for a, b, c in coords], .022 * scale, brass, cloth, True)
    # An original lantern-and-crown device, built as metalwork geometry.
    yy = y - .062
    w, h = .105 * scale, .25 * scale
    box('Heraldic lantern field', (cx, yy, z - .03 * scale), (w * 1.25, .024, h), brass, cloth, .008)
    for zz in (-.17, .1):
        box('Heraldic lantern cap', (cx, yy - .007, z + zz * scale), (w * 2, .027, .028 * scale), brass, cloth, .003)
    ring('Heraldic lantern handle', (cx, yy, z + .15 * scale), .045 * scale, .029 * scale, .023, brass, cloth, segments=20)
    for dx in (-.065, .065):
        beam('Heraldic dark mullion', (cx + dx * scale, yy - .022, z - .15 * scale),
             (cx + dx * scale, yy - .022, z + .075 * scale), .013, .025, teal_dark, cloth, .002)
    for side in (-1, 1):
        tube('Heraldic laurel stem', [(cx + side * (.17 + .055 * math.sin(t * math.pi)) * scale,
                                     yy, z + (-.24 + .42 * t) * scale) for t in [i / 9 for i in range(10)]],
             .008 * scale, brass, cloth)
        for i in range(4):
            t = i / 4
            xx = cx + side * (.17 + .055 * math.sin(t * math.pi)) * scale
            zz = z + (-.2 + .34 * t) * scale
            beam('Heraldic laurel leaf', (xx, yy, zz), (xx + side * .055 * scale, yy, zz + .05 * scale),
                 .024 * scale, .018, brass, cloth, .006)


shield(-1.04, -1.245, 3.48, .82)
shield(1.07, -1.245, 3.48, .82)

# Pleated side awning over the front side window, with stitched gold hem.
verts, faces = [], []
nx, ny = 25, 9
for j in range(ny):
    t = j / (ny - 1)
    for i in range(nx):
        u = i / (nx - 1)
        x = .64 + u * 1.86
        y = -1.02 - .54 * t
        z = 3.2 - .3 * t - .08 * math.sin(math.pi * t) + .026 * math.sin(u * math.pi * 12) * t
        verts.append((x, y, z))
for j in range(ny - 1):
    for i in range(nx - 1):
        k = j * nx + i
        faces.append((k, k + 1, k + nx + 1, k + nx))
awning = mesh('Pleated teal side awning', verts, faces, teal, cloth, smooth=True)
awning.modifiers.new('Canvas thickness', 'SOLIDIFY').thickness = .017
tube('Awning stitched leading edge', verts[-nx:], .016, brass_dark, cloth)
for i in range(8):
    x = .64 + i * 1.86 / 7
    beam('Awning tassel', (x, -1.56, 2.89), (x, -1.57, 2.77 - .025 * (i % 2)), .027, .027, linen, cloth)
for x in (.65, 2.49):
    beam('Awning bracket', (x, -1.09, 2.56), (x, -1.58, 2.9), .045, .045, iron, deck)

# A tall wind-shaped swallowtail. It is deliberately short enough to read in 9:7.
pole_x, pole_y = -1.8, .23
cylinder('Banner oak mast', (pole_x, pole_y, 4.1), .044, 2.47, oak_dark, cloth, vertices=16)
for z in (3.14, 3.45, 4.57, 5.21):
    cylinder('Mast bronze collar', (pole_x, pole_y, z), .066, .085, brass, cloth)
finial(pole_x, pole_y, 5.3, cloth)
verts, faces = [], []
nx, ny = 35, 13
for j in range(ny):
    v = j / (ny - 1)
    for i in range(nx):
        u = i / (nx - 1)
        length = 1.96 - .43 * (1 - abs(v * 2 - 1)) ** 2
        x = pole_x + u * length
        y = pole_y - .1 + .13 * math.sin(u * math.pi * 3.5 + v * .9) * u
        z = 5.21 - v * .68 - .19 * u + .06 * math.sin(u * math.pi * 2) * u
        verts.append((x, y, z))
for j in range(ny - 1):
    for i in range(nx - 1):
        k = j * nx + i
        faces.append((k, k + 1, k + nx + 1, k + nx))
flag = mesh('Wind-shaped swallowtail banner', verts, faces, teal, cloth, smooth=True)
flag.modifiers.new('Canvas thickness', 'SOLIDIFY').thickness = .012
for indices in (range(nx), range((ny - 1) * nx, ny * nx), range(nx - 1, nx * ny, nx)):
    tube('Banner ochre stitched border', [verts[k] for k in indices], .013, brass_dark, cloth)
# Simple gold radiating lantern device on the flag, following the near surface.
fx, fy, fz = -1.23, .02, 4.84
box('Banner lantern symbol', (fx, fy, fz), (.14, .013, .21), brass, cloth, .005)
for zz in (-.13, .13):
    box('Banner lantern cap', (fx, fy, fz + zz), (.23, .016, .023), brass, cloth, .003)
ring('Banner lantern loop', (fx, fy, fz + .19), .047, .031, .016, brass, cloth, segments=20)
for angle in (0, math.pi / 4, math.pi, 3 * math.pi / 4):
    beam('Banner rays', (fx + math.cos(angle) * .21, fy, fz + math.sin(angle) * .21),
         (fx + math.cos(angle) * .27, fy, fz + math.sin(angle) * .27), .017, .016, brass, cloth, .002)
tube('Mast lash rope', [(pole_x + .056 * math.cos(i * .65), pole_y + .056 * math.sin(i * .65),
                         3.13 + i * .008) for i in range(60)], .013, linen, cloth)


def barrel(name, x, y, z, radius=.27, height=.65):
    segments = 14
    profile = [(0, .85), (.08, .91), (.35, 1), (.65, 1), (.92, .91), (1, .85)]
    for i in range(segments):
        a, b = i * math.tau / segments + .009, (i + 1) * math.tau / segments - .009
        verts = [(x + math.cos(angle) * radius * r, y + math.sin(angle) * radius * r, z + t * height)
                 for t, r in profile for angle in (a, b)]
        faces = [(j * 2, j * 2 + 1, j * 2 + 3, j * 2 + 2) for j in range(len(profile) - 1)]
        mesh(name + ' stave', verts, faces, oak_light if i % 4 == 0 else oak, supplies, .006)
    for t, r in ((.1, .93), (.3, .995), (.78, .97), (.93, .92)):
        ring(name + ' iron hoop', (x, y, z + t * height), radius * r + .012,
             radius * r - .016, .047, iron, supplies, 'Z', 32)
    cylinder(name + ' lid', (x, y, z + height), radius * .84, .036, oak_dark, supplies, vertices=14)
    for offset in (-.09, .09):
        box(name + ' lid batten', (x + offset, y, z + height + .025), (.044, radius * 1.45, .025), oak_light, supplies, .004)


barrel('Front provision cask', 2.47, .17, 1.44, .29, .7)
barrel('Roof water cask', -1.73, .57, 3.32, .25, .56)
barrel('Roof small cask', -2.24, .39, 3.32, .2, .48)

for x, y, z, sx in ((-.53, .24, 3.57, .75), (.31, .27, 3.54, .7)):
    box('Roof supply chest', (x, y, z), (sx, .62, .46), oak, supplies)
    box('Chest lid', (x, y, z + .25), (sx + .04, .67, .08), oak_light, supplies)
    for dx in (-sx * .3, sx * .3):
        box('Chest iron binding', (x + dx, y, z + .305), (.045, .68, .025), iron, supplies)
        box('Chest face binding', (x + dx, y - .325, z), (.045, .026, .5), iron, supplies)
    box('Chest clasp', (x, y - .35, z + .12), (.1, .04, .18), brass_dark, supplies)
    rivet(x, y - .382, z + .09, .025, brass, supplies)

# A tied canvas bedroll and a small stack of firewood.
roll = cylinder('Rolled teal sleeping canvas', (.17, .52, 3.98), .17, 1.22, teal, supplies, 'X', 32)
for x in (-.28, .57):
    loop = ring('Bedroll leather strap', (0, 0, 0), .184, .168, .07, leather, supplies, 'Z', 32)
    loop.rotation_euler.y = math.pi / 2
    loop.location = (x, .52, 3.98)
for i in range(5):
    cylinder('Split firewood bundle', (-2.62, -.15 + i * .13, 1.68 + .03 * (i % 2)), .073, .65, oak_light,
             supplies, 'X', 7, .008)


def lantern(name, x, y, bottom, size=1):
    h = .45 * size
    cylinder(name + ' amber glass', (x, y, bottom + h / 2), .112 * size, h, amber, lanterns, vertices=8)
    for z in (bottom, bottom + h):
        cylinder(name + ' brass cap', (x, y, z), .158 * size, .055 * size, brass, lanterns, vertices=8)
    for i in range(4):
        a = math.pi / 4 + i * math.pi / 2
        xx, yy = x + math.cos(a) * .117 * size, y + math.sin(a) * .117 * size
        beam(name + ' cage', (xx, yy, bottom), (xx, yy, bottom + h), .021 * size, .021 * size, iron, lanterns, .004)
    bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=.176 * size, radius2=.055 * size,
                                    depth=.15 * size, location=(x, y, bottom + h + .09 * size))
    obj = bpy.context.object
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    lanterns.objects.link(obj)
    obj.name = name + ' pagoda hood'
    obj.data.materials.append(brass_dark)
    ring(name + ' hanging loop', (x, y, bottom + h + .23 * size), .062 * size, .043 * size,
         .023 * size, brass, lanterns, segments=20)
    data = bpy.data.lights.new(name + ' warm spill', 'POINT')
    data.color, data.energy, data.shadow_soft_size = (1, .38, .085), 12 * size, .16
    light = bpy.data.objects.new(name + ' warm spill', data)
    lanterns.objects.link(light)
    light.location = (x, y - .1, bottom + h / 2)


# Curled brackets at each end; lanterns are the caravan's warm focal points.
for x, sign, z in ((-2.5, -1, 2.85), (2.24, 1, 3.04)):
    coords = [(x, -1.0, z), (x + sign * .18, -1.05, z + .21), (x + sign * .45, -1.08, z + .2),
              (x + sign * .54, -1.08, z + .09), (x + sign * .49, -1.08, z - .02)]
    tube('Curled lantern bracket', coords, .035, iron, lanterns)
    lantern('Road lantern', x + sign * .49, -1.08, z - .7, 1)
lantern('Gallery lantern', 2.3, .79, 3.71, .76)

# Presentation floor is never part of the exported transparent sprite.
ground_mat = material('Studio • midnight forest', '172925', .94)
floor = box('Studio floor', (0, 0, -.07), (200, 200, .1), ground_mat, studio, 0)
floor.hide_render = True
floor.hide_set(True)

scene = bpy.context.scene
setup_render(scene, args.samples)
scene.render.resolution_x, scene.render.resolution_y = args.width, args.width * 7 // 9
sprite_camera = camera('CAMERA • battle / locked 9:7', (7.5, -32, 12), (0.12, 0, 2.65), 8.7, rig)
hero_camera = camera('CAMERA • studio three-quarter', (10.5, -20, 11), (.15, 0, 2.55), 9.2, rig)
scene.camera = sprite_camera
area_light('KEY • warm dawn', (-4, -7, 10), (0, 0, 2), 1300, (1, .79, .56), 5, rig)
area_light('FILL • cool open sky', (4, -4, 6), (0, 0, 2), 650, (.61, .77, 1), 5, rig)
area_light('RIM • dusk gold', (1, 4, 8), (0, 0, 2.5), 1550, (1, .82, .52), 4, rig)
area_light('FRONT • broad soft bounce', (-1, -9, 3), (0, 0, 2), 170, (.77, .88, 1), 4, rig)

scene['asset_id'] = 'war_wagon'
scene['art_direction'] = 'Lantern Caravan: oak, hammered iron, antique brass, teal canvas, amber lanterns.'
scene['sprite_contract'] = '1440x1120 RGBA; 9:7 matches 180x140 battle slot; forward +X; near side -Y.'
scene['authoring_units'] = 'meters; ground z=0; wheel pivots rotate around Y'
scene['source_generator'] = 'art/blender/build_caravan.py'
scene['upgrade_mounts'] = 'Roof gallery; keep default deck free of purchased weapon models.'
scene['version'] = 1

# Useful first-open view: rendered material colors and the asset camera.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.region_3d.view_perspective = 'CAMERA'
            area.spaces.active.shading.color_type = 'MATERIAL'
            area.spaces.active.shading.light = 'STUDIO'
            area.spaces.active.overlay.show_extras = False
            area.spaces.active.clip_end = 300

sprite = ROOT / 'assets/structures/war_wagon.png'
blend = SOURCE / 'lantern_caravan.blend'
preview = ROOT / 'artifacts/blender/lantern_caravan_studio.png'
sprite.parent.mkdir(parents=True, exist_ok=True)
preview.parent.mkdir(parents=True, exist_ok=True)
scene.render.filepath = str(sprite)

# Verify the full silhouette (including curve objects) before publishing a sprite.
from bpy_extras.object_utils import world_to_camera_view
bpy.context.view_layer.update()
projected = [world_to_camera_view(scene, sprite_camera, obj.matrix_world @ Vector(corner))
             for obj in scene.objects if obj.type in ('MESH', 'CURVE') and not obj.hide_render
             for corner in obj.bound_box]
framing = [(min(p[i] for p in projected), max(p[i] for p in projected)) for i in (0, 1)]
if any(lo < .02 or hi > .98 for lo, hi in framing):
    raise RuntimeError(f'Caravan must have at least 2% frame padding; camera bounds are {framing}')
print('CROWNROAD_FRAMING_OK ' + json.dumps(framing), flush=True)
bpy.ops.wm.save_as_mainfile(filepath=str(blend))

if not args.no_render:
    bpy.ops.render.render(write_still=True)
    if args.preview:
        floor.hide_render = False
        floor.hide_set(False)
        scene.camera = hero_camera
        scene.render.film_transparent = False
        scene.render.resolution_x, scene.render.resolution_y = 1600, 1200
        scene.render.filepath = str(preview)
        bpy.ops.render.render(write_still=True)
        floor.hide_render = True
        floor.hide_set(True)
        scene.camera = sprite_camera
        scene.render.film_transparent = True
        scene.render.resolution_x, scene.render.resolution_y = args.width, args.width * 7 // 9
        scene.render.filepath = str(sprite)

print('CROWNROAD_ASSET_COMPLETE ' + json.dumps({
    'blend': str(blend), 'sprite': str(sprite), 'studio': str(preview) if args.preview else None,
    'objects': len(scene.objects), 'materials': len(bpy.data.materials),
    'resolution': [scene.render.resolution_x, scene.render.resolution_y],
    'device': scene.cycles.device,
}))
