"""Procedural textures for the Unity project that do not exist in the art sources:
cracked asphalt tile (+normal map), road, crack/pothole/manhole decals, blob shadow, aim line,
crosshair cursor, UI sprites, and defringed car sprites (pickup / sedan).
python gen_unity_textures.py"""
import os, math, json
import numpy as np
from PIL import Image, ImageDraw, ImageFilter
from scipy import ndimage as nd
from scipy.spatial import cKDTree

ROOT = r"D:\zombi-caaar\unity\Assets\Art"
REF = r"D:\zombi-caaar\referens"
ENV = os.path.join(ROOT, "Textures", "Environment")
SPR = os.path.join(ROOT, "Textures", "Sprites")
UI = os.path.join(ROOT, "Textures", "UI")
VFX = os.path.join(ROOT, "VFX")
for d in (ENV, SPR, UI, VFX):
    os.makedirs(d, exist_ok=True)


def save_rgb(a, path):
    Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8), "RGB").save(path); print("saved", path)


def save_rgba(a, path):
    Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8), "RGBA").save(path); print("saved", path)


# ------------------------------------------------------------------ periodic noise
def pnoise(n, cells, seed):
    """Tileable value noise n x n in [0,1]."""
    g = np.random.default_rng(seed).random((cells, cells))
    t = np.arange(n) * cells / n
    i0 = np.floor(t).astype(int) % cells; i1 = (i0 + 1) % cells
    f = t - np.floor(t); f = f * f * (3 - 2 * f)
    a = g[i0][:, i0]; b = g[i0][:, i1]; c = g[i1][:, i0]; d = g[i1][:, i1]
    fx = f[None, :]; fy = f[:, None]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def pfbm(n, cells, octaves, seed):
    acc = np.zeros((n, n)); amp = 1.0; tot = 0.0
    for o in range(octaves):
        acc += amp * pnoise(n, cells * 2 ** o, seed + 17 * o); tot += amp; amp *= 0.5
    return acc / tot


def voronoi_edges(n, count, seed, warp=0.0, warp_seed=0):
    """Tileable Voronoi: returns (d2 - d1) edge distance in pixels and the cell id per pixel."""
    r = np.random.default_rng(seed)
    pts = r.random((count, 2))
    tiles = np.concatenate([pts + np.array([dx, dy]) for dx in (-1, 0, 1) for dy in (-1, 0, 1)])
    ids = np.tile(np.arange(count), 9)
    yy, xx = np.mgrid[0:n, 0:n] / n
    if warp:
        xx = xx + warp * (pfbm(n, 4, 3, warp_seed) - 0.5)
        yy = yy + warp * (pfbm(n, 4, 3, warp_seed + 5) - 0.5)
    q = np.stack([xx.ravel(), yy.ravel()], 1)
    d, idx = cKDTree(tiles).query(q, k=2)
    edge = ((d[:, 1] - d[:, 0]) * n).reshape(n, n)
    cell = ids[idx[:, 0]].reshape(n, n)
    return edge, cell


