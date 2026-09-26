"""Sheet 5 atlas: nature (cartoon), RGBA.  python tex_nature.py"""
import os, sys, math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from tex_common import *  # noqa
from layout_nature import *  # noqa

A = Image.new("RGBA", (ATLAS, ATLAS), (60, 60, 60, 255))


def putA(k, im):
    x, y, w, h = REG[k]
    if im.size != (w, h):
        im = im.resize((w, h), Image.LANCZOS)
    A.paste(im.convert("RGBA"), (x, y))


def leaf_shape(d, cx, cy, r, ang, fill, dark, light, lobes=4):
    """Rounded clover leaf: distinct round lobes + dark outline + painted highlight + veins."""
    pts = []
    for k in range(lobes):
        a = ang + 2 * math.pi * k / lobes
        pts.append((cx + math.cos(a) * r * 0.55, cy + math.sin(a) * r * 0.55))
    lr = r * 0.50
    for (px, py) in pts:
        d.ellipse([px - lr - 4, py - lr - 4, px + lr + 4, py + lr + 4], fill=dark)
    for (px, py) in pts:
        d.ellipse([px - lr, py - lr, px + lr, py + lr], fill=fill)
    for (px, py) in pts:
        d.ellipse([px - lr * 0.62, py - lr * 0.7, px - lr * 0.05, py - lr * 0.2], fill=light)
    for (px, py) in pts:
        d.line([(cx, cy), (px + (px - cx) * 0.3, py + (py - cy) * 0.3)], fill=dark, width=3)
    d.ellipse([cx - 5, cy - 5, cx + 5, cy + 5], fill=dark)


