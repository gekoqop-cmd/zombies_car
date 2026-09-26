"""Sheet 5 — nature (cartoon).  blender -b --factory-startup --python build_nature.py
Bushes: opaque core + double-sided alpha leaf cards with spherical normals.
Grass: curved flat double-sided blades, vertex colour gradient (dark base -> light tips) for a Unity wind shader.
Rocks: faceted chamfered blocks with a flat base.  Origin at the base center."""
import bpy, bmesh, math, os, sys, random
from mathutils import Vector, Matrix, Euler

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import bcommon as C
from layout_nature import REG, ATLAS, ATLAS_FILE

C.open_all()
S = C.Sheet("Nature", C.Atlas(REG, ATLAS), ATLAS_FILE, None, row_y=48.0, vcol=True, alpha=True)
PI = math.pi


def grad(h):
    """Vertex colour: dark at the base, white at the tips (R = wind weight)."""
    def f(co):
        g = 0.62 + 0.38 * min(max(co.z / h, 0.0), 1.0)
        return (g, g, g, 1.0)
    return f


WHITE = (1.0, 1.0, 1.0, 1.0)


def card(b, c, n, w, h, key, spin=0.0, color=WHITE):
    """Double-sided textured quad centred at c, facing n."""
    c, n = Vector(c), Vector(n).normalized()
    ref = Vector((0, 0, 1)) if abs(n.z) < 0.95 else Vector((0, 1, 0))
    tx = ref.cross(n).normalized(); ty = n.cross(tx)
    cs, sn = math.cos(spin), math.sin(spin)
    tx, ty = tx * cs + ty * sn, ty * cs - tx * sn
    pts = [c - tx * w / 2 - ty * h / 2, c + tx * w / 2 - ty * h / 2, c + tx * w / 2 + ty * h / 2, c - tx * w / 2 + ty * h / 2]
    uvs = [(0, 0), (1, 0), (1, 1), (0, 1)]
    t = bmesh.new(); uvl = t.loops.layers.uv.verify()
    for order in ((0, 1, 2, 3), (3, 2, 1, 0)):
        f = t.faces.new([t.verts.new(pts[i]) for i in order]); f.smooth = True
        for l, i in zip(f.loops, order):
            l[uvl].uv = uvs[i]
    b.add(t, ('remap', key), color=color)


def finish_part(model, name, b, normal_center=None):
    ob = S.part(model, name, b)
    me = ob.data
    if "Col" not in me.color_attributes:
        a = me.color_attributes.new("Col", 'FLOAT_COLOR', 'CORNER')
        for d in a.data:
            d.color = WHITE
    if normal_center is not None:                    # spherical normals: soft volumetric foliage lighting
        c = Vector(normal_center)
        for p in me.polygons:
            p.use_smooth = True
        try:
            me.normals_split_custom_set_from_vertices([(v.co - c).normalized() for v in me.vertices])
        except Exception as e:
            print("custom normals failed:", e)
    return ob


# ------------------------------------------------------------------ bushes
def bush(b, R, Hc, z0, n, keys, seed, core=True):
    r = random.Random(seed)
    Cn = Vector((0, 0, z0 + Hc / 2))
    if core:
        t = bmesh.new()
        bmesh.ops.create_icosphere(t, subdivisions=1, radius=1.0)
        for v in t.verts:
            v.co = Vector((v.co.x * R * 0.62, v.co.y * R * 0.62, v.co.z * Hc * 0.31)) + Cn
        b.add(t, ('fit', 'leaf_core'), color=WHITE)
    for k in range(3):                                   # crossed vertical layers through the middle
        a = k * PI / 3 + r.uniform(-0.2, 0.2)
        card(b, Cn, (math.cos(a), math.sin(a), 0), 2 * R * 0.95, Hc * 0.95, keys[1], spin=r.uniform(-0.2, 0.2))
    for i in range(n):                                   # outer shell of leaf cards (golden-angle spread)
        zf = 1 - (i + 0.5) / n * 1.4
        rr = math.sqrt(max(0.0, 1 - zf * zf)); th = i * 2.39996 + r.uniform(-0.2, 0.2)
        d = Vector((rr * math.cos(th), rr * math.sin(th), zf))
        pos = Cn + Vector((d.x * R * 0.80, d.y * R * 0.80, d.z * Hc * 0.42)) + Vector([r.uniform(-0.04, 0.04) for _ in range(3)])
        nrm = (d + Vector((0, 0, 0.35))).normalized()
        s = R * 0.95 * r.uniform(0.85, 1.15)
        key = keys[0] if zf > 0.4 else (keys[1] if zf > -0.15 else keys[2])
        card(b, pos, nrm, s, s, key, spin=r.uniform(0, 2 * PI))
    return Cn


def stems(b, pts_list, radius=0.022):
    for pts in pts_list:
        b.pipe(pts, radius, ('fit', 'stem'), segs=3, fillet=0.0, caps=False)


