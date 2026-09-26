"""Nature v2 — alpha-card bushes/grass from priroda2.png, recoloured rocks, ground decals.
blender -b --factory-startup --python build_nature2.py      (run tex_nature2.py first)
Cards are double-sided geometry with up-facing normals (lit like the ground, same from every side)."""
import bpy, bmesh, math, os, sys, json, random
from mathutils import Vector, Matrix, Euler

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import bcommon as C

L = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "layout_nature2.json")))
AT = L["atlas"]
SP = L["sprites"]
PI = math.pi
DECAL_DIR = os.path.join(C.ROOT, "Nature", "Decals")

C.open_all()
# ---- remove the v1 nature data (collection is rebuilt by Sheet(); old material + image purged here)
for mname in ("M_Nature_Atlas", "M_Nature_Foliage", "M_Rocks") + tuple(f"M_Decal_Stone{i}" for i in range(1, 4)):
    if bpy.data.materials.get(mname):
        bpy.data.materials.remove(bpy.data.materials[mname])
for im in list(bpy.data.images):
    if os.path.basename(im.filepath).lower() in ("nature_atlas.png", "rocks_atlas.png") or im.name.lower().startswith("decal_stone"):
        bpy.data.images.remove(im)

S = C.Sheet("Nature", C.Atlas({}, AT), L["file"], None, row_y=48.0, alpha=True)
S.colors_type = 'SRGB'       # vertex colours written exactly as painted (0..1 ramp)


def cutout(m, clip=0.5):
    """Alpha Cutout: alpha > clip -> 1 else 0 (Unity: Alpha Clipping 0.5, Render Face Both)."""
    nt = m.node_tree
    bs = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    tex = next(n for n in nt.nodes if n.type == 'TEX_IMAGE')
    for l in list(nt.links):
        if l.to_socket == bs.inputs["Alpha"]:
            nt.links.remove(l)
    g = nt.nodes.new("ShaderNodeMath"); g.operation = 'GREATER_THAN'; g.inputs[1].default_value = clip
    nt.links.new(tex.outputs["Alpha"], g.inputs[0]); nt.links.new(g.outputs[0], bs.inputs["Alpha"])
    for attr, val in (("surface_render_method", 'DITHERED'), ("blend_method", 'CLIP'), ("alpha_threshold", clip)):
        try:
            setattr(m, attr, val)
        except Exception:
            pass
    bs.inputs["Roughness"].default_value = 0.9
    m.use_backface_culling = False
    return m


M_FOL = S.mats[0]; M_FOL.name = "M_Nature_Foliage"; cutout(M_FOL)
S.mats = (M_FOL, None)
M_ROCK = C.material("M_Rocks", L["rocks"]["file"], rough=0.85)
A_ROCK = C.Atlas(L["rocks"]["reg"], L["rocks"]["atlas"])


def uv_rect(key, fy0=0.0, fy1=1.0):
    """Atlas UV rect of a sprite's visible box; fy0/fy1 crop it vertically (0 = bottom, 1 = top)."""
    x0, y0, x1, y1 = SP[key]["visible"]
    u0, u1 = x0 / AT, x1 / AT
    vb, vt = 1 - y1 / AT, 1 - y0 / AT
    return u0, vb + (vt - vb) * fy0, u1, vb + (vt - vb) * fy1


def quad2(b, pts, uvs, color=None):
    """Double-sided quad (front + flipped back), UVs kept as given."""
    t = bmesh.new(); uvl = t.loops.layers.uv.verify()
    for order in ((0, 1, 2, 3), (3, 2, 1, 0)):
        f = t.faces.new([t.verts.new(pts[i]) for i in order]); f.smooth = True
        for l, i in zip(f.loops, order):
            l[uvl].uv = uvs[i]
    b.add(t, ('keep',), color=color)


def vcard(b, ang, w, h, uv, color=None, z0=-0.02):
    """Vertical card through the origin rotated by ang around Z; bottom edge at z0."""
    d = Vector((math.cos(ang), math.sin(ang), 0))
    u0, v0, u1, v1 = uv
    pts = [-d * w / 2 + Vector((0, 0, z0)), d * w / 2 + Vector((0, 0, z0)), d * w / 2 + Vector((0, 0, z0 + h)),
           -d * w / 2 + Vector((0, 0, z0 + h))]
    quad2(b, pts, [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], color)


def up_normals(ob, spread=0.35):
    me = ob.data
    for p in me.polygons:
        p.use_smooth = True
    me.normals_split_custom_set_from_vertices(
        [Vector((v.co.x * spread, v.co.y * spread, 1.0)).normalized() for v in me.vertices])


def grad(h, lo=0.0):          # wind mask: 0 at the root (fixed), 1 at the tips
    return lambda co: (lambda g: (g, g, g, 1.0))(lo + (1 - lo) * min(max(co.z / h, 0.0), 1.0))


def part(model, name, b, mats, normals=True):
    ob = b.finish(name, mats, model["root"], S.col)
    model["parts"].append(ob)
    if normals:
        up_normals(ob)
    return ob


