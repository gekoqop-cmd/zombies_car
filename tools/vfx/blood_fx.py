"""Blood textures of the zombie death burst (ZombieGibs): a glossy drop and a ground splat with droplets.
python tools/vfx/blood_fx.py  ->  unity/Assets/Art/VFX/FX_BloodDrop.png, FX_BloodSplat.png
"""
import os

import numpy as np
from PIL import Image
from scipy import ndimage as nd

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "unity", "Assets", "Art", "VFX")
DARK = np.array([0.30, 0.01, 0.02])
MID = np.array([0.62, 0.03, 0.05])
SHINE = np.array([0.95, 0.45, 0.42])


def save(rgb, alpha, name):
    img = np.dstack([np.clip(rgb, 0, 1), np.clip(alpha, 0, 1)])
    Image.fromarray((img * 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, name + ".png"), optimize=True)


def shade(mask, size):
    """Thick liquid: dark rim, mid body, a highlight up-left."""
    depth = nd.distance_transform_edt(mask)
    depth = np.clip(depth / (size * 0.06), 0, 1)
    rgb = DARK[None, None] * (1 - depth[..., None]) + MID[None, None] * depth[..., None]
    lit = nd.shift(depth, (size * 0.025, size * 0.025), order=1) - depth
    rgb += np.clip(-lit * 2.5, 0, 1)[..., None] * (SHINE - MID)[None, None]
    alpha = nd.gaussian_filter(mask.astype(float), 0.8)
    return rgb, alpha


def drop(size=64):
    y, x = np.mgrid[0:size, 0:size] / (size - 1) * 2 - 1
    r = np.sqrt(x ** 2 + y ** 2)
    mask = r < 0.82
    rgb, alpha = shade(mask, size)
    hl = np.exp(-(((x + 0.3) ** 2 + (y + 0.35) ** 2) / 0.02))
    rgb += hl[..., None] * (SHINE - MID)[None, None]
    save(rgb, alpha, "FX_BloodDrop")


def splat(size=256, seed=7):
    rnd = np.random.RandomState(seed)
    y, x = np.mgrid[0:size, 0:size].astype(float)
    c = size / 2
    ang = np.arctan2(y - c, x - c)
    r = np.hypot(x - c, y - c)
    # A lumpy pool: radius modulated by a few low harmonics, then spikes (spatter arms).
    rad = size * 0.26
    for k in (3, 5, 7, 11):
        rad = rad + size * rnd.uniform(0.015, 0.035) * np.sin(k * ang + rnd.uniform(0, 6.28))
    mask = r < rad
    for _ in range(9):
        a = rnd.uniform(0, 6.28)
        length = size * rnd.uniform(0.32, 0.46)
        width = size * rnd.uniform(0.025, 0.045)
        t = (x - c) * np.cos(a) + (y - c) * np.sin(a)
        n = np.abs(-(x - c) * np.sin(a) + (y - c) * np.cos(a))
        mask |= (t > 0) & (t < length) & (n < width * (1 - t / length))
        # A droplet at the end of the arm.
        dx, dy = c + np.cos(a) * length * 1.08, c + np.sin(a) * length * 1.08
        mask |= np.hypot(x - dx, y - dy) < width * 1.2
    for _ in range(16):
        a, d = rnd.uniform(0, 6.28), size * rnd.uniform(0.3, 0.47)
        mask |= np.hypot(x - c - np.cos(a) * d, y - c - np.sin(a) * d) < size * rnd.uniform(0.008, 0.022)
    mask = nd.binary_opening(mask, iterations=1)
    rgb, alpha = shade(mask, size)
    save(rgb, alpha * 0.92, "FX_BloodSplat")


if __name__ == "__main__":
    drop()
    splat()
    print("blood textures written")
