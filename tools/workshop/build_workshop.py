"""Builds the low-poly 'Мастерская' (workshop) asset in Blender and exports FBX.
Run:  blender -b --factory-startup --python build_workshop.py
"""
import bpy, bmesh, math, random, os
from mathutils import Vector, Matrix, Euler

ROOT = r"D:\zombi-caaar"
TEX = os.path.join(ROOT, "models", "Textures")
OUT = os.path.join(ROOT, "models")
random.seed(7)

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'

# ------------------------------------------------------------------ materials
MATS = {}


def make_mat(name, tex, rough=0.85, metal=0.0, emit=0.0):
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    img = bpy.data.images.load(os.path.join(TEX, tex), check_existing=True)
    n = nt.nodes.new("ShaderNodeTexImage"); n.image = img; n.location = (-400, 200)
    nt.links.new(n.outputs["Color"], bsdf.inputs["Base Color"])
    if emit > 0:
        nt.links.new(n.outputs["Color"], bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = emit
    px = img.pixels[:4] if img.size[0] else (0.5, 0.5, 0.5, 1)
    m.diffuse_color = (px[0], px[1], px[2], 1)
    MATS[name] = m
    return m


for name, tex, kw in [
    ("M_StoneWall", "stone_wall.png", {}),
    ("M_Plaster", "plaster.png", {}),
    ("M_RedBand", "red_band.png", {"rough": 0.7}),
    ("M_StoneGray", "stone_gray.png", {}),
    ("M_Roof", "roof_concrete.png", {}),
    ("M_Floor", "floor_tiles.png", {"rough": 0.7}),
    ("M_Paver", "paver.png", {}),
    ("M_Soil", "soil.png", {"rough": 1.0}),
    ("M_Rock", "rock.png", {}),
    ("M_Wood", "wood.png", {"rough": 0.8}),
    ("M_MetalDark", "metal_dark.png", {"rough": 0.5, "metal": 0.6}),
    ("M_MetalLight", "metal_light.png", {"rough": 0.5, "metal": 0.3}),
    ("M_MetalRed", "metal_red.png", {"rough": 0.45, "metal": 0.2}),
    ("M_Cardboard", "cardboard.png", {"rough": 0.9}),
    ("M_Tire", "tire.png", {"rough": 0.9}),
    ("M_Barrel", "barrel.png", {"rough": 0.5, "metal": 0.3}),
    ("M_Glass", "glass.png", {"rough": 0.1}),
    ("M_LampGlow", "lamp_glow.png", {"rough": 0.3, "emit": 6.0}),
    ("M_Vent", "vent.png", {"rough": 0.6}),
    ("M_Fan", "fan.png", {"rough": 0.5, "metal": 0.3}),
    ("M_Sign", "sign.png", {"rough": 0.6}),
    ("M_Pegboard", "pegboard.png", {"rough": 0.8}),
    ("M_Leaf", "leaf.png", {"rough": 0.6}),
    ("M_Grass", "grass.png", {"rough": 0.6}),
]:
    make_mat(name, tex, **kw)

# UV mode per material: ('box', tile_m) | 'fit' | 'keep'
UVMODE = {
    "M_StoneWall": ("box", 2.0), "M_Plaster": ("box", 2.0), "M_RedBand": ("box", 2.0),
    "M_StoneGray": ("box", 1.6), "M_Roof": ("box", 3.0), "M_Floor": ("box", 2.0),
    "M_Paver": ("box", 1.4), "M_Soil": ("box", 3.0), "M_Rock": ("box", 0.6),
    "M_Wood": ("box", 1.2), "M_MetalDark": ("box", 1.0), "M_MetalLight": ("box", 1.0),
    "M_MetalRed": ("box", 1.0), "M_LampGlow": ("box", 1.0),
    "M_Cardboard": "fit", "M_Glass": "fit", "M_Vent": "fit", "M_Fan": "fit",
    "M_Sign": "fit", "M_Pegboard": "fit",
    "M_Tire": "keep", "M_Barrel": "keep", "M_Leaf": "keep", "M_Grass": "keep",
}

# ------------------------------------------------------------------ builder


class B:
    """Accumulates geometry for one object in local space."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.verify()
        self.mats = []

    def mi(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def _merge(self, tmp, double_sided=False):
        if double_sided:
            dup = bmesh.ops.duplicate(tmp, geom=list(tmp.verts) + list(tmp.edges) + list(tmp.faces))
            bmesh.ops.reverse_faces(tmp, faces=[g for g in dup['geom'] if isinstance(g, bmesh.types.BMFace)])
        me = bpy.data.meshes.new("_tmp")
        tmp.to_mesh(me); tmp.free()
        self.bm.from_mesh(me)
        bpy.data.meshes.remove(me)
        self.uv = self.bm.loops.layers.uv.verify()

    def box(self, size, center, mat, bevel=0.0, rot=(0, 0, 0)):
        t = bmesh.new()
        bmesh.ops.create_cube(t, size=1.0)
        for v in t.verts:
            v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        if bevel > 0:
            bmesh.ops.bevel(t, geom=list(t.edges), offset=min(bevel, min(size) * 0.45),
                            segments=1, affect='EDGES', profile=0.5, clamp_overlap=True)
        m = Matrix.Translation(Vector(center)) @ Euler(rot).to_matrix().to_4x4()
        bmesh.ops.transform(t, matrix=m, verts=t.verts)
        idx = self.mi(mat)
        for f in t.faces:
            f.material_index = idx
        self._merge(t)

    def box_between(self, p0, p1, mat, bevel=0.0):
        c = [(a + b) / 2 for a, b in zip(p0, p1)]
        s = [abs(b - a) for a, b in zip(p0, p1)]
        self.box(s, c, mat, bevel)

    def lathe(self, profile, segs, mat, center=(0, 0, 0), rot=(0, 0, 0), u_rep=1.0,
              v_range=(0.0, 1.0), smooth=True, phase=0.0):
        """profile: [(r, z)] traversed bottom-center -> out -> up -> top-center."""
        t = bmesh.new()
        uvl = t.loops.layers.uv.verify()
        lens = [0.0]
        for i in range(1, len(profile)):
            a, b = profile[i - 1], profile[i]
            lens.append(lens[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
        tot = lens[-1] or 1
        vs = [v_range[0] + (v_range[1] - v_range[0]) * l / tot for l in lens]
        rings = []
        for (r, z) in profile:
            if r < 1e-6:
                c = t.verts.new((0, 0, z)); rings.append([c] * (segs + 1))
            else:
                ring = [t.verts.new((r * math.cos(2 * math.pi * j / segs + phase),
                                     r * math.sin(2 * math.pi * j / segs + phase), z))
                        for j in range(segs)]
                rings.append(ring + [ring[0]])
        idx = self.mi(mat)
        for i in range(len(rings) - 1):
            for j in range(segs):
                quad = [rings[i][j], rings[i][j + 1], rings[i + 1][j + 1], rings[i + 1][j]]
                uvs = [(j / segs * u_rep, vs[i]), ((j + 1) / segs * u_rep, vs[i]),
                       ((j + 1) / segs * u_rep, vs[i + 1]), (j / segs * u_rep, vs[i + 1])]
                vv, uu = [], []
                for q, uvv in zip(quad, uvs):
                    if q not in vv:
                        vv.append(q); uu.append(uvv)
                if len(vv) < 3:
                    continue
                try:
                    f = t.faces.new(vv)
                except ValueError:
                    continue
                f.material_index = idx
                f.smooth = smooth
                for l, uvv in zip(f.loops, uu):
                    l[uvl].uv = uvv
        m = Matrix.Translation(Vector(center)) @ Euler(rot).to_matrix().to_4x4()
        bmesh.ops.transform(t, matrix=m, verts=t.verts)
        self._merge(t)

    def cyl(self, r, h, center, mat, segs=12, rot=(0, 0, 0), cap=True):
        prof = ([(0, -h / 2)] if cap else []) + [(r, -h / 2), (r, h / 2)] + ([(0, h / 2)] if cap else [])
        self.lathe(prof, segs, mat, center, rot, smooth=False)

    def pipe(self, pts, radius, mat, segs=8, fillet=0.12):
        """Polyline tube with rounded corners."""
        P = [Vector(p) for p in pts]
        path = [P[0]]
        for i in range(1, len(P) - 1):
            a, p, b = P[i - 1], P[i], P[i + 1]
            da = (a - p).normalized(); db = (b - p).normalized()
            rf = min(fillet, (a - p).length * 0.45, (b - p).length * 0.45)
            p0 = p + da * rf; p2 = p + db * rf
            for k in range(5):
                s = k / 4
                q = (1 - s) ** 2 * p0 + 2 * (1 - s) * s * p + s * s * p2
                path.append(q)
        path.append(P[-1])
        t = bmesh.new()
        rings = []
        prev_n = None
        for i, p in enumerate(path):
            if i == 0:
                d = (path[1] - p).normalized()
            elif i == len(path) - 1:
                d = (p - path[i - 1]).normalized()
            else:
                d = ((path[i + 1] - p).normalized() + (p - path[i - 1]).normalized()).normalized()
            if prev_n is None:
                up = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))
                n = d.cross(up).normalized()
            else:
                n = (prev_n - d * prev_n.dot(d)).normalized()
            prev_n = n
            bn = d.cross(n)
            rings.append([t.verts.new(p + radius * (math.cos(2 * math.pi * j / segs) * n +
                                                    math.sin(2 * math.pi * j / segs) * bn))
                          for j in range(segs)])
        idx = self.mi(mat)
        for i in range(len(rings) - 1):
            for j in range(segs):
                f = t.faces.new((rings[i][j], rings[i][(j + 1) % segs],
                                 rings[i + 1][(j + 1) % segs], rings[i + 1][j]))
                f.material_index = idx; f.smooth = True
        for ring in (rings[0], rings[-1]):
            f = t.faces.new(ring); f.material_index = idx
        bmesh.ops.recalc_face_normals(t, faces=t.faces)
        self._merge(t)

    def finish(self, name, loc=(0, 0, 0), rot=(0, 0, 0), parent=None, collection=None,
               uv_seed=0):
        bm = self.bm
        bm.normal_update()
        uv = bm.loops.layers.uv.verify()
        rnd = random.Random(uv_seed)
        for f in bm.faces:
            mname = self.mats[f.material_index]
            mode = UVMODE.get(mname, ("box", 1.0))
            if mode == "keep":
                continue
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))

            def proj(co):
                if ax == 0:
                    return (co.y if n.x > 0 else -co.y), co.z
                if ax == 1:
                    return (-co.x if n.y > 0 else co.x), co.z
                return co.x, (co.y if n.z > 0 else -co.y)
            pts = [proj(l.vert.co) for l in f.loops]
            if mode == "fit":
                us = [p[0] for p in pts]; vs = [p[1] for p in pts]
                du = (max(us) - min(us)) or 1; dv = (max(vs) - min(vs)) or 1
                for l, p in zip(f.loops, pts):
                    l[uv].uv = ((p[0] - min(us)) / du, (p[1] - min(vs)) / dv)
            else:
                tile = mode[1]
                for l, p in zip(f.loops, pts):
                    l[uv].uv = (p[0] / tile, p[1] / tile)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me); bm.free()
        for m in self.mats:
            me.materials.append(MATS[m])
        ob = bpy.data.objects.new(name, me)
        (collection or scene.collection).objects.link(ob)
        ob.location = loc
        ob.rotation_euler = rot
        if parent:
            ob.parent = parent
        return ob


def link_col(name, parent=None):
    c = bpy.data.collections.new(name)
    (parent or scene.collection).children.link(c)
    return c


COL = link_col("Workshop")
root = bpy.data.objects.new("Workshop", None)
COL.objects.link(root)

# ------------------------------------------------------------------ dimensions
W, D = 6.0, 4.4                     # outer footprint (x, y)
T = 0.30                            # wall thickness
X0, X1, Y0, Y1 = -W / 2, W / 2, -D / 2, D / 2
ZF = 0.10                           # interior floor top
ZW = 3.00                           # wall top
ZBAND = 2.42                        # red band bottom
ZROOF = 2.75                        # roof top surface
GARAGE = (-1.50, 2.10, ZF, 2.30)    # x0, x1, z0, z1
WIN_L = (-0.55, 0.25, 1.30, 1.88)   # y0, y1, z0, z1 on -X wall
WIN_R = (-0.95, 0.55, 1.22, 1.82)   # y0, y1, z0, z1 on +X wall

# ------------------------------------------------------------------ building


def wall_x(b, y0, y1, xa, xb, z0, z1, holes, mat):
    """Wall running along X (front/back) from xa..xb with rectangular holes (hx0,hx1,hz0,hz1)."""
    cur = xa
    for (hx0, hx1, hz0, hz1) in sorted(holes):
        b.box_between((cur, y0, z0), (hx0, y1, z1), mat)
        if hz0 > z0:
            b.box_between((hx0, y0, z0), (hx1, y1, hz0), mat)
        if hz1 < z1:
            b.box_between((hx0, y0, hz1), (hx1, y1, z1), mat)
        cur = hx1
    b.box_between((cur, y0, z0), (xb, y1, z1), mat)


def wall_y(b, x0, x1, ya, yb, z0, z1, holes, mat):
    cur = ya
    for (hy0, hy1, hz0, hz1) in sorted(holes):
        b.box_between((x0, cur, z0), (x1, hy0, z1), mat)
        if hz0 > z0:
            b.box_between((x0, hy0, z0), (x1, hy1, hz0), mat)
        if hz1 < z1:
            b.box_between((x0, hy0, hz1), (x1, hy1, z1), mat)
        cur = hy1
    b.box_between((x0, cur, z0), (x1, yb, z1), mat)


b = B()
# walls
wall_x(b, Y0, Y0 + T, X0, X1, 0, ZW, [GARAGE], "M_StoneWall")              # front
wall_x(b, Y1 - T, Y1, X0, X1, 0, ZW, [], "M_StoneWall")                    # back
wall_y(b, X0, X0 + T, Y0 + T, Y1 - T, 0, ZW, [WIN_L], "M_StoneWall")      # left
wall_y(b, X1 - T, X1, Y0 + T, Y1 - T, 0, ZW, [WIN_R], "M_StoneWall")      # right

# interior faces -> plaster (below roof)
b.bm.normal_update()
iw = b.mi("M_Plaster"); sw = b.mats.index("M_StoneWall")
for f in b.bm.faces:
    if f.material_index != sw:
        continue
    c = f.calc_center_median()
    if (abs(c.x) <= W / 2 - T + 1e-3 and abs(c.y) <= D / 2 - T + 1e-3 and c.z < ZROOF
            and f.normal.dot(Vector((c.x, c.y, 0))) < 0):
        f.material_index = iw

# red band (protrudes 4cm) with thin gray ledge under it
PB = 0.04
for (p0, p1) in [((X0 - PB, Y0 - PB, ZBAND), (X1 + PB, Y0, ZW)),
                 ((X0 - PB, Y1, ZBAND), (X1 + PB, Y1 + PB, ZW)),
                 ((X0 - PB, Y0, ZBAND), (X0, Y1, ZW)),
                 ((X1, Y0, ZBAND), (X1 + PB, Y1, ZW))]:
    b.box_between(p0, p1, "M_RedBand")
PL = 0.07
for (p0, p1) in [((X0 - PL, Y0 - PL, ZBAND - 0.07), (X1 + PL, Y0, ZBAND)),
                 ((X0 - PL, Y1, ZBAND - 0.07), (X1 + PL, Y1 + PL, ZBAND)),
                 ((X0 - PL, Y0, ZBAND - 0.07), (X0, Y1, ZBAND)),
                 ((X1, Y0, ZBAND - 0.07), (X1 + PL, Y1, ZBAND))]:
    b.box(( (p1[0]-p0[0]), (p1[1]-p0[1]), (p1[2]-p0[2]) ),
          ((p0[0]+p1[0])/2, (p0[1]+p1[1])/2, (p0[2]+p1[2])/2), "M_StoneGray", bevel=0.015)

# plinth (skip garage opening)
PP, ZP = 0.05, 0.38
for (p0, p1) in [((X0 - PP, Y0 - PP, 0), (GARAGE[0], Y0, ZP)),
                 ((GARAGE[1], Y0 - PP, 0), (X1 + PP, Y0, ZP)),
                 ((X0 - PP, Y1, 0), (X1 + PP, Y1 + PP, ZP)),
                 ((X0 - PP, Y0, 0), (X0, Y1, ZP)),
                 ((X1, Y0, 0), (X1 + PP, Y1, ZP))]:
    s = [p1[i] - p0[i] for i in range(3)]
    b.box(s, [(p0[i] + p1[i]) / 2 for i in range(3)], "M_StoneGray", bevel=0.02)

# corner quoins: slightly protruding stone blocks on the 4 vertical edges
for cx, cy, sx, sy in [(X0, Y0, -1, -1), (X1, Y0, 1, -1), (X0, Y1, -1, 1), (X1, Y1, 1, 1)]:
    z = ZP + 0.02
    k = 0
    while z < ZBAND - 0.35:
        h = random.uniform(0.30, 0.40)
        lx = 0.34 if k % 2 == 0 else 0.22
        ly = 0.22 if k % 2 == 0 else 0.34
        b.box((lx, ly, h - 0.02), (cx - sx * lx / 2 + sx * 0.02, cy - sy * ly / 2 + sy * 0.02, z + h / 2),
              "M_StoneWall", bevel=0.025)
        z += h; k += 1

# parapet cap blocks
CO, CI, ZC = 0.10, 0.05, 0.24


def cap_run(axis, fixed, a, bnd, inward):
    pos = a
    while pos < bnd - 0.05:
        L = min(random.uniform(0.75, 1.15), bnd - pos)
        if bnd - pos - L < 0.35:
            L = bnd - pos
        h = ZC + random.uniform(-0.012, 0.012)
        depth = T + CO + CI
        if axis == 'x':
            cy = fixed + inward * (depth / 2 - CO)
            b.box((L - 0.02, depth, h), (pos + L / 2, cy, ZW + h / 2), "M_StoneGray", bevel=0.03)
        else:
            cx = fixed + inward * (depth / 2 - CO)
            b.box((depth, L - 0.02, h), (cx, pos + L / 2, ZW + h / 2), "M_StoneGray", bevel=0.03)
        pos += L


cap_run('x', Y0, X0 - CO, X1 + CO, 1)
cap_run('x', Y1, X0 - CO, X1 + CO, -1)
cap_run('y', X0, Y0 - CO + (T + CO + CI), Y1 + CO - (T + CO + CI), 1)
cap_run('y', X1, Y0 - CO + (T + CO + CI), Y1 + CO - (T + CO + CI), -1)

# floor slab, roof slab
b.box_between((X0 + 0.01, Y0 + 0.01, -0.02), (X1 - 0.01, Y1 - 0.01, ZF), "M_Floor")
b.box_between((X0 + T - 0.01, Y0 + T - 0.01, ZROOF - 0.18), (X1 - T + 0.01, Y1 - T + 0.01, ZROOF), "M_Roof")

# garage frame (dark metal)
gx0, gx1, _, gz1 = GARAGE
for gx in (gx0, gx1):
    b.box((0.16, T + 0.08, gz1 - ZF + 0.1), (gx, Y0 + T / 2 - 0.02, ZF + (gz1 - ZF + 0.1) / 2), "M_MetalDark", bevel=0.015)
b.box((gx1 - gx0 + 0.16, T + 0.08, 0.14), ((gx0 + gx1) / 2, Y0 + T / 2 - 0.02, gz1 + 0.05), "M_MetalDark", bevel=0.015)
# roller shutter housing inside + partially raised shutter
b.box((gx1 - gx0 - 0.1, 0.34, 0.30), ((gx0 + gx1) / 2, Y0 + T + 0.17, gz1 - 0.12), "M_MetalDark", bevel=0.03)
for i in range(3):
    b.box((gx1 - gx0 - 0.2, 0.03, 0.075), ((gx0 + gx1) / 2, Y0 + T + 0.04, gz1 - 0.02 - 0.08 * i), "M_MetalLight", bevel=0.008)
# threshold strip
b.box((gx1 - gx0, 0.10, 0.025), ((gx0 + gx1) / 2, Y0 + 0.06, ZF + 0.005), "M_MetalLight", bevel=0.005)

# sign board
SX, SWID, SZ0, SZ1 = 0.30, 3.90, 2.44, 3.16
SY_BACK, SY_FRONT = Y0 - 0.14, Y0 - 0.20
b.box((SWID, SY_BACK - SY_FRONT, SZ1 - SZ0), (SX, (SY_BACK + SY_FRONT) / 2, (SZ0 + SZ1) / 2), "M_Sign")
fr = 0.06
for (sx, sz, px, pz) in [(SWID + 2 * fr, fr, SX, SZ1 + fr / 2), (SWID + 2 * fr, fr, SX, SZ0 - fr / 2),
                         (fr, SZ1 - SZ0, SX - SWID / 2 - fr / 2, (SZ0 + SZ1) / 2),
                         (fr, SZ1 - SZ0, SX + SWID / 2 + fr / 2, (SZ0 + SZ1) / 2)]:
    b.box((sx, 0.09, sz), (px, SY_FRONT + 0.035, pz), "M_MetalDark", bevel=0.012)
for bx in (SX - 1.4, SX + 1.4):
    for bz in (SZ0 + 0.15, SZ1 - 0.15):
        b.box((0.08, Y0 - PB - SY_BACK + 0.02, 0.08), (bx, (SY_BACK + Y0 - PB) / 2, bz), "M_MetalDark")

# wall lamps on both sides of the sign


def wall_lamp(b, x, z):
    yw = Y0 - PB
    ly = Y0 - 0.46
    b.box((0.16, 0.05, 0.24), (x, yw - 0.025, z + 0.35), "M_MetalDark", bevel=0.01)       # wall plate
    b.pipe([(x, yw - 0.02, z + 0.40), (x, ly, z + 0.40), (x, ly, z + 0.30)], 0.022, "M_MetalDark", segs=6, fillet=0.1)
    b.lathe([(0, 0), (0.07, 0), (0.08, 0.03), (0.075, 0.04)], 10, "M_MetalDark", (x, ly, z - 0.02))
    b.lathe([(0.075, 0.0), (0.085, 0.22)], 10, "M_LampGlow", (x, ly, z + 0.02), smooth=False)
    b.lathe([(0, 0.20), (0.075, 0.20), (0.13, 0.22), (0.12, 0.25), (0.05, 0.31), (0.02, 0.33), (0, 0.34)],
            10, "M_MetalDark", (x, ly, z + 0.02))
    for k in range(4):   # cage bars
        a = k * math.pi / 2 + math.pi / 4
        b.box((0.012, 0.012, 0.22), (x + 0.088 * math.cos(a), ly + 0.088 * math.sin(a), z + 0.13), "M_MetalDark")


LAMPS = [(SX - SWID / 2 - 0.40, 2.12), (SX + SWID / 2 + 0.40, 2.12)]
for lx, lz in LAMPS:
    wall_lamp(b, lx, lz)

# drain pipes
PR = 0.055
b.pipe([(X0 + 0.55, Y0 + 0.55, ZROOF + 0.06), (X0 + 0.55, Y0 + 0.55, ZW + 0.36),
        (X0 + 0.55, Y0 - 0.14, ZW + 0.36), (X0 + 0.55, Y0 - 0.14, 0.40), (X0 + 0.55, Y0 - 0.40, 0.14)],
       PR, "M_MetalDark", segs=8, fillet=0.16)
b.pipe([(0.9, -0.2, ZROOF + 0.14), (X1 - 0.55, -0.2, ZROOF + 0.14), (X1 - 0.55, -1.35, ZROOF + 0.14),
        (X1 - 0.55, -1.35, ZW + 0.36), (X1 + 0.14, -1.35, ZW + 0.36), (X1 + 0.14, -1.35, 0.40),
        (X1 + 0.40, -1.35, 0.14)], PR, "M_MetalDark", segs=8, fillet=0.16)
for z in (0.9, 1.7):
    b.box((0.16, 0.06, 0.05), (X0 + 0.55, Y0 - 0.03 - PR, z), "M_MetalDark")
    b.box((0.06, 0.16, 0.05), (X1 + 0.03 + PR, -1.35, z), "M_MetalDark")
b.cyl(0.075, 0.1, (X0 + 0.55, Y0 - 0.14, ZW + 0.28), "M_MetalDark", segs=8)
# pipe clamps on roof run
for x in (1.6, 2.2):
    b.box((0.06, 0.16, 0.10), (x, -0.2, ZROOF + 0.05), "M_MetalDark")


def window(b, side, y0, y1, z0, z1, mullions):
    sgn = -1 if side < 0 else 1
    xo = sgn * W / 2               # outer face x
    xm = sgn * (W / 2 - T / 2)     # glass plane
    fw, fp = 0.09, 0.05
    # frame around the hole on the outer face
    cx = xo + sgn * fp / 2
    b.box((fp, y1 - y0 + 2 * fw, fw), (cx, (y0 + y1) / 2, z1 + fw / 2), "M_StoneGray", bevel=0.012)
    b.box((fp, fw, z1 - z0), (cx, y0 - fw / 2, (z0 + z1) / 2), "M_StoneGray", bevel=0.012)
    b.box((fp, fw, z1 - z0), (cx, y1 + fw / 2, (z0 + z1) / 2), "M_StoneGray", bevel=0.012)
    b.box((0.14, y1 - y0 + 0.3, 0.08), (xo + sgn * 0.05, (y0 + y1) / 2, z0 - 0.04), "M_StoneGray", bevel=0.015)
    # glass + dark mullions
    b.box((0.02, y1 - y0, z1 - z0), (xm, (y0 + y1) / 2, (z0 + z1) / 2), "M_Glass")
    b.box((0.05, y1 - y0, 0.04), (xm + sgn * 0.02, (y0 + y1) / 2, z0 + 0.02), "M_MetalDark")
    b.box((0.05, y1 - y0, 0.04), (xm + sgn * 0.02, (y0 + y1) / 2, z1 - 0.02), "M_MetalDark")
    b.box((0.05, 0.04, z1 - z0), (xm + sgn * 0.02, y0 + 0.02, (z0 + z1) / 2), "M_MetalDark")
    b.box((0.05, 0.04, z1 - z0), (xm + sgn * 0.02, y1 - 0.02, (z0 + z1) / 2), "M_MetalDark")
    for k in range(1, mullions + 1):
        yy = y0 + (y1 - y0) * k / (mullions + 1)
        b.box((0.05, 0.035, z1 - z0), (xm + sgn * 0.02, yy, (z0 + z1) / 2), "M_MetalDark")
    b.box((0.05, y1 - y0, 0.035), (xm + sgn * 0.02, (y0 + y1) / 2, (z0 + z1) / 2), "M_MetalDark")


window(b, -1, *WIN_L, 0)
window(b, 1, *WIN_R, 1)

# back wall AC unit
acx, acz = -1.5, 1.95
b.box((0.80, 0.36, 0.56), (acx, Y1 + 0.18 + 0.01, acz), "M_MetalLight", bevel=0.025)
b.box((0.56, 0.02, 0.46), (acx - 0.05, Y1 + 0.37, acz), "M_Fan")
b.box((0.12, 0.02, 0.40), (acx + 0.31, Y1 + 0.37, acz), "M_Vent")
for sx in (-0.3, 0.3):
    b.box((0.05, 0.30, 0.05), (acx + sx, Y1 + 0.15, acz - 0.31), "M_MetalDark")
b.pipe([(acx + 0.3, Y1 + 0.2, acz - 0.28), (acx + 0.3, Y1 + 0.06, acz - 0.6), (acx + 0.3, Y1 + 0.06, 0.45)],
       0.02, "M_MetalDark", segs=6)

# roof props: corner vent box, AC unit with fan, small box, chimney
vx, vy = X0 + 0.22, Y0 + 0.22
b.box((0.46, 0.46, 0.46), (vx, vy, ZW + ZC + 0.23), "M_StoneWall", bevel=0.03)
b.box((0.56, 0.56, 0.08), (vx, vy, ZW + ZC + 0.50), "M_StoneGray", bevel=0.02)
b.box((0.30, 0.02, 0.16), (vx, vy - 0.235, ZW + ZC + 0.26), "M_Vent")
b.box((0.02, 0.30, 0.16), (vx - 0.235, vy, ZW + ZC + 0.26), "M_Vent")

b.box((0.95, 0.75, 0.42), (-1.1, 0.35, ZROOF + 0.25), "M_MetalLight", bevel=0.03)
b.box((0.62, 0.62, 0.02), (-1.1, 0.35, ZROOF + 0.465), "M_Fan")
for sx in (-0.4, 0.4):
    b.box((0.06, 0.7, 0.05), (-1.1 + sx, 0.35, ZROOF + 0.025), "M_MetalDark")
b.box((0.40, 0.40, 0.32), (0.75, -0.2, ZROOF + 0.16), "M_MetalLight", bevel=0.02)
b.box((0.22, 0.02, 0.14), (0.75, -0.41, ZROOF + 0.18), "M_Vent")
cx, cy = 1.35, 1.45
b.lathe([(0, 0), (0.22, 0), (0.22, 0.06), (0.14, 0.10), (0.14, 1.05), (0.19, 1.05), (0.19, 1.12),
         (0.24, 1.16), (0.24, 1.20), (0.12, 1.30), (0.0, 1.32)], 12, "M_MetalDark", (cx, cy, ZROOF), smooth=False)
b.cyl(0.16, 0.05, (cx, cy, ZROOF + 0.55), "M_MetalDark", segs=12)
# hatch
b.box((0.7, 0.6, 0.08), (-1.6, -1.1, ZROOF + 0.04), "M_MetalDark", bevel=0.02)
b.box((0.5, 0.05, 0.03), (-1.6, -1.35, ZROOF + 0.09), "M_MetalLight")

# pegboard + shelf on interior back wall
PBX, PBZ = 0.85, 1.62
b.box((1.70, 0.04, 0.86), (PBX, Y1 - T - 0.02, PBZ), "M_Pegboard")
b.box((1.80, 0.05, 0.94), (PBX, Y1 - T - 0.005, PBZ), "M_Wood", bevel=0.01)
b.box((1.4, 0.26, 0.04), (PBX, Y1 - T - 0.13, 2.22), "M_Wood", bevel=0.008)
for sx in (-0.6, 0.6):
    b.box((0.03, 0.2, 0.12), (PBX + sx, Y1 - T - 0.1, 2.15), "M_MetalDark")
for k, (dx, sz) in enumerate([(-0.45, 0.16), (-0.2, 0.2), (0.25, 0.14)]):
    b.cyl(sz / 2 * 0.8, sz, (PBX + dx, Y1 - T - 0.13, 2.24 + sz / 2), "M_MetalRed" if k != 1 else "M_MetalLight", segs=10)
b.box((0.3, 0.2, 0.18), (PBX + 0.5, Y1 - T - 0.13, 2.33), "M_Cardboard", bevel=0.01)

# ceiling lamp
clx, cly = 0.3, 0.4
b.cyl(0.008, 0.36, (clx, cly, ZROOF - 0.18 - 0.18), "M_MetalDark", segs=6)
b.lathe([(0.03, 0.0), (0.05, 0.02), (0.20, 0.16), (0.21, 0.18), (0.035, 0.05), (0.02, 0.0)],
        12, "M_MetalDark", (clx, cly, ZROOF - 0.18 - 0.36), rot=(math.pi, 0, 0))
b.lathe([(0, -0.06), (0.05, -0.04), (0.055, 0.0), (0, 0.02)], 10, "M_LampGlow", (clx, cly, ZROOF - 0.18 - 0.50))

building = b.finish("Workshop_Building", parent=root, collection=COL)

# ------------------------------------------------------------------ ground / pavers
g = B()
g.box_between((-4.05, -3.55, -0.16), (4.05, 3.05, 0.0), "M_Soil", bevel=0.04)


def slabs(x0, x1, y0, y1, sw, sd, skip=0.06):
    y = y0
    row = 0
    while y < y1 - 0.05:
        d_ = min(sd, y1 - y)
        x = x0 - (sw * 0.5 if row % 2 else 0)
        while x < x1 - 0.05:
            xa, xb = max(x, x0), min(x + sw, x1)
            if xb - xa > 0.15 and random.random() > skip:
                h = random.uniform(0.07, 0.11)
                g.box((xb - xa - 0.05, d_ - 0.05, h),
                      ((xa + xb) / 2 + random.uniform(-0.01, 0.01), y + d_ / 2, h / 2 - 0.02),
                      "M_Paver", bevel=0.025, rot=(random.uniform(-0.02, 0.02), random.uniform(-0.02, 0.02),
                                                    random.uniform(-0.03, 0.03)))
            x += sw
        y += d_
        row += 1


slabs(-3.95, 3.95, -3.45, Y0 - 0.02, 0.92, 0.62)          # front apron
slabs(-3.95, X0 - 0.06, Y0 - 0.02, 2.95, 0.9, 0.72, 0.1)   # left strip
slabs(X1 + 0.06, 3.95, Y0 - 0.02, 2.95, 0.9, 0.72, 0.1)    # right strip
slabs(X0 - 0.06, X1 + 0.06, Y1 + 0.04, 2.95, 0.95, 0.72, 0.12)  # back strip


def rock(bb, c, r, mat="M_Rock"):
    t = bmesh.new()
    bmesh.ops.create_icosphere(t, subdivisions=1, radius=r)
    for v in t.verts:
        v.co = Vector((v.co.x * random.uniform(0.8, 1.3), v.co.y * random.uniform(0.8, 1.3),
                       v.co.z * random.uniform(0.5, 0.8)))
    bmesh.ops.translate(t, vec=Vector(c), verts=t.verts)
    idx = bb.mi(mat)
    for f in t.faces:
        f.material_index = idx
    bb._merge(t)


for c, r in [((-3.85, -3.2, 0.05), 0.12), ((-3.7, -3.35, 0.03), 0.07), ((3.85, -3.3, 0.04), 0.1),
             ((3.9, 2.8, 0.05), 0.13), ((-3.9, 2.6, 0.04), 0.1), ((1.0, -3.45, 0.03), 0.08),
             ((3.7, 0.9, 0.05), 0.09)]:
    rock(g, c, r)
ground = g.finish("Workshop_Ground", parent=root, collection=COL)

# ------------------------------------------------------------------ props (local space, origin at base)


def p_tire(b, c=(0, 0, 0), rot=(0, 0, 0)):
    # simple chamfered ring: no rim/inner detail (game camera is top-down)
    R, r_in, w = 0.36, 0.18, 0.24
    prof = [(r_in, 0.0), (R - 0.04, 0.0), (R, 0.04), (R, w - 0.04), (R - 0.04, w), (r_in, w), (r_in, 0.0)]
    b.lathe(prof, 10, "M_Tire", c, rot, u_rep=2.0)


def p_tire_stack(b, n=3, c=(0, 0, 0)):
    z = 0.0
    for i in range(n):
        p_tire(b, (c[0] + random.uniform(-0.04, 0.04), c[1] + random.uniform(-0.04, 0.04), c[2] + z),
               (random.uniform(-0.03, 0.03), random.uniform(-0.03, 0.03), random.uniform(0, 3)))
        z += 0.235


def p_barrel(b, c=(0, 0, 0)):
    r, h = 0.30, 0.88
    prof = [(0, 0.0), (r - 0.02, 0.0), (r, 0.02), (r, 0.27), (r + 0.015, 0.29), (r, 0.31), (r, 0.57),
            (r + 0.015, 0.59), (r, 0.61), (r, h - 0.02), (r - 0.01, h), (r - 0.03, h), (r - 0.03, h - 0.03),
            (0, h - 0.03)]
    b.lathe(prof, 12, "M_Barrel", c, u_rep=2.0, v_range=(0.0, 1.0))
    b.cyl(0.035, 0.03, (c[0] + 0.14, c[1], c[2] + h - 0.015), "M_MetalDark", segs=8)


def p_cardboard(b, size, c, rz=0.0):
    b.box(size, (c[0], c[1], c[2] + size[2] / 2), "M_Cardboard", bevel=0.012, rot=(0, 0, rz))


def p_crate(b, s=0.6, c=(0, 0, 0), rz=0.0):
    b.box((s - 0.04, s - 0.04, s - 0.04), (c[0], c[1], c[2] + s / 2), "M_Wood", bevel=0.01, rot=(0, 0, rz))
    ca, sa = math.cos(rz), math.sin(rz)
    for sx in (-1, 1):
        for sy in (-1, 1):
            ox, oy = sx * (s / 2 - 0.03), sy * (s / 2 - 0.03)
            b.box((0.07, 0.07, s), (c[0] + ox * ca - oy * sa, c[1] + ox * sa + oy * ca, c[2] + s / 2), "M_Wood",
                  bevel=0.01, rot=(0, 0, rz))
    for z in (0.04, s - 0.04):
        for sy in (-1, 1):
            oy = sy * (s / 2 - 0.02)
            b.box((s, 0.045, 0.08), (c[0] - oy * sa, c[1] + oy * ca, c[2] + z), "M_Wood", bevel=0.008, rot=(0, 0, rz))
            b.box((0.045, s, 0.08), (c[0] + oy * ca, c[1] + oy * sa, c[2] + z), "M_Wood", bevel=0.008, rot=(0, 0, rz))


def p_fence(b, length=1.5, h=1.1, c=(0, 0, 0), rz=0.0):
    """Wooden fence panel running along local X."""
    ca, sa = math.cos(rz), math.sin(rz)

    def P(x, y, z):
        return (c[0] + x * ca - y * sa, c[1] + x * sa + y * ca, c[2] + z)
    for x in (-length / 2 + 0.05, length / 2 - 0.05):
        b.box((0.09, 0.09, h + 0.1), P(x, 0.03, (h + 0.1) / 2), "M_Wood", bevel=0.012,
              rot=(random.uniform(-0.04, 0.04), random.uniform(-0.04, 0.04), rz))
    n = 5
    for i in range(n):
        z = 0.14 + i * (h - 0.14) / n + 0.08
        b.box((length, 0.035, 0.15), P(random.uniform(-0.03, 0.03), -0.03, z), "M_Wood", bevel=0.008,
              rot=(0, random.uniform(-0.05, 0.05), rz))
    b.box((0.08, 0.03, h * 1.02), P(0, -0.07, h / 2 + 0.05), "M_Wood", bevel=0.008, rot=(0, 0.85, rz))


def p_cabinet(b, c=(0, 0, 0)):
    w, d_, h = 0.72, 0.50, 0.82
    x, y, z = c
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.06, 0.06, 0.06), (x + sx * 0.28, y + sy * 0.19, z + 0.03), "M_MetalDark")
    b.box((w, d_, h), (x, y, z + 0.06 + h / 2), "M_MetalRed", bevel=0.015)
    hs = [0.14, 0.16, 0.20, 0.26]
    zz = z + 0.06 + h - 0.03
    for hh in hs:
        b.box((w - 0.06, 0.02, hh - 0.025), (x, y - d_ / 2 - 0.01, zz - hh / 2), "M_MetalRed", bevel=0.006)
        b.box((0.32, 0.025, 0.025), (x, y - d_ / 2 - 0.03, zz - 0.045), "M_MetalLight", bevel=0.005)
        zz -= hh
    # top chest
    tz = z + 0.06 + h
    b.box((w - 0.04, d_ - 0.04, 0.30), (x, y, tz + 0.15), "M_MetalRed", bevel=0.012)
    b.box((w, d_, 0.06), (x, y, tz + 0.33), "M_MetalRed", bevel=0.02)
    for k in range(2):
        b.box((w - 0.1, 0.02, 0.10), (x, y - (d_ - 0.04) / 2 - 0.01, tz + 0.08 + k * 0.13), "M_MetalRed", bevel=0.005)
        b.box((0.22, 0.02, 0.02), (x, y - (d_ - 0.04) / 2 - 0.03, tz + 0.11 + k * 0.13), "M_MetalLight")
    b.box((0.26, 0.04, 0.04), (x, y, tz + 0.38), "M_MetalDark")
    for sx in (-1, 1):
        b.box((0.02, 0.04, 0.2), (x + sx * (w / 2 + 0.015), y, z + 0.06 + h - 0.15), "M_MetalLight")


def p_workbench(b, c=(0, 0, 0)):
    L, Dp, H = 1.9, 0.72, 0.90
    x, y, z = c
    b.box((L, Dp, 0.07), (x, y, z + H - 0.035), "M_Wood", bevel=0.012)
    for sx in (-1, 1):
        for sy in (-1, 1):
            b.box((0.08, 0.08, H - 0.07), (x + sx * (L / 2 - 0.08), y + sy * (Dp / 2 - 0.08), z + (H - 0.07) / 2),
                  "M_Wood", bevel=0.01)
        b.box((0.06, Dp - 0.16, 0.08), (x + sx * (L / 2 - 0.08), y, z + 0.18), "M_Wood", bevel=0.008)
    b.box((L - 0.1, Dp - 0.1, 0.04), (x, y, z + 0.26), "M_Wood", bevel=0.008)
    b.box((L - 0.16, 0.05, 0.12), (x, y - Dp / 2 + 0.05, z + H - 0.13), "M_Wood", bevel=0.008)
    # vise
    vx = x - L / 2 + 0.25
    b.box((0.2, 0.18, 0.08), (vx, y - Dp / 2 + 0.12, z + H + 0.04), "M_MetalDark", bevel=0.01)
    b.box((0.18, 0.05, 0.1), (vx, y - Dp / 2 + 0.04, z + H + 0.09), "M_MetalDark", bevel=0.008)
    b.box((0.18, 0.05, 0.1), (vx, y - Dp / 2 + 0.16, z + H + 0.09), "M_MetalDark", bevel=0.008)
    b.cyl(0.012, 0.3, (vx, y - Dp / 2 - 0.02, z + H + 0.07), "M_MetalLight", segs=6, rot=(0, math.pi / 2, 0))
    # items on top
    b.box((0.42, 0.22, 0.16), (x + 0.1, y + 0.12, z + H + 0.08), "M_MetalRed", bevel=0.012)
    b.box((0.36, 0.03, 0.03), (x + 0.1, y + 0.12, z + H + 0.18), "M_MetalDark")
    b.cyl(0.07, 0.14, (x + 0.55, y - 0.05, z + H + 0.07), "M_MetalLight", segs=10)
    b.box((0.3, 0.25, 0.2), (x + 0.72, y + 0.15, z + H + 0.1), "M_Cardboard", bevel=0.01)
    b.box((0.25, 0.04, 0.02), (x - 0.3, y - 0.15, z + H + 0.01), "M_MetalDark", rot=(0, 0, 0.4))
    # under the bench
    b.box((0.45, 0.4, 0.3), (x - 0.45, y, z + 0.28 + 0.15), "M_Cardboard", bevel=0.01)
    b.box((0.5, 0.45, 0.35), (x + 0.4, y, z + 0.28 + 0.175), "M_Wood", bevel=0.015)


def p_leafcluster(b, c, radius, height, n, seed, leaf_len=(0.18, 0.3), variant=None):
    rnd = random.Random(seed)
    t = bmesh.new()
    uvl = t.loops.layers.uv.verify()
    idx = b.mi("M_Leaf")
    for i in range(n):
        a = rnd.uniform(0, 2 * math.pi)
        rr = radius * math.sqrt(rnd.random()) * 0.35
        zz = rnd.uniform(0.0, height * 0.45)
        base = Vector((c[0] + rr * math.cos(a), c[1] + rr * math.sin(a), c[2] + zz))
        out = Vector((math.cos(a), math.sin(a), 0))
        d = (out * rnd.uniform(0.4, 1.0) + Vector((0, 0, rnd.uniform(0.35, 1.3)))).normalized()
        L = rnd.uniform(*leaf_len)
        side = d.cross(Vector((0, 0, 1)))
        if side.length < 1e-3:
            side = Vector((1, 0, 0))
        side.normalize()
        nrm = side.cross(d).normalized()
        wv = L * 0.62
        tip = base + d * L - nrm * L * 0.18 - Vector((0, 0, L * 0.12))
        s1 = base + d * L * 0.45 + side * wv / 2 - nrm * 0.03
        s2 = base + d * L * 0.45 - side * wv / 2 - nrm * 0.03
        v = [t.verts.new(p) for p in (base, s1, tip, s2)]
        var = variant if variant is not None else rnd.randint(0, 1)
        u0 = var * 0.5
        uvs = [(u0 + 0.25, 0.0), (u0 + 0.0, 0.45), (u0 + 0.25, 1.0), (u0 + 0.5, 0.45)]
        for tri in ((0, 1, 2), (0, 2, 3)):
            try:
                f = t.faces.new([v[k] for k in tri])
            except ValueError:
                continue
            f.material_index = idx; f.smooth = True
            for l, k in zip(f.loops, tri):
                l[uvl].uv = uvs[k]
    b._merge(t, double_sided=True)


def p_grass(b, c, n, h, seed, spread=0.12):
    rnd = random.Random(seed)
    t = bmesh.new()
    uvl = t.loops.layers.uv.verify()
    idx = b.mi("M_Grass")
    for i in range(n):
        a = rnd.uniform(0, 2 * math.pi)
        base = Vector((c[0] + rnd.uniform(-spread, spread) * 0.4, c[1] + rnd.uniform(-spread, spread) * 0.4, c[2]))
        out = Vector((math.cos(a), math.sin(a), 0))
        hh = h * rnd.uniform(0.6, 1.1)
        bend = rnd.uniform(0.15, 0.5)
        side = Vector((-out.y, out.x, 0))
        w = rnd.uniform(0.035, 0.055)
        pts = []
        for k in range(4):
            s = k / 3
            p = base + out * (bend * hh * s * s) + Vector((0, 0, hh * s * (1 - 0.25 * s)))
            ww = w * (1 - s) + 0.002
            pts.append((t.verts.new(p + side * ww / 2), t.verts.new(p - side * ww / 2), s))
        for k in range(3):
            a1, b1, s0 = pts[k]; a2, b2, s1 = pts[k + 1]
            for quad, uvq in (((a1, b1, b2, a2), ((0, s0), (1, s0), (1, s1), (0, s1))),):
                f = t.faces.new(quad); f.material_index = idx; f.smooth = True
                for l, uv_ in zip(f.loops, uvq):
                    l[uvl].uv = uv_
    b._merge(t, double_sided=True)


def p_bush(b, c, radius, height, seed, leaves=38):
    p_leafcluster(b, c, radius, height, leaves, seed, (0.22, 0.38))
    p_grass(b, c, 6, height * 0.5, seed + 1, spread=radius)


# ---- place props (each prop is its own object, origin at its base)
PROPS = []


def place(name, fn, loc, rz=0.0, **kw):
    pb_ = B()
    fn(pb_, **kw)
    ob = pb_.finish(name, loc=loc, rot=(0, 0, rz), parent=root, collection=COL)
    PROPS.append(ob)
    return ob


place("Prop_Cabinet", p_cabinet, (-0.95, 1.65, ZF), 0.0)
place("Prop_Workbench", p_workbench, (0.85, 1.62, ZF), 0.0)
place("Prop_TireStack_In", lambda bb: p_tire_stack(bb, 3), (2.25, -1.05, ZF))
place("Prop_Crate_In", lambda bb: p_crate(bb, 0.5), (-2.2, 1.55, ZF), 0.1)
place("Prop_Cardboard_In", lambda bb: (p_cardboard(bb, (0.5, 0.45, 0.4), (0, 0, 0), 0.0),
                                       p_cardboard(bb, (0.36, 0.34, 0.28), (0.02, 0, 0.4), 0.3)),
      (-2.3, 0.4, ZF), 0.2)
place("Prop_TireStack_Out", lambda bb: p_tire_stack(bb, 3), (-3.45, -2.7, 0.07))
place("Prop_Boxes_Out", lambda bb: (p_cardboard(bb, (0.62, 0.55, 0.52), (0, 0, 0), 0.0),
                                    p_cardboard(bb, (0.46, 0.44, 0.40), (0.04, -0.02, 0.52), 0.35),
                                    p_cardboard(bb, (0.44, 0.40, 0.36), (0.0, -0.62, 0.0), -0.2)),
      (-3.5, -1.55, 0.07), 0.08)
place("Prop_Fence_L", lambda bb: p_fence(bb, 1.6, 1.15), (-3.72, -0.35, 0.05), math.pi / 2 + 0.05)
place("Prop_Fence_R", lambda bb: p_fence(bb, 1.4, 1.05), (3.72, -0.55, 0.05), -math.pi / 2 - 0.06)
place("Prop_Barrel", p_barrel, (3.35, -2.55, 0.07), 0.4)
place("Prop_Crate_Back", lambda bb: p_crate(bb, 0.62), (0.4, 2.62, 0.07), -0.08)
place("Prop_Barrel_Back", p_barrel, (-2.9, 2.6, 0.07), 1.2)

veg = B()
p_bush(veg, (-3.2, -2.05, 0.05), 0.6, 1.2, 11, leaves=64)
p_leafcluster(veg, (-3.15, -2.2, 0.5), 0.4, 1.3, 34, 12, (0.24, 0.4))   # climbing up the corner
p_leafcluster(veg, (-3.1, -2.25, 1.15), 0.3, 0.8, 18, 13, (0.2, 0.3))
p_bush(veg, (3.25, -2.1, 0.05), 0.5, 0.9, 21, leaves=56)
p_bush(veg, (-2.6, -3.3, 0.06), 0.35, 0.55, 31, leaves=22)
p_bush(veg, (3.7, 1.8, 0.06), 0.4, 0.6, 41, leaves=24)
p_bush(veg, (-3.75, 1.5, 0.06), 0.4, 0.7, 51, leaves=26)
for i, (gx, gy) in enumerate([(-1.9, -3.3), (2.6, -3.35), (0.3, -3.4), (-3.8, -0.9), (3.85, -1.9),
                              (-3.6, 2.8), (3.3, 2.8), (1.8, 2.75), (-1.2, -3.35), (2.35, -2.35),
                              (-1.75, -2.35), (3.8, 0.2)]):
    p_grass(veg, (gx, gy, 0.05), random.randint(7, 12), random.uniform(0.25, 0.42), 100 + i)
vegetation = veg.finish("Workshop_Vegetation", parent=root, collection=COL)

# ------------------------------------------------------------------ lights (render only, not exported)
LCOL = link_col("Lights")


def add_light(name, kind, loc, energy, color, size=0.1, rot=(0, 0, 0)):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = energy; ld.color = color
    if kind == 'AREA':
        ld.size = size[0]; ld.shape = 'RECTANGLE'; ld.size_y = size[1]
    elif kind in ('POINT', 'SPOT'):
        ld.shadow_soft_size = size
    ob = bpy.data.objects.new(name, ld); LCOL.objects.link(ob)
    ob.location = loc; ob.rotation_euler = rot
    return ob


add_light("Sun", 'SUN', (0, 0, 10), 4.2, (1.0, 0.93, 0.84), rot=(math.radians(50), 0, math.radians(-38)))
bpy.data.lights["Sun"].angle = math.radians(3)
add_light("Fill", 'SUN', (0, 0, 10), 0.6, (0.6, 0.7, 1.0), rot=(math.radians(60), 0, math.radians(150)))
add_light("Interior", 'AREA', (0.3, 0.2, ZROOF - 0.2), 55, (1.0, 0.52, 0.22), size=(4.8, 3.4))
add_light("CeilingLamp", 'POINT', (clx, cly, ZROOF - 0.72), 70, (1.0, 0.55, 0.22), size=0.08)
for i, (lx, lz) in enumerate(LAMPS):
    add_light(f"WallLamp{i}", 'POINT', (lx, Y0 - 0.46, lz + 0.13), 35, (1.0, 0.7, 0.35), size=0.05)

world = bpy.data.worlds.new("World"); scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes.get("Background")
bg.inputs[0].default_value = (0.20, 0.23, 0.30, 1); bg.inputs[1].default_value = 0.6

# ------------------------------------------------------------------ stats + save + export
tris = 0
for ob in COL.objects:
    if ob.type == 'MESH':
        ob.data.calc_loop_triangles()
        tris += len(ob.data.loop_triangles)
print("TRIS", tris, "OBJECTS", len([o for o in COL.objects if o.type == 'MESH']))
with open(os.path.join(OUT, "_stats.txt"), "w") as fh:
    fh.write(f"{tris}\n")

os.makedirs(OUT, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, "Workshop.blend"), relative_remap=True)


def export_fbx(path, objs):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
                             apply_scale_options='FBX_SCALE_ALL', apply_unit_scale=True,
                             bake_space_transform=True, axis_forward='-Z', axis_up='Y',
                             mesh_smooth_type='FACE', use_mesh_modifiers=True,
                             path_mode='RELATIVE', embed_textures=False, add_leaf_bones=False,
                             bake_anim=False)


export_fbx(os.path.join(OUT, "Workshop.fbx"), [root] + list(root.children))

# individual props at origin
os.makedirs(os.path.join(OUT, "Props"), exist_ok=True)
for ob in PROPS + [vegetation]:
    loc, par = ob.location.copy(), ob.parent
    ob.parent = None; ob.location = (0, 0, 0)
    export_fbx(os.path.join(OUT, "Props", ob.name + ".fbx"), [ob])
    ob.location = loc; ob.parent = par
print("EXPORT OK")