# ================================================================== bushes: 2 crossed cards + mirrored cap
BW, BH = 1.10, 0.95
CAP_CUT = 0.55              # top 45 % of the sprite = leaves only
for i in range(1, 6):
    key = f"Bush{i}"
    m = S.model(key, "Nature", 1.4)
    b = C.B(S.A)
    full = uv_rect(key)
    vcard(b, PI / 4, BW, BH, full)
    vcard(b, -PI / 4, BW, BH, full)
    # cap: two halves, each maps sprite top -> cut row, mirrored so the outline is round front and back
    u0, vc, u1, vt = uv_rect(key, CAP_CUT, 1.0)
    x0, y0, x1, y1 = SP[key]["visible"]
    mpp = 1.0 / (x1 - x0)                                            # cap 1.0 m wide
    half = (y1 - y0) * (1 - CAP_CUT) * mpp
    zc, tilt = BH * 0.66, math.radians(12)
    M = Matrix.Translation((0, 0, zc)) @ Euler((tilt, 0, 0)).to_matrix().to_4x4()   # tilted toward the camera (-Y)
    P = lambda x, y: M @ Vector((x, y, 0))
    quad2(b, [P(-0.5, 0), P(0.5, 0), P(0.5, half), P(-0.5, half)], [(u0, vc), (u1, vc), (u1, vt), (u0, vt)])
    quad2(b, [P(-0.5, -half), P(0.5, -half), P(0.5, 0), P(-0.5, 0)], [(u0, vt), (u1, vt), (u1, vc), (u0, vc)])
    part(m, key + "_Body", b, (M_FOL, None))

# ================================================================== grass: 3 cards in a 60-degree fan
for i in range(1, 5):
    key = f"Grass{i}"
    m = S.model(key, "Nature", 1.0)
    x0, y0, x1, y1 = SP[key]["visible"]
    GW = 0.72; GH = GW * (y1 - y0) / (x1 - x0)
    b = C.B(S.A)
    for k in range(3):
        vcard(b, k * PI / 3 + 0.2, GW, GH, uv_rect(key), color=grad(GH))
    part(m, key + "_Body", b, (M_FOL, None))

# ================================================================== rocks (same geometry as v1, beige-brown texture)
ROCK = ('dir', {'+z': 'rock_top', '*': 'rock', 'bevel': 'rock_hi', 'bevel_lo': 'rock_lo'})
SLAB = ('dir', {'+z': 'slab', '*': 'slab_side', 'bevel': 'rock_hi', 'bevel_lo': 'rock_lo'})


def rock(b, size, c, rz, seed, taper=0.72, jit=0.06):
    r = random.Random(seed)
    t = bmesh.new(); bmesh.ops.create_cube(t, size=1.0)
    for v in t.verts:
        top = v.co.z > 0
        k = taper if top else 1.0
        v.co = Vector((v.co.x * size[0] * k, v.co.y * size[1] * k, (v.co.z + 0.5) * size[2]))
        v.co.x += r.uniform(-jit, jit) * size[0]; v.co.y += r.uniform(-jit, jit) * size[1]
        if top:
            v.co.z += r.uniform(-jit, jit) * size[2]
    bmesh.ops.bevel(t, geom=list(t.edges), offset=min(size) * 0.16, segments=1, affect='EDGES', clamp_overlap=True)
    b.add(t, ROCK, Matrix.Translation(Vector(c)) @ Euler((0, 0, rz)).to_matrix().to_4x4())


def slab(b, c, rad, h, seed, sides=6, tilt=0.06):
    r = random.Random(seed)
    angs = sorted(r.uniform(0, 2 * PI) for _ in range(sides))
    angs = [a0 + (2 * PI * i / sides - a0) * 0.6 for i, a0 in enumerate(angs)]
    tx, ty = r.uniform(-tilt, tilt), r.uniform(-tilt, tilt)
    poly = [(math.cos(a) * rad * r.uniform(0.8, 1.1), math.sin(a) * rad * r.uniform(0.8, 1.1)) for a in angs]
    t = bmesh.new()
    bot = [t.verts.new((x, y, 0)) for x, y in poly]
    top = [t.verts.new((x, y, h + tx * x + ty * y)) for x, y in poly]
    t.faces.new(list(reversed(bot))); t.faces.new(top)
    for i in range(sides):
        j = (i + 1) % sides
        t.faces.new((bot[i], bot[j], top[j], top[i]))
    bmesh.ops.recalc_face_normals(t, faces=t.faces)
    topset = set(top)
    edges = [e for e in t.edges if all(v in topset for v in e.verts)]
    bmesh.ops.bevel(t, geom=edges, offset=0.035, segments=1, affect='EDGES', clamp_overlap=True)
    b.add(t, SLAB, Matrix.Translation(Vector(c)) @ Euler((0, 0, r.uniform(0, PI))).to_matrix().to_4x4())


