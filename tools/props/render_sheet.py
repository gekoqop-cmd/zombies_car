"""Render views for one prop sheet from Props_All.blend.
blender -b D:/zombi-caaar/models/source/Props_All.blend --python render_sheet.py -- <SheetName> <outdir>"""
import bpy, math, sys, os
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
SHEET, OUTD = args[0], args[1]
os.makedirs(OUTD, exist_ok=True)
sc = bpy.context.scene
sc.render.engine = 'BLENDER_EEVEE'
sc.render.film_transparent = True
sc.render.image_settings.file_format = 'PNG'
sc.render.image_settings.color_mode = 'RGBA'
try:
    sc.eevee.taa_render_samples = 48
    sc.eevee.use_raytracing = True
    sc.eevee.fast_gi_method = 'GLOBAL_ILLUMINATION'
except Exception:
    pass
# Standard view transform ~ what Unity shows without tonemapping (keeps hand-painted colors saturated)
sc.view_settings.view_transform = 'Standard'
sc.view_settings.look = 'None'
sc.view_settings.exposure = -0.1

for lc in sc.view_layers[0].layer_collection.children:
    lc.exclude = lc.name != "Sheet_" + SHEET
col = bpy.data.collections["Sheet_" + SHEET]
roots = [o for o in col.objects if o.type == 'EMPTY' and o.parent is None]
roots.sort(key=lambda o: o.location.x)
meshes = [o for o in col.all_objects if o.type == 'MESH']


def root_bounds(r):
    objs = [o for o in r.children_recursive if o.type == 'MESH']
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    return (min(p.x for p in pts) - r.location.x, max(p.x for p in pts) - r.location.x,
            min(p.y for p in pts) - r.location.y, max(p.y for p in pts) - r.location.y)


# arrange the sheet's models in a compact grid for the overview renders
n = len(roots)
nrows = 1 if n <= 4 else (2 if n <= 10 else 3)
per = math.ceil(n / nrows)
bb = {r.name: root_bounds(r) for r in roots}
row_list = [roots[ri * per:(ri + 1) * per] for ri in range(nrows)]
if SHEET == "Nature":        # one row per kind, like the reference sheet: bushes / grass / rocks / decals
    import re
    kinds = []
    for r in roots:
        k = re.sub(r"\d+$", "", r.name)
        if k not in kinds:
            kinds.append(k)
    row_list = [sorted([r for r in roots if re.sub(r"\d+$", "", r.name) == k], key=lambda o: o.name) for k in kinds]
    row_list.reverse()       # decals nearest the camera, bushes at the back = top of the image, as on the sheet
y = 0.0
for row in row_list:
    if not row:
        break
    widths = [bb[r.name][1] - bb[r.name][0] for r in row]
    x = -(sum(widths) + 0.6 * (len(row) - 1)) / 2
    depth = max(bb[r.name][3] - bb[r.name][2] for r in row)
    for r, w in zip(row, widths):
        r.location = (x - bb[r.name][0], y - bb[r.name][2], 0)
        x += w + 0.6
    y += depth + (0.8 if SHEET == "Nature" else 2.4)
bpy.context.view_layer.update()

cd = bpy.data.cameras.new("Cam"); cam = bpy.data.objects.new("Cam", cd); sc.collection.objects.link(cam)
sc.camera = cam; cd.lens = 50; cd.clip_end = 500


def bounds(objs):
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi, pts


def frame(objs, direction, res, lens=50, margin=1.08):
    lo, hi, pts = bounds(objs)
    c = (lo + hi) / 2
    d = Vector(direction).normalized()
    cd.lens = lens
    sc.render.resolution_x, sc.render.resolution_y = res
    cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    # fit projected extents
    rot = cam.rotation_euler.to_matrix()
    right, up, fwd = rot.col[0], rot.col[1], -rot.col[2]
    fov = 2 * math.atan(18 / lens)  # sensor 36mm (width)
    aspect = res[0] / res[1]
    tan_x = math.tan(fov / 2); tan_y = tan_x / aspect
    dist = 0
    for p in pts:
        v = p - c
        x, y, z = v.dot(right), v.dot(up), v.dot(fwd)
        dist = max(dist, abs(x) / tan_x - z, abs(y) / tan_y - z)
    cam.location = c - fwd * dist * margin


def show_only(objs):
    keep = set(objs)
    for o in meshes:
        o.hide_render = o not in keep


def render(name):
    sc.render.filepath = os.path.join(OUTD, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("RENDERED", name)


HERO = (-0.28, -1.0, 0.40)
el, az = math.radians(50), math.radians(0 if SHEET == "Nature" else 35)   # cross cards: camera along the world axis
GAME = (math.cos(el) * math.sin(az), -math.cos(el) * math.cos(az), math.sin(el))

show_only(meshes)
frame(meshes, HERO, (2400, 1100), lens=60); render("hero")
frame(meshes, GAME, (1600, 1000), lens=50); render("game")

clay = bpy.data.materials.new("_Clay"); clay.use_nodes = True
b = next(n for n in clay.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
b.inputs["Base Color"].default_value = (0.62, 0.62, 0.62, 1); b.inputs["Roughness"].default_value = 0.8
_atl = bpy.data.materials.get("M_" + SHEET + "_Atlas")
_img = next((n.image for n in _atl.node_tree.nodes if n.type == 'TEX_IMAGE'), None) if _atl else None
if _img is not None and any(l.to_socket.name == "Alpha" for l in _atl.node_tree.links):   # keep leaf cut-outs in clay
    tn = clay.node_tree.nodes.new("ShaderNodeTexImage"); tn.image = _img
    clay.node_tree.links.new(tn.outputs["Alpha"], b.inputs["Alpha"])
    try:
        clay.surface_render_method = 'DITHERED'
    except Exception:
        pass
bpy.context.view_layer.material_override = clay
frame(meshes, HERO, (1600, 800), lens=60); render("clay")
bpy.context.view_layer.material_override = None

# per-model thumbnails
for r in roots:
    objs = [o for o in r.children_recursive if o.type == 'MESH']
    show_only(objs)
    view = (0.0, -0.25, 1.0) if r.name.startswith("Decal") else         ((0.0, -1.0, 0.55) if SHEET == "Nature" and r.name.startswith(("Bush", "Grass")) else (-0.6, -1.0, 0.45))
    frame(objs, view, (700, 700), lens=50, margin=1.05)
    render("thumb_" + r.name)
show_only(meshes)

# wireframe
wm = bpy.data.materials.new("_Wire"); wm.use_nodes = True
nt = wm.node_tree
for n in list(nt.nodes):
    if n.type != 'OUTPUT_MATERIAL':
        nt.nodes.remove(n)
em = nt.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = (0.85, 0.88, 0.95, 1)
nt.links.new(em.outputs[0], next(n for n in nt.nodes if n.type == 'OUTPUT_MATERIAL').inputs[0])
wires = []
for o in meshes:
    o.hide_render = True
    c = o.copy(); c.data = o.data.copy(); sc.collection.objects.link(c)
    c.hide_render = False; c.parent = None; c.matrix_world = o.matrix_world.copy()
    c.data.materials.clear(); c.data.materials.append(wm)
    md = c.modifiers.new("wf", 'WIREFRAME'); md.thickness = 0.008; md.use_replace = True
    wires.append(c)
frame(meshes, HERO, (1600, 800), lens=60); render("wire")
