"""Cuts every take into work/raw/<key>/NNNN.png and draws numbered contact sheets work/sheets/<key>.png."""
import os
import subprocess
import sys

from PIL import Image, ImageDraw

from takes import TAKES, path

HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.join(HERE, "work")


def main(keys):
    os.makedirs(os.path.join(WORK, "sheets"), exist_ok=True)
    for key in keys or TAKES:
        out = os.path.join(WORK, "raw", key)
        if not (os.path.isdir(out) and os.listdir(out)):
            os.makedirs(out, exist_ok=True)
            subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", path(key), "-fps_mode", "passthrough",
                            os.path.join(out, "%04d.png")], check=True)
        names = sorted(os.listdir(out))
        ims = [Image.open(os.path.join(out, n)) for n in names]
        cols = 10
        w, h = ims[0].size
        s = 180 / w
        cw, ch = int(w * s), int(h * s)
        rows = (len(ims) + cols - 1) // cols
        sheet = Image.new("RGB", (cw * cols, ch * rows))
        d = ImageDraw.Draw(sheet)
        for i, im in enumerate(ims):
            x, y = (i % cols) * cw, (i // cols) * ch
            sheet.paste(im.convert("RGB").resize((cw, ch)), (x, y))
            d.text((x + 3, y + 3), str(i), fill=(0, 0, 0))
        sheet.save(os.path.join(WORK, "sheets", key + ".png"))
        print(key, len(ims))


if __name__ == "__main__":
    main(sys.argv[1:])
