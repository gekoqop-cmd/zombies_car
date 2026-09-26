"""Builds the low-poly player car in Blender and exports FBX.
Run:  blender -b --factory-startup --python build_car.py
Car faces -Y in Blender (= +Z forward in Unity). Units: meters, origin at ground.
"""
import bpy, bmesh, math, random, os, sys
from mathutils import Vector, Matrix, Euler

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from car_layout import *  # noqa

ROOT = r"D:\zombi-caaar"
OUT = os.path.join(ROOT, "models", "Car")
TEX = os.path.join(OUT, "Textures")
random.seed(3)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene

# ------------------------------------------------------------------ materials (4 total)


def make_mat(name, tex, emit=0.0, rough=0.6, metal=0.15):
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    nt = m.node_tree
    bs = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    bs.inputs["Roughness"].default_value = rough
    bs.inputs["Metallic"].default_value = metal
    img = bpy.data.images.load(os.path.join(TEX, tex), check_existing=True)
    n = nt.nodes.new("ShaderNodeTexImage"); n.image = img; n.location = (-400, 200)
    nt.links.new(n.outputs["Color"], bs.inputs["Base Color"])
    if emit:
        nt.links.new(n.outputs["Color"], bs.inputs["Emission Color"])
        bs.inputs["Emission Strength"].default_value = emit
    m.use_fake_user = True
    return m


M_INTACT = make_mat("M_Car_Intact", "car_atlas_intact.png")
M_DAMAGED = make_mat("M_Car_Damaged", "car_atlas_damaged.png")
M_WRECKED = make_mat("M_Car_Wrecked", "car_atlas_wrecked.png")
M_EMIT = make_mat("M_Car_Emissive", "car_emissive.png", emit=4.0, rough=0.3, metal=0.0)

# ------------------------------------------------------------------ UV helpers


def reg_uv(key, fx, fy, emit=False, pad=3):
    """fx, fy in 0..1 (fy bottom->top) -> atlas uv inside region."""
    size = EMIT if emit else ATLAS
    x, y, w, h = (EREG if emit else REG)[key]
    fx = min(max(fx, 0.0), 1.0); fy = min(max(fy, 0.0), 1.0)
    px = x + pad + fx * (w - 2 * pad)
    py = y + pad + (1 - fy) * (h - 2 * pad)
    return (px / size, 1 - py / size)


def paint_uv(co, n):
    ax = max(range(3), key=lambda i: abs(n[i]))
    s = -co.y
    if ax == 0:
        return reg_uv("side", (S1 - s) / (S1 - S0), (co.z - Z0) / (Z1 - Z0))
    if ax == 2:
        return reg_uv("top", (S1 - s) / (S1 - S0), (co.x - X0) / (X1 - X0))
    return reg_uv("front", (co.x - X0) / (X1 - X0), (co.z - Z0) / (Z1 - Z0))


def face_fit(f):
    n = f.normal
    ax = max(range(3), key=lambda i: abs(n[i]))
    pts = []
    for l in f.loops:
        c = l.vert.co
        if ax == 0:
            pts.append((c.y if n.x > 0 else -c.y, c.z))
        elif ax == 1:
            pts.append((-c.x if n.y > 0 else c.x, c.z))
        else:
            pts.append((c.x, c.y))
    us = [p[0] for p in pts]; vs = [p[1] for p in pts]
    du = (max(us) - min(us)) or 1; dv = (max(vs) - min(vs)) or 1
    return [((p[0] - min(us)) / du, (p[1] - min(vs)) / dv) for p in pts]


# ------------------------------------------------------------------ builder


