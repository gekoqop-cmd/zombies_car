"""The three bosses' roars (their entrance and the enrage), each its own creature, layered like a film monster:

Boss1_BigRoar  Matka   — a wet, gurgling female roar breaking into a shriek, insect chitter under it, acid bubbles
Boss2_BigRoar  Taran   — two snorts, then a huge low bull/gorilla roar with chest boom and an iron ring
Boss3_BigRoar  Zhnets  — a sub-bass demon growl swelling into a raspy scream, fire crackle and a whispering layer

Run: python tools/audio/boss_roars.py   ->  unity/Assets/Audio/SFX/Boss<N>_BigRoar.ogg (CharacterBuilder prefers them
over the BossVoiceSynth roar).
"""
import os

import numpy as np

from dsp import (SR, adsr, bandpass, boom, decay, drive, glide, highpass, lowpass, mix, noise, normalize, reverb, saw,
                 sine, times, trim, fade, write_ogg)

OUT = os.path.join(os.path.dirname(__file__), "..", "..", "unity", "Assets", "Audio", "SFX")


def wander(n, rnd, rate=14.0, depth=0.03):
    """Slow random pitch drift (a living voice never holds a pitch)."""
    return 1.0 + lowpass(rnd.randn(n), rate) * depth * 40.0


def voice(seconds, f0, f1, rnd, formants, gains, rough=0.4, jitter=0.03, sub=0.4, curve="exp"):
    """Throat: detuned saws + a sub, breath noise, through formant bands, saturated."""
    n = len(times(seconds))
    f = glide(f0, f1, seconds, curve) * wander(n, rnd, depth=jitter)
    src = saw(f, seconds) + 0.8 * saw(f * 1.017, seconds, 0.3) + 0.6 * saw(f * 0.993, seconds, 0.7)
    src += sub * sine(f * 0.5, seconds)
    src += bandpass(noise(seconds, rnd), 250, 4500) * rough
    out = sum(bandpass(src, fc * 0.82, fc * 1.22) * g for fc, g in zip(formants, gains))
    return drive(out * 2.5, 2.2)


def gurgle(seconds, rnd, rate=32.0):
    """Liquid in the throat: lowpassed noise chopped by an uneven tremolo."""
    t = times(seconds)
    chop = 0.5 + 0.5 * np.sin(2 * np.pi * rate * t + 3 * lowpass(rnd.randn(len(t)), 6.0))
    return lowpass(noise(seconds, rnd), 900) * chop ** 2


def bubbles(seconds, rnd, count, lo=500, hi=1600):
    x = np.zeros(len(times(seconds)))
    for _ in range(count):
        i = int(rnd.uniform(0.05, 0.9) * len(x))
        f = rnd.uniform(lo, hi)
        d = sine(glide(f, f * 1.8, 0.05), 0.05) * decay(0.05, 0.012)
        x[i:i + len(d)] += d[:len(x) - i] * rnd.uniform(0.2, 0.5)
    return x


def chitter(seconds, rnd):
    """Insect clicks: short bright ticks in uneven bursts."""
    x = np.zeros(len(times(seconds)))
    t = 0.0
    while t < seconds - 0.05:
        burst = rnd.randint(3, 7)
        for k in range(burst):
            i = int((t + k * 0.022) * SR)
            if i >= len(x) - 300:
                break
            tick = highpass(noise(0.006, rnd), 2500) * decay(0.006, 0.0015)
            x[i:i + len(tick)] += tick
        t += rnd.uniform(0.18, 0.4)
    return x


def snort(rnd):
    n = noise(0.35, rnd)
    return bandpass(n, 300, 2600) * adsr(0.35, 0.02, 0.1, 0.5, 0.15) + lowpass(n, 180) * decay(0.35, 0.08) * 0.8


def metal_ring(seconds, rnd, base=210.0):
    t = times(seconds)
    partials = (1.0, 2.76, 5.4, 8.9)
    return sum(np.sin(2 * np.pi * base * p * t + rnd.rand() * 6) * np.exp(-t / (0.9 / p ** 0.5)) / (i + 1)
               for i, p in enumerate(partials))


def crackle(seconds, rnd, density=90):
    x = np.zeros(len(times(seconds)))
    for _ in range(int(density * seconds)):
        i = rnd.randint(0, len(x) - 200)
        pop = highpass(noise(0.004, rnd), 1800) * decay(0.004, 0.001) * rnd.uniform(0.3, 1.0)
        x[i:i + len(pop)] += pop
    return x


