"""Crownroad material/lighting finish. All textures are editable Blender nodes.

Generated coordinates travel with articulated parts; no world-space swimming,
external texture downloads, UV dependencies, or runtime shader costs.
"""
import json
import bpy
from mathutils import Vector

REVISION = 'roadworn-2.1'


def profile_for(name, metallic, emission):
    name = name.lower()
    if emission: return 'emissive'
    if 'water' in name: return 'water'
    if any(k in name for k in ('recess', 'archway', 'studio', 'shadow')): return 'quiet'
    if any(k in name for k in ('oak', 'wood', 'walnut', 'ash ')): return 'wood'
    if any(k in name for k in ('cloth', 'canvas', 'banner', 'flax', 'rope', 'artifact color')): return 'cloth'
    if 'paint' in name: return 'paint'
    if 'leather' in name: return 'leather'
    if any(k in name for k in ('bone', 'ivory')): return 'bone'
    if any(k in name for k in ('earth', 'road dust')): return 'ground'
    if any(k in name for k in ('stone', 'slate', 'roof', 'limestone')): return 'stone'
    if 'foliage' in name: return 'foliage'
    if 'skin' == name: return 'skin'
    if metallic >= .5: return 'metal'
    return 'quiet'


def recipe_for(mat):
    if mat.get('crownroad_surface_recipe'):
        return json.loads(mat['crownroad_surface_recipe'])
    if not mat.use_nodes: return None
    bsdf = next((n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if bsdf is None: return None
    base = list(bsdf.inputs['Base Color'].default_value)
    variant = None
    grain = None
    for node in mat.node_tree.nodes:
        if node.type == 'VALTORGB':
            base = list(node.color_ramp.elements[0].color)
            variant = list(node.color_ramp.elements[-1].color)
        if node.type == 'VECT_MATH' and node.operation == 'MULTIPLY':
            grain = list(node.inputs[1].default_value)
    emission = bsdf.inputs['Emission Strength'].default_value
    metal = bsdf.inputs['Metallic'].default_value
    return dict(base=base, variant=variant, grain=grain,
                roughness=bsdf.inputs['Roughness'].default_value,
                metallic=metal, emission=emission,
                emission_color=list(bsdf.inputs['Emission Color'].default_value),
                profile=profile_for(mat.name, metal, emission))


def scaled(color, amount):
    return tuple(min(1, max(0, x * amount)) for x in color[:3]) + (1,)


def finish_material(mat, recipe=None):
    r = recipe or recipe_for(mat)
    if r is None: return False
    mat['crownroad_surface_recipe'] = json.dumps(r)
    mat['crownroad_finish'] = REVISION
    mat['surface_family'] = r['profile']
    mat.diffuse_color = r['base']
    mat.use_nodes = True
    nodes, links = mat.node_tree.nodes, mat.node_tree.links
    nodes.clear()

    def node(kind, label, x, y):
        result = nodes.new('ShaderNode' + kind)
        result.label = result.name = label
        result.location = (x, y)
        return result

    def noise(label, vector, scale, detail, x, y):
        n = node('TexNoise', label, x, y)
        n.inputs['Scale'].default_value = scale
        n.inputs['Detail'].default_value = detail
        n.inputs['Roughness'].default_value = .66
        links.new(vector, n.inputs['Vector'])
        return n.outputs['Fac']

    def ramp(label, value, dark, light, x, y):
        n = node('ValToRGB', label, x, y)
        n.color_ramp.elements[0].position = .2
        n.color_ramp.elements[0].color = dark
        n.color_ramp.elements[1].position = .8
        n.color_ramp.elements[1].color = light
        links.new(value, n.inputs[0])
        return n.outputs['Color']

    out = node('OutputMaterial', 'Finished surface', 980, 200)
    bsdf = node('BsdfPrincipled', 'Material response', 670, 200)
    links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    family = r['profile']
    bsdf.inputs['Base Color'].default_value = r['base']
    bsdf.inputs['Roughness'].default_value = r['roughness']
    bsdf.inputs['Metallic'].default_value = r['metallic']
    if family == 'emissive':
        bsdf.inputs['Emission Color'].default_value = r['emission_color']
        bsdf.inputs['Emission Strength'].default_value = r['emission'] * .85
        return True
    if family == 'quiet': return True

    coord = node('TexCoord', 'Part-local coordinates — animation safe', -1100, 200)
    mapping = node('VectorMath', 'Grain direction', -880, 200)
    mapping.operation = 'MULTIPLY'
    grain = r['grain'] or (1, 1, 1)
    if family == 'ground': grain = (.22, .22, .22)
    if family == 'water': grain = (4, 14, 1)
    mapping.inputs[1].default_value = grain
    links.new(coord.outputs['Object' if family == 'ground' else 'Generated'], mapping.inputs[0])
    vector = mapping.outputs[0]
    macro = noise('Broad material variation', vector, 3.5, 2.5, -650, 440)
    fine = noise('Fine surface grain', vector, 52 if family != 'ground' else 13, 2, -650, -100)
    dark = scaled(r['base'], .82 if family in ('cloth', 'paint', 'leather') else .94)
    light = r['variant']
    if light is None or sum(abs(a-b) for a,b in zip(light[:3], r['base'][:3])) < .005:
        light = scaled(r['base'], 1.2 if family != 'skin' else 1.08)
    color = ramp('Pigment / natural color', macro, dark, light, -380, 460)
    if family in ('metal', 'paint'):
        geometry = node('NewGeometry', 'Convex crafted edges', -860, 710)
        wear = node('MapRange', 'Restrained edge burnishing', -610, 710)
        wear.inputs['From Min'].default_value = .51
        wear.inputs['From Max'].default_value = .59
        wear.inputs['To Max'].default_value = .22 if family == 'metal' else .13
        links.new(geometry.outputs['Pointiness'], wear.inputs['Value'])
        polish = node('MixRGB', 'Worn edges catch light', -80, 710)
        links.new(wear.outputs['Result'], polish.inputs[0])
        links.new(color, polish.inputs[1])
        polish.inputs[2].default_value = scaled(light, 1.45)
        color = polish.outputs[0]
    if family in ('stone', 'wood'):
        info = node('ObjectInfo', 'Individual block / timber tone', -860, 710)
        shade = node('MapRange', 'Small natural variation', -610, 710)
        shade.inputs['To Min'].default_value = .84
        shade.inputs['To Max'].default_value = 1.08
        links.new(info.outputs['Random'], shade.inputs['Value'])
        multiply = node('MixRGB', 'Varied pieces, one palette', -80, 710)
        multiply.blend_type = 'MULTIPLY'
        multiply.inputs[0].default_value = 1
        links.new(color, multiply.inputs[1])
        links.new(shade.outputs['Result'], multiply.inputs[2])
        color = multiply.outputs[0]

    # Broad variation carries at game size; fine relief is deliberately restrained.
    settings = {
        'wood': (.58, .82, .28, .012), 'metal': (.27, .48, .16, .0025),
        'cloth': (.76, .95, .26, .002), 'leather': (.53, .79, .24, .004),
        'paint': (.39, .62, .2, .003), 'bone': (.61, .84, .2, .003),
        'stone': (.78, .96, .36, .035), 'ground': (.88, 1, .3, .045),
        'foliage': (.85, .98, .13, .008), 'skin': (.65, .83, .12, .0015),
        'water': (.2, .35, .22, .016),
        'cut_steel': (.4, .62, .08, .001),
    }
    low, high, strength, distance = settings[family]
    rough = node('MapRange', 'Broken highlights — roughness', -80, -140)
    rough.inputs['To Min'].default_value = low
    rough.inputs['To Max'].default_value = high
    links.new(fine, rough.inputs['Value'])
    links.new(rough.outputs['Result'], bsdf.inputs['Roughness'])
    height = fine
    if family == 'cloth':
        waves = []
        for index, direction in enumerate(('DIAGONAL', 'Z')):
            wave = node('TexWave', 'Woven ' + ('warp' if index == 0 else 'weft'), -650, -360-index*240)
            wave.bands_direction = direction
            wave.inputs['Scale'].default_value = 65
            wave.inputs['Distortion'].default_value = .55
            links.new(coord.outputs['Generated'], wave.inputs['Vector'])
            waves.append(wave.outputs['Fac'])
        weave = node('Math', 'Crossed threads', -370, -390)
        weave.operation = 'MULTIPLY'
        for i, value in enumerate(waves): links.new(value, weave.inputs[i])
        height = weave.outputs[0]
        bsdf.inputs['Sheen Weight'].default_value = .16
        bsdf.inputs['Sheen Tint'].default_value = scaled(light, .85)
        bsdf.inputs['Sheen Roughness'].default_value = .65
    elif family == 'metal':
        bsdf.inputs['Metallic'].default_value = .88
        # Very light directional tooling, not mirror-chrome reflections.
        bsdf.inputs['Anisotropic'].default_value = .16
    elif family == 'cut_steel':
        # Broad single-plane axe heads need a diffuse contribution to stay legible
        # between reflections. Preserve the dark armor response on the body.
        bsdf.inputs['Metallic'].default_value = .5
    elif family in ('paint', 'leather'):
        bsdf.inputs['Coat Weight'].default_value = .13 if family == 'paint' else .06
        bsdf.inputs['Coat Roughness'].default_value = .45
    elif family == 'skin':
        bsdf.inputs['Subsurface Weight'].default_value = .035
        bsdf.inputs['Subsurface Scale'].default_value = .025
    if family == 'wood':
        height = noise('Long cut wood fibers', vector, 7, 3, -650, -370)
    if family == 'ground' and 'road' in mat.name.lower():
        separate = node('SeparateXYZ', 'Road edge coordinate', -850, -540)
        links.new(coord.outputs['Generated'], separate.inputs[0])
        center = node('Math', 'Distance from road center', -620, -540)
        center.operation = 'SUBTRACT'
        center.inputs[1].default_value = .5
        links.new(separate.outputs['Y'], center.inputs[0])
        absolute = node('Math', 'Both road verges', -390, -540)
        absolute.operation = 'ABSOLUTE'
        links.new(center.outputs[0], absolute.inputs[0])
        edge = node('MapRange', 'Dust blends into the verge', -100, -540)
        edge.inputs['From Min'].default_value = .24
        edge.inputs['From Max'].default_value = .49
        edge.inputs['To Min'].default_value = 1
        edge.inputs['To Max'].default_value = 0
        links.new(absolute.outputs[0], edge.inputs['Value'])
        links.new(edge.outputs['Result'], bsdf.inputs['Alpha'])
    bump = node('Bump', 'Micro relief — no silhouette changes', 350, -140)
    bump.inputs['Strength'].default_value = strength
    bump.inputs['Distance'].default_value = distance
    links.new(height, bump.inputs['Height'])
    links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])

    if family not in ('ground', 'water', 'foliage'):
        ao = node('AmbientOcclusion', 'Subtle contact / crevice shading', -80, 420)
        ao.inputs['Distance'].default_value = .2 if family == 'stone' else .12
        ao.samples = 8
        contact = node('MixRGB', 'Keep recesses readable', 340, 410)
        contact.blend_type = 'MULTIPLY'
        contact.inputs[0].default_value = .3
        links.new(color, contact.inputs[1])
        links.new(ao.outputs['Color'], contact.inputs[2])
        color = contact.outputs[0]
    links.new(color, bsdf.inputs['Base Color'])
    return True


