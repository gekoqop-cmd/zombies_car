"""Character frames from green-screen videos (takes.py) + the gib pieces of every regular zombie.

python process.py [Name ...]      the named characters (all without names), then the gibs of every zombie type
python process.py gibs            only the gibs (from the current idle frame of every zombie type)

Per take: ffmpeg cuts it into work/raw/<take>/NNNN.png (sheets.py), the green background is keyed out (green excess
G - max(R, B): the olive skins and lime pustules stay far below the screen green), the green spill is taken out of the
soft edge. Every take starts in the character's idle pose: its first frames give the take's scale (the standing
figure becomes TARGET px tall), its anchor (head centre, x) and the feet line (y). Each clip goes onto its own canvas
with the feet on the bottom row and the anchor on the vertical centre line (bottom-centre pivot: no jitter between
frames or clips).

Output: unity/Assets/Art/Textures/Characters/<Name>/<Name>_<Clip>_NN.png
        unity/Assets/Art/Textures/Characters/<Name>/<Name>_Gib_N.png   (centre pivot, see gibs())
        unity/Assets/Data/Characters/<Name>_Frames.json                clips (fps, loop, strike moment), pixel scale
        work/<name>_check.png                                          the clips on dark and light
"""
import json
import os
import sys

import numpy as np
from PIL import Image
from scipy import ndimage as nd

HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.join(HERE, "work")
ROOT = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
UNITY = os.path.join(ROOT, "unity", "Assets")
CHAR_TEX = os.path.join(UNITY, "Art", "Textures", "Characters")
DATA = os.path.join(UNITY, "Data", "Characters")

MARGIN = 4
NEAR = 12                    # px: loose pieces this close to the body are kept
CALM = 6                     # first frames of every take: the idle pose (scale, anchor, feet line)


def loop(take, lo, hi, min_len, max_len, step, fps, bad=()):
    """The best seamless loop inside [lo, hi] of the take, every <step>-th frame; <bad>: smeared source frames that
    are left out of it."""
    return {"kind": "loop", "take": take, "lo": lo, "hi": hi, "min": min_len, "max": max_len, "step": step, "fps": fps,
            "bad": set(bad)}


def cut(take, first, last, step, fps, strike=None, looped=False, skip=None, no_globs=False):
    """Frames first..last of the take; <strike>: the source frame of the blow (the attack lands there); <skip>: a
    (from, to) range of source frames left out (a botched stretch of the take); <no_globs>: the lime acid globs of the
    take are cut out (the game throws its own; the painted ones would pile up at the feet)."""
    return {"kind": "cut", "take": take, "first": first, "last": last, "step": step, "fps": fps, "strike": strike,
            "loop": looped, "skip": skip, "no_globs": no_globs}


def back(take, first, last, step, fps):
    """Frames last..first: the take played backwards (rising out of the ground from a sinking take)."""
    return {"kind": "back", "take": take, "first": first, "last": last, "step": step, "fps": fps}


