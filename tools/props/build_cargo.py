"""Sheet 4 — cargo + furniture (cartoon).  blender -b --factory-startup --python build_cargo.py
Origin at the base center, front = -Y."""
import bpy, math, os, sys, random
from mathutils import Vector

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
import bcommon as C
from layout_cargo import REG, ATLAS, ATLAS_FILE

C.open_all()
S = C.Sheet("Cargo", C.Atlas(REG, ATLAS), ATLAS_FILE, None, row_y=36.0, wn=True)
PI = math.pi


def Dr(front, side=None, top=None, fam='wood', back=None, bottom=None):
    side = side or front; top = top or side
    return ('dir', {'-y': front, '+y': back or front, '-x': side, '+x': side, '+z': top, '-z': bottom or top,
                    '*': side, 'bevel': fam + '_hi', 'bevel_lo': fam + '_lo'})


F = lambda k: ('fit', k)


def board(b, size, center, bolts=True, rot=(0, 0, 0)):
    """Wooden board; grain follows its long axis on every face, end grain on the ends."""
    la = max(range(3), key=lambda i: size[i])

    def sp(f):
        n = f.normal; na = max(range(3), key=lambda i: abs(n[i]))
        if la == na:
            return F('endgrain')
        uax = {0: 1, 1: 0, 2: 0}[na]
        if la == uax:
            return F('board_h' if bolts else 'board_hn')
        return F('board_v' if bolts else 'board_vn')
    b.box(size, center, sp, rot=rot, uvlocal=True)


def frame12(b, W, D, H, T, z0=0.0, spec=None, bolts=True):
    """Boards along all 12 edges of a W x D x H box."""
    put = (lambda s, c: b.box(s, c, spec)) if spec else (lambda s, c: board(b, s, c, bolts))
    for y in (-(D / 2 - T / 2), D / 2 - T / 2):
        for z in (z0 + T / 2, z0 + H - T / 2):
            put((W, T, T), (0, y, z))
    for x in (-(W / 2 - T / 2), W / 2 - T / 2):
        for z in (z0 + T / 2, z0 + H - T / 2):
            put((T, D - 2 * T, T), (x, 0, z))
        for y in (-(D / 2 - T / 2), D / 2 - T / 2):
            put((T, T, H - 2 * T), (x, y, z0 + H / 2))


def caster(b, x, y, r=0.06):
    b.cyl(r, 0.045, (x, y, r), F('rubber'), segs=8, rot=(0, PI / 2, 0))
    b.box((0.07, 0.07, 0.045), (x, y, 2 * r + 0.02), F('grey_lo'))


def can(b, x, y, z, r, h, key, segs=8):
    b.lathe([(r * 0.92, 0), (r, 0.012), (r, h - 0.012), (r * 0.92, h)], segs, ('remap', key), center=(x, y, z))
    b.lathe([(r * 0.92, h), (0, h - 0.004)], segs, ('planar', 'can_top', 2, (x - r, x + r, y - r, y + r)),
            center=(x, y, z))


def bottle(b, x, y, z, r, h, key):
    b.lathe([(r * 0.9, 0), (r, 0.01), (r, h * 0.72), (r * 0.6, h * 0.86), (r * 0.45, h * 0.9)], 8, ('remap', key),
            center=(x, y, z))
    b.cyl(r * 0.45, h * 0.12, (x, y, z + h * 0.95), F('black'), segs=6)


def uhandle(b, p0, p1, out, h, r, key, segs=4):
    p0, p1, out = Vector(p0), Vector(p1), Vector(out)
    b.pipe([p0, p0 + out * h, p1 + out * h, p1], r, F(key), segs=segs, fillet=0)


def strap(b, box_c, box_s, dx, key, rot=0.0):
    b.box((0.035, box_s[1] + 0.014, box_s[2] + 0.014), (box_c[0] + dx, box_c[1], box_c[2]), F(key), rot=(0, 0, rot))


def small_box(b, size, c, front, side, fam, rot=0.0, bevel=0.015):
    b.box(size, (c[0], c[1], c[2] + size[2] / 2), Dr(front, side, side, fam), bevel=bevel, rot=(0, 0, rot),
          uvlocal=True)


# ================================================================== CARGO
G = "Cargo"

