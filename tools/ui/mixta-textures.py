"""
The interface's own materials («Mixta», ui/themes/mixta.css), drawn here (no
pack art): a brushed-steel sheen and a grime layer for the panels, both
tileable and nearly transparent, laid over the panels' own gradient. Written to
packages/client/public/ui/mixta/. Deterministic: the same files every run.

  python3 tools/ui/mixta-textures.py
"""
import os
import random

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', '..'))
OUT = os.path.join(ROOT, 'packages/client/public/ui/mixta')
SIZE = 256


def tiled_blur(im: Image.Image, radius: float) -> Image.Image:
    """Blurs across the edges too, so the result still tiles."""
    w, h = im.size
    big = Image.new(im.mode, (w * 3, h * 3))
    for i in range(3):
        for j in range(3):
            big.paste(im, (i * w, j * h))
    return big.filter(ImageFilter.GaussianBlur(radius)).crop((w, h, 2 * w, 2 * h))


def noise(rng: random.Random, size: int) -> Image.Image:
    im = Image.new('L', (size, size))
    im.putdata([rng.randrange(256) for _ in range(size * size)])
    return im


def stretch(im: Image.Image) -> Image.Image:
    lo, hi = im.getextrema()
    span = max(1, hi - lo)
    return im.point(lambda v: int((v - lo) * 255 / span))


def steel(rng: random.Random) -> Image.Image:
    """Brushed metal: horizontal streaks (one value per row), swelling and fading along the row, a few per cent strong."""
    rows = Image.new('L', (1, SIZE))
    rows.putdata([rng.randrange(256) for _ in range(SIZE)])
    rows = rows.resize((SIZE, SIZE), Image.NEAREST)
    swell = stretch(tiled_blur(noise(rng, SIZE), 7))
    streak = ImageChops.multiply(rows, swell)
    streak = stretch(streak)
    light = streak.point(lambda v: max(0, v - 140) * 26 // 115)
    dark = streak.point(lambda v: max(0, 60 - v) * 30 // 60)
    out = Image.new('RGBA', (SIZE, SIZE), (255, 255, 255, 0))
    out.putalpha(light)
    shade = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    shade.putalpha(dark)
    out.alpha_composite(shade)
    return out


def grime(rng: random.Random) -> Image.Image:
    """Dirt: soft dark blotches, fine specks and a few hairline scratches."""
    blot = stretch(tiled_blur(noise(rng, SIZE), 14))
    dark = blot.point(lambda v: max(0, v - 120) * 70 // 135)
    specks = noise(rng, SIZE).point(lambda v: 34 if v > 253 else 0)
    out = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    out.putalpha(dark)
    light = Image.new('RGBA', (SIZE, SIZE), (255, 255, 255, 0))
    light.putalpha(specks)
    out.alpha_composite(light)
    d = ImageDraw.Draw(out)
    for _ in range(9):
        x, y = rng.randrange(SIZE), rng.randrange(SIZE)
        dx, dy = rng.randrange(-60, 60), rng.randrange(-14, 14)
        for ox in (-SIZE, 0, SIZE):
            for oy in (-SIZE, 0, SIZE):
                d.line((x + ox, y + oy, x + dx + ox, y + dy + oy), fill=(255, 255, 255, 22), width=1)
    return out


os.makedirs(OUT, exist_ok=True)
rng = random.Random(7)
for name, im in (('steel', steel(rng)), ('grime', grime(rng))):
    path = os.path.join(OUT, name + '.webp')
    im.save(path, 'WEBP', quality=88, method=6)
    print(path, os.path.getsize(path) // 1024, 'KB')
