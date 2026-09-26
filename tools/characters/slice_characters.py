"""Cuts referens/characters.png (7 rows x 7 frames: player + 6 zombie types) into sprite frames for Unity.
python slice_characters.py [--preview <png>]

Per frame: the beige background and the painted ground shadow are removed (the game draws its own blob shadow),
motion lines / tracers / exclamation marks that float free of the body are dropped, edge colours are un-mixed from
the background. All frames of one character share one canvas size with the feet on the bottom edge and the feet
centre on the vertical centre line, so every frame imports with the same bottom-centre pivot and the animation
does not jitter. Output: unity/Assets/Art/Textures/Characters/<Name>/<Name>_<frame>.png (+ Player_MuzzleFlash.png);
frame sizes and the player's gun tip (used by CharacterBuilder.cs) are written to tools/characters/characters.json.
"""
import os, sys, json
import numpy as np
from PIL import Image
from scipy import ndimage as nd

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "referens", "characters.png")
OUT = os.path.join(ROOT, "unity", "Assets", "Art", "Textures", "Characters")

ROWS = ["Player", "Walker", "Worker", "RedCap", "Hoodie", "Brute", "Hound"]
FRAMES = 7
LABEL_EDGE = 60                # the row captions are boxes starting at x ~28
BG = np.array([204.0, 180.0, 141.0])
BODY_DISTANCE = 45             # colour distance from the background that counts as character
OUTLINE_DISTANCE = 190         # distance of the dark outline: edge pixels get alpha = distance / this
ATTACH_PX = 5                  # loose pieces closer than this to the body stay (blood, dust on the feet)
MIN_PIECE = 15
BG_HOLE = 10                   # enclosed flat background (gap between arm and body) is cut out again; pale skin
                               # patches of the brute are close to the beige too but noisier (mean distance > this)
PAD = 3
SHOOT_FRAMES = (3, 4)          # player frames with a painted muzzle flash / tracer
GUN_REACH = 55                 # flash pixels are this far in front of the feet (the face is closer)


def distance_map(rgb):
    d = np.sqrt(((rgb - BG) ** 2).sum(-1))
    ratio = rgb / BG
    k = ratio.mean(-1)
    spread = ratio.max(-1) - ratio.min(-1)
    shadow = (k > 0.5) & (k < 0.99) & (spread < 0.12)      # darker beige = painted ground shadow
    dust = (k > 1.03) & (k < 1.4) & (spread < 0.12)         # lighter beige = dust puffs at the feet
    return d, shadow | dust


def find_bands(body):
    rows = body.sum(1) > 3
    lab, n = nd.label(rows)
    bands = []
    for i in range(1, n + 1):
        ys = np.nonzero(lab == i)[0]
        if ys.size > 40:
            bands.append((ys.min(), ys.max() + 1))
    assert len(bands) == len(ROWS), f"expected {len(ROWS)} rows, found {len(bands)}"
    return bands


def frame_masks(body, y0, y1):
    """Returns 7 (mask, offset) pairs for one row, left to right."""
    y0, y1 = max(0, y0 - 6), min(body.shape[0], y1 + 6)
    m = body[y0:y1]
    lab, n = nd.label(nd.binary_dilation(m, iterations=2))
    sizes = nd.sum(m, lab, range(1, n + 1))
    order = np.argsort(sizes)[::-1][:FRAMES]
    boxes = nd.find_objects(lab)
    mains = sorted(order + 1, key=lambda i: boxes[i - 1][1].start)
    result = []
    for li in mains:
        main = (lab == li) & m
        # loose pieces near the body
        dist_to_main = nd.distance_transform_edt(~main)
        pieces, pn = nd.label(m & ~main)
        for pi in range(1, pn + 1):
            piece = pieces == pi
            if piece.sum() >= MIN_PIECE and dist_to_main[piece].min() <= ATTACH_PX:
                main |= piece
        result.append((main, y0))
    return result


def flash_mask(rgb):
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    return (r > 200) & (g > 130) & (b < 150) & (r - b > 80)


def split_flash(mask, oy, flash):
    """Removes the muzzle flash and tracer in front of the gun; returns (body mask, flash mask)."""
    ys, xs = np.nonzero(mask)
    ax = xs[ys >= ys.max() - 10].mean()
    region = flash[oy:oy + mask.shape[0]] & (np.arange(mask.shape[1])[None, :] > ax + GUN_REACH)
    lab, n = nd.label(nd.binary_dilation(region, iterations=2))
    fl = np.zeros_like(mask)
    for i in range(1, n + 1):
        comp = lab == i
        if (comp & mask).sum() > 20:
            fl |= comp & (mask | region)
    body = mask & ~fl
    lab, n = nd.label(body)
    sizes = nd.sum(body, lab, range(1, n + 1))
    body = lab == 1 + int(np.argmax(sizes))                     # the tracer bullet floats free: gone
    return body, fl


