"""Store trailer from the gameplay recordings in Promo/raw: day horde -> flash into the night -> boss -> workshop ->
case roll with a push-in on the drop -> logo. 16:9 1920x1080, 30 fps, H.264 MP4, under 28 s (Yandex Games).

python tools/trailer/make_trailer.py   ->  Promo/raw/new_trailer.mp4
"""
import os
import subprocess

import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
RAW = os.path.join(ROOT, "Promo", "raw")
OUT = os.path.join(RAW, "new_trailer.mp4")
WORK = os.path.join(ROOT, "tools", "characters", "zombie_mp4", "work", "trailer")
LOGO = os.path.join(ROOT, "unity", "Assets", "Art", "Textures", "UI", "UI_Logo.png")
MUSIC = os.path.join(ROOT, "unity", "Assets", "Audio", "Music", "Music_Boss.ogg")
FPS = 30
W, H = 1920, 1080

NIGHT_BOSS = "2026-10-07 15-40-19.mp4"
NIGHT_BOOM = "2026-10-07 15-41-00.mp4"
CASE = "2026-10-07 15-41-19.mp4"
DAY = "2026-10-07 15-41-56.mp4"
WORKSHOP = "2026-10-07 15-47-51.mp4"

# (file, start s, end s, speed, extra filter) — the cut list, in order.
SHOTS = [
    (DAY, 0.3, 3.6, 1.0, ""),              # the horde on the car, blood everywhere
    (DAY, 15.0, 18.2, 1.0, ""),            # by the workshop, the crowd bursting
    (DAY, 25.0, 27.6, 1.0, ""),            # carnage
    (DAY, 33.6, 34.9, 1.0, ""),            # the legendary card
    (NIGHT_BOOM, 4.6, 6.6, 1.0, ""),       # night: the barrels go БУМ! / БАХ! (cut before the level-up card)
    (NIGHT_BOSS, 2.0, 6.2, 1.0, ""),       # the Broodmother spits acid
    (NIGHT_BOSS, 11.0, 13.8, 1.0, ""),     # the boss fight goes on
    (WORKSHOP, 0.3, 6.2, 2.0, ""),         # the workshop (sped up), cut as it closes
    (CASE, 1.0, 5.3, 1.6, "roll"),         # the case rolls (the strip brought up close)
    (CASE, 5.3, 6.9, 1.0, "zoom"),         # the drop, pushed in
]
# Transition into each shot after the first: (kind, seconds).
TRANSITIONS = [
    ("fade", 0.25),
    ("fade", 0.25),
    ("slideleft", 0.3),
    ("fadewhite", 0.6),                     # day -> night
    ("fade", 0.25),
    ("fade", 0.25),
    ("slideleft", 0.35),
    ("fadeblack", 0.35),
    ("fade", 0.15),
]
LOGO_SECONDS = 2.8
LOGO_FADE = ("fadeblack", 0.45)


def run(cmd):
    print(" ".join(cmd[:6]), "...")
    subprocess.run(cmd, check=True)


