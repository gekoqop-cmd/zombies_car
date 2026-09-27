"""Stage 2: clean, align, loop and export the player frames from refsanim/Player/*.mp4.

Input : work/raw/<clip>/NNNN.png (ffmpeg frames, 24 fps) and work/mask/<clip>/NNNN.png (rembg alpha, masks.py).
Output: unity/Assets/Art/Textures/Characters/Player/Player_<Clip>_NN.png   aligned frames, feet at the bottom centre
        unity/Assets/Art/Textures/UI/UI_PortraitPlayer.png                   head and shoulders of the first idle frame
        unity/Assets/Data/Characters/Player_Frames.json                      clips, fps, gun tip per frame, run speed
        work/checksheet.png                                                  6 frames per clip on dark and light
python process.py
"""
import json
import os

import numpy as np
from PIL import Image
from scipy import ndimage as nd

HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.join(HERE, "work")
UNITY = os.path.normpath(os.path.join(HERE, "..", "..", "..", "unity", "Assets"))
OUT_FRAMES = os.path.join(UNITY, "Art", "Textures", "Characters", "Player")
OUT_PORTRAIT = os.path.join(UNITY, "Art", "Textures", "UI", "UI_PortraitPlayer.png")
OUT_JSON = os.path.join(UNITY, "Data", "Characters", "Player_Frames.json")

SOURCE_FPS = 24
TARGET_HEIGHT = 320          # px of the standing character (idle) in the exported frames
MARGIN = 6                   # px of empty border around the canvas
WATERMARK = (0.72, 0.90)     # KlingAI mark: x > 72 % and y > 90 % of the frame
NEAR = 14                    # px: detached pieces this close to the body are kept (hair tips, gun parts)


# ------------------------------------------------------------------ cleanup of one frame
def hsv(rgb):
    mx = rgb.max(axis=2)
    mn = rgb.min(axis=2)
    sat = np.where(mx > 1e-6, (mx - mn) / np.maximum(mx, 1e-6), 0.0)
    return sat, mx


def clean(rgb, alpha):
    """rgb, alpha in 0..1 (full video frame). Returns rgb (decontaminated), alpha."""
    h, w = alpha.shape
    alpha = alpha.copy()
    alpha[int(h * WATERMARK[1]):, int(w * WATERMARK[0]):] = 0.0            # watermark area

    # Keep the body and pieces close to it, drop specks and leftovers of the watermark.
    solid = alpha > 0.15
    lab, n = nd.label(solid)
    if n > 1:
        sizes = nd.sum(np.ones_like(alpha), lab, range(1, n + 1))
        main = lab == (1 + int(np.argmax(sizes)))
        near = nd.binary_dilation(main, iterations=NEAR)
        keep_ids = np.unique(lab[near & solid])
        keep = np.isin(lab, keep_ids[keep_ids > 0])
        alpha = alpha * nd.binary_dilation(keep, iterations=2)

    sat, val = hsv(rgb)
    background = alpha < 0.08

    # White background pockets rembg kept: near-white pixels connected to the background through near-white
    # pixels (enclosed whites such as the eyes and the medkit cross are not reached).
    whiteish = (val > 0.9) & (sat < 0.08)
    lab, _ = nd.label(whiteish | background)
    outside = np.unique(lab[background])
    pocket = np.isin(lab, outside[outside > 0]) & whiteish
    alpha[pocket] = 0.0

    # Soft floor shadow (shot, reload): greyish, mid-bright, in the lowest part of the body, touching the background.
    ys = np.nonzero((alpha > 0.5).any(axis=1))[0]
    if len(ys):
        top, bottom = ys[0], ys[-1]
        band = np.zeros_like(alpha, dtype=bool)
        band[int(bottom - (bottom - top) * 0.1):, :] = True
        greyish = (sat < 0.2) & (val > 0.42) & band
        lab, _ = nd.label(greyish | (alpha < 0.08))
        touching = np.unique(lab[alpha < 0.08])
        shadow = np.isin(lab, touching[touching > 0]) & greyish
        alpha[shadow] = 0.0

    # Despill: take the white background back out of the soft edge, then erode the matte by 1 px.
    bg = np.median(rgb[background], axis=0) if background.any() else np.ones(3)
    a = np.clip(alpha, 1e-3, 1.0)[..., None]
    rgb = np.clip((rgb - (1.0 - a) * bg) / a, 0.0, 1.0)
    alpha = nd.grey_erosion(alpha, size=(3, 3))
    alpha[alpha < 0.04] = 0.0
    return rgb, alpha