class B:
    def __init__(self):
        self.bm = bmesh.new()
        self.bm.loops.layers.uv.verify()

    def add(self, t, spec, xform=None):
        """spec: 'paint' | ('fit', region) | ('remap', region) | ('emit', quad) | ('fitsub', region)"""
        if xform is not None:
            bmesh.ops.transform(t, matrix=xform, verts=t.verts)
        t.normal_update()
        uv = t.loops.layers.uv.verify()
        emit = isinstance(spec, tuple) and spec[0] == 'emit'
        sub = None
        if isinstance(spec, tuple) and spec[0] == 'fitsub':
            sx, sy = random.uniform(0, 0.35), random.uniform(0, 0.35)
            sub = (sx, sy, random.uniform(0.55, 0.65))
        for f in t.faces:
            f.material_index = 1 if emit else 0
            if spec == 'paint':
                for l in f.loops:
                    l[uv].uv = paint_uv(l.vert.co, f.normal)
            elif spec[0] in ('fit', 'emit', 'fitsub'):
                for l, (a, b) in zip(f.loops, face_fit(f)):
                    if sub:
                        a, b = sub[0] + a * sub[2], sub[1] + b * sub[2]
                    l[uv].uv = reg_uv(spec[1], a, b, emit=emit)
            elif spec[0] == 'remap':
                for l in f.loops:
                    a, b = l[uv].uv
                    l[uv].uv = reg_uv(spec[1], a % 1.0 if a > 1 else a, b)
        me = bpy.data.meshes.new("_t"); t.to_mesh(me); t.free()
        self.bm.from_mesh(me); bpy.data.meshes.remove(me)

    # --- primitives (all return via add)
    def box(self, size, center, spec, bevel=0.0, rot=(0, 0, 0)):
        t = bmesh.new()
        bmesh.ops.create_cube(t, size=1.0)
        for v in t.verts:
            v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        if bevel > 0:
            bmesh.ops.bevel(t, geom=list(t.edges), offset=min(bevel, min(size) * 0.45), segments=1,
                            affect='EDGES', profile=0.5, clamp_overlap=True)
        self.add(t, spec, Matrix.Translation(Vector(center)) @ Euler(rot).to_matrix().to_4x4())

    def lathe(self, profile, segs, spec, center=(0, 0, 0), rot=(0, 0, 0), smooth=True):
        t = bmesh.new(); uvl = t.loops.layers.uv.verify()
        lens = [0.0]
        for i in range(1, len(profile)):
            a, b = profile[i - 1], profile[i]
            lens.append(lens[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
        tot = lens[-1] or 1
        rings = []
        for (r, z) in profile:
            if r < 1e-6:
                c = t.verts.new((0, 0, z)); rings.append([c] * (segs + 1))
            else:
                ring = [t.verts.new((r * math.cos(2 * math.pi * j / segs), r * math.sin(2 * math.pi * j / segs), z))
                        for j in range(segs)]
                rings.append(ring + [ring[0]])
        for i in range(len(rings) - 1):
            for j in range(segs):
                q = [rings[i][j], rings[i][j + 1], rings[i + 1][j + 1], rings[i + 1][j]]
                uvs = [(j / segs, lens[i] / tot), ((j + 1) / segs, lens[i] / tot),
                       ((j + 1) / segs, lens[i + 1] / tot), (j / segs, lens[i + 1] / tot)]
                vv, uu = [], []
                for a, b in zip(q, uvs):
                    if a not in vv:
                        vv.append(a); uu.append(b)
                if len(vv) < 3:
                    continue
                try:
                    f = t.faces.new(vv)
                except ValueError:
                    continue
                f.smooth = smooth
                for l, u in zip(f.loops, uu):
                    l[uvl].uv = u
        self.add(t, spec, Matrix.Translation(Vector(center)) @ Euler(rot).to_matrix().to_4x4())

    def cyl(self, r, h, center, spec, segs=8, rot=(0, 0, 0)):
        self.lathe([(0, -h / 2), (r, -h / 2), (r, h / 2), (0, h / 2)], segs, spec, center, rot, smooth=False)

    def pipe(self, pts, radius, spec, segs=6, fillet=0.1, caps=True):
        P = [Vector(p) for p in pts]
        path = [P[0]]
        for i in range(1, len(P) - 1):
            a, p, b = P[i - 1], P[i], P[i + 1]
            rf = min(fillet, (a - p).length * 0.45, (b - p).length * 0.45)
            if rf < 1e-4:
                path.append(p); continue
            p0 = p + (a - p).normalized() * rf; p2 = p + (b - p).normalized() * rf
            for k in range(4):
                s = k / 3
                path.append((1 - s) ** 2 * p0 + 2 * (1 - s) * s * p + s * s * p2)
        path.append(P[-1])
        t = bmesh.new(); rings = []; prev = None
        for i, p in enumerate(path):
            if i == 0:
                d = (path[1] - p).normalized()
            elif i == len(path) - 1:
                d = (p - path[i - 1]).normalized()
            else:
                d = ((path[i + 1] - p).normalized() + (p - path[i - 1]).normalized()).normalized()
            if prev is None:
                up = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))
                nn = d.cross(up).normalized()
            else:
                nn = (prev - d * prev.dot(d)).normalized()
            prev = nn; bn = d.cross(nn)
            rings.append([t.verts.new(p + radius * (math.cos(2 * math.pi * j / segs) * nn +
                                                    math.sin(2 * math.pi * j / segs) * bn)) for j in range(segs)])
        for i in range(len(rings) - 1):
            for j in range(segs):
                f = t.faces.new((rings[i][j], rings[i][(j + 1) % segs], rings[i + 1][(j + 1) % segs], rings[i + 1][j]))
                f.smooth = True
        if caps:
            t.faces.new(rings[0]); t.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(t, faces=t.faces)
        self.add(t, spec)

    def quad(self, pts, spec, uvs=((0, 0), (1, 0), (1, 1), (0, 1))):
        t = bmesh.new(); uvl = t.loops.layers.uv.verify()
        f = t.faces.new([t.verts.new(p) for p in pts])
        for l, u in zip(f.loops, uvs):
            l[uvl].uv = u
        self.add(t, spec)

    def solid(self, verts, faces, spec, smooth=False):
        t = bmesh.new()
        vs = [t.verts.new(v) for v in verts]
        for fi in faces:
            f = t.faces.new([vs[i] for i in fi]); f.smooth = smooth
        bmesh.ops.recalc_face_normals(t, faces=t.faces)
        self.add(t, spec)

    def finish(self, name, parent, loc=(0, 0, 0), smooth_angle=None):
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me); self.bm.free()
        me.materials.append(M_INTACT)
        if any(p.material_index == 1 for p in me.polygons):
            me.materials.append(M_EMIT)
        ob = bpy.data.objects.new(name, me)
        COL.objects.link(ob)
        ob.location = loc
        ob.parent = parent
        return ob


COL = bpy.data.collections.new("Car_Player"); scene.collection.children.link(COL)
root = bpy.data.objects.new("Car_Player", None); COL.objects.link(root)
root.empty_display_type = 'ARROWS'


def empty(name, loc, parent=root, size=0.25, kind='PLAIN_AXES'):
    e = bpy.data.objects.new(name, None); COL.objects.link(e)
    e.location = loc; e.parent = parent; e.empty_display_size = size; e.empty_display_type = kind
    return e


