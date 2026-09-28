"""Draws the game's small pictograms (level-up cards, workshop upgrades, HUD counters, weapon slots) in the painted
cartoon style of the HUD: thick dark-brown outline, warm colours, soft shading, a gloss on round parts.

Writes unity/Assets/Art/Textures/UI/UI_Icon_<Name>.png (192 x 192, transparent). Drawn on a 256-unit grid.
Run: python tools/ui/make_icons.py
"""
import math
import os

from PIL import ImageChops

from paint import Painter, rotate_points, star_points

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
OUT = os.path.join(ROOT, "unity", "Assets", "Art", "Textures", "UI")
SIZE = 256            # drawing grid
OUT_SIZE = 192        # saved size (multiple of 4: block compression)

BRASS = (236, 180, 76)
COPPER = (214, 112, 58)
STEEL = (178, 188, 202)
DARK_STEEL = (92, 102, 118)
WOOD = (176, 108, 56)
RED = (226, 62, 52)
GREEN = (104, 202, 78)
BLUE = (78, 152, 232)
WHITE = (246, 241, 230)
YELLOW = (255, 212, 70)
ORANGE = (242, 132, 40)
BLACK = (52, 50, 56)
BONE = (238, 230, 208)


def new():
    return Painter(SIZE, ss=4, outline=7)


def save(p, name):
    p.save(os.path.join(OUT, f"UI_Icon_{name}.png"), (OUT_SIZE, OUT_SIZE))
    print("UI_Icon_" + name)


# ---------------------------------------------------------------------------------------------------- parts
def bullet(p, cx, cy, length, width, angle, casing=BRASS, tip=COPPER):
    """A cartridge centred at (cx, cy), pointing at `angle` degrees (0 = up, clockwise)."""
    h = length
    w = width
    top = cy - h / 2
    tip_len = h * 0.38
    rim = h * 0.07
    case_pts = [(cx - w / 2, top + tip_len), (cx + w / 2, top + tip_len), (cx + w / 2, cy + h / 2 - rim),
                (cx - w / 2, cy + h / 2 - rim)]
    rim_pts = [(cx - w / 2 - w * 0.08, cy + h / 2 - rim), (cx + w / 2 + w * 0.08, cy + h / 2 - rim),
               (cx + w / 2 + w * 0.08, cy + h / 2), (cx - w / 2 - w * 0.08, cy + h / 2)]
    ogive = []
    for i in range(0, 21):
        t = i / 20.0
        a = math.pi * t
        x = cx - (w / 2) * math.cos(a)
        y = top + tip_len - tip_len * math.sin(a) ** 0.75
        ogive.append((x, y))
    rot = lambda pts: rotate_points(pts, (cx, cy), angle)
    p.fill(p.poly_mask(rot(case_pts)), casing, gloss=0.35)
    p.fill(p.poly_mask(rot(rim_pts)), tuple(int(c * 0.8) for c in casing))
    p.fill(p.poly_mask(rot(ogive)), tip, gloss=0.4)


def plus_badge(p, cx, cy, r, color=GREEN):
    p.fill(p.ellipse_mask((cx - r, cy - r, cx + r, cy + r)), color, gloss=0.3)
    t = r * 0.28
    arm = r * 0.62
    cross = p.poly_mask([(cx - t, cy - arm), (cx + t, cy - arm), (cx + t, cy - t), (cx + arm, cy - t), (cx + arm, cy + t),
                         (cx + t, cy + t), (cx + t, cy + arm), (cx - t, cy + arm), (cx - t, cy + t), (cx - arm, cy + t),
                         (cx - arm, cy - t), (cx - t, cy - t)])
    p.fill(cross, WHITE, shade=0.05, outline=False)


def heart_points(cx, cy, s):
    pts = []
    for i in range(0, 64):
        t = math.pi * 2 * i / 64
        x = 16 * math.sin(t) ** 3
        y = -(13 * math.cos(t) - 5 * math.cos(2 * t) - 2 * math.cos(3 * t) - math.cos(4 * t))
        pts.append((cx + x * s, cy + y * s))
    return pts


