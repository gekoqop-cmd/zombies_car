"""Textures used only by the Ultra graphics tier.

T_Asphalt_MS.png - metallic/smoothness map for the Ultra asphalt (URP Lit): R = metallic (0), A = smoothness.
The wet patches follow the oil/wet stains painted into T_Asphalt.png (same periodic noise, same seed as in
gen_unity_textures.py), so the stains turn into glossy puddles that catch the reflection probe.
python gen_ultra_textures.py"""
import os

import numpy as np
from PIL import Image

ENV = r"D:\zombi-caaar\unity\Assets\Art\Textures\Environment"
N = 1024
BASE_SMOOTHNESS = 0.18
PUDDLE_SMOOTHNESS = 0.82


def pnoise(n, cells, seed):
    """Tileable value noise n x n in [0,1] (identical to gen_unity_textures.py)."""
    g = np.random.default_rng(seed).random((cells, cells))
    t = np.arange(n) * cells / n
    i0 = np.floor(t).astype(int) % cells; i1 = (i0 + 1) % cells
    f = t - np.floor(t); f = f * f * (3 - 2 * f)
    a = g[i0][:, i0]; b = g[i0][:, i1]; c = g[i1][:, i0]; d = g[i1][:, i1]
    fx = f[None, :]; fy = f[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def pfbm(n, cells, octaves, seed):
    acc = np.zeros((n, n)); amp = 1.0; tot = 0.0
    for o in range(octaves):
        acc += amp * pnoise(n, cells * 2 ** o, seed + 17 * o); tot += amp; amp *= 0.5
    return acc / tot


# Same noise field as the asphalt stains (those start at 0.66); the wet area reaches a little past the dark stain.
stain = np.clip((pfbm(N, 5, 3, 11) - 0.6) * 3.5, 0, 1)
smooth = BASE_SMOOTHNESS + (PUDDLE_SMOOTHNESS - BASE_SMOOTHNESS) * stain ** 0.7
out = np.zeros((N, N, 4))
out[..., 3] = smooth
Image.fromarray((np.clip(out, 0, 1) * 255).astype(np.uint8), "RGBA").save(os.path.join(ENV, "T_Asphalt_MS.png"))
print("saved T_Asphalt_MS.png, wet share", round(float((stain > 0.5).mean()), 3))
