"""Generates the game's synthesised sound effects (UI, waves, level-up, victory / defeat, bosses, explosions, the axe,
ammo refill, hits) into unity/Assets/Audio/SFX/<Name>.ogg. The shot / reload / step / bullet-impact sounds cut from
the reference videos are not touched.
Run: python tools/audio/make_sfx.py [name ...]   (no names: all)
"""
import os
import sys

import numpy as np

from dsp import (SR, adsr, bandpass, bell, boom, brass, crash, db, decay, drive, fade, glide, highpass, kick, lowpass,
                 mix, noise, normalize, note, pluck, reverb, saw, sine, snare, square, sweep, times, tom, trim, write_ogg)

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
OUT = os.path.join(ROOT, "unity", "Assets", "Audio", "SFX")
SOUNDS = {}


def sound(fn):
    SOUNDS[fn.__name__] = fn
    return fn


def finish(x, peak=-3.0, tail=True):
    x = trim(x) if tail else x
    return normalize(fade(x, 0.001, 0.03), peak)


# ================================================================================================ UI
@sound
def UI_Click():
    t = times(0.07)
    f = glide(1300, 650, 0.07)
    x = np.sin(2 * np.pi * np.cumsum(f / SR)) * np.exp(-t / 0.014)
    x += highpass(noise(0.07), 3000) * np.exp(-t / 0.002) * 0.4
    return finish(x, -8)