def axe(p, cx, cy, scale=1.0, angle=-35.0):
    """Wooden handle with a steel head, head up-left of the grip."""
    s = scale
    handle = [(cx - 9 * s, cy - 92 * s), (cx + 9 * s, cy - 92 * s), (cx + 12 * s, cy + 96 * s), (cx - 12 * s, cy + 96 * s)]
    head = [(cx - 6 * s, cy - 96 * s), (cx - 60 * s, cy - 112 * s), (cx - 78 * s, cy - 84 * s), (cx - 82 * s, cy - 44 * s),
            (cx - 62 * s, cy - 22 * s), (cx - 6 * s, cy - 44 * s)]
    back = [(cx + 6 * s, cy - 88 * s), (cx + 30 * s, cy - 82 * s), (cx + 30 * s, cy - 56 * s), (cx + 6 * s, cy - 52 * s)]
    edge = [(cx - 60 * s, cy - 112 * s), (cx - 78 * s, cy - 84 * s), (cx - 82 * s, cy - 44 * s), (cx - 62 * s, cy - 22 * s),
            (cx - 70 * s, cy - 44 * s), (cx - 67 * s, cy - 82 * s)]
    grip = [(cx - 13 * s, cy + 50 * s), (cx + 13 * s, cy + 50 * s), (cx + 14 * s, cy + 90 * s), (cx - 14 * s, cy + 90 * s)]
    rot = lambda pts: rotate_points(pts, (cx, cy), angle)
    p.fill(p.poly_mask(rot(handle)), WOOD, gloss=0.25)
    p.fill(p.poly_mask(rot(grip)), (120, 64, 40))
    p.fill(p.poly_mask(rot(back)), DARK_STEEL)
    p.fill(p.poly_mask(rot(head)), STEEL, gloss=0.35)
    p.fill(p.poly_mask(rot(edge)), (236, 242, 248), shade=0.05, outline=False)


def wrench(p, cx, cy, angle=45.0, scale=1.0):
    s = scale
    shaft = [(cx - 13 * s, cy - 58 * s), (cx + 13 * s, cy - 58 * s), (cx + 13 * s, cy + 70 * s), (cx - 13 * s, cy + 70 * s)]
    rot = lambda pts: rotate_points(pts, (cx, cy), angle)
    p.fill(p.poly_mask(rot(shaft)), STEEL, gloss=0.3)
    # Open jaw at the top: a disc with a notch; ring at the bottom.
    jx, jy = rotate_points([(cx, cy - 72 * s)], (cx, cy), angle)[0]
    jaw = p.ellipse_mask((jx - 36 * s, jy - 36 * s, jx + 36 * s, jy + 36 * s))
    notch = p.poly_mask(rot([(cx - 14 * s, cy - 118 * s), (cx + 14 * s, cy - 118 * s), (cx + 14 * s, cy - 70 * s),
                             (cx - 14 * s, cy - 70 * s)]))
    p.fill(ImageChops.subtract(jaw, notch), STEEL, gloss=0.3)
    rx, ry = rotate_points([(cx, cy + 78 * s)], (cx, cy), angle)[0]
    ring = p.ellipse_mask((rx - 30 * s, ry - 30 * s, rx + 30 * s, ry + 30 * s))
    hole = p.ellipse_mask((rx - 13 * s, ry - 13 * s, rx + 13 * s, ry + 13 * s))
    p.fill(ImageChops.subtract(ring, hole), STEEL, gloss=0.3)


def gear(p, cx, cy, r, teeth=8, color=DARK_STEEL):
    pts = []
    for i in range(teeth * 4):
        a = math.pi * 2 * i / (teeth * 4)
        rr = r if (i % 4) in (0, 1) else r * 0.76
        pts.append((cx + rr * math.cos(a), cy + rr * math.sin(a)))
    body = p.poly_mask(pts)
    hole = p.ellipse_mask((cx - r * 0.32, cy - r * 0.32, cx + r * 0.32, cy + r * 0.32))
    p.fill(ImageChops.subtract(body, hole), color, gloss=0.25)


def crate(p, box):
    x0, y0, x1, y1 = box
    p.fill(p.rrect_mask(box, 8), WOOD, gloss=0.15)
    for t in (0.36, 0.68):
        y = y0 + (y1 - y0) * t
        p.fill(p.line_mask([(x0 + 6, y), (x1 - 6, y)], 3, False), (120, 68, 36), outline=False)
    p.fill(p.line_mask([(x0 + 10, y1 - 10), (x1 - 10, y0 + 10)], 9), (150, 90, 46), outline_width=3)