# ---- Crate_Big: wooden crate, plank panels, edge frame, X braces front/back
m = S.model("Crate_Big", G, 1.2)
b = S.new()
W, D, H, T = 1.0, 0.85, 0.85, 0.085
b.box((W - 0.05, D - 0.05, H - 0.05), (0, 0, H / 2), Dr('wood_v', 'wood_v', 'wood_h'))
frame12(b, W, D, H, T)
Lx, ang = math.hypot(W - 2 * T, H - 2 * T), math.atan2(H - 2 * T, W - 2 * T)
for y in (-(D / 2 - 0.035), D / 2 - 0.035):
    for s in (1, -1):
        board(b, (Lx, 0.03, 0.10), (0, y, H / 2), bolts=False, rot=(0, s * ang, 0))
S.part(m, "Crate_Big_Body", b)

# ---- Crate_Small1: olive military box
m = S.model("Crate_Small1", G, 0.9)
b = S.new()
W, D, H = 0.70, 0.50, 0.46
b.box((W - 0.02, D - 0.02, H - 0.12), (0, 0, (H - 0.12) / 2), Dr('olive_face', 'olive_face', 'olive', 'olive'))
b.box((W, D, 0.12), (0, 0, H - 0.06), Dr('olive', 'olive', 'olive_face', 'olive'))
frame12(b, W + 0.02, D + 0.02, H, 0.045, spec=F('olive_flat'))
for x in (-0.18, 0.18):                                                     # latches across the lid seam
    b.box((0.07, 0.02, 0.11), (x, -D / 2 - 0.02, H - 0.12), F('chrome'))
    b.box((0.05, 0.035, 0.03), (x, -D / 2 - 0.035, H - 0.15), F('grey_lo'))
for sx in (-1, 1):                                                          # side carry handles
    uhandle(b, (sx * (W / 2 + 0.02), -0.10, 0.30), (sx * (W / 2 + 0.02), 0.10, 0.30), (sx, 0, -0.35), 0.06,
            0.013, 'chrome')
S.part(m, "Crate_Small1_Body", b)

# ---- Crate_Small2: cardboard box with tape
m = S.model("Crate_Small2", G, 0.8)
b = S.new()
W, D, H = 0.56, 0.46, 0.42
b.box((W, D, H), (0, 0, H / 2), Dr('card_side', 'card_side', 'card_top', 'card'), bevel=0.015)
b.box((W * 0.2, D + 0.008, 0.006), (0, 0, H + 0.002), F('tan'))                    # tape over the top ...
for y in (-(D / 2 + 0.003), D / 2 + 0.003):                                         # ... and down the sides
    b.box((W * 0.2, 0.006, H * 0.35), (0, y, H - H * 0.175), F('tan'))
S.part(m, "Crate_Small2_Body", b)

# ---- Crate_Small3: small wooden crate, horizontal planks
m = S.model("Crate_Small3", G, 0.8)
b = S.new()
W, D, H, T = 0.56, 0.50, 0.46, 0.065
b.box((W - 0.04, D - 0.04, H - 0.04), (0, 0, H / 2), Dr('wood_h', 'wood_h', 'wood_h'))
frame12(b, W, D, H, T)
S.part(m, "Crate_Small3_Body", b)

# ---- Jerrycans
for name, face, flat, fam in (("Jerrycan_Red", 'jerry_red', 'red_flat', 'red'),
                              ("Jerrycan_Green", 'jerry_green', 'green_flat', 'olive')):
    m = S.model(name, G, 0.6)
    b = S.new()
    W, D, H = 0.36, 0.18, 0.46
    b.box((W, D, H), (0, 0, H / 2), Dr(face, flat, flat, fam), bevel=0.03)
    b.pipe([(-0.13, 0, H - 0.02), (-0.11, 0, H + 0.075), (0.05, 0, H + 0.075), (0.07, 0, H - 0.02)], 0.02, F(flat),
           segs=5, fillet=0)
    b.pipe([(-0.03, 0, H - 0.02), (-0.03, 0, H + 0.075)], 0.016, F(flat), segs=5, caps=False)
    b.cyl(0.03, 0.07, (0.115, 0, H + 0.02), F('black'), segs=8)
    b.cyl(0.037, 0.03, (0.115, 0, H + 0.065), F('black'), segs=8)
    S.part(m, name + "_Body", b)

# ---- Cart_Cargo: flatbed cart with rail and strapped boxes
m = S.model("Cart_Cargo", G, 1.6)
b = S.new()
DZ = 0.30
b.box((1.30, 0.80, 0.06), (0, 0, DZ - 0.03), Dr('grey_panel', 'grey_panel', 'grey_panel', 'grey'), bevel=0.015)
for y in (-0.25, 0.25):
    b.box((1.20, 0.06, 0.06), (0, y, DZ - 0.09), F('grey_lo'))
for x in (-0.55, 0.55):
    for y in (-0.32, 0.32):
        caster(b, x, y, 0.075)