def P(s, x, z):
    """car coords: s forward (front = -Y), x right, z up."""
    return (x, -s, z)


# ------------------------------------------------------------------ dimensions
HW = 1.05                   # half width of lower body
WHEEL_R, WHEEL_W = 0.40, 0.30
AXLE_F, AXLE_R, AXLE_Z = 1.41, -1.29, 0.40
TRACK = 0.93                # wheel center x
ARCH_R = 0.52
SILL = SILL_Z
BELT = BELT_Z
# cabin
CAB_BOT_F, CAB_BOT_R = 1.36, -2.02     # s at belt line
CAB_TOP_F, CAB_TOP_R = 1.02, -1.74     # s at roof
CAB_TOP_Z = 2.06
CAB_HW_BOT, CAB_HW_TOP = 1.02, 0.90

# ------------------------------------------------------------------ BODY
body = B()


def arc(cs, a0, a1, n):
    out = []
    for k in range(n + 1):
        a = math.radians(a0 + (a1 - a0) * k / n)
        out.append((cs + ARCH_R * math.cos(a), AXLE_Z + ARCH_R * math.sin(a)))
    return out


a0 = math.degrees(math.asin((SILL - AXLE_Z) / ARCH_R))
prof = [(2.15, 0.60), (2.11, SILL)]
prof += arc(AXLE_F, a0, 180 - a0, 8)
prof += arc(AXLE_R, a0, 180 - a0, 8)
prof += [(-2.10, SILL), (-2.15, 0.60), (-2.16, BELT - 0.06), (-2.12, BELT), (CAB_BOT_F + 0.02, BELT),
         (CAB_BOT_F + 0.06, BELT - 0.10), (2.08, BELT - 0.12), (2.15, BELT - 0.18)]
# prism along x
t = bmesh.new()
L = [t.verts.new(P(s, -HW, z)) for s, z in prof]
R = [t.verts.new(P(s, HW, z)) for s, z in prof]
t.faces.new(L); t.faces.new(list(reversed(R)))
n = len(prof)
for i in range(n):
    j = (i + 1) % n
    t.faces.new((L[i], L[j], R[j], R[i]))
bmesh.ops.recalc_face_normals(t, faces=t.faces)
cap_edges = [e for e in t.edges if all(abs(abs(v.co.x) - HW) < 1e-4 for v in e.verts)
             and abs(e.verts[0].co.x - e.verts[1].co.x) < 1e-4]
bmesh.ops.bevel(t, geom=cap_edges, offset=0.05, segments=1, affect='EDGES', profile=0.5, clamp_overlap=True)
body.add(t, 'paint')

# cabin (tapered hull)
cab = [P(CAB_BOT_F, -CAB_HW_BOT, BELT - 0.01), P(CAB_BOT_F, CAB_HW_BOT, BELT - 0.01),
       P(CAB_BOT_R, CAB_HW_BOT, BELT - 0.01), P(CAB_BOT_R, -CAB_HW_BOT, BELT - 0.01),
       P(CAB_TOP_F, -CAB_HW_TOP, CAB_TOP_Z), P(CAB_TOP_F, CAB_HW_TOP, CAB_TOP_Z),
       P(CAB_TOP_R, CAB_HW_TOP, CAB_TOP_Z), P(CAB_TOP_R, -CAB_HW_TOP, CAB_TOP_Z)]
body.solid(cab, [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)], 'paint')
# roof panel with drip rail
body.box((2 * CAB_HW_TOP + 0.08, CAB_TOP_F - CAB_TOP_R + 0.08, 0.07),
         P((CAB_TOP_F + CAB_TOP_R) / 2, 0, CAB_TOP_Z + 0.02), 'paint', bevel=0.03)


def cab_hw(z):
    return CAB_HW_BOT + (CAB_HW_TOP - CAB_HW_BOT) * (z - BELT) / (CAB_TOP_Z - BELT)


def s_front(z):
    return CAB_BOT_F + (CAB_TOP_F - CAB_BOT_F) * (z - BELT) / (CAB_TOP_Z - BELT)


def s_rear(z):
    return CAB_BOT_R + (CAB_TOP_R - CAB_BOT_R) * (z - BELT) / (CAB_TOP_Z - BELT)


# side windows (4 per side) with rubber frames
WZ0, WZ1 = BELT + 0.07, CAB_TOP_Z - 0.09
WINS = [(None, 0.66), (0.54, -0.16), (-0.27, -0.93), (-1.04, None)]
for side in (-1, 1):
    for i, (sa, sb) in enumerate(WINS):
        def edge(z, s_, front):
            if s_ is not None:
                return s_
            return s_front(z) - 0.11 if front else s_rear(z) + 0.12
        for off, spec, grow in ((0.004, ('fit', 'rubber'), 0.035), (0.010, ('fitsub', 'glass'), 0.0)):
            pts = []
            for (z, front) in ((WZ0 - grow, True), (WZ0 - grow, False), (WZ1 + grow, False), (WZ1 + grow, True)):
                s = edge(z, sa if front else sb, front) + (grow if front else -grow)
                pts.append(P(s, side * (cab_hw(z) + off), z))
            if side < 0:
                pts = [pts[1], pts[0], pts[3], pts[2]]
            body.quad(pts, spec)


