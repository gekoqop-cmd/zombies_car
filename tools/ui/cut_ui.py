"""Cuts the UI references in refmenu/ into the parts the Unity UI is assembled from (no baked text or values).

Writes to unity/Assets/Art/Textures/UI:
  UI_MenuBackground.png   menumain.png (full-screen menu background, shown in cover mode)
  UI_Logo.png             logo.png trimmed to its alpha
  UI_MapCityDay/Night.png map1.png / map2.png, the "City" map card pictures (night warped onto the day one)
  UI_HpPanel.png          hp_widget panel with the labels, numbers and bars painted out
  UI_HpFrame.png          portrait frame (ring + the hair that overlaps it), transparent inside
  UI_HpPortrait.png       the portrait inside the frame
  UI_BarTrack.png         empty bar trough (9-slice), from the level bar's empty end
  UI_BarFillRed/Blue.png  health / experience fills (9-slice)
  UI_AmmoPanel.png        patronset ammo panel, text and bullets painted out
  UI_AmmoIcon.png         the three bullets
  UI_GearButton.png       the settings button plate (gear painted out); also the menu button 9-slice
  UI_GearIcon.png         the gear
  UI_WavePanel.png        wave panel with its notched shape, text painted out
  UI_IconSun/Moon.png, UI_Arrow.png  small drawn icons for the map selection
  UI_StickRing/Knob.png, UI_RoundButton.png  round touch controls (mobile)
The references' backgrounds are already transparent (checked), so no background removal is needed; cut edges get
a 1 px defringe (colour bled from the inside).
"""
import os

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.join(os.path.dirname(__file__), "..", "..")
REF = os.path.join(ROOT, "refmenu")
OUT = os.path.join(ROOT, "unity", "Assets", "Art", "Textures", "UI")
HUD_SCALE = 0.5            # the references are ~2x the HUD's on-screen size at 1080p


def load(name):
    return np.asarray(Image.open(os.path.join(REF, name)).convert("RGBA")).copy()


def mult4(n):
    return max(4, int(round(n / 4.0)) * 4)


def save(arr, name, scale=1.0):
    """Saves at a size that is a multiple of 4 on both sides: GPU block compression (DXT) needs it, otherwise Unity
    keeps the sprite uncompressed (4-6x the download)."""
    im = Image.fromarray(arr.astype(np.uint8), "RGBA")
    im = im.resize((mult4(im.width * scale), mult4(im.height * scale)), Image.LANCZOS)
    im.save(os.path.join(OUT, name + ".png"), optimize=True)
    print(name, im.size)


def defringe(rgba):
    """Bleed the colour of opaque pixels into the transparent ones (no dark halo when filtered/scaled)."""
    rgb = rgba[..., :3].astype(np.float32)
    a = rgba[..., 3]
    solid = (a > 200).astype(np.uint8)
    bled = cv2.inpaint(rgb.astype(np.uint8), (1 - solid).astype(np.uint8), 3, cv2.INPAINT_TELEA)
    out = rgba.copy()
    out[..., :3] = np.where(solid[..., None] == 1, rgba[..., :3], bled)
    return out


def paint_out(rgba, boxes, whole=()):
    """Inpaints text (bright letters + their dark outline) inside `boxes` and everything inside `whole` boxes."""
    rgb = np.ascontiguousarray(rgba[..., :3])
    lum = rgb.mean(axis=2)
    mask = np.zeros(lum.shape, np.uint8)
    for x0, y0, x1, y1 in boxes:
        region = lum[y0:y1, x0:x1]
        m = ((region > 75) | (region < 20)).astype(np.uint8)
        mask[y0:y1, x0:x1] |= m
    for x0, y0, x1, y1 in whole:
        mask[y0:y1, x0:x1] = 1
    mask = cv2.dilate(mask, np.ones((9, 9), np.uint8))
    out = rgba.copy()
    out[..., :3] = cv2.inpaint(rgb, mask * 255, 11, cv2.INPAINT_TELEA)
    return out


def rounded_mask(shape, box, radius):
    im = Image.new("L", (shape[1], shape[0]), 0)
    ImageDraw.Draw(im).rounded_rectangle(box, radius=radius, fill=255)
    return np.asarray(im.filter(ImageFilter.GaussianBlur(1))) / 255.0


def nine_slice_from_right_cap(src, cap):
    """Symmetric 9-slice bar: the right end of src (cap px) and its mirror joined by a 16 px middle strip."""
    right = src[:, -cap:]
    middle = src[:, -cap - 16:-cap]
    left = right[:, ::-1]
    return np.concatenate([left, middle, right], axis=1)


