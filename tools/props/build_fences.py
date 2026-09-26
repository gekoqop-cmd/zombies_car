"""Sheet 2 — cartoon fences.  blender -b --factory-startup --python build_fences.py
Sections run along X, front = -Y. Straight sections are 2.0 m long (post outer faces at x = +-1.0)."""
import bpy, math, os, sys, random
from mathutils import Matrix, Euler, Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import bcommon as C
from layout_fences import REG, ATLAS, ATLAS_FILE, POST_H, RAIL_Z

C.open_all()
S = C.Sheet("Fences", C.Atlas(REG, ATLAS), ATLAS_FILE, None, row_y=12.0, wn=True)
G = "Fences"
PW, PD = 0.26, 0.24            # chunky cartoon posts
RH, RT = 0.26, 0.10            # rails: two planks tall
RY = 0.035                     # rails recessed behind the post front
PX = 1.0 - PW / 2
EDGE = {'bevel': 'edge_hi', 'bevel_lo': 'edge_lo'}
POST = ('dir', {'-y': 'post_front', '+z': 'post_top', '-z': 'post_top', '*': 'post_side', **EDGE})
RAIL = ('dir', {'-y': 'rail', '+y': 'rail', '-x': 'endgrain', '+x': 'endgrain', '+z': 'rail_top', '-z': 'rail_top',
                '*': 'plank', **EDGE})
RAILB = ('dir', {**RAIL[1], '-y': 'rail_b', '+y': 'rail_b'})
PLANK = ('dir', {**RAIL[1], '-y': 'plank', '+y': 'plank'})
STILE = ('dir', {'-y': 'stile', '+y': 'stile', '*': 'post_side', '+z': 'post_top', **EDGE})
METAL = ('fit', 'metal')


def post(b, x, y=0.0, rotz=0.0, seed=0):
    r = random.Random(seed)
    h = POST_H + r.uniform(-0.03, 0.05)
    b.box((PW, PD, h), (x, y, h / 2), POST, bevel=0.035, taper=(0.95, 0.95),
          rot=(r.uniform(-0.025, 0.025), r.uniform(-0.03, 0.03), rotz + r.uniform(-0.03, 0.03)), uvlocal=True)


def rail(b, x0, x1, z, spec=RAIL, y=RY, seed=0, h=RH, t=RT):
    r = random.Random(seed)
    b.box((x1 - x0, t, h), ((x0 + x1) / 2, y + r.uniform(-0.01, 0.01), z + r.uniform(-0.02, 0.02)), spec,
          bevel=0.028, bevel_axis=0, rot=(r.uniform(-0.03, 0.03), r.uniform(-0.035, 0.035), r.uniform(-0.02, 0.02)),
          uvlocal=True)


def bracket(b, x, z=0.60):
    b.box((0.06, PD * 0.8, 0.24), (x, 0, z), METAL, bevel=0.012)


# ------------------------------------------------------------------ Fence_Straight1
m = S.model("Fence_Straight1", G, 2.2)
b = S.new()
post(b, -PX, seed=1); post(b, PX, seed=2)
for i, z in enumerate(RAIL_Z):
    rail(b, -PX + 0.09, PX - 0.09, z, seed=10 + i)
bracket(b, -1.02)
S.part(m, "Fence_Straight1_Body", b)

# ------------------------------------------------------------------ Fence_Straight2 (diagonal brace)
m = S.model("Fence_Straight2", G, 2.2)
b = S.new()
post(b, -PX, seed=3); post(b, PX, seed=4)
for i, z in enumerate(RAIL_Z):
    rail(b, -PX + 0.09, PX - 0.09, z, seed=20 + i)
ang = math.atan2(RAIL_Z[1] - RAIL_Z[0] - 0.05, 1.45)
b.box((1.55, RT * 0.8, 0.15), (0.0, RY + 0.06, (RAIL_Z[0] + RAIL_Z[1]) / 2), PLANK, bevel=0.022, bevel_axis=0,
      rot=(0, -ang, 0), uvlocal=True)
bracket(b, -1.02)
S.part(m, "Fence_Straight2_Body", b)

# ------------------------------------------------------------------ Fence_Broken
m = S.model("Fence_Broken", G, 2.2)
b = S.new()
post(b, -PX, seed=5); post(b, PX, seed=6)
rail(b, -PX + 0.09, PX - 0.09, RAIL_Z[1], RAILB, seed=30)
z0 = RAIL_Z[0]
rail(b, -PX + 0.09, PX - 0.09, z0 - RH / 4, PLANK, seed=31, h=RH / 2)          # lower plank intact
jag = [(-0.78, z0), (0.40, z0), (0.50, z0 + 0.03), (0.45, z0 + 0.06), (0.60, z0 + 0.085), (0.38, z0 + RH / 2),
       (-0.78, z0 + RH / 2)]