def slanted_pane(front, t0, t1, u0, u1, off, spec):
    sb = CAB_BOT_F if front else CAB_BOT_R
    st = CAB_TOP_F if front else CAB_TOP_R
    pb = Vector(P(sb, 0, BELT)); pt = Vector(P(st, 0, CAB_TOP_Z))
    nrm = Vector((0, -1, 0)) if front else Vector((0, 1, 0))
    d = (pt - pb)
    nrm = (nrm - d * nrm.dot(d) / d.length_squared).normalized()
    pts = []
    for (u, tt) in ((u0, t0), (u1, t0), (u1, t1), (u0, t1)):
        c = pb + d * tt
        hw = CAB_HW_BOT + (CAB_HW_TOP - CAB_HW_BOT) * tt
        pts.append(tuple(Vector((u * hw, c.y, c.z)) + nrm * off))
    if not front:
        pts = [pts[1], pts[0], pts[3], pts[2]]
    body.quad(pts, spec)


slanted_pane(True, 0.05, 0.93, -0.90, 0.90, 0.004, ('fit', 'rubber'))
slanted_pane(True, 0.09, 0.89, -0.85, 0.85, 0.010, ('fitsub', 'glass'))
slanted_pane(False, 0.08, 0.90, -0.84, 0.84, 0.004, ('fit', 'rubber'))
slanted_pane(False, 0.12, 0.86, -0.79, 0.79, 0.010, ('fitsub', 'glass'))

# wipers
for x in (-0.35, 0.3):
    body.box((0.55, 0.03, 0.02), P(CAB_BOT_F - 0.10, x, BELT + 0.10), ('fit', 'rubber'),
             rot=(math.radians(-40), 0, math.radians(8)))
body.box((0.4, 0.03, 0.02), P(CAB_BOT_R - 0.04, 0.1, BELT + 0.2), ('fit', 'rubber'), rot=(math.radians(10), 0, 0.2))

# front face: grille, headlights, indicators
FS = 2.15
body.box((1.00, 0.06, 0.30), P(FS + 0.01, 0, 0.98), ('fit', 'trim'), bevel=0.02)
for k in range(3):
    body.box((0.92, 0.04, 0.035), P(FS + 0.035, 0, 0.88 + k * 0.09), ('fit', 'rubber'))
for x in (-0.76, 0.76):
    body.box((0.34, 0.07, 0.30), P(FS + 0.015, x, 0.98), ('fit', 'trim'), bevel=0.03)
    body.lathe([(0, 0), (0.115, 0), (0.12, 0.03), (0.09, 0.05), (0, 0.055)], 10, ('emit', 'head'),
               P(FS + 0.045, x, 0.98), rot=(math.radians(90), 0, 0))
    body.box((0.14, 0.03, 0.07), P(FS + 0.02, x * 1.02, 0.74), ('emit', 'ind'))
# rear face: tail lights, plate, handle
RS = -2.16
for x in (-0.90, 0.90):
    body.box((0.16, 0.06, 0.46), P(RS - 0.005, x, 1.03), ('fit', 'trim'), bevel=0.02)
    body.box((0.12, 0.03, 0.40), P(RS - 0.035, x, 1.03), ('emit', 'tail'))
body.box((0.50, 0.02, 0.15), P(RS - 0.02, 0, 0.82), ('fit', 'plate'))
body.box((0.30, 0.04, 0.04), P(RS - 0.02, 0, 1.12), ('fit', 'trim'))

# rear bumper
body.box((2.16, 0.22, 0.20), P(RS - 0.08, 0, 0.58), ('fit', 'trim'), bevel=0.03)
for x in (-0.95, 0.95):
    body.box((0.24, 0.14, 0.18), P(RS - 0.10, x, 0.60), ('fit', 'plate'), bevel=0.02)

# fender flares (dark plastic arches)
for cs in (AXLE_F, AXLE_R):
    for side in (-1, 1):
        pts = []
        for k in range(9):
            a = math.radians(a0 + (180 - 2 * a0) * k / 8)
            pts.append(P(cs + (ARCH_R + 0.03) * math.cos(a), side * (HW + 0.03), AXLE_Z + (ARCH_R + 0.03) * math.sin(a)))
        body.pipe(pts, 0.05, ('fit', 'trim'), segs=4, fillet=0.0)

# rock sliders, chassis, mud flaps
for side in (-1, 1):
    body.box((0.10, 1.10, 0.12), P(0.25, side * (HW + 0.02), SILL + 0.02), ('fit', 'trim'), bevel=0.02)
    for s in (AXLE_F - 0.6, AXLE_R - 0.6):
        body.box((0.18, 0.02, 0.22), P(s, side * (HW - 0.12), SILL - 0.08), ('fit', 'rubber'))
body.box((1.5, 3.9, 0.18), P(0.05, 0, SILL - 0.07), ('fit', 'trim'))
for cs in (AXLE_F, AXLE_R):
    body.cyl(0.06, 2 * TRACK - 0.2, P(cs, 0, AXLE_Z), ('fit', 'trim'), segs=6, rot=(0, math.pi / 2, 0))

# door handles
for side in (-1, 1):
    for s in (0.48, -0.30):
        body.box((0.03, 0.18, 0.045), P(s, side * (HW + 0.01), 1.27), ('fit', 'trim'), bevel=0.01)

# mirrors
for side in (-1, 1):
    body.box((0.05, 0.12, 0.05), P(CAB_BOT_F - 0.05, side * (HW + 0.02), BELT + 0.12), ('fit', 'trim'))
    body.box((0.08, 0.12, 0.26), P(CAB_BOT_F - 0.06, side * (HW + 0.12), BELT + 0.22), ('fit', 'trim'), bevel=0.02)

