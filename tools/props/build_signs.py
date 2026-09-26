"""Sheet 1 — signs and lights.  blender -b --factory-startup --python build_signs.py
Models face -Y (front), origin at the base center."""
import bpy, math, os, sys, random
from mathutils import Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import bcommon as C
from layout_signs import REG, ATLAS, ATLAS_FILE, EMIT_FILE, ARROW

random.seed(1)
C.open_all()
S = C.Sheet("Signs", C.Atlas(REG, ATLAS), ATLAS_FILE, EMIT_FILE, row_y=0.0)
GROUP = "Signs"

W_D = lambda z0, z1: ('vert', 'wood_dark', z0, z1)
W_O = lambda z0, z1: ('vert', 'wood_orange', z0, z1)
CONC = ('fit', 'concrete'); MDARK = ('fit', 'metal_dark'); BAND = ('fit', 'band')
MGREY = ('fit', 'metal_grey'); CABLE = ('fit', 'cable'); CER = ('fit', 'ceramic'); RED = ('fit', 'red')


def base_block(b, x, y, bw=0.42, h=0.32):
    b.box((bw, bw, h), (x, y, h / 2), CONC, bevel=0.03, taper=(0.8, 0.8))


def bands(b, x, y, zs, w, h=0.06, bolts=True):
    for z in zs:
        b.box((w + 0.03, w + 0.03, h), (x, y, z), BAND)
        if bolts:
            b.box((0.035, 0.03, 0.035), (x, y - w / 2 - 0.03, z), MDARK)


def lantern(b, x, y, z, r=0.10):
    """Hanging lantern: cone shade + glowing glass + bottom ring. z = top of shade."""
    b.lathe([(0, 0), (0.03, 0), (r * 1.35, -0.10), (r * 1.4, -0.12), (0, -0.12)], 8, MDARK, (x, y, z), smooth=False)
    b.cyl(0.02, 0.05, (x, y, z + 0.02), MDARK, segs=6)
    b.lathe([(r * 0.85, 0), (r, -0.16), (r * 0.9, -0.17)], 8, ('emit',), (x, y, z - 0.12), smooth=False)
    b.lathe([(r * 0.95, 0), (r * 0.8, -0.04), (0, -0.05)], 8, MDARK, (x, y, z - 0.29), smooth=False)
    for k in range(4):
        a = k * math.pi / 2 + math.pi / 4
        b.box((0.012, 0.012, 0.17), (x + r * 0.97 * math.cos(a), y + r * 0.97 * math.sin(a), z - 0.2), MDARK)


# ======================================================================== Sign_Gas
m = S.model("Sign_Gas", GROUP, 3.0)
PX = 0.95
posts = S.new()
for x, top in ((-PX, 3.02), (PX, 2.92)):
    base_block(posts, x, 0)
    posts.box((0.14, 0.14, top - 0.28), (x, 0, 0.28 + (top - 0.28) / 2), W_D(0.28, top), bevel=0.015)
    posts.box((0.18, 0.18, 0.05), (x, 0, top + 0.025), MDARK)
    bands(posts, x, 0, (0.42, 1.05, 1.78), 0.14)
S.part(m, "Sign_Gas_Posts", posts)

brd = S.new()
AX0, AX1, AZ0, AZ1 = -1.40, 1.30, 1.84, 2.84
poly = [(AX0 + (AX1 - AX0) * u, AZ0 + (AZ1 - AZ0) * v) for u, v in ARROW]
brd.prism(poly, 0.07, 'y', (0, -0.14, 0), ('planar', 'arrow', 1, (AX0, AX1, AZ0, AZ1)), RED)
for x in (-PX, PX):   # brackets bolting the board to the posts
    for z in (1.98, 2.62):
        brd.box((0.20, 0.10, 0.06), (x, -0.075, z), MDARK)
S.part(m, "Sign_Gas_Board", brd)

