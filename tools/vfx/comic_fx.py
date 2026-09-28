"""Comic-book effect sprites for the game (bright colours, thick ink outlines, "BOOM"-style lettering).

Writes to unity/Assets/Art/VFX (alpha-blended VFX import rules):
  FX_ShockRing.png   electric ring of the car's discharge upgrade (flat on the ground)
  FX_MarkerRing.png  soft dashed ring under interaction spots (the workshop)
  FX_TargetRing.png  warning circle on the ground (boss spit landing, slam radius); white, tinted by the material
  FX_ChargeLine.png  chevron strip showing a boss's charge path
  FX_AcidBall.png    the boss's acid glob (comic blob with ink outline)
  FX_AcidSplat.png   acid splash on the ground
  FX_Aura.png        soft glowing ring under a boss
  FX_ComicBurst.png  jagged comic explosion burst (yellow core, orange-red rim, ink outline)
  FX_Boom_*.png      comic lettering: БАХ!, БУМ!, БАБАХ! (fat letters, hot gradient, thick ink, drop shadow)
  FX_Star.png        little star flying out of explosions
  FX_Cloud.png       cartoon smoke puff (grey-white, ink outline)
  FX_Scorch.png      dark scorch mark left on the ground
  FX_Ring.png        thin shockwave ring
  FX_AxeWeapon.png   the player's axe, upright, grip at the bottom (the swing turns it around the grip)
  FX_Slash.png       white comic slash crescent left by an axe swing
Run: python tools/vfx/comic_fx.py
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "ui"))
from paint import Painter, defringe  # noqa: E402

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
OUT = os.path.join(ROOT, "unity", "Assets", "Art", "VFX")
FONT = os.path.join(ROOT, "unity", "Assets", "Art", "Fonts", "Inter-SemiBold.ttf")


def save(img, name):
    arr = defringe(np.asarray(img.convert("RGBA")))
    Image.fromarray(arr, "RGBA").save(os.path.join(OUT, name + ".png"), optimize=True)
    print(name, img.size)


def shock_ring():
    """Jagged cyan-white lightning ring with a glow, drawn on a 512 canvas (white: tinted by the material)."""
    size, ss = 512, 2
    W = size * ss
    rnd = np.random.RandomState(7)
    glow = Image.new("L", (W, W), 0)
    core = Image.new("L", (W, W), 0)
    dg, dc = ImageDraw.Draw(glow), ImageDraw.Draw(core)
    c = W / 2
    for strand in range(3):
        pts = []
        n = 90
        for i in range(n + 1):
            a = 2 * math.pi * i / n
            r = W * 0.42 + rnd.uniform(-1, 1) * W * (0.025 if strand == 0 else 0.04)
            pts.append((c + r * math.cos(a), c + r * math.sin(a)))
        dg.line(pts, fill=255, width=int(W * 0.03), joint="curve")
        dc.line(pts, fill=255, width=int(W * (0.008 if strand else 0.012)), joint="curve")
    glow = glow.filter(ImageFilter.GaussianBlur(W * 0.02))
    alpha = np.maximum(np.asarray(glow, np.float32) * 0.7, np.asarray(core, np.float32))
    rgb = np.zeros((W, W, 3), np.float32)
    k = np.asarray(core, np.float32)[..., None] / 255.0
    rgb[:] = np.array([140, 230, 255], np.float32) * (1 - k) + np.array([255, 255, 255], np.float32) * k
    img = Image.fromarray(np.dstack([rgb, alpha]).astype(np.uint8), "RGBA").resize((size, size), Image.LANCZOS)
    save(img, "FX_ShockRing")


def marker_ring():
    """Dashed ring with a soft inner fill (white; the material tints it)."""
    size, ss = 256, 4
    W = size * ss
    m = Image.new("L", (W, W), 0)
    d = ImageDraw.Draw(m)
    c = W / 2
    r = W * 0.44
    dashes = 16
    for i in range(dashes):
        a0 = 360.0 * i / dashes
        d.arc([c - r, c - r, c + r, c + r], a0, a0 + 360.0 / dashes * 0.62, fill=255, width=int(W * 0.045))
    fill = Image.new("L", (W, W), 0)
    ImageDraw.Draw(fill).ellipse([c - r, c - r, c + r, c + r], fill=60)
    fill = fill.filter(ImageFilter.GaussianBlur(W * 0.04))
    a = np.maximum(np.asarray(m, np.float32), np.asarray(fill, np.float32))
    rgb = np.full((W, W, 3), 255, np.float32)
    img = Image.fromarray(np.dstack([rgb, a]).astype(np.uint8), "RGBA").resize((size, size), Image.LANCZOS)
    save(img, "FX_MarkerRing")


def target_ring():
    size, ss = 256, 4
    W = size * ss
    c = W / 2
    ring = Image.new("L", (W, W), 0)
    d = ImageDraw.Draw(ring)
    r = W * 0.45
    d.ellipse([c - r, c - r, c + r, c + r], outline=255, width=int(W * 0.05))
    # Four inward ticks.
    for k in range(4):
        a = math.pi / 2 * k + math.pi / 4
        x0, y0 = c + math.cos(a) * r * 0.98, c + math.sin(a) * r * 0.98
        x1, y1 = c + math.cos(a) * r * 0.72, c + math.sin(a) * r * 0.72
        d.line([(x0, y0), (x1, y1)], fill=255, width=int(W * 0.04))
    fill = Image.new("L", (W, W), 0)
    ImageDraw.Draw(fill).ellipse([c - r, c - r, c + r, c + r], fill=70)
    a = np.maximum(np.asarray(ring, np.float32), np.asarray(fill, np.float32))
    img = Image.fromarray(np.dstack([np.full((W, W, 3), 255, np.float32), a]).astype(np.uint8), "RGBA")
    save(img.resize((size, size), Image.LANCZOS), "FX_TargetRing")


def charge_line():
    w, h, ss = 512, 128, 4
    W, H = w * ss, h * ss
    m = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(m)
    step = W / 7.0
    for i in range(6):
        x = i * step + step * 0.3
        d.polygon([(x, H * 0.18), (x + step * 0.45, H * 0.5), (x, H * 0.82), (x + step * 0.22, H * 0.82),
                   (x + step * 0.67, H * 0.5), (x + step * 0.22, H * 0.18)], fill=255 - i * 10)
    d.polygon([(W * 0.9, H * 0.05), (W, H * 0.5), (W * 0.9, H * 0.95)], fill=255)
    band = Image.new("L", (W, H), 0)
    ImageDraw.Draw(band).rectangle([0, H * 0.3, W, H * 0.7], fill=60)
    a = np.maximum(np.asarray(m, np.float32), np.asarray(band, np.float32))
    img = Image.fromarray(np.dstack([np.full((H, W, 3), 255, np.float32), a]).astype(np.uint8), "RGBA")
    save(img.resize((w, h), Image.LANCZOS), "FX_ChargeLine")


def acid_ball():
    p = Painter(128, 4, outline=5)
    rnd = np.random.RandomState(3)
    pts = []
    for i in range(24):
        a = 2 * math.pi * i / 24
        r = 46 + rnd.uniform(-5, 5)
        pts.append((64 + r * math.cos(a), 66 + r * math.sin(a)))
    p.fill(p.poly_mask(pts), (120, 235, 60), gloss=0.55)
    for x, y, r in ((44, 50, 9), (78, 82, 7), (60, 88, 5)):
        p.fill(p.ellipse_mask((x - r, y - r, x + r, y + r)), (70, 170, 40), outline=False)
    p.save(os.path.join(OUT, "FX_AcidBall.png"))
    print("FX_AcidBall")


def acid_splat():
    p = Painter(256, 4, outline=6)
    rnd = np.random.RandomState(11)
    pts = []
    for i in range(40):
        a = 2 * math.pi * i / 40
        r = 70 + (34 if i % 5 == 0 else 0) + rnd.uniform(-10, 10)
        pts.append((128 + r * math.cos(a), 128 + r * math.sin(a) * 0.9))
    p.fill(p.poly_mask(pts), (110, 220, 50), shade=0.3)
    for i in range(9):
        a = rnd.uniform(0, 2 * math.pi)
        d = rnd.uniform(96, 118)
        r = rnd.uniform(6, 12)
        x, y = 128 + d * math.cos(a), 128 + d * math.sin(a) * 0.9
        p.fill(p.ellipse_mask((x - r, y - r, x + r, y + r)), (110, 220, 50))
    p.fill(p.ellipse_mask((88, 92, 150, 136)), (170, 255, 110), outline=False)
    p.save(os.path.join(OUT, "FX_AcidSplat.png"))
    print("FX_AcidSplat")


def aura():
    size = 256
    y, x = np.mgrid[0:size, 0:size]
    r = np.sqrt((x - size / 2 + 0.5) ** 2 + (y - size / 2 + 0.5) ** 2) / (size / 2)
    a = np.exp(-((r - 0.72) / 0.16) ** 2) * 255 + np.clip(1 - r, 0, 1) ** 2 * 90
    a = np.clip(a, 0, 255)
    img = Image.fromarray(np.dstack([np.full((size, size, 3), 255.0), a]).astype(np.uint8), "RGBA")
    save(img, "FX_Aura")


def comic_burst():
    from paint import burst_points
    p = Painter(512, 2, outline=9)
    outer = burst_points(256, 256, 214, 140, n=14, rot=7, jitter=0.3, seed=4)
    p.fill(p.poly_mask(outer), (240, 70, 30), gradient=(200, 40, 20))
    mid = burst_points(256, 256, 172, 112, n=12, rot=-5, jitter=0.3, seed=9)
    p.fill(p.poly_mask(mid), (255, 150, 30), outline=False, gradient=(245, 110, 25))
    core = burst_points(256, 256, 130, 86, n=10, rot=12, jitter=0.25, seed=13)
    p.fill(p.poly_mask(core), (255, 245, 150), outline=False, gradient=(255, 215, 60))
    p.fill(p.ellipse_mask((196, 196, 316, 316)), (255, 255, 235), outline=False, shade=0.0)
    p.save(os.path.join(OUT, "FX_ComicBurst.png"))
    print("FX_ComicBurst")


def lettering(text, name, rot=-8.0):
    import cv2
    w, h, ss = 512, 256, 3
    size_px = 164 if len(text) <= 4 else 104
    font = ImageFont.truetype(FONT, int(size_px * ss))
    mask = Image.new("L", (w * ss, h * ss), 0)
    d = ImageDraw.Draw(mask)
    bbox = d.textbbox((0, 0), text, font=font)
    tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
    d.text(((w * ss - tw) / 2 - bbox[0], (h * ss - th) / 2 - bbox[1]), text, font=font, fill=255)
    # Fatter letters (comic style), sheared and tilted.
    arr = np.asarray(mask)
    k = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (int(9 * ss) | 1, int(9 * ss) | 1))
    arr = cv2.dilate(arr, k)
    shear = np.float32([[1, -0.22, 0.11 * h * ss], [0, 1, 0]])
    arr = cv2.warpAffine(arr, shear, (w * ss, h * ss))
    rotm = cv2.getRotationMatrix2D((w * ss / 2, h * ss / 2), -rot, 1.0)
    arr = cv2.warpAffine(arr, rotm, (w * ss, h * ss))
    fill = arr.astype(np.float32) / 255.0
    outline = cv2.dilate(arr, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (int(16 * ss) | 1, int(16 * ss) | 1))).astype(np.float32) / 255.0
    shadow = np.roll(np.roll(outline, int(10 * ss), axis=0), int(8 * ss), axis=1)
    yy = np.linspace(0, 1, h * ss)[:, None]
    top = np.array([255, 245, 120], np.float32)
    bottom = np.array([245, 90, 30], np.float32)
    color = top[None, None, :] * (1 - yy[..., None]) + bottom[None, None, :] * yy[..., None]
    color = np.broadcast_to(color, (h * ss, w * ss, 3)).copy()
    # A white shine stripe across the upper letters.
    shine = ((yy > 0.28) & (yy < 0.36)).astype(np.float32)
    color = color * (1 - shine[..., None] * 0.6) + 255 * shine[..., None] * 0.6
    out = np.zeros((h * ss, w * ss, 4), np.float32)
    ink = np.array([30, 18, 12], np.float32)
    out[..., :3] = ink
    out[..., 3] = np.maximum(shadow * 0.55, outline) * 255
    a_fill = fill[..., None]
    out[..., :3] = out[..., :3] * (1 - a_fill) + color * a_fill
    img = Image.fromarray(out.astype(np.uint8), "RGBA").resize((w, h), Image.LANCZOS)
    save(img, name)


def star():
    from paint import star_points
    p = Painter(128, 4, outline=6)
    p.fill(p.poly_mask(star_points(64, 68, 58, 25)), (255, 225, 70), gloss=0.5)
    p.save(os.path.join(OUT, "FX_Star.png"))
    print("FX_Star")


def cloud():
    p = Painter(256, 3, outline=8)
    blobs = [(80, 150, 62), (140, 120, 76), (190, 158, 56), (120, 180, 58), (170, 190, 46)]
    from PIL import ImageChops
    m = p.mask()
    for x, y, r in blobs:
        m = ImageChops.lighter(m, p.ellipse_mask((x - r, y - r, x + r, y + r)))
    p.fill(m, (238, 234, 228), gradient=(170, 165, 160))
    for x, y, r in ((116, 104, 26), (170, 118, 18)):
        p.fill(p.ellipse_mask((x - r, y - r, x + r, y + r)), (255, 255, 255), outline=False, shade=0.0)
    p.save(os.path.join(OUT, "FX_Cloud.png"))
    print("FX_Cloud")


def scorch():
    size = 256
    rnd = np.random.RandomState(21)
    y, x = np.mgrid[0:size, 0:size]
    cx = cy = size / 2
    ang = np.arctan2(y - cy, x - cx)
    r = np.sqrt((x - cx) ** 2 + (y - cy) ** 2) / (size / 2)
    edge = 0.72 + 0.12 * np.sin(ang * 7 + 1.3) + 0.06 * np.sin(ang * 13)
    a = np.clip((edge - r) / 0.25, 0, 1) ** 0.8
    noise = rnd.rand(size, size)
    a *= 0.75 + 0.25 * noise
    rgb = np.zeros((size, size, 3), np.float32) + np.array([28, 22, 18], np.float32)
    img = Image.fromarray(np.dstack([rgb, a * 235]).astype(np.uint8), "RGBA").filter(ImageFilter.GaussianBlur(1.2))
    save(img, "FX_Scorch")


def ring():
    size = 256
    y, x = np.mgrid[0:size, 0:size]
    r = np.sqrt((x - size / 2 + 0.5) ** 2 + (y - size / 2 + 0.5) ** 2) / (size / 2)
    a = np.exp(-((r - 0.86) / 0.06) ** 2)
    img = Image.fromarray(np.dstack([np.full((size, size, 3), 255.0), a * 255]).astype(np.uint8), "RGBA")
    save(img, "FX_Ring")


def axe_weapon():
    sys.path.insert(0, os.path.join(os.path.dirname(__file__), "..", "ui"))
    from make_icons import axe
    p = Painter((128, 256), 4, outline=5)
    # make_icons.axe draws on a 256 grid around its centre: head up-left, handle down; shrink into 128 x 256.
    axe(p, 90, 132, 0.95, 0.0)
    p.save(os.path.join(OUT, "FX_AxeWeapon.png"))
    print("FX_AxeWeapon")


def slash():
    size, ss = 256, 4
    W = size * ss
    m = Image.new("L", (W, W), 0)
    d = ImageDraw.Draw(m)
    c = W / 2
    # A crescent: thick arc tapering to both ends, brightest in the middle.
    for i in range(60):
        t = i / 59.0
        a0 = 200 + t * 140
        width = int(W * 0.07 * math.sin(t * math.pi) + 2)
        r = W * 0.42
        d.arc([c - r, c - r, c + r, c + r], a0, a0 + 3, fill=255, width=width)
    glow = m.filter(ImageFilter.GaussianBlur(W * 0.012))
    a = np.maximum(np.asarray(glow, np.float32) * 0.9, np.asarray(m, np.float32))
    img = Image.fromarray(np.dstack([np.full((W, W, 3), 255.0), a]).astype(np.uint8), "RGBA").resize((size, size), Image.LANCZOS)
    save(img, "FX_Slash")


if __name__ == "__main__":
    axe_weapon()
    slash()
    comic_burst()
    lettering("БАХ!", "FX_Boom_Bah", -8)
    lettering("БУМ!", "FX_Boom_Bum", 6)
    lettering("БАБАХ!", "FX_Boom_Babah", -5)
    star()
    cloud()
    scorch()
    ring()
    shock_ring()
    marker_ring()
    target_ring()
    charge_line()
    acid_ball()
    acid_splat()
    aura()
