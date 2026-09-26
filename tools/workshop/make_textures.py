"""Procedural hand-painted style textures for the Workshop asset.
Run with system Python (needs numpy + Pillow):  python make_textures.py
"""
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

OUT = r"D:\zombi-caaar\models\Textures"
FONT_HEAVY = r"C:\Windows\Fonts\ariblk.ttf"
os.makedirs(OUT, exist_ok=True)


# ---------------------------------------------------------------- noise utils
def vnoise(size, cells, seed):
    """Tileable value noise, shape (size, size), range 0..1."""
    r = np.random.default_rng(seed)
    g = r.random((cells, cells))
    t = np.arange(size) * cells / size
    i0 = np.floor(t).astype(int) % cells
    i1 = (i0 + 1) % cells
    f = t - np.floor(t)
    f = f * f * (3 - 2 * f)
    a = g[i0][:, i0]; b = g[i0][:, i1]; c = g[i1][:, i0]; d = g[i1][:, i1]
    fx = f[None, :]; fy = f[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(size, cells, octaves, seed, gain=0.5):
    acc = np.zeros((size, size)); amp = 1.0; tot = 0.0
    for o in range(octaves):
        acc += amp * vnoise(size, cells * 2 ** o, seed + o * 17)
        tot += amp; amp *= gain
    return acc / tot


def grain(size, seed, amt=0.04):
    r = np.random.default_rng(seed)
    return 1 + (r.random((size, size)) - 0.5) * 2 * amt


def save(arr, name):
    a = np.clip(arr, 0, 1)
    Image.fromarray((a * 255).astype(np.uint8)).save(os.path.join(OUT, name))
    print("saved", name)


def blocks(size, rows, wmin, wmax, seed, base, var=0.08, mortar=(0, 0, 0),
           mw=4, bevel=8, hi=0.12, lo=0.16, hue=0.03):
    """Staggered tileable block pattern with painted bevels.
    Returns rgb image and mortar mask."""
    r = np.random.default_rng(seed)
    img = np.zeros((size, size, 3))
    mmask = np.zeros((size, size), bool)
    h = size / rows
    xs = np.arange(size)
    base = np.array(base, float)
    for ri in range(rows):
        widths = []
        while sum(widths) < size:
            widths.append(r.integers(wmin, wmax + 1))
        widths = np.array(widths, float)
        widths *= size / widths.sum()
        edges = np.concatenate([[0], np.cumsum(widths)])
        off = r.random() * size
        xl = (xs - off) % size
        bi = np.clip(np.searchsorted(edges, xl, side="right") - 1, 0, len(widths) - 1)
        u = xl - edges[bi]
        w = widths[bi]
        cols = base[None, :] * (1 + var * (r.random((len(widths), 1)) * 2 - 1))
        cols *= 1 + hue * (r.random((len(widths), 3)) * 2 - 1)
        y0 = int(round(ri * h)); y1 = int(round((ri + 1) * h))
        v = (np.arange(y0, y1) - ri * h)[:, None]
        U = u[None, :]; W = w[None, :]
        dt = v - mw; dl = U - mw; db = h - v; dr = W - U
        c = lambda d: np.clip(1 - d / bevel, 0, 1)
        shade = hi * c(dt) + hi * 0.5 * c(dl) - lo * c(db) - lo * 0.5 * c(dr)
        blk = cols[bi][None, :, :] * (1 + shade[..., None])
        m = (v < mw) | (U < mw)
        blk[np.broadcast_to(m, blk.shape[:2])] = mortar
        img[y0:y1] = blk
        mmask[y0:y1] = np.broadcast_to(m, (y1 - y0, size))
    return img, mmask


def chips(img, size, seed, thr, color, rim, scale=4, rim_w=0.035, inner=None):
    m = fbm(size, scale, 4, seed)
    m = m * 0.7 + 0.3 * fbm(size, scale * 4, 2, seed + 5)
    inside = m > thr
    ring = (m > thr - rim_w) & ~inside
    fill = np.array(color)[None, None, :] if inner is None else inner
    img = np.where(inside[..., None], fill, img)
    img = np.where(ring[..., None], np.array(rim)[None, None, :], img)
    return img


def paint(img, size, seed, amt=0.18, cells=6):
    n = fbm(size, cells, 4, seed)
    return img * (1 - amt / 2 + amt * n)[..., None] * grain(size, seed + 99)[..., None]


# ---------------------------------------------------------------- textures
S = 1024

# beige stone wall with chipped plaster revealing orange brick
wall, _ = blocks(S, 6, 230, 380, 1, (0.86, 0.76, 0.62), var=0.07,
                 mortar=(0.56, 0.47, 0.39), mw=5, bevel=10)
brick, _ = blocks(S, 24, 70, 110, 2, (0.74, 0.44, 0.29), var=0.1,
                  mortar=(0.50, 0.33, 0.24), mw=3, bevel=4)
wall = chips(wall, S, 3, 0.66, (0, 0, 0), (0.52, 0.38, 0.28), inner=brick)
wall = paint(wall, S, 4, amt=0.22)
save(wall, "stone_wall.png")

# interior plaster (warm, softer)
pl, _ = blocks(S, 6, 230, 380, 11, (0.70, 0.58, 0.45), var=0.05,
               mortar=(0.55, 0.44, 0.34), mw=3, bevel=6, hi=0.06, lo=0.08)
pl = chips(pl, S, 12, 0.7, (0, 0, 0), (0.48, 0.36, 0.27), inner=brick * 0.9)
save(paint(pl, S, 13, amt=0.25), "plaster.png")

# red painted band
red, _ = blocks(S, 6, 260, 420, 21, (0.72, 0.13, 0.11), var=0.06,
                mortar=(0.40, 0.07, 0.06), mw=5, bevel=10, hi=0.15, lo=0.2)
wear = wall * 0.95
red = chips(red, S, 22, 0.70, (0, 0, 0), (0.45, 0.10, 0.08), inner=wear)
save(paint(red, S, 23, amt=0.2), "red_band.png")

# gray cap / plinth stone
gs, _ = blocks(S, 4, 300, 480, 31, (0.42, 0.43, 0.46), var=0.1,
               mortar=(0.20, 0.20, 0.22), mw=5, bevel=14, hi=0.18, lo=0.2)
gs = chips(gs, S, 32, 0.72, (0.52, 0.52, 0.54), (0.28, 0.28, 0.30), scale=5)
save(paint(gs, S, 33, amt=0.25), "stone_gray.png")

# roof concrete slabs
rf, _ = blocks(S, 4, 250, 262, 41, (0.30, 0.31, 0.33), var=0.08,
               mortar=(0.16, 0.16, 0.18), mw=6, bevel=10, hi=0.08, lo=0.1)
stain = fbm(S, 3, 4, 42)
rf *= (0.75 + 0.35 * stain)[..., None]
save(paint(rf, S, 43, amt=0.2), "roof_concrete.png")

# interior floor tiles
fl, _ = blocks(S, 4, 256, 256, 51, (0.80, 0.68, 0.52), var=0.06,
               mortar=(0.45, 0.37, 0.29), mw=5, bevel=10, hi=0.1, lo=0.12)
fl = chips(fl, S, 52, 0.76, (0.66, 0.54, 0.40), (0.52, 0.42, 0.32), scale=6)
save(paint(fl, S, 53, amt=0.25), "floor_tiles.png")

# paver stone (per slab, untiled look)
pv = np.ones((S, S, 3)) * np.array([0.80, 0.71, 0.58])
pv = paint(pv, S, 61, amt=0.35, cells=4)
pvi = Image.fromarray((np.clip(pv, 0, 1) * 255).astype(np.uint8))
d = ImageDraw.Draw(pvi)
rng = np.random.default_rng(62)
for _ in range(5):  # hairline cracks
    x, y = rng.integers(0, S, 2)
    pts = [(int(x), int(y))]
    for _ in range(6):
        x += rng.integers(-40, 40); y += rng.integers(10, 50)
        pts.append((int(x), int(y)))
    d.line(pts, fill=(160, 138, 112), width=2)
save(np.asarray(pvi) / 255.0, "paver.png")

# soil
so = np.ones((S, S, 3)) * np.array([0.27, 0.21, 0.16])
save(paint(so, S, 71, amt=0.5, cells=8), "soil.png")

# rock
rk = np.ones((512, 512, 3)) * np.array([0.52, 0.52, 0.54])
save(paint(rk, 512, 72, amt=0.45, cells=4), "rock.png")

# wood planks (horizontal)
W = 1024
yy, xx = np.mgrid[0:W, 0:W]
wd, wm = blocks(W, 5, 600, 1024, 81, (0.56, 0.36, 0.20), var=0.12,
                mortar=(0.18, 0.11, 0.06), mw=7, bevel=10, hi=0.1, lo=0.2)
warp = fbm(W, 4, 3, 82)
gr = 0.5 + 0.5 * np.sin(yy * 0.35 + warp * 18 + np.sin(xx * 0.01) * 2)
wd *= (0.85 + 0.2 * gr)[..., None]
knots = fbm(W, 8, 2, 83) > 0.78
wd[knots] *= 0.7
save(paint(wd, W, 84, amt=0.15), "wood.png")

# dark metal
mt = np.ones((512, 512, 3)) * np.array([0.20, 0.21, 0.23])
mt = paint(mt, 512, 91, amt=0.3, cells=4)
mti = Image.fromarray((np.clip(mt, 0, 1) * 255).astype(np.uint8)); d = ImageDraw.Draw(mti)
for _ in range(60):
    x, y = rng.integers(0, 512, 2); l = rng.integers(5, 25)
    d.line([(int(x), int(y)), (int(x + l), int(y + rng.integers(-4, 4)))], fill=(95, 95, 100), width=1)
save(np.asarray(mti) / 255.0, "metal_dark.png")

# light metal (AC units)
ml = np.ones((512, 512, 3)) * np.array([0.62, 0.62, 0.60])
save(paint(ml, 512, 92, amt=0.25, cells=4), "metal_light.png")

# red painted metal
mr = np.ones((512, 512, 3)) * np.array([0.74, 0.12, 0.10])
mr = paint(mr, 512, 93, amt=0.25, cells=4)
mri = Image.fromarray((np.clip(mr, 0, 1) * 255).astype(np.uint8)); d = ImageDraw.Draw(mri)
for _ in range(50):
    x, y = rng.integers(0, 512, 2); l = rng.integers(4, 18)
    d.line([(int(x), int(y)), (int(x + l), int(y + rng.integers(-3, 3)))], fill=(215, 150, 140), width=1)
save(np.asarray(mri) / 255.0, "metal_red.png")

# cardboard with tape
cb = np.ones((512, 512, 3)) * np.array([0.76, 0.56, 0.33])
cb = paint(cb, 512, 101, amt=0.2, cells=4)
cb[:, 225:287] = cb[:, 225:287] * 0.8 + np.array([0.85, 0.72, 0.50]) * 0.35
edge = np.zeros((512, 512)); edge[:14] = 1; edge[-14:] = 1; edge[:, :14] = 1; edge[:, -14:] = 1
cb *= (1 - 0.25 * edge)[..., None]
save(cb, "cardboard.png")

# tire: v across profile, u around circumference
T = 512
tr = np.ones((T, T, 3)) * 0.11
v = np.arange(T)[:, None] / T; u = np.arange(T)[None, :] / T
tread = (v > 0.3) & (v < 0.7)
lug = ((np.floor(u * 48 + np.where(v < 0.5, 0, 0.5)) % 2) == 0) & tread
groove = tread & ((np.abs(v - 0.5) < 0.025) | ~lug & ((v < 0.35) | (v > 0.65)))
tr = np.where(groove[..., None], 0.05, tr)
tr = np.where((lug & ~groove)[..., None], 0.14, tr)
ring = (np.abs(v - 0.15) < 0.01) | (np.abs(v - 0.85) < 0.01)
tr = np.where(ring[..., None], 0.18, tr)
save(paint(np.broadcast_to(tr, (T, T, 3)).copy(), T, 111, amt=0.2), "tire.png")

# barrel stripes (v = height, image row 0 = top)
B = 512
bv = 1 - np.arange(B)[:, None] / B  # 0 bottom -> 1 top
col = np.where(((bv > 0.28) & (bv < 0.42)) | ((bv > 0.60) & (bv < 0.74)),
               1.0, 0.0)
redc = np.array([0.72, 0.10, 0.08]); whc = np.array([0.90, 0.88, 0.84])
br = col[..., None] * whc + (1 - col[..., None]) * redc
br = np.broadcast_to(br, (B, B, 3)).copy()
rib = (np.abs(bv - 0.33) < 0.006) | (np.abs(bv - 0.67) < 0.006) | (bv < 0.03) | (bv > 0.97)
br[np.broadcast_to(rib, (B, B))] *= 0.6
br = chips(br, B, 112, 0.74, (0.35, 0.33, 0.32), (0.25, 0.08, 0.06), scale=4, rim_w=0.02)
save(paint(br, B, 113, amt=0.2), "barrel.png")

# glass
gl = np.ones((256, 256, 3)) * np.array([0.10, 0.13, 0.17])
gy, gx = np.mgrid[0:256, 0:256]
streak = np.abs((gx + gy) - 200) < 18
gl[streak] += 0.12
save(gl, "glass.png")

# lamp glow
save(np.ones((64, 64, 3)) * np.array([1.0, 0.78, 0.42]), "lamp_glow.png")

# vent slats
vt = np.ones((256, 256, 3)) * 0.22
for i in range(6):
    y0 = 20 + i * 38
    vt[y0:y0 + 18] = 0.07
    vt[y0 + 18:y0 + 22] = 0.35
save(paint(vt, 256, 121, amt=0.15), "vent.png")

# fan grille
F = 512
fy, fx = np.mgrid[0:F, 0:F] - F / 2 + 0.5
rr = np.sqrt(fx ** 2 + fy ** 2) / (F / 2)
ang = np.arctan2(fy, fx)
fan = np.ones((F, F, 3)) * np.array([0.60, 0.60, 0.58])
hole = rr < 0.84
fan[hole] = 0.08
blade = hole & (rr > 0.12) & (np.sin(ang * 5 + rr * 2.5) > 0.35)
fan[blade] = 0.30
rings = hole & (np.abs((rr * 9) % 1 - 0.5) > 0.44)
fan[rings] = 0.55
spokes = hole & (np.abs(np.sin(ang * 2)) < 0.03)
fan[spokes] = 0.55
fan[rr < 0.12] = 0.4
fan[(rr > 0.84) & (rr < 0.88)] = 0.35
edge = (np.abs(fx) > F / 2 - 14) | (np.abs(fy) > F / 2 - 14)
fan[edge] *= 0.7
save(paint(fan, F, 131, amt=0.15), "fan.png")

# leaves: two variants side by side (u 0..0.5 dark, 0.5..1 light)
L = 256
lv = np.zeros((L, 2 * L, 3))
ly = 1 - np.arange(L)[:, None] / L  # 0 base -> 1 tip
for k, (c0, c1) in enumerate([((0.13, 0.30, 0.08), (0.34, 0.56, 0.14)),
                              ((0.22, 0.42, 0.10), (0.52, 0.72, 0.20))]):
    lx = np.arange(L)[None, :] / L
    t = np.clip(ly, 0, 1)
    colr = np.array(c0) * (1 - t[..., None]) + np.array(c1) * t[..., None]
    colr = np.broadcast_to(colr, (L, L, 3)).copy()
    mid = np.abs(lx - 0.5) < 0.02
    colr[np.broadcast_to(mid, (L, L))] *= 1.35
    veins = (np.abs(((lx - 0.5) * 6 + ly * 3) % 1 - 0.5) < 0.04) & ~mid
    colr[np.broadcast_to(veins, (L, L))] *= 1.12
    rim = np.abs(lx - 0.5) > 0.43
    colr[np.broadcast_to(rim, (L, L))] *= 0.8
    lv[:, k * L:(k + 1) * L] = colr
save(lv, "leaf.png")

# grass blades
G = 256
gt = 1 - np.arange(G)[:, None] / G
gcol = np.array([0.16, 0.34, 0.09]) * (1 - gt[..., None]) + np.array([0.58, 0.76, 0.24]) * gt[..., None]
gcol = np.broadcast_to(gcol, (G, 64, 3)).copy()
gcol[:, 30:34] *= 1.2
save(gcol, "grass.png")


# ---------------------------------------------------------------- sign
def wrench_img(length, thick, color):
    """Open-end wrench pointing right, RGBA."""
    Wd, Hd = length + 40, int(thick * 3.4)
    im = Image.new("L", (Wd, Hd), 0); dr = ImageDraw.Draw(im)
    cy = Hd // 2
    hr = int(thick * 1.5)
    dr.rounded_rectangle([hr, cy - thick // 2, Wd - hr - 10, cy + thick // 2], radius=thick // 2, fill=255)
    hx = Wd - hr - 12
    dr.ellipse([hx - hr, cy - hr, hx + hr, cy + hr], fill=255)
    dr.polygon([(hx - int(hr * 0.2), cy - int(hr * 0.45)), (hx + hr + 4, cy - int(hr * 0.65)),
                (hx + hr + 4, cy + int(hr * 0.65)), (hx - int(hr * 0.2), cy + int(hr * 0.45))], fill=0)
    r2 = int(thick * 1.05)
    dr.ellipse([hr - r2, cy - r2, hr + r2, cy + r2], fill=255)
    r3 = int(thick * 0.5)
    dr.ellipse([hr - r3, cy - r3, hr + r3, cy + r3], fill=0)
    rgba = Image.new("RGBA", im.size, color + (0,)); rgba.putalpha(im)
    return rgba


SW, SH = 2048, 420
base = np.ones((SH, SW, 3)) * np.array([0.93, 0.87, 0.74])
n = np.zeros((SH, SW))
nn = fbm(SW, 6, 4, 141)[:SH]
base *= (0.88 + 0.16 * nn)[..., None]
yy, xx = np.mgrid[0:SH, 0:SW]
edgef = np.minimum(np.minimum(xx, SW - xx), np.minimum(yy, SH - yy)) / 60
base *= (0.78 + 0.22 * np.clip(edgef, 0, 1))[..., None]
sign = Image.fromarray((np.clip(base, 0, 1) * 255).astype(np.uint8)).convert("RGBA")
d = ImageDraw.Draw(sign)
ink = (44, 40, 38)
d.rectangle([22, 22, SW - 23, SH - 23], outline=(70, 62, 56), width=6)
for (x, y) in [(46, 46), (SW - 46, 46), (46, SH - 46), (SW - 46, SH - 46)]:
    d.ellipse([x - 12, y - 12, x + 12, y + 12], fill=(120, 110, 100), outline=(60, 55, 50), width=3)
txt = "МАСТЕРСКАЯ"
font = ImageFont.truetype(FONT_HEAVY, 250)
tb = d.textbbox((0, 0), txt, font=font)
tw, th = tb[2] - tb[0], tb[3] - tb[1]
timg = Image.new("RGBA", (tw + 20, th + 40), (0, 0, 0, 0))
ImageDraw.Draw(timg).text((10 - tb[0], 20 - tb[1]), txt, font=font, fill=ink + (255,))
target_w = 1480
timg = timg.resize((target_w, int(timg.height * 1.0)), Image.LANCZOS)
tx = 110; ty = (SH - timg.height) // 2
sign.alpha_composite(timg, (tx, ty))
wr = wrench_img(300, 60, ink).rotate(40, expand=True, resample=Image.BICUBIC)
sign.alpha_composite(wr, (tx + target_w + 40, (SH - wr.height) // 2))
save(np.asarray(sign.convert("RGB")) / 255.0, "sign.png")

# ---------------------------------------------------------------- pegboard with tools
PW, PH = 1024, 512
pb = np.ones((PH, PW, 3)) * np.array([0.60, 0.40, 0.24])
pb *= (0.9 + 0.15 * fbm(PW, 6, 3, 151)[:PH])[..., None]
peg = Image.fromarray((np.clip(pb, 0, 1) * 255).astype(np.uint8)).convert("RGBA")
d = ImageDraw.Draw(peg)
for y in range(24, PH, 34):
    for x in range(24, PW, 34):
        d.ellipse([x - 4, y - 4, x + 4, y + 4], fill=(70, 45, 28))
d.rectangle([0, 0, PW - 1, PH - 1], outline=(80, 52, 30), width=14)
toolc = (58, 60, 66)
xpos = 90
for L in (300, 260, 220, 190):
    w = wrench_img(L, 34, toolc).rotate(90, expand=True, resample=Image.BICUBIC)
    sh = Image.new("RGBA", w.size, (30, 18, 10, 0)); sh.putalpha(w.getchannel("A").point(lambda a: a * 0.5))
    peg.alpha_composite(sh, (xpos + 8, 70 + 8)); peg.alpha_composite(w, (xpos, 70))
    xpos += w.width + 30
# hammer
hx = xpos + 50
d.rectangle([hx + 8, 110, hx + 14, 110], fill=toolc)
d.rounded_rectangle([hx + 30, 120, hx + 52, 420], radius=8, fill=(150, 95, 50))
d.rounded_rectangle([hx - 10, 90, hx + 92, 140], radius=8, fill=toolc)
# screwdrivers
for i, c in enumerate([(190, 40, 30), (230, 170, 40)]):
    sx = hx + 170 + i * 80
    d.rounded_rectangle([sx, 90, sx + 36, 230], radius=12, fill=c)
    d.rectangle([sx + 14, 230, sx + 22, 400], fill=(170, 170, 175))
# pliers
px = hx + 360
d.line([(px, 100), (px + 60, 380)], fill=(170, 40, 30), width=22)
d.line([(px + 110, 100), (px + 60, 380)], fill=(170, 40, 30), width=22)
d.polygon([(px + 40, 360), (px + 80, 360), (px + 70, 470), (px + 50, 470)], fill=toolc)
save(np.asarray(peg.convert("RGB")) / 255.0, "pegboard.png")
print("done")

