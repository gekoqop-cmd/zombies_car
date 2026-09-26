"""Renders presentation views of Workshop.blend into the scratch dir.
Run:  blender -b D:/zombi-caaar/models/Workshop.blend --python render_views.py -- <outdir> [views]
"""
import bpy, math, sys, os
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
OUTD = args[0]
ONLY = set(args[1].split(",")) if len(args) > 1 else None
os.makedirs(OUTD, exist_ok=True)
scene = bpy.context.scene
scene.render.engine = 'BLENDER_EEVEE'
scene.render.film_transparent = True
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
try:
    scene.eevee.taa_render_samples = 64
    scene.eevee.use_shadows = True
except Exception:
    pass
try:
    scene.eevee.use_raytracing = True
    scene.eevee.ray_tracing_options.resolution_scale = '2'
    scene.eevee.fast_gi_method = 'GLOBAL_ILLUMINATION'
except Exception as e:
    print("rt", e)
scene.view_settings.view_transform = 'AgX'
try:
    scene.view_settings.look = 'AgX - Punchy'
except Exception:
    pass
scene.view_settings.exposure = 0.3

root = bpy.data.objects["Workshop"]
meshes = [o for o in bpy.data.objects if o.type == 'MESH']
TARGET = Vector((0.2, -0.3, 1.2))


def cam(name, loc, target=None, ortho=None, lens=50, rot=None):
    cd = bpy.data.cameras.new(name)
    ob = bpy.data.objects.new(name, cd); scene.collection.objects.link(ob)
    ob.location = loc
    if ortho:
        cd.type = 'ORTHO'; cd.ortho_scale = ortho
    else:
        cd.lens = lens
    if rot:
        ob.rotation_euler = rot
    else:
        d = (Vector(target) - Vector(loc))
        ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    cd.clip_end = 200
    return ob


def render(name, camobj, res):
    if ONLY and name not in ONLY:
        return
    scene.camera = camobj
    scene.render.resolution_x, scene.render.resolution_y = res
    scene.render.filepath = os.path.join(OUTD, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", name)


hero = cam("CamHero", (-7.8, -15.5, 8.4), TARGET, lens=60)
render("hero", hero, (1800, 1250))

# game-like isometric view (like concept.png)
iso = cam("CamIso", (7.5, -15.0, 13.0), (0.0, -0.8, 0.8), lens=70)
render("iso", iso, (1200, 800))

ORT = 9.2
for nm, loc, rot in [("front", (0, -30, 1.7), (math.pi / 2, 0, 0)),
                     ("back", (0, 30, 1.7), (math.pi / 2, 0, math.pi)),
                     ("left", (-30, 0, 1.7), (math.pi / 2, 0, -math.pi / 2)),
                     ("right", (30, 0, 1.7), (math.pi / 2, 0, math.pi / 2))]:
    c = cam("Cam_" + nm, loc, ortho=ORT, rot=rot)
    render(nm, c, (1000, 620))
top = cam("CamTop", (0, -0.25, 30), ortho=8.6, rot=(0, 0, 0))
render("top", top, (900, 760))

# clay
clay = bpy.data.materials.new("Clay"); clay.use_nodes = True
bs = next(n for n in clay.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
bs.inputs["Base Color"].default_value = (0.62, 0.62, 0.62, 1); bs.inputs["Roughness"].default_value = 0.8
if not ONLY or "clay" in ONLY:
    bpy.context.view_layer.material_override = clay
    for l in bpy.data.lights:
        if l.type != 'SUN':
            l.energy *= 0.25
    render("clay", hero, (1100, 760))
    bpy.context.view_layer.material_override = None
    for l in bpy.data.lights:
        if l.type != 'SUN':
            l.energy *= 4

# wireframe: emissive wire copies
if not ONLY or "wire" in ONLY:
    wm = bpy.data.materials.new("Wire"); wm.use_nodes = True
    nt = wm.node_tree
    for n in list(nt.nodes):
        if n.type != 'OUTPUT_MATERIAL':
            nt.nodes.remove(n)
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = (0.85, 0.88, 0.95, 1)
    em.inputs[1].default_value = 1.5
    nt.links.new(em.outputs[0], next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL').inputs[0])
    for o in meshes:
        o.hide_render = True
        c = o.copy(); c.data = o.data.copy(); scene.collection.objects.link(c)
        c.hide_render = False
        c.parent = None; c.matrix_world = o.matrix_world.copy()
        c.data.materials.clear(); c.data.materials.append(wm)
        md = c.modifiers.new("wf", 'WIREFRAME'); md.thickness = 0.012; md.use_replace = True
    render("wire", hero, (1100, 760))


# props lineup
if not ONLY or "props" in ONLY:
    for o in bpy.data.objects:
        if o.type == 'MESH':
            o.hide_render = True
    lineup = [("Prop_Cabinet", 0.9), ("Prop_Workbench", 2.1), ("Prop_TireStack_Out", 0.8),
              ("Prop_Barrel", 0.7), ("Prop_Boxes_Out", 1.0), ("Prop_Crate_Back", 0.8), ("Prop_Fence_L", 1.7)]
    x = 0.0
    for nm, w in lineup:
        src = bpy.data.objects[nm]
        c = src.copy(); scene.collection.objects.link(c)
        c.parent = None; c.hide_render = False
        c.rotation_euler = (0, 0, 0.35 if nm != "Prop_Fence_L" else 0.25)
        x += w / 2
        c.location = (x, 100, 0)
        x += w / 2 + 0.35
    mid = x / 2 - 0.2
    pc = cam("CamProps", (mid, 100 - 13, 4.2), (mid, 100, 0.55), lens=48)
    for l in bpy.data.lights:
        if l.type != 'SUN':
            l.energy = 0
    render("props", pc, (1800, 520))
