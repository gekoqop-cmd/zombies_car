"""Sheet 2 atlas: cartoon fences.  python tex_fences.py"""
import os, sys
import numpy as np
from PIL import Image, ImageDraw

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from tex_common import *  # noqa
from layout_fences import *  # noqa

A = Image.new("RGB", (ATLAS, ATLAS), (60, 60, 60))
WOOD = (0.92, 0.48, 0.17)
WOOD_D = (0.84, 0.42, 0.14)

# posts: three vertical strips, bolts at rail heights on the front
x, y, w, h = REG["post_front"]
im = to_img(ao_bottom(toon_wood(w, h, WOOD, 1, vertical=True, planks=3, strokes=45, cracks=3, knots=1,
                                notches=3), 0.35, 0.85))
d = ImageDraw.Draw(im)
for v in BOLT_V:
    toon_bolt(d, w / 2, (1 - v) * h, 17)
put(A, REG["post_front"], im)
x, y, w, h = REG["post_side"]
put(A, REG["post_side"], ao_bottom(toon_wood(w, h, WOOD_D, 2, vertical=True, planks=2, strokes=45, cracks=3,
                                             knots=1, notches=3), 0.4, 0.85))
x, y, w, h = REG["post_top"]
img = solid(w, h, (0.97, 0.60, 0.26), 3, amt=0.1)
yy, xx = np.mgrid[0:h, 0:w]; rr = np.hypot(yy - h * 0.55, xx - w * 0.45)
img *= (0.93 + 0.08 * np.sin(rr * 0.45))[..., None]
put(A, REG["post_top"], bevel_edges(img, 10, 0.25, 0.3))
put(A, REG["edge_hi"], solid(64, 64, (1.0, 0.64, 0.24), 4, amt=0.08))
put(A, REG["edge_lo"], solid(64, 64, (0.52, 0.21, 0.06), 5, amt=0.08))
x, y, w, h = REG["endgrain"]
img = solid(w, h, (0.80, 0.42, 0.16), 6, amt=0.12)
rr = np.hypot(*(np.mgrid[0:h, 0:w] - np.array([h / 2, w / 2])[:, None, None]))
img *= (0.9 + 0.1 * np.sin(rr * 0.5))[..., None]
put(A, REG["endgrain"], bevel_edges(img, 8, 0.2, 0.3))

# rails
x, y, w, h = REG["rail"]
put(A, REG["rail"], toon_wood(w, h, WOOD, 10, vertical=False, planks=2, strokes=70, cracks=4, knots=2, notches=8))
x, y, w, h = REG["rail_b"]
im = to_img(toon_wood(w, h, WOOD, 11, vertical=False, planks=2, strokes=70, cracks=5, knots=1, notches=6))
d = ImageDraw.Draw(im)
d.polygon([(w * 0.60, 0), (w * 0.64, h * 0.40), (w * 0.68, h * 0.22), (w * 0.72, h * 0.46), (w * 0.77, 0)],
          fill=(70, 30, 10))
d.line([(w * 0.64, h * 0.40), (w * 0.68, h * 0.22), (w * 0.72, h * 0.46)], fill=(255, 190, 120), width=3)
put(A, REG["rail_b"], im)
x, y, w, h = REG["plank"]
put(A, REG["plank"], toon_wood(w, h, WOOD, 12, vertical=False, strokes=50, cracks=3, knots=1, notches=5, edge=9))
x, y, w, h = REG["rail_top"]
put(A, REG["rail_top"], toon_wood(w, h, (0.98, 0.58, 0.24), 13, vertical=False, strokes=30, cracks=1, knots=0,
                                  notches=3, edge=6, hi=0.2, lo=0.2))

# gate: vertical planks
x, y, w, h = REG["gate"]
put(A, REG["gate"], ao_edges(toon_wood(w, h, WOOD_D, 20, vertical=True, planks=5, strokes=90, cracks=6, knots=2,
                                       notches=4, edge=14), 0.25, 0.08))
x, y, w, h = REG["stile"]
put(A, REG["stile"], toon_wood(w, h, WOOD, 21, vertical=True, strokes=35, cracks=2, knots=1, notches=3, edge=9))

# metal (hinges, brackets)
x, y, w, h = REG["metal"]
img = solid(w, h, (0.58, 0.62, 0.68), 30, amt=0.12)
img = blotches(img, 31, 0.78, (0.62, 0.36, 0.18), None, 6)
im = to_img(bevel_edges(img, 12, 0.35, 0.45)); d = ImageDraw.Draw(im)
for (px, py) in ((w * 0.28, h * 0.3), (w * 0.72, h * 0.3), (w * 0.28, h * 0.72), (w * 0.72, h * 0.72)):
    d.ellipse([px - 9, py - 9, px + 9, py + 9], fill=(40, 42, 48)); d.ellipse([px - 5, py - 6, px, py - 1], fill=(190, 195, 205))
put(A, REG["metal"], im)

save(A, ATLAS_FILE)
