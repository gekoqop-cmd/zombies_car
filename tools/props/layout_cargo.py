"""Atlas layout for sheet 4 (Cargo + Furniture), 2048 px."""
ATLAS = 2048
ATLAS_FILE = "cargo_atlas.png"
REG = {
    # wood
    "wood_v":      (0, 0, 512, 512),        # vertical planks (crate panels)
    "wood_h":      (512, 0, 512, 512),      # horizontal planks
    "board_h":     (1024, 0, 640, 96),      # frame board, grain along u, bolts at the ends
    "board_hn":    (1024, 96, 640, 96),     # same, no bolts (braces)
    "wood_top":    (1024, 192, 640, 320),   # workbench top
    "board_v":     (1664, 0, 96, 512),
    "board_vn":    (1760, 0, 96, 512),
    # painted panels
    "olive":       (0, 512, 256, 256),
    "olive_face":  (256, 512, 256, 256),
    "card_side":   (512, 512, 256, 256),
    "card_top":    (768, 512, 256, 256),
    "jerry_red":   (1024, 512, 256, 256),
    "jerry_green": (1280, 512, 256, 256),
    "red_panel":   (1536, 512, 256, 256),
    "grey_panel":  (1792, 512, 256, 256),
    "red_drawer":  (0, 768, 256, 128),
    "grey_drawer": (0, 896, 256, 128),
    "red_door":    (256, 768, 256, 256),
    "pegboard":    (512, 768, 768, 256),
    "can_grey":    (1280, 768, 256, 128),   # cans: u around, v height
    "can_blue":    (1280, 896, 256, 128),
    "can_yellow":  (1536, 768, 256, 128),
    "can_red":     (1536, 896, 256, 128),
    "can_top":     (1792, 768, 128, 128),
    "hardhat":     (1920, 768, 128, 128),
    "box_red":     (1792, 896, 128, 128),   # small box fronts with latch
    "box_olive":   (1920, 896, 128, 128),
    "box_tan":     (0, 1024, 128, 128),
    "grey_post":   (128, 1024, 96, 512),    # rack upright with bolt holes
    "wood_leg":    (224, 1024, 96, 512),
}
_SW = [["wood_hi", "wood_lo", "endgrain"], ["red_hi", "red_lo", "red_flat"], ["olive_hi", "olive_lo", "rubber"],
       ["grey_hi", "grey_lo", "chrome"], ["card_hi", "card_lo", "black"], ["blue", "yellow", "orange"],
       ["strap_g", "strap_d", "brass"], ["green_flat", "olive_flat", "tan"]]
for r, row in enumerate(_SW):
    for c, k in enumerate(row):
        REG[k] = (1856 + 64 * c, 64 * r, 64, 64)