def car_side(p, cx, cy, s=1.0, color=ORANGE):
    body = [(cx - 96 * s, cy - 6 * s), (cx - 88 * s, cy - 26 * s), (cx - 44 * s, cy - 30 * s), (cx - 26 * s, cy - 62 * s),
            (cx + 40 * s, cy - 62 * s), (cx + 62 * s, cy - 28 * s), (cx + 94 * s, cy - 22 * s), (cx + 98 * s, cy + 16 * s),
            (cx - 96 * s, cy + 16 * s)]
    p.fill(p.poly_mask(body), color, gloss=0.3)
    window = [(cx - 16 * s, cy - 52 * s), (cx + 34 * s, cy - 52 * s), (cx + 50 * s, cy - 28 * s), (cx - 30 * s, cy - 28 * s)]
    p.fill(p.poly_mask(window), (150, 210, 240), shade=0.25, outline_width=4)
    for wx in (-54, 56):
        x = cx + wx * s
        p.fill(p.ellipse_mask((x - 26 * s, cy - 8 * s, x + 26 * s, cy + 44 * s)), BLACK)
        p.fill(p.ellipse_mask((x - 11 * s, cy + 7 * s, x + 11 * s, cy + 29 * s)), STEEL, outline=False)


# ---------------------------------------------------------------------------------------------------- icons
def icon_bullet():
    p = new()
    bullet(p, 128, 132, 190, 62, 28)
    save(p, "Damage")


def icon_fire_rate():
    p = new()
    for i, (y, l) in enumerate(((78, 70), (128, 96), (178, 70))):
        p.fill(p.line_mask([(26, y), (26 + l, y)], 12), (255, 226, 150), shade=0.0, outline_width=4)
    bullet(p, 170, 128, 150, 54, 90)
    save(p, "FireRate")


def icon_reload():
    p = new()
    p.fill(p.arc_mask((128, 128), 92, 200, 500, 26), GREEN, gloss=0.2)
    # Arrow head at the end of the arc (500 deg = 140 deg).
    a = math.radians(140)
    ex, ey = 128 + 92 * math.cos(a), 128 + 92 * math.sin(a)
    tang = a + math.pi / 2
    head = [(ex + 34 * math.cos(a), ey + 34 * math.sin(a)), (ex - 34 * math.cos(a), ey - 34 * math.sin(a)),
            (ex + 40 * math.cos(tang), ey + 40 * math.sin(tang))]
    p.fill(p.poly_mask(head), GREEN)
    mag = [(104, 70), (150, 70), (160, 186), (116, 190)]
    p.fill(p.poly_mask(mag), DARK_STEEL, gloss=0.3)
    save(p, "Reload")


def icon_magazine():
    p = new()
    mag = []
    for i in range(0, 21):
        t = i / 20
        mag.append((86 + 40 * t ** 1.6, 40 + 170 * t))
    for i in range(20, -1, -1):
        t = i / 20
        mag.append((150 + 44 * t ** 1.6, 40 + 170 * t))
    p.fill(p.poly_mask(mag), DARK_STEEL, gloss=0.3)
    p.fill(p.rrect_mask((80, 28, 156, 50), 6), BRASS)
    for y in (90, 130, 170):
        p.fill(p.line_mask([(104 + (y - 40) * 0.12, y), (160 + (y - 40) * 0.18, y)], 5, False), (70, 78, 92), outline=False)
    plus_badge(p, 196, 64, 40)
    save(p, "Magazine")


def icon_ammo_box():
    p = new()
    for x, a in ((88, -10), (128, 0), (168, 10)):
        bullet(p, x, 96, 120, 36, a)
    crate(p, (40, 120, 216, 222))
    save(p, "AmmoBox")


def icon_pierce():
    p = new()
    for cx, cy in ((104, 130), (170, 130)):
        p.fill(p.ellipse_mask((cx - 40, cy - 44, cx + 40, cy + 44)), (126, 176, 96), gloss=0.2)
        p.fill(p.ellipse_mask((cx - 22, cy - 18, cx - 6, cy - 2)), WHITE, outline_width=3)
        p.fill(p.ellipse_mask((cx + 6, cy - 18, cx + 22, cy - 2)), WHITE, outline_width=3)
    p.fill(p.line_mask([(18, 130), (190, 130)], 12), (255, 230, 150), shade=0.0, outline_width=4)
    bullet(p, 214, 130, 70, 30, 90)
    save(p, "Pierce")


