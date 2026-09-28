"""Generates the game's music loops into unity/Assets/Audio/Music/<Name>.ogg (mono, seamless loops: note tails and the
reverb that run past the end are wrapped round to the start).

  Music_Menu   calm, atmospheric: slow pads, a soft harp arpeggio, wind (main menu)
  Music_Day    the evening map: driving drums, bass, palm-muted rusty guitar, a pentatonic lead
  Music_Night  the night map: half-time drums, low drone, music-box bells, tense harmony
  Music_Boss   boss waves: fast, pounding, phrygian riff with brass stabs

Run: python tools/audio/make_music.py [name ...]
"""
import os
import sys

import numpy as np

from dsp import (SR, adsr, bass, bell, brass, crash, db, decay, drive, hat, highpass, kick, lead, lowpass, n_of, noise,
                 note, pad, pluck, power_chord, reverb, saw, sine, snare, sweep, times, tom, write_ogg)

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
OUT = os.path.join(ROOT, "unity", "Assets", "Audio", "Music")
TRACKS = {}


def track(fn):
    TRACKS[fn.__name__] = fn
    return fn


class Song:
    """A loop of `bars` bars at `bpm`, 4/4. Notes are placed by beat; everything is summed into buses."""

    def __init__(self, bpm, bars):
        self.bpm = bpm
        self.beat = 60.0 / bpm
        self.bars = bars
        self.length = bars * 4 * self.beat
        self.n = n_of(self.length)
        self.buses = {}

    def bus(self, name):
        if name not in self.buses:
            self.buses[name] = np.zeros(self.n + n_of(4.0))       # room for tails (wrapped later)
        return self.buses[name]

    def add(self, name, beat, sound, gain=1.0):
        b = self.bus(name)
        i = n_of(beat * self.beat)
        end = min(len(b), i + len(sound))
        b[i:end] += sound[:end - i] * gain

    def render(self, sends, master=1.0):
        """sends: bus -> (gain, reverb send). Returns the looped mix (tails wrapped)."""
        dry = np.zeros(self.n)
        wet_in = np.zeros(self.n)
        for name, buf in self.buses.items():
            gain, send = sends.get(name, (1.0, 0.0))
            looped = buf[:self.n].copy()
            tail = buf[self.n:]
            looped[:len(tail)] += tail[:self.n]
            dry += looped * gain
            wet_in += looped * gain * send
        wet = reverb(wet_in, 1.0, 2.4, 4200.0, circular=True)
        wet = wet[:self.n] if len(wet) >= self.n else np.pad(wet, (0, self.n - len(wet)))
        mixbus = dry + wet * 0.6
        mixbus = highpass(mixbus, 30.0)
        # Gentle glue: soft clip, then level to about -16 dBFS RMS with the peak under -1 dBFS.
        mixbus = drive(mixbus / (np.max(np.abs(mixbus)) or 1) * 1.2, 1.2)
        rms = np.sqrt(np.mean(mixbus ** 2)) or 1.0
        mixbus *= db(-16.0) / rms * master
        peak = np.max(np.abs(mixbus))
        if peak > db(-1.0):
            mixbus *= db(-1.0) / peak
        return mixbus


def chord_notes(root, kind="m", octave=3):
    r = note(f"{root}{octave}")
    third = 3 if kind == "m" else 4
    return [r, r * 2 ** (third / 12), r * 2 ** (7 / 12)]