# snorkel on the right fender
body.pipe([P(1.75, HW + 0.02, 1.05), P(1.62, HW + 0.08, 1.12), P(1.44, HW + 0.08, BELT + 0.05),
           P(1.12, HW - 0.02, CAB_TOP_Z - 0.05), P(1.12, HW - 0.02, CAB_TOP_Z + 0.08)], 0.065, ('fit', 'trim'), segs=6)
body.box((0.20, 0.16, 0.16), P(1.16, HW - 0.02, CAB_TOP_Z + 0.14), ('fit', 'trim'), bevel=0.03)

body_ob = body.finish("Body", root)

# ------------------------------------------------------------------ HOOD (+ dented variant)


def hood_slab(bb, dent=False):
    s0, s1 = CAB_BOT_F + 0.08, 2.14
    nx, ny = (6, 5) if dent else (1, 1)
    rnd = random.Random(9)
    top, bot = [], []
    for i in range(nx + 1):
        for j in range(ny + 1):
            u, v = i / nx, j / ny
            s = s1 + (s0 - s1) * u
            x = -HW + 0.06 + (2 * HW - 0.12) * v
            zt = BELT - 0.06 + 0.055 - 0.035 * (1 - u) ** 2
            dz = dy = 0.0
            if dent:
                d1 = math.exp(-((u - 0.25) ** 2 + (v - 0.3) ** 2) / 0.02)
                d2 = math.exp(-((u - 0.55) ** 2 + (v - 0.75) ** 2) / 0.03)
                dz = -0.075 * d1 - 0.06 * d2 + rnd.uniform(-0.012, 0.012)
                if i == 0:                     # crumpled front edge
                    dz += 0.10 + 0.05 * math.sin(v * 9)
                    s -= 0.09
                if i == 1:
                    dz += 0.06 * math.sin(v * 7 + 1) + 0.03
            top.append((P(s, x, zt + dz)))
            bot.append((P(s, x, BELT - 0.115 + max(dz, 0.0) * 0.5)))
    verts = top + bot
    k = lambda i, j: i * (ny + 1) + j
    faces = []
    off = len(top)
    for i in range(nx):
        for j in range(ny):
            faces.append((k(i, j), k(i, j + 1), k(i + 1, j + 1), k(i + 1, j)))
            faces.append((off + k(i, j), off + k(i + 1, j), off + k(i + 1, j + 1), off + k(i, j + 1)))
    for i in range(nx):
        for j in (0, ny):
            faces.append((k(i, j), k(i + 1, j), off + k(i + 1, j), off + k(i, j)))
    for j in range(ny):
        for i in (0, nx):
            faces.append((k(i, j), off + k(i, j), off + k(i, j + 1), k(i, j + 1)))
    bb.solid(verts, faces, 'paint', smooth=False)


hb = B(); hood_slab(hb); hood_ob = hb.finish("Hood", root)
hb = B(); hood_slab(hb, dent=True); dmg_hood = hb.finish("DMG1_Hood_Dented", root)

# ------------------------------------------------------------------ FRONT BUMPER (+ bent)


def bumper(bb, bent=False):
    n = 8 if bent else 1
    L, D, H = 2.20, 0.24, 0.22
    sc, zc = FS + 0.12, 0.60
    rings = []
    for i in range(n + 1):
        x = -L / 2 + L * i / n
        ds = dz = rz = 0.0
        if bent and x > 0.2:
            k = (x - 0.2) / (L / 2 - 0.2)
            ds, dz, rz = -0.42 * k ** 1.5, -0.18 * k ** 2, 0.45 * k
        if bent and x < -0.6:
            dz += 0.04 * (-0.6 - x)
        ring = []
        for (a, b) in ((-D / 2, -H / 2), (D / 2, -H / 2), (D / 2, H / 2), (-D / 2, H / 2)):
            bb_ = b * math.cos(rz) - a * math.sin(rz); aa = a * math.cos(rz) + b * math.sin(rz)
            ring.append(P(sc + ds + aa, x, zc + dz + bb_))
        rings.append(ring)
    verts = [v for r in rings for v in r]
    faces = [(0, 1, 2, 3), tuple(4 * n + k for k in (3, 2, 1, 0))]
    for i in range(n):
        for k in range(4):
            a, b = 4 * i + k, 4 * i + (k + 1) % 4
            faces.append((a, b, b + 4, a + 4))
    bb.solid(verts, faces, ('fit', 'trim'))
    # tow hooks / end caps
    bb.box((0.08, 0.10, 0.08), P(sc + 0.14, -0.45, zc - 0.05), ('fit', 'plate'))
    if not bent:
        bb.box((0.08, 0.10, 0.08), P(sc + 0.14, 0.45, zc - 0.05), ('fit', 'plate'))


bb = B(); bumper(bb); bumper_ob = bb.finish("Bumper_Front", root)
bb = B(); bumper(bb, bent=True); dmg_bumper = bb.finish("DMG2_Bumper_Bent", root)

# ------------------------------------------------------------------ WHEELS (origin at wheel center)