# Per character: the standing height in the new frames (TARGET), the height of the old idle frame in sheet pixels
# (the new frames are drawn at the old size in the game: pixelScale = TARGET / OLD), and the clips.
CHARACTERS = {
    "Walker": {"target": 176, "old": 127, "clips": {
        "Idle": loop("walker_attack", 0, 31, 18, 31, 3, 8.0),            # the calm sway before the wind-up
        "Move": loop("walker_run", 0, 120, 30, 48, 2, 12.0),
        "Attack": cut("walker_attack", 33, 71, 2, 20.0, strike=55),
    }},
    "Worker": {"target": 176, "old": 134, "clips": {
        "Idle": loop("worker_attack", 0, 15, 8, 15, 2, 8.0),
        "Move": loop("worker_run", 2, 96, 20, 48, 2, 12.0),
        "Attack": cut("worker_attack", 16, 68, 3, 17.0, strike=39),      # overhead smash
    }},
    "RedCap": {"target": 176, "old": 134, "clips": {
        "Idle": loop("redcap_attack", 58, 72, 8, 14, 2, 8.0),
        "Move": loop("redcap_run", 2, 96, 14, 40, 2, 14.0),
        "Attack": cut("redcap_attack", 10, 58, 3, 18.0, strike=21),      # lunge with the claws out
    }},
    "Hoodie": {"target": 170, "old": 126, "clips": {
        "Idle": loop("hoodie_attack", 56, 72, 8, 16, 2, 8.0),
        "Move": loop("hoodie_run", 2, 72, 16, 40, 2, 12.0),
        "Attack": cut("hoodie_attack", 9, 56, 3, 17.0, strike=30),       # claw slash
    }},
    "Brute": {"target": 210, "old": 166, "clips": {
        "Idle": loop("brute_attack", 0, 10, 6, 10, 2, 6.0),
        "Move": loop("brute_run", 16, 46, 14, 30, 2, 10.0),
        "Attack": cut("brute_attack", 10, 64, 3, 14.0, strike=31),       # the big punch
    }},
    "Hound": {"target": 130, "old": 100, "clips": {
        "Idle": loop("hound_attack", 0, 29, 10, 29, 2, 10.0),
        "Move": loop("hound_run", 10, 72, 12, 26, 2, 14.0),
        "Attack": cut("hound_attack", 30, 60, 2, 20.0, strike=46),       # rears up and bites
    }},
    # Bosses: drawn 240 px tall (the old sheets showed the idle pose 330 px tall at PixelScale 2: 165 sheet px), every
    # third source frame at most: their frames are big, the download and the video memory count.
    "Boss1": {"target": 240, "old": 165, "clips": {
        "Idle": loop("boss1_idle", 0, 72, 30, 72, 4, 6.0),
        "Move": loop("boss1_run", 10, 72, 20, 50, 3, 8.0),
        "Attack": cut("boss1_attack", 10, 68, 3, 16.0, strike=39),       # also her acid spit (points and spits)
        "Summon": cut("boss1_summon", 14, 68, 3, 14.0, strike=40),
        "Dead": cut("boss1_dead", 6, 62, 4, 9.0),
    }},
    "Boss2": {"target": 240, "old": 165, "clips": {
        "Idle": loop("boss2_idle", 0, 72, 24, 72, 4, 6.0),
        "Move": loop("boss2_run", 20, 72, 16, 40, 3, 8.0),
        "Attack": cut("boss2_attack", 18, 62, 3, 16.0, strike=32),       # cannon thrust (also the ground slam)
        "ChargeWindup": cut("boss2_windup", 0, 44, 3, 11.0, strike=44),
        "Charge": loop("boss2_charge", 3, 72, 10, 30, 2, 16.0,
                       bad=(13, 14, 17, 18, 21, 22, 25, 26, 33, 34, 49, 50, 57, 58)),   # smeared frames of the take
        "Stun": loop("boss2_stun", 40, 96, 16, 56, 4, 6.0),
        "Spit": cut("boss2_rain", 0, 72, 3, 14.0, strike=31, no_globs=True),
        "Dead": cut("boss2_dead", 4, 44, 3, 10.0, skip=(10, 21)),        # 10-21: the cannon comes off wrong
    }},
    # The player with the axe in hand (Player/PlayerAxe_*): drawn at the player's frame size (Player_Frames.json:
    # standingHeightPx 192), the blade masked out for the axe skins (PlayerAxeBlade_*). The painted blood bursts of
    # the takes are left out (skip): the game has its own hit effects.
    "PlayerAxe": {"target": 192, "old": 192, "folder": "Player", "blade": True, "clips": {
        "Idle": loop("axe_idle", 0, 72, 24, 72, 3, 8.0),
        "SwingA": cut("axe_attack2", 24, 60, 2, 26.0, strike=38, skip=(39, 48)),   # sideways sweep
        "SwingB": cut("axe_attack", 34, 66, 2, 26.0, strike=46, skip=(47, 57)),    # overhead chop
        "Run": loop("axe_run", 10, 96, 14, 30, 2, 12.0),
        "Raise": cut("axe_stun", 22, 42, 2, 16.0, strike=42),                      # charging: the axe goes up...
        "Hold": loop("axe_stun", 44, 120, 30, 76, 3, 8.0),                         # ...and stays up (breathes, blinks)
        "Slam": cut("axe_attack", 44, 68, 2, 22.0, strike=46, skip=(47, 57)),      # the charged blow
    }},
    "Boss3": {"target": 240, "old": 165, "clips": {
        "Idle": loop("boss3_idle", 0, 72, 30, 72, 4, 6.0),
        "Move": loop("boss3_run", 12, 72, 16, 40, 3, 8.0),
        "Attack": cut("boss3_attack", 15, 62, 3, 14.0, strike=36),       # scythe sweep
        "Cracks": cut("boss3_cracks", 17, 90, 3, 14.0, strike=45),       # scythe into the ground
        "Vanish": cut("boss3_sink", 40, 80, 3, 10.0, strike=80),
        "Appear": back("boss3_sink", 40, 80, 3, 14.0),
        "Dead": cut("boss3_dead", 14, 72, 4, 9.0),
    }},
}

