"""Atlas layout for sheet 1 (Signs & lights). Regions: (x, y, w, h) px in a 2048 atlas, y from top."""
ATLAS = 2048
ATLAS_FILE = "signs_atlas.png"
EMIT_FILE = "signs_emissive.png"

REG = {
    "wood_dark":   (0, 0, 256, 1024),
    "wood_orange": (256, 0, 256, 1024),
    "metal_pole":  (512, 0, 256, 1024),
    "arrow":       (768, 0, 1280, 480),
    "kafe":        (768, 480, 640, 224),
    "road_face":   (1408, 480, 512, 512),
    "ceramic":     (1920, 480, 128, 128),
    "cable":       (1920, 608, 128, 128),
    "red":         (1920, 736, 128, 128),
    "wood_frame":  (1920, 864, 128, 128),
    "concrete":    (768, 704, 320, 320),
    "metal_dark":  (1088, 704, 320, 320),
    "box_front":   (0, 1024, 512, 512),
    "metal_grey":  (512, 1024, 512, 512),
    "road_back":   (1024, 1024, 512, 512),
    "band":        (1536, 1024, 256, 128),
    "lamp_inner":  (1536, 1152, 256, 128),
}

# arrow outline in normalized (x, y) with y bottom -> top; geometry and texture share it
ARROW = [(0.0, 0.50), (0.055, 0.79), (0.62, 0.80), (0.60, 1.0), (1.0, 0.52), (0.62, 0.03),
         (0.63, 0.24), (0.055, 0.22)]