def icon_crit():
    p = new()
    p.fill(p.arc_mask((128, 128), 84, 0, 360, 18), RED)
    for (x0, y0, x1, y1) in ((128, 18, 128, 70), (128, 186, 128, 238), (18, 128, 70, 128), (186, 128, 238, 128)):
        p.fill(p.line_mask([(x0, y0), (x1, y1)], 14, False), RED)
    p.fill(p.poly_mask(star_points(128, 130, 60, 26)), YELLOW, gloss=0.4)
    save(p, "Crit")


def icon_range():
    p = new()
    for r, c in ((78, RED), (54, WHITE), (30, RED)):
        p.fill(p.ellipse_mask((176 - r * 0.62, 128 - r, 176 + r * 0.62, 128 + r)), c)
    for i in range(4):
        x = 20 + i * 30
        p.fill(p.line_mask([(x, 150 - i * 5), (x + 16, 147 - i * 5)], 9), (255, 226, 150), outline_width=3)
    bullet(p, 146, 132, 64, 26, 80)
    save(p, "Range")


def icon_heart():
    p = new()
    p.fill(p.poly_mask(heart_points(122, 134, 6.4)), RED, gloss=0.45)
    plus_badge(p, 196, 66, 38)
    save(p, "Health")


def icon_regen():
    p = new()
    p.fill(p.poly_mask(heart_points(128, 134, 6.6)), RED, gloss=0.45)
    t, arm, cx, cy = 13, 36, 128, 128
    p.fill(p.poly_mask([(cx - t, cy - arm), (cx + t, cy - arm), (cx + t, cy - t), (cx + arm, cy - t), (cx + arm, cy + t),
                        (cx + t, cy + t), (cx + t, cy + arm), (cx - t, cy + arm), (cx - t, cy + t), (cx - arm, cy + t),
                        (cx - arm, cy - t), (cx - t, cy - t)]), (200, 255, 190), outline_width=4)
    for (sx, sy, r) in ((40, 50, 20), (214, 52, 16), (212, 206, 14)):
        p.fill(p.poly_mask(star_points(sx, sy, r, r * 0.4, 4)), (180, 255, 150), outline_width=3)
    save(p, "Regen")


def icon_speed():
    p = new()
    boot = [(86, 50), (148, 50), (152, 142), (214, 160), (222, 196), (216, 214), (80, 214), (78, 150)]
    p.fill(p.poly_mask(boot), (150, 92, 50), gloss=0.25)
    p.fill(p.rrect_mask((74, 196, 224, 222), 8), (70, 50, 40))
    p.fill(p.rrect_mask((80, 42, 154, 68), 6), (120, 70, 40))
    for y, l in ((84, 60), (120, 74), (156, 56)):
        p.fill(p.line_mask([(10, y), (10 + l, y)], 11), (255, 226, 150), outline_width=4)
    save(p, "Speed")


def icon_vampire():
    p = new()
    drop = []
    for i in range(0, 41):
        t = math.pi * 2 * i / 40
        r = 74
        x = 128 + r * math.sin(t) * (0.9 if math.cos(t) < 0 else 1.0)
        y = 150 + r * math.cos(t)
        drop.append((x, y))
    top = [(128, 18), (190, 132), (66, 132)]
    m = ImageChops.add(p.poly_mask(drop), p.poly_mask(top))
    p.fill(m, (200, 30, 40), gloss=0.45)
    plus_badge(p, 128, 158, 36, (255, 120, 120))
    save(p, "Vampire")


def icon_axe():
    p = new()
    axe(p, 146, 134, 1.0, -30)
    save(p, "Axe")


def icon_axe_range():
    p = new()
    p.fill(p.arc_mask((128, 140), 100, 190, 330, 16), (255, 246, 214), shade=0.0, outline_width=4)
    p.fill(p.arc_mask((128, 140), 76, 205, 315, 9), (255, 230, 160), shade=0.0, outline_width=3)
    axe(p, 150, 150, 0.8, -40)
    save(p, "AxeRange")


