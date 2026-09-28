"""Comic-book effect sprites for the game (bright colours, thick ink outlines, "BOOM"-style lettering).

Writes to unity/Assets/Art/VFX (alpha-blended VFX import rules):
  FX_ShockRing.png   electric ring of the car's discharge upgrade (flat on the ground)
  FX_MarkerRing.png  soft dashed ring under interaction spots (the workshop)
Run: python tools/vfx/comic_fx.py
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

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


if __name__ == "__main__":
    shock_ring()
    marker_ring()
