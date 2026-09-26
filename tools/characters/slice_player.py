"""Cuts referens/person.png (labelled panels: run right/left, idle, shoot, reload, turn down/up) into the player's
sprite frames for Unity. python slice_player.py [--preview <png>]

Each panel is a row of evenly spaced frames on a flat dark background (no painted ground shadow, unlike
characters.png). Frames are found by splitting each panel into equal slots and keeping the largest connected
shape in every slot (small bleed from the neighbouring frame stays a separate, smaller piece and is dropped).
All frames of one clip share one canvas: the feet (lowest opaque pixel) always sit on the bottom row, centred on x,
so Unity's bottom-centre sprite pivot (see ArtImportPostprocessor) lines up for every frame without per-frame data.
Output: unity/Assets/Art/Textures/Characters/Player/<Clip>_<i>.png; the shoot frame's gun tip (for the muzzle
flash anchor, used by CharacterBuilder.cs) is printed at the end.
"""
import os, sys
import numpy as np
from PIL import Image
from scipy import ndimage as nd

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "referens", "person.png")
OUT = os.path.join(ROOT, "unity", "Assets", "Art", "Textures", "Characters", "Player")

LO, HI = 20.0, 65.0        # distance-from-background soft alpha ramp
PAD = 3
SLOT_MARGIN = 0.12         # extra share of one slot width kept on each side (catches limbs reaching out)


def flash_mask(rgb):
    """Painted muzzle flash / spark colours (orange-yellow-red), baked into some shoot frames: not part of the
    body (the game shows its own procedural flash only while firing) so it is dropped like the background.
    Dilated generously: a faint ray tip left behind would otherwise bridge into the next frame's slot."""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    mask = (r > 190) & (g > 110) & (r - b > 70)
    return nd.binary_dilation(mask, iterations=3)


def panel_bg(panel):
    """The panel's own flat interior fill (slightly lighter than the outer canvas), sampled where no
    character reaches: a band just below the header/border, before any silhouette starts."""
    band = panel[4:14, :, :].reshape(-1, 3)
    return np.median(band, axis=0)

# (x0, x1, y0, y1, frame count, clip name); panel bodies only (header, numbered chip labels below the
# character and the "run left" panel -- redundant with Move mirrored -- are excluded).
PANELS = [
    (0, 1448, 46, 234, 6, "Move"),
    (0, 718, 586, 796, 4, "Idle"),
    (718, 1448, 586, 796, 4, "Shoot"),
    (0, 565, 850, 1022, 4, "Reload"),
    (565, 1035, 850, 1022, 4, "TurnDown"),
    (1035, 1448, 850, 1022, 4, "TurnUp"),
]

# Right-side slot margin override: the shoot pose's muzzle flash reaches well into the next slot; a normal
# margin there would pull in the next frame's backpack once the flash itself is masked out.
RIGHT_MARGIN_OVERRIDE = {"Shoot": -0.28}


def alpha_of(rgb, bg):
    d = np.sqrt(((rgb - bg) ** 2).sum(-1))
    alpha = np.clip((d - LO) / (HI - LO), 0.0, 1.0)
    alpha[flash_mask(rgb)] = 0.0
    return alpha


def largest_component(mask):
    lab, n = nd.label(nd.binary_dilation(mask, iterations=1))
    if n == 0:
        return None
    sizes = nd.sum(mask, lab, range(1, n + 1))
    return lab == (1 + int(np.argmax(sizes)))


def extract_frame(rgb_panel, x0_slot, x1_slot, bg):
    crop_rgb = rgb_panel[:, x0_slot:x1_slot]
    alpha = alpha_of(crop_rgb, bg)
    core = largest_component(alpha > 0.5)
    if core is None:
        raise RuntimeError("no shape found in slot")
    ys, xs = np.nonzero(nd.binary_dilation(core, iterations=2))
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    sub_rgb = crop_rgb[y0:y1, x0:x1]
    sub_alpha = alpha[y0:y1, x0:x1]
    sub_core = core[y0:y1, x0:x1]
    # keep only the main shape's soft edge (drop stray bleed from the neighbouring frame inside this crop)
    keep = nd.binary_dilation(sub_core, iterations=3)
    sub_alpha = np.where(keep, sub_alpha, 0.0)
    a = sub_alpha[..., None]
    fg = np.where(a > 0.02, (sub_rgb - (1 - a) * bg) / np.maximum(a, 1e-3), sub_rgb)
    solid = sub_alpha > 0.5
    ys2 = np.nonzero(solid.max(1))[0]
    xs2 = np.nonzero(solid.max(0))[0]
    bottom = ys2.max()
    low = solid[max(0, bottom - 10):bottom + 1]
    feet_x = float(np.nonzero(low.max(0))[0].mean()) if low.any() else float((xs2.min() + xs2.max()) / 2)
    return np.clip(fg, 0, 255), sub_alpha, feet_x, bottom


