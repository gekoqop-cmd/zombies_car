"""Shared helpers for hand-painted atlas textures (system Python: numpy + Pillow)."""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont

TEX_DIR = r"D:\zombi-caaar\models\textures"
os.makedirs(TEX_DIR, exist_ok=True)
FONT_VAR = r"C:\Windows\Fonts\bahnschrift.ttf"
FONT_HEAVY = r"C:\Windows\Fonts\ariblk.ttf"


def font(size, style="Bold Condensed"):
    f = ImageFont.truetype(FONT_VAR, size)
    try:
        f.set_variation_by_name(style)
    except Exception:
        pass
    return f


def vnoise(h, w, cells, seed):
    r = np.random.default_rng(seed)
    cy, cx = max(2, int(round(cells * h / max(h, w)))), max(2, int(round(cells * w / max(h, w))))
    g = r.random((cy + 1, cx + 1))
    ty = np.linspace(0, cy, h, endpoint=False); tx = np.linspace(0, cx, w, endpoint=False)
    iy = ty.astype(int); ix = tx.astype(int)
    fy = ty - iy; fx = tx - ix
    fy = fy * fy * (3 - 2 * fy); fx = fx * fx * (3 - 2 * fx)
    a = g[iy][:, ix]; b = g[iy][:, ix + 1]; c = g[iy + 1][:, ix]; d = g[iy + 1][:, ix + 1]
    fx = fx[None, :]; fy = fy[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(h, w, cells, octaves, seed):
    acc = np.zeros((h, w)); amp = 1; tot = 0
    for o in range(octaves):
        acc += amp * vnoise(h, w, cells * 2 ** o, seed + 31 * o); tot += amp; amp *= 0.5
    return acc / tot


def to_img(a):
    return Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))


def to_arr(im):
    return np.asarray(im.convert("RGB")) / 255.0


def base(w, h, color, seed, amt=0.2, cells=6, grain=0.04):
    img = np.ones((h, w, 3)) * np.array(color, float)
    img *= (1 - amt / 2 + amt * fbm(h, w, cells, 4, seed))[..., None]
    img *= (1 + (np.random.default_rng(seed + 9).random((h, w)) - 0.5) * 2 * grain)[..., None]
    return img


def blotches(img, seed, thr, color, rim=None, cells=8, rim_w=0.03, mask=None):
    h, w = img.shape[:2]
    m = fbm(h, w, cells, 4, seed) * 0.75 + fbm(h, w, cells * 4, 2, seed + 3) * 0.25
    if mask is not None:
        m = m + mask
    if rim is not None:
        img = np.where(((m > thr - rim_w) & (m <= thr))[..., None], np.array(rim), img)
    return np.where((m > thr)[..., None], np.array(color), img)


def rust(img, seed, amount=0.7, cells=9, bottom=0.0):
    """Hand-painted rust patches: orange rim, dark brown core. bottom biases toward lower rows."""
    h = img.shape[0]
    bias = bottom * np.clip(np.linspace(-0.5, 1.0, h), 0, 1)[:, None] if bottom else None
    img = blotches(img, seed, amount, (0.60, 0.30, 0.10), None, cells, mask=bias)
    img = blotches(img, seed, amount + 0.035, (0.40, 0.18, 0.07), None, cells, mask=bias)
    return img


def bevel_edges(img, width=6, hi=0.18, lo=0.28):
    """Painted bevel: light top/left edge, dark bottom/right edge (per region = per face)."""
    h, w = img.shape[:2]
    y = np.arange(h)[:, None]; x = np.arange(w)[None, :]
    c = lambda d: np.clip(1 - d / width, 0, 1)
    shade = hi * c(y) + hi * 0.6 * c(x) - lo * c(h - 1 - y) - lo * 0.6 * c(w - 1 - x)
    return img * (1 + shade)[..., None]


def ao_bottom(img, strength=0.45, start=0.75):
    """Painted AO: darken towards the bottom rows (v = 0)."""
    h = img.shape[0]
    t = np.clip((np.linspace(0, 1, h) - start) / (1 - start), 0, 1)[:, None]
    return img * (1 - strength * t ** 1.5)[..., None]


def ao_edges(img, strength=0.25, width=0.12):
    h, w = img.shape[:2]
    y = np.linspace(0, 1, h)[:, None]; x = np.linspace(0, 1, w)[None, :]
    d = np.minimum(np.minimum(x, 1 - x), np.minimum(y, 1 - y))
    return img * (1 - strength * np.clip(1 - d / width, 0, 1) ** 2)[..., None]


def wood(w, h, color, seed, vertical=True, planks=1, knots=True):
    img = base(w, h, color, seed, amt=0.15)
    yy, xx = np.mgrid[0:h, 0:w]
    along, across = (yy, xx) if vertical else (xx, yy)
    size_across = w if vertical else h
    warp = fbm(h, w, 5, 3, seed + 1)
    gr = 0.5 + 0.5 * np.sin(across * (40.0 / size_across) * np.pi + warp * 14 + np.sin(along * 0.01) * 1.5)
    img *= (0.82 + 0.22 * gr)[..., None]
    if knots:
        k = fbm(h, w, 10, 2, seed + 2) > 0.8
        img[k] *= 0.72
    if planks > 1:
        step = size_across / planks
        gaps = (across % step) < max(3, step * 0.04)
        img[gaps] *= 0.45
    return img