def wheel(bb, side):
    R, W = WHEEL_R, WHEEL_W
    rin = 0.25
    prof = [(rin, -W / 2), (R - 0.05, -W / 2), (R, -W / 2 + 0.05), (R, W / 2 - 0.05), (R - 0.05, W / 2), (rin, W / 2), (rin, -W / 2)]
    bb.lathe(prof, 14, ('remap', 'tire'), (0, 0, 0), rot=(0, math.pi / 2, 0))
    # rim dish on the outer face + flat inner face
    xo = side * (W / 2 - 0.03)
    t = bmesh.new(); uvl = t.loops.layers.uv.verify()
    segs = 14
    rings = []
    for r, dx in ((rin, 0.0), (rin - 0.03, -0.04), (0.10, -0.05), (0.0, -0.03)):
        if r == 0:
            rings.append([t.verts.new((xo + side * dx, 0, 0))] * segs)
        else:
            rings.append([t.verts.new((xo + side * dx, r * math.cos(2 * math.pi * j / segs),
                                       r * math.sin(2 * math.pi * j / segs))) for j in range(segs)])
    for i in range(3):
        for j in range(segs):
            q = [rings[i][j], rings[i][(j + 1) % segs], rings[i + 1][(j + 1) % segs], rings[i + 1][j]]
            vv = []
            for v in q:
                if v not in vv:
                    vv.append(v)
            f = t.faces.new(vv)
            for l in f.loops:
                c = l.vert.co
                l[uvl].uv = reg_uv("rim", 0.5 + c.y / (2 * rin) * side, 0.5 + c.z / (2 * rin))
    inner = [t.verts.new((-side * (W / 2 - 0.02), rin * math.cos(2 * math.pi * j / segs),
                          rin * math.sin(2 * math.pi * j / segs))) for j in range(segs)]
    f = t.faces.new(inner)
    for l in f.loops:
        l[uvl].uv = reg_uv("rim", 0.5, 0.5)
    bmesh.ops.recalc_face_normals(t, faces=t.faces)
    me = bpy.data.meshes.new("_t"); t.to_mesh(me); t.free()
    bb.bm.from_mesh(me); bpy.data.meshes.remove(me)


WHEELS = {}
for nm, s, side in (("Wheel_FL", AXLE_F, -1), ("Wheel_FR", AXLE_F, 1), ("Wheel_RL", AXLE_R, -1), ("Wheel_RR", AXLE_R, 1)):
    wb = B(); wheel(wb, side)
    WHEELS[nm] = wb.finish(nm, root, loc=P(s, side * TRACK, AXLE_Z))

# ------------------------------------------------------------------ BULL BAR
bar = B()
BS = FS + 0.30
TR = 0.04
bar.pipe([P(BS, -0.95, 0.62), P(BS, 0.95, 0.62)], TR, ('fit', 'trim'))
for x in (-0.34, 0.34):
    bar.pipe([P(FS + 0.20, x, 0.55), P(BS + 0.02, x, 0.62), P(BS + 0.02, x, 1.32), P(FS - 0.05, x, BELT - 0.02)],
             TR, ('fit', 'trim'), fillet=0.12)
for z in (0.86, 1.10):
    bar.pipe([P(BS + 0.02, -0.34, z), P(BS + 0.02, 0.34, z)], TR * 0.8, ('fit', 'trim'))
for sgn in (-1, 1):
    bar.pipe([P(BS + 0.02, sgn * 0.34, 1.20), P(BS - 0.02, sgn * 0.98, 1.20), P(BS - 0.08, sgn * 1.04, 0.62),
              P(FS + 0.12, sgn * 1.04, 0.56)], TR, ('fit', 'trim'), fillet=0.14)
    bar.box((0.12, 0.10, 0.10), P(FS + 0.20, sgn * 0.34, 0.57), ('fit', 'trim'))
bullbar_ob = bar.finish("BullBar", root)

# ------------------------------------------------------------------ ROOF RACK
rk = B()
RZ = CAB_TOP_Z + 0.06     # roof top
R0, R1 = CAB_TOP_F - 0.04, CAB_TOP_R + 0.10
RHW = CAB_HW_TOP - 0.02
ZB, ZT = RZ + 0.10, RZ + 0.30
rr = 0.022
rk.pipe([P(R0, -RHW, ZB), P(R1, -RHW, ZB), P(R1, RHW, ZB), P(R0, RHW, ZB), P(R0, -RHW, ZB)], rr, ('fit', 'trim'), fillet=0.12)
rk.pipe([P(R0 - 0.02, -RHW, ZT), P(R1 + 0.02, -RHW, ZT), P(R1 + 0.02, RHW, ZT), P(R0 - 0.02, RHW, ZT), P(R0 - 0.02, -RHW, ZT)],
        rr, ('fit', 'trim'), fillet=0.15)
nposts = 4
for i in range(nposts):
    s = R0 + (R1 - R0) * i / (nposts - 1)
    for side in (-1, 1):
        rk.pipe([P(s, side * RHW, RZ - 0.01), P(s, side * RHW, ZT)], rr, ('fit', 'trim'), caps=False)
        rk.box((0.10, 0.08, 0.04), P(s, side * RHW, RZ + 0.005), ('fit', 'rubber'))
for i in range(1, 5):
    s = R0 + (R1 - R0) * i / 5
    rk.pipe([P(s, -RHW, ZB), P(s, RHW, ZB)], rr * 0.8, ('fit', 'trim'), caps=False)
# light bar mounts
rk.pipe([P(R0 - 0.02, -RHW, ZT), P(R0 - 0.02, RHW, ZT)], rr, ('fit', 'trim'), caps=False)
rack_ob = rk.finish("RoofRack", root)

