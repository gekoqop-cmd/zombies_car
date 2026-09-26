"""Nature v2: cut sprites out of referens/priroda2.png, defringe, pack Nature_Atlas.png (2048, RGBA),
write decal PNGs and a new beige-brown rock texture.  python tex_nature2.py"""
import os, sys, json
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage as nd

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from tex_common import base, blotches, bevel_edges, to_img, tint, TEX_DIR  # noqa

SRC = r"D:\zombi-caaar\referens\priroda2.png"
DECALS = r"D:\zombi-caaar\models\Nature\Decals"
LAYOUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "layout_nature2.json")
ATLAS, SCALE, GUT = 2048, 1.36, 10
os.makedirs(DECALS, exist_ok=True)

img = np.asarray(Image.open(SRC).convert("RGBA")).astype(np.float64) / 255.0
alpha = img[..., 3]
lab, n = nd.label(alpha > 0.5)
sizes = nd.sum(np.ones_like(alpha), lab, range(1, n + 1))
objs = nd.find_objects(lab)
comps = [(i + 1, objs[i]) for i in range(n) if sizes[i] > 5000]
assert len(comps) == 12, len(comps)


def row_of(sl):
    return 0 if sl[0].start < 360 else (1 if sl[0].start < 600 else 2)


comps.sort(key=lambda c: (row_of(c[1]), c[1][1].start))
NAMES = [f"Bush{i}" for i in range(1, 6)] + [f"Grass{i}" for i in range(1, 5)] + [f"Stone{i}" for i in range(1, 4)]


def fill_rgb(rgb, solid):
    """Every pixel outside `solid` takes the colour of its nearest solid pixel (edge padding)."""
    _, (iy, ix) = nd.distance_transform_edt(~solid, return_indices=True)
    return rgb[iy, ix]


def extract(cid, sl, m=4):
    y0, y1 = max(sl[0].start - m, 0), min(sl[0].stop + m, alpha.shape[0])
    x0, x1 = max(sl[1].start - m, 0), min(sl[1].stop + m, alpha.shape[1])
    a = alpha[y0:y1, x0:x1]; rgb = img[y0:y1, x0:x1, :3]
    main = lab[y0:y1, x0:x1] == cid
    keep = nd.binary_dilation(main, iterations=2)                   # drop stray specks, keep the AA rim
    a = a * keep
    a_er = nd.grey_erosion(a, size=(3, 3))                          # 1 px alpha erosion removes the halo ring
    core = nd.grey_erosion(a, size=(5, 5)) >= 0.9                   # 2 px inside: colour we trust
    rgb = fill_rgb(rgb, core)                                       # defringe: edge colour <- nearest interior colour
    a_f = np.clip(a_er / 0.95, 0, 1)
    return rgb, a_f


