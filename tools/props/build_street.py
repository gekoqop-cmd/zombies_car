"""Sheet 3 — street props.  blender -b --factory-startup --python build_street.py
Origin at the base center, front = -Y. Each model has an optional <Name>_Grass part (tufts at the base)."""
import bpy, bmesh, math, os, sys, random
from mathutils import Vector, Matrix, Euler

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import bcommon as C
from layout_street import *  # noqa

C.open_all()
S = C.Sheet("Street", C.Atlas(REG, ATLAS), ATLAS_FILE, None, row_y=24.0, wn=True)
G = "Street"
GRASS = ('remap', 'grass')


def add_grass(model, name, spots, seed):
    g = S.new()
    for i, (x, y, n, h) in enumerate(spots):
        g.grass((x, y, 0), n + 5, h * 1.15, seed + i, GRASS, spread=0.04, width=0.065, lean=(0.3, 0.75))
    S.part(model, name + "_Grass", g)


# ------------------------------------------------------------------ barrels
def barrel(b, body, lid):
    b.lathe(BARREL_PROFILE, 12, ('remap', body))
    top = BARREL_PROFILE[-1]
    lid_spec = ('planar', lid, 2, (-0.29, 0.29, -0.29, 0.29))
    b.lathe([top, (0.272, 0.858), (0.0, 0.858)], 12, lid_spec)
    b.lathe([(0.0, 0.0), (BARREL_PROFILE[0][0], 0.0)], 12, lid_spec, smooth=False)
    b.lathe([(0.052, 0.858), (0.052, 0.875), (0.036, 0.875), (0.036, 0.862)], 6, ('fit', 'rubber'),
            center=(0, 0.116, 0), smooth=False)                     # raised bung ring


for name, body, lid, seed in (("Barrel_RedWhite", "barrel_rw", "lid_rw", 1), ("Barrel_Grey", "barrel_grey", "lid_grey", 2)):
    m = S.model(name, G, 1.0)
    b = S.new(); barrel(b, body, lid)
    S.part(m, name + "_Body", b)
    add_grass(m, name, [(-0.30, -0.12, 6, 0.26), (0.30, -0.05, 5, 0.22)], 10 * seed)


# ------------------------------------------------------------------ tires (simple ring, tread painted)
def tire(b, c=(0, 0, 0), rot=(0, 0, 0)):
    kw = dict(center=c, rot=rot)
    b.lathe([(0.21, 0.03), (0.37, 0.0)], 12, ('remap', 'tire_side'), **kw)
    b.lathe([(0.37, 0.0), (0.405, 0.04), (0.412, 0.13), (0.405, 0.22), (0.37, 0.26)], 12, ('remap', 'tire_tread'), **kw)
    b.lathe([(0.37, 0.26), (0.21, 0.23)], 12, ('remap', 'tire_side', 'flipv'), **kw)
    b.lathe([(0.21, 0.23), (0.195, 0.13), (0.21, 0.03)], 12, ('remap', 'tire_inner'), **kw)


m = S.model("Tires_Stack", G, 1.0)
b = S.new()
r = random.Random(3)
for i in range(3):
    tire(b, (r.uniform(-0.03, 0.03), r.uniform(-0.03, 0.03), i * 0.258),
         (r.uniform(-0.02, 0.02), r.uniform(-0.02, 0.02), r.uniform(0, 1)))
S.part(m, "Tires_Stack_Body", b)
add_grass(m, "Tires_Stack", [(-0.40, -0.18, 6, 0.26), (0.40, -0.10, 6, 0.24)], 40)

m = S.model("Tire_Single", G, 1.0)
b = S.new(); tire(b)
S.part(m, "Tire_Single_Body", b)
add_grass(m, "Tire_Single", [(-0.42, -0.10, 5, 0.22), (0.40, 0.05, 5, 0.2)], 50)

# ------------------------------------------------------------------ cone
m = S.model("Cone", G, 0.6)
b = S.new()
b.box((0.44, 0.44, 0.045), (0, 0, 0.0225), ('fit', 'cone_base'), bevel=0.014)
b.lathe(CONE_PROFILE, 12, ('remap', 'cone'))
b.lathe([(0.205, 0.045), (0.19, 0.062), (0.172, 0.062)], 12, ('fit', 'cone_base'), smooth=False)   # base collar
S.part(m, "Cone_Body", b)
add_grass(m, "Cone", [(-0.26, -0.05, 5, 0.22), (0.25, 0.08, 5, 0.2)], 60)

# ------------------------------------------------------------------ concrete barrier (jersey)
m = S.model("Barrier", G, 2.2)
L, H = BARRIER_L, BARRIER_H
Z0 = 0.10
PROF = [(-0.30, Z0), (0.30, Z0), (0.30, 0.18), (0.17, 0.34), (0.10, 0.80), (0.07, H), (-0.07, H), (-0.10, 0.80),
        (-0.17, 0.34), (-0.30, 0.18)]
cy = sum(p[0] for p in PROF) / len(PROF); cz = sum(p[1] for p in PROF) / len(PROF)
CH = 0.045
t = bmesh.new()
loops = []
for x, s in ((-L / 2, 0.88), (-L / 2 + CH, 1.0), (L / 2 - CH, 1.0), (L / 2, 0.88)):
    loops.append([t.verts.new((x, cy + (py - cy) * s, cz + (pz - cz) * s)) for py, pz in PROF])
n = len(PROF)
for i in range(3):
    for j in range(n):
        t.faces.new((loops[i][j], loops[i][(j + 1) % n], loops[i + 1][(j + 1) % n], loops[i + 1][j]))
t.faces.new(loops[0]); t.faces.new(loops[-1])
bmesh.ops.recalc_face_normals(t, faces=t.faces)
FACE = ('planar', 'barrier_face', 1, (-L / 2, L / 2, 0.0, H))


def barrier_spec(f):
    nrm = f.normal
    if abs(nrm.x) > 0.95:
        return ('fit', 'barrier_end')
    if nrm.z > 0.95:
        return ('fit', 'barrier_top')
    if nrm.z < -0.95:
        return ('fit', 'concrete')
    return FACE


b = S.new()
b.add(t, barrier_spec)
for x0, x1 in ((-L / 2 + 0.02, -0.60), (-0.20, 0.20), (0.60, L / 2 - 0.02)):     # feet with forklift slots between
    b.box((x1 - x0, 0.58, Z0 + 0.01), ((x0 + x1) / 2, 0, (Z0 + 0.01) / 2), ('fit', 'concrete'), bevel=0.02)
S.part(m, "Barrier_Body", b)
add_grass(m, "Barrier", [(-0.95, -0.32, 6, 0.28), (0.85, -0.30, 6, 0.26), (0.0, 0.33, 5, 0.22)], 70)

stats = S.export(os.path.join(C.SRC, "stats_Street.json"))
C.save_all()
print("DONE", {k: v["tris"] for k, v in stats.items()})