# ------------------------------------------------------------------ ROOF LAMPS
lp = B()
for (s, x, big) in ((R0 - 0.06, -0.52, True), (R0 - 0.06, 0.52, True), (R1 - 0.03, -0.62, False), (R1 - 0.03, 0.62, False)):
    r = 0.11 if big else 0.07
    rot = (math.radians(90), 0, 0) if big else (math.radians(-90), 0, 0)
    lp.lathe([(0, 0), (r * 0.7, 0), (r, 0.06), (r, 0.10)], 10, ('fit', 'yellow'), P(s, x, ZT + r * 0.2), rot=rot)
    lp.lathe([(r * 0.92, 0.10), (0, 0.115)], 10, ('emit', 'lamp' if big else 'tail'), P(s, x, ZT + r * 0.2), rot=rot)
    lp.box((0.04, 0.05, 0.08), P(s + (0.02 if big else -0.02), x, ZT - 0.02), ('fit', 'trim'))
lamps_ob = lp.finish("RoofLamps", root)

# ------------------------------------------------------------------ ANTENNA
an = B()
an.box((0.06, 0.06, 0.05), (0, 0, 0.025), ('fit', 'trim'))
an.pipe([(0, 0, 0.04), (0, 0.03, 0.75)], 0.010, ('fit', 'orange'), segs=5)
an.lathe([(0, -0.03), (0.03, -0.015), (0.03, 0.015), (0, 0.03)], 6, ('fit', 'orange'), (0, 0.03, 0.77))
ant_ob = an.finish("Antenna", root, loc=P(R0 + 0.02, -RHW - 0.06, ZT - 0.02))

# ------------------------------------------------------------------ CARGO


def jerrycan(bb, key, c=(0, 0, 0), rot=(0, 0, 0)):
    Wc, Tc, Hc = 0.40, 0.22, 0.56
    m = Matrix.Translation(Vector(c)) @ Euler(rot).to_matrix().to_4x4()
    t = bmesh.new(); bmesh.ops.create_cube(t, size=1.0)
    for v in t.verts:
        v.co = Vector((v.co.x * Tc, v.co.y * Wc, v.co.z * Hc + Hc / 2))
    bmesh.ops.bevel(t, geom=list(t.edges), offset=0.03, segments=1, affect='EDGES', clamp_overlap=True)
    bb.add(t, ('fit', key), m)
    # handle + spout
    t = bmesh.new(); bmesh.ops.create_cube(t, size=1.0)
    for v in t.verts:
        v.co = Vector((v.co.x * 0.05, v.co.y * 0.22, v.co.z * 0.05 + Hc + 0.05))
    bb.add(t, ('fit', key), m)
    for yy in (-0.09, 0.09):
        t = bmesh.new(); bmesh.ops.create_cube(t, size=1.0)
        for v in t.verts:
            v.co = Vector((v.co.x * 0.04, v.co.y * 0.03 + yy, v.co.z * 0.06 + Hc + 0.02))
        bb.add(t, ('fit', key), m)
    t = bmesh.new()
    bmesh.ops.create_cone(t, cap_ends=True, segments=6, radius1=0.035, radius2=0.035, depth=0.07)
    bmesh.ops.transform(t, matrix=Matrix.Translation((0, Wc / 2 - 0.07, Hc + 0.03)), verts=t.verts)
    bb.add(t, ('fit', 'trim'), m)


def crate(bb, c=(0, 0, 0), rot=(0, 0, 0)):
    m = Matrix.Translation(Vector(c)) @ Euler(rot).to_matrix().to_4x4()
    t = bmesh.new(); bmesh.ops.create_cube(t, size=1.0)
    for v in t.verts:
        v.co = Vector((v.co.x * 0.72, v.co.y * 0.46, v.co.z * 0.34 + 0.17))
    bmesh.ops.bevel(t, geom=list(t.edges), offset=0.02, segments=1, affect='EDGES', clamp_overlap=True)
    bb.add(t, ('fit', 'crate'), m)
    t = bmesh.new(); bmesh.ops.create_cube(t, size=1.0)
    for v in t.verts:
        v.co = Vector((v.co.x * 0.74, v.co.y * 0.47, v.co.z * 0.05 + 0.33))
    bb.add(t, ('fit', 'trim'), m)


def spare(bb, c=(0, 0, 0), rot=(0, 0, 0)):
    R, W, rin = 0.40, 0.28, 0.22
    prof = [(rin, -W / 2), (R - 0.05, -W / 2), (R, -W / 2 + 0.05), (R, W / 2 - 0.05), (R - 0.05, W / 2), (rin, W / 2),
            (rin, -W / 2)]
    m = Matrix.Translation(Vector(c)) @ Euler(rot).to_matrix().to_4x4()
    t = bmesh.new()
    tb = B(); tb.lathe(prof, 12, ('remap', 'tire'))
    tb.lathe([(0, -0.06), (rin + 0.005, -0.06), (rin + 0.005, 0.06), (0, 0.06)], 12, ('fit', 'rim'), smooth=False)
    me = bpy.data.meshes.new("_s"); tb.bm.to_mesh(me); tb.bm.free()
    t.from_mesh(me); bpy.data.meshes.remove(me)
    bmesh.ops.transform(t, matrix=m @ Matrix.Translation((0, 0, W / 2)), verts=t.verts)
    me = bpy.data.meshes.new("_t"); t.to_mesh(me); t.free()
    bb.bm.from_mesh(me); bpy.data.meshes.remove(me)


