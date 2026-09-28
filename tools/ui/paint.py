"""Tiny cartoon painter for the game's generated sprites (icons, comic effects, the axe).

Every part is drawn as a filled shape with a thick dark outline (the painted look of the characters and the HUD),
a soft top-to-bottom shade and an optional gloss. Drawing happens at SS x the output size and is downsampled with
LANCZOS, so edges are anti-aliased.
"""
import math

import cv2
import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

INK = (38, 24, 18, 255)          # the dark brown outline of the painted art


def mult4(n):
    return max(4, int(round(n / 4.0)) * 4)


class Painter:
    def __init__(self, size, ss=4, outline=None):
        self.w, self.h = (size, size) if isinstance(size, int) else size
        self.ss = ss
        self.W, self.H = self.w * ss, self.h * ss
        self.img = Image.new("RGBA", (self.W, self.H), (0, 0, 0, 0))
        # Outline width in output pixels (about 1/24 of the size by default).
        self.outline = outline if outline is not None else max(2.0, min(self.w, self.h) / 24.0)

    # ------------------------------------------------------------------ coordinates (output pixels -> canvas)
    def p(self, x, y):
        return (x * self.ss, y * self.ss)

    def pts(self, points):
        return [self.p(x, y) for x, y in points]

    # ------------------------------------------------------------------ masks
    def mask(self):
        return Image.new("L", (self.W, self.H), 0)

    def poly_mask(self, points):
        m = self.mask()
        ImageDraw.Draw(m).polygon(self.pts(points), fill=255)
        return m

    def ellipse_mask(self, box):
        m = self.mask()
        x0, y0, x1, y1 = box
        ImageDraw.Draw(m).ellipse([*self.p(x0, y0), *self.p(x1, y1)], fill=255)
        return m

    def rrect_mask(self, box, radius):
        m = self.mask()
        x0, y0, x1, y1 = box
        ImageDraw.Draw(m).rounded_rectangle([*self.p(x0, y0), *self.p(x1, y1)], radius=radius * self.ss, fill=255)
        return m

    def line_mask(self, points, width, round_caps=True):
        m = self.mask()
        d = ImageDraw.Draw(m)
        pts = self.pts(points)
        d.line(pts, fill=255, width=int(width * self.ss), joint="curve")
        if round_caps:
            r = width * self.ss / 2
            for x, y in (pts[0], pts[-1]):
                d.ellipse([x - r, y - r, x + r, y + r], fill=255)
        return m

    def arc_mask(self, center, radius, start, end, width):
        """Thick arc, angles in degrees (0 = right, clockwise, PIL convention)."""
        m = self.mask()
        cx, cy = center
        box = [*self.p(cx - radius, cy - radius), *self.p(cx + radius, cy + radius)]
        ImageDraw.Draw(m).arc(box, start, end, fill=255, width=int(width * self.ss))
        return m

    def text_mask(self, text, font_path, size, center, rotate=0.0, stretch=1.0):
        font = ImageFont.truetype(font_path, int(size * self.ss))
        tmp = Image.new("L", (self.W, self.H), 0)
        d = ImageDraw.Draw(tmp)
        bbox = d.textbbox((0, 0), text, font=font)
        tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
        layer = Image.new("L", (tw + 8, th + 8), 0)
        ImageDraw.Draw(layer).text((4 - bbox[0], 4 - bbox[1]), text, font=font, fill=255)
        if stretch != 1.0:
            layer = layer.resize((layer.width, int(layer.height * stretch)), Image.LANCZOS)
        if rotate:
            layer = layer.rotate(rotate, resample=Image.BICUBIC, expand=True)
        cx, cy = self.p(*center)
        tmp.paste(layer, (int(cx - layer.width / 2), int(cy - layer.height / 2)), layer)
        return tmp

    # ------------------------------------------------------------------ painting
    def fill(self, mask, color, shade=0.22, gloss=0.0, outline=True, outline_width=None, ink=INK, gradient=None):
        """Paints a part: outline (dilated mask in ink), then the colour shaded from light (top) to dark (bottom)."""
        if outline:
            ow = (outline_width if outline_width is not None else self.outline) * self.ss
            self._paste(dilate(mask, ow), ink)
        bbox = mask.getbbox()
        if bbox is None:
            return
        colour = self._shaded(color, bbox, shade, gradient)
        self.img.paste(colour, (0, 0), mask)
        if gloss > 0:
            self.gloss(mask, gloss)

    def solid(self, mask, color):
        self._paste(mask, color)

    def gloss(self, mask, strength):
        """A soft white highlight on the upper-left of the part."""
        bbox = mask.getbbox()
        if bbox is None:
            return
        x0, y0, x1, y1 = bbox
        g = self.mask()
        w, h = x1 - x0, y1 - y0
        ImageDraw.Draw(g).ellipse([x0 + w * 0.12, y0 + h * 0.06, x0 + w * 0.62, y0 + h * 0.42], fill=int(255 * strength))
        g = g.filter(ImageFilter.GaussianBlur(max(w, h) * 0.06))
        g = ImageChops.multiply(g, mask)
        self._paste(g, (255, 255, 255, 255))

    def _paste(self, mask, color):
        layer = Image.new("RGBA", (self.W, self.H), color)
        self.img.paste(layer, (0, 0), mask)

    def _shaded(self, color, bbox, shade, gradient):
        x0, y0, x1, y1 = bbox
        top = np.array(color[:3], np.float32)
        if gradient is not None:
            bottom = np.array(gradient[:3], np.float32)
        else:
            bottom = top * (1.0 - shade)
            top = np.minimum(255, top * (1.0 + shade * 0.6))
        t = np.clip((np.arange(self.H, dtype=np.float32) - y0) / max(1, y1 - y0), 0, 1)[:, None]
        rows = top[None, :] * (1 - t) + bottom[None, :] * t
        arr = np.zeros((self.H, self.W, 4), np.uint8)
        arr[..., :3] = rows[:, None, :].astype(np.uint8)
        arr[..., 3] = color[3] if len(color) > 3 else 255
        return Image.fromarray(arr, "RGBA")

    # ------------------------------------------------------------------ output
    def result(self, size=None):
        out = self.img.resize((self.w, self.h), Image.LANCZOS)
        if size is not None:
            out = out.resize(size, Image.LANCZOS)
        return out

    def save(self, path, size=None):
        out = self.result(size)
        out = Image.fromarray(defringe(np.asarray(out)), "RGBA")
        out.save(path, optimize=True)
        return out


