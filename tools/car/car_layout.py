"""Shared atlas layout for the player car (used by texture generator and Blender builder).
Regions are (x, y, w, h) in pixels of a 2048 atlas, y measured from the top of the image."""

ATLAS = 2048
EMIT = 256

REG = {
    "side":   (0, 0, 2048, 640),      # body sides, projected (s, z)
    "top":    (0, 640, 2048, 880),    # hood / roof, projected (s, x)
    "front":  (0, 1520, 512, 440),    # front / back faces, projected (x, z)
    "trim":   (512, 1520, 256, 256),  # dark metal
    "rubber": (768, 1520, 256, 256),
    "tire":   (1024, 1520, 256, 528), # u around, v across profile
    "rim":    (1280, 1520, 256, 256),
    "glass":  (1536, 1520, 512, 512),
    "can_g":  (512, 1776, 256, 256),
    "can_r":  (768, 1776, 256, 256),
    "crate":  (1280, 1776, 256, 256),
    "yellow": (0, 1960, 256, 88),
    "plate":  (256, 1960, 256, 88),
    "orange": (512, 2032, 256, 16),
}

# emissive texture quadrants (x, y, w, h), 256 px
EREG = {
    "head": (0, 0, 128, 128),
    "lamp": (128, 0, 128, 128),
    "tail": (0, 128, 128, 128),
    "ind":  (128, 128, 128, 128),
}

# projection bounds for painted body (s = forward distance = -y)
S0, S1 = -2.45, 2.45
Z0, Z1 = 0.30, 2.25
X0, X1 = -1.15, 1.15

# key body stations (s, z) used for painting door seams etc.
DOOR_SEAMS = [0.61, -0.21, -0.98]
BELT_Z = 1.39
SILL_Z = 0.55
FUEL_CAP = (-1.87, 1.08)
HOOD_SEAM_S = 1.40