# Zombie types whose death is a burst of chunks cut from their idle frame.
GIB_TYPES = ["Walker", "Worker", "RedCap", "Hoodie", "Brute", "Hound"]
GIB_PIECES = {"Hound": 6, "Brute": 9}


# ------------------------------------------------------------------ keying
def key(rgb):
    """rgb 0..255 float. Returns rgb 0..1 (spill removed) and alpha 0..1."""
    excess = rgb[..., 1] - np.maximum(rgb[..., 0], rgb[..., 2])
    border = np.concatenate([excess[:8].ravel(), excess[-8:].ravel(), excess[:, :8].ravel(), excess[:, -8:].ravel()])
    screen = float(np.median(border))                                    # ~230
    lo, hi = screen * 0.32, screen * 0.62                                # opaque below lo, clear above hi
    alpha = np.clip((hi - excess) / (hi - lo), 0.0, 1.0)

    solid = alpha > 0.3
    lab, n = nd.label(solid)
    if n > 1:
        sizes = nd.sum(solid, lab, range(1, n + 1))
        main = lab == 1 + int(np.argmax(sizes))
        near = nd.binary_dilation(main, iterations=NEAR)
        ids = np.unique(lab[near & solid])
        keep = np.isin(lab, ids[ids > 0])
        alpha = alpha * nd.binary_dilation(keep, iterations=2)

    bgmask = alpha < 0.02
    bg = np.median(rgb[bgmask], axis=0) if bgmask.any() else np.array([20.0, 250.0, 15.0])
    a = np.clip(alpha, 1e-3, 1.0)[..., None]
    out = np.clip((rgb - (1.0 - a) * bg) / a, 0.0, 255.0)
    edge = alpha < 0.98
    cap = np.maximum(out[..., 0], out[..., 2]) + 40.0
    out[..., 1] = np.where(edge, np.minimum(out[..., 1], cap), out[..., 1])
    alpha = nd.grey_erosion(alpha, size=(2, 2))
    alpha[alpha < 0.05] = 0.0
    return out / 255.0, alpha


def drop_globs(rgb, alpha):
    """Removes lime acid (green well above red and far above blue: the boss's pale skin has blue in it)."""
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    lime = (g > r + 0.06) & (g - b > 0.3) & (alpha > 0.05)
    lime = nd.binary_dilation(lime, iterations=2)
    alpha = alpha * ~lime
    # Leftover crumbs of the globs: only the body and what touches it stays.
    solid = alpha > 0.3
    lab, n = nd.label(solid)
    if n > 1:
        sizes = nd.sum(solid, lab, range(1, n + 1))
        keep = np.isin(lab, 1 + np.nonzero(sizes >= sizes.max() * 0.02)[0])
        alpha = alpha * nd.binary_dilation(keep, iterations=2)
    return alpha


def bleed(rgb, alpha):
    solid = alpha > 0.5
    if not solid.any():
        return rgb
    _, (iy, ix) = nd.distance_transform_edt(~solid, return_indices=True)
    return rgb[iy, ix]


def bbox(alpha, thr=0.5):
    ys, xs = np.nonzero(alpha > thr)
    if len(xs) == 0:
        return None
    return xs.min(), ys.min(), xs.max() + 1, ys.max() + 1


def head_x(alpha):
    x0, y0, x1, y1 = bbox(alpha)
    rows = alpha[y0:y0 + int((y1 - y0) * 0.22)] > 0.5
    return float(np.nonzero(rows)[1].mean())


def thumb(rgb, alpha, anchor, half, size=80):
    ax, gy = anchor
    crop = (rgb * alpha[..., None])[max(0, int(gy - half * 2)):int(gy), max(0, int(ax - half)):int(ax + half)]
    im = Image.fromarray((np.clip(crop, 0, 1) * 255).astype(np.uint8)).resize((size, size), Image.BILINEAR)
    return np.asarray(im).astype(np.float64) / 255


def best_loop(thumbs, lo, hi, min_len, max_len):
    best = None
    for s in range(lo, hi + 1):
        for length in range(min_len, max_len + 1):
            if s + length > hi or s + length >= len(thumbs):
                break
            d = np.abs(thumbs[s] - thumbs[s + length]).mean()
            if best is None or d < best[0]:
                best = (d, s, length)
    return best


