"""Atlas layout for sheet 2 (Fences), 1024 px."""
ATLAS = 1024
ATLAS_FILE = "fences_atlas.png"
REG = {
    "post_front": (0, 0, 160, 512),
    "post_side":  (160, 0, 160, 512),
    "post_top":   (320, 0, 128, 128),
    "edge_hi":    (320, 128, 64, 64),
    "edge_lo":    (384, 128, 64, 64),
    "endgrain":   (320, 192, 128, 128),
    "rail":       (448, 0, 576, 160),
    "rail_b":     (448, 160, 576, 160),
    "plank":      (448, 320, 576, 96),
    "rail_top":   (448, 416, 576, 64),
    "gate":       (0, 512, 512, 512),
    "metal":      (512, 512, 160, 160),
    "stile":      (672, 512, 96, 512),
}
POST_H = 1.15
RAIL_Z = (0.36, 0.80)          # rail centers
BOLT_V = tuple(z / POST_H for z in RAIL_Z)
