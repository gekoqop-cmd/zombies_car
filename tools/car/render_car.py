"""Render presentation views of Car_Player.blend.
Run: blender -b D:/zombi-caaar/models/Car/Car_Player.blend --python render_car.py -- <outdir> [views]
"""
import bpy, math, sys, os
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
OUTD = args[0]
ONLY = set(args[1].split(",")) if len(args) > 1 else None
os.makedirs(OUTD, exist_ok=True)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE'
sc.render.film_transparent = True
sc.render.image_settings.file_format = 'PNG'
sc.render.image_settings.color_mode = 'RGBA'
try:
    sc.eevee.taa_render_samples = 64
    sc.eevee.use_raytracing = True
    sc.eevee.fast_gi_method = 'GLOBAL_ILLUMINATION'
except Exception:
    pass
sc.view_settings.view_transform = 'AgX'
try:
    sc.view_settings.look = 'AgX - Punchy'
except Exception:
    pass
sc.view_settings.exposure = 0.6

O = bpy.data.objects
MESHES = [o for o in O if o.type == 'MESH']
DMG = {"hood": ("DMG1_Hood_Dented", ["Hood"]), "bumper": ("DMG2_Bumper_Bent", ["Bumper_Front"]),
       "cargo": ("DMG3_Cargo_Fallen", ["Cargo_Jerrycan_Green", "Cargo_Jerrycan_Red", "Cargo_Crate", "Cargo_SpareTire"])}
MAT = {k: bpy.data.materials["M_Car_" + k] for k in ("Intact", "Damaged", "Wrecked")}

# ground shadow catcher-ish: dark disc
bpy.ops.mesh.primitive_circle_add(vertices=48, radius=3.2, fill_type='NGON', location=(0, 0, 0))
ground = bpy.context.active_object; ground.name = "_Ground"
gm = bpy.data.materials.new("_G"); gm.use_nodes = True
bs = next(n for n in gm.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
bs.inputs["Base Color"].default_value = (0.05, 0.055, 0.065, 1); bs.inputs["Roughness"].default_value = 0.9
ground.data.materials.append(gm)
try:
    ground.is_shadow_catcher = True
except Exception:
    pass


def state(stage):
    """stage 0 intact, 1 light, 2 heavy, 3 almost dead."""
    on = {0: [], 1: ["hood"], 2: ["hood", "bumper"], 3: ["hood", "bumper", "cargo"]}[stage]
    mat = MAT[{0: "Intact", 1: "Intact", 2: "Damaged", 3: "Wrecked"}[stage]]
    for k, (dmg, normal) in DMG.items():
        O[dmg].hide_render = k not in on
        for n in normal:
            O[n].hide_render = k in on
    for o in MESHES:
        if o.data.materials and o.data.materials[0].name.startswith("M_Car_") and "Emissive" not in o.data.materials[0].name:
            o.data.materials[0] = mat


def cam(name, loc, target=(0, 0, 1.0), ortho=None, lens=50, rot=None):
    cd = bpy.data.cameras.new(name); ob = bpy.data.objects.new(name, cd); sc.collection.objects.link(ob)
    ob.location = loc
    if ortho:
        cd.type = 'ORTHO'; cd.ortho_scale = ortho
    else:
        cd.lens = lens
    ob.rotation_euler = rot if rot else (Vector(target) - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    cd.clip_end = 300
    return ob


def render(name, c, res):
    if ONLY and name not in ONLY:
        return
    sc.camera = c
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.filepath = os.path.join(OUTD, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", name)


T = (0, 0.05, 1.05)
hero = cam("Hero", (7.2, -8.6, 4.6), T, lens=55)
state(0)
render("hero", hero, (1600, 1100))

el, az, dist = math.radians(50), math.radians(35), 16
game = cam("Game", (dist * math.cos(el) * math.sin(az), -dist * math.cos(el) * math.cos(az), dist * math.sin(el) + 0.8),
           (0, 0, 0.8), lens=50)
render("game", game, (1000, 800))

ORT = 5.6
for nm, loc, rot in [("front", (0, -40, 1.35), (math.pi / 2, 0, 0)),
                     ("back", (0, 40, 1.35), (math.pi / 2, 0, math.pi)),
                     ("side", (40, 0, 1.35), (math.pi / 2, 0, math.pi / 2)),
                     ("side_l", (-40, 0, 1.35), (math.pi / 2, 0, -math.pi / 2))]:
    render(nm, cam("C_" + nm, loc, ortho=ORT if "side" not in nm else 5.4, rot=rot),
           (900, 700) if "side" not in nm else (1200, 700))
ground.hide_render = True
render("top", cam("C_top", (0, 0, 40), ortho=5.4, rot=(0, 0, math.pi / 2)), (1100, 620))
ground.hide_render = False

for st in (1, 2, 3):
    if not ONLY or f"dmg{st}" in ONLY:
        state(st)
        render(f"dmg{st}", hero, (1100, 800))
state(0)

if not ONLY or "clay" in ONLY:
    clay = bpy.data.materials.new("Clay"); clay.use_nodes = True
    b = next(n for n in clay.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    b.inputs["Base Color"].default_value = (0.62, 0.62, 0.62, 1); b.inputs["Roughness"].default_value = 0.75
    bpy.context.view_layer.material_override = clay
    ground.hide_render = True
    render("clay", hero, (1100, 800))
    ground.hide_render = False
    bpy.context.view_layer.material_override = None

if not ONLY or "wire" in ONLY:
    wm = bpy.data.materials.new("Wire"); wm.use_nodes = True
    nt = wm.node_tree
    for n in list(nt.nodes):
        if n.type != 'OUTPUT_MATERIAL':
            nt.nodes.remove(n)
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = (0.85, 0.88, 0.95, 1)
    nt.links.new(em.outputs[0], next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL').inputs[0])
    ground.hide_render = True
    for o in MESHES:
        if o.hide_render:
            continue
        o.hide_render = True
        c = o.copy(); c.data = o.data.copy(); sc.collection.objects.link(c)
        c.hide_render = False; c.parent = None; c.matrix_world = o.matrix_world.copy()
        c.data.materials.clear(); c.data.materials.append(wm)
        md = c.modifiers.new("wf", 'WIREFRAME'); md.thickness = 0.006; md.use_replace = True
    render("wire", hero, (1100, 800))
