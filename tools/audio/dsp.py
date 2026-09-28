"""Small numpy synthesiser for the game's generated sounds and music (tools/audio/make_sfx.py, make_music.py).

Mono, 44.1 kHz float arrays in -1..1. Oscillators are band-limited (PolyBLEP), filters are Butterworth (scipy),
time-varying filters run in short blocks. Instruments are plain functions returning an array for one note.
"""
import os
import subprocess
import tempfile
import wave

import numpy as np
from scipy import signal

SR = 44100
RNG = np.random.RandomState(1234)


# ------------------------------------------------------------------------------------------------ basics
def n_of(seconds):
    return max(1, int(round(seconds * SR)))


def times(seconds):
    return np.arange(n_of(seconds)) / SR


def midi(m):
    return 440.0 * 2.0 ** ((m - 69) / 12.0)


NOTE = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6, "G": 7, "G#": 8,
        "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def note(name):
    """'A4' -> frequency (Hz)."""
    pitch = name[:-1]
    octave = int(name[-1])
    return midi(12 * (octave + 1) + NOTE[pitch])


def db(x):
    return 10.0 ** (x / 20.0)


def noise(seconds, rng=RNG):
    return rng.uniform(-1.0, 1.0, n_of(seconds))


# ------------------------------------------------------------------------------------------------ oscillators
def _phase(freq, n):
    f = np.broadcast_to(np.asarray(freq, np.float64), (n,)) if np.ndim(freq) == 0 else np.asarray(freq, np.float64)
    ph = np.cumsum(f / SR)
    return ph - np.floor(ph), f / SR


def _blep(ph, dt):
    out = np.zeros_like(ph)
    dt = np.maximum(dt, 1e-9)
    lo = ph < dt
    t = ph[lo] / dt[lo]
    out[lo] = t + t - t * t - 1.0
    hi = ph > 1.0 - dt
    t = (ph[hi] - 1.0) / dt[hi]
    out[hi] = t * t + t + t + 1.0
    return out


def saw(freq, seconds, phase=0.0):
    n = n_of(seconds)
    ph, dt = _phase(freq, n)
    ph = (ph + phase) % 1.0
    return 2.0 * ph - 1.0 - _blep(ph, dt)


def square(freq, seconds, duty=0.5):
    n = n_of(seconds)
    ph, dt = _phase(freq, n)
    a = 2.0 * ph - 1.0 - _blep(ph, dt)
    ph2 = (ph + (1.0 - duty)) % 1.0
    b = 2.0 * ph2 - 1.0 - _blep(ph2, dt)
    return (a - b) * 0.5


def sine(freq, seconds, phase=0.0):
    n = n_of(seconds)
    ph, _ = _phase(freq, n)
    return np.sin(2 * np.pi * (ph + phase))


def triangle(freq, seconds):
    s = square(freq, seconds)
    tri = np.cumsum(s)
    tri -= signal.lfilter([1], [1, -0.999], tri) * 0.001    # remove drift
    tri = signal.lfilter([1, -1], [1, -0.995], tri)
    m = np.max(np.abs(tri)) or 1.0
    return tri / m


def glide(f0, f1, seconds, curve="exp"):
    t = np.linspace(0.0, 1.0, n_of(seconds))
    if curve == "exp":
        return f0 * (f1 / f0) ** t
    return f0 + (f1 - f0) * t


# ------------------------------------------------------------------------------------------------ envelopes
def adsr(seconds, a=0.01, d=0.1, s=0.7, r=0.2, hold=None):
    """Attack / decay / sustain until `hold` (default: seconds - r) / release."""
    n = n_of(seconds)
    t = np.arange(n) / SR
    hold = seconds - r if hold is None else hold
    env = np.empty(n)
    env[:] = s
    atk = t < a
    env[atk] = t[atk] / max(a, 1e-6)
    dec = (t >= a) & (t < a + d)
    env[dec] = 1.0 - (1.0 - s) * (t[dec] - a) / max(d, 1e-6)
    rel = t >= hold
    level = np.interp(hold, [0, a, a + d], [0, 1, s]) if hold < a + d else s
    env[rel] = level * np.maximum(0.0, 1.0 - (t[rel] - hold) / max(r, 1e-6))
    return env


def decay(seconds, tau, attack=0.002):
    t = times(seconds)
    env = np.exp(-t / tau)
    if attack > 0:
        env *= np.minimum(1.0, t / attack)
    return env


# ------------------------------------------------------------------------------------------------ filters
def lowpass(x, cutoff, order=2):
    b, a = signal.butter(order, min(cutoff, SR * 0.45) / (SR / 2), "low")
    return signal.lfilter(b, a, x)


def highpass(x, cutoff, order=2):
    b, a = signal.butter(order, min(cutoff, SR * 0.45) / (SR / 2), "high")
    return signal.lfilter(b, a, x)


def bandpass(x, lo, hi, order=2):
    b, a = signal.butter(order, [lo / (SR / 2), min(hi, SR * 0.45) / (SR / 2)], "band")
    return signal.lfilter(b, a, x)


