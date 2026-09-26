"""Sheet 1 atlas: signs and lights.  python tex_signs.py"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from tex_common import *  # noqa
from layout_signs import *  # noqa

A = Image.new("RGB", (ATLAS, ATLAS), (60, 60, 60))

# --- woods (v = along the post, bottom of region = base)
for key, col, seed in (("wood_dark", (0.42, 0.27, 0.17), 1), ("wood_orange", (0.72, 0.38, 0.17), 2)):
    x, y, w, h = REG[key]
    img = wood(w, h, col, seed, vertical=True)
    img = rust(img, seed + 5, 0.80, cells=10)
    img = bevel_edges(img, 10, 0.2, 0.3)
    img = ao_bottom(img, 0.45, 0.8)
    put(A, REG[key], img)

# metal pole (dark grey, rust streaks running down)
x, y, w, h = REG["metal_pole"]
img = base(w, h, (0.30, 0.30, 0.32), 10, amt=0.25)
streak = np.clip(fbm(h, w, 4, 3, 11) - 0.5, 0, 1) * np.clip(np.sin(np.arange(w) / w * 17) ** 8, 0, 1)[None, :]
img = img * (1 - streak[..., None]) + np.array([0.55, 0.28, 0.12]) * streak[..., None] * 1.3
img = rust(img, 12, 0.76, 10)
img = bevel_edges(img, 10, 0.2, 0.3)
put(A, REG["metal_pole"], ao_bottom(img, 0.45, 0.8))

# concrete base
x, y, w, h = REG["concrete"]
img = base(w, h, (0.80, 0.74, 0.66), 20, amt=0.22)
img = blotches(img, 21, 0.70, (0.72, 0.42, 0.20), (0.62, 0.50, 0.40), cells=6)
img = bevel_edges(img, 14, 0.2, 0.3)
put(A, REG["concrete"], ao_bottom(img, 0.5, 0.6))

for key, col, seed, rs in (("metal_dark", (0.20, 0.20, 0.22), 30, 0.74), ("metal_grey", (0.52, 0.53, 0.52), 31, 0.72),
                           ("band", (0.26, 0.26, 0.28), 32, 0.7), ("cable", (0.08, 0.08, 0.09), 33, 2),
                           ("ceramic", (0.88, 0.86, 0.80), 34, 2), ("red", (0.78, 0.13, 0.10), 35, 0.72),
                           ("lamp_inner", (0.35, 0.25, 0.15), 36, 2)):
    x, y, w, h = REG[key]
    img = base(w, h, col, seed, amt=0.2)
    if rs < 1:
        img = rust(img, seed + 1, rs, 8)
    put(A, REG[key], bevel_edges(img, max(4, min(w, h) // 20), 0.2, 0.3))

x, y, w, h = REG["wood_frame"]
put(A, REG["wood_frame"], bevel_edges(wood(w, h, (0.36, 0.22, 0.13), 37, vertical=False), 8))

# --- arrow sign face
x, y, w, h = REG["arrow"]
pts = [(px * w, (1 - py) * h) for px, py in ARROW]
cream = base(w, h, (0.93, 0.86, 0.72), 40, amt=0.18)
cream = blotches(cream, 41, 0.66, (0.85, 0.62, 0.40), None, cells=10)
redimg = rust(base(w, h, (0.80, 0.13, 0.10), 42, amt=0.18), 43, 0.72, 12)
POLY = Image.new("L", (w, h), 0); ImageDraw.Draw(POLY).polygon(pts, fill=255)
cx = sum(p[0] for p in pts) / len(pts); cy = sum(p[1] for p in pts) / len(pts)
mask_in = Image.new("L", (w, h), 0); dm = ImageDraw.Draw(mask_in)
dm.polygon(pts, fill=255)
dm.line(pts + [pts[0]], fill=0, width=96, joint="curve")
m_in = np.asarray(mask_in)[..., None] / 255.0
img = redimg * (1 - m_in) + cream * m_in
im = to_img(img); d = ImageDraw.Draw(im)
d.line(pts + [pts[0]], fill=(95, 20, 16), width=6, joint="curve")
for k in range(len(pts)):   # rivets along the middle of the red border
    ax, ay = pts[k]; bx, by = pts[(k + 1) % len(pts)]
    L = np.hypot(bx - ax, by - ay); n = max(1, int(L / 150))
    nx, ny = -(by - ay) / L, (bx - ax) / L
    for i in range(n + 1):
        t = 0.1 + 0.8 * i / max(n, 1)
        for sgn in (1, -1):
            px, py = ax + (bx - ax) * t + sgn * nx * 24, ay + (by - ay) * t + sgn * ny * 24
            if 0 <= px < w and 0 <= py < h and POLY.getpixel((int(px), int(py))) > 0:
                d.ellipse([px - 8, py - 8, px + 8, py + 8], fill=(70, 60, 55), outline=(35, 30, 28), width=2)
                break
txt = "ЗАПРАВКА"
f = font(170)
d.text((w * 0.405, h * 0.51), txt, font=f, fill=(150, 22, 20), anchor="mm")
put(A, REG["arrow"], im)

# --- KAFE plank
x, y, w, h = REG["kafe"]
img = wood(w, h, (0.82, 0.62, 0.40), 50, vertical=False, planks=2)
img = blotches(img, 51, 0.72, (0.75, 0.48, 0.28), None, 10)
im = to_img(ao_edges(img, 0.2, 0.08)); d = ImageDraw.Draw(im)
d.rectangle([0, 0, w - 1, h - 1], outline=(70, 42, 24), width=18)
for (px, py) in [(26, 26), (w - 26, 26), (26, h - 26), (w - 26, h - 26)]:
    d.ellipse([px - 8, py - 8, px + 8, py + 8], fill=(60, 55, 50))
f = font(120)
d.text((w * 0.40, h * 0.52), "КАФЕ", font=f, fill=(48, 32, 24), anchor="mm")
cx, cy = w * 0.78, h * 0.58   # coffee cup icon
d.rounded_rectangle([cx - 42, cy - 30, cx + 30, cy + 34], radius=14, fill=(48, 32, 24))
d.ellipse([cx + 18, cy - 16, cx + 50, cy + 16], outline=(48, 32, 24), width=9)
d.ellipse([cx - 62, cy + 28, cx + 52, cy + 46], fill=(48, 32, 24))
for k in (-20, 0, 20):
    d.arc([cx + k - 10, cy - 78, cx + k + 10, cy - 38], 100, 260, fill=(48, 32, 24), width=6)
put(A, REG["kafe"], im)

# --- road sign face "ЦЕНТР ГОРОДА 2 КМ"
x, y, w, h = REG["road_face"]
img = base(w, h, (0.93, 0.89, 0.80), 60, amt=0.15)
img = blotches(img, 61, 0.70, (0.78, 0.50, 0.30), (0.70, 0.56, 0.44), cells=9)
im = to_img(bevel_edges(img, 16, 0.15, 0.25)); d = ImageDraw.Draw(im)
d.rounded_rectangle([34, 34, w - 35, h - 35], radius=22, outline=(35, 32, 30), width=14)
for (px, py) in [(22, 22), (w - 22, 22), (22, h - 22), (w - 22, h - 22)]:
    d.ellipse([px - 9, py - 9, px + 9, py + 9], fill=(90, 80, 70), outline=(40, 35, 30), width=2)
ink = (30, 28, 28)
f = font(118)
for i, line in enumerate(("ЦЕНТР", "ГОРОДА", "2 КМ")):
    d.text((w / 2, 115 + i * 105), line, font=f, fill=ink, anchor="mm")
ay = 425
d.rectangle([150, ay - 12, 318, ay + 12], fill=ink)
d.polygon([(310, ay - 40), (372, ay), (310, ay + 40)], fill=ink)
put(A, REG["road_face"], im)

x, y, w, h = REG["road_back"]
img = rust(base(w, h, (0.55, 0.56, 0.55), 62, amt=0.2), 63, 0.7, 8)
put(A, REG["road_back"], bevel_edges(img, 16, 0.15, 0.3))

# --- transformer box front: panel door with handle, rust
x, y, w, h = REG["box_front"]
img = rust(base(w, h, (0.55, 0.56, 0.55), 70, amt=0.2), 71, 0.72, 8, bottom=0.25)
im = to_img(bevel_edges(img, 18, 0.2, 0.3)); d = ImageDraw.Draw(im)
d.rectangle([80, 70, w - 80, h - 70], outline=(70, 72, 72), width=10)
d.rectangle([92, 82, w - 92, h - 82], outline=(185, 185, 180), width=3)
d.rectangle([w - 140, h / 2 - 40, w - 118, h / 2 + 40], fill=(50, 50, 52))
for (px, py) in [(40, 40), (w - 40, 40), (40, h - 40), (w - 40, h - 40)]:
    d.ellipse([px - 10, py - 10, px + 10, py + 10], fill=(80, 80, 80), outline=(40, 40, 40), width=2)
put(A, REG["box_front"], im)

save(A, ATLAS_FILE)
save(emissive_sheet(), EMIT_FILE)
