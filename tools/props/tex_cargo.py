"""Sheet 4 atlas: cargo + furniture (cartoon).  python tex_cargo.py"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from tex_common import *  # noqa
from layout_cargo import *  # noqa

A = Image.new("RGB", (ATLAS, ATLAS), (60, 60, 60))
WOOD = (0.90, 0.47, 0.17); WOOD_D = (0.80, 0.40, 0.14)
RED = (0.90, 0.16, 0.10); OLIVE = (0.52, 0.56, 0.20); GREY = (0.55, 0.57, 0.62)
RUST = (0.66, 0.32, 0.10)


def reg(k):
    return REG[k]


def P(k, img):
    put(A, REG[k], img)


def painted(w, h, col, seed, rust=0.76, scratches=30, edge=10, hi=0.3, lo=0.4):
    img = base(w, h, col, seed, amt=0.12, cells=3, grain=0.01)
    img = blotches(img, seed + 1, rust, RUST, None, 9)
    img = blotches(img, seed + 2, rust + 0.05, (0.40, 0.18, 0.07), None, 9)
    im = to_img(bevel_edges(img, edge, hi, lo)); d = ImageDraw.Draw(im)
    r = np.random.default_rng(seed + 3)
    lite = tint(tuple(int(c * 255) for c in col), 1.35)
    for _ in range(scratches):
        x, y = r.uniform(0, w), r.uniform(0, h); l = r.uniform(6, 22)
        d.line([(x, y), (x + l, y + r.uniform(-4, 4))], fill=lite, width=1)
    return im


def rivets(d, pts, r=6):
    for (x, y) in pts:
        d.ellipse([x - r - 1, y - r + 1, x + r + 1, y + r + 3], fill=(40, 30, 25))
        d.ellipse([x - r, y - r, x + r, y + r], fill=(95, 98, 108))
        d.ellipse([x - r * 0.6, y - r * 0.6, x - r * 0.05, y - r * 0.1], fill=(200, 205, 215))


# ------------------------------------------------------------------ wood
x, y, w, h = reg("wood_v"); P("wood_v", ao_edges(toon_wood(w, h, WOOD_D, 1, vertical=True, planks=4, strokes=80,
                                                          cracks=5, knots=2, notches=3, edge=14), 0.3, 0.1))
x, y, w, h = reg("wood_h"); P("wood_h", ao_edges(toon_wood(w, h, WOOD_D, 2, vertical=False, planks=4, strokes=80,
                                                          cracks=5, knots=2, notches=3, edge=14), 0.3, 0.1))
for k, vert, bolts, seed in (("board_h", False, True, 3), ("board_hn", False, False, 4), ("board_v", True, True, 5),
                             ("board_vn", True, False, 6)):
    x, y, w, h = reg(k)
    im = to_img(toon_wood(w, h, WOOD, seed, vertical=vert, strokes=40, cracks=2, knots=1, notches=3, edge=9))
    if bolts:
        d = ImageDraw.Draw(im)
        if vert:
            toon_bolt(d, w / 2, 26, 11); toon_bolt(d, w / 2, h - 26, 11)
        else:
            toon_bolt(d, 26, h / 2, 11); toon_bolt(d, w - 26, h / 2, 11)
    P(k, im)
x, y, w, h = reg("wood_top"); P("wood_top", toon_wood(w, h, WOOD, 7, vertical=False, planks=3, strokes=90, cracks=6,
                                                      knots=2, notches=6, edge=10))
x, y, w, h = reg("wood_leg"); P("wood_leg", ao_bottom(toon_wood(w, h, WOOD_D, 8, vertical=True, strokes=30, cracks=2,
                                                                knots=1, notches=2, edge=8), 0.35, 0.85))

# ------------------------------------------------------------------ olive military box
x, y, w, h = reg("olive"); P("olive", painted(w, h, OLIVE, 10, rust=0.74))
x, y, w, h = reg("olive_face")
im = painted(w, h, OLIVE, 11, rust=0.75); d = ImageDraw.Draw(im)
d.rectangle([34, 34, w - 35, h - 35], outline=tint((133, 143, 51), 1.3), width=4)
d.rectangle([38, 38, w - 39, h - 39], outline=tint((133, 143, 51), 0.6), width=3)
d.line([(40, 40), (w - 40, h - 40)], fill=tint((133, 143, 51), 0.75), width=3)
rivets(d, [(18, 18), (w - 18, 18), (18, h - 18), (w - 18, h - 18)])
P("olive_face", im)

# ------------------------------------------------------------------ cardboard
CARD = (0.82, 0.58, 0.33)
for k, top, seed in (("card_side", False, 20), ("card_top", True, 21)):
    x, y, w, h = reg(k)
    img = base(w, h, CARD, seed, amt=0.14, cells=4)
    img = blotches(img, seed + 1, 0.74, (0.70, 0.46, 0.24), None, 8)
    im = to_img(bevel_edges(img, 10, 0.2, 0.3)); d = ImageDraw.Draw(im)
    tape = (232, 196, 132)
    if top:
        d.rectangle([0, h * 0.40, w, h * 0.60], fill=tape)
        d.line([(0, h * 0.40), (w, h * 0.40)], fill=(190, 150, 90), width=2)
        d.line([(0, h * 0.5), (w, h * 0.5)], fill=(150, 100, 55), width=2)          # flap seam under tape
    else:
        d.rectangle([w * 0.40, 0, w * 0.60, h * 0.35], fill=tape)
        ink = (70, 45, 28)                                                         # "this side up" + glass icons
        for i, xx in enumerate((w * 0.18, w * 0.27)):
            d.line([(xx, h * 0.88), (xx, h * 0.74)], fill=ink, width=4)
            d.polygon([(xx - 9, h * 0.76), (xx + 9, h * 0.76), (xx, h * 0.70)], fill=ink)
        gx, gy = w * 0.78, h * 0.78
        d.polygon([(gx - 12, gy - 14), (gx + 12, gy - 14), (gx + 5, gy + 2), (gx - 5, gy + 2)], outline=ink, width=3)
        d.line([(gx, gy + 2), (gx, gy + 12)], fill=ink, width=3); d.line([(gx - 8, gy + 13), (gx + 8, gy + 13)], fill=ink, width=3)
        d.rectangle([w * 0.06, h * 0.06, w * 0.36, h * 0.22], outline=ink, width=2)
    P(k, im)

# ------------------------------------------------------------------ jerrycans
for k, col, seed in (("jerry_red", RED, 30), ("jerry_green", (0.56, 0.62, 0.20), 31)):
    x, y, w, h = reg(k)
    im = painted(w, h, col, seed, rust=0.78, scratches=20); d = ImageDraw.Draw(im)
    c255 = tuple(int(v * 255) for v in col)
    d.rectangle([22, 22, w - 23, h - 23], outline=tint(c255, 0.62), width=6)
    d.rectangle([28, 28, w - 29, h - 29], outline=tint(c255, 1.3), width=3)
    for off, cc in ((4, tint(c255, 0.55)), (0, tint(c255, 1.28))):                  # raised X emboss
        d.line([(48 + off, 48 + off), (w - 48 + off, h - 48 + off)], fill=cc, width=16)
        d.line([(w - 48 + off, 48 + off), (48 + off, h - 48 + off)], fill=cc, width=16)
    d.rectangle([w * 0.38, h * 0.38, w * 0.62, h * 0.62], outline=tint(c255, 0.6), width=5)
    P(k, im)

# ------------------------------------------------------------------ painted metal furniture
x, y, w, h = reg("red_panel"); im = painted(w, h, RED, 40); d = ImageDraw.Draw(im)
rivets(d, [(16, 16), (w - 16, 16), (16, h - 16), (w - 16, h - 16)], 5); P("red_panel", im)
x, y, w, h = reg("grey_panel"); im = painted(w, h, GREY, 41, rust=0.70); d = ImageDraw.Draw(im)
rivets(d, [(16, 16), (w - 16, 16), (16, h - 16), (w - 16, h - 16)], 5); P("grey_panel", im)
for k, col, seed in (("red_drawer", RED, 42), ("grey_drawer", GREY, 43)):
    x, y, w, h = reg(k)
    im = painted(w, h, col, seed, rust=0.8, edge=12, hi=0.35, lo=0.5); d = ImageDraw.Draw(im)
    c255 = tuple(int(v * 255) for v in col)
    d.rectangle([12, 12, w - 13, h - 13], outline=tint(c255, 0.7), width=3)
    P(k, im)
x, y, w, h = reg("red_door"); im = painted(w, h, RED, 44, rust=0.76, edge=12, hi=0.35, lo=0.5); d = ImageDraw.Draw(im)
d.rectangle([16, 16, w - 17, h - 17], outline=tint((230, 41, 26), 0.7), width=4)
for i in range(4):                                                                  # vent slots
    d.rounded_rectangle([w * 0.3, h * 0.62 + i * 16, w * 0.7, h * 0.62 + i * 16 + 7], radius=3, fill=(90, 15, 10))
P("red_door", im)

# pegboard with hanging wrenches
x, y, w, h = reg("pegboard")
im = to_img(toon_wood(w, h, (0.80, 0.50, 0.24), 50, vertical=False, strokes=40, cracks=2, knots=1, notches=4, edge=12))
d = ImageDraw.Draw(im)
for yy in range(26, h, 30):
    for xx in range(26, w, 30):
        d.ellipse([xx - 4, yy - 4, xx + 4, yy + 4], fill=(90, 50, 22))
tool = (150, 156, 168); dark = (60, 62, 70)
for i in range(7):
    cx = 120 + i * 85; L = 150 - i * 9
    d.ellipse([cx - 6, 30, cx + 6, 42], fill=dark)                                  # hook
    for off, cc in ((3, dark), (0, tool)):
        d.rounded_rectangle([cx - 8 + off, 60 + off, cx + 8 + off, 60 + L + off], radius=6, fill=cc)
        d.ellipse([cx - 20 + off, 42 + off, cx + 20 + off, 82 + off], fill=cc)
        d.ellipse([cx - 16 + off, 50 + L + off, cx + 16 + off, 82 + L + off], fill=cc)
    d.polygon([(cx - 6, 42), (cx + 6, 42), (cx + 4, 62), (cx - 4, 62)], fill=(150, 90, 45))    # open jaw
    d.ellipse([cx - 7, 58 + L, cx + 7, 72 + L], fill=(150, 90, 45))
    d.line([(cx - 4, 70), (cx - 4, 50 + L)], fill=(215, 220, 230), width=2)
P("pegboard", im)

# cans (u around, v height): metal body + coloured label band
for k, body, label, seed in (("can_grey", (0.62, 0.64, 0.68), (0.95, 0.52, 0.16), 60),
                             ("can_blue", (0.30, 0.46, 0.70), (0.90, 0.88, 0.78), 61),
                             ("can_yellow", (0.78, 0.74, 0.28), (0.95, 0.55, 0.15), 62),
                             ("can_red", (0.88, 0.14, 0.10), (0.95, 0.93, 0.88), 63)):
    x, y, w, h = reg(k)
    img = base(w, h, body, seed, amt=0.1, cells=3)
    v = np.linspace(1, 0, h)[:, None]
    lab = ((v > 0.28) & (v < 0.72)).astype(float)[..., None]
    img = img * (1 - lab) + np.array(label) * lab
    img *= (1 + 0.25 * np.exp(-((v - 0.95) / 0.03) ** 2) - 0.3 * np.exp(-((v - 0.05) / 0.04) ** 2))[..., None]
    img = blotches(img, seed + 1, 0.8, RUST, None, 6)
    im = to_img(img); d = ImageDraw.Draw(im)
    d.rectangle([w * 0.1, h * 0.42, w * 0.45, h * 0.58], fill=tint(tuple(int(c * 255) for c in label), 0.7))
    P(k, im)
x, y, w, h = reg("can_top")
img = base(w, h, (0.66, 0.68, 0.72), 64, amt=0.1)
rr = np.hypot(*(np.mgrid[0:h, 0:w] - (h - 1) / 2)) / (w / 2)
img *= (1 + 0.25 * np.exp(-((rr - 0.9) / 0.05) ** 2) - 0.25 * np.exp(-((rr - 0.78) / 0.04) ** 2))[..., None]
P("can_top", img)
x, y, w, h = reg("hardhat")
img = base(w, h, (1.0, 0.52, 0.10), 65, amt=0.1)
img *= (0.75 + 0.45 * np.linspace(1, 0, h)[:, None])[..., None]
P("hardhat", blotches(img, 66, 0.8, (0.62, 0.30, 0.10), None, 5))
for k, col, seed in (("box_red", RED, 70), ("box_olive", OLIVE, 71), ("box_tan", (0.86, 0.66, 0.38), 72)):
    x, y, w, h = reg(k)
    im = painted(w, h, col, seed, rust=0.8, scratches=8, edge=8); d = ImageDraw.Draw(im)
    d.rectangle([w * 0.42, h * 0.18, w * 0.58, h * 0.40], fill=(150, 152, 160), outline=(50, 50, 55), width=2)
    d.line([(8, h * 0.3), (w - 8, h * 0.3)], fill=tint(tuple(int(c * 255) for c in col), 0.6), width=3)
    P(k, im)
x, y, w, h = reg("grey_post")
im = painted(w, h, GREY, 80, rust=0.72, edge=10); d = ImageDraw.Draw(im)
for yy in range(20, h, 28):
    d.rounded_rectangle([w / 2 - 5, yy, w / 2 + 5, yy + 12], radius=4, fill=(40, 42, 48))
P("grey_post", im)

# ------------------------------------------------------------------ swatches
SW = {"wood_hi": (1.0, 0.64, 0.26), "wood_lo": (0.50, 0.21, 0.07), "endgrain": (0.78, 0.42, 0.16),
      "red_hi": (1.0, 0.45, 0.35), "red_lo": (0.48, 0.06, 0.04), "red_flat": RED,
      "olive_hi": (0.76, 0.78, 0.38), "olive_lo": (0.26, 0.28, 0.10), "rubber": (0.12, 0.11, 0.11),
      "grey_hi": (0.82, 0.83, 0.86), "grey_lo": (0.28, 0.29, 0.32), "chrome": (0.62, 0.64, 0.69),
      "card_hi": (0.96, 0.76, 0.50), "card_lo": (0.55, 0.36, 0.18), "black": (0.10, 0.10, 0.11),
      "blue": (0.24, 0.40, 0.66), "yellow": (0.92, 0.78, 0.20), "orange": (1.0, 0.50, 0.10),
      "strap_g": (0.30, 0.42, 0.16), "strap_d": (0.16, 0.16, 0.14), "brass": (0.85, 0.70, 0.30),
      "green_flat": (0.56, 0.62, 0.20), "olive_flat": OLIVE, "tan": (0.86, 0.66, 0.38)}
for k, c in SW.items():
    P(k, solid(64, 64, c, hash(k) % 1000, amt=0.08))

save(A, ATLAS_FILE)
