"""Atlas layout + shared profiles for sheet 3 (Street), 2048 px."""
import math

ATLAS = 2048
ATLAS_FILE = "street_atlas.png"
REG = {
    "barrel_rw":    (0, 0, 512, 512),       # u around, v along profile (bottom -> top)
    "barrel_grey":  (512, 0, 512, 512),
    "lid_rw":       (1024, 0, 256, 256),
    "lid_grey":     (1280, 0, 256, 256),
    "cone":         (1536, 0, 256, 512),
    "cone_base":    (1792, 0, 256, 256),
    "rubber":       (1792, 256, 128, 128),
    "grass":        (1920, 256, 128, 256),  # blade: u across, v base -> tip
    "tire_tread":   (0, 512, 1024, 192),
    "tire_side":    (0, 704, 1024, 160),
    "tire_inner":   (0, 864, 1024, 96),
    "concrete":     (1024, 512, 256, 256),
    "barrier_face": (0, 1024, 1536, 512),   # planar: x along the barrier, z up
    "barrier_end":  (1536, 1024, 512, 512),
    "barrier_top":  (0, 1536, 1536, 128),
}

# barrel (r, z), bottom -> top; ribs at 0.295 and 0.585
BARREL_R, BARREL_H = 0.30, 0.88
BARREL_PROFILE = [(0.282, 0.0), (0.312, 0.02), (0.312, 0.045), (0.288, 0.062),
                  (0.288, 0.276), (0.309, 0.295), (0.288, 0.314),
                  (0.288, 0.566), (0.309, 0.585), (0.288, 0.604),
                  (0.288, 0.818), (0.312, 0.835), (0.312, 0.86), (0.282, 0.88)]
CONE_PROFILE = [(0.175, 0.045), (0.042, 0.675), (0.030, 0.705), (0.0, 0.715)]
BARRIER_L, BARRIER_H = 1.9, 0.85


def arc_fracs(profile):
    L = [0.0]
    for (r0, z0), (r1, z1) in zip(profile, profile[1:]):
        L.append(L[-1] + math.hypot(r1 - r0, z1 - z0))
    return [l / L[-1] for l in L]


def z_of_v(profile, v):
    """Height at arc-length fraction v (used to paint bands at the right heights)."""
    fr = arc_fracs(profile)
    for i in range(len(fr) - 1):
        if fr[i] <= v <= fr[i + 1]:
            t = (v - fr[i]) / ((fr[i + 1] - fr[i]) or 1)
            return profile[i][1] + (profile[i + 1][1] - profile[i][1]) * t
    return profile[-1][1]