def cut(rgb, d, shadow, mask):
    hard = nd.binary_closing(mask, iterations=2) | mask
    filled = nd.binary_fill_holes(hard)
    holes, hn = nd.label(filled & ~hard)
    for hi in range(1, hn + 1):
        h = holes == hi
        if h.sum() > 12 and d[h].mean() < BG_HOLE:
            filled &= ~h
    interior = nd.binary_erosion(filled, iterations=1)
    ring = nd.binary_dilation(filled, iterations=1) & ~interior
    alpha = np.where(interior, 1.0, 0.0)
    edge = np.clip(d / OUTLINE_DISTANCE, 0, 1)
    edge[ring & shadow & ~filled] = 0.0
    alpha = np.where(ring, np.maximum(alpha, edge * (filled | (edge > 0.35))), alpha)
    a = alpha[..., None]
    fg = np.where(a > 0.02, (rgb - (1 - a) * BG) / np.maximum(a, 1e-3), rgb)   # un-mix the beige
    return np.clip(fg, 0, 255), alpha


def feet_anchor(alpha, lying):
    solid = alpha > 0.5
    ys, xs = np.nonzero(solid)
    bottom = ys.max()
    if lying:
        low = ys > (ys.min() + bottom) / 2
        return float(xs[low].mean()), bottom
    low = ys >= bottom - 10
    return float(xs[low].mean()), bottom


def main():
    preview_path = sys.argv[sys.argv.index("--preview") + 1] if "--preview" in sys.argv else None
    rgb = np.asarray(Image.open(SRC).convert("RGB")).astype(np.float64)
    d, shadow = distance_map(rgb)
    body = (d > BODY_DISTANCE) & ~shadow
    lab, _ = nd.label(nd.binary_dilation(body, iterations=1))
    for i, box in enumerate(nd.find_objects(lab)):
        if box is not None and box[1].start < LABEL_EDGE:
            body[lab == i + 1] = False                           # caption boxes
    flash = flash_mask(rgb)
    bands = find_bands(body)

    info = {}
    previews = []
    for name, (y0, y1) in zip(ROWS, bands):
        frames = []
        for fi, (mask, oy) in enumerate(frame_masks(body, y0, y1)):
            if name == "Player" and fi in SHOOT_FRAMES:
                mask, fl = split_flash(mask, oy, flash)
                if fi == SHOOT_FRAMES[0]:
                    flash_piece = (mask, oy, fl)
            full = np.zeros(body.shape, bool)
            full[oy:oy + mask.shape[0]] = mask
            ys, xs = np.nonzero(nd.binary_dilation(full, iterations=3))
            sl = (slice(ys.min(), ys.max() + 1), slice(xs.min(), xs.max() + 1))
            fg, alpha = cut(rgb[sl], d[sl], shadow[sl], full[sl])
            lying = fi == FRAMES - 1 and name != "Player"
            ax, bottom = feet_anchor(alpha, lying)
            frames.append((fg, alpha, ax, bottom))
            if name == "Player" and fi == SHOOT_FRAMES[0]:
                feet_src = (sl[1].start + ax, sl[0].start + bottom)
                tip = gun_tip(flash_piece[0], flash_piece[1], flash_piece[2], feet_src)

        # One canvas per character: feet centre at x = W/2, feet on the bottom row (bottom-centre pivot in Unity).
        left = max(ax - np.nonzero(al.max(0) > 0.01)[0].min() for _, al, ax, _ in frames)
        right = max(np.nonzero(al.max(0) > 0.01)[0].max() + 1 - ax for _, al, ax, _ in frames)
        up = max(b - np.nonzero(al.max(1) > 0.01)[0].min() for _, al, _, b in frames)
        half = int(np.ceil(max(left, right))) + PAD
        W = (2 * half + 3) // 4 * 4
        H = (int(np.ceil(up)) + 1 + PAD + 3) // 4 * 4
        folder = os.path.join(OUT, name)
        os.makedirs(folder, exist_ok=True)
        heights = []
        for fi, (fg, alpha, ax, bottom) in enumerate(frames):
            canvas = np.zeros((H, W, 4))
            ox = int(round(W / 2 - ax))
            oy = H - 1 - bottom
            h, w = alpha.shape
            ys0, xs0 = max(0, oy), max(0, ox)
            ys1, xs1 = min(H, oy + h), min(W, ox + w)
            canvas[ys0:ys1, xs0:xs1, :3] = fg[ys0 - oy:ys1 - oy, xs0 - ox:xs1 - ox]
            canvas[ys0:ys1, xs0:xs1, 3] = alpha[ys0 - oy:ys1 - oy, xs0 - ox:xs1 - ox] * 255
            # transparent texels take the nearest opaque colour (no dark rim in bilinear / mip sampling)
            solid = canvas[..., 3] > 127
            _, (iy, ix) = nd.distance_transform_edt(~solid, return_indices=True)
            canvas[..., :3] = np.where(solid[..., None], canvas[..., :3], canvas[iy, ix, :3])
            img = Image.fromarray(canvas.astype(np.uint8), "RGBA")
            img.save(os.path.join(folder, f"{name}_{fi}.png"))
            previews.append(img)
            ys = np.nonzero(canvas[..., 3].max(1) > 127)[0]
            heights.append(int(ys.max() - ys.min() + 1))
        info[name] = {"canvas": [W, H], "pad": PAD, "heights": heights}
        print(f"{name}: canvas {W}x{H}, frame heights {heights}")
        if name == "Player":
            info[name]["muzzle"] = {"flash_canvas": save_flash(folder), "tip_px": tip}
            print(f"Player gun tip {tip} px from the feet")

    json.dump(info, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "characters.json"), "w"), indent=1)
    if preview_path:
        write_preview(previews, preview_path)


