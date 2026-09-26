"""Procedural atlas textures for the player car: intact / damaged / wrecked + emissive.
Run with system Python (numpy + Pillow):  python make_car_textures.py
"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from car_layout import *  # noqa

OUT = r"D:\zombi-caaar\models\Car\Textures"
os.makedirs(OUT, exist_ok=True)
FONT = r"C:\Windows\Fonts\arialbd.ttf"


# ------------------------------------------------------------------ noise
def vnoise(h, w, cells, seed):
    r = np.random.default_rng(seed)
    cy, cx = max(2, int(cells * h / max(h, w))), max(2, int(cells * w / max(h, w)))
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


def grain(h, w, seed, amt=0.05):
    return 1 + (np.random.default_rng(seed).random((h, w)) - 0.5) * 2 * amt


def col(c):
    return np.array(c, float)[None, None, :]


def layer(img, mask, color, rim=None, rim_mask=None):
    img = np.where(mask[..., None], color if np.ndim(color) == 3 else col(color), img)
    if rim is not None:
        img = np.where(rim_mask[..., None], col(rim), img)
    return img


# ------------------------------------------------------------------ painted body
LEVEL = {  # patch thr, rust thr, rust bottom bias, orange saturation, soot
    0: dict(patch=0.60, rust=0.74, bias=0.10, sat=1.0, soot=0.0, scratch=0),
    1: dict(patch=0.57, rust=0.66, bias=0.16, sat=0.92, soot=0.10, scratch=60),
    2: dict(patch=0.55, rust=0.56, bias=0.22, sat=0.72, soot=0.25, scratch=120),
}


def painted(h, w, seed, lv, vgrad=True):
    p = LEVEL[lv]
    orange = np.array([0.93, 0.45, 0.10]) * p["sat"] + np.array([0.55, 0.45, 0.38]) * (1 - p["sat"])
    img = np.ones((h, w, 3)) * orange
    img *= (0.88 + 0.2 * fbm(h, w, 6, 4, seed))[..., None]
    # faded cream patches (cartoon shapes)
    m = fbm(h, w, 7, 4, seed + 1)
    img = layer(img, m > p["patch"] - 0.018, (0.70, 0.40, 0.18))
    cream = np.ones((h, w, 3)) * np.array([0.88, 0.83, 0.74])
    cream *= (0.9 + 0.12 * fbm(h, w, 12, 3, seed + 2))[..., None]
    img = np.where((m > p["patch"])[..., None], cream, img)
    # rust (more towards the bottom)
    r = fbm(h, w, 12, 4, seed + 3) * 0.75 + fbm(h, w, 40, 2, seed + 4) * 0.25
    if vgrad:
        r = r + p["bias"] * np.linspace(-0.4, 1.0, h)[:, None] ** 2 * np.sign(np.linspace(-0.4, 1.0, h))[:, None]
    img = layer(img, r > p["rust"] - 0.03, (0.62, 0.30, 0.10))
    img = layer(img, r > p["rust"], (0.40, 0.19, 0.08))
    img = layer(img, r > p["rust"] + 0.05, (0.28, 0.13, 0.06))
    # soot / grime
    if p["soot"] > 0:
        s = fbm(h, w, 5, 3, seed + 5)
        img *= (1 - p["soot"] * np.clip((s - 0.45) * 3, 0, 1))[..., None]
    if vgrad:  # dirt near the bottom
        img *= (1 - 0.30 * np.clip(np.linspace(-1.2, 1.0, h), 0, 1) ** 1.5)[:, None, None]
    img *= grain(h, w, seed + 6)[..., None]
    im = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    d = ImageDraw.Draw(im)
    rng = np.random.default_rng(seed + 7)
    for _ in range(p["scratch"]):
        x, y = rng.integers(0, w), rng.integers(0, h); l = rng.integers(8, 50)
        d.line([(int(x), int(y)), (int(x + l), int(y + rng.integers(-6, 6)))], fill=(225, 205, 180), width=1)
    return im


def s_to_px(s, w):
    return (S1 - s) / (S1 - S0) * w


def z_to_py(z, h):
    return (1 - (z - Z0) / (Z1 - Z0)) * h


def x_to_py_top(x, h):
    return (1 - (x - X0) / (X1 - X0)) * h


# ------------------------------------------------------------------ small parts
def swatch(w, h, c, seed, amt=0.25, rust=None):
    img = np.ones((h, w, 3)) * np.array(c)
    img *= (1 - amt / 2 + amt * fbm(h, w, 6, 3, seed))[..., None] * grain(h, w, seed + 1)[..., None]
    if rust:
        r = fbm(h, w, 10, 3, seed + 2)
        img = layer(img, r > rust, (0.38, 0.19, 0.08))
    return Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))


def tire_img(w, h, seed):
    img = np.ones((h, w, 3)) * 0.10
    v = np.arange(h)[:, None] / h; u = np.arange(w)[None, :] / w
    tread = (v > 0.33) & (v < 0.67)
    lug = ((np.floor(u * 20 + np.where(v < 0.5, 0, 0.5)) % 2) == 0) & tread
    img[np.broadcast_to(lug, (h, w))] = 0.15
    groove = tread & (np.abs(v - 0.5) < 0.02)
    img[np.broadcast_to(groove, (h, w))] = 0.05
    edge = (np.abs(v - 0.33) < 0.01) | (np.abs(v - 0.67) < 0.01)
    img[np.broadcast_to(edge, (h, w))] = 0.06
    ring = (np.abs(v - 0.18) < 0.012) | (np.abs(v - 0.82) < 0.012)
    img[np.broadcast_to(ring, (h, w))] = 0.17
    img *= (0.85 + 0.3 * fbm(h, w, 8, 3, seed))[..., None]
    return Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))


def rim_img(n, lv, seed):
    y, x = np.mgrid[0:n, 0:n] - n / 2 + 0.5
    r = np.sqrt(x * x + y * y) / (n / 2); a = np.arctan2(y, x)
    img = np.ones((n, n, 3)) * np.array([0.10, 0.10, 0.11])
    steel = np.array([0.58, 0.56, 0.52])
    img[r < 0.98] = steel * 0.75
    img[r < 0.90] = steel
    img[(r < 0.70) & (r > 0.64)] = steel * 0.6
    holes = (r > 0.40) & (r < 0.58) & (np.cos(a * 5) > 0.75)
    img[holes] = 0.08
    img[r < 0.30] = steel * 1.12
    img[(r < 0.12)] = 0.15
    bolts = (r > 0.18) & (r < 0.24) & (np.cos(a * 5 + np.pi / 5) > 0.93)
    img[bolts] = 0.2
    rr = fbm(n, n, 8, 3, seed)
    img = layer(img, (rr > [0.72, 0.64, 0.55][lv]) & (r < 0.9), (0.42, 0.21, 0.08))
    img *= (0.85 + 0.25 * fbm(n, n, 6, 2, seed + 1))[..., None]
    return Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))


def glass_img(n, lv, seed):
    y, x = np.mgrid[0:n, 0:n] / n
    img = np.ones((n, n, 3)) * np.array([0.09, 0.12, 0.16])
    img += (0.10 * (1 - y))[..., None] * np.array([0.6, 0.8, 1.0])
    streak = np.abs((x + y) - 0.75) < 0.07
    img[streak] += 0.10
    streak2 = np.abs((x + y) - 0.95) < 0.025
    img[streak2] += 0.08
    im = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    d = ImageDraw.Draw(im)
    rng = np.random.default_rng(seed)
    if lv >= 1:  # crack webs
        for _ in range(3 if lv == 1 else 6):
            cx, cy = rng.integers(60, n - 60, 2)
            if lv == 2 and rng.random() < 0.6:  # hole
                pts = []
                for k in range(14):
                    ang = k / 14 * 2 * np.pi; rad = rng.uniform(30, 75)
                    pts.append((cx + rad * np.cos(ang), cy + rad * np.sin(ang)))
                d.polygon(pts, fill=(8, 8, 10), outline=(200, 210, 220))
            for k in range(rng.integers(7, 12)):
                ang = rng.uniform(0, 2 * np.pi); x0, y0 = cx, cy
                for seg in range(4):
                    ang += rng.uniform(-0.4, 0.4); ln = rng.uniform(25, 60)
                    x1, y1 = x0 + ln * np.cos(ang), y0 + ln * np.sin(ang)
                    d.line([(x0, y0), (x1, y1)], fill=(215, 225, 235), width=2)
                    x0, y0 = x1, y1
            for rad in (18, 34):
                d.ellipse([cx - rad, cy - rad, cx + rad, cy + rad], outline=(190, 200, 210), width=1)
    return im


def can_img(n, base, lv, seed):
    img = np.ones((n, n, 3)) * np.array(base)
    img *= (0.9 + 0.15 * fbm(n, n, 6, 3, seed))[..., None]
    im = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    d = ImageDraw.Draw(im)
    dark = tuple(int(c * 255 * 0.65) for c in base); light = tuple(min(255, int(c * 255 * 1.3)) for c in base)
    d.rectangle([6, 6, n - 7, n - 7], outline=dark, width=10)
    d.rectangle([18, 18, n - 19, n - 19], outline=light, width=4)
    for off, c in ((3, light), (0, dark)):
        d.line([(40 + off, 40), (n - 40 + off, n - 40)], fill=c, width=14)
        d.line([(n - 40 + off, 40), (40 + off, n - 40)], fill=c, width=14)
    arr = np.asarray(im) / 255.0
    r = fbm(n, n, 10, 3, seed + 3)
    arr = layer(arr, r > [0.78, 0.70, 0.62][lv], (0.36, 0.30, 0.25))
    return Image.fromarray((np.clip(arr, 0, 1) * 255).astype(np.uint8))


def crate_img(n, lv, seed):
    img = np.ones((n, n, 3)) * np.array([0.33, 0.23, 0.16])
    y = np.arange(n)[:, None]
    img[np.broadcast_to((y % 64) < 4, (n, n))] *= 0.5
    img *= (0.85 + 0.25 * fbm(n, n, 8, 3, seed))[..., None]
    im = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))
    d = ImageDraw.Draw(im)
    for x in (n * 0.22, n * 0.78):
        d.rectangle([x - 12, 0, x + 12, n], fill=(190, 150, 55))
        d.rectangle([x - 14, n / 2 - 16, x + 14, n / 2 + 16], fill=(90, 90, 95))
    d.rectangle([0, 0, n - 1, n - 1], outline=(40, 40, 44), width=12)
    for cx in (12, n - 13):
        for cy in (12, n - 13):
            d.rectangle([cx - 12, cy - 12, cx + 12, cy + 12], fill=(70, 70, 76))
    return im


def plate_img(w, h):
    im = Image.new("RGB", (w, h), (225, 222, 210)); d = ImageDraw.Draw(im)
    d.rectangle([2, 2, w - 3, h - 3], outline=(30, 30, 30), width=5)
    f = ImageFont.truetype(FONT, 50)
    d.text((w / 2, h / 2), "Z 666 RX", font=f, fill=(25, 25, 25), anchor="mm")
    return im


# ------------------------------------------------------------------ assemble atlas
def build(lv):
    A = Image.new("RGB", (ATLAS, ATLAS), (40, 40, 40))

    def put(key, im):
        x, y, w, h = REG[key]
        A.paste(im.resize((w, h)) if im.size != (w, h) else im, (x, y))

    # side
    x, y, w, h = REG["side"]
    side = painted(h, w, 100, lv)
    d = ImageDraw.Draw(side)
    zb, zs = z_to_py(BELT_Z + 0.61, h), z_to_py(SILL_Z + 0.05, h)
    for s in DOOR_SEAMS:
        px = s_to_px(s, w)
        d.line([(px, zb), (px, zs)], fill=(45, 28, 18), width=4)
        d.line([(px + 3, zb), (px + 3, zs)], fill=(240, 190, 140), width=1)
    d.line([(s_to_px(1.35, w), z_to_py(0.72, h)), (s_to_px(-1.95, w), z_to_py(0.72, h))], fill=(70, 40, 20), width=3)
    fx, fz = FUEL_CAP
    cx, cy = s_to_px(fx, w), z_to_py(fz, h)
    d.ellipse([cx - 14, cy - 14, cx + 14, cy + 14], fill=(60, 55, 50), outline=(25, 20, 18), width=3)
    put("side", side)
    # top
    x, y, w, h = REG["top"]
    top = painted(h, w, 200, lv, vgrad=False)
    d = ImageDraw.Draw(top)
    px = s_to_px(HOOD_SEAM_S, w)
    d.line([(px, 0), (px, h)], fill=(45, 28, 18), width=4)
    put("top", top)
    # front/back
    x, y, w, h = REG["front"]
    put("front", painted(h, w, 300, lv))
    put("trim", swatch(256, 256, (0.17, 0.17, 0.19), 400, rust=[0.8, 0.7, 0.6][lv]))
    put("rubber", swatch(256, 256, (0.08, 0.08, 0.09), 410))
    put("tire", tire_img(256, 528, 420))
    put("rim", rim_img(256, lv, 430))
    put("glass", glass_img(512, lv, 440 + lv))
    put("can_g", can_img(256, (0.47, 0.54, 0.20), lv, 450))
    put("can_r", can_img(256, (0.80, 0.14, 0.11), lv, 460))
    put("crate", crate_img(256, lv, 470))
    put("yellow", swatch(256, 88, (0.95, 0.72, 0.10), 480))
    put("plate", plate_img(256, 88))
    put("orange", swatch(256, 16, (0.95, 0.40, 0.08), 490))
    return A


for lv, name in enumerate(["car_atlas_intact.png", "car_atlas_damaged.png", "car_atlas_wrecked.png"]):
    build(lv).save(os.path.join(OUT, name)); print("saved", name)

# emissive
E = Image.new("RGB", (EMIT, EMIT), (0, 0, 0)); d = ImageDraw.Draw(E)
for key, c_in, c_out in [("head", (255, 250, 225), (255, 205, 130)), ("lamp", (255, 245, 160), (255, 190, 30)),
                         ("tail", (255, 90, 70), (200, 20, 15)), ("ind", (255, 200, 90), (255, 120, 10))]:
    x, y, w, h = EREG[key]
    d.rectangle([x, y, x + w - 1, y + h - 1], fill=c_out)
    for k, rad in enumerate((50, 36, 22)):
        cc = tuple(int(c_out[i] + (c_in[i] - c_out[i]) * (k + 1) / 3) for i in range(3))
        d.ellipse([x + w / 2 - rad, y + h / 2 - rad, x + w / 2 + rad, y + h / 2 + rad], fill=cc)
E.save(os.path.join(OUT, "car_emissive.png")); print("saved emissive")
