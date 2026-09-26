"""Shared layout for the parked wreck cars (pickup + sedan): parts atlas regions and paint tiling.
Regions are (x, y, w, h) in pixels of a PARTS-sized atlas, y measured from the top of the image."""

PAINT = 1024          # tileable paint texture (one per car), box-projected
PAINT_TILE = 1.6      # metres covered by one paint tile
PARTS = 1024

REG = {
    "trim":     (0, 0, 256, 256),      # dark rusty metal: bumpers, grille, frames
    "rubber":   (256, 0, 256, 256),    # window seals, mud flaps
    "chrome":   (512, 0, 256, 256),    # dull steel: handles, mirror glass frame
    "interior": (768, 0, 256, 256),    # brown seats / dashboard
    "tire":     (0, 256, 256, 512),    # u around, v across profile
    "rim":      (256, 256, 256, 256),
    "tail":     (512, 256, 256, 128),
    "head":     (512, 384, 256, 128),
    "ind":      (768, 256, 256, 128),  # orange indicators
    "plate":    (768, 384, 256, 128),
    "wood":     (256, 512, 512, 256),  # pickup bed planks
    "glass":    (0, 768, 512, 256),    # dirty glass with a crack web
    "broken":   (512, 768, 512, 256),  # smashed glass with holes
    "rust":     (768, 512, 256, 256),  # bare rust: bed floor edges, exhaust
}