def bar_fill(src, cap, width):
    """A fill as wide as its track (Filled images do not 9-slice): left cap, the middle stretched, mirrored cap."""
    left = src[:, :cap]
    middle = cv2.resize(np.ascontiguousarray(src[:, cap:-cap]), (width - 2 * cap, src.shape[0]), interpolation=cv2.INTER_LINEAR)
    right = left[:, ::-1]
    return np.concatenate([left, middle, right], axis=1)


def hp_widget():
    w = load("hp_widget.png")
    # Portrait: frame ring + overlapping hair in one layer, the inside in another.
    crop = w[35:645, 55:690].copy()
    inner = rounded_mask(crop.shape, (54, 82, 578, 558), 40)              # frame's inner opening, crop coords
    frame = crop.copy()
    frame[..., 3] = (frame[..., 3] * (1 - inner)).astype(np.uint8)
    portrait = crop.copy()
    portrait[..., 3] = (portrait[..., 3] * inner).astype(np.uint8)
    save(defringe(frame), "UI_HpFrame", HUD_SCALE)
    save(defringe(portrait), "UI_HpPortrait", HUD_SCALE)

    # Panel: labels, numbers and both bars painted out (the live bars and texts go on top).
    panel = paint_out(w, boxes=[(725, 145, 1320, 270), (715, 445, 1170, 555)],
                      whole=[(700, 272, 2032, 424), (1168, 452, 2032, 562)])
    save(defringe(panel[85:625, 560:2080]), "UI_HpPanel", HUD_SCALE)

    # Bars: the empty end of the level trough, the red / blue fills with their round caps.
    track = w[461:556, 1560:2020]
    save(defringe(nine_slice_from_right_cap(track, 70)), "UI_BarTrack", HUD_SCALE)
    # Fills in their tracks' proportions (health ~1300 x 110 px, level ~820 x 65 px in the reference).
    red = w[291:401, 724:1330]
    save(defringe(bar_fill(red, 60, 1290)), "UI_BarFillRed", HUD_SCALE)
    blue = w[472:540, 1193:1575]
    save(defringe(bar_fill(np.concatenate([blue[:, :50], blue[:, 60:-60], blue[:, -50:][:, ::-1][:, ::-1]], axis=1), 50, 820)),
         "UI_BarFillBlue", HUD_SCALE)


def patronset():
    p = load("patronset.png")
    bullets = p[235:470, 135:365].copy()
    gold = (bullets[..., 0].astype(int) - bullets[..., 2].astype(int) > 60) | (bullets[..., :3].mean(axis=2) > 170)
    mask = cv2.dilate(gold.astype(np.uint8), np.ones((13, 13), np.uint8))
    bullets[..., 3] = np.minimum(bullets[..., 3], cv2.GaussianBlur(mask * 255, (5, 5), 0))
    save(defringe(bullets), "UI_AmmoIcon", HUD_SCALE)
    panel = paint_out(p, boxes=[(395, 285, 960, 430), (1010, 280, 1600, 445)], whole=[(130, 230, 370, 475)])
    save(defringe(panel[170:553, 50:1710]), "UI_AmmoPanel", HUD_SCALE)

    gear = p[235:495, 1795:2055].copy()
    light = gear[..., :3].mean(axis=2) > 140
    mask = cv2.dilate(light.astype(np.uint8), np.ones((15, 15), np.uint8))
    gear[..., 3] = np.minimum(gear[..., 3], cv2.GaussianBlur(mask * 255, (5, 5), 0))
    save(defringe(gear), "UI_GearIcon", HUD_SCALE)
    button = paint_out(p, boxes=[], whole=[(1805, 245, 2045, 485)])
    save(defringe(button[170:553, 1734:2124]), "UI_GearButton", HUD_SCALE)


def wave():
    v = load("wave.png")
    panel = paint_out(v, boxes=[(425, 230, 1370, 435), (565, 485, 1205, 700)])
    save(defringe(panel[142:755, 108:1666]), "UI_WavePanel", HUD_SCALE)


def icons():
    s = 128
    sun = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(sun)
    c = s / 2
    for i in range(8):
        a = i * np.pi / 4
        d.line((c + np.cos(a) * 36, c + np.sin(a) * 36, c + np.cos(a) * 58, c + np.sin(a) * 58), fill=(255, 206, 84, 255), width=11)
    d.ellipse((c - 28, c - 28, c + 28, c + 28), fill=(255, 214, 96, 255))
    sun.save(os.path.join(OUT, "UI_IconSun.png"))
    moon = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(moon)
    d.ellipse((14, 14, 114, 114), fill=(200, 216, 255, 255))
    d.ellipse((44, 4, 134, 94), fill=(0, 0, 0, 0))
    moon.save(os.path.join(OUT, "UI_IconMoon.png"))
    arrow = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ImageDraw.Draw(arrow).polygon([(40, 14), (100, 64), (40, 114), (26, 100), (70, 64), (26, 28)], fill=(255, 255, 255, 255))
    arrow.save(os.path.join(OUT, "UI_Arrow.png"))
    sticks()
    print("icons")