def finish_scene_materials(scene):
    # Shields are painted surfaces, not the same fabric as the matching tabard.
    for obj in scene.objects:
        if obj.type != 'MESH' or not obj.name.startswith(('Kite shield face', 'Heraldic shield')): continue
        for slot in obj.material_slots:
            if not slot.material: continue
            r = recipe_for(slot.material)
            if r and r['profile'] == 'cloth':
                painted = slot.material.copy()
                painted.name = 'Painted heraldry'
                r['profile'] = 'paint'
                finish_material(painted, r)
                slot.material = painted
    for obj in scene.objects:
        if obj.type != 'MESH' or not obj.name.startswith('Crescent axe blade'): continue
        for slot in obj.material_slots:
            if not slot.material: continue
            r = recipe_for(slot.material)
            if r and r['profile'] != 'cut_steel':
                blade = slot.material.copy()
                blade.name = 'Readable cut steel'
                r['profile'] = 'cut_steel'
                finish_material(blade, r)
                slot.material = blade
    count = 0
    for mat in bpy.data.materials:
        if mat.users: count += int(finish_material(mat))
    scene['crownroad_finish'] = REVISION
    return count


def light_finish(scene, category, night=False, route=''):
    """Rebalance existing studio rigs, preserving all geometry and practical lights."""
    world = scene.world.node_tree.nodes.get('Background')
    scene.view_settings.view_transform = 'AgX'
    scene.view_settings.look = 'AgX - Medium High Contrast'
    scene.view_settings.exposure = .2
    areas = [o for o in scene.objects if o.type == 'LIGHT' and o.data.type == 'AREA']
    if category in ('battlefields', 'maps', 'menus'):
        world.inputs[0].default_value = (.18, .25, .32, 1) if night else (.3, .37, .43, 1)
        world.inputs[1].default_value = .28 if night else .38
        for obj in areas:
            if 'key' in obj.name.lower():
                obj.data.energy = 10500 if night else 20000
                obj.data.color = (.63, .77, 1) if night else (1, .88, .7)
                obj.data.size = 12
            elif 'fill' in obj.name.lower():
                obj.data.energy = 2400
                obj.data.color = (.6, .78, 1)
                obj.data.size = 22
            else:
                obj.data.energy = 12000
                obj.data.color = (1, .58, .3) if route in ('foundry','citadel') else (.75, .88, 1)
                obj.data.size = 14
        # Keep distance, but remove the milky veil over midground silhouettes.
        for mat in bpy.data.materials:
            if mat.name.startswith('Distance haze'):
                for node in mat.node_tree.nodes:
                    if node.type == 'PRINCIPLED_VOLUME':
                        node.inputs['Density'].default_value = .014 if route in ('mire','quarantine') else .008
        return
    world.inputs[0].default_value = (.2, .28, .36, 1)
    world.inputs[1].default_value = .32
    scale = 2.05 if category in ('caravan', 'caravans') else 1.7 if category == 'gatehouse' else 1
    target = Vector((0, 0, 2.3 if scale > 1 else .95))
    # Matching key direction across subjects; cooler rim separates from warm earth.
    presets = [((-3.6,-5,6.8), 680, (1,.9,.76), 3.0),
               ((4,-3,3.5), 180, (.64,.79,1), 4.5),
               ((1.5,3.8,5.2), 800, (.72,.85,1), 2.8),
               ((-1,-7,2), 65, (1,.88,.73), 4)]
    for i, obj in enumerate(areas):
        loc, power, color, size = presets[min(i, 3)]
        # Named rigs are stable even when Blender changes datablock iteration order.
        name = obj.name.lower()
        index = 1 if 'fill' in name else 2 if 'rim' in name or 'horizon' in name else 3 if 'front' in name else 0
        loc, power, color, size = presets[index]
        obj.location = Vector(loc) * scale
        obj.rotation_euler = (target-obj.location).to_track_quat('-Z','Y').to_euler()
        obj.data.energy, obj.data.color, obj.data.size = power * scale * scale, color, size * scale
