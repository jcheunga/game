"""Finishing pass for menu backgrounds: soft highlight bloom, gentle split-tone grade,
vignette and an optional calm-zone dim where UI panels sit. Pure numpy + OpenImageIO."""
from pathlib import Path

import numpy as np
import OpenImageIO as oiio


def read(path):
    buf = oiio.ImageBuf(str(path))
    arr = np.asarray(buf.get_pixels(oiio.FLOAT), dtype=np.float32)
    return arr[..., :3]


def write(arr, path):
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    h, w = arr.shape[:2]
    data = np.clip(np.round(arr * 255.0), 0, 255).astype(np.uint8)
    spec = oiio.ImageSpec(w, h, 3, oiio.UINT8)
    out = oiio.ImageOutput.create(str(path))
    out.open(str(path), spec)
    out.write_image(data)
    out.close()


def _blur1d(a, sigma, axis):
    r = int(np.ceil(sigma * 3))
    k = np.exp(-0.5 * (np.arange(-r, r + 1) / sigma) ** 2)
    k /= k.sum()
    pad = [(0, 0)] * a.ndim
    pad[axis] = (r, r)
    p = np.pad(a, pad, mode='edge')
    out = np.zeros_like(a)
    n = a.shape[axis]
    for i, w in enumerate(k):
        sl = [slice(None)] * a.ndim
        sl[axis] = slice(i, i + n)
        out += w * p[tuple(sl)]
    return out


def blur(a, sigma):
    # downsample for big radii (cheap and smooth)
    f = 1
    while sigma / f > 6 and min(a.shape[:2]) // (f * 2) > 16:
        f *= 2
    small = a
    if f > 1:
        h, w = a.shape[:2]
        hh, ww = h // f * f, w // f * f
        small = a[:hh, :ww].reshape(hh // f, f, ww // f, f, -1).mean(axis=(1, 3))
    b = _blur1d(_blur1d(small, sigma / f, 0), sigma / f, 1)
    if f > 1:
        b = np.repeat(np.repeat(b, f, axis=0), f, axis=1)
        b = _blur1d(_blur1d(b, f * 0.6, 0), f * 0.6, 1)
        h, w = a.shape[:2]
        b = np.pad(b, ((0, h - b.shape[0]), (0, w - b.shape[1]), (0, 0)), mode='edge')
    return b


def finish(src, dst, bloom=0.16, bloom_threshold=0.62, vignette=0.32, shadows='18222a', highlights='ffe2b0',
           split=0.12, saturation=1.0, contrast=1.0, calm=None, calm_amount=0.0, exposure=1.0, lift=0.0):
    img = read(src) * exposure
    h, w = img.shape[:2]
    luma = img @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    # bloom from highlights (lanterns, sun, windows)
    if bloom:
        hi = np.clip((luma - bloom_threshold) / (1 - bloom_threshold), 0, 1)[..., None] * img
        glow = blur(hi, w * 0.006) * 0.6 + blur(hi, w * 0.025) * 0.4
        img = 1 - (1 - img) * (1 - np.clip(glow * bloom * 2.2, 0, 1))
    # split-tone: cool shadows, warm highlights
    if split:
        sh = np.array([int(shadows[i:i + 2], 16) / 255 for i in (0, 2, 4)], np.float32)
        hl = np.array([int(highlights[i:i + 2], 16) / 255 for i in (0, 2, 4)], np.float32)
        luma = img @ np.array([0.2126, 0.7152, 0.0722], np.float32)
        t = np.clip(luma, 0, 1)[..., None]
        tone = sh * (1 - t) + hl * t
        img = img * (1 - split) + img * tone * 1.6 * split
    if lift:
        img = img + lift * (1 - img)
    if contrast != 1.0:
        img = (img - 0.5) * contrast + 0.5
    if saturation != 1.0:
        luma = (img @ np.array([0.2126, 0.7152, 0.0722], np.float32))[..., None]
        img = luma + (img - luma) * saturation
    # vignette (elliptical, stronger at corners)
    if vignette:
        y, x = np.mgrid[0:h, 0:w].astype(np.float32)
        u = (x + 0.5) / w * 2 - 1
        v = (y + 0.5) / h * 2 - 1
        d = np.sqrt((u * 0.9) ** 2 + (v * 1.05) ** 2)
        vg = 1 - vignette * np.clip((d - 0.55) / 0.85, 0, 1) ** 1.6
        img = img * vg[..., None]
    if calm is not None and calm_amount:
        # soften and dim a rectangle (x0, y0, x1, y1 in 0..1) where panels sit
        x0, y0, x1, y1 = calm
        y, x = np.mgrid[0:h, 0:w].astype(np.float32)
        fx = np.clip(np.minimum((x / w - x0), (x1 - x / w)) / 0.08, 0, 1)
        fy = np.clip(np.minimum((y / h - y0), (y1 - y / h)) / 0.08, 0, 1)
        m = (fx * fy)[..., None] * calm_amount
        soft = blur(img, w * 0.002)
        img = img * (1 - m) + soft * (1 - 0.25) * m
    write(np.clip(img, 0, 1), dst)
    return dst