# ------------------------------------------------------------------ cracked asphalt (one 6 m ground tile) + normal map
N = 1024
grain = np.random.default_rng(1).random((N, N))
base = np.array([0.50, 0.45, 0.40])
img = np.ones((N, N, 3)) * base
img *= (0.86 + 0.26 * pfbm(N, 3, 4, 2))[..., None]                       # big tonal patches
img *= (0.94 + 0.12 * pfbm(N, 24, 2, 3))[..., None]                      # mid aggregate
img *= (0.93 + 0.14 * grain)[..., None]                                   # fine grain
edge, cell = voronoi_edges(N, 26, 4, warp=0.05, warp_seed=5)             # slabs of broken asphalt
plate = np.random.default_rng(6).uniform(0.9, 1.08, 26)[cell]
img *= plate[..., None]
fine_edge, _ = voronoi_edges(N, 110, 7, warp=0.03, warp_seed=8)          # hairline crazing
width = 2.2 + 2.0 * pfbm(N, 6, 2, 9)
crack = np.clip(1 - edge / width, 0, 1)
hair = np.clip(1 - fine_edge / 1.1, 0, 1) * (pfbm(N, 5, 2, 10) > 0.52)
rim = np.clip(1 - np.abs(edge - width - 1.5) / 1.5, 0, 1) * (1 - crack)
dark = np.array([0.20, 0.17, 0.15])
img = img * (1 - 0.9 * crack[..., None]) + dark * 0.9 * crack[..., None]
img = img * (1 - 0.45 * hair[..., None]) + dark * 0.45 * hair[..., None]
img = img * (1 + 0.10 * rim[..., None])                                   # sun-lit crack lips
stain = np.clip((pfbm(N, 5, 3, 11) - 0.66) * 4, 0, 1)                     # oil / wet stains
img *= (1 - 0.25 * stain)[..., None]
pebbles = (grain > 0.9985)
img[pebbles] = np.array([0.72, 0.66, 0.58])
save_rgb(img, os.path.join(ENV, "T_Asphalt.png"))

h = 1.0 - 0.9 * crack - 0.35 * hair + 0.04 * grain + 0.10 * (plate[..., None][..., 0] - 1.0)
k = 3.0
dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * 0.5 * k
dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * 0.5 * k
nrm = np.dstack([-dx, dy, np.ones_like(h)])
nrm /= np.linalg.norm(nrm, axis=2, keepdims=True)
save_rgb(nrm * 0.5 + 0.5, os.path.join(ENV, "T_Asphalt_N.png"))

# ------------------------------------------------------------------ road (u = along, v = across; 7 m wide, 7 m tile)
R = 512
rd = np.ones((R, R, 3)) * np.array([0.28, 0.27, 0.26])
rd *= (0.88 + 0.22 * pfbm(R, 4, 4, 20))[..., None]
rd *= (0.94 + 0.12 * np.random.default_rng(21).random((R, R)))[..., None]
e2, _ = voronoi_edges(R, 14, 22, warp=0.06, warp_seed=23)
rd *= (1 - 0.5 * np.clip(1 - e2 / 1.6, 0, 1))[..., None]
v = np.arange(R)[:, None] / R; u = np.arange(R)[None, :] / R
paint = np.array([0.86, 0.82, 0.66])
wear = pfbm(R, 16, 2, 24) > 0.38
center = (np.abs(v - 0.5) < 0.012) & ((u % 0.5) < 0.28)
edges = (np.abs(v - 0.075) < 0.009) | (np.abs(v - 0.925) < 0.009)
mask = (center | edges) & wear
rd[np.broadcast_to(mask, (R, R))] = paint * 0.92
curb = (v < 0.035) | (v > 0.965)
rd[np.broadcast_to(curb, (R, R))] *= 0.7
save_rgb(rd, os.path.join(ENV, "T_Road.png"))

