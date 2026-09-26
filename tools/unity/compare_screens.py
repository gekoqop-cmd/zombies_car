"""Side-by-side comparison of referens/concept.png with the Map01 screenshots captured by
Tools/Capture Map01 Screenshots (unity/Screenshots). Writes unity/Screenshots/Compare_*.png."""
import os

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CONCEPT = os.path.join(ROOT, "referens", "concept.png")
SHOTS = os.path.join(ROOT, "unity", "Screenshots")
PAIRS = [("Map01_Gameplay_16x9", "Compare_Gameplay.png"), ("Map01_Overview", "Compare_Overview.png"),
         ("Map01_Workshop", "Compare_Workshop.png"), ("Map01_GasStation", "Compare_GasStation.png")]
HEIGHT = 720
LABEL_H = 44


def font(size):
    for name in ("arialbd.ttf", "arial.ttf", "DejaVuSans-Bold.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            continue
    return ImageFont.load_default()


def fit(img):
    return img.resize((round(img.width * HEIGHT / img.height), HEIGHT), Image.LANCZOS)


def main():
    concept = fit(Image.open(CONCEPT).convert("RGB"))
    f = font(26)
    for shot, out in PAIRS:
        path = os.path.join(SHOTS, shot + ".png")
        if not os.path.exists(path):
            print("missing", path)
            continue
        right = fit(Image.open(path).convert("RGB"))
        gap = 12
        canvas = Image.new("RGB", (concept.width + gap + right.width, HEIGHT + LABEL_H), (24, 24, 28))
        canvas.paste(concept, (0, LABEL_H))
        canvas.paste(right, (concept.width + gap, LABEL_H))
        d = ImageDraw.Draw(canvas)
        d.text((12, 8), "concept.png", font=f, fill=(240, 200, 150))
        d.text((concept.width + gap + 12, 8), f"Unity: {shot}", font=f, fill=(150, 210, 240))
        canvas.save(os.path.join(SHOTS, out))
        print("saved", out, canvas.size)


if __name__ == "__main__":
    main()
