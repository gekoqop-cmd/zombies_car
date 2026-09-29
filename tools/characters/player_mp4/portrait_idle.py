"""The HUD's animated portrait: head and shoulders of the idle take, looped inside the metal frame (ram/rrr.png).

Input : work/raw/idle + work/mask/idle (masks.py), the same frames process.py uses; ram/rrr.png, the frame.
Output: unity/Assets/Art/Textures/UI/UI_PortraitIdle_NN.png   frames at 8 fps, the frame's size, one fixed crop (the
                                                              camera is static, so the breathing and the sway show)
        unity/Assets/Art/Textures/UI/UI_HpPortraitBack.png    the frame's opening (a little under the frame too),
                                                              dark blue: the frames' background and mask
        unity/Assets/Art/Textures/UI/UI_HpFrameMetal.png      the frame, drawn over the face
python portrait_idle.py
"""
import os

import numpy as np
from PIL import Image
from scipy import ndimage as nd

import process as p

OUT_DIR = os.path.join(p.UNITY, "Art", "Textures", "UI")
FRAME = os.path.normpath(os.path.join(p.HERE, "..", "..", "..", "ram", "rrr.png"))
SCALE = 0.4                  # 1332x1181 -> 533x472: about 3x its size on a 1080p HUD
FPS_STEP = 3                 # 24 fps source -> 8 fps
UNDER = 10                   # px (at full size) the background reaches under the frame: no gap at its inner edge


def opening(frame_alpha):
    """The hole in the frame: the transparent region connected to the centre."""
    clear = frame_alpha < 0.5
    labels, _ = nd.label(clear)
    h, w = clear.shape
    hole = labels == labels[h // 2, w // 2]
    return nd.binary_dilation(hole, iterations=UNDER)


def main():
    frame = Image.open(FRAME).convert("RGBA")
    size = (round(frame.width * SCALE), round(frame.height * SCALE))
    frame_small = frame.resize(size, Image.LANCZOS)
    frame_small.save(os.path.join(OUT_DIR, "UI_HpFrameMetal.png"), optimize=True)

    hole = opening(np.asarray(frame).astype(np.float32)[..., 3] / 255.0)
    ys, xs = np.nonzero(hole)
    hx0, hy0, hx1, hy1 = xs.min(), ys.min(), xs.max(), ys.max()           # the opening's box, full frame px

    # Background: the opening filled with a dark blue from top to bottom.
    h, w = hole.shape
    t = np.linspace(0.0, 1.0, h)[:, None, None]
    rgb = np.array([0.20, 0.24, 0.31]) * (1 - t) + np.array([0.08, 0.1, 0.15]) * t
    back = np.dstack([np.broadcast_to(rgb, (h, w, 3)), hole.astype(np.float32)])
    Image.fromarray((back * 255).astype(np.uint8), "RGBA").resize(size, Image.LANCZOS).save(
        os.path.join(OUT_DIR, "UI_HpPortraitBack.png"), optimize=True)

    frames = p.load_clip("idle")
    ground = float(np.percentile([p.bbox(a)[3] for _, a in frames], 90))
    head = float(np.median([p.head_x(a) for _, a in frames]))
    thumbs = [p.small(r, a, (head, ground)) for r, a in frames]
    diff, start, length = p.best_loop(thumbs, 36, 72, range(0, 12))
    sel = list(range(start, start + length, FPS_STEP))
    print("idle loop", start, length, "diff %.4f" % diff, "->", len(sel), "frames")

    # The head and shoulders fill the opening; the crop around them is widened to the whole frame's box.
    top = min(p.bbox(frames[i][1])[1] for i in sel)
    body = ground - top
    view_h = body * 0.58                                                   # source px shown in the opening's height
    k = view_h / (hy1 - hy0)                                               # source px per frame px
    view_w = (hx1 - hx0) * k
    vx0 = head - view_w * 0.46
    vy0 = top - view_h * 0.08
    box = (vx0 - hx0 * k, vy0 - hy0 * k, vx0 - hx0 * k + w * k, vy0 - hy0 * k + h * k)
    box = tuple(int(round(v)) for v in box)

    for f in os.listdir(OUT_DIR):
        if f.startswith("UI_PortraitIdle_"):
            os.remove(os.path.join(OUT_DIR, f))
    for n, i in enumerate(sel):
        rgb, a = frames[i]
        img = Image.fromarray((np.dstack([p.bleed(rgb, a), a]) * 255).astype(np.uint8), "RGBA")
        img.crop(box).resize(size, Image.LANCZOS).save(os.path.join(OUT_DIR, f"UI_PortraitIdle_{n:02d}.png"), optimize=True)
    print("wrote", len(sel), "portrait frames, the back and the frame at", size)


if __name__ == "__main__":
    main()
