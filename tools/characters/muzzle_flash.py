"""Procedural muzzle-flash sprites for the player's rifle (replace the flash cut out of the video, which read as
a laser streak).

Writes to unity/Assets/Art/Textures/Characters/Player:
  Player_MuzzleFlash_0..2.png  three frames of a compact star flash: white-yellow core, orange body, darker orange
                               rim, short uneven spikes (the forward one only a little longer). Frame 0 is the
                               brightest, 1 and 2 are smaller and more orange (the flash dying out).
  Player_MuzzleGlow.png        soft orange halo around the muzzle.
  Player_MuzzleSmoke.png       faint grey puff that drifts up after a shot.
All are 128x128 with the muzzle at the centre (Center pivot import rule), facing +x (the sprite flips with the body).
"""
import math
import os

import numpy as np
from PIL import Image, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "unity", "Assets", "Art", "Textures", "Characters", "Player")
SIZE = 128
SS = 4                       # supersampling for smooth edges

CORE = np.array([1.0, 1.0, 0.92])
YELLOW = np.array([1.0, 0.86, 0.36])
ORANGE = np.array([1.0, 0.52, 0.12])
RIM = np.array([0.72, 0.24, 0.04])


def star_radius(angles, spikes, base, lengths, sharpness):
    """Radius of the star outline for each angle: a base disc plus narrow spikes."""
    r = np.full_like(angles, base)
    for a0, length in zip(spikes, lengths):
        d = np.angle(np.exp(1j * (angles - a0)))          # signed angular distance
        r = np.maximum(r, base + length * np.exp(-(d / sharpness) ** 2))
    return r


def flash(scale, heat, seed):
    rng = np.random.default_rng(seed)
    n = SIZE * SS
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    cx = n * 0.44                                             # a little behind centre: the flash points forward
    cy = n * 0.5
    dx, dy = x - cx, cy - y
    ang = np.arctan2(dy, dx)
    dist = np.hypot(dx, dy) / SS

    # Six short spikes around the muzzle, the forward ones a bit longer; small random wobble per frame.
    spikes = np.array([0.0, 0.55, -0.55, 1.35, -1.35, math.pi]) + rng.uniform(-0.12, 0.12, 6)
    lengths = np.array([21, 17, 17, 13, 13, 6], float) * scale * rng.uniform(0.85, 1.15, 6)
    base = 15 * scale
    outline = star_radius(ang, spikes, base, lengths, 0.2)
    t = dist / outline                                        # 0 at the core, 1 on the outline

    alpha = np.clip((1.0 - t) * 3.0, 0, 1)                   # soft edge
    colour = np.empty(t.shape + (3,), np.float32)
    k1 = np.clip((t - 0.1) / 0.35, 0, 1)[..., None]
    k2 = np.clip((t - 0.45) / 0.3, 0, 1)[..., None]
    k3 = np.clip((t - 0.75) / 0.25, 0, 1)[..., None]
    colour[:] = CORE * (1 - k1) + YELLOW * k1
    colour = colour * (1 - k2) + ORANGE * k2
    colour = colour * (1 - k3) + RIM * k3
    # Cooler, more orange frames as the flash dies.
    colour = colour * (1 - heat) + (ORANGE * 0.9) * heat * (t[..., None] > 0.15) + colour * heat * (t[..., None] <= 0.15)
    img = np.dstack([np.clip(colour, 0, 1), alpha])
    im = Image.fromarray((img * 255).astype(np.uint8), "RGBA").resize((SIZE, SIZE), Image.LANCZOS)
    return im


def radial(colour, alpha_peak, radius, noise=0.0, seed=0):
    n = SIZE
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    d = np.hypot(x - n / 2, y - n / 2) / radius
    a = np.clip(1 - d, 0, 1) ** 2 * alpha_peak
    if noise:
        rng = np.random.default_rng(seed)
        field = Image.fromarray((rng.random((16, 16)) * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC)
        a *= 1 - noise + noise * np.asarray(field, np.float32) / 255
    rgb = np.broadcast_to(np.array(colour, np.float32), (n, n, 3))
    im = Image.fromarray((np.dstack([rgb, a]) * 255).astype(np.uint8), "RGBA")
    return im.filter(ImageFilter.GaussianBlur(1.5))


def main():
    os.makedirs(OUT, exist_ok=True)
    frames = [(1.0, 0.0, 1), (0.8, 0.35, 2), (0.55, 0.75, 3)]
    for i, (scale, heat, seed) in enumerate(frames):
        flash(scale, heat, seed).save(os.path.join(OUT, f"Player_MuzzleFlash_{i}.png"), optimize=True)
    radial((1.0, 0.62, 0.22), 0.55, 60).save(os.path.join(OUT, "Player_MuzzleGlow.png"), optimize=True)
    radial((0.62, 0.6, 0.58), 0.5, 52, noise=0.6, seed=4).save(os.path.join(OUT, "Player_MuzzleSmoke.png"), optimize=True)

    # Check sheet on dark and light backgrounds.
    names = [f"Player_MuzzleFlash_{i}.png" for i in range(3)] + ["Player_MuzzleGlow.png", "Player_MuzzleSmoke.png"]
    sheet = Image.new("RGBA", (SIZE * len(names), SIZE * 2))
    for row, bg in enumerate([(24, 26, 32, 255), (225, 205, 170, 255)]):
        for i, name in enumerate(names):
            tile = Image.new("RGBA", (SIZE, SIZE), bg)
            tile.alpha_composite(Image.open(os.path.join(OUT, name)))
            sheet.paste(tile, (i * SIZE, row * SIZE))
    sheet.save(os.path.join(os.path.dirname(__file__), "player_mp4", "work", "muzzle_flash_check.png"))
    print("ok")


if __name__ == "__main__":
    main()