def bleed(rgb, alpha):
    """RGB of transparent pixels = nearest opaque colour (no dark or white halo in mip maps)."""
    solid = alpha > 0.5
    if not solid.any():
        return rgb
    _, (iy, ix) = nd.distance_transform_edt(~solid, return_indices=True)
    return rgb[iy, ix]


# ------------------------------------------------------------------ geometry helpers
def bbox(alpha, thr=0.5):
    ys, xs = np.nonzero(alpha > thr)
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


def head_x(alpha):
    """Centre x of the head (top 22 % of the silhouette): a stable horizontal anchor in every pose."""
    x0, y0, x1, y1 = bbox(alpha)
    rows = alpha[y0:y0 + int((y1 - y0) * 0.22)] > 0.5
    xs = np.nonzero(rows)[1]
    return float(xs.mean())


def gun_tip(alpha, x_anchor):
    """Rightmost silhouette point between 25 % and 60 % of the height (the rifle muzzle, facing right)."""
    x0, y0, x1, y1 = bbox(alpha)
    r0, r1 = y0 + int((y1 - y0) * 0.25), y0 + int((y1 - y0) * 0.6)
    band = alpha[r0:r1] > 0.5
    cols = np.nonzero(band.any(axis=0))[0]
    x = cols.max()
    ys = np.nonzero(band[:, x - 2:x + 1].any(axis=1))[0]
    return float(x), float(r0 + ys.mean())


FLASH_BAND = 110            # source px above / below the barrel that can belong to the flash


def flash_mask(alpha, tip):
    """The muzzle flash is the only thing right of the barrel end (tip from a frame without a flash)."""
    m = alpha > 0.03
    m[:, :int(tip[0]) + 2] = False
    m[:max(0, int(tip[1]) - FLASH_BAND), :] = False
    m[int(tip[1]) + FLASH_BAND:, :] = False
    return m


def contact_speed(frames, anchors):
    """Ground speed of the run (the take runs on the spot): the planted foot slides back at that speed."""
    xs = []
    for (_, a), (_, ground) in zip(frames, anchors):
        rows = a[int(ground) - 6:int(ground) + 2] > 0.5
        px = np.nonzero(rows)[1]
        xs.append(px.mean() if len(px) > 20 else None)
    dx = [xs[i + 1] - xs[i] for i in range(len(xs) - 1)
          if xs[i] is not None and xs[i + 1] is not None and abs(xs[i + 1] - xs[i]) < 40]
    back = [d for d in dx if d < -1.0]
    return -float(np.median(back)) * SOURCE_FPS if back else 0.0


# ------------------------------------------------------------------ loading
def load_clip(clip):
    names = sorted(os.listdir(os.path.join(WORK, "raw", clip)))
    frames = []
    for name in names:
        rgb = np.asarray(Image.open(os.path.join(WORK, "raw", clip, name)).convert("RGB")).astype(np.float64) / 255
        alpha = np.asarray(Image.open(os.path.join(WORK, "mask", clip, name))).astype(np.float64) / 255
        frames.append(clean(rgb, alpha))
    return frames


def small(rgb, alpha, anchor, size=96):
    """Premultiplied, anchored thumbnail for frame comparisons (loop search)."""
    x0 = int(anchor[0]) - 260
    y1 = int(anchor[1])
    crop = (rgb * alpha[..., None])[max(0, y1 - 560):y1, max(0, x0):x0 + 520]
    im = Image.fromarray((np.clip(crop, 0, 1) * 255).astype(np.uint8)).resize((size, size), Image.BILINEAR)
    return np.asarray(im).astype(np.float64) / 255


def best_loop(thumbs, min_len, max_len, starts):
    best = None
    for s in starts:
        for length in range(min_len, max_len + 1):
            if s + length >= len(thumbs):
                break
            d = np.abs(thumbs[s] - thumbs[s + length]).mean()
            if best is None or d < best[0]:
                best = (d, s, length)
    return best