# ================================================================================================ menu
@track
def Music_Menu():
    s = Song(72, 16)
    prog = [("A", "m"), ("F", "M"), ("C", "M"), ("G", "M")]
    arp_order = [0, 1, 2, 3, 2, 1, 2, 3]
    melody = {  # bar -> [(beat in bar, note, length in beats)]
        9: [(0, "E5", 1.5), (1.5, "D5", 0.5), (2, "C5", 2)], 10: [(0, "A4", 3)],
        11: [(0, "G4", 1.5), (1.5, "A4", 0.5), (2, "C5", 2)], 12: [(0, "B4", 4)],
        13: [(0, "E5", 1.5), (1.5, "G5", 0.5), (2, "E5", 2)], 14: [(0, "C5", 3)],
        15: [(0, "D5", 1.5), (1.5, "C5", 0.5), (2, "B4", 1)], 16: [(0, "G4", 2), (2, "A4", 2)],
    }
    for bar in range(16):
        root, kind = prog[(bar // 2) % 4]
        start = bar * 4
        if bar % 2 == 0:
            tones = chord_notes(root, kind, 3)
            s.add("pad", start, pad(tones + [tones[0] * 2], 8 * s.beat + 1.5, 1100, 1.4, 1.5), 0.55)
            s.add("bass", start, sine(tones[0] / 2, 8 * s.beat) * adsr(8 * s.beat, 0.4, 0.5, 0.8, 1.0), 0.35)
        if bar >= 2:
            tones = chord_notes(root, kind, 4) + [chord_notes(root, kind, 5)[0]]
            for i, k in enumerate(arp_order):
                vel = 0.5 if i % 2 else 0.7
                s.add("arp", start + i * 0.5, pluck(tones[k], 1.6, 0.6, 0.55), vel * (0.6 if bar < 8 else 0.8))
        for beat, n, length in melody.get(bar + 1, []):
            s.add("melody", start + beat, bell(note(n), length * s.beat + 1.2, 0.9), 0.35)
    wind = lowpass(noise(s.length + 4.0), 700.0) * (0.6 + 0.4 * np.sin(np.linspace(0, 2 * np.pi * 3, n_of(s.length + 4.0))))
    s.add("wind", 0, wind, 0.08)
    return s.render({"pad": (1.0, 0.5), "bass": (0.9, 0.1), "arp": (0.8, 0.6), "melody": (0.9, 0.8), "wind": (1.0, 0.2)}, 0.9)


# ================================================================================================ day
@track
def Music_Day():
    s = Song(104, 24)
    prog = [("D", "m", "D2"), ("Bb", "M", "Bb1"), ("F", "M", "F2"), ("C", "M", "C2")]
    riff = ["D5", "F5", "G5", "A5", "C6", "A5", "G5", "F5"]
    for bar in range(24):
        root, kind, bass_note = prog[bar % 4]
        start = bar * 4
        section = 0 if bar < 4 else 1 if bar < 12 else 2 if bar < 20 else 1
        # Drums.
        for b in (0, 2.5, 2):
            if b == 2.5 and bar % 2 == 0:
                continue
            s.add("kick", start + b, kick(0.4, 140, 48), 1.0)
        if section > 0 or bar >= 2:
            s.add("snare", start + 1, snare(0.3), 0.7)
            s.add("snare", start + 3, snare(0.3), 0.7)
        for i in range(8):
            s.add("hat", start + i * 0.5, hat(0.05), 0.45 if i % 2 else 0.3)
        if bar in (0, 4, 12, 20):
            s.add("crash", start, crash(2.0), 0.5)
        if bar % 4 == 3 and section > 0:
            for i, f in enumerate((140, 120, 100, 85)):
                s.add("tom", start + 2 + i * 0.5, tom(f, 0.4), 0.5)
        # Bass: pumping eighths with an octave jump.
        bf = note(bass_note)
        for i in range(8):
            f = bf * (2 if i in (3, 7) else 1)
            s.add("bass", start + i * 0.5, bass(f, 0.5 * s.beat, 650, 2.0), 0.55)
        # Guitar: palm-muted eighths in A, open chords in B.
        g = note(bass_note) * (2 if note(bass_note) < 80 else 1)
        if section == 1:
            for i in range(8):
                s.add("guitar", start + i * 0.5, power_chord(g, 0.45 * s.beat, True), 0.35)
        elif section == 2:
            s.add("guitar", start, power_chord(g, 3.8 * s.beat, False), 0.3)
            for i, n in enumerate(riff if bar % 2 == 0 else riff[::-1]):
                s.add("lead", start + i * 0.5, lead(note(n), 0.48 * s.beat, "square"), 0.2)
        # Pad underneath (keeps the harmony when the guitar rests).
        if bar % 2 == 0:
            s.add("pad", start, pad(chord_notes(root, kind, 3), 8 * s.beat, 900, 0.4, 0.6), 0.25)
    return s.render({"kick": (1.0, 0.05), "snare": (0.9, 0.25), "hat": (0.6, 0.1), "crash": (0.6, 0.2), "tom": (0.8, 0.3),
                     "bass": (0.9, 0.0), "guitar": (0.8, 0.15), "lead": (0.8, 0.35), "pad": (0.7, 0.4)}, 1.0)


# ================================================================================================ night
@track
def Music_Night():
    s = Song(84, 20)
    prog = [("E", "m"), ("C", "M"), ("A", "m"), ("B", "M")]
    box = ["E5", "G5", "B5", "E6", "D6", "B5", "G5", "F#5"]
    for bar in range(20):
        root, kind = prog[bar % 4]
        start = bar * 4
        s.add("kick", start, kick(0.5, 120, 40), 0.9)
        if bar >= 4:
            s.add("snare", start + 2, snare(0.35), 0.55)
            s.add("kick", start + 3.5, kick(0.4, 110, 40), 0.5)
            for i in range(4):
                s.add("hat", start + i + 0.5, hat(0.12, True), 0.18)
        tones = chord_notes(root, kind, 3)
        s.add("pad", start, pad(tones, 4 * s.beat + 1.0, 650, 0.9, 1.0, 0.009), 0.45)
        if bar % 4 == 0:
            s.add("drone", start, lowpass(saw(note("E1"), 16 * s.beat + 1) + saw(note("E1") * 1.005, 16 * s.beat + 1, 0.4), 220)
                  * adsr(16 * s.beat + 1, 1.5, 1.0, 0.9, 1.5), 0.5)
        if bar >= 8:
            order = box if bar % 2 == 0 else box[::-1]
            for i, n in enumerate(order):
                s.add("bells", start + i * 0.5, bell(note(n), 1.8, 0.7), 0.25 if i % 2 else 0.32)
        if bar >= 12 and bar % 2 == 1:
            s.add("tension", start, brass(note(f"{root}4"), 3.5 * s.beat, depth=0.01, bright=900), 0.12)
    return s.render({"kick": (1.0, 0.1), "snare": (0.8, 0.45), "hat": (0.5, 0.3), "pad": (0.9, 0.55), "drone": (0.9, 0.1),
                     "bells": (0.9, 0.8), "tension": (0.8, 0.5)}, 0.95)


# ================================================================================================ boss
@track
def Music_Boss():
    s = Song(140, 24)
    ostinato = ["E2", "E2", "F2", "E2", "G2", "E2", "F2", "E2", "E2", "E2", "F2", "E2", "A2", "G2", "F2", "D2"]
    stabs = [("E4", "F4"), ("G4", "A#4"), ("E4", "F4"), ("C5", "B4")]
    for bar in range(24):
        start = bar * 4
        for b in range(4):
            s.add("kick", start + b, kick(0.35, 160, 50), 1.0)
        s.add("snare", start + 1, snare(0.25), 0.8)
        s.add("snare", start + 3, snare(0.25), 0.8)
        for i in range(16):
            s.add("hat", start + i * 0.25, hat(0.04), 0.35 if i % 2 else 0.22)
        for i, n in enumerate(ostinato):
            s.add("bass", start + i * 0.25, bass(note(n), 0.24 * s.beat, 900, 3.0), 0.5)
        if bar >= 4:
            a, b2 = stabs[(bar // 2) % 4]
            s.add("stab", start, brass(note(a), 0.9 * s.beat, depth=0.0, bright=3200) + brass(note(b2), 0.9 * s.beat, depth=0.0, bright=3200), 0.18)
            s.add("stab", start + 2.5, brass(note(a), 0.5 * s.beat, depth=0.0, bright=3200) + brass(note(b2), 0.5 * s.beat, depth=0.0, bright=3200), 0.16)
        if bar >= 8 and bar % 2 == 0:
            s.add("guitar", start, power_chord(note("E2"), 1.9 * s.beat, False), 0.3)
            s.add("guitar", start + 2, power_chord(note("F2"), 1.9 * s.beat, False), 0.3)
        if bar >= 16:
            for i, n in enumerate(("B4", "C5", "B4", "A4", "G4", "A4", "B4", "E5")):
                s.add("lead", start + i * 0.5, lead(note(n), 0.45 * s.beat, "saw"), 0.14)
        if bar % 8 == 0:
            s.add("crash", start, crash(2.2), 0.55)
        if bar % 4 == 3:
            for i, f in enumerate((180, 150, 120, 95, 80, 70)):
                s.add("tom", start + 2.5 + i * 0.25, tom(f, 0.35), 0.55)
    return s.render({"kick": (1.0, 0.03), "snare": (0.9, 0.2), "hat": (0.6, 0.05), "bass": (0.95, 0.0), "stab": (0.8, 0.3),
                     "guitar": (0.75, 0.15), "lead": (0.8, 0.3), "crash": (0.6, 0.2), "tom": (0.8, 0.25)}, 1.0)


def main(names):
    for name in names or list(TRACKS):
        write_ogg(TRACKS[name](), os.path.join(OUT, name + ".ogg"), quality=5)


if __name__ == "__main__":
    main(sys.argv[1:])