RZ = DZ + 0.22
for x in (-0.62, 0.0, 0.62):
    for y in (-0.37, 0.37):
        b.pipe([(x, y, DZ), (x, y, RZ)], 0.02, F('chrome'), segs=5, caps=False)
b.pipe([(-0.62, -0.37, RZ), (0.62, -0.37, RZ), (0.62, 0.37, RZ), (-0.62, 0.37, RZ), (-0.62, -0.37, RZ)], 0.022,
       F('chrome'), segs=5, fillet=0.06)
S.part(m, "Cart_Cargo_Body", b)
c = S.new()
boxes = [((0.36, 0.34, 0.32), (-0.40, -0.14, DZ), 'box_olive', 'olive_flat', 'olive', 0.04),
         ((0.46, 0.34, 0.28), (0.02, -0.16, DZ), 'box_red', 'red_flat', 'red', -0.03),
         ((0.40, 0.34, 0.36), (-0.36, 0.18, DZ), 'box_olive', 'olive_flat', 'olive', 0.02),
         ((0.50, 0.40, 0.32), (0.34, 0.14, DZ), 'box_olive', 'olive_flat', 'olive', 0.0),
         ((0.46, 0.36, 0.28), (0.32, 0.12, DZ + 0.32), 'box_tan', 'tan', 'card', 0.10)]
for size, cc, front, side, fam, rz in boxes:
    small_box(c, size, cc, front, side, fam, rot=rz)
bc = (0.02, -0.16, DZ + 0.14)
strap(c, bc, (0.46, 0.34, 0.28), -0.12, 'strap_g', -0.03); strap(c, bc, (0.46, 0.34, 0.28), 0.12, 'strap_g', -0.03)
strap(c, (0.34, 0.14, DZ + 0.16), (0.5, 0.40, 0.32), 0.0, 'strap_d')
strap(c, (0.32, 0.12, DZ + 0.46), (0.46, 0.36, 0.28), 0.0, 'strap_g', 0.10)
S.part(m, "Cart_Cargo_Load", c)

# ================================================================== FURNITURE
G = "Furniture"

# ---- Toolbox_Red: rolling tool cabinet
m = S.model("Toolbox_Red", G, 0.9)
b = S.new()
for x in (-0.27, 0.27):
    for y in (-0.18, 0.18):
        caster(b, x, y, 0.05)
RP = Dr('red_panel', 'red_panel', 'red_panel', 'red')
b.box((0.66, 0.46, 0.62), (0, 0, 0.13 + 0.31), RP, bevel=0.02)
b.box((0.67, 0.47, 0.03), (0, 0, 0.765), F('grey_lo'))
b.box((0.64, 0.44, 0.34), (0, 0, 0.78 + 0.17), RP, bevel=0.02)
b.box((0.66, 0.46, 0.05), (0, 0, 1.145), RP, bevel=0.015)
FY = -0.23 - 0.008
b.box((0.58, 0.016, 0.28), (0, FY, 0.31), F('red_door'))
b.box((0.24, 0.03, 0.025), (0, FY - 0.02, 0.42), F('chrome'))
for z0, z1 in ((0.48, 0.55), (0.565, 0.635), (0.65, 0.72), (0.81, 0.92), (0.94, 1.08)):
    b.box((0.58, 0.016, z1 - z0), (0, FY - 0.005 if z0 > 0.8 else FY, (z0 + z1) / 2), F('red_drawer'))
    b.box((0.26, 0.03, 0.022), (0, FY - 0.025, z1 - 0.02), F('chrome'))
uhandle(b, (-0.13, 0, 1.17), (0.13, 0, 1.17), (0, 0, 1), 0.07, 0.02, 'black', segs=5)
uhandle(b, (0.33, -0.10, 0.95), (0.33, 0.10, 0.95), (1, 0, 0), 0.05, 0.013, 'chrome')
S.part(m, "Toolbox_Red_Body", b)

# ---- Shelf_Red: red shelf with cans and a toolbox
m = S.model("Shelf_Red", G, 1.1)
b = S.new()
W, D, H = 0.90, 0.42, 1.60
for x in (-(W / 2 - 0.025), W / 2 - 0.025):
    b.box((0.05, D, H - 0.10), (x, 0, 0.05 + (H - 0.10) / 2), RP, bevel=0.012)
b.box((W + 0.04, D + 0.03, 0.06), (0, 0, H - 0.03), RP, bevel=0.015)
b.box((W - 0.05, D - 0.01, 0.07), (0, 0, 0.085), F('red_panel'))
b.box((W - 0.06, 0.02, H - 0.14), (0, D / 2 - 0.02, 0.07 + (H - 0.14) / 2), F('red_panel'))
for z in (0.58, 1.06):
    b.box((W - 0.08, D - 0.04, 0.03), (0, 0.0, z), F('red_panel'))
