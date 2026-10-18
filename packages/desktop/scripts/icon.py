# The app's icon, made from our own art: La Mula as the render lab draws it
# (renders.html, its 'mula' shot at 1024 px, saved as build/mula-1024.png) on a
# dark rounded square with a snowy sierra and a rust glow low down.
#
#   python3 packages/desktop/scripts/icon.py
#
# Writes build/icon.png (1024, the window's), build/icon.icns (macOS, with
# Apple's margin) and build/icon.ico (Windows, filling more of its square).
# Needs Pillow; iconutil comes with macOS.
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

BUILD = Path(__file__).resolve().parent.parent / 'build'
SIZE = 1024


def lerp(a, b, k):
    return tuple(round(x + (y - x) * k) for x, y in zip(a, b))


def tile(square: int) -> Image.Image:
    """The rounded square: night sky to dark ground, the sierra, a rust glow."""
    top, bottom = (44, 54, 66), (16, 19, 24)
    bg = Image.new('RGBA', (square, square))
    px = bg.load()
    for y in range(square):
        row = lerp(top, bottom, y / (square - 1)) + (255,)
        for x in range(square):
            px[x, y] = row
    # A rust glow along the horizon, like the base's evening.
    glow = Image.new('RGBA', (square, square), (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((-square * 0.2, square * 0.36, square * 1.2, square * 0.72), fill=(150, 72, 38, 110))
    bg.alpha_composite(glow.filter(ImageFilter.GaussianBlur(square * 0.08)))
    # The sierra: two ridges of peaks, snow on the far one.
    far = [(0.0, 0.5), (0.1, 0.36), (0.2, 0.43), (0.33, 0.22), (0.47, 0.38), (0.6, 0.27), (0.74, 0.4), (0.87, 0.3), (1.0, 0.42), (1.0, 1.0), (0.0, 1.0)]
    near = [(0.0, 0.56), (0.16, 0.47), (0.3, 0.54), (0.45, 0.46), (0.6, 0.53), (0.78, 0.47), (1.0, 0.55), (1.0, 1.0), (0.0, 1.0)]
    ridges = Image.new('RGBA', (square, square), (0, 0, 0, 0))
    d = ImageDraw.Draw(ridges)
    d.polygon([(x * square, y * square) for x, y in far], fill=(70, 82, 96, 255))
    # Snow caps: the far ridge's peaks, cut off a little below each summit.
    snow = Image.new('L', (square, square), 0)
    ds = ImageDraw.Draw(snow)
    for (x0, y0), (x1, y1), (x2, y2) in zip(far, far[1:], far[2:]):
        if y1 < y0 and y1 < y2:
            k = 0.35
            ds.polygon([(x1 * square, y1 * square), ((x1 + (x0 - x1) * k) * square, (y1 + (y0 - y1) * k) * square), ((x1 + (x2 - x1) * k) * square, (y1 + (y2 - y1) * k) * square)], fill=255)
    ridges.alpha_composite(Image.composite(Image.new('RGBA', (square, square), (205, 214, 224, 230)), Image.new('RGBA', (square, square), (0, 0, 0, 0)), snow))
    d = ImageDraw.Draw(ridges)
    d.polygon([(x * square, y * square) for x, y in near], fill=(30, 35, 42, 255))
    bg.alpha_composite(ridges.filter(ImageFilter.GaussianBlur(square * 0.003)))
    # The rounded corners, and a faint light edge on top.
    mask = Image.new('L', (square, square), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, square - 1, square - 1), radius=round(square * 0.225), fill=255)
    edge = Image.new('RGBA', (square, square), (0, 0, 0, 0))
    ImageDraw.Draw(edge).rounded_rectangle((1, 1, square - 2, square - 2), radius=round(square * 0.225), outline=(255, 255, 255, 40), width=max(2, square // 200))
    bg.alpha_composite(edge)
    bg.putalpha(ImageChops.multiply(bg.getchannel('A'), mask))
    return bg


def icon(margin: float) -> Image.Image:
    """The whole icon at 1024: the tile `margin` in from each edge, La Mula on it with a shadow under its wheels."""
    square = round(SIZE * (1 - 2 * margin))
    out = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    # A soft drop shadow under the tile, as macOS icons have.
    shade = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    o = (SIZE - square) // 2
    ImageDraw.Draw(shade).rounded_rectangle((o, o + SIZE * 0.012, o + square, o + square + SIZE * 0.012), radius=round(square * 0.225), fill=(0, 0, 0, 110))
    out.alpha_composite(shade.filter(ImageFilter.GaussianBlur(SIZE * 0.012)))
    out.alpha_composite(tile(square), (o, o))
    bus = Image.open(BUILD / 'mula-1024.png').convert('RGBA')
    bus = bus.crop(bus.getchannel('A').getbbox())
    width = round(square * 0.8)
    bus = bus.resize((width, round(bus.height * width / bus.width)), Image.LANCZOS)
    x = (SIZE - bus.width) // 2
    y = o + round(square * 0.64) - bus.height // 2
    ground = Image.new('RGBA', (SIZE, SIZE), (0, 0, 0, 0))
    ImageDraw.Draw(ground).ellipse((x + bus.width * 0.08, y + bus.height * 0.86, x + bus.width * 0.92, y + bus.height * 1.04), fill=(0, 0, 0, 150))
    out.alpha_composite(ground.filter(ImageFilter.GaussianBlur(SIZE * 0.015)))
    out.alpha_composite(bus, (x, y))
    return out


def main() -> None:
    # macOS: Apple's grid keeps the tile at 824 of 1024.
    mac = icon(0.098)
    mac.save(BUILD / 'icon.png')
    with tempfile.TemporaryDirectory() as tmp:
        iconset = Path(tmp) / 'icon.iconset'
        iconset.mkdir()
        for size in (16, 32, 128, 256, 512):
            mac.resize((size, size), Image.LANCZOS).save(iconset / f'icon_{size}x{size}.png')
            mac.resize((size * 2, size * 2), Image.LANCZOS).save(iconset / f'icon_{size}x{size}@2x.png')
        if shutil.which('iconutil'):
            subprocess.run(['iconutil', '-c', 'icns', str(iconset), '-o', str(BUILD / 'icon.icns')], check=True)
        else:
            print('Sin iconutil (no es un Mac): icon.icns no se ha rehecho.', file=sys.stderr)
    # Windows: the tile fills more of the square, so it reads on the taskbar.
    icon(0.03).save(BUILD / 'icon.ico', sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print(f'Iconos en {BUILD}')


if __name__ == '__main__':
    main()