@sound
def UI_Open():
    whoosh = sweep(noise(0.3), glide(500, 3500, 0.3), "band") * np.sin(np.linspace(0, np.pi, SR * 3 // 10)) * 0.8
    x = mix(0.6, [(0, whoosh, 0.5), (0.07, pluck(note("E5"), 0.5, 0.8, 0.18), 0.5), (0.12, pluck(note("B5"), 0.5, 0.8, 0.2), 0.45)])
    return finish(reverb(x, 0.2, 0.8), -9)


@sound
def UI_Close():
    whoosh = sweep(noise(0.25), glide(3200, 450, 0.25), "band") * np.sin(np.linspace(0, np.pi, SR // 4)) * 0.8
    x = mix(0.5, [(0, whoosh, 0.5), (0.05, pluck(note("B4"), 0.4, 0.7, 0.15), 0.45), (0.1, pluck(note("E4"), 0.4, 0.7, 0.15), 0.4)])
    return finish(reverb(x, 0.2, 0.7), -10)


@sound
def UI_Buy():
    # Cash register "cha-ching": a latch click, then two bright coin rings.
    t = times(0.05)
    latch = highpass(noise(0.05), 1500) * np.exp(-t / 0.008)
    ring1 = bell(note("G6"), 0.7, 0.25) + 0.5 * bell(note("G6") * 1.5, 0.7, 0.18)
    ring2 = bell(note("C7"), 0.8, 0.3) + 0.5 * bell(note("E7"), 0.8, 0.2)
    x = mix(0.9, [(0, latch, 0.8), (0.04, ring1, 0.45), (0.13, ring2, 0.5), (0.0, kick(0.2, 300, 120, 0.01), 0.25)])
    return finish(reverb(x, 0.18, 0.9), -5)


@sound
def UI_Denied():
    x = square(150, 0.28, 0.5) + square(143, 0.28, 0.5)
    x = lowpass(x, 1100) * adsr(0.28, 0.005, 0.05, 0.8, 0.08)
    return finish(x, -10)


# ================================================================================================ progression
@sound
def LevelUp():
    notes = ["C5", "E5", "G5", "C6"]
    parts = []
    for i, n in enumerate(notes):
        tone = square(note(n), 0.5, 0.25) * decay(0.5, 0.12, 0.003)
        tone = lowpass(tone, 5000)
        parts.append((i * 0.075, tone, 0.35))
        parts.append((i * 0.075, bell(note(n) * 2, 0.9, 0.35), 0.25))
    chord = sum(pluck(note(n), 1.2, 1.0, 0.5) for n in ("C5", "E5", "G5", "C6"))
    sparkle = highpass(noise(0.9), 6000) * decay(0.9, 0.25) * (0.5 + 0.5 * np.sin(np.linspace(0, 60, SR * 9 // 10)))
    parts += [(0.32, chord, 0.3), (0.3, sparkle, 0.12)]
    x = mix(1.6, parts)
    return finish(reverb(x, 0.3, 1.3), -3)


@sound
def Card_Pick():
    rise = sweep(noise(0.25), glide(800, 6000, 0.25), "band") * np.linspace(0, 1, SR // 4) ** 2
    chord = sum(pluck(note(n), 0.9, 1.0, 0.3) for n in ("G4", "C5", "E5", "G5"))
    x = mix(1.1, [(0, rise, 0.4), (0.22, chord, 0.4), (0.22, bell(note("C6"), 0.9, 0.4), 0.3)])
    return finish(reverb(x, 0.25, 1.0), -4)


# ================================================================================================ waves
@sound
def Wave_Start():
    # War horn: two low brass blasts over a drum boom.
    h1 = brass(note("D3"), 0.55, depth=0.004, bright=1800) + 0.6 * brass(note("D4"), 0.55, depth=0.004, bright=1500)
    h2 = brass(note("A3"), 1.1, depth=0.006, bright=2200) + 0.6 * brass(note("A2"), 1.1, depth=0.004, bright=1400)
    x = mix(2.0, [(0, boom(1.2, 110, 40), 0.9), (0.02, h1, 0.35), (0.5, tom(95, 0.6), 0.6), (0.5, h2, 0.4)])
    return finish(reverb(x, 0.3, 1.5), -2)


@sound
def Wave_End():
    parts = [(i * 0.12, bell(note(n), 1.6, 0.6), 0.35) for i, n in enumerate(("G5", "B5", "D6", "G6"))]
    from dsp import pad
    parts.append((0.0, pad([note("G3"), note("D4"), note("B4")], 1.4, 2000, 0.05, 0.8), 0.5))
    x = mix(2.0, parts)
    return finish(reverb(x, 0.35, 1.6), -4)


@sound
def Victory():
    parts = []
    # Pick-ups (triplet), then the held major chord with timpani and a crash.
    for i, n in enumerate(("G4", "G4", "G4")):
        parts.append((i * 0.12, brass(note(n), 0.13, depth=0.0, bright=2600), 0.35))
    for i, (n, d) in enumerate((("C5", 0.35), ("E5", 0.35), ("G5", 0.35))):
        parts.append((0.4 + i * 0.36, brass(note(n), d + 0.05, depth=0.003, bright=2800), 0.35))
        parts.append((0.4 + i * 0.36, brass(note(n) / 2, d + 0.05, depth=0.003, bright=2000), 0.25))
    held = 1.6
    for n in ("C5", "E5", "G5", "C4"):
        parts.append((1.5, brass(note(n), held, depth=0.006, bright=3000), 0.22))
    for i in range(6):
        parts.append((1.2 + i * 0.05, tom(70, 0.5), 0.25))
    parts += [(1.5, tom(65, 1.2), 0.8), (1.5, crash(2.0), 0.35), (0.4, tom(80, 0.5), 0.4)]
    x = mix(3.6, parts)
    return finish(reverb(x, 0.3, 1.8), -2)


@sound
def Defeat():
    # Sad trombone: four falling notes, the last one wobbling, and a low thud.
    parts = []
    for i, n in enumerate(("G3", "F#3", "F3")):
        parts.append((i * 0.45, brass(note(n), 0.45, depth=0.004, bright=1400), 0.4))
    t = times(1.4)
    last = brass(note("E3"), 1.4, vibrato=6.0, depth=0.02, bright=1200)
    last *= 0.75 + 0.25 * np.sin(2 * np.pi * 6.0 * t)
    parts += [(1.35, last, 0.4), (1.35, boom(1.5, 70, 28), 0.7)]
    x = mix(3.2, parts)
    return finish(reverb(x, 0.3, 1.6), -3)


# ================================================================================================ car
@sound
def Car_Shock():
    t = times(0.55)
    gate = (np.random.RandomState(5).rand(len(t) // 400 + 1) > 0.35).repeat(400)[:len(t)].astype(float)
    crackle = bandpass(noise(0.55), 1800, 7500) * gate
    buzz = drive(saw(glide(95, 60, 0.55), 0.55) * 2, 3) * 0.5
    x = (crackle + buzz) * np.exp(-t / 0.22) * np.minimum(1, t / 0.004)
    x += highpass(noise(0.55), 4000) * np.exp(-t / 0.02) * 0.8
    return finish(x, -4)


def main(names):
    todo = names or list(SOUNDS)
    for name in todo:
        write_ogg(SOUNDS[name](), os.path.join(OUT, name + ".ogg"))


if __name__ == "__main__":
    main(sys.argv[1:])