class Take:
    """One keyed video: frames, scale to TARGET, anchor per frame."""

    def __init__(self, key_name, target):
        folder = os.path.join(WORK, "raw", key_name)
        if not os.path.isdir(folder):
            raise SystemExit(f"{key_name}: run sheets.py first")
        self.name = key_name
        self.frames = [key(np.asarray(Image.open(os.path.join(folder, n)).convert("RGB")).astype(np.float64))
                       for n in sorted(os.listdir(folder))]
        calm = [bbox(a) for _, a in self.frames[:CALM]]
        self.scale = target / float(np.median([b[3] - b[1] for b in calm]))
        self.ground = float(np.median([b[3] for b in calm]))
        hx = np.array([head_x(a) if bbox(a) is not None else np.nan for _, a in self.frames])
        if key_name.endswith("_run") or key_name.endswith("_charge"):
            # Running on the spot may drift across the frame: follow it with a line.
            t = np.arange(len(hx))
            ok = ~np.isnan(hx)
            k, b = np.polyfit(t[ok], hx[ok], 1)
            self.xs = k * t + b
        else:
            self.xs = np.full(len(hx), float(np.nanmedian(hx[:CALM])))

    def anchor(self, i):
        return self.xs[i], self.ground


# ------------------------------------------------------------------ one character
def process(name, spec):
    takes = {}

    def take(k):
        if k not in takes:
            takes[k] = Take(k, spec["target"])
        return takes[k]

    selection = {}
    for clip, c in spec["clips"].items():
        t = take(c["take"])
        strike = None
        if c["kind"] == "loop":
            half = spec["target"] / t.scale * 0.7
            th = [thumb(r, a, t.anchor(i), half) for i, (r, a) in enumerate(t.frames)]
            d, s, length = best_loop(th, c["lo"], c["hi"], c["min"], c["max"])
            sel = [i for i in range(s, s + length, c["step"]) if i not in c["bad"]]
            print(f"{name}.{clip}: loop {s}..{s + length} diff {d:.4f} -> {len(sel)} frames")
            looped = True
        elif c["kind"] == "cut":
            sel = list(range(c["first"], c["last"] + 1, c["step"]))
            if c["skip"]:
                sel = [i for i in sel if not c["skip"][0] <= i <= c["skip"][1]]
            looped = c["loop"]
            if c["strike"] is not None:
                k = int(np.argmin([abs(i - c["strike"]) for i in sel]))
                strike = round((k + 0.5) / len(sel), 3)
        else:
            sel = list(range(c["last"], c["first"] - 1, -c["step"]))
            looped = False
        selection[clip] = (t, sel, c["fps"], looped, strike, c.get("no_globs", False))

    folder = os.path.join(CHAR_TEX, spec.get("folder", name))
    os.makedirs(folder, exist_ok=True)
    blade_prefix = name + "Blade_"
    for f in os.listdir(folder):
        if (f.startswith(name + "_") and not f.startswith(name + "_Gib_")) or f.startswith(blade_prefix):
            os.remove(os.path.join(folder, f))                         # old sheet frames and old takes

    meta = {"name": name, "standingHeightPx": spec["target"], "pixelScale": spec["target"] / spec["old"],
            "strikeMoment": 0.5, "clips": []}
    rows = []
    for clip, (t, sel, fps, looped, strike, no_globs) in selection.items():
        frames = {i: (t.frames[i][0], drop_globs(*t.frames[i]) if no_globs else t.frames[i][1]) for i in sel}
        # Canvas of this clip: symmetric around the anchor, the feet on the bottom row.
        half, top = 0.0, 0.0
        for i in sel:
            b = bbox(frames[i][1], 0.05)
            if b is None:
                continue
            ax, gy = t.anchor(i)
            half = max(half, (ax - b[0]) * t.scale, (b[2] - ax) * t.scale)
            top = max(top, (gy - b[1]) * t.scale)
        cw = int(np.ceil(half)) * 2 + MARGIN * 2
        ch = int(np.ceil(top)) + MARGIN
        cw += (-cw) % 4                                                  # block-compressed formats: multiples of 4
        ch += (-ch) % 4
        names, previews = [], []
        for k, i in enumerate(sel):
            rgb, alpha = frames[i]
            ax, gy = t.anchor(i)
            h, w = alpha.shape
            img = Image.fromarray((np.dstack([bleed(rgb, alpha), alpha]) * 255).astype(np.uint8), "RGBA")
            img = img.resize((round(w * t.scale), round(h * t.scale)), Image.LANCZOS)
            canvas = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
            canvas.paste(img, (round(cw / 2 - ax * t.scale), round(ch - gy * t.scale)))
            fname = f"{name}_{clip}_{k:02d}"
            canvas.save(os.path.join(folder, fname + ".png"), optimize=True)
            if spec.get("blade"):
                blade(canvas).save(os.path.join(folder, f"{blade_prefix}{clip}_{k:02d}.png"), optimize=True)
            names.append(fname)
            previews.append(canvas)
        entry = {"name": clip, "frames": names, "fps": fps, "loop": looped, "strike": strike if strike is not None else -1}
        meta["clips"].append(entry)
        if clip == "Attack" and strike is not None:
            meta["strikeMoment"] = strike
        print(f"{name}.{clip}: {len(names)} frames {cw}x{ch} @{fps} fps ({len(names) / fps:.2f} s)"
              + (f", strike {strike}" if strike is not None else ""))
        pick = np.linspace(0, len(previews) - 1, min(8, len(previews))).round().astype(int)
        rows.append([previews[p] for p in pick])

    os.makedirs(DATA, exist_ok=True)
    json.dump(meta, open(os.path.join(DATA, f"{name}_Frames.json"), "w"), indent=1)
    check_sheet(rows, os.path.join(WORK, f"{name.lower()}_check.png"))


