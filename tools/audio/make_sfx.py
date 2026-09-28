"""Generates the game's synthesised sound effects (UI, waves, level-up, victory / defeat, bosses, explosions, the axe,
ammo refill, hits) into unity/Assets/Audio/SFX/<Name>.ogg. The shot / reload / step / bullet-impact sounds cut from
the reference videos are not touched.
Run: python tools/audio/make_sfx.py [name ...]   (no names: all)
"""
import os
import sys

import numpy as np

from dsp import (SR, adsr, n_of, bandpass, bell, boom, brass, crash, db, decay, drive, fade, glide, highpass, kick, lowpass,
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


# ================================================================================================ bosses
def growl(seconds, f0, f1, formants=(420.0, 950.0, 2300.0), rough=0.45, flutter=28.0, seed=1):
    """Monster voice: a low sawtooth pair with a wandering pitch, fluttering amplitude and formant filters."""
    rnd = np.random.RandomState(seed)
    n = len(times(seconds))
    wander = lowpass(rnd.randn(n), 18.0) * 60.0
    f = glide(f0, f1, seconds) * (1.0 + wander * 0.02)
    src = saw(f, seconds) + 0.7 * saw(f * 1.013, seconds, 0.4) + 0.35 * saw(f * 0.5, seconds, 0.2)
    src += bandpass(noise(seconds, rnd), 300, 3500) * rough
    t = times(seconds)
    src *= 1.0 + 0.45 * np.sin(2 * np.pi * flutter * t + rnd.rand() * 6) * (0.6 + 0.4 * np.sin(2 * np.pi * 3.1 * t))
    voice = sum(bandpass(src, fc * 0.8, fc * 1.25) * g for fc, g in zip(formants, (1.0, 0.7, 0.35)))
    return drive(voice * 3.0, 2.5)


@sound
def Boss_Roar():
    v = growl(1.9, 115, 72, seed=3) * adsr(1.9, 0.12, 0.4, 0.85, 0.7)
    x = mix(2.4, [(0, v, 0.8), (0, boom(1.4, 70, 30), 0.6), (0.05, lowpass(noise(1.6), 400) * decay(1.6, 0.6), 0.4)])
    return finish(reverb(x, 0.3, 1.6, 3500), -1.5)


@sound
def Boss_Attack():
    grunt = growl(0.35, 130, 90, seed=5, rough=0.6) * adsr(0.35, 0.02, 0.1, 0.8, 0.15)
    crunch = bandpass(noise(0.3), 600, 4500) * decay(0.3, 0.05)
    x = mix(0.8, [(0, kick(0.5, 110, 38, 0.04), 1.0), (0, crunch, 0.6), (0.02, grunt, 0.55)])
    return finish(reverb(x, 0.15, 0.8), -2)


@sound
def Boss_Death():
    t = times(2.6)
    v = growl(2.6, 105, 32, seed=7, rough=0.7) * adsr(2.6, 0.08, 0.5, 0.9, 1.4) * (1.0 - 0.5 * t / 2.6)
    x = mix(3.4, [(0, v, 0.8), (1.25, boom(1.8, 80, 26), 0.9), (1.25, bandpass(noise(0.6), 200, 2000) * decay(0.6, 0.15), 0.5)])
    return finish(reverb(x, 0.35, 2.0, 3000), -1.5)


@sound
def Boss_Summon():
    t = times(1.5)
    rise = sweep(saw(glide(55, 110, 1.5), 1.5) + saw(glide(55.7, 111, 1.5), 1.5, 0.5), glide(200, 2200, 1.5), "low")
    rise *= np.linspace(0, 1, len(t)) ** 1.5
    rnd = np.random.RandomState(9)
    bubbles = np.zeros(len(t))
    for _ in range(18):
        start = rnd.uniform(0.1, 1.3)
        f = rnd.uniform(300, 900)
        b = sine(glide(f, f * 1.8, 0.06), 0.06) * decay(0.06, 0.02)
        i = int(start * SR)
        bubbles[i:i + len(b)] += b * rnd.uniform(0.3, 0.7)
    x = mix(2.0, [(0, rise, 0.5), (0, bubbles, 0.35), (0.9, boom(1.0, 60, 30), 0.5)])
    return finish(reverb(x, 0.35, 1.4), -3)


@sound
def Boss_Spit():
    t = times(0.4)
    hawk = sweep(noise(0.4), glide(3200, 500, 0.4), "band") * decay(0.4, 0.12, 0.01)
    gurgle = lowpass(noise(0.4), 900) * (0.5 + 0.5 * np.sin(2 * np.pi * 38 * t)) * decay(0.4, 0.1)
    return finish(hawk * 0.8 + gurgle * 0.6, -4)


@sound
def Spit_Splat():
    t = times(0.5)
    splash = lowpass(noise(0.5), 1800) * decay(0.5, 0.07, 0.002)
    fizz = highpass(noise(0.5), 3500) * decay(0.5, 0.18) * 0.35
    rnd = np.random.RandomState(4)
    drips = np.zeros(len(t))
    for _ in range(5):
        i = int(rnd.uniform(0.08, 0.4) * SR)
        f = rnd.uniform(900, 1800)
        d = sine(glide(f, f * 0.6, 0.04), 0.04) * decay(0.04, 0.012)
        drips[i:i + len(d)] += d * 0.4
    return finish(splash + fizz + drips, -4)


@sound
def Boss_Charge():
    v = growl(1.0, 70, 150, seed=12, rough=0.5, flutter=34) * adsr(1.0, 0.05, 0.2, 0.9, 0.2)
    engine = sweep(saw(glide(45, 95, 1.0), 1.0) * 1.5, glide(300, 1600, 1.0), "low") * np.linspace(0.3, 1, SR)
    x = mix(1.3, [(0, v, 0.6), (0, drive(engine, 2.0), 0.4)])
    return finish(reverb(x, 0.2, 1.0), -2)


@sound
def Boss_Slam():
    crack = highpass(noise(0.2), 2500) * decay(0.2, 0.02)
    debris = bandpass(noise(1.0), 200, 1500) * decay(1.0, 0.3)
    x = mix(1.6, [(0, boom(1.4, 100, 28), 1.0), (0, crack, 0.7), (0.02, debris, 0.5), (0, kick(0.4, 180, 40, 0.02), 0.6)])
    return finish(reverb(x, 0.25, 1.4), -1)


# ================================================================================================ explosions
def explosion(seed, low=85.0):
    rnd = np.random.RandomState(seed)
    t = times(2.2)
    thump = kick(0.6, 140, low * 0.5, 0.03)
    body = lowpass(noise(2.2, rnd), 900) * np.exp(-t / 0.45) * np.minimum(1, t / 0.004)
    roar = bandpass(noise(2.2, rnd), 200, 3000) * np.exp(-t / 0.18)
    crackle = np.zeros(len(t))
    for _ in range(28):
        i = int(rnd.uniform(0.05, 1.2) * SR)
        c = highpass(noise(0.02, rnd), 2500) * decay(0.02, 0.004)
        crackle[i:i + len(c)] += c * rnd.uniform(0.2, 0.6) * np.exp(-i / SR / 0.5)
    x = mix(2.6, [(0, boom(1.8, low, 26), 1.0), (0, thump, 0.8), (0, body, 0.7), (0, roar, 0.5), (0, crackle, 0.5)])
    return finish(reverb(drive(x * 1.4, 1.6), 0.25, 1.4, 3500), -0.8)


@sound
def Explosion1():
    return explosion(31)


@sound
def Explosion2():
    return explosion(47, 75.0)


# ================================================================================================ axe
def swing(seed, f0, f1, seconds=0.32):
    rnd = np.random.RandomState(seed)
    t = times(seconds)
    env = np.sin(np.clip(t / seconds, 0, 1) * np.pi) ** 1.6
    x = sweep(noise(seconds, rnd), glide(f0, f1, seconds), "band") * env
    x += sweep(noise(seconds, rnd), glide(f0 * 0.5, f1 * 0.5, seconds), "band") * env * 0.5
    return finish(x, -5)


@sound
def Axe_Swing1():
    return swing(61, 700, 2600)


@sound
def Axe_Swing2():
    return swing(62, 600, 2200, 0.36)


def chop(seed, pitch):
    rnd = np.random.RandomState(seed)
    t = times(0.35)
    thud = kick(0.3, 190 * pitch, 70 * pitch, 0.015)
    crunch = bandpass(noise(0.35, rnd), 900, 5000) * np.exp(-t / 0.05)
    squelch = lowpass(noise(0.35, rnd), 1200) * (0.5 + 0.5 * np.sin(2 * np.pi * 45 * t)) * np.exp(-t / 0.09)
    blade = (np.sin(2 * np.pi * 2300 * pitch * t) + 0.5 * np.sin(2 * np.pi * 3470 * pitch * t)) * np.exp(-t / 0.05) * 0.25
    x = mix(0.5, [(0, thud, 0.9), (0, crunch, 0.6), (0.005, squelch, 0.5), (0, blade, 1.0)])
    return finish(drive(x * 1.3, 1.5), -2)


@sound
def Axe_Hit1():
    return chop(71, 1.0)


@sound
def Axe_Hit2():
    return chop(72, 0.9)


@sound
def Weapon_Switch():
    t = times(0.25)
    click1 = highpass(noise(0.03), 2000) * decay(0.03, 0.006)
    click2 = bandpass(noise(0.05), 800, 4000) * decay(0.05, 0.012)
    tone = np.sin(2 * np.pi * 1400 * t[:len(click2)]) * decay(0.05, 0.01) * 0.3
    x = mix(0.3, [(0, click1, 0.8), (0.09, click2 + tone, 0.9)])
    return finish(x, -6)


@sound
def Ammo_Refill():
    # The crate lid opens, cartridges clink into the bag one after another, then the magazine slides home and latches.
    rnd = np.random.RandomState(81)
    t = times(0.2)
    lid = mix(0.2, [(0, kick(0.15, 240, 110, 0.01), 0.6), (0, highpass(noise(0.05, rnd), 1800) * decay(0.05, 0.008), 0.7)])
    parts = [(0, lid, 0.8)]
    at = 0.14
    for i in range(8):
        k = rnd.uniform(0.85, 1.2)
        tc = times(0.09)
        ring = (np.sin(2 * np.pi * 2900 * k * tc) + 0.6 * np.sin(2 * np.pi * 4350 * k * tc)
                + 0.3 * np.sin(2 * np.pi * 6900 * k * tc)) * np.exp(-tc / rnd.uniform(0.025, 0.04))
        tick = highpass(noise(0.09, rnd), 3000) * np.exp(-tc / 0.003)
        parts.append((at, ring * 0.35 + tick * 0.6, rnd.uniform(0.45, 0.7)))
        at += rnd.uniform(0.07, 0.11)
    slide = sweep(noise(0.18, rnd), glide(900, 2600, 0.18), "band") * np.sin(np.linspace(0, np.pi, n_of(0.18)))
    parts.append((at + 0.05, slide, 0.5))
    latch = mix(0.12, [(0, highpass(noise(0.03, rnd), 2200) * decay(0.03, 0.005), 0.9),
                       (0.035, bandpass(noise(0.06, rnd), 700, 3500) * decay(0.06, 0.012), 0.9),
                       (0.035, kick(0.1, 180, 90, 0.008), 0.5)])
    parts.append((at + 0.24, latch, 1.0))
    x = mix(at + 0.45, parts)
    return finish(reverb(x, 0.12, 0.5), -4)


# ================================================================================================ hits
def car_hit(seed, pitch):
    # A zombie's blow on the car body: a dull thud, a sheet-metal "bonk" (inharmonic partials) and a short scrape.
    rnd = np.random.RandomState(seed)
    t = times(0.5)
    thud = kick(0.3, 110 * pitch, 50 * pitch, 0.012)
    bonk = sum(np.sin(2 * np.pi * f * pitch * t + rnd.rand() * 6) * np.exp(-t / tau) * g
               for f, tau, g in ((310, 0.22, 1.0), (747, 0.14, 0.7), (1187, 0.09, 0.5), (1930, 0.05, 0.35)))
    scrape = bandpass(noise(0.5, rnd), 1200, 4500) * np.exp(-t / 0.035)
    x = mix(0.5, [(0, thud, 1.0), (0, bonk, 0.35), (0, scrape, 0.45)])
    return finish(drive(x * 1.2, 1.3), -5)


@sound
def Car_Hit1():
    return car_hit(91, 1.0)


@sound
def Car_Hit2():
    return car_hit(92, 0.88)


@sound
def Car_Hit3():
    return car_hit(93, 1.12)


def player_hurt(seed, pitch):
    # A blow taken by the player: a punchy body thud, a slap and a short, low grunt under them.
    rnd = np.random.RandomState(seed)
    t = times(0.3)
    thud = kick(0.25, 150 * pitch, 60 * pitch, 0.01)
    slap = highpass(noise(0.3, rnd), 1500) * np.exp(-t / 0.012)
    body = lowpass(noise(0.3, rnd), 900) * np.exp(-t / 0.05)
    grunt = growl(0.2, 175 * pitch, 120 * pitch, (620.0, 1150.0, 2500.0), rough=0.25, flutter=18.0, seed=seed)
    grunt = grunt * np.sin(np.linspace(0, np.pi, len(grunt))) ** 0.5
    x = mix(0.35, [(0, thud, 1.0), (0, slap, 0.7), (0, body, 0.5), (0.015, grunt, 0.22)])
    return finish(x, -4)


@sound
def Player_Hurt1():
    return player_hurt(101, 1.0)


@sound
def Player_Hurt2():
    return player_hurt(102, 0.9)


def main(names):
    todo = names or list(SOUNDS)
    for name in todo:
        write_ogg(SOUNDS[name](), os.path.join(OUT, name + ".ogg"))


if __name__ == "__main__":
    main(sys.argv[1:])