pl = S.new()
pl.box((1.95, 0.06, 0.52), (-0.02, -0.11, 1.44), ('fit', 'wood_frame'), bevel=0.01)
pl.box((1.93, 0.005, 0.50), (-0.02, -0.1425, 1.44), ('fit', 'kafe'))
for x in (-PX, PX):
    for z in (1.30, 1.58):
        pl.box((0.18, 0.08, 0.05), (x, -0.07, z), MDARK)
S.part(m, "Sign_Gas_Plate", pl)

lp = S.new()
for x, top, dirx in ((-PX, 3.02, 1), (PX, 2.92, 1)):
    lp.pipe([(x, 0, top + 0.03), (x, 0, top + 0.16), (x + dirx * 0.30, -0.05, top + 0.18),
             (x + dirx * 0.36, -0.05, top + 0.10)], 0.022, MDARK, segs=6, fillet=0.08)
    lp.pipe([(x, 0.0, top - 0.25), (x + dirx * 0.16, -0.03, top + 0.16)], 0.014, MDARK, segs=5)
    lantern(lp, x + dirx * 0.36, -0.05, top + 0.08, r=0.085)
S.part(m, "Sign_Gas_Lamps", lp)

# ======================================================================== Sign_Road
m = S.model("Sign_Road", GROUP, 1.4)
post = S.new()
base_block(post, 0, 0, 0.44, 0.34)
post.box((0.12, 0.12, 2.0), (0, 0, 0.30 + 1.0), W_D(0.30, 2.30), bevel=0.015)
bands(post, 0, 0, (0.45,), 0.12)
S.part(m, "Sign_Road_Post", post)
pt = S.new()
SW, SH, SZ = 1.0, 1.02, 1.28
pt.box((SW, 0.035, SH), (0, -0.09, SZ + SH / 2), ('fit', 'road_back'), bevel=0.012)
pt.box((SW - 0.02, 0.005, SH - 0.02), (0, -0.11, SZ + SH / 2), ('fit', 'road_face'))
for z in (SZ + 0.2, SZ + SH - 0.2):
    pt.box((0.18, 0.12, 0.06), (0, -0.02, z), BAND, bevel=0.008)
    pt.box((0.04, 0.03, 0.04), (0.07, -0.075, z), MDARK)
S.part(m, "Sign_Road_Plate", pt)

# ======================================================================== Lamp_Street
m = S.model("Lamp_Street", GROUP, 1.6)
pole = S.new()
base_block(pole, 0, 0, 0.52, 0.38)
pole.lathe([(0.13, 0.36), (0.13, 0.52), (0.095, 0.56)], 8, BAND, smooth=False)
pole.lathe([(0.095, 0.55), (0.085, 3.80), (0, 3.80)], 8, ('vert', 'metal_pole', 0.55, 3.8), smooth=False)
for z in (1.0, 2.25, 3.35):
    pole.cyl(0.105, 0.08, (0, 0, z), BAND, segs=8)
pole.box((0.24, 0.24, 0.07), (0, 0, 3.83), MDARK, bevel=0.012)
pole.box((0.14, 0.14, 0.07), (0, 0, 3.90), MDARK, bevel=0.01)
pole.pipe([(0.05, 0, 3.66), (0.45, 0, 3.80), (0.86, 0, 3.80), (0.92, 0, 3.72)], 0.03, MDARK, segs=6, fillet=0.12)
pole.pipe([(0.07, 0, 3.25), (0.40, 0, 3.55), (0.62, 0, 3.78)], 0.018, MDARK, segs=5, fillet=0.2)
S.part(m, "Lamp_Street_Pole", pole)
hd = S.new()
lantern(hd, 0.92, 0, 3.70, r=0.13)
S.part(m, "Lamp_Street_Head", hd)

# ======================================================================== Pole_Power
m = S.model("Pole_Power", GROUP, 2.4)
pp = S.new()
base_block(pp, 0, 0, 0.62, 0.42)
pp.lathe([(0.12, 0.40), (0.11, 6.0), (0, 6.02)], 8, W_O(0.40, 6.0), smooth=False)
bands(pp, 0, 0, (0.58, 2.7, 3.5, 4.35), 0.22, h=0.08)
pp.box((1.95, 0.15, 0.15), (0, -0.14, 5.55), ('along', 'wood_orange', 0, -0.98, 0.98), bevel=0.02)
pp.box((0.30, 0.04, 0.24), (0, -0.07, 5.55), BAND)
for x in (-0.07, 0.07):
    pp.box((0.04, 0.05, 0.04), (x, -0.23, 5.55), MDARK)