def rock_grass(model, name, spots):
    """Small grass tufts next to the rock: 2 crossed cards of a grass sprite (foliage material)."""
    g = C.B(S.A)
    for (x, y, key, w, ang) in spots:
        x0, y0, x1, y1 = SP[key]["visible"]; h = w * (y1 - y0) / (x1 - x0)
        for k in range(2):
            d = Vector((math.cos(ang + k * PI / 2), math.sin(ang + k * PI / 2), 0))
            base = Vector((x, y, -0.01))
            u0, v0, u1, v1 = uv_rect(key)
            quad2(g, [base - d * w / 2, base + d * w / 2, base + d * w / 2 + Vector((0, 0, h)), base - d * w / 2 + Vector((0, 0, h))],
                  [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], color=grad(h))
    part(model, name + "_Grass", g, (M_FOL, None))


def rock_body(model, name, build):
    b = C.B(A_ROCK); build(b)
    part(model, name + "_Body", b, (M_ROCK, None), normals=False)


m = S.model("Rock1", "Nature", 1.5)
rock_body(m, "Rock1", lambda b: (rock(b, (0.85, 0.72, 0.82), (0.0, 0.05, 0), 0.25, 1),
                                 rock(b, (0.38, 0.34, 0.36), (0.55, -0.12, 0), -0.4, 2),
                                 rock(b, (0.30, 0.28, 0.24), (-0.50, -0.30, 0), 0.6, 3),
                                 rock(b, (0.22, 0.20, 0.16), (0.28, -0.48, 0), 0.2, 4)))
rock_grass(m, "Rock1", [(-0.55, 0.12, "Grass1", 0.38, 0.3), (0.50, 0.28, "Grass4", 0.32, 1.1)])

m = S.model("Rock2", "Nature", 1.6)
rock_body(m, "Rock2", lambda b: (rock(b, (0.62, 0.55, 0.88), (-0.18, 0.10, 0), 0.4, 11, taper=0.68),
                                 rock(b, (0.55, 0.48, 0.50), (0.32, -0.12, 0), -0.25, 12),
                                 rock(b, (0.30, 0.26, 0.24), (-0.58, -0.25, 0), 0.9, 13),
                                 rock(b, (0.22, 0.20, 0.17), (0.02, -0.46, 0), 0.3, 14),
                                 rock(b, (0.18, 0.17, 0.14), (0.66, -0.40, 0), 0.7, 15)))
rock_grass(m, "Rock2", [(0.64, 0.22, "Grass4", 0.36, 0.5), (-0.42, 0.36, "Grass1", 0.32, 1.3)])


def rock3(b):
    slab(b, (0.0, 0.0, 0.0), 0.34, 0.20, 21)
    for k in range(6):
        a = k * 2 * PI / 6 + 0.3
        slab(b, (math.cos(a) * 0.52, math.sin(a) * 0.46, 0.0), 0.28, 0.12 + 0.05 * (k % 3), 30 + k, sides=5 + (k % 2))
    rock(b, (0.22, 0.20, 0.16), (0.72, -0.42, 0), 0.4, 22)


m = S.model("Rock3", "Nature", 1.8)
rock_body(m, "Rock3", rock3)
rock_grass(m, "Rock3", [(-0.78, -0.22, "Grass4", 0.34, 0.2), (0.70, 0.32, "Grass1", 0.30, 0.9)])

# ================================================================== ground decals (flat quads, top view)
DECAL_W = 1.2                                            # world width of the stone tile
for i in range(1, 4):
    key = f"Stone{i}"
    m = S.model(f"Decal_Stone{i}", "Nature/Decals", 1.6)
    mat = cutout(C.material(f"M_Decal_Stone{i}", os.path.join(DECAL_DIR, f"Decal_Stone{i}.png"), rough=0.9))
    cx0, cy0, cx1, cy1 = SP[key]["decal_content"]
    side = DECAL_W / (cx1 - cx0)                          # full 512 texture -> quad; tile content is DECAL_W wide
    b = C.B(S.A)
    t = bmesh.new(); uvl = t.loops.layers.uv.verify()
    z = 0.005
    f = t.faces.new([t.verts.new((-side / 2, -side / 2, z)), t.verts.new((side / 2, -side / 2, z)),
                     t.verts.new((side / 2, side / 2, z)), t.verts.new((-side / 2, side / 2, z))])
    for l, u in zip(f.loops, [(0, 0), (1, 0), (1, 1), (0, 1)]):
        l[uvl].uv = u
    b.add(t, ('keep',))
    part(m, f"Decal_Stone{i}_Body", b, (mat, None), normals=False)

stats = S.export(os.path.join(C.SRC, "stats_Nature.json"))
# purge orphaned data from the v1 sheet
for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.images):
    for dblock in list(coll):
        if dblock.users == 0:
            coll.remove(dblock)
C.save_all()
print("DONE", {k: v["tris"] for k, v in stats.items()})
print("MATS", sorted({mm for v in stats.values() for mm in v["materials"]}))
