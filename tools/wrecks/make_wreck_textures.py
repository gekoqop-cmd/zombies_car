"""Procedural textures for the parked wreck cars (referens/pickup.png, referens/sedan.png):
tileable rusty paint per car + a shared parts atlas.
Run with system Python (numpy + Pillow):  python make_wreck_textures.py
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from wreck_layout import *  # noqa

OUT = r"D:\zombi-caaar\models\Vehicles\Textures"
os.makedirs(OUT, exist_ok=True)
FONT = r"C:\Windows\Fonts\arialbd.ttf"


# ------------------------------------------------------------------ periodic noise (tiles seamlessly)
def vnoise(n, cells, seed):
    g = np.random.default_rng(seed).random((cells, cells))
    t = np.linspace(0, cells, n, endpoint=False)
    i = t.astype(int); f = t - i
    f = f * f * (3 - 2 * f)
    i1 = (i + 1) % cells
    a = g[i][:, i]; b = g[i][:, i1]; c = g[i1][:, i]; d = g[i1][:, i1]
    fx = f[None, :]; fy = f[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(n, cells, octaves, seed):
    acc = np.zeros((n, n)); amp = 1; tot = 0
    for o in range(octaves):
        acc += amp * vnoise(n, cells * 2 ** o, seed + 31 * o); tot += amp; amp *= 0.5
    return acc / tot


def fbm_rect(h, w, cells, octaves, seed):
    n = max(h, w)
    return fbm(n, cells, octaves, seed)[:h, :w]


def grain(h, w, seed, amt=0.05):
    return 1 + (np.random.default_rng(seed).random((h, w)) - 0.5) * 2 * amt


def col(c):
    return np.array(c, float)[None, None, :]


def layer(img, mask, color):
    return np.where(mask[..., None], color if np.ndim(color) == 3 else col(color), img)


def to_im(a):
    return Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8))


# ------------------------------------------------------------------ paint
def paint(seed, base, patch_col, patch_thr, rust_thr, blocky):
    """Faded paint with pixel-ish rust islands (the references use chunky, stepped rust shapes)."""
    n = PAINT
    img = np.ones((n, n, 3)) * np.array(base)
    img *= (0.86 + 0.24 * fbm(n, 4, 4, seed))[..., None]
    # faded / bare-metal patches
    m = fbm(n, 5, 4, seed + 1)
    img = layer(img, m > patch_thr, col(patch_col) * (0.92 + 0.12 * fbm(n, 16, 2, seed + 2))[..., None])
    # rust: quantise the noise to blocks so the edges step like the painted references
    r = fbm(n, 6, 5, seed + 3) * 0.7 + fbm(n, 24, 2, seed + 4) * 0.3
    if blocky:
        q = n // 128
        r = r.reshape(n // q, q, n // q, q).mean(axis=(1, 3)).repeat(q, 0).repeat(q, 1)
    img = layer(img, r > rust_thr - 0.035, (0.86, 0.44, 0.14))   # bright orange rim
    img = layer(img, r > rust_thr, (0.66, 0.30, 0.10))
    img = layer(img, r > rust_thr + 0.06, (0.45, 0.19, 0.07))
    img = layer(img, r > rust_thr + 0.12, (0.30, 0.12, 0.05))
    # vertical rust streaks
    streak = fbm(n, 32, 2, seed + 5)[:1, :].repeat(n, 0) * fbm(n, 3, 2, seed + 6)
    img = layer(img, streak > 0.5, img * col((0.93, 0.84, 0.76)))
    img *= grain(n, n, seed + 7, 0.04)[..., None]
    return to_im(img)


# ------------------------------------------------------------------ parts
def swatch(w, h, c, seed, amt=0.25, rust=None):
    img = np.ones((h, w, 3)) * np.array(c)
    img *= (1 - amt / 2 + amt * fbm_rect(h, w, 6, 3, seed))[..., None] * grain(h, w, seed + 1)[..., None]
    if rust:
        r = fbm_rect(h, w, 10, 3, seed + 2)
        img = layer(img, r > rust, (0.42, 0.20, 0.08))
        img = layer(img, r > rust + 0.06, (0.30, 0.13, 0.06))
    return to_im(img)


def tire_img(w, h, seed):
    img = np.ones((h, w, 3)) * 0.13
    v = np.arange(h)[:, None] / h; u = np.arange(w)[None, :] / w
    tread = (v > 0.33) & (v < 0.67)
    lug = ((np.floor(u * 18 + np.where(v < 0.5, 0, 0.5)) % 2) == 0) & tread
    img[np.broadcast_to(lug, (h, w))] = 0.17
    img[np.broadcast_to(tread & (np.abs(v - 0.5) < 0.02), (h, w))] = 0.07
    img[np.broadcast_to((np.abs(v - 0.33) < 0.01) | (np.abs(v - 0.67) < 0.01), (h, w))] = 0.08
    img *= (0.8 + 0.35 * fbm_rect(h, w, 8, 3, seed))[..., None]
    img *= np.array([1.0, 0.94, 0.88])        # dusty brown rubber
    return to_im(img)


def rim_img(n, seed):
    y, x = np.mgrid[0:n, 0:n] - n / 2 + 0.5
    r = np.sqrt(x * x + y * y) / (n / 2); a = np.arctan2(y, x)
    img = np.ones((n, n, 3)) * np.array([0.12, 0.10, 0.09])
    steel = np.array([0.55, 0.36, 0.24])       # rusty rims like the references
    img[r < 0.98] = steel * 0.7
    img[r < 0.88] = steel
    img[(r < 0.68) & (r > 0.62)] = steel * 0.55
    img[(r > 0.40) & (r < 0.56) & (np.cos(a * 6) > 0.78)] = 0.07
    img[r < 0.30] = steel * 1.15
    img[r < 0.12] = 0.16
    img[(r > 0.18) & (r < 0.24) & (np.cos(a * 5 + np.pi / 5) > 0.93)] = 0.22
    rr = fbm(n, 8, 3, seed)
    img = layer(img, (rr > 0.56) & (r < 0.9), (0.38, 0.18, 0.07))
    img *= (0.85 + 0.25 * fbm(n, 6, 2, seed + 1))[..., None]
    return to_im(img)


def glass_img(w, h, seed, holes):
    y, x = np.mgrid[0:h, 0:w] / np.array([h, w])[:, None, None]
    img = np.ones((h, w, 3)) * np.array([0.10, 0.11, 0.12])
    img += (0.10 * (1 - y))[..., None] * np.array([0.7, 0.8, 0.9])
    img[np.abs((x + y) - 0.8) < 0.06] += 0.09
    img *= (0.8 + 0.4 * fbm_rect(h, w, 6, 3, seed))[..., None]       # dirt
    img = layer(img, fbm_rect(h, w, 5, 3, seed + 1) > 0.68, (0.36, 0.28, 0.20))
    im = to_im(img)
    d = ImageDraw.Draw(im)
    rng = np.random.default_rng(seed + 2)
    for _ in range(4 if holes else 2):
        cx, cy = rng.integers(60, w - 60), rng.integers(50, h - 50)
        if holes:   # jagged hole showing the dark interior
            pts = []
            for k in range(16):
                ang = k / 16 * 2 * np.pi
                rad = rng.uniform(25, 70) if k % 2 else rng.uniform(55, 95)
                pts.append((cx + rad * np.cos(ang), cy + 0.8 * rad * np.sin(ang)))
            d.polygon(pts, fill=(22, 16, 13), outline=(215, 222, 228))
        for k in range(rng.integers(6, 10)):
            ang = rng.uniform(0, 2 * np.pi); x0, y0 = cx, cy
            for seg in range(3):
                ang += rng.uniform(-0.5, 0.5); ln = rng.uniform(18, 45)
                x1, y1 = x0 + ln * np.cos(ang), y0 + ln * np.sin(ang)
                d.line([(x0, y0), (x1, y1)], fill=(215, 222, 230), width=2)
                x0, y0 = x1, y1
    return im


def wood_img(w, h, seed):
    img = np.ones((h, w, 3)) * np.array([0.33, 0.22, 0.16])
    yy = np.arange(h)[:, None]
    plank = (yy // (h // 6)) % 2
    img *= (0.92 + 0.12 * plank)[..., None]
    img *= (0.8 + 0.35 * fbm_rect(h, w, 12, 3, seed))[..., None]
    img[np.broadcast_to((yy % (h // 6)) < 4, (h, w))] *= 0.35
    return to_im(img)


def lamp_img(w, h, c_in, c_out, cracked=False):
    im = Image.new("RGB", (w, h), tuple(int(v * 0.55) for v in c_out)); d = ImageDraw.Draw(im)
    d.rectangle([8, 8, w - 9, h - 9], fill=c_out)
    for k in range(4):
        d.line([(12, 16 + k * (h - 32) // 3), (w - 12, 16 + k * (h - 32) // 3)], fill=c_in, width=3)
    if cracked:
        d.line([(w * 0.3, 10), (w * 0.45, h * 0.5), (w * 0.4, h - 10)], fill=(230, 230, 230), width=2)
    return im


def plate_img(w, h):
    im = Image.new("RGB", (w, h), (170, 160, 140)); d = ImageDraw.Draw(im)
    d.rectangle([3, 3, w - 4, h - 4], outline=(40, 34, 30), width=6)
    d.text((w / 2, h / 2), "A 013 KO", font=ImageFont.truetype(FONT, 46), fill=(45, 38, 32), anchor="mm")
    arr = np.asarray(im) / 255.0
    arr = layer(arr, fbm_rect(h, w, 8, 3, 90) > 0.62, (0.45, 0.24, 0.10))
    return to_im(arr)


def parts_atlas():
    A = Image.new("RGB", (PARTS, PARTS), (40, 36, 34))

    def put(key, im):
        x, y, w, h = REG[key]
        A.paste(im.resize((w, h)) if im.size != (w, h) else im, (x, y))

    put("trim", swatch(256, 256, (0.20, 0.18, 0.17), 10, rust=0.60))
    put("rubber", swatch(256, 256, (0.08, 0.08, 0.08), 20))
    put("chrome", swatch(256, 256, (0.52, 0.48, 0.44), 30, rust=0.66))
    put("interior", swatch(256, 256, (0.30, 0.18, 0.12), 40, amt=0.4))
    put("tire", tire_img(256, 512, 50))
    put("rim", rim_img(256, 60))
    put("tail", lamp_img(256, 128, (240, 80, 60), (170, 22, 18)))
    put("head", lamp_img(256, 128, (240, 236, 214), (170, 164, 140), cracked=True))
    put("ind", lamp_img(256, 128, (255, 170, 60), (210, 100, 20)))
    put("plate", plate_img(256, 128))
    put("wood", wood_img(512, 256, 70))
    put("glass", glass_img(512, 256, 80, holes=False))
    put("broken", glass_img(512, 256, 85, holes=True))
    put("rust", swatch(256, 256, (0.50, 0.24, 0.09), 95, amt=0.5, rust=0.5))
    return A


# Pickup: mostly rust-orange over grey primer; sedan: blue-grey paint with orange rust islands.
paint(100, (0.76, 0.44, 0.22), (0.56, 0.55, 0.54), 0.55, 0.53, True).save(os.path.join(OUT, "wreck_paint_pickup.png"))
paint(200, (0.40, 0.46, 0.55), (0.52, 0.56, 0.62), 0.60, 0.60, True).save(os.path.join(OUT, "wreck_paint_sedan.png"))
parts_atlas().save(os.path.join(OUT, "wreck_parts.png"))
print("textures saved to", OUT)
