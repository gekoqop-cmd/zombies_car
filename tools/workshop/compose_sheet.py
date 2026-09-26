"""Composes rendered views into a presentation sheet.
Usage: python compose_sheet.py <renders_dir> <out.png>
"""
import sys, os
from PIL import Image, ImageDraw, ImageFont

RD, OUT = sys.argv[1], sys.argv[2]
STATS = r"D:\zombi-caaar\models\_stats.txt"
FB = r"C:\Windows\Fonts\bahnschrift.ttf"
W, H = 3200, 2240
BG, PANEL, LINE = (24, 26, 30), (32, 35, 40), (60, 64, 72)
TXT, SUB, ACC = (236, 232, 224), (150, 152, 160), (205, 60, 45)

sheet = Image.new("RGB", (W, H), BG)
d = ImageDraw.Draw(sheet)


def font(sz, bold=True):
    f = ImageFont.truetype(FB, sz)
    try:
        f.set_variation_by_name("Bold" if bold else "Regular")
    except Exception:
        pass
    return f


def panel(box, title, img_name, pad=24):
    x0, y0, x1, y1 = box
    d.rounded_rectangle(box, radius=18, fill=PANEL, outline=LINE, width=2)
    d.text((x0 + 26, y0 + 18), title, font=font(34), fill=TXT)
    im = Image.open(os.path.join(RD, img_name + ".png")).convert("RGBA")
    bb = im.getbbox()
    if bb:
        im = im.crop(bb)
    aw, ah = x1 - x0 - 2 * pad, y1 - y0 - 70 - pad
    s = min(aw / im.width, ah / im.height)
    im = im.resize((int(im.width * s), int(im.height * s)), Image.LANCZOS)
    sheet.paste(im, (x0 + (x1 - x0 - im.width) // 2, y0 + 64 + (ah - im.height) // 2), im)


# header
d.text((60, 44), "МАСТЕРСКАЯ", font=font(96), fill=TXT)
d.text((64, 158), "LOW-POLY GAME ASSET  ·  BLENDER  >  UNITY", font=font(38), fill=SUB)
d.rectangle([60, 222, 250, 230], fill=ACC)
tris = open(STATS).read().strip() if os.path.exists(STATS) else "?"
info = [f"~{int(tris):,} треугольников (всё вместе с пропсами)".replace(",", " "),
        "24 материала · текстуры PNG 1024px",
        "15 объектов · FBX, масштаб 1 ед. = 1 м",
        "Пропсы отдельными FBX в models/Props"]
y = 44
for s in info:
    d.ellipse([2250, y + 16, 2262, y + 28], fill=ACC)
    d.text((2280, y), s, font=font(34, False), fill=TXT)
    y += 50

# layout
M, G = 60, 30
top_y = 270
panel((M, top_y, 1900, 1370), "ОСНОВНОЙ РЕНДЕР", "hero")
rx0 = 1900 + G
cw = (W - M - rx0 - G) // 2
ch = (1370 - top_y - G) // 2
panel((rx0, top_y, rx0 + cw, top_y + ch), "ВИД СПЕРЕДИ", "front")
panel((rx0 + cw + G, top_y, W - M, top_y + ch), "ВИД СЗАДИ", "back")
panel((rx0, top_y + ch + G, rx0 + cw, 1370), "ВИД СЛЕВА", "left")
panel((rx0 + cw + G, top_y + ch + G, W - M, 1370), "ВИД СПРАВА", "right")

row_y0, row_y1 = 1370 + G, 1830
bw = (W - 2 * M - 3 * G) // 4
xs = [M + i * (bw + G) for i in range(4)]
panel((xs[0], row_y0, xs[0] + bw, row_y1), "ИГРОВАЯ КАМЕРА", "iso")
panel((xs[1], row_y0, xs[1] + bw, row_y1), "CLAY РЕНДЕР", "clay")
panel((xs[2], row_y0, xs[2] + bw, row_y1), "КАРКАС (WIREFRAME)", "wire")
panel((xs[3], row_y0, xs[3] + bw, row_y1), "ВИД СВЕРХУ", "top")
panel((M, row_y1 + G, W - M, H - M), "ОСНОВНЫЕ ПРОПСЫ", "props", pad=10)

sheet.save(OUT)
print("sheet", OUT)