def icon_plate():
    p = new()
    plate = [(40, 44), (216, 44), (216, 170), (128, 226), (40, 170)]
    p.fill(p.poly_mask(plate), STEEL, gloss=0.35)
    inner = [(62, 64), (194, 64), (194, 158), (128, 200), (62, 158)]
    p.fill(p.poly_mask(inner), (150, 162, 180), outline_width=4)
    for x, y in ((56, 58), (200, 58), (56, 160), (200, 160), (128, 212)):
        p.fill(p.ellipse_mask((x - 9, y - 9, x + 9, y + 9)), DARK_STEEL, outline_width=3)
    plus_badge(p, 128, 118, 40)
    save(p, "CarPlate")


def icon_car_regen():
    p = new()
    gear(p, 170, 86, 52)
    wrench(p, 112, 140, 45, 0.9)
    save(p, "CarRegen")


def icon_nut():
    p = new()
    hexagon = [(128 + 96 * math.cos(math.radians(a)), 128 + 96 * math.sin(math.radians(a))) for a in range(30, 390, 60)]
    body = p.poly_mask(hexagon)
    hole = p.ellipse_mask((88, 88, 168, 168))
    p.fill(ImageChops.subtract(body, hole), BRASS, gloss=0.45)
    p.fill(p.arc_mask((128, 128), 40, 0, 360, 8), (170, 120, 40), outline=False)
    save(p, "Scrap")


def icon_money_gain():
    p = new()
    for cx, cy, r in ((96, 158, 66), (168, 110, 58)):
        hexagon = [(cx + r * math.cos(math.radians(a)), cy + r * math.sin(math.radians(a))) for a in range(30, 390, 60)]
        body = p.poly_mask(hexagon)
        hole = p.ellipse_mask((cx - r * 0.4, cy - r * 0.4, cx + r * 0.4, cy + r * 0.4))
        p.fill(ImageChops.subtract(body, hole), BRASS, gloss=0.45)
    plus_badge(p, 204, 204, 36)
    save(p, "ScrapGain")


def icon_xp():
    p = new()
    p.fill(p.poly_mask(star_points(128, 136, 108, 46)), (90, 170, 255), gloss=0.5)
    p.fill(p.poly_mask(star_points(128, 136, 50, 22)), (200, 234, 255), outline=False)
    save(p, "Xp")


def icon_bomb():
    p = new()
    p.fill(p.ellipse_mask((40, 70, 196, 226)), BLACK, gloss=0.45)
    p.fill(p.rrect_mask((136, 58, 184, 92), 6), DARK_STEEL)
    p.fill(p.line_mask([(166, 62), (190, 36), (214, 40)], 8), (190, 150, 110), outline_width=3)
    p.fill(p.poly_mask(star_points(220, 36, 30, 12, 8)), YELLOW, outline_width=4)
    save(p, "Bomb")


def icon_medkit():
    p = new()
    p.fill(p.rrect_mask((92, 36, 164, 70), 14), (200, 196, 190))
    p.fill(p.rrect_mask((96, 46, 160, 64), 6), (120, 116, 110), outline=False)
    p.fill(p.rrect_mask((28, 62, 228, 214), 22), WHITE, gloss=0.3)
    t, arm, cx, cy = 20, 54, 128, 138
    p.fill(p.poly_mask([(cx - t, cy - arm), (cx + t, cy - arm), (cx + t, cy - t), (cx + arm, cy - t), (cx + arm, cy + t),
                        (cx + t, cy + t), (cx + t, cy + arm), (cx - t, cy + arm), (cx - t, cy + t), (cx - arm, cy + t),
                        (cx - arm, cy - t), (cx - t, cy - t)]), RED, outline_width=4)
    save(p, "Medkit")


def icon_shield():
    p = new()
    # Heater shield: straight top with rounded corners, sides curving down to a point.
    shield = [(44, 50), (84, 38), (128, 30), (172, 38), (212, 50)]
    for i in range(1, 21):
        t = i / 20
        shield.append((212 - 84 * t ** 2.2, 50 + 186 * t))
    for i in range(19, 0, -1):
        t = i / 20
        shield.append((44 + 84 * t ** 2.2, 50 + 186 * t))
    p.fill(p.poly_mask(shield), BLUE, gloss=0.35)
    inner = [(128 + (x - 128) * 0.72, 118 + (y - 118) * 0.72) for x, y in shield]
    p.fill(p.poly_mask(inner), (120, 180, 245), outline_width=4)
    chevron = [(84, 122), (128, 94), (172, 122), (172, 150), (128, 122), (84, 150)]
    p.fill(p.poly_mask(chevron), WHITE, outline_width=4)
    save(p, "Armor")