b.prism(jag, RT, 'y', (0, RY, 0), ('planar', 'plank', 1, (-0.78, 0.78, z0, z0 + RH / 2)), PLANK)
piece = [(0, -0.065), (0.50, -0.065), (0.58, -0.02), (0.49, 0.0), (0.62, 0.035), (0.46, 0.065), (0, 0.065)]
mrot = Matrix.Translation((-0.76, RY - 0.03, 0.58)) @ Euler((0, 0.40, 0)).to_matrix().to_4x4()
b.prism(piece, RT * 0.9, 'y', (0, 0, 0), ('planar', 'plank', 1, (0, 0.62, -0.07, 0.07)), PLANK, post=mrot)
bracket(b, -1.02)
S.part(m, "Fence_Broken_Body", b)

# ------------------------------------------------------------------ Fence_Corner (origin at the corner post)
m = S.model("Fence_Corner", G, 2.0)
b = S.new()
ARM = 1.35
post(b, 0, 0, seed=7)
post(b, ARM, 0, seed=8)
post(b, 0, ARM, rotz=-math.pi / 2, seed=9)
for i, z in enumerate(RAIL_Z):
    rail(b, PW / 2 - 0.04, ARM - PW / 2 + 0.04, z, y=RY, seed=40 + i)                       # X arm
    tmp = C.bmesh.new(); C.bmesh.ops.create_cube(tmp, size=1.0)
    L = ARM - PW + 0.08
    for v in tmp.verts:
        v.co = Vector((v.co.x * L, v.co.y * RT, v.co.z * RH))
    C.bmesh.ops.bevel(tmp, geom=[e for e in tmp.edges if abs((e.verts[0].co - e.verts[1].co).normalized()[0]) > 0.9],
                      offset=0.028, segments=1, affect='EDGES', clamp_overlap=True)
    r = random.Random(50 + i)
    b.add(tmp, RAIL, post=Matrix.Translation((RY, ARM / 2, z + r.uniform(-0.02, 0.02))) @
          Euler((r.uniform(-0.03, 0.03), r.uniform(-0.03, 0.03), -math.pi / 2)).to_matrix().to_4x4())  # Y arm
S.part(m, "Fence_Corner_Body", b)

# ------------------------------------------------------------------ Fence_Gate (door pivots on its hinge)
m = S.model("Fence_Gate", G, 4.6)
b = S.new()
GX = (-2.10, -0.76, 0.76, 2.10)
for i, x in enumerate(GX):
    post(b, x, seed=60 + i)
for j, (xa, xb) in enumerate(((GX[0], GX[1]), (GX[2], GX[3]))):
    for i, z in enumerate(RAIL_Z):
        rail(b, xa + 0.09, xb - 0.09, z, seed=70 + 2 * j + i)
b.box((0.14, 0.03, 0.08), (0.70, -PD / 2 - 0.02, 0.80), METAL)          # latch catch on the post
S.part(m, "Fence_Gate_Frame", b)

d = S.new()
DX0, DX1, DY, DZ0, DZ1 = -0.62, 0.62, 0.0, 0.07, 1.07
d.box((DX1 - DX0 - 0.18, 0.04, DZ1 - DZ0 - 0.12), (0, DY + 0.015, (DZ0 + DZ1) / 2), ('fit', 'gate'))
for x in (DX0 + 0.06, DX1 - 0.06):
    d.box((0.12, 0.08, DZ1 - DZ0), (x, DY, (DZ0 + DZ1) / 2), STILE, bevel=0.022, bevel_axis=2)
for z in (DZ0 + 0.08, DZ1 - 0.08):
    d.box((DX1 - DX0 - 0.22, 0.07, 0.15), (0, DY - 0.005, z), PLANK)
ang = math.atan2(DZ1 - DZ0 - 0.40, DX1 - DX0 - 0.36)
d.box((1.05, 0.06, 0.15), (0, DY - 0.012, (DZ0 + DZ1) / 2), PLANK, bevel=0.018, bevel_axis=0, rot=(0, -ang, 0),
      uvlocal=True)
for z in (0.30, 0.82):                                                   # big hinge plates bridging post and door
    d.box((0.34, 0.025, 0.11), (DX0 - 0.03, -PD / 2 - 0.013, z), METAL)
d.box((0.14, 0.035, 0.06), (DX1 - 0.08, DY - 0.05, 0.80), METAL)          # latch bar
S.part(m, "Fence_Gate_Door", d, origin=(DX0 - 0.03, -PD / 2 - 0.02, 0.0))

stats = S.export(os.path.join(C.SRC, "stats_Fences.json"))
C.save_all()
print("DONE", {k: v["tris"] for k, v in stats.items()})