def blade(frame):
    """
    The red of the axe blade in lifted greys (an overlay tinted by the axe skin). The blade is as red as the jacket, so
    it is found by place: of the red areas apart from the jacket (the largest one), the blade-sized one furthest from
    the jacket's centre, plus red bits right next to it (a highlight splits it); the metal edge and the ink stay.
    """
    a = np.asarray(frame).astype(np.float64) / 255
    rgb, alpha = a[..., :3], a[..., 3]
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
    red = (r > 0.35) & (r - np.maximum(g, b) > 0.22) & (alpha > 0.5)
    lab, n = nd.label(red)
    out = np.zeros_like(alpha, dtype=bool)
    if n > 1:
        sizes = nd.sum(red, lab, range(1, n + 1))
        jacket = 1 + int(np.argmax(sizes))
        centres = nd.center_of_mass(red, lab, range(1, n + 1))
        jc = np.array(centres[jacket - 1])
        h2 = frame.height * frame.height
        best, far = None, -1.0
        for i in range(1, n + 1):
            if i == jacket or not 0.003 * h2 <= sizes[i - 1] <= 0.04 * h2:
                continue
            d = float(np.linalg.norm(np.array(centres[i - 1]) - jc))
            if d > far:
                best, far = i, d
        if best is not None:
            out = lab == best
            near = nd.binary_dilation(out, iterations=4)
            for i in np.unique(lab[near & red]):
                if i and i != jacket:
                    out |= lab == i
    out = nd.binary_dilation(out, iterations=1) & (alpha > 0.5) & (r > 0.25)
    grey = np.clip(rgb.max(axis=2) * 1.15, 0, 1)                         # lifted: the tint shows bright
    img = np.dstack([grey, grey, grey, np.where(out, alpha, 0.0)])
    return Image.fromarray((img * 255).astype(np.uint8), "RGBA")