def align_to(reference, image):
    """The night picture is drawn ~1 % taller and a few px off the day one: warp it onto the day picture (affine,
    fitted on the edges) so the Day / Night cross-fade does not jump."""
    ref = np.asarray(reference)
    img = cv2.resize(np.asarray(image), (ref.shape[1], ref.shape[0]))
    def edges(rgb, lo, hi):
        e = cv2.Canny(cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY), lo, hi).astype(np.float32)
        return cv2.GaussianBlur(e, (0, 0), 3)
    warp = np.eye(2, 3, dtype=np.float32)
    criteria = (cv2.TERM_CRITERIA_EPS | cv2.TERM_CRITERIA_COUNT, 200, 1e-6)
    _, warp = cv2.findTransformECC(edges(ref, 60, 150), edges(img, 40, 120), warp, cv2.MOTION_AFFINE, criteria, None, 5)
    print("night picture aligned:", np.round(warp, 4).tolist())
    out = cv2.warpAffine(img, warp, (ref.shape[1], ref.shape[0]), flags=cv2.INTER_LINEAR | cv2.WARP_INVERSE_MAP,
                         borderMode=cv2.BORDER_REPLICATE)
    return Image.fromarray(out)


def sticks():
    """Round touch controls in the HUD style: the stick ring (dark translucent disc, light rim), the knob and a round
    button plate. Drawn 4x and scaled down for smooth edges."""
    def disc(size, fill, rim, rim_width, inner_shadow=False, gloss=False):
        n = size * 4
        im = Image.new("RGBA", (n, n), (0, 0, 0, 0))
        d = ImageDraw.Draw(im)
        d.ellipse((2, 2, n - 3, n - 3), fill=rim)
        w = rim_width * 4
        d.ellipse((w, w, n - 1 - w, n - 1 - w), fill=fill)
        if inner_shadow:
            ring = Image.new("RGBA", (n, n), (0, 0, 0, 0))
            ImageDraw.Draw(ring).ellipse((w, w, n - 1 - w, n - 1 - w), outline=(0, 0, 0, 90), width=w * 2)
            im.alpha_composite(ring.filter(ImageFilter.GaussianBlur(w)))
        if gloss:
            g = Image.new("RGBA", (n, n), (0, 0, 0, 0))
            ImageDraw.Draw(g).ellipse((n * 0.2, n * 0.1, n * 0.8, n * 0.5), fill=(255, 255, 255, 60))
            im.alpha_composite(g.filter(ImageFilter.GaussianBlur(n * 0.04)))
        return im.resize((size, size), Image.LANCZOS)
    disc(256, (12, 14, 20, 110), (225, 215, 200, 170), 5, inner_shadow=True).save(os.path.join(OUT, "UI_StickRing.png"))
    disc(128, (58, 62, 74, 235), (235, 228, 214, 255), 6, gloss=True).save(os.path.join(OUT, "UI_StickKnob.png"))
    disc(160, (20, 23, 31, 215), (205, 200, 190, 220), 5, gloss=True).save(os.path.join(OUT, "UI_RoundButton.png"))
    print("sticks")


def pictures():
    # 1600 x 900: 16:9, a multiple of 4 (compressible), enough for a background seen through a dim and a slow zoom.
    Image.open(os.path.join(REF, "menumain.png")).convert("RGB").resize((1600, 900), Image.LANCZOS).save(
        os.path.join(OUT, "UI_MenuBackground.png"), optimize=True)
    logo = Image.open(os.path.join(REF, "logo.png")).convert("RGBA")
    logo = logo.crop(logo.getbbox())
    logo = logo.resize((1200, mult4(logo.height * 1200 / logo.width)), Image.LANCZOS)
    logo.save(os.path.join(OUT, "UI_Logo.png"), optimize=True)
    day = Image.open(os.path.join(REF, "map1.png")).convert("RGB")
    day.resize((1024, 576), Image.LANCZOS).save(os.path.join(OUT, "UI_MapCityDay.png"), optimize=True)
    night = align_to(day, Image.open(os.path.join(REF, "map2.png")).convert("RGB"))
    night.resize((1024, 576), Image.LANCZOS).save(os.path.join(OUT, "UI_MapCityNight.png"), optimize=True)
    print("pictures")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    pictures()
    hp_widget()
    patronset()
    wave()
    icons()
