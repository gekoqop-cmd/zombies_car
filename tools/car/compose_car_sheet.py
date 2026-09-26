"""Composes the car presentation sheet. Usage: python compose_car_sheet.py <renders_dir> <out.png>"""
import sys, os
from PIL import Image, ImageDraw, ImageFont

RD, OUT = sys.argv[1], sys.argv[2]
STATS = r"D:\zombi-caaar\models\Car\_stats.txt"
FB = r"C:\Windows\Fonts\bahnschrift.ttf"
W, H = 3200, 2300
BG, PANEL, LINE = (24, 26, 30), (32, 35, 40), (60, 64, 72)
TXT, SUB, ACC = (236, 232, 224), (150, 152, 160), (230, 110, 30)
sheet = Image.new("RGB", (W, H), BG); d = ImageDraw.Draw(sheet)


def font(sz, bold=True):
    f = ImageFont.truetype(FB, sz)
    try:
        f.set_variation_by_name("Bold" if bold else "Regular")
    except Exception:
        pass
    return f


def panel(box, title, name, sub=None, pad=22):
    x0, y0, x1, y1 = box
    d.rounded_rectangle(box, radius=18, fill=PANEL, outline=LINE, width=2)
    d.text((x0 + 24, y0 + 16), title, font=font(32), fill=TXT)
    if sub:
        d.text((x0 + 24, y1 - 46), sub, font=font(24, False), fill=SUB)
    im = Image.open(os.path.join(RD, name + ".png")).convert("RGBA")
    bb = im.getbbox(); im = im.crop(bb) if bb else im
    aw, ah = x1 - x0 - 2 * pad, y1 - y0 - 64 - pad - (40 if sub else 0)
    s = min(aw / im.width, ah / im.height)
    im = im.resize((int(im.width * s), int(im.height * s)), Image.LANCZOS)
    sheet.paste(im, (x0 + (x1 - x0 - im.width) // 2, y0 + 60 + (ah - im.height) // 2), im)


base, dmg, allt = open(STATS).read().split()
d.text((60, 40), "ИГРОВОЙ АВТОМОБИЛЬ", font=font(92), fill=TXT)
d.text((64, 150), "LOW-POLY  ·  BLENDER  >  UNITY  ·  Car_Player.fbx", font=font(36), fill=SUB)
d.rectangle([60, 210, 250, 218], fill=ACC)
info = [f"{int(base):,} трисов — машина с грузом".replace(",", " "),
        f"+{int(dmg)} трисов — элементы повреждений (всего {int(allt):,})".replace(",", " "),
        "4 материала: Intact / Damaged / Wrecked + Emissive",
        "Атлас 2048 px, ржавчина — процедурная текстура"]
y = 34
for s in info:
    d.ellipse([2050, y + 15, 2062, y + 27], fill=ACC); d.text((2080, y), s, font=font(32, False), fill=TXT); y += 46

M, G = 60, 28
y0, y1 = 250, 1150
panel((M, y0, 1500, y1), "ОСНОВНОЙ ВИД", "hero")
panel((1500 + G, y0, 2360, y1), "ИГРОВАЯ КАМЕРА ~50°", "game")
panel((2360 + G, y0, W - M, y1), "CLAY", "clay")
y2 = y1 + G; y3 = y2 + 470
cols = [(M, 700), (700 + G, 1340), (1340 + G, 2240), (2240 + G, W - M)]
for (a, b), (t, n) in zip(cols, [("СПЕРЕДИ", "front"), ("СЗАДИ", "back"), ("СБОКУ (ПРАВЫЙ)", "side"), ("СВЕРХУ", "top")]):
    panel((a, y2, b, y3), t, n)
y4 = y3 + G; y5 = H - M
cw = (W - 2 * M - 3 * G) // 4
xs = [M + i * (cw + G) for i in range(4)]
panel((xs[0], y4, xs[0] + cw, y5), "КАРКАС (WIREFRAME)", "wire")
panel((xs[1], y4, xs[1] + cw, y5), "СТАДИЯ 1 — СЛЕГКА ПОМЯТАЯ", "dmg1", "Intact + DMG1_Hood_Dented")
panel((xs[2], y4, xs[2] + cw, y5), "СТАДИЯ 2 — СИЛЬНО ПОБИТАЯ", "dmg2", "Damaged + капот + DMG2_Bumper_Bent")
panel((xs[3], y4, xs[3] + cw, y5), "СТАДИЯ 3 — ПОЧТИ УБИТАЯ", "dmg3", "Wrecked + капот + бампер + DMG3_Cargo_Fallen")
sheet.save(OUT); print("sheet", OUT)