# ------------------------------------------------------------------ decals: cracks (2) + manhole
def crack_decal(seed, n=512):
    r = np.random.default_rng(seed)
    im = Image.new("RGBA", (n * 2, n * 2), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    lines = []

    def branch(x, y, ang, length, w, depth):
        pts = [(x, y)]
        for _ in range(int(length / 18)):
            ang += r.uniform(-0.45, 0.45)
            x += math.cos(ang) * 18; y += math.sin(ang) * 18
            pts.append((x, y))
            if depth < 3 and r.random() < 0.10:
                branch(x, y, ang + r.choice([-1, 1]) * r.uniform(0.6, 1.2), length * 0.45, max(2, w * 0.6), depth + 1)
        lines.append((pts, w))

    for k in range(3):
        a0 = r.uniform(0, 2 * math.pi)
        branch(n + math.cos(a0) * 60, n + math.sin(a0) * 60, a0, r.uniform(500, 800), r.uniform(7, 11), 0)
    for pts, w in lines:                                                  # soft dark stain around cracks
        d.line(pts, fill=(40, 32, 26, 60), width=int(w * 5), joint="curve")
    im = im.filter(ImageFilter.GaussianBlur(10)); d = ImageDraw.Draw(im)
    for pts, w in lines:
        d.line([(p[0] + 2, p[1] + 3) for p in pts], fill=(190, 176, 158, 110), width=max(2, int(w * 0.5)), joint="curve")
        d.line(pts, fill=(36, 30, 26, 235), width=int(w), joint="curve")
    im = im.resize((n, n), Image.LANCZOS)
    a = np.asarray(im).astype(float) / 255
    # fade toward the border so the quad edge never shows
    yy, xx = np.mgrid[0:n, 0:n] / n
    fade = np.clip((0.5 - np.maximum(np.abs(xx - 0.5), np.abs(yy - 0.5))) / 0.08, 0, 1)
    a[..., 3] *= fade
    return a


save_rgba(crack_decal(30), os.path.join(ENV, "Decal_Crack1.png"))
save_rgba(crack_decal(31), os.path.join(ENV, "Decal_Crack2.png"))


def pothole_decal(seed, n=512):
    """Dark broken-asphalt hole with a lit rim, pebbles inside and short radiating cracks (concept potholes)."""
    r = np.random.default_rng(seed)
    yy, xx = (np.mgrid[0:n, 0:n] - (n - 1) / 2) / (n / 2)                 # -1..1, y down
    rr = np.hypot(xx, yy); ang = np.arctan2(yy, xx)
    outline = 0.44 + (pfbm(n, 6, 3, seed) - 0.5) * 0.16
    for h in range(2, 6):
        outline = outline + r.uniform(0.02, 0.06) / (h - 1) * np.cos(h * ang + r.uniform(0, 2 * np.pi))
    d = rr - outline                                                      # < 0 inside the hole
    hole = np.clip(-d / 0.03, 0, 1)
    rim = np.clip(1 - np.abs(d - 0.02) / 0.035, 0, 1) * (pfbm(n, 16, 2, seed + 2) > 0.3)
    lit = np.clip(0.5 - 0.5 * (xx * 0.6 + yy * 0.8) / np.maximum(rr, 1e-3), 0, 1)   # rim facing the top-left sun
    depth = np.clip(-d / 0.12, 0, 1)                                      # 0 at the edge .. 1 deep inside
    dark = np.array([0.25, 0.20, 0.16]) * (0.65 + 0.7 * pfbm(n, 12, 3, seed + 1))[..., None]
    rgb = dark * (0.7 + 0.3 * depth[..., None])
    edge = np.array([0.60, 0.53, 0.46]) * (0.65 + 0.55 * lit)[..., None]
    rgb = rgb * (1 - rim[..., None]) + edge * rim[..., None]
    a = np.maximum(hole * 0.88, rim * 0.85)
    for _ in range(int(r.integers(18, 30))):                              # gravel in the hole
        a0 = r.uniform(0, 2 * np.pi); rad = r.uniform(0, 0.33)
        s = r.uniform(0.012, 0.03)
        m = np.clip(1 - np.hypot(xx - np.cos(a0) * rad, yy - np.sin(a0) * rad) / s, 0, 1) ** 0.5 * hole
        rgb = rgb * (1 - m[..., None]) + np.array([0.52, 0.47, 0.42]) * r.uniform(0.8, 1.15) * m[..., None]
    im = Image.new("RGBA", (n * 2, n * 2), (0, 0, 0, 0)); dr = ImageDraw.Draw(im)
    for _ in range(int(r.integers(4, 7))):                               # cracks running out of the rim
        a0 = r.uniform(0, 2 * np.pi)
        rad0 = float(outline[int(n / 2 + math.sin(a0) * n * 0.2), int(n / 2 + math.cos(a0) * n * 0.2)])
        x, y, a1 = n + math.cos(a0) * rad0 * n, n + math.sin(a0) * rad0 * n, a0
        pts = [(x, y)]
        for _ in range(int(r.integers(6, 12))):
            a1 += r.uniform(-0.5, 0.5); x += math.cos(a1) * 16; y += math.sin(a1) * 16
            pts.append((x, y))
        dr.line(pts, fill=(34, 28, 24, 225), width=int(r.integers(4, 8)), joint="curve")
    cr = np.asarray(im.resize((n, n), Image.LANCZOS)).astype(float) / 255
    ca = cr[..., 3] * (1 - hole)
    rgb = rgb * (1 - ca[..., None]) + cr[..., :3] * ca[..., None]
    a = np.maximum(a, ca)
    fade = np.clip((0.5 - np.maximum(np.abs(xx / 2), np.abs(yy / 2))) / 0.06, 0, 1)   # quad edge never shows
    return np.dstack([rgb, a * fade])


save_rgba(pothole_decal(50), os.path.join(ENV, "Decal_Pothole1.png"))
save_rgba(pothole_decal(51), os.path.join(ENV, "Decal_Pothole2.png"))

M = 256
yy, xx = (np.mgrid[0:M, 0:M] - (M - 1) / 2) / (M / 2)
rr = np.hypot(xx, yy); ang = np.arctan2(yy, xx)
mh = np.zeros((M, M, 4))
ring = (rr < 0.98) & (rr >= 0.86)
cover = rr < 0.86
mh[ring, :3] = np.array([0.50, 0.47, 0.43]) * (0.9 + 0.2 * np.random.default_rng(40).random(ring.sum()))[:, None]
mh[ring, 3] = 1
iron = np.array([0.24, 0.23, 0.23])
c = np.ones((M, M, 3)) * iron
c *= (0.85 + 0.3 * pfbm(M, 6, 3, 41))[..., None]
pattern = ((np.abs(((xx + yy) * 7) % 1 - 0.5) < 0.12) | (np.abs(((xx - yy) * 7) % 1 - 0.5) < 0.12)) & (rr < 0.66)
c[pattern] *= 1.45
c[(np.abs(rr - 0.74) < 0.035)] *= 1.6                                     # raised ring
c[(np.abs(rr - 0.84) < 0.02)] *= 0.55                                     # gap to the frame
rust = pfbm(M, 8, 3, 42) > 0.64
c[rust] = c[rust] * 0.5 + np.array([0.46, 0.26, 0.14]) * 0.5
c *= (1 + 0.25 * np.clip(-(xx + yy) * 0.5, -1, 1))[..., None]            # painted top-left light
mh[cover, :3] = c[cover]; mh[cover, 3] = 1
aa = np.clip((0.98 - rr) * M / 2, 0, 1)
mh[..., 3] = np.minimum(mh[..., 3], aa)
save_rgba(mh, os.path.join(ENV, "Decal_Manhole.png"))

# ------------------------------------------------------------------ blob shadow (ellipse)
S_W, S_H = 256, 128
yy, xx = np.mgrid[0:S_H, 0:S_W]
ex = (xx - (S_W - 1) / 2) / (S_W / 2); ey = (yy - (S_H - 1) / 2) / (S_H / 2)
e = np.clip(1 - (ex ** 2 + ey ** 2), 0, 1)
sh = np.zeros((S_H, S_W, 4)); sh[..., 3] = 0.72 * e ** 1.4
save_rgba(sh, os.path.join(ENV, "T_ShadowBlob.png"))

# ------------------------------------------------------------------ aim line (u = along, fades out) and bullet glow
AL = np.zeros((32, 256, 4))
u = np.linspace(0, 1, 256)[None, :]; vv = np.linspace(-1, 1, 32)[:, None]
AL[..., :3] = 1.0
AL[..., 3] = 0.9 * (1 - u) ** 0.7 * np.clip(1 - np.abs(vv) ** 3, 0, 1)
save_rgba(AL, os.path.join(VFX, "T_AimLine.png"))
G = 64
gy, gx = (np.mgrid[0:G, 0:G] - (G - 1) / 2) / (G / 2)
gr = np.clip(1 - np.hypot(gx, gy), 0, 1)
glow = np.zeros((G, G, 4)); glow[..., :3] = 1.0; glow[..., 3] = gr ** 1.8
save_rgba(glow, os.path.join(VFX, "T_BulletGlow.png"))

# ------------------------------------------------------------------ crosshair cursor (64 px, hotspot = centre)
C = 64 * 4
im = Image.new("RGBA", (C, C), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
cx = cy = C / 2
for col, w in (((20, 20, 24, 230), 22), ((255, 244, 220, 255), 12)):
    d.ellipse([cx - 76, cy - 76, cx + 76, cy + 76], outline=col, width=w)
    for sx, sy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        d.line([(cx + sx * 50, cy + sy * 50), (cx + sx * 116, cy + sy * 116)], fill=col, width=w)
d.ellipse([cx - 14, cy - 14, cx + 14, cy + 14], fill=(20, 20, 24, 230))
d.ellipse([cx - 8, cy - 8, cx + 8, cy + 8], fill=(255, 120, 40, 255))
im.resize((64, 64), Image.LANCZOS).save(os.path.join(UI, "UI_Crosshair.png")); print("saved crosshair")


# ------------------------------------------------------------------ UI sprites
def rounded(w, h, r, scale=4):
    im = Image.new("RGBA", (w * scale, h * scale), (255, 255, 255, 0))
    ImageDraw.Draw(im).rounded_rectangle([0, 0, w * scale - 1, h * scale - 1], radius=r * scale, fill=(255, 255, 255, 255))
    return im.resize((w, h), Image.LANCZOS)


rounded(64, 64, 18).save(os.path.join(UI, "UI_Panel.png"))
rounded(64, 32, 12).save(os.path.join(UI, "UI_Bar.png"))
rounded(96, 96, 28).save(os.path.join(UI, "UI_Button.png"))

GS = 512
im = Image.new("RGBA", (GS, GS), (0, 0, 0, 0)); d = ImageDraw.Draw(im); c0 = GS / 2
for k in range(8):
    a = k * math.pi / 4
    pts = []
    for (rad, da) in ((150, -0.20), (225, -0.13), (225, 0.13), (150, 0.20)):
        pts.append((c0 + rad * math.cos(a + da), c0 + rad * math.sin(a + da)))
    d.polygon(pts, fill=(255, 255, 255, 255))
d.ellipse([c0 - 165, c0 - 165, c0 + 165, c0 + 165], fill=(255, 255, 255, 255))
d.ellipse([c0 - 70, c0 - 70, c0 + 70, c0 + 70], fill=(0, 0, 0, 0))
im.resize((128, 128), Image.LANCZOS).save(os.path.join(UI, "UI_IconGear.png"))

im = Image.new("RGBA", (512, 512), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
for i, x in enumerate((140, 256, 372)):
    d.rounded_rectangle([x - 44, 190, x + 44, 440], radius=12, fill=(70, 50, 20, 255))
    d.rounded_rectangle([x - 36, 198, x + 36, 432], radius=10, fill=(236, 186, 64, 255))
    d.rectangle([x - 20, 206, x - 6, 424], fill=(255, 226, 140, 255))
    d.pieslice([x - 44, 70, x + 44, 290], 180, 360, fill=(70, 40, 20, 255))
    d.pieslice([x - 36, 80, x + 36, 282], 180, 360, fill=(214, 120, 60, 255))
    d.rectangle([x - 44, 176, x + 44, 196], fill=(70, 50, 20, 255))
im.resize((128, 128), Image.LANCZOS).save(os.path.join(UI, "UI_IconAmmo.png"))

P = 512
im = Image.new("RGBA", (P, P), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
d.ellipse([0, 0, P - 1, P - 1], fill=(38, 48, 66, 255))
d.ellipse([16, 16, P - 17, P - 17], fill=(62, 84, 112, 255))
d.pieslice([90, 330, 422, 640], 180, 360, fill=(196, 44, 38, 255))          # shoulders
d.ellipse([160, 150, 352, 370], fill=(236, 188, 150, 255))                 # face
d.pieslice([140, 96, 372, 300], 180, 360, fill=(214, 40, 36, 255))         # cap
d.rounded_rectangle([230, 190, 400, 222], radius=14, fill=(180, 30, 28, 255))  # visor
d.ellipse([214, 240, 238, 268], fill=(40, 30, 28, 255)); d.ellipse([276, 240, 300, 268], fill=(40, 30, 28, 255))
mask = Image.new("L", (P, P), 0); ImageDraw.Draw(mask).ellipse([0, 0, P - 1, P - 1], fill=255)
im.putalpha(Image.fromarray(np.minimum(np.asarray(im)[..., 3], np.asarray(mask))))
im.resize((256, 256), Image.LANCZOS).save(os.path.join(UI, "UI_Portrait.png"))
print("saved UI sprites")


# ------------------------------------------------------------------ car sprites: defringe + crop + resize
def fill_rgb(rgb, solid):
    _, (iy, ix) = nd.distance_transform_edt(~solid, return_indices=True)
    return rgb[iy, ix]


def car_sprite(src, dst, max_side=1024):
    a = np.asarray(Image.open(os.path.join(REF, src)).convert("RGBA")).astype(np.float64) / 255
    alpha = a[..., 3]; rgb = a[..., :3]
    lab, n = nd.label(alpha > 0.5)
    sizes = nd.sum(np.ones_like(alpha), lab, range(1, n + 1))
    main = lab == (1 + int(np.argmax(sizes)))                              # the car, no stray specks
    keep = nd.binary_dilation(main, iterations=2)
    alpha = alpha * keep
    a_er = nd.grey_erosion(alpha, size=(3, 3))                             # 1 px alpha erosion (halo ring gone)
    a_er = nd.grey_erosion(a_er, size=(2, 2))                              # + ~0.5 px more on the soft side
    core = nd.grey_erosion(alpha, size=(5, 5)) >= 0.9
    rgb = fill_rgb(rgb, core)                                              # edge colour <- nearest interior colour
    ys, xs = np.nonzero(a_er > 0.5)
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    pad = 6
    y0, x0 = max(0, y0 - pad), max(0, x0 - pad); y1, x1 = min(a.shape[0], y1 + pad), min(a.shape[1], x1 + pad)
    rgb, a_er = rgb[y0:y1, x0:x1], a_er[y0:y1, x0:x1]
    s = max_side / max(rgb.shape[:2])
    W, H = int(round(rgb.shape[1] * s)), int(round(rgb.shape[0] * s))
    ri = Image.fromarray((rgb * 255).astype(np.uint8)).resize((W, H), Image.LANCZOS)
    ai = Image.fromarray((np.clip(a_er / 0.95, 0, 1) * 255).astype(np.uint8)).resize((W, H), Image.BICUBIC)
    out = ri.convert("RGBA"); out.putalpha(ai)
    # Block compression (DXT/ETC2/ASTC) needs sizes divisible by 4: pad transparent rows at the bottom, so pixel
    # coordinates measured from the top (wheel contacts in PrefabBuilder) stay valid. RGB repeats the last row.
    H4 = (H + 3) // 4 * 4
    if H4 != H:
        padded = Image.new("RGBA", (W, H4))
        padded.paste(out, (0, 0))
        last = np.asarray(out)[-1:].copy(); last[..., 3] = 0
        padded.paste(Image.fromarray(np.repeat(last, H4 - H, axis=0), "RGBA"), (0, H))
        out, H = padded, H4
    out.save(os.path.join(SPR, dst)); print("saved", dst, (W, H))
    return W, H


info = {"Car_Pickup": car_sprite("pickup.png", "Car_Pickup.png"), "Car_Sedan": car_sprite("sedan.png", "Car_Sedan.png")}
json.dump(info, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "sprite_sizes.json"), "w"))