def dilate(mask, radius):
    """Grows an L mask by `radius` canvas pixels (round brush), fast (OpenCV)."""
    r = int(round(radius))
    if r < 1:
        return mask
    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (2 * r + 1, 2 * r + 1))
    return Image.fromarray(cv2.dilate(np.asarray(mask), kernel), "L")


def defringe(rgba):
    """Colour of the opaque pixels bled into the transparent ones (no dark halo when the sprite is filtered)."""
    rgba = rgba.copy()
    a = rgba[..., 3]
    rgb = rgba[..., :3].astype(np.float32)
    weight = (a > 8).astype(np.float32)
    acc = rgb * weight[..., None]
    wsum = weight.copy()
    for _ in range(6):
        acc = _blur3(acc)
        wsum = _blur3(wsum[..., None])[..., 0]
    bled = acc / np.maximum(wsum[..., None], 1e-6)
    rgba[..., :3] = np.where((a > 8)[..., None], rgba[..., :3], np.clip(bled, 0, 255).astype(np.uint8))
    return rgba


def _blur3(x):
    p = np.pad(x, ((1, 1), (1, 1), (0, 0)), mode="edge")
    return (p[:-2, :-2] + p[:-2, 1:-1] + p[:-2, 2:] + p[1:-1, :-2] + p[1:-1, 1:-1] + p[1:-1, 2:] +
            p[2:, :-2] + p[2:, 1:-1] + p[2:, 2:]) / 9.0


def star_points(cx, cy, r_out, r_in, n=5, rot=-90.0):
    pts = []
    for i in range(n * 2):
        r = r_out if i % 2 == 0 else r_in
        a = math.radians(rot + i * 180.0 / n)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def burst_points(cx, cy, r_out, r_in, n=12, rot=0.0, jitter=0.0, seed=1):
    rnd = np.random.RandomState(seed)
    pts = []
    for i in range(n * 2):
        r = r_out if i % 2 == 0 else r_in
        r *= 1.0 + (rnd.rand() - 0.5) * jitter
        a = math.radians(rot + i * 180.0 / n + (rnd.rand() - 0.5) * jitter * 20)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def rotate_points(points, center, degrees):
    cx, cy = center
    a = math.radians(degrees)
    c, s = math.cos(a), math.sin(a)
    return [(cx + (x - cx) * c - (y - cy) * s, cy + (x - cx) * s + (y - cy) * c) for x, y in points]
