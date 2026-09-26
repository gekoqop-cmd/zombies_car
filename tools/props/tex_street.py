"""Sheet 3 atlas: street props (cartoon).  python tex_street.py"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from tex_common import *  # noqa
from layout_street import *  # noqa

A = Image.new("RGB", (ATLAS, ATLAS), (60, 60, 60))
RUST = (0.72, 0.36, 0.12); RUST_D = (0.42, 0.18, 0.07)


def rows_z(profile, h):
    """z value for every texture row (row 0 = top of the image = end of profile)."""
    return np.array([z_of_v(profile, 1 - (i + 0.5) / h) for i in range(h)])


def rust_drips(im, seed, n, color=(150, 70, 25), top_rows=None):
    r = np.random.default_rng(seed); d = ImageDraw.Draw(im); w, h = im.size
    for _ in range(n):
        x = r.uniform(0, w); y0 = r.choice(top_rows) if top_rows is not None else r.uniform(0, h * 0.8)
        ln = r.uniform(12, 45); wd = r.uniform(3, 7)
        d.polygon([(x - wd, y0), (x + wd, y0), (x + wd * 0.3, y0 + ln), (x, y0 + ln + 4), (x - wd * 0.3, y0 + ln)],
                  fill=color)
    return im


def barrel_body(key, grey):
    x, y, w, h = REG[key]
    z = rows_z(BARREL_PROFILE, h)[:, None]
    if grey:
        img = base(w, h, (0.50, 0.55, 0.62), 101, amt=0.12, cells=3, grain=0.01)
        img = blotches(img, 102, 0.58, RUST, (0.62, 0.40, 0.25), cells=9)
        img = blotches(img, 103, 0.70, RUST_D, None, cells=9)
    else:
        red = base(w, h, (0.88, 0.15, 0.10), 104, amt=0.12, cells=3, grain=0.01)
        white = base(w, h, (0.95, 0.89, 0.78), 105, amt=0.10, cells=3, grain=0.01)
        band = ((z > 0.31) & (z < 0.57)).astype(float)[..., None]
        img = red * (1 - band) + white * band
        img = blotches(img, 106, 0.70, RUST, None, cells=10)
        img = blotches(img, 107, 0.76, RUST_D, None, cells=10)
    # painted cartoon lighting on rims / ribs: light on the upper side, dark lip below
    for zc in (0.035, 0.295, 0.585, 0.845):
        d = (z - zc)
        img *= (1 + 0.28 * np.exp(-((d - 0.004) / 0.008) ** 2) - 0.35 * np.exp(-((d + 0.016) / 0.007) ** 2))[..., None]
    img *= (1 - 0.35 * np.clip((0.06 - z) / 0.06, 0, 1))[..., None]          # AO at the base
    im = to_img(img)
    ribs = [int((1 - v) * h) for v in (arc_fracs(BARREL_PROFILE)[5], arc_fracs(BARREL_PROFILE)[8],
                                         arc_fracs(BARREL_PROFILE)[11])]
    im = rust_drips(im, 108 + grey, 45, top_rows=ribs)
    put(A, REG[key], im)


barrel_body("barrel_rw", 0)
barrel_body("barrel_grey", 1)


def lid(key, col):
    x, y, w, h = REG[key]
    img = base(w, h, col, 110, amt=0.12, cells=3)
    yy, xx = (np.mgrid[0:h, 0:w] - (h - 1) / 2) / (w / 2)
    rr = np.hypot(xx, yy)
    img *= (1 + 0.25 * np.exp(-((rr - 0.93) / 0.04) ** 2) - 0.3 * np.exp(-((rr - 0.86) / 0.03) ** 2))[..., None]
    img = blotches(img, 111, 0.72, RUST, None, 6)
    im = to_img(img); d = ImageDraw.Draw(im)
    cx, cy, rb = w * 0.5, h * 0.30, w * 0.14                  # big bung opening
    d.ellipse([cx - rb - 6, cy - rb - 6, cx + rb + 6, cy + rb + 6], fill=(60, 60, 64))
    d.ellipse([cx - rb, cy - rb, cx + rb, cy + rb], fill=(18, 18, 20))
    sx, sy = w * 0.72, h * 0.62                                # small vent cap
    d.ellipse([sx - 12, sy - 12, sx + 12, sy + 12], fill=(70, 72, 78), outline=(30, 30, 34), width=3)
    put(A, REG[key], im)


lid("lid_rw", (0.86, 0.15, 0.10))
lid("lid_grey", (0.50, 0.55, 0.62))

# --- tires (warm dark rubber, chunky lugs painted)
RUB = (0.25, 0.21, 0.19)
x, y, w, h = REG["tire_tread"]
im = to_img(base(w, h, (0.12, 0.10, 0.09), 120, amt=0.1)); d = ImageDraw.Draw(im)
n = 24
for row in range(2):
    for k in range(n):
        off = 0.5 * row
        x0 = (k + off) / n * w + 3; x1 = (k + off + 1) / n * w - 5
        y0 = 10 + row * (h / 2); y1 = y0 + h / 2 - 16
        for dx in (0, -w):
            d.rounded_rectangle([x0 + dx, y0, x1 + dx, y1], radius=6, fill=tint((64, 54, 48), 1.0))
            d.line([(x0 + dx + 4, y0 + 3), (x1 + dx - 4, y0 + 3)], fill=(110, 94, 84), width=3)
            d.line([(x0 + dx + 4, y1 - 2), (x1 + dx - 4, y1 - 2)], fill=(30, 26, 24), width=3)
            d.ellipse([x0 + dx + (x1 - x0) * 0.45, y0 + (y1 - y0) * 0.45, x0 + dx + (x1 - x0) * 0.55,
                       y0 + (y1 - y0) * 0.55], fill=(40, 34, 30))
put(A, REG["tire_tread"], im)
x, y, w, h = REG["tire_side"]
img = base(w, h, RUB, 121, amt=0.12, cells=4)
v = np.linspace(1, 0, h)[:, None]                             # v 0 = inner edge, 1 = outer edge
img *= (0.8 + 0.35 * np.exp(-((v - 0.72) / 0.08) ** 2) - 0.25 * np.exp(-((v - 0.2) / 0.1) ** 2))[..., None]
put(A, REG["tire_side"], img)
x, y, w, h = REG["tire_inner"]
put(A, REG["tire_inner"], base(w, h, (0.07, 0.06, 0.06), 122, amt=0.1))
put(A, REG["rubber"], base(128, 128, RUB, 123, amt=0.1))

# --- cone: orange with two reflective white bands and a white tip
x, y, w, h = REG["cone"]
z = rows_z(CONE_PROFILE, h)[:, None]
org = base(w, h, (1.0, 0.40, 0.08), 130, amt=0.10, cells=3)
wht = base(w, h, (0.98, 0.95, 0.88), 131, amt=0.08, cells=3)
band = (((z > 0.26) & (z < 0.36)) | ((z > 0.46) & (z < 0.54)) | (z > 0.64)).astype(float)[..., None]
img = org * (1 - band) + wht * band
img = blotches(img, 132, 0.74, (0.55, 0.30, 0.15), None, 8)
img *= (1 - 0.3 * np.clip((0.12 - z) / 0.08, 0, 1))[..., None]
put(A, REG["cone"], img)
x, y, w, h = REG["cone_base"]
img = blotches(base(w, h, (0.36, 0.22, 0.16), 133, amt=0.15), 134, 0.7, (0.62, 0.30, 0.10), None, 6)
put(A, REG["cone_base"], bevel_edges(img, 16, 0.3, 0.4))

# --- concrete barrier: cream concrete, bold red diagonal stripes
x, y, w, h = REG["barrier_face"]
xx = (np.arange(w)[None, :] / w - 0.5) * BARRIER_L
zz = (1 - np.arange(h)[:, None] / h) * BARRIER_H
cream = base(w, h, (0.93, 0.87, 0.77), 140, amt=0.14, cells=6)
redp = base(w, h, (0.90, 0.16, 0.10), 141, amt=0.10, cells=5)
stripe = ((((xx - zz) / 0.56) % 1.0) < 0.5).astype(float)[..., None]
img = cream * (1 - stripe) + redp * stripe
img = blotches(img, 142, 0.745, (0.80, 0.72, 0.64), (0.62, 0.52, 0.44), cells=12)     # chipped paint
img = blotches(img, 143, 0.77, (0.66, 0.36, 0.16), None, cells=12)                   # rust stains
img *= (1 - 0.35 * np.clip((0.14 - zz) / 0.14, 0, 1))[..., None]
im = to_img(img); d = ImageDraw.Draw(im)
r = np.random.default_rng(144)
for _ in range(10):                                           # cracks
    cx, cy = r.uniform(0, w), r.uniform(h * 0.2, h)
    pts = [(cx, cy)]
    for _ in range(4):
        cx += r.uniform(-18, 18); cy -= r.uniform(10, 30); pts.append((cx, cy))
    d.line([(p[0] + 1, p[1] + 2) for p in pts], fill=(250, 235, 215), width=2)
    d.line(pts, fill=(90, 60, 45), width=3)
put(A, REG["barrier_face"], im)
x, y, w, h = REG["barrier_end"]
img = blotches(base(w, h, (0.90, 0.84, 0.74), 145, amt=0.15), 146, 0.72, (0.66, 0.36, 0.16), None, 8)
im = to_img(ao_bottom(img, 0.35, 0.8)); d = ImageDraw.Draw(im)
cx, cy, rh = w * 0.5, h * 0.28, w * 0.075                      # lifting hole
d.ellipse([cx - rh - 8, cy - rh - 8, cx + rh + 8, cy + rh + 8], fill=(150, 135, 120))
d.ellipse([cx - rh, cy - rh, cx + rh, cy + rh], fill=(25, 22, 20))
d.line([(cx, h * 0.02), (cx, cy - rh - 8)], fill=(120, 100, 85), width=4)            # casting seam
d.line([(cx, cy + rh + 8), (cx, h * 0.98)], fill=(120, 100, 85), width=4)
put(A, REG["barrier_end"], im)
x, y, w, h = REG["barrier_top"]
put(A, REG["barrier_top"], bevel_edges(blotches(base(w, h, (0.96, 0.91, 0.82), 147, amt=0.12), 148, 0.7,
                                               (0.80, 0.72, 0.64), None, 10), 10, 0.2, 0.25))
x, y, w, h = REG["concrete"]
put(A, REG["concrete"], bevel_edges(blotches(base(w, h, (0.84, 0.78, 0.68), 149, amt=0.2), 150, 0.72,
                                           (0.66, 0.36, 0.16), None, 6), 14, 0.2, 0.35))

# --- grass blade: dark base -> yellow-green tip, painted midrib
x, y, w, h = REG["grass"]
t = np.linspace(1, 0, h)[:, None, None]                        # 0 base .. 1 tip
g0 = np.array([0.20, 0.36, 0.06]); g1 = np.array([0.86, 0.84, 0.24])
img = g0 * (1 - t) + g1 * t
img = np.broadcast_to(img, (h, w, 3)).copy()
img[:, w // 2 - 3:w // 2 + 3] *= 1.15
img *= (1 - 0.25 * (np.abs(np.arange(w) - w / 2) / (w / 2)))[None, :, None]
put(A, REG["grass"], img)

save(A, ATLAS_FILE)