def icon_hull():
    p = new()
    car_side(p, 124, 150, 1.0)
    plus_badge(p, 200, 64, 38)
    save(p, "Hull")


def icon_spikes():
    p = new()
    for y in (74, 128, 182):
        p.fill(p.poly_mask([(118, y - 24), (232, y), (118, y + 24)]), STEEL, gloss=0.3)
    p.fill(p.rrect_mask((28, 36, 128, 220), 16), (90, 96, 110), gloss=0.2)
    for y in (70, 128, 186):
        p.fill(p.ellipse_mask((66, y - 12, 90, y + 12)), DARK_STEEL, outline_width=3)
    save(p, "Spikes")


def icon_shock():
    p = new()
    bolt = [(150, 14), (58, 142), (118, 142), (92, 242), (200, 102), (138, 102), (178, 14)]
    p.fill(p.poly_mask(bolt), YELLOW, gloss=0.45)
    save(p, "Shock")


def icon_repair():
    p = new()
    wrench(p, 128, 128, -45, 1.0)
    save(p, "Repair")


def icon_skull():
    p = new()
    head = p.ellipse_mask((44, 26, 212, 186))
    jaw = p.rrect_mask((84, 150, 172, 226), 16)
    p.fill(ImageChops.add(head, jaw), BONE, gloss=0.3)
    for cx in (96, 160):
        p.fill(p.ellipse_mask((cx - 26, 88, cx + 26, 140)), (44, 34, 30), outline=False)
    p.fill(p.poly_mask([(128, 148), (116, 170), (140, 170)]), (44, 34, 30), outline=False)
    for x in (104, 128, 152):
        p.fill(p.line_mask([(x, 186), (x, 220)], 5, False), (120, 100, 80), outline=False)
    save(p, "Skull")


def icon_rifle():
    p = new()
    body = [(18, 116), (160, 110), (236, 112), (236, 128), (160, 134), (118, 136), (100, 160), (70, 160), (62, 136),
            (18, 138)]
    p.fill(p.poly_mask(body), DARK_STEEL, gloss=0.25)
    stock = [(10, 112), (62, 118), (62, 146), (26, 170), (8, 164)]
    p.fill(p.poly_mask(stock), WOOD, gloss=0.2)
    guard = [(150, 110), (206, 110), (206, 132), (150, 134)]
    p.fill(p.poly_mask(guard), WOOD, gloss=0.2)
    mag = [(112, 134), (140, 134), (150, 200), (124, 206)]
    p.fill(p.poly_mask(mag), (70, 78, 92))
    save(p, "Rifle")


def icon_boss():
    p = new()
    for side in (-1, 1):
        horn = [(128 + side * 50, 70), (128 + side * 104, 14), (128 + side * 82, 84)]
        p.fill(p.poly_mask(horn), BONE)
    head = p.ellipse_mask((48, 36, 208, 190))
    jaw = p.rrect_mask((86, 152, 170, 228), 16)
    p.fill(ImageChops.add(head, jaw), (220, 70, 60), gloss=0.3)
    for cx in (98, 158):
        p.fill(p.poly_mask([(cx - 26, 96), (cx + 26, 104), (cx + 20, 136), (cx - 20, 132)]), (255, 214, 70), outline_width=4)
    for x in (106, 128, 150):
        p.fill(p.line_mask([(x, 190), (x, 222)], 5, False), (120, 40, 40), outline=False)
    save(p, "Boss")


if __name__ == "__main__":
    for fn in (icon_bullet, icon_fire_rate, icon_reload, icon_magazine, icon_ammo_box, icon_pierce, icon_crit, icon_range,
               icon_heart, icon_regen, icon_speed, icon_vampire, icon_axe, icon_axe_range, icon_plate, icon_car_regen,
               icon_nut, icon_money_gain, icon_xp, icon_bomb, icon_medkit, icon_shield, icon_hull, icon_spikes,
               icon_shock, icon_repair, icon_skull, icon_rifle, icon_boss):
        fn()