def surface_points(R, Hc, z0, n, seed, zmin=-0.1):
    r = random.Random(seed)
    out = []
    Cn = Vector((0, 0, z0 + Hc / 2))
    for i in range(n):
        zf = r.uniform(zmin, 1.0); rr = math.sqrt(1 - zf * zf); th = r.uniform(0, 2 * PI)
        d = Vector((rr * math.cos(th), rr * math.sin(th), zf))
        out.append((Cn + Vector((d.x * R * 1.0, d.y * R * 1.0, d.z * Hc * 0.55)), d))
    return out


def base_grass(model, name, spots, seed):
    g = S.new()
    for i, (x, y, n, h, key) in enumerate(spots):
        g.grass((x, y, 0), n, h * 1.3, seed + i, ('remap', key), spread=0.05, width=0.07, lean=(0.35, 0.85), color=grad(h), splay=0.5)
    finish_part(model, name + "_Grass", g, normal_center=(0, 0, -3.0))


# Bush1: yellow-green clover dome
m = S.model("Bush1", "Nature", 1.3)
b = S.new()
Cn = bush(b, 0.55, 0.95, 0.10, 56, ('leaves_a', 'leaves_b', 'leaves_c'), 1)
stems(b, [[(0.0, 0.0, 0.0), (-0.06, 0.02, 0.30)], [(0.02, -0.02, 0.0), (0.12, -0.05, 0.28)]])
finish_part(m, "Bush1_Body", b, normal_center=Cn - Vector((0, 0, 0.25)))
base_grass(m, "Bush1", [(-0.25, -0.35, 7, 0.34, 'grass_c'), (0.30, -0.30, 6, 0.30, 'grass_a')], 10)

# Bush2: deep green with white flowers
m = S.model("Bush2", "Nature", 1.3)
b = S.new()
Cn = bush(b, 0.55, 0.92, 0.10, 52, ('leaves_b', 'leaves_b', 'leaves_c'), 2)
stems(b, [[(0.0, 0.0, 0.0), (0.05, 0.03, 0.30)], [(-0.02, 0.0, 0.0), (-0.12, -0.04, 0.26)]])
for i, (p, d) in enumerate(surface_points(0.55, 0.92, 0.10, 11, 21, zmin=-0.2)):
    card(b, p + d * 0.07, (d + Vector((0, 0, 0.9))).normalized(), 0.17, 0.17, 'flower_w', spin=i * 1.3)
finish_part(m, "Bush2_Body", b, normal_center=Cn - Vector((0, 0, 0.25)))
base_grass(m, "Bush2", [(-0.30, -0.32, 6, 0.30, 'grass_a'), (0.28, -0.34, 6, 0.28, 'grass_c')], 20)

# Bush3: yellow-green, brown branches poking out with orange bud clusters
m = S.model("Bush3", "Nature", 1.3)
b = S.new()
Cn = bush(b, 0.50, 0.85, 0.14, 48, ('leaves_a', 'leaves_b', 'leaves_c'), 3)
r = random.Random(31)
branch_tips = []
for k in range(6):
    a = k * 2 * PI / 6 + r.uniform(-0.3, 0.3)
    tip = Vector((math.cos(a) * r.uniform(0.18, 0.40), math.sin(a) * r.uniform(0.18, 0.40), r.uniform(1.02, 1.22)))
    mid = Vector((tip.x * 0.4, tip.y * 0.4, 0.45))
    stems(b, [[(r.uniform(-0.03, 0.03), r.uniform(-0.03, 0.03), 0.0), mid, tip]], radius=0.014)
    branch_tips.append(tip)
for k, tip in enumerate(branch_tips):
    card(b, tip + Vector((0, 0, 0.03)), (0.3, -1, 0.4), 0.15, 0.15, 'flower_o', spin=k)
    card(b, tip + Vector((0, 0, 0.03)), (1, 0.3, 0.4), 0.15, 0.15, 'flower_o', spin=k + 1)
finish_part(m, "Bush3_Body", b, normal_center=Cn - Vector((0, 0, 0.25)))
base_grass(m, "Bush3", [(-0.28, -0.30, 7, 0.34, 'grass_c'), (0.30, -0.28, 6, 0.30, 'grass_c')], 30)


