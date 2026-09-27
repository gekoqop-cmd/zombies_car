"""Soft light pool under a street lamp (Map02): radial falloff, bright core, long smooth tail.
Writes unity/Assets/Art/VFX/T_LightPool.png (white RGB, falloff in alpha; tinted and added by M_FX_LightPool)."""
import os

import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "unity", "Assets", "Art", "VFX", "T_LightPool.png")
SIZE = 256

y, x = np.mgrid[0:SIZE, 0:SIZE].astype(np.float32)
d = np.hypot(x - SIZE / 2 + 0.5, y - SIZE / 2 + 0.5) / (SIZE / 2)
alpha = np.clip(1 - d, 0, 1) ** 2.2                      # smooth tail to the edge
alpha = alpha * 0.85 + np.exp(-(d / 0.25) ** 2) * 0.15   # brighter core right under the lamp
rgba = np.dstack([np.full_like(alpha, 255), np.full_like(alpha, 255), np.full_like(alpha, 255), alpha * 255]).astype(np.uint8)
Image.fromarray(rgba, "RGBA").save(OUT, optimize=True)
print(OUT)
