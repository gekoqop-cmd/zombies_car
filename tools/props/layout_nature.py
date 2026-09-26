"""Atlas layout for sheet 5 (Nature), 2048 px RGBA (alpha used by leaf cards and flowers)."""
ATLAS = 2048
ATLAS_FILE = "nature_atlas.png"
REG = {
    "leaves_a":   (0, 0, 512, 512),       # alpha leaf clusters (cards)
    "leaves_b":   (512, 0, 512, 512),
    "leaves_c":   (1024, 0, 512, 512),    # darker, for inner/lower cards
    "leaf_core":  (1536, 0, 256, 256),    # opaque dark core
    "flower_w":   (1792, 0, 128, 128),    # alpha
    "flower_o":   (1920, 0, 128, 128),    # alpha
    "stem":       (1536, 256, 64, 256),
    "grass_a":    (1600, 256, 96, 256),   # blades: u across, v base -> tip
    "grass_b":    (1696, 256, 96, 256),
    "grass_c":    (1792, 256, 96, 256),
    "seed":       (1888, 256, 64, 256),
    "bud":        (1952, 256, 96, 96),
    "rock":       (0, 512, 512, 512),
    "rock_top":   (512, 512, 512, 512),
    "slab":       (1024, 512, 512, 512),
    "slab_side":  (1536, 512, 512, 256),
    "rock_hi":    (1536, 768, 128, 128),
    "rock_lo":    (1664, 768, 128, 128),
}