def make_logo_card(background_frame):
    """A darkened, blurred game frame with the logo growing in: frames -> logo.mp4."""
    folder = os.path.join(WORK, "logo_frames")
    os.makedirs(folder, exist_ok=True)
    for f in os.listdir(folder):
        os.remove(os.path.join(folder, f))
    bg = Image.open(background_frame).convert("RGB").resize((W, H)).filter(ImageFilter.GaussianBlur(14))
    bg = Image.fromarray((np.asarray(bg).astype(np.float32) * 0.45).astype(np.uint8))
    logo = Image.open(LOGO).convert("RGBA")
    n = int(LOGO_SECONDS * FPS)
    for i in range(n):
        t = i / FPS
        k = min(1.0, t / 0.6)
        ease = 1 - (1 - k) ** 3
        scale = 0.82 + 0.18 * ease + 0.02 * t                     # grows in, then keeps creeping
        w = int(W * 0.62 * scale)
        h = int(logo.height * w / logo.width)
        lg = logo.resize((w, h), Image.LANCZOS)
        if ease < 1:
            a = np.asarray(lg).copy()
            a[..., 3] = (a[..., 3] * ease).astype(np.uint8)
            lg = Image.fromarray(a, "RGBA")
        frame = bg.convert("RGBA")
        frame.alpha_composite(lg, ((W - w) // 2, (H - h) // 2 - 20))
        frame.convert("RGB").save(os.path.join(folder, f"{i:04d}.png"))
    out = os.path.join(WORK, "logo.mp4")
    run(["ffmpeg", "-v", "error", "-y", "-framerate", str(FPS), "-i", os.path.join(folder, "%04d.png"),
         "-c:v", "libx264", "-pix_fmt", "yuv420p", "-crf", "16", out])
    return out


def main():
    os.makedirs(WORK, exist_ok=True)
    # Logo background: a frame of the day horde.
    bg = os.path.join(WORK, "logo_bg.png")
    run(["ffmpeg", "-v", "error", "-y", "-ss", "26.0", "-i", os.path.join(RAW, DAY), "-frames:v", "1", bg])
    logo = make_logo_card(bg)

    inputs, filters, durations = [], [], []
    for k, (name, a, b, speed, extra) in enumerate(SHOTS):
        inputs += ["-ss", f"{a:.3f}", "-t", f"{b - a:.3f}", "-i", os.path.join(RAW, name)]
        d = (b - a) / speed
        chain = f"[{k}:v]setpts=(PTS-STARTPTS)/{speed},fps={FPS},scale={W}:{H},setsar=1"
        frames = int(d * FPS)
        if extra == "roll":
            # The roll strip is small in the middle of a dark screen: brought up close, creeping in a little.
            chain += (f",zoompan=z='1.9+0.15*on/{frames}':x='iw/2-(iw/zoom/2)':y='ih*0.47-(ih/zoom/2)'"
                      f":d=1:s={W}x{H}:fps={FPS}")
        elif extra == "zoom":
            # Push in on the dropped skin (centre, a little above the middle).
            chain += (f",zoompan=z='1.5+0.6*on/{frames}':x='iw/2-(iw/zoom/2)':y='ih*0.43-(ih/zoom/2)'"
                      f":d=1:s={W}x{H}:fps={FPS}")
        chain += f",format=yuv420p,trim=duration={d:.3f}[v{k}]"
        filters.append(chain)
        durations.append(d)
    li = len(SHOTS)
    inputs += ["-i", logo]
    filters.append(f"[{li}:v]fps={FPS},scale={W}:{H},setsar=1,format=yuv420p[v{li}]")
    durations.append(LOGO_SECONDS)

    transitions = TRANSITIONS + [LOGO_FADE]
    cur, length = "v0", durations[0]
    for k, (kind, td) in enumerate(transitions, start=1):
        offset = length - td
        out = f"x{k}"
        filters.append(f"[{cur}][v{k}]xfade=transition={kind}:duration={td}:offset={offset:.3f}[{out}]")
        cur, length = out, offset + durations[k]
    total = length
    print(f"trailer length {total:.2f} s")
    assert total < 28.0, total

    # Music: the boss theme, faded in and out.
    mi = li + 1
    inputs += ["-i", MUSIC]
    filters.append(f"[{mi}:a]atrim=0:{total:.3f},afade=t=in:d=0.4,afade=t=out:st={total - 1.2:.3f}:d=1.2,volume=0.9[a]")
    graph = ";".join(filters)
    run(["ffmpeg", "-v", "error", "-y", *inputs, "-filter_complex", graph, "-map", f"[{cur}]", "-map", "[a]",
         "-c:v", "libx264", "-preset", "slow", "-crf", "19", "-pix_fmt", "yuv420p", "-r", str(FPS),
         "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart", "-t", f"{total:.3f}", OUT])
    size = os.path.getsize(OUT) / 1e6
    print(f"{OUT}: {size:.1f} MB")


if __name__ == "__main__":
    main()
