"""Reference vs result sheet for Nature v2.
python compose_nature2.py <renders_dir> <stats.json> <out.png>"""
import sys, os, json
from PIL import Image, ImageDraw, ImageFont

RD, STATS, OUT = sys.argv[1:4]
REF = r"D:\zombi-caaar\referens\priroda2.png"
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


def load(p):
    im = Image.open(p).convert("RGBA")
    bb = im.getbbox()
    return im.crop(bb) if bb else im


def fit(im, aw, ah):
    s = min(aw / im.width, ah / im.height)
    return im.resize((max(1, int(im.width * s)), max(1, int(im.height * s))), Image.LANCZOS)


W, M, G = 3400, 60, 28
names = list(stats.keys())
cols = 5; card_w = (W - 2 * M - (cols - 1) * 24) // cols; card_h = int(card_w * 0.78) + 120
rows = (len(names) + cols - 1) // cols
TOP = 250; PH = 1250
H = TOP + PH + G + 70 + rows * (card_h + 24) + 40
sheet = Image.new("RGB", (W, H), BG); d = ImageDraw.Draw(sheet)


def panel(box, title, im=None, pad=24, bg=None):
    x0, y0, x1, y1 = box
    d.rounded_rectangle(box, radius=18, fill=PANEL, outline=LINE, width=2)
    d.text((x0 + 24, y0 + 16), title, font=font(32), fill=TXT)
    if im is not None:
        im = fit(im, x1 - x0 - 2 * pad, y1 - y0 - 70 - pad)
        px, py = x0 + (x1 - x0 - im.width) // 2, y0 + 62 + (y1 - y0 - 70 - pad - im.height) // 2
        if bg:
            d.rectangle([px, py, px + im.width, py + im.height], fill=bg)
        sheet.paste(im, (px, py), im)


tot = sum(v["tris"] for v in stats.values())
mats = sorted({m for v in stats.values() for m in v["materials"]})
d.text((M, 40), "ПРИРОДА v2 — РЕФЕРЕНС / РЕЗУЛЬТАТ", font=font(84), fill=TXT)
d.text((M + 4, 146), "Карточки с альфой (Alpha Cutout 0.5) · камни 3D · декали мостовой  ·  BLENDER  >  UNITY",
       font=font(32), fill=SUB)
d.rectangle([M, 204, M + 190, 212], fill=ACC)
info = [f"Моделей: {len(stats)}   ·   трисов: {tot:,}".replace(",", " "),
        f"Материалов: {len(mats)}",
        ", ".join(mats)]
y = 40
for s in info:
    d.ellipse([2150, y + 15, 2162, y + 27], fill=ACC); d.text((2180, y), s, font=font(28, False), fill=TXT); y += 44

half = (W - 2 * M - G) // 2
panel((M, TOP, M + half, TOP + PH), "РЕФЕРЕНС · priroda2.png", Image.open(REF).convert("RGBA"), bg=(14, 15, 18))
rx = M + half + G
hh = int((PH - G) * 0.62)
panel((rx, TOP, W - M, TOP + hh), "РЕЗУЛЬТАТ · ОСНОВНОЙ ВИД", load(os.path.join(RD, "hero.png")), pad=12)
panel((rx, TOP + hh + G, W - M, TOP + PH), "РЕЗУЛЬТАТ · ИГРОВАЯ КАМЕРА ~50°", load(os.path.join(RD, "game.png")), pad=12)

y2 = TOP + PH + G
d.text((M, y2 + 10), "СПИСОК МОДЕЛЕЙ", font=font(40), fill=TXT)
y2 += 70
th_h = card_h - 120
for i, n in enumerate(names):
    r, c = divmod(i, cols)
    x0 = M + c * (card_w + 24); yy = y2 + r * (card_h + 24)
    d.rounded_rectangle((x0, yy, x0 + card_w, yy + card_h), radius=16, fill=PANEL, outline=LINE, width=2)
    th = fit(load(os.path.join(RD, "thumb_" + n + ".png")), card_w - 40, th_h - 30)
    sheet.paste(th, (x0 + (card_w - th.width) // 2, yy + 15 + (th_h - 30 - th.height) // 2), th)
    v = stats[n]
    d.text((x0 + 20, yy + th_h - 4), n, font=font(34), fill=TXT)
    d.text((x0 + 20, yy + th_h + 40), f"{v['tris']} трисов · {', '.join(v['materials'])}", font=font(24, False), fill=ACC)
    d.text((x0 + 20, yy + th_h + 74), f"models/{v['group']}/{n}.fbx", font=font(22, False), fill=SUB)
sheet.save(OUT); print("sheet", OUT, sheet.size)