for x in (-(W / 2 + 0.01), W / 2 + 0.01):
    b.box((0.07, 0.05, 0.07), (x, -D / 2, H - 0.03), F('brass'))
    for y in (-(D / 2 - 0.03), D / 2 - 0.03):
        b.box((0.06, 0.06, 0.05), (x * 0.96, y, 0.025), F('black'))
S.part(m, "Shelf_Red_Body", b)
it = S.new()
L0, L1, L2 = 0.12, 0.595, 1.075
can(it, -0.25, -0.03, L2, 0.07, 0.15, 'can_grey'); bottle(it, -0.06, 0.0, L2, 0.05, 0.24, 'can_yellow')
can(it, 0.20, -0.02, L2, 0.09, 0.12, 'can_grey')
can(it, -0.26, -0.03, L1, 0.075, 0.24, 'can_blue'); bottle(it, -0.06, 0.0, L1, 0.055, 0.26, 'can_yellow')
can(it, 0.20, -0.02, L1, 0.07, 0.25, 'can_grey')
small_box(it, (0.38, 0.28, 0.22), (-0.12, -0.02, L0), 'box_red', 'red_flat', 'red')
bottle(it, 0.26, 0.0, L0, 0.05, 0.24, 'can_yellow')
S.part(m, "Shelf_Red_Items", it)

# ---- Cart_Tools: grey tool cart
m = S.model("Cart_Tools", G, 1.0)
b = S.new()
for x in (-0.33, 0.33):
    for y in (-0.19, 0.19):
        caster(b, x, y, 0.06)
GP = Dr('grey_panel', 'grey_panel', 'grey_panel', 'grey')
b.box((0.80, 0.50, 0.58), (0, 0, 0.15 + 0.29), GP, bevel=0.02)
b.box((0.84, 0.54, 0.03), (0, 0, 0.745), F('grey_panel'))
for (sx, sy, cx, cy) in ((0.84, 0.03, 0, -0.255), (0.84, 0.03, 0, 0.255), (0.03, 0.48, -0.405, 0), (0.03, 0.48, 0.405, 0)):
    b.box((sx, sy, 0.07), (cx, cy, 0.79), F('grey_panel'))
FY = -0.25 - 0.008
b.box((0.70, 0.016, 0.12), (0, FY, 0.62), F('grey_drawer'))
b.box((0.26, 0.03, 0.022), (0, FY - 0.02, 0.64), F('chrome'))
b.box((0.70, 0.016, 0.34), (0, FY, 0.36), F('grey_drawer'))
b.box((0.30, 0.03, 0.025), (0, FY - 0.02, 0.48), F('chrome'))
uhandle(b, (0.40, -0.14, 0.64), (0.40, 0.14, 0.64), (1, 0, 0), 0.07, 0.015, 'chrome')
S.part(m, "Cart_Tools_Body", b)
it = S.new()
TZ = 0.76
bottle(it, -0.26, -0.06, TZ, 0.045, 0.22, 'can_red'); bottle(it, -0.14, 0.08, TZ, 0.045, 0.20, 'can_red')
it.box((0.24, 0.035, 0.015), (0.10, -0.05, TZ + 0.008), F('chrome'), rot=(0, 0, 0.5))
it.box((0.20, 0.03, 0.015), (0.22, 0.06, TZ + 0.008), F('chrome'), rot=(0, 0, -0.3))
it.box((0.16, 0.025, 0.02), (0.05, 0.10, TZ + 0.01), F('red_flat'), rot=(0, 0, 0.25))
it.box((0.16, 0.025, 0.02), (0.05, 0.10, TZ + 0.01), F('red_flat'), rot=(0, 0, -0.25))
S.part(m, "Cart_Tools_Items", it)

# ---- Workbench with pegboard and red vise
m = S.model("Workbench", G, 1.9)
b = S.new()
TOP = 0.92
b.box((1.60, 0.72, 0.08), (0, 0, TOP - 0.04), Dr('board_hn', 'board_hn', 'wood_top', 'wood'), bevel=0.02)
LEG = Dr('wood_leg', 'wood_leg', 'endgrain', 'wood')
for x in (-0.72, 0.72):
    for y in (-0.28, 0.28):
        b.box((0.10, 0.10, TOP - 0.08), (x, y, (TOP - 0.08) / 2), LEG, bevel=0.015)
    b.box((0.10, 0.10, 0.64), (x, 0.28, TOP + 0.32), LEG, bevel=0.015)                # posts for the pegboard