def sweep(x, cutoffs, kind="low", block=256, order=2):
    """Time-varying filter: `cutoffs` per sample (or per block), processed in blocks with the state carried."""
    cut = np.broadcast_to(np.asarray(cutoffs, np.float64), (len(x),)) if np.ndim(cutoffs) == 0 else np.asarray(cutoffs)
    out = np.empty_like(x)
    zi = None
    for i in range(0, len(x), block):
        c = float(np.clip(cut[min(i + block // 2, len(cut) - 1)], 20.0, SR * 0.45))
        if kind == "band":
            b, a = signal.butter(order, [c * 0.7 / (SR / 2), min(c * 1.4, SR * 0.45) / (SR / 2)], "band")
        else:
            b, a = signal.butter(order, c / (SR / 2), kind)
        if zi is None or len(zi) != max(len(a), len(b)) - 1:
            zi = signal.lfilter_zi(b, a) * 0.0
        out[i:i + block], zi = signal.lfilter(b, a, x[i:i + block], zi=zi)
    return out


def drive(x, amount):
    return np.tanh(x * amount) / np.tanh(amount)


# ------------------------------------------------------------------------------------------------ space
def impulse(seconds=2.0, damp=5000.0, predelay=0.012, rng=None):
    rng = rng or np.random.RandomState(99)
    n = n_of(seconds)
    t = np.arange(n) / SR
    ir = rng.uniform(-1, 1, n) * np.exp(-t * 6.9 / seconds)
    ir = lowpass(ir, damp)
    ir[:n_of(predelay)] = 0.0
    ir[0] = 0.0
    return ir / np.sqrt(np.sum(ir ** 2))


def reverb(x, wet=0.25, seconds=1.6, damp=5000.0, circular=False):
    ir = impulse(seconds, damp)
    if circular:
        n = len(x)
        wet_sig = np.real(np.fft.ifft(np.fft.fft(x) * np.fft.fft(ir, n)))
    else:
        wet_sig = signal.fftconvolve(np.concatenate([x, np.zeros(len(ir))]), ir)[:len(x) + len(ir)]
        x = np.concatenate([x, np.zeros(len(ir))])
    return x * (1.0 - wet * 0.5) + wet_sig * wet


def trim(x, threshold=db(-60)):
    idx = np.where(np.abs(x) > threshold)[0]
    return x[:idx[-1] + 1] if len(idx) else x


def fade(x, fin=0.002, fout=0.02):
    x = x.copy()
    a, b = n_of(fin), n_of(fout)
    x[:a] *= np.linspace(0, 1, a)
    x[-b:] *= np.linspace(1, 0, b)
    return x


def mix(length_seconds, parts):
    """parts: (start seconds, array, gain)."""
    out = np.zeros(n_of(length_seconds))
    for start, arr, gain in parts:
        i = n_of(start) if start > 0 else 0
        end = min(len(out), i + len(arr))
        if end > i:
            out[i:end] += arr[:end - i] * gain
    return out


def normalize(x, peak_db=-1.0):
    m = np.max(np.abs(x)) or 1.0
    return x / m * db(peak_db)


# ------------------------------------------------------------------------------------------------ output
def write_ogg(x, path, quality=6):
    """Encodes a float array to Ogg Vorbis (ffmpeg, libvorbis)."""
    x = np.clip(x, -1.0, 1.0)
    pcm = (x * 32767.0).astype(np.int16)
    fd, tmp = tempfile.mkstemp(suffix=".wav")
    os.close(fd)
    with wave.open(tmp, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())
    os.makedirs(os.path.dirname(path), exist_ok=True)
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", tmp, "-c:a", "libvorbis", "-q:a", str(quality), path], check=True)
    os.remove(tmp)
    print(os.path.basename(path), f"{len(x) / SR:.2f} s", f"{os.path.getsize(path) // 1024} KB")


def write_wav(x, path):
    x = np.clip(x, -1.0, 1.0)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767.0).astype(np.int16).tobytes())


# ------------------------------------------------------------------------------------------------ instruments
def pluck(freq, seconds=1.0, bright=1.0, tau=0.35, harmonics=10):
    """Piano / harp-like tone: harmonics with faster decay for higher partials."""
    t = times(seconds)
    out = np.zeros(len(t))
    for k in range(1, harmonics + 1):
        f = freq * k * (1.0 + 0.0004 * k * k)          # slight inharmonicity
        if f > SR * 0.45:
            break
        amp = (1.0 / k) ** (1.6 - 0.6 * bright)
        out += amp * np.sin(2 * np.pi * f * t) * np.exp(-t / (tau / (1 + 0.35 * (k - 1))))
    out *= np.minimum(1.0, t / 0.003)
    return out / (np.max(np.abs(out)) or 1.0)


def bell(freq, seconds=2.0, tau=0.8):
    t = times(seconds)
    partials = [(1.0, 1.0, 1.0), (2.0, 0.5, 0.7), (2.76, 0.45, 0.5), (5.4, 0.25, 0.3), (8.93, 0.12, 0.2)]
    out = np.zeros(len(t))
    for ratio, amp, life in partials:
        f = freq * ratio
        if f < SR * 0.45:
            out += amp * np.sin(2 * np.pi * f * t) * np.exp(-t / (tau * life))
    out *= np.minimum(1.0, t / 0.002)
    return out / (np.max(np.abs(out)) or 1.0)


def pad(freqs, seconds, cutoff=1400.0, attack=0.8, release=1.2, detune=0.007, voices=3):
    out = np.zeros(n_of(seconds))
    for f in freqs:
        for v in range(voices):
            d = 1.0 + detune * (v - (voices - 1) / 2)
            out += saw(f * d, seconds, phase=RNG.rand())
    out = lowpass(out, cutoff, 2)
    out *= adsr(seconds, attack, 0.3, 0.85, release)
    return out / (len(freqs) * voices) * 1.6


def bass(freq, seconds, cutoff=500.0, drive_amount=1.5, sub=0.6):
    x = saw(freq, seconds) * 0.6 + sine(freq * 0.5 if sub < 0 else freq, seconds) * abs(sub)
    x = drive(lowpass(x, cutoff, 2), drive_amount)
    return x * adsr(seconds, 0.005, 0.12, 0.7, 0.06)


def brass(freq, seconds, vibrato=5.0, depth=0.006, bright=2600.0):
    t = times(seconds)
    f = freq * (1.0 + depth * np.sin(2 * np.pi * vibrato * t) * np.minimum(1, t / 0.3))
    x = saw(f, seconds) + 0.35 * saw(f * 1.004, seconds, 0.3)
    env = adsr(seconds, 0.05, 0.15, 0.8, 0.12)
    cut = 300.0 + bright * env
    x = sweep(x, cut, "low")
    return drive(x * env, 1.4)


def power_chord(root, seconds, muted=False):
    x = saw(root, seconds) + saw(root * 1.5, seconds, 0.2) * 0.8 + saw(root * 2.0, seconds, 0.5) * 0.5
    x = drive(x * 2.5, 3.0)
    x = lowpass(x, 1800.0 if muted else 3200.0, 2)
    x = highpass(x, 90.0)
    env = decay(seconds, 0.09, 0.003) if muted else adsr(seconds, 0.005, 0.2, 0.6, 0.08)
    return x * env


def lead(freq, seconds, kind="square"):
    t = times(seconds)
    f = freq * (1.0 + 0.008 * np.sin(2 * np.pi * 5.5 * t) * np.minimum(1, t / 0.25))
    x = square(f, seconds, 0.4) if kind == "square" else saw(f, seconds)
    x = lowpass(x, 3200.0, 2)
    return x * adsr(seconds, 0.01, 0.1, 0.75, 0.08)


# ------------------------------------------------------------------------------------------------ drums
def kick(seconds=0.45, f0=150.0, f1=45.0, punch=0.03):
    t = times(seconds)
    f = f1 + (f0 - f1) * np.exp(-t / punch)
    ph = np.cumsum(f / SR)
    x = np.sin(2 * np.pi * ph) * np.exp(-t / 0.22)
    click = highpass(noise(seconds), 2000) * np.exp(-t / 0.004) * 0.3
    return drive(x + click, 1.5)


def snare(seconds=0.3):
    t = times(seconds)
    tone = np.sin(2 * np.pi * 190 * t) * np.exp(-t / 0.05)
    nz = bandpass(noise(seconds), 1200, 7000) * np.exp(-t / 0.12)
    return (tone * 0.6 + nz) * np.minimum(1, t / 0.001)


def hat(seconds=0.06, open_=False):
    t = times(seconds)
    x = highpass(noise(seconds), 7000, 2)
    return x * np.exp(-t / (0.12 if open_ else 0.018))


def tom(freq=110.0, seconds=0.5):
    t = times(seconds)
    f = freq * (1.0 + 0.6 * np.exp(-t / 0.05))
    ph = np.cumsum(f / SR)
    x = np.sin(2 * np.pi * ph) * np.exp(-t / 0.18)
    return x + bandpass(noise(seconds), 300, 2500) * np.exp(-t / 0.05) * 0.25


def crash(seconds=1.8):
    t = times(seconds)
    x = highpass(noise(seconds), 3500, 2) + bandpass(noise(seconds), 5000, 12000) * 0.6
    return x * np.exp(-t / 0.55) * np.minimum(1, t / 0.002) * 0.6


def boom(seconds=1.4, f0=90.0, f1=30.0):
    """Low cinematic hit."""
    t = times(seconds)
    f = f1 + (f0 - f1) * np.exp(-t / 0.12)
    ph = np.cumsum(f / SR)
    body = np.sin(2 * np.pi * ph) * np.exp(-t / 0.45)
    rumble = lowpass(noise(seconds), 220, 2) * np.exp(-t / 0.5) * 1.5
    return drive(body + rumble, 1.3)
