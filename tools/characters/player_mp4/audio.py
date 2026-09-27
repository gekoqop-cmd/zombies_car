"""Player sounds from the mp4 audio tracks (idle's track is not used: bad sound, idle is silent).

work/audio/<clip>.wav (ffmpeg -vn -ac 1 -ar 44100) ->
  unity/Assets/Audio/SFX/Player_Shot.ogg       first shot of shot.mp4
  unity/Assets/Audio/SFX/Player_Reload.ogg     magazine out / in / charge from reload.mp4
  unity/Assets/Audio/SFX/Player_Step1..3.ogg   single footsteps from run.mp4 (onset detection)
  unity/Assets/Audio/SFX/Bullet_Impact.ogg     synthesised metallic ricochet (bullet hits car / metal)
Mono, silence trimmed, short fade-out, Ogg Vorbis (ffmpeg libvorbis). Also prints the step times of run.mp4
(frames at 24 fps) so the footstep animation events can be matched: work/steps.json.
python audio.py
"""
import json
import os
import subprocess
import wave

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.join(HERE, "work")
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "..", "unity", "Assets", "Audio", "SFX"))
RATE = 44100
SILENCE = 0.02            # linear amplitude treated as silence when trimming
FADE = 0.03               # seconds of fade-out at the end of every sound


def load(name):
    with wave.open(os.path.join(WORK, "audio", name + ".wav")) as w:
        return np.frombuffer(w.readframes(w.getnframes()), dtype=np.int16).astype(np.float64) / 32768.0


def trim(a, pre=0.005):
    idx = np.nonzero(np.abs(a) > SILENCE)[0]
    if len(idx) == 0:
        return a
    start = max(0, idx[0] - int(pre * RATE))
    return a[start:idx[-1] + 1]


def fade(a):
    n = min(len(a), int(FADE * RATE))
    a = a.copy()
    a[-n:] *= np.linspace(1.0, 0.0, n)
    return a


def write_ogg(a, name, peak=0.9):
    a = a / max(1e-6, np.abs(a).max()) * peak
    tmp = os.path.join(WORK, "audio", name + "_out.wav")
    with wave.open(tmp, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes((a * 32767).astype(np.int16).tobytes())
    os.makedirs(OUT, exist_ok=True)
    dst = os.path.join(OUT, name + ".ogg")
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", tmp, "-c:a", "libvorbis", "-q:a", "3", "-ac", "1", dst], check=True)
    print("saved", name, "%.2fs" % (len(a) / RATE), os.path.getsize(dst), "bytes")


def segment(a, t0, t1):
    return a[int(t0 * RATE):int(t1 * RATE)]


def envelope(a, win):
    n = len(a) // win
    return np.sqrt((a[:n * win].reshape(n, win) ** 2).mean(axis=1))


def main():
    # Shot: the first shot of the take (0.55..1.0 s), its tail decays naturally.
    write_ogg(fade(trim(segment(load("shot"), 0.55, 1.0))), "Player_Shot")
    # Reload: magazine out, magazine in and the charging handle (1.1..2.45 s), ~ WeaponConfig.ReloadTime.
    write_ogg(fade(trim(segment(load("reload"), 1.1, 2.45))), "Player_Reload")

    # Footsteps: onsets of the 10 ms envelope that rise well above the floor.
    run = load("run")
    win = RATE // 100
    env = envelope(run, win)
    floor = np.median(env)
    onsets = []
    for i in range(1, len(env) - 1):
        if env[i] > floor * 3.5 and env[i] >= env[i - 1] and env[i] >= env[i + 1]:
            if not onsets or i - onsets[-1] > 20:               # steps are ~0.35 s apart
                onsets.append(i)
    times = [i * win / RATE for i in onsets]
    loud = sorted(onsets, key=lambda i: -env[i])[:3]
    for k, i in enumerate(sorted(loud)):
        t = i * win / RATE
        write_ogg(fade(trim(segment(run, max(0.0, t - 0.03), t + 0.22))), "Player_Step%d" % (k + 1), peak=0.8)
    with open(os.path.join(WORK, "steps.json"), "w") as f:
        json.dump({"times": times, "frames": [round(t * 24) for t in times]}, f)
    print("step times", ["%.2f" % t for t in times])

    # Ricochet: inharmonic metal partials with a fast decay plus a short noise click.
    t = np.arange(int(0.35 * RATE)) / RATE
    rng = np.random.default_rng(3)
    ping = sum(amp * np.sin(2 * np.pi * f * t) * np.exp(-t * d)
               for f, amp, d in ((2310, 1.0, 14), (3490, 0.6, 18), (5120, 0.35, 25), (1580, 0.5, 11)))
    click = rng.normal(0, 1, len(t)) * np.exp(-t * 90) * 0.8
    write_ogg(fade(ping + click), "Bullet_Impact", peak=0.7)


if __name__ == "__main__":
    main()
