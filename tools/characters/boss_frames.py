"""Boss sprites: recoloured, upscaled copies of the painted zombie frames (the same seven poses, so they animate like
the zombies), one clearly different look per boss.

  Boss1  "Матка"  (wave 10)  the brute, sickly violet skin, toxic-green glowing egg sacs
  Boss2  "Таран"  (wave 20)  the hard-hat worker, iron helmet, blue-grey skin, dark purple shirt
  Boss3  "Жнец"   (wave 30)  the brute, dark crimson skin, molten orange boils, black clothes

Frames are upscaled 2x (LANCZOS + a light unsharp mask) because bosses are drawn about twice as big as a zombie;
CharacterBuilder takes the 2x into account. Writes unity/Assets/Art/Textures/Characters/Boss<N>/Boss<N>_<frame>.png.
Run: python tools/characters/boss_frames.py
"""
import os

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CHARS = os.path.join(ROOT, "unity", "Assets", "Art", "Textures", "Characters")
FRAMES = 7
UPSCALE = 2


def rgb_to_hsv(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mx = rgb.max(-1)
    mn = rgb.min(-1)
    d = mx - mn
    h = np.zeros_like(mx)
    nz = d > 1e-6
    rr = nz & (mx == r)
    gg = nz & (mx == g) & ~rr
    bb = nz & ~rr & ~gg
    h[rr] = ((g - b)[rr] / d[rr]) % 6
    h[gg] = (b - r)[gg] / d[gg] + 2
    h[bb] = (r - g)[bb] / d[bb] + 4
    h = h * 60.0
    s = np.where(mx > 1e-6, d / np.maximum(mx, 1e-6), 0)
    return h, s, mx


def hsv_to_rgb(h, s, v):
    h = (h % 360.0) / 60.0
    i = np.floor(h).astype(int) % 6
    f = h - np.floor(h)
    p = v * (1 - s)
    q = v * (1 - s * f)
    t = v * (1 - s * (1 - f))
    out = np.zeros(h.shape + (3,))
    for k, (a, b, c) in enumerate(((v, t, p), (q, v, p), (p, v, t), (p, q, v), (t, p, v), (v, p, q))):
        m = i == k
        out[m, 0], out[m, 1], out[m, 2] = a[m], b[m], c[m]
    return out


def hue_band(h, lo, hi):
    return (h >= lo) & (h < hi) if lo < hi else (h >= lo) | (h < hi)


def recolor(img, rules):
    """rules: (hue_lo, hue_hi, min_sat, min_val, new_hue or None, sat_mul, val_mul); the first matching rule wins."""
    arr = np.asarray(img.convert("RGBA")).astype(np.float64) / 255.0
    rgb, a = arr[..., :3], arr[..., 3]
    h, s, v = rgb_to_hsv(rgb)
    done = np.zeros(h.shape, bool)
    nh, ns, nv = h.copy(), s.copy(), v.copy()
    for lo, hi, min_sat, min_val, new_hue, sat_mul, val_mul in rules:
        m = hue_band(h, lo, hi) & (s >= min_sat) & (v >= min_val) & ~done
        if new_hue is not None:
            nh[m] = new_hue
        ns[m] = np.clip(s[m] * sat_mul, 0, 1)
        nv[m] = np.clip(v[m] * val_mul, 0, 1)
        done |= m
    out = hsv_to_rgb(nh, ns, nv)
    return Image.fromarray((np.dstack([out, a]) * 255.0 + 0.5).astype(np.uint8), "RGBA")


BOSSES = {
    "Boss1": ("Brute", [
        (330, 25, 0.35, 0.72, 110.0, 1.0, 1.2),      # bright red boils -> toxic green glow
        (330, 25, 0.2, 0.0, 300.0, 0.55, 0.75),      # dark red flesh -> deep violet
        (25, 120, 0.12, 0.0, 285.0, 0.7, 0.95),      # olive skin -> sickly violet
        (180, 260, 0.15, 0.0, 265.0, 0.3, 0.6),      # blue jeans -> grey-violet rags
    ]),
    "Boss2": ("Worker", [
        (30, 65, 0.3, 0.6, 215.0, 0.16, 0.6),        # yellow hard hat -> iron helmet
        (62, 150, 0.12, 0.0, 195.0, 0.5, 0.82),      # green skin -> blue-grey
        (330, 30, 0.3, 0.0, 285.0, 0.75, 0.62),      # orange-red shirt -> dark purple
        (180, 260, 0.12, 0.0, 230.0, 0.6, 0.45),     # jeans -> almost black
    ]),
    "Boss3": ("Brute", [
        (330, 25, 0.35, 0.72, 34.0, 1.2, 1.35),      # bright boils -> molten orange
        (330, 25, 0.2, 0.0, 355.0, 0.8, 0.5),        # dark flesh -> dark crimson
        (25, 120, 0.12, 0.0, 350.0, 0.65, 0.42),     # skin -> charred crimson-grey
        (180, 260, 0.12, 0.0, 250.0, 0.2, 0.3),      # clothes -> black
    ]),
}


def main():
    for boss, (base, rules) in BOSSES.items():
        folder = os.path.join(CHARS, boss)
        os.makedirs(folder, exist_ok=True)
        for i in range(FRAMES):
            src = Image.open(os.path.join(CHARS, base, f"{base}_{i}.png"))
            img = recolor(src, rules)
            w, h = img.size
            img = img.resize((w * UPSCALE, h * UPSCALE), Image.LANCZOS)
            rgb = img.convert("RGB").filter(ImageFilter.UnsharpMask(radius=2, percent=60, threshold=2))
            img = Image.merge("RGBA", (*rgb.split(), img.split()[3]))
            img.save(os.path.join(folder, f"{boss}_{i}.png"), optimize=True)
        print(boss, "from", base, img.size)


if __name__ == "__main__":
    main()