def scaled(rgb, a, s):
    h, w = a.shape
    W, H = int(round(w * s)), int(round(h * s))
    rgb_i = Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8)).resize((W, H), Image.LANCZOS)
    a_i = Image.fromarray((a * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)
    return np.asarray(rgb_i) / 255.0, np.asarray(a_i) / 255.0


sprites = {}
for (cid, sl), name in zip(comps, NAMES):
    rgb, a = extract(cid, sl)
    rgb, a = scaled(rgb, a, SCALE)
    ys, xs = np.nonzero(a > 0.5)
    sprites[name] = (rgb, a, (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1))
    print(name, a.shape[::-1], "visible", sprites[name][2])

# ------------------------------------------------------------------ pack the atlas (3 rows)
rows = [NAMES[0:5], NAMES[5:9], NAMES[9:12]]
A_rgb = np.zeros((ATLAS, ATLAS, 3)); A_a = np.zeros((ATLAS, ATLAS))
layout = {"atlas": ATLAS, "file": "Nature_Atlas.png", "sprites": {}}
y = GUT
for row in rows:
    ws = [sprites[k][1].shape[1] for k in row]
    gap = (ATLAS - sum(ws)) / (len(row) + 1)
    assert gap >= 4, gap
    x = gap
    for k in row:
        rgb, a, (vx0, vy0, vx1, vy1) = sprites[k]
        h, w = a.shape
        xi = int(round(x))
        A_rgb[y:y + h, xi:xi + w] = rgb; A_a[y:y + h, xi:xi + w] = a
        layout["sprites"][k] = {"rect": [xi, y, w, h], "visible": [xi + vx0, y + vy0, xi + vx1, y + vy1]}
        x += w + gap
    y += max(sprites[k][1].shape[0] for k in row) + GUT
assert y <= ATLAS, y
A_rgb = fill_rgb(A_rgb, A_a > 0.5)                                   # colour-bleed into gutters (mip-safe)
atlas = np.dstack([A_rgb, A_a])
old = os.path.join(TEX_DIR, "nature_atlas.png")
if os.path.exists(old):
    os.remove(old)                                                   # old v1 atlas (case-insensitive FS)
Image.fromarray((atlas * 255).astype(np.uint8), "RGBA").save(os.path.join(TEX_DIR, "Nature_Atlas.png"))
print("saved Nature_Atlas.png, used height", y)

# ------------------------------------------------------------------ decals (square 512 PNGs)
for i in range(1, 4):
    rgb, a, (vx0, vy0, vx1, vy1) = sprites[f"Stone{i}"]
    rgb, a = rgb[vy0:vy1, vx0:vx1], a[vy0:vy1, vx0:vx1]
    s = 500 / max(a.shape)
    rgb, a = scaled(rgb, a, s)
    h, w = a.shape
    R = np.zeros((512, 512, 3)); Al = np.zeros((512, 512))
    oy, ox = (512 - h) // 2, (512 - w) // 2
    R[oy:oy + h, ox:ox + w] = rgb; Al[oy:oy + h, ox:ox + w] = a
    R = fill_rgb(R, Al > 0.5)
    Image.fromarray((np.dstack([R, Al]) * 255).astype(np.uint8), "RGBA").save(os.path.join(DECALS, f"Decal_Stone{i}.png"))
    layout["sprites"][f"Stone{i}"]["decal_content"] = [ox / 512, oy / 512, (ox + w) / 512, (oy + h) / 512]
    print("saved Decal_Stone%d.png" % i, (w, h))

# ------------------------------------------------------------------ rocks: beige-brown stone (sampled from the pavement)
RA = 1024
ROCK_REG = {"rock": (0, 0, 512, 512), "rock_top": (512, 0, 512, 512), "slab": (0, 512, 512, 512),
            "slab_side": (512, 512, 512, 256), "rock_hi": (512, 768, 128, 128), "rock_lo": (640, 768, 128, 128)}
CRACK = (84, 58, 36)


def stone_tex(w, h, col, seed, cracks=4, hi=0.30, lo=0.40):
    im = base(w, h, col, seed, amt=0.14, cells=4, grain=0.015)
    im = blotches(im, seed + 1, 0.66, tuple(c * 0.90 for c in col), None, 7)
    im = blotches(im, seed + 2, 0.72, tuple(min(1, c * 1.08) for c in col), None, 11)
    im = blotches(im, seed + 3, 0.80, (0.62, 0.44, 0.28), None, 14)             # warm weathering spots
    pim = to_img(bevel_edges(im, 24, hi, lo)); d = ImageDraw.Draw(pim)
    r = np.random.default_rng(seed + 4)
    c255 = tuple(int(c * 255) for c in col)
    for _ in range(cracks):
        x0, y0 = r.uniform(0.15, 0.85) * w, r.uniform(0.1, 0.5) * h
        pts = [(x0, y0)]
        for _ in range(4):
            x0 += r.uniform(-40, 40); y0 += r.uniform(20, 60); pts.append((x0, y0))
        d.line([(p[0] + 2, p[1] + 3) for p in pts], fill=tint(c255, 1.18), width=3)
        d.line(pts, fill=CRACK, width=4)
    for _ in range(70):
        x0, y0 = r.uniform(0, w), r.uniform(0, h)
        d.ellipse([x0 - 2, y0 - 2, x0 + 2, y0 + 2], fill=tint(c255, 0.72))
    return pim


R_ = Image.new("RGB", (RA, RA), (120, 100, 80))
for k, im in (("rock", stone_tex(512, 512, (0.68, 0.50, 0.37), 20)),
              ("rock_top", stone_tex(512, 512, (0.78, 0.58, 0.43), 21, cracks=3, hi=0.22, lo=0.32)),
              ("slab", stone_tex(512, 512, (0.74, 0.55, 0.41), 22, cracks=6)),
              ("slab_side", stone_tex(512, 256, (0.54, 0.39, 0.28), 23, cracks=2)),
              ("rock_hi", to_img(base(128, 128, (0.86, 0.67, 0.50), 24, amt=0.06))),
              ("rock_lo", to_img(base(128, 128, (0.37, 0.25, 0.17), 25, amt=0.06)))):
    x, y, w, h = ROCK_REG[k]
    R_.paste(im.resize((w, h)), (x, y))
R_.save(os.path.join(TEX_DIR, "Rocks_Atlas.png"))
layout["rocks"] = {"atlas": RA, "file": "Rocks_Atlas.png", "reg": ROCK_REG}
json.dump(layout, open(LAYOUT, "w"), indent=1)
print("saved Rocks_Atlas.png + layout")