# ------------------------------------------------------------------ grass tufts (<= 150 tris each)
def stalk_with_head(b, base_pt, h, lean, key, head_w, head_h, color):
    x, y = base_pt
    a = random.Random(int(h * 1000) + int(x * 97)).uniform(0, 2 * PI)
    top = Vector((x + math.cos(a) * lean, y + math.sin(a) * lean, h))
    side = Vector((-math.sin(a), math.cos(a), 0)) * 0.008
    t = bmesh.new(); uvl = t.loops.layers.uv.verify()               # thin stalk: one triangle, both sides
    v0, v1, v2 = Vector((x, y, 0)) - side, Vector((x, y, 0)) + side, top
    for tri, uvs in (((v0, v1, v2), ((0, 0), (1, 0), (0.5, 1))), ((v2, v1, v0), ((0.5, 1), (1, 0), (0, 0)))):
        f = t.faces.new([t.verts.new(p) for p in tri])
        for l, u in zip(f.loops, uvs):
            l[uvl].uv = u
    b.add(t, ('remap', 'grass_b'), color=color)
    for k in range(2):                                              # crossed head cards
        ang = a + k * PI / 2
        card(b, top + Vector((0, 0, head_h * 0.35)), (math.cos(ang), math.sin(ang), 0), head_w, head_h, key, color=color)


GRASS = [("Grass1", 24, 0.46, 0.070, 'grass_a', None),
         ("Grass2", 17, 0.42, 0.060, 'grass_c', ('seed', 3, 0.62, 0.05, 0.16)),
         ("Grass3", 15, 0.40, 0.065, 'grass_b', ('flower_o', 4, 0.52, 0.09, 0.09)),
         ("Grass4", 24, 0.36, 0.095, 'grass_c', None)]
for i, (name, n, h, w, key, extra) in enumerate(GRASS):
    m = S.model(name, "Nature", 0.8)
    b = S.new()
    lean = (0.55, 1.05) if name == "Grass4" else (0.4, 0.9)
    b.grass((0, 0, 0), n, h, 100 + i, ('remap', key), spread=0.045, width=w, lean=lean, color=grad(h), splay=0.5)
    if extra:
        hk, cnt, sh, hw, hh = extra
        r = random.Random(200 + i)
        for k in range(cnt):
            stalk_with_head(b, (r.uniform(-0.04, 0.04), r.uniform(-0.04, 0.04)), sh * r.uniform(0.85, 1.1),
                            r.uniform(0.05, 0.16), hk, hw, hh, grad(h))
    finish_part(m, name + "_Body", b, normal_center=(0, 0, -3.0))


# ------------------------------------------------------------------ rocks
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
    angs = [a0 + (2 * PI * i / sides - a0) * 0.6 for i, a0 in enumerate(angs)]   # keep it roughly regular
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


def rock_grass(model, name, spots, seed):
    base_grass(model, name, [(x, y, n, h, 'grass_c' if i % 2 else 'grass_a') for i, (x, y, n, h) in enumerate(spots)], seed)


m = S.model("Rock1", "Nature", 1.5)
b = S.new()
rock(b, (0.85, 0.72, 0.82), (0.0, 0.05, 0), 0.25, 1)
rock(b, (0.38, 0.34, 0.36), (0.55, -0.12, 0), -0.4, 2)
rock(b, (0.30, 0.28, 0.24), (-0.50, -0.30, 0), 0.6, 3)
rock(b, (0.22, 0.20, 0.16), (0.28, -0.48, 0), 0.2, 4)
finish_part(m, "Rock1_Body", b)
rock_grass(m, "Rock1", [(-0.52, 0.10, 6, 0.30), (0.48, 0.25, 5, 0.26)], 40)

m = S.model("Rock2", "Nature", 1.6)
b = S.new()
rock(b, (0.62, 0.55, 0.88), (-0.18, 0.10, 0), 0.4, 11, taper=0.68)
rock(b, (0.55, 0.48, 0.50), (0.32, -0.12, 0), -0.25, 12)
rock(b, (0.30, 0.26, 0.24), (-0.58, -0.25, 0), 0.9, 13)
rock(b, (0.22, 0.20, 0.17), (0.02, -0.46, 0), 0.3, 14)
rock(b, (0.18, 0.17, 0.14), (0.66, -0.40, 0), 0.7, 15)
finish_part(m, "Rock2_Body", b)
rock_grass(m, "Rock2", [(0.62, 0.20, 6, 0.30), (-0.40, 0.35, 5, 0.26)], 50)

m = S.model("Rock3", "Nature", 1.8)
b = S.new()
slab(b, (0.0, 0.0, 0.0), 0.34, 0.20, 21)
for k in range(6):
    a = k * 2 * PI / 6 + 0.3
    slab(b, (math.cos(a) * 0.52, math.sin(a) * 0.46, 0.0), 0.28, 0.12 + 0.05 * (k % 3), 30 + k, sides=5 + (k % 2))
rock(b, (0.22, 0.20, 0.16), (0.72, -0.42, 0), 0.4, 22)
finish_part(m, "Rock3_Body", b)
rock_grass(m, "Rock3", [(-0.75, -0.25, 6, 0.28), (0.68, 0.30, 5, 0.24)], 60)

stats = S.export(os.path.join(C.SRC, "stats_Nature.json"))
C.save_all()
print("DONE", {k: v["tris"] for k, v in stats.items()})
print("PARTS", {k: v["part_tris"] for k, v in stats.items()})
