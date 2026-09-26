"""Compose a prop-sheet presentation.
python compose_props.py "<title>" <renders_dir> <stats.json> <out.png>"""
import sys, os, json
from PIL import Image, ImageDraw, ImageFont

TITLE, RD, STATS, OUT = sys.argv[1:5]
EXTRA = sys.argv[5] if len(sys.argv) > 5 else "Общий атлас + эмиссия, 1 unit = 1 м"
FB = r"C:\Windows\Fonts\bahnschrift.ttf"
BG, PANEL, LINE = (24, 26, 30), (32, 35, 40), (60, 64, 72)
TXT, SUB, ACC = (236, 232, 224), (150, 152, 160), (230, 110, 30)
stats = json.load(open(STATS, encoding="utf8"))


def font(sz, bold=True):
    f = ImageFont.truetype(FB, sz)
    try:
        f.set_variation_by_name("Bold" if bold else "Regular")
    except Exception:
        pass
    return f


def load(name):
    im = Image.open(os.path.join(RD, name + ".png")).convert("RGBA")
    bb = im.getbbox()
    return im.crop(bb) if bb else im


def fit(im, aw, ah):
    s = min(aw / im.width, ah / im.height)
    return im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))), Image.LANCZOS)


W = 3200
names = list(stats.keys())
cols = min(6, max(4, len(names)))
card_w = (W - 120 - (cols - 1) * 24) // cols
card_h = card_w + 130
rows = (len(names) + cols - 1) // cols
H = 270 + 900 + 28 + 620 + 28 + 70 + rows * (card_h + 24) + 40
sheet = Image.new("RGB", (W, H), BG); d = ImageDraw.Draw(sheet)


def panel(box, title, im=None, pad=24):
    x0, y0, x1, y1 = box
    d.rounded_rectangle(box, radius=18, fill=PANEL, outline=LINE, width=2)
    d.text((x0 + 24, y0 + 16), title, font=font(32), fill=TXT)
    if im is not None:
        im = fit(im, x1 - x0 - 2 * pad, y1 - y0 - 70 - pad)
        sheet.paste(im, (x0 + (x1 - x0 - im.width) // 2, y0 + 62 + (y1 - y0 - 70 - pad - im.height) // 2), im)


tot = sum(v["tris"] for v in stats.values())
tot_grass = sum(t for v in stats.values() for k, t in v.get("part_tris", {}).items() if k.endswith("_Grass"))
mats = sorted({m for v in stats.values() for m in v["materials"]})
d.text((60, 40), TITLE, font=font(88), fill=TXT)
d.text((64, 148), "HAND-PAINTED LOW-POLY  ·  BLENDER  >  UNITY  ·  FBX на каждую модель", font=font(34), fill=SUB)
d.rectangle([60, 206, 250, 214], fill=ACC)
info = [(f"Моделей: {len(stats)}   ·   трисов: {tot - tot_grass:,}" + (f" + трава {tot_grass:,}" if tot_grass else "")).replace(",", " "),
        f"Материалы: {', '.join(mats)}",
        EXTRA]
y = 40
for s in info:
    d.ellipse([1700, y + 15, 1712, y + 27], fill=ACC); d.text((1730, y), s, font=font(30, False), fill=TXT); y += 46

M, G = 60, 28
y0 = 250
panel((M, y0, 2000, y0 + 900), "ОСНОВНОЙ ВИД", load("hero"))
panel((2000 + G, y0, W - M, y0 + 900), "ИГРОВАЯ КАМЕРА ~50°", load("game"))
y1 = y0 + 900 + G
half = (W - 2 * M - G) // 2
panel((M, y1, M + half, y1 + 620), "CLAY", load("clay"))
panel((M + half + G, y1, W - M, y1 + 620), "КАРКАС (WIREFRAME)", load("wire"))
y2 = y1 + 620 + G
d.text((M, y2 + 10), "СПИСОК МОДЕЛЕЙ", font=font(40), fill=TXT)
y2 += 70
for i, n in enumerate(names):
    r, c = divmod(i, cols)
    x0 = M + c * (card_w + 24); yy = y2 + r * (card_h + 24)
    d.rounded_rectangle((x0, yy, x0 + card_w, yy + card_h), radius=16, fill=PANEL, outline=LINE, width=2)
    th = fit(load("thumb_" + n), card_w - 40, card_w - 40)
    sheet.paste(th, (x0 + (card_w - th.width) // 2, yy + 20 + (card_w - 40 - th.height) // 2), th)
    v = stats[n]
    d.text((x0 + 20, yy + card_w - 6), n, font=font(34), fill=TXT)
    grass = sum(t for k, t in v.get('part_tris', {}).items() if k.endswith('_Grass'))
    line = f"{v['tris'] - grass} трисов" + (f" (+{grass} трава)" if grass else "") + f" · частей: {len(v['parts'])}"
    d.text((x0 + 20, yy + card_w + 40), line, font=font(26, False), fill=ACC)
    d.text((x0 + 20, yy + card_w + 76), f"models/{v['group']}/{n}.fbx", font=font(22, False), fill=SUB)
sheet.save(OUT); print("sheet", OUT)