def check_sheet(rows, path):
    cell = 150
    cols = max(len(r) for r in rows)
    sheet = Image.new("RGB", (cell * cols, cell * len(rows) * 2), (0, 0, 0))
    for r, row in enumerate(rows):
        for k, im in enumerate(row):
            s = cell / max(im.size)
            small = im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))), Image.LANCZOS)
            for j, bgc in enumerate(((28, 30, 38), (236, 236, 236))):
                c = Image.new("RGBA", (cell, cell), bgc + (255,))
                c.alpha_composite(small, ((cell - small.width) // 2, cell - small.height))
                sheet.paste(c.convert("RGB"), (k * cell, (r * 2 + j) * cell))
    sheet.save(path)


# ------------------------------------------------------------------ gibs
def gibs(name):
    """
    Cuts the idle frame into chunks that fly apart when the zombie dies: k-means over the body pixels (seeded by
    height bands: head, chest, arms, hips, legs), jagged borders from noise, a dark red raw rim along every cut.
    Each piece is saved cropped (centre pivot); its centre relative to the feet (px, y up) goes into <Name>_Gibs.json.
    """
    folder = os.path.join(CHAR_TEX, name)
    idle = os.path.join(folder, f"{name}_Idle_00.png")
    if not os.path.exists(idle):
        idle = os.path.join(folder, f"{name}_0.png")
    img = np.asarray(Image.open(idle).convert("RGBA")).astype(np.float64) / 255
    rgb, alpha = img[..., :3], img[..., 3]
    h, w = alpha.shape
    body = alpha > 0.1
    ys, xs = np.nonzero(body)
    k = GIB_PIECES.get(name, 8)

    rnd = np.random.RandomState(sum(map(ord, name)))
    pts = np.stack([xs, ys], 1).astype(np.float64)
    order = np.argsort(pts[:, 1])
    seeds = np.array([pts[order[int((i + 0.5) / k * len(order))]] for i in range(k)])
    seeds += rnd.uniform(-6, 6, seeds.shape)
    for _ in range(12):
        d = ((pts[:, None, :] - seeds[None]) ** 2).sum(-1)
        lab = d.argmin(1)
        for j in range(k):
            if (lab == j).any():
                seeds[j] = pts[lab == j].mean(0)
    noise = np.stack([nd.gaussian_filter(rnd.normal(0, 1, (h, w)), 2.5) for _ in range(k)], -1)
    noise *= 7.0 / noise.std()                                           # ~7 px wobble of every cut
    d = ((pts[:, None, :] - seeds[None]) ** 2).sum(-1) ** 0.5 + noise[ys, xs]
    lab = d.argmin(1)
    labels = np.full((h, w), -1)
    labels[ys, xs] = lab

    pieces = []
    for j in range(k):
        m = labels == j
        if m.sum() < 30:
            continue
        cl, cn = nd.label(m)
        if cn > 1:
            sizes = nd.sum(m, cl, range(1, cn + 1))
            m = cl == 1 + int(np.argmax(sizes))
        others = body & ~m
        cut_band = m & nd.binary_dilation(others, iterations=3)
        rim_in = m & nd.binary_dilation(others, iterations=1)
        prgb = rgb.copy()
        meat = np.array([0.62, 0.06, 0.07])
        dark = np.array([0.22, 0.02, 0.03])
        prgb[cut_band] = prgb[cut_band] * 0.35 + meat * 0.65
        prgb[rim_in] = dark
        palpha = np.where(m, alpha, 0.0)
        palpha[cut_band] = np.maximum(palpha[cut_band], 0.95)
        y0, y1 = np.nonzero(m.any(1))[0][[0, -1]]
        x0, x1 = np.nonzero(m.any(0))[0][[0, -1]]
        y0, x0 = max(0, y0 - 2), max(0, x0 - 2)
        y1, x1 = min(h, y1 + 3), min(w, x1 + 3)
        crop_rgb = bleed(prgb, palpha)[y0:y1, x0:x1]
        crop = Image.fromarray((np.dstack([crop_rgb, palpha[y0:y1, x0:x1]]) * 255).astype(np.uint8), "RGBA")
        cx, cy = (x0 + x1) / 2.0, (y0 + y1) / 2.0
        pieces.append((crop, (cx - w / 2.0, h - cy), int(m.sum())))

    for f in os.listdir(folder):
        if f.startswith(name + "_Gib_"):
            os.remove(os.path.join(folder, f))
    entries = []
    for i, (crop, offset, area) in enumerate(pieces):
        fname = f"{name}_Gib_{i}"
        crop.save(os.path.join(folder, fname + ".png"), optimize=True)
        entries.append({"sprite": fname, "x": round(offset[0], 2), "y": round(offset[1], 2), "area": area})
    json.dump({"name": name, "pieces": entries}, open(os.path.join(DATA, f"{name}_Gibs.json"), "w"), indent=1)

    preview = Image.new("RGBA", (w * 2, h * 2), (28, 30, 38, 255))
    for crop, (ox, oy), _ in pieces:
        px = w + ox * 1.35 - crop.width / 2
        py = h * 1.6 - oy * 1.35 - crop.height / 2
        preview.alpha_composite(crop, (int(px), int(py)))
    preview.save(os.path.join(WORK, f"gibs_{name.lower()}.png"))
    print(f"{name}: {len(entries)} gib pieces")


def main(args):
    os.makedirs(WORK, exist_ok=True)
    if "gibs" not in args:
        for name in args or CHARACTERS:
            process(name, CHARACTERS[name])
    for name in GIB_TYPES:
        if not args or "gibs" in args or name in args:
            gibs(name)


if __name__ == "__main__":
    main(sys.argv[1:])