# ------------------------------------------------------------------ main
def main():
    clips = {c: load_clip(c) for c in ("idle", "run", "shot", "reload")}

    # Scale from the standing idle pose; every take of the character was generated at the same size.
    heights = {c: np.median([bbox(a)[3] - bbox(a)[1] for _, a in f]) for c, f in clips.items()}
    print("median heights", {c: round(h) for c, h in heights.items()})
    scale = TARGET_HEIGHT / heights["idle"]

    # Anchors: ground = feet line of the take (static camera); x = head centre, the run's drift fitted by a line.
    anchors, run_speed_px = {}, 0.0
    for c, frames in clips.items():
        ground = float(np.percentile([bbox(a)[3] for _, a in frames], 90))
        hx = np.array([head_x(a) for _, a in frames])
        t = np.arange(len(frames))
        if c == "run":
            k, b = np.polyfit(t, hx, 1)                                     # drift across the frame, if any
            xs = k * t + b
        else:
            xs = np.full(len(frames), float(np.median(hx)))
        anchors[c] = [(xs[i], ground) for i in range(len(frames))]

    # Frame selection.
    thumbs = {c: [small(r, a, anchors[c][i]) for i, (r, a) in enumerate(f)] for c, f in clips.items()}
    d, s, length = best_loop(thumbs["idle"], 36, 72, range(0, 12))
    idle_sel = list(range(s, s + length, 2))                                 # 24 -> 12 fps
    print("idle loop", s, length, "diff %.4f" % d)
    d, s, length = best_loop(thumbs["run"], 14, 20, range(8, 80))
    run_sel = list(range(s, s + length, 2))
    print("run loop", s, length, "diff %.4f" % d)

    run_speed_px = contact_speed(clips["run"], anchors["run"])

    # Shot: the barrel end from the calm start of the take; the flash frame has most pixels right of it.
    shot_tip = tuple(np.median([gun_tip(a, 0) for _, a in clips["shot"][:8]], axis=0))
    flash_area = [flash_mask(a, shot_tip).sum() for _, a in clips["shot"]]
    f0 = int(np.argmax(flash_area))
    shot_sel = list(range(max(0, f0 - 1), min(len(clips["shot"]), f0 + 5)))
    print("shot flash frame", f0, "area", flash_area[f0])

    # Reload: the part of the take with motion, sampled down to ~16 frames.
    motion = [np.abs(thumbs["reload"][i + 1] - thumbs["reload"][i]).mean() for i in range(len(thumbs["reload"]) - 1)]
    active = np.nonzero(np.array(motion) > np.median(motion) * 1.2)[0]
    r0, r1 = int(active.min()), int(active.max()) + 1
    step = max(1, round((r1 - r0) / 16))
    reload_sel = list(range(r0, r1 + 1, step))
    print("reload active", r0, r1, "frames", len(reload_sel))

    selection = {"Idle": ("idle", idle_sel, 12.0, True), "Run": ("run", run_sel, 12.0, True),
                 "Shot": ("shot", shot_sel, 24.0, False), "Reload": ("reload", reload_sel, len(reload_sel) / 1.4, False)}


    # Canvas: symmetric around the anchor (bottom-centre pivot) and large enough for every exported frame.
    half, top = 0.0, 0.0
    for key, (c, sel, fps, loop) in selection.items():
        for i in sel:
            x0, y0, x1, y1 = bbox(clips[c][i][1], 0.04)
            ax, gy = anchors[c][i]
            half = max(half, (ax - x0) * scale, (x1 - ax) * scale)
            top = max(top, (gy - y0) * scale)
    cw = int(np.ceil(half)) * 2 + MARGIN * 2
    ch = int(np.ceil(top)) + MARGIN                                          # the feet line is the bottom row
    pivot = (cw / 2.0, 0.0)                                                  # px from the bottom-left (bottom centre)
    print("canvas", cw, ch)

    def render(rgb, alpha, anchor, erase=None):
        if erase is not None:
            alpha = np.where(erase, 0.0, alpha)
        rgb = bleed(rgb, alpha)
        ax, gy = anchor
        h, w = alpha.shape
        img = Image.fromarray((np.dstack([rgb, alpha]) * 255).astype(np.uint8), "RGBA")
        img = img.resize((round(w * scale), round(h * scale)), Image.LANCZOS)
        canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
        canvas.paste(img, (round(cw / 2 - ax * scale), round(ch - gy * scale)))
        return canvas

    def to_canvas(px, anchor):
        """Source px -> px relative to the pivot, y up."""
        return ((px[0] - anchor[0]) * scale, (anchor[1] - px[1]) * scale)

    os.makedirs(OUT_FRAMES, exist_ok=True)
    for f in os.listdir(OUT_FRAMES):
        if f.startswith("Player_") and (f.endswith(".png") or f.endswith(".png.meta")):
            os.remove(os.path.join(OUT_FRAMES, f))                          # the old person.png player frames too

    meta = {"pixelsPerUnitHint": TARGET_HEIGHT, "canvas": [cw, ch], "pivot": pivot, "standingHeightPx": TARGET_HEIGHT,
            "sourceFps": SOURCE_FPS, "clips": {}}
    sheet_rows = []
    for key, (c, sel, fps, loop) in selection.items():
        tips, names, previews = [], [], []
        for k, i in enumerate(sel):
            rgb, a = clips[c][i]
            erase = None
            if c == "shot":
                erase = flash_mask(a, shot_tip)
                t = gun_tip(np.where(erase, 0.0, a), anchors[c][i][0])
            else:
                t = gun_tip(a, anchors[c][i][0])
            img = render(rgb, a, anchors[c][i], erase)
            name = f"Player_{key}_{k:02d}"
            img.save(os.path.join(OUT_FRAMES, name + ".png"), optimize=True)
            names.append(name)
            tips.append(to_canvas(t, anchors[c][i]))
            previews.append(img)
        entry = {"frames": names, "fps": fps, "loop": loop, "gunTips": tips}
        if key == "Run":
            # Foot strikes: the widest stride of each half of the cycle (both feet furthest apart).
            spread = []
            for im in previews:
                al = np.asarray(im)[..., 3] > 128
                rows = np.nonzero(al.any(axis=1))[0]
                cols = np.nonzero(al[rows[-1] - 18:rows[-1] + 1].any(axis=0))[0]
                spread.append(int(cols.max() - cols.min()))
            half = len(spread) // 2
            entry["stepFrames"] = [int(np.argmax(spread[:half])), half + int(np.argmax(spread[half:]))]
            entry["strideMetres"] = max(spread) / TARGET_HEIGHT * 1.75
        meta["clips"][key] = entry
        pick = np.linspace(0, len(previews) - 1, min(6, len(previews))).round().astype(int)
        sheet_rows.append([previews[p] for p in pick])

    # The flash sprite itself is drawn by tools/characters/muzzle_flash.py (compact star, not the video streak).

    # Native run speed: source px/s -> metres/s with the standing height = 1.75 m.
    meta["runSpeedMetresPerSecond"] = run_speed_px * scale / TARGET_HEIGHT * 1.75
    print("run speed in the video: %.2f m/s" % meta["runSpeedMetresPerSecond"])

    # Portrait: head and shoulders of the first idle frame.
    rgb, a = clips["idle"][0]
    x0, y0, x1, y1 = bbox(a)
    hx = head_x(a)
    side = int((y1 - y0) * 0.42)
    box = (int(hx - side * 0.55), y0 - int(side * 0.04), int(hx - side * 0.55) + side, y0 - int(side * 0.04) + side)
    pr = Image.fromarray((np.dstack([bleed(rgb, a), a]) * 255).astype(np.uint8), "RGBA").crop(box).resize((256, 256), Image.LANCZOS)
    pr.save(OUT_PORTRAIT)

    # Unity's JsonUtility reads lists of objects, not dictionaries: clips become a list, points {x, y}.
    unity_meta = dict(meta)
    unity_meta["clips"] = [dict(v, name=k, gunTips=[{"x": t[0], "y": t[1]} for t in v["gunTips"]],
                                stepFrames=v.get("stepFrames", []), strideMetres=v.get("strideMetres", 0.0))
                           for k, v in meta["clips"].items()]
    unity_meta["pivot"] = {"x": pivot[0], "y": pivot[1]}
    os.makedirs(os.path.dirname(OUT_JSON), exist_ok=True)
    json.dump(unity_meta, open(OUT_JSON, "w"), indent=1)

    # Check sheet: every row 6 frames of one clip, on dark then light.
    cell = (cw // 2, ch // 2)
    sheet = Image.new("RGB", (cell[0] * 6, cell[1] * len(sheet_rows) * 2), (0, 0, 0))
    for r, row in enumerate(sheet_rows):
        for k, im in enumerate(row):
            im = im.resize(cell, Image.LANCZOS)
            for j, bgc in enumerate(((28, 30, 38), (236, 236, 236))):
                bgi = Image.new("RGBA", cell, bgc + (255,))
                bgi.alpha_composite(im)
                sheet.paste(bgi.convert("RGB"), (k * cell[0], (r * 2 + j) * cell[1]))
    sheet.save(os.path.join(WORK, "checksheet.png"))
    print("done:", {k: len(v["frames"]) for k, v in meta["clips"].items()})


if __name__ == "__main__":
    main()