def put(atlas, reg, img):
    x, y, w, h = reg
    im = img if isinstance(img, Image.Image) else to_img(img)
    if im.size != (w, h):
        im = im.resize((w, h), Image.LANCZOS)
    atlas.paste(im.convert("RGB"), (x, y))


def save(im, name):
    p = os.path.join(TEX_DIR, name)
    im.save(p); print("saved", p)


def emissive_sheet(size=256):
    """Warm glow gradient (radial) — used for lamp glass/bulbs."""
    y, x = np.mgrid[0:size, 0:size] / size - 0.5
    r = np.sqrt(x * x + y * y) * 2
    c0 = np.array([1.0, 0.80, 0.38]); c1 = np.array([1.0, 0.48, 0.06])
    t = np.clip(r, 0, 1)[..., None]
    return to_img(c0 * (1 - t) + c1 * t)


# ------------------------------------------------------------------ cartoon (hand-painted) helpers
def tint(c, k):
    return tuple(int(max(0, min(255, v * k))) for v in c)


def toon_wood(w, h, color, seed, vertical=True, strokes=60, cracks=5, knots=2, planks=1, notches=6,
              hi=0.30, lo=0.40, edge=12):
    """Bright cartoon wood: painted grain strokes, cracks with light rims, knots, V-notches, strong bevels."""
    from PIL import ImageFilter
    r = np.random.default_rng(seed)
    c255 = tuple(int(v * 255) for v in color)
    img = base(w, h, color, seed, amt=0.14, cells=3, grain=0.012)
    im = to_img(img).convert("RGBA")
    ov = Image.new("RGBA", (w, h), (0, 0, 0, 0)); d = ImageDraw.Draw(ov)
    L = h if vertical else w          # length along grain
    A = w if vertical else h          # across grain
    P = lambda a, l: (a, l) if vertical else (l, a)
    for _ in range(strokes):          # long painted grain strokes
        a = r.uniform(0, A); l0 = r.uniform(-0.2 * L, 0.8 * L); ln = r.uniform(0.25, 0.8) * L
        k = r.choice([0.72, 0.8, 1.12, 1.2])
        pts = [P(a + r.uniform(-2, 2) + 3 * np.sin(t * 3 + a), l0 + t * ln) for t in np.linspace(0, 1, 8)]
        d.line(pts, fill=tint(c255, k) + (int(r.uniform(70, 150)),), width=int(r.uniform(2, 5)))
    im = Image.alpha_composite(im, ov.filter(ImageFilter.GaussianBlur(1.2)))
    d = ImageDraw.Draw(im)
    if planks > 1:                    # dark seams between planks with a light lip
        for i in range(1, planks):
            a = A * i / planks
            d.line([P(a + 2, 0), P(a + 2, L)], fill=tint(c255, 1.25), width=2)
            d.line([P(a, 0), P(a, L)], fill=tint(c255, 0.40), width=4)
    for _ in range(knots):
        a, l = r.uniform(0.2, 0.8) * A, r.uniform(0.15, 0.85) * L
        ra, rl = r.uniform(5, 9), r.uniform(10, 18)
        box = [P(a - ra, l - rl)[0], P(a - ra, l - rl)[1], P(a + ra, l + rl)[0], P(a + ra, l + rl)[1]]
        box = [min(box[0], box[2]), min(box[1], box[3]), max(box[0], box[2]), max(box[1], box[3])]
        d.ellipse(box, fill=tint(c255, 0.55), outline=tint(c255, 0.35), width=3)
        d.ellipse([box[0] + 4, box[1] + 4, box[2] - 4, box[3] - 4], fill=tint(c255, 0.8))
    for _ in range(cracks):           # dark crack + light rim underneath
        a, l = r.uniform(0.1, 0.9) * A, r.uniform(0.05, 0.7) * L
        pts = [P(a, l)]
        for _ in range(4):
            a += r.uniform(-3, 3); l += r.uniform(0.03, 0.08) * L
            pts.append(P(a, l))
        d.line([(x + 1, y + 2) for x, y in pts], fill=tint(c255, 1.35), width=2)
        d.line(pts, fill=tint(c255, 0.35), width=3)
    for _ in range(notches):          # V-notch chips along the long edges
        l = r.uniform(0.08, 0.92) * L; side = r.choice([0, A - 1]); dep = r.uniform(8, 16); wd = r.uniform(10, 18)
        s = 1 if side == 0 else -1
        tri = [P(side, l - wd), P(side + s * dep, l), P(side, l + wd)]
        d.polygon(tri, fill=tint(c255, 0.45))
        d.line([P(side + s * dep, l), P(side, l + wd)], fill=tint(c255, 1.3), width=2)
    arr = to_arr(im)
    return bevel_edges(arr, edge, hi, lo)


def toon_bolt(d, x, y, r=13):
    d.ellipse([x - r - 4, y - r - 3, x + r + 4, y + r + 5], fill=(90, 40, 14))
    d.ellipse([x - r, y - r, x + r, y + r], fill=(78, 82, 92), outline=(32, 33, 38), width=3)
    d.ellipse([x - r * 0.6, y - r * 0.65, x - r * 0.05, y - r * 0.1], fill=(205, 210, 220))


def solid(w, h, color, seed, amt=0.12, hi=0.0, lo=0.0, edge=8):
    img = base(w, h, color, seed, amt=amt, cells=3, grain=0.01)
    return bevel_edges(img, edge, hi, lo) if (hi or lo) else img