b.box((1.46, 0.58, 0.04), (0, 0, 0.24), Dr('board_hn', 'board_hn', 'wood_top', 'wood'))
for y in (-0.30, 0.30):
    board(b, (1.36, 0.04, 0.10), (0, y, TOP - 0.13), bolts=False)
b.box((1.36, 0.04, 0.54), (0, 0.29, TOP + 0.33), Dr('pegboard', 'board_vn', 'endgrain', 'wood', back='board_vn'))
board(b, (1.58, 0.08, 0.08), (0, 0.28, TOP + 0.66))
S.part(m, "Workbench_Body", b)
v = S.new()                                                                           # red vise
VX, VY = 0.60, -0.28
VR = Dr('red_flat', 'red_flat', 'red_flat', 'red')
v.box((0.18, 0.16, 0.05), (VX, VY, TOP + 0.025), VR, bevel=0.01)
v.box((0.14, 0.20, 0.10), (VX, VY - 0.02, TOP + 0.10), VR, bevel=0.012)
v.box((0.20, 0.04, 0.11), (VX, VY - 0.14, TOP + 0.12), VR)
v.box((0.20, 0.04, 0.11), (VX, VY + 0.08, TOP + 0.12), VR)
v.cyl(0.015, 0.20, (VX, VY - 0.22, TOP + 0.10), F('chrome'), segs=6, rot=(PI / 2, 0, 0))
v.cyl(0.012, 0.18, (VX, VY - 0.32, TOP + 0.10), F('chrome'), segs=6, rot=(0, PI / 2, 0))
can(v, -0.55, 0.10, TOP, 0.07, 0.18, 'can_blue')
v.box((0.24, 0.035, 0.015), (-0.20, -0.12, TOP + 0.008), F('chrome'), rot=(0, 0, 0.4))
v.box((0.20, 0.03, 0.015), (0.05, -0.05, TOP + 0.008), F('chrome'), rot=(0, 0, -0.2))
small_box(v, (0.34, 0.22, 0.17), (-0.35, -0.02, 0.26), 'blue', 'blue', 'grey')
v.box((0.16, 0.03, 0.03), (-0.35, -0.02, 0.26 + 0.19), F('black'))
small_box(v, (0.34, 0.26, 0.20), (0.25, 0.0, 0.26), 'wood_h', 'wood_h', 'wood')
S.part(m, "Workbench_Items", v)

# ---- Shelf_Metal: grey rack with a hard hat, cans and boxes
m = S.model("Shelf_Metal", G, 1.2)
b = S.new()
W, D, H = 1.0, 0.45, 1.55
POSTS = Dr('grey_post', 'grey_post', 'grey_panel', 'grey')
for x in (-(W / 2 - 0.03), W / 2 - 0.03):
    for y in (-(D / 2 - 0.03), D / 2 - 0.03):
        b.box((0.06, 0.06, H), (x, y, H / 2), POSTS, bevel=0.01)
SH = (0.10, 0.52, 0.95, 1.40)
for z in SH:
    b.box((W - 0.02, D - 0.02, 0.04), (0, 0, z), F('grey_panel'))
S.part(m, "Shelf_Metal_Body", b)
it = S.new()
z = SH[2] + 0.02
it.lathe([(0, 0), (0.19, 0), (0.19, 0.018), (0.14, 0.028), (0.135, 0.09), (0.10, 0.15), (0.05, 0.175), (0, 0.18)],
         8, ('remap', 'hardhat'), center=(-0.26, -0.03, z))
can(it, 0.08, -0.02, z, 0.075, 0.13, 'can_grey'); can(it, 0.28, -0.01, z, 0.075, 0.13, 'can_grey')
z = SH[1] + 0.02
can(it, -0.28, -0.03, z, 0.09, 0.16, 'can_grey')
small_box(it, (0.34, 0.26, 0.20), (0.18, -0.01, z), 'box_olive', 'olive_flat', 'olive')
z = SH[0] + 0.02
can(it, -0.40, -0.05, z, 0.05, 0.10, 'can_blue')
small_box(it, (0.30, 0.28, 0.17), (-0.16, -0.01, z), 'box_tan', 'tan', 'card')
small_box(it, (0.34, 0.28, 0.19), (0.22, -0.02, z), 'box_red', 'red_flat', 'red')
S.part(m, "Shelf_Metal_Items", it)

stats = S.export(os.path.join(C.SRC, "stats_Cargo.json"))
C.save_all()
print("DONE", {k: v["tris"] for k, v in stats.items()})