# diagonal braces
for sx in (-1, 1):
    pp.box((0.035, 0.035, 0.60), (sx * 0.32, -0.10, 5.30), BAND, rot=(0, sx * 0.83, 0))
S.part(m, "Pole_Power_Pole", pp)

ins = S.new()
INS = [(-0.82, -0.14), (-0.55, -0.14), (0.82, -0.14)]
for x, y in INS:
    ins.box((0.10, 0.10, 0.04), (x, y, 5.645), BAND)
    ins.lathe([(0.035, 5.66), (0.064, 5.71), (0.035, 5.75), (0.052, 5.79), (0, 5.85)], 6, CER,
              (x, y, 0), smooth=False)
S.part(m, "Pole_Power_Insulators", ins)

bx = S.new()
BX, BZ = 0.66, 4.55
bx.box((0.42, 0.30, 0.58), (BX, -0.24, BZ), MGREY, bevel=0.02)
bx.box((0.40, 0.01, 0.56), (BX, -0.395, BZ), ('fit', 'box_front'))
bx.box((0.48, 0.36, 0.05), (BX, -0.24, BZ + 0.31), MGREY)
bx.box((0.22, 0.18, 0.08), (BX, -0.24, BZ + 0.37), MGREY)
bx.cyl(0.05, 0.10, (BX, -0.24, BZ + 0.46), MDARK, segs=6)
bx.pipe([(BX, -0.24, BZ + 0.50), (BX, -0.24, 5.30), (BX - 0.08, -0.20, 5.42), (BX - 0.08, -0.14, 5.48)], 0.02, MDARK,
        segs=5, fillet=0)
for z in (BZ - 0.16, BZ + 0.16):   # straps to the pole
    bx.box((0.55, 0.05, 0.05), (BX / 2 + 0.05, -0.12, z), BAND)
for dx in (-0.1, 0.1):
    bx.cyl(0.025, 0.06, (BX + dx, -0.24, BZ - 0.32), MDARK, segs=6)
bx.box((0.06, 0.08, 0.10), (BX - 0.235, -0.24, BZ + 0.05), MDARK)
S.part(m, "Pole_Power_Box", bx)

wr = S.new()
R = 0.012
for (x, y) in INS[:1]:
    wr.pipe(wr.catenary((x, y, 5.84), (0.05, -0.17, 5.05), 0.12, 5), R, CABLE, segs=3, fillet=0)
wr.pipe(wr.catenary((0.82, -0.14, 5.84), (BX + 0.05, -0.24, BZ + 0.30), 0.10, 5), R, CABLE, segs=3, fillet=0)
wr.pipe(wr.catenary((-0.55, -0.14, 5.84), (BX - 0.1, -0.24, BZ + 0.30), 0.35, 6), R, CABLE, segs=3, fillet=0)
wr.pipe([(BX - 0.26, -0.24, BZ + 0.05), (BX - 0.42, -0.24, BZ - 0.10), (BX - 0.40, -0.22, BZ - 0.60),
         (0.10, -0.12, BZ - 0.95), (0.10, -0.12, 3.5)], R * 1.3, CABLE, segs=3, fillet=0.10)
wr.pipe([(BX + 0.1, -0.24, BZ - 0.35), (BX + 0.12, -0.24, BZ - 0.55), (BX - 0.05, -0.20, BZ - 0.70),
         (0.12, -0.12, BZ - 0.62)], R * 1.3, CABLE, segs=3, fillet=0.08)
S.part(m, "Pole_Power_Wires", wr)

stats = S.export(os.path.join(C.SRC, "stats_Signs.json"))
C.save_all()
print("DONE", {k: v["tris"] for k, v in stats.items()})