def pack_clip(frames, name):
    left = max(fx - np.nonzero(al.max(0) > 0.01)[0].min() for _, al, fx, _ in frames)
    right = max(np.nonzero(al.max(0) > 0.01)[0].max() + 1 - fx for _, al, fx, _ in frames)
    up = max(bottom - np.nonzero(al.max(1) > 0.01)[0].min() for _, al, fx, bottom in frames)
    half = int(np.ceil(max(left, right))) + PAD
    W = (2 * half + 3) // 4 * 4
    H = (int(np.ceil(up)) + 1 + PAD + 3) // 4 * 4
    os.makedirs(OUT, exist_ok=True)
    outputs = []
    for i, (fg, alpha, feet_x, bottom) in enumerate(frames):
        canvas = np.zeros((H, W, 4))
        ox = int(round(W / 2 - feet_x))
        oy = H - 1 - bottom
        h, w = alpha.shape
        ys0, xs0 = max(0, oy), max(0, ox)
        ys1, xs1 = min(H, oy + h), min(W, ox + w)
        canvas[ys0:ys1, xs0:xs1, :3] = fg[ys0 - oy:ys1 - oy, xs0 - ox:xs1 - ox]
        canvas[ys0:ys1, xs0:xs1, 3] = alpha[ys0 - oy:ys1 - oy, xs0 - ox:xs1 - ox] * 255
        solid = canvas[..., 3] > 127
        if solid.any():
            _, (iy, ix) = nd.distance_transform_edt(~solid, return_indices=True)
            canvas[..., :3] = np.where(solid[..., None], canvas[..., :3], canvas[iy, ix, :3])
        img = Image.fromarray(canvas.astype(np.uint8), "RGBA")
        img.save(os.path.join(OUT, f"Player_{name}_{i}.png"))
        outputs.append(img)
    print(f"{name}: canvas {W}x{H}, {len(frames)} frames")
    return outputs


def gun_tip(alpha, feet_x, bottom):
    """Rightmost opaque pixel in the gun/arm band (35%-60% of the sprite height from the top), x right / y up from feet."""
    h, w = alpha.shape
    solid = alpha > 0.5
    top = np.nonzero(solid.max(1))[0].min()
    band0, band1 = int(top + 0.30 * (bottom - top)), int(top + 0.62 * (bottom - top))
    band = solid[band0:band1 + 1]
    ys, xs = np.nonzero(band)
    tip_x, tip_y = int(xs.max()), int(band0 + ys[xs == xs.max()].mean())
    return round(tip_x - feet_x, 1), round(bottom - tip_y, 1)


def main():
    preview_path = sys.argv[sys.argv.index("--preview") + 1] if "--preview" in sys.argv else None
    rgb = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float64)
    previews = []
    tip = None
    for x0, x1, y0, y1, count, name in PANELS:
        panel = rgb[y0:y1, x0:x1]
        bg = panel_bg(panel)
        slot = panel.shape[1] / count
        margin = int(slot * SLOT_MARGIN)
        right_margin = int(slot * RIGHT_MARGIN_OVERRIDE.get(name, SLOT_MARGIN))
        frames = []
        for i in range(count):
            sx0 = max(0, int(round(i * slot)) - margin)
            sx1 = min(panel.shape[1], int(round((i + 1) * slot)) + right_margin)
            frames.append(extract_frame(panel, sx0, sx1, bg))
        imgs = pack_clip(frames, name)
        previews.append((name, imgs))
        if name == "Shoot":
            _, alpha0, feet_x0, bottom0 = frames[0]
            tip = gun_tip(alpha0, feet_x0, bottom0)
            print("Shoot_0 gun tip (px from feet, x right / y up):", tip)

    if preview_path:
        cw = max(im.width for _, imgs in previews for im in imgs) + 8
        ch = max(im.height for _, imgs in previews for im in imgs) + 8
        cols = max(len(imgs) for _, imgs in previews)
        sheet = Image.new("RGBA", (cw * cols, ch * len(previews)), (90, 110, 90, 255))
        for r, (name, imgs) in enumerate(previews):
            for c, im in enumerate(imgs):
                x, y = c * cw + (cw - im.width) // 2, r * ch + ch - 4 - im.height
                tile = Image.new("RGBA", im.size, (150, 150, 150, 255) if (r + c) % 2 else (120, 120, 140, 255))
                sheet.alpha_composite(tile, (x, y))
                sheet.alpha_composite(im, (x, y))
        sheet.save(preview_path)
        print("preview", preview_path)


if __name__ == "__main__":
    main()