cargo = empty("Cargo", P(0, 0, ZB + rr), size=0.3)
CB = ZB + rr
CARGO_PARTS = []


def cargo_obj(name, fn, loc, **kw):
    cb = B(); fn(cb, **kw)
    ob = cb.finish(name, cargo)
    ob.location = Vector(loc) - Vector(cargo.location)
    CARGO_PARTS.append(ob)
    return ob


cargo_obj("Cargo_Jerrycan_Green", lambda b_: jerrycan(b_, "can_g"), P(0.62, -0.42, CB), )
cargo_obj("Cargo_Jerrycan_Red", lambda b_: jerrycan(b_, "can_r", rot=(0, 0, 0.06)), P(0.62, 0.05, CB))
cargo_obj("Cargo_Crate", lambda b_: crate(b_, rot=(0, 0, math.pi / 2)), P(-0.25, -0.30, CB))
cargo_obj("Cargo_SpareTire", lambda b_: spare(b_), P(-0.95, 0.30, CB))
# origins of cargo items at their base
for ob in CARGO_PARTS:
    pass

# fallen cargo (stage 3): cans tipped, crate slid, spare hanging off the back
fb = B()
jerrycan(fb, "can_r", c=Vector(P(0.45, 0.35, CB + 0.11)) - Vector(P(0, 0, 0)), rot=(math.radians(90), 0, 0.5))
crate(fb, c=P(-0.55, -0.55, CB + 0.02), rot=(math.radians(-12), math.radians(8), math.pi / 2 + 0.35))
spare(fb, c=P(-1.55, 0.25, CB - 0.02), rot=(math.radians(-28), 0, 0.2))
dmg_cargo = fb.finish("DMG3_Cargo_Fallen", root)

# ------------------------------------------------------------------ upgrade sockets
for nm, loc in [("UPG_DoorArmor_L", P(0.15, -HW - 0.04, 0.95)), ("UPG_DoorArmor_R", P(0.15, HW + 0.04, 0.95)),
                ("UPG_WindowBars_Front", P((CAB_BOT_F + CAB_TOP_F) / 2 + 0.03, 0, (BELT + CAB_TOP_Z) / 2)),
                ("UPG_WindowBars_L", P(-0.5, -CAB_HW_BOT - 0.02, (BELT + CAB_TOP_Z) / 2)),
                ("UPG_WindowBars_R", P(-0.5, CAB_HW_BOT + 0.02, (BELT + CAB_TOP_Z) / 2)),
                ("UPG_WindowBars_Rear", P((CAB_BOT_R + CAB_TOP_R) / 2 - 0.03, 0, (BELT + CAB_TOP_Z) / 2)),
                ("UPG_Turret", P(-0.55, 0, ZT + 0.05))]:
    empty(nm, loc, kind='SINGLE_ARROW' if "Turret" in nm else 'CUBE', size=0.15)

# damage variants hidden by default
DMG = [dmg_hood, dmg_bumper, dmg_cargo]
for o in DMG:
    o.hide_render = True; o.hide_viewport = True

# ------------------------------------------------------------------ lights / world for preview
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN')); scene.collection.objects.link(sun)
sun.data.energy = 4.0; sun.data.color = (1.0, 0.93, 0.84); sun.data.angle = math.radians(3)
sun.rotation_euler = (math.radians(50), 0, math.radians(-35))
fill = bpy.data.objects.new("Fill", bpy.data.lights.new("Fill", 'SUN')); scene.collection.objects.link(fill)
fill.data.energy = 0.8; fill.data.color = (0.6, 0.7, 1.0); fill.rotation_euler = (math.radians(60), 0, math.radians(150))
world = bpy.data.worlds.new("World"); scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes.get("Background")
bg.inputs[0].default_value = (0.22, 0.25, 0.32, 1); bg.inputs[1].default_value = 0.7

# ------------------------------------------------------------------ stats
def tri_count(objs):
    t = 0
    for o in objs:
        if o.type == 'MESH':
            o.data.calc_loop_triangles(); t += len(o.data.loop_triangles)
    return t


meshes = [o for o in COL.objects if o.type == 'MESH']
base = [o for o in meshes if o not in DMG]
stats = {"base": tri_count(base), "dmg": tri_count(DMG), "all": tri_count(meshes)}
per = {o.name: tri_count([o]) for o in meshes}
print("TRIS", stats)
for k, v in sorted(per.items(), key=lambda kv: -kv[1]):
    print("   ", k, v)
os.makedirs(OUT, exist_ok=True)
with open(os.path.join(OUT, "_stats.txt"), "w") as fh:
    fh.write(f"{stats['base']} {stats['dmg']} {stats['all']}\n")

# ------------------------------------------------------------------ save + export
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "Car_Player.blend"), relative_remap=True)
for o in DMG:
    o.hide_viewport = False
bpy.ops.object.select_all(action='DESELECT')
for o in COL.objects:
    o.select_set(True)
bpy.context.view_layer.objects.active = root
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, "Car_Player.fbx"), use_selection=True,
                         object_types={'MESH', 'EMPTY'}, apply_scale_options='FBX_SCALE_ALL',
                         apply_unit_scale=True, bake_space_transform=True, axis_forward='-Z', axis_up='Y',
                         mesh_smooth_type='FACE', path_mode='RELATIVE', embed_textures=False,
                         add_leaf_bones=False, bake_anim=False)
print("EXPORT OK")
