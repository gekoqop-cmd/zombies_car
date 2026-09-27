"""Stage 1: neural background removal (rembg, isnet-general-use) for every extracted frame.

Frames come from refsanim/Player/*.mp4 (ffmpeg -fps_mode passthrough -> work/raw/<clip>/0001.png).
Writes the alpha matte of each frame to work/mask/<clip>/0001.png (cached: existing masks are skipped).
python masks.py
"""
import os
import sys

import numpy as np
from PIL import Image
from rembg import new_session, remove

HERE = os.path.dirname(os.path.abspath(__file__))
WORK = os.path.join(HERE, "work")
CLIPS = ("idle", "run", "shot", "reload")


def main():
    session = new_session("isnet-general-use")
    for clip in CLIPS:
        src = os.path.join(WORK, "raw", clip)
        dst = os.path.join(WORK, "mask", clip)
        os.makedirs(dst, exist_ok=True)
        names = sorted(os.listdir(src))
        for i, name in enumerate(names):
            out = os.path.join(dst, name)
            if os.path.exists(out):
                continue
            rgba = remove(Image.open(os.path.join(src, name)).convert("RGB"), session=session)
            Image.fromarray(np.asarray(rgba)[..., 3]).save(out)
            if i % 10 == 0:
                print(clip, i, "/", len(names), flush=True)
    print("masks done")


if __name__ == "__main__":
    sys.exit(main())