def matka():
    rnd = np.random.RandomState(11)
    s = 2.8
    body = voice(s, 230, 150, rnd, (520, 1150, 2600), (1.0, 0.75, 0.4), rough=0.5) * adsr(s, 0.12, 0.4, 0.8, 0.8)
    shriek = voice(1.3, 420, 680, rnd, (1300, 2600, 4200), (0.8, 1.0, 0.5), rough=0.7, jitter=0.05, sub=0.0, curve="lin")
    shriek *= adsr(1.3, 0.25, 0.2, 0.8, 0.5)
    wet = gurgle(s, rnd, 34) * adsr(s, 0.05, 0.3, 0.9, 0.8)
    x = mix(3.4, [(0, body, 0.9), (0.55, shriek, 0.55), (0, wet, 0.7), (0, chitter(s, rnd), 0.25),
                  (0.2, bubbles(2.6, rnd, 18), 0.45)])
    return normalize(fade(trim(reverb(x, 0.25, 1.4, 4000)), 0.002, 0.25), -1.0)


def taran():
    rnd = np.random.RandomState(22)
    s = 2.6
    roar = voice(s, 95, 62, rnd, (300, 700, 1700), (1.0, 0.8, 0.3), rough=0.55, jitter=0.02, sub=0.8)
    roar *= adsr(s, 0.15, 0.4, 0.85, 0.9)
    chest = lowpass(roar, 220) * 1.4
    x = mix(3.6, [(0, snort(rnd), 0.8), (0.3, snort(rnd), 0.9), (0.62, roar, 0.9), (0.62, chest, 0.7),
                  (0.62, boom(1.6, 75, 32), 0.7), (0.68, metal_ring(1.8, rnd), 0.22)])
    return normalize(fade(trim(reverb(x, 0.3, 1.8, 3000)), 0.002, 0.3), -1.0)


def zhnets():
    rnd = np.random.RandomState(33)
    s = 3.2
    t = times(s)
    growl = voice(s, 58, 46, rnd, (220, 520, 1200), (1.0, 0.7, 0.3), rough=0.6, jitter=0.025, sub=1.0)
    growl *= adsr(s, 0.3, 0.4, 0.9, 1.0)
    scream = voice(1.9, 220, 330, rnd, (900, 2100, 3600), (0.9, 1.0, 0.5), rough=0.9, jitter=0.05, sub=0.0, curve="lin")
    scream *= adsr(1.9, 0.5, 0.3, 0.8, 0.7)
    whisper = bandpass(noise(s, rnd), 2500, 7000) * (0.5 + 0.5 * np.sin(2 * np.pi * 4.0 * t)) * adsr(s, 0.6, 0.4, 0.6, 1.0)
    fire = lowpass(noise(s, rnd), 500) * adsr(s, 0.4, 0.3, 0.8, 1.0)
    x = mix(4.0, [(0, growl, 0.9), (0.9, scream, 0.6), (0, whisper, 0.18), (0, fire, 0.35), (0.1, crackle(3.2, rnd), 0.3),
                  (0, boom(2.0, 55, 25), 0.6)])
    return normalize(fade(trim(reverb(x, 0.4, 2.4, 3200)), 0.002, 0.4), -1.0)


def taran_charge():
    """The ram's charge: a snort and boots digging in during the wind-up, then a war grunt and heavy armoured
    footfalls speeding up (a thud and a clank of armour each) as he runs."""
    rnd = np.random.RandomState(44)
    total = 2.6
    x = np.zeros(int(total * SR))

    def put(sig, at, gain):
        i = int(at * SR)
        n = min(len(sig), len(x) - i)
        if n > 0:
            x[i:i + n] += sig[:n] * gain

    put(snort(rnd), 0.0, 0.9)
    scrape = bandpass(noise(0.35, rnd), 400, 3000) * adsr(0.35, 0.03, 0.1, 0.6, 0.15)
    put(scrape, 0.35, 0.5)
    put(scrape, 0.65, 0.5)
    grunt = voice(0.6, 120, 85, rnd, (320, 760, 1800), (1.0, 0.7, 0.3), rough=0.6, sub=0.6) * adsr(0.6, 0.03, 0.2, 0.7, 0.3)
    put(grunt, 0.95, 0.7)
    t, step = 1.0, 0.3
    while t < total - 0.25:
        thud = boom(0.35, 70, 32)
        dirt = lowpass(noise(0.12, rnd), 300) * decay(0.12, 0.03)
        clank = metal_ring(0.25, rnd, base=rnd.uniform(260, 330)) * 0.18
        put(thud, t, 0.9)
        put(dirt, t, 0.6)
        put(clank, t + 0.01, 1.0)
        t += step
        step = max(0.16, step * 0.85)
    return normalize(fade(trim(reverb(x, 0.15, 0.8, 3500)), 0.002, 0.15), -1.5)


def main():
    for n, fn in ((1, matka), (2, taran), (3, zhnets)):
        x = fn()
        path = os.path.join(OUT, f"Boss{n}_BigRoar.ogg")
        write_ogg(x, path)
    # Replaces the sound cut from the charge video (it did not fit).
    write_ogg(taran_charge(), os.path.join(OUT, "Boss2_Charge.ogg"))


if __name__ == "__main__":
    main()