def leaf_cluster(size, seed, palette, n=11, blob=0.27):
    S2 = size * 2
    im = Image.new("RGBA", (S2, S2), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    r = np.random.default_rng(seed)
    leaves = []
    for _ in range(n):
        a = r.uniform(0, 2 * math.pi); rad = S2 * blob * math.sqrt(r.uniform(0.0, 1.0))
        leaves.append((S2 / 2 + math.cos(a) * rad, S2 / 2 + math.sin(a) * rad * 0.92))
    leaves.sort(key=lambda p: p[1])                                   # back (top of image) first
    for (x, y) in leaves:
        t = 1 - (y / S2)                                              # higher leaves lighter
        base_c = palette[0] * (1 - t) + palette[1] * t
        base_c = base_c * r.uniform(0.9, 1.1)
        fill = tuple(int(min(1, c) * 255) for c in base_c) + (255,)
        dark = tuple(int(c * 255 * 0.45) for c in base_c) + (255,)
        light = tuple(int(min(1, c * 1.28) * 255) for c in base_c) + (255,)
        rr = r.uniform(0.16, 0.19) * S2
        # thin stem stub under the leaf
        d.line([(x, y), (x + r.uniform(-8, 8), y + rr * 1.3)], fill=tuple(int(c * 255 * 0.5) for c in palette[0]) + (255,),
               width=4)
        leaf_shape(d, x, y, rr, r.uniform(0, math.pi), fill, dark, light, lobes=int(r.choice([3, 4, 4])))
    return im.resize((size, size), Image.LANCZOS)


P_A = (np.array([0.46, 0.64, 0.12]), np.array([0.96, 0.90, 0.24]))
P_B = (np.array([0.34, 0.56, 0.12]), np.array([0.74, 0.86, 0.22]))
P_C = (np.array([0.24, 0.44, 0.10]), np.array([0.52, 0.68, 0.16]))
putA("leaves_a", leaf_cluster(512, 1, P_A))
putA("leaves_b", leaf_cluster(512, 2, P_B))
putA("leaves_c", leaf_cluster(512, 3, P_C))
x, y, w, h = REG["leaf_core"]
core = base(w, h, (0.22, 0.38, 0.09), 4, amt=0.3, cells=6)
putA("leaf_core", to_img(core))

# flowers
F2 = 256
im = Image.new("RGBA", (F2, F2), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
c = F2 / 2
for k in range(5):
    a = k * 2 * math.pi / 5 - math.pi / 2
    px, py = c + math.cos(a) * 58, c + math.sin(a) * 58
    d.ellipse([px - 52, py - 52, px + 52, py + 52], fill=(200, 180, 150, 255))
    d.ellipse([px - 46, py - 46, px + 46, py + 46], fill=(250, 244, 228, 255))
d.ellipse([c - 30, c - 30, c + 30, c + 30], fill=(235, 170, 40, 255))
d.ellipse([c - 16, c - 20, c + 4, c], fill=(255, 225, 120, 255))
putA("flower_w", im)
im = Image.new("RGBA", (F2, F2), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
for (px, py, rr) in ((90, 150, 48), (160, 140, 50), (126, 80, 52), (70, 90, 34), (185, 80, 34)):
    d.ellipse([px - rr - 4, py - rr - 4, px + rr + 4, py + rr + 4], fill=(205, 85, 25, 255))
    d.ellipse([px - rr, py - rr, px + rr, py + rr], fill=(255, 128, 40, 255))
    d.ellipse([px - rr * 0.6, py - rr * 0.7, px - rr * 0.05, py - rr * 0.15], fill=(255, 200, 120, 255))
putA("flower_o", im)

x, y, w, h = REG["stem"]
putA("stem", to_img(bevel_edges(base(w, h, (0.66, 0.30, 0.14), 5, amt=0.2), 8, 0.3, 0.3)))

# grass blades (texture = colour; the vertex colour adds the dark base / wind mask)
for k, c0, c1, seed in (("grass_a", (0.52, 0.66, 0.14), (1.0, 0.94, 0.36), 6),
                        ("grass_b", (0.40, 0.62, 0.14), (0.84, 0.92, 0.32), 7),
                        ("grass_c", (0.62, 0.64, 0.14), (1.0, 0.86, 0.30), 8)):
    x, y, w, h = REG[k]
    t = np.linspace(1, 0, h)[:, None, None]
    img = np.array(c0) * (1 - t) + np.array(c1) * t
    img = np.broadcast_to(img, (h, w, 3)).copy()
    u = np.abs(np.arange(w) - w / 2) / (w / 2)
    img *= (1.08 - 0.3 * u ** 2)[None, :, None]
    img[:, int(w * 0.47):int(w * 0.53)] *= 1.18                    # midrib highlight
    img[:, :int(w * 0.46)] *= 0.9                                   # one side a touch darker (folded blade)
    putA(k, to_img(img))
x, y, w, h = REG["seed"]                                   # wheat-like seed head, alpha cut-out
im = Image.new("RGBA", (w * 4, h * 4), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
W4, H4 = w * 4, h * 4
d.line([(W4 / 2, H4), (W4 / 2, H4 * 0.1)], fill=(150, 120, 60, 255), width=10)
for k in range(9):
    yy = H4 * (0.82 - k * 0.085); ww = W4 * (0.30 - k * 0.018)
    for sx in (-1, 1):
        cx = W4 / 2 + sx * ww * 0.55
        d.ellipse([cx - ww * 0.55, yy - H4 * 0.06, cx + ww * 0.55, yy + H4 * 0.06], fill=(150, 110, 50, 255))
        d.ellipse([cx - ww * 0.45, yy - H4 * 0.05, cx + ww * 0.45, yy + H4 * 0.05], fill=(236, 200, 120, 255))
        d.ellipse([cx - ww * 0.3, yy - H4 * 0.045, cx, yy - H4 * 0.01], fill=(255, 235, 170, 255))
putA("seed", im.resize((w, h), Image.LANCZOS))
x, y, w, h = REG["bud"]
putA("bud", to_img(bevel_edges(base(w, h, (0.96, 0.44, 0.14), 9, amt=0.1), 10, 0.4, 0.4)))

# rocks: sandy salmon stone, painted per-face bevel + cracks
ROCK = (0.86, 0.52, 0.35)


def rock_tex(w, h, col, seed, cracks=4, hi=0.35, lo=0.45):
    img = base(w, h, col, seed, amt=0.16, cells=4, grain=0.02)
    img = blotches(img, seed + 1, 0.70, tuple(c * 0.86 for c in col), None, 8)
    img = blotches(img, seed + 2, 0.78, tuple(min(1, c * 1.1) for c in col), None, 12)
    im = to_img(bevel_edges(img, 26, hi, lo)); d = ImageDraw.Draw(im)
    r = np.random.default_rng(seed + 3)
    for _ in range(cracks):
        x0, y0 = r.uniform(0.15, 0.85) * w, r.uniform(0.1, 0.5) * h
        pts = [(x0, y0)]
        for _ in range(4):
            x0 += r.uniform(-40, 40); y0 += r.uniform(20, 60); pts.append((x0, y0))
        d.line([(p[0] + 2, p[1] + 3) for p in pts], fill=tint(tuple(int(c * 255) for c in col), 1.25), width=3)
        d.line(pts, fill=tint(tuple(int(c * 255) for c in col), 0.5), width=4)
    for _ in range(60):
        x0, y0 = r.uniform(0, w), r.uniform(0, h)
        d.ellipse([x0 - 2, y0 - 2, x0 + 2, y0 + 2], fill=tint(tuple(int(c * 255) for c in col), 0.7))
    return im


putA("rock", rock_tex(512, 512, ROCK, 20))
putA("rock_top", rock_tex(512, 512, (0.95, 0.64, 0.44), 21, cracks=3, hi=0.25, lo=0.35))
putA("slab", rock_tex(512, 512, (0.93, 0.60, 0.40), 22, cracks=6, hi=0.3, lo=0.4))
putA("slab_side", rock_tex(512, 256, (0.74, 0.42, 0.28), 23, cracks=2))

putA("rock_hi", to_img(base(128, 128, (1.0, 0.74, 0.54), 24, amt=0.08)))
putA("rock_lo", to_img(base(128, 128, (0.50, 0.27, 0.18), 25, amt=0.08)))
A.save(os.path.join(TEX_DIR, ATLAS_FILE)); print("saved", ATLAS_FILE)