def gun_tip(body_mask, oy, flash_mask_piece, feet_src):
    """Barrel end of the shoot frame (rightmost gun pixel level with the flash), in pixels from the feet (x right, y up)."""
    fy = np.nonzero(flash_mask_piece)[0].mean()
    rows = np.nonzero(body_mask)
    near = np.abs(rows[0] - fy) <= 4
    tip_x = rows[1][near].max()
    tip_y = oy + fy
    return [round(float(tip_x - feet_src[0]), 1), round(float(feet_src[1] - tip_y), 1)]


def save_flash(folder, size=(128, 64)):
    """Procedural muzzle flash (the painted one is tiny and mixed with the beige). Centre pivot = gun tip: the flash
    fills the right half of the canvas, pointing +x."""
    W, H = size
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float64)
    x = (xx + 0.5 - W / 2) / (W / 2)                # 0 at the tip .. 1 at the right edge
    y = (yy + 0.5 - H / 2) / (W / 2)                # same scale as x
    ang = np.arctan2(y, np.maximum(x, 1e-3))
    r = np.hypot(x, y)
    rays = [(0.0, 0.72, 0.2), (0.5, 0.45, 0.18), (-0.5, 0.45, 0.18), (1.0, 0.25, 0.16), (-1.0, 0.25, 0.16)]
    limit = 0.2 + sum(length * np.exp(-((ang - a) / w) ** 2) for a, length, w in rays)
    alpha = np.clip((limit - r) / 0.05, 0, 1) * (x > -0.02)
    t = np.clip(r / limit, 0, 1)                    # 0 core .. 1 ray tips
    white, yellow, orange = np.array([255, 252, 235]), np.array([255, 214, 70]), np.array([255, 128, 24])
    rgb = np.where((t < 0.45)[..., None], white + (yellow - white) * (t / 0.45)[..., None],
                   yellow + (orange - yellow) * ((t - 0.45) / 0.55)[..., None])
    img = Image.fromarray(np.dstack([rgb, alpha * 255]).astype(np.uint8), "RGBA")
    img.save(os.path.join(folder, "Player_MuzzleFlash.png"))
    return [W, H]


def write_preview(images, path):
    cw = max(i.width for i in images) + 8
    ch = max(i.height for i in images) + 8
    sheet = Image.new("RGBA", (cw * FRAMES, ch * len(ROWS)), (90, 110, 90, 255))
    for n, im in enumerate(images):
        r, c = divmod(n, FRAMES)
        x, y = c * cw + (cw - im.width) // 2, r * ch + ch - 4 - im.height
        tile = Image.new("RGBA", im.size, (150, 150, 150, 255) if (r + c) % 2 else (120, 120, 140, 255))
        sheet.alpha_composite(tile, (x, y))
        sheet.alpha_composite(im, (x, y))
    sheet.save(path)
    print("preview", path)


if __name__ == "__main__":
    main()
