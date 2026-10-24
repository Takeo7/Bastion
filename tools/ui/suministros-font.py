# The supplies' sign (playtest 16/10 #4, Arturo: «el dinero no parece dinero»): a font of one glyph, U+00A7
# «§» drawn as the counter's crate (our own drawing, after mh-crate's silhouette: a lid and a body, three
# latches across the seam, two feet). ui/themes/mixta.css puts this family first for that character only
# (unicode-range), so every «§» in a text, a button or the log reads as the crate.
#
#   pip install fonttools brotli
#   python tools/ui/suministros-font.py packages/client/src/ui/themes/suministros.woff2
#
# For Unity (docs/UNITY-UI.md §6: its Font Asset is made from a TTF), the same glyph as a .ttf. Without fontTools
# (it isn't installed here, and installing it is a download), a .ttf is written with the standard library alone:
#
#   python3 tools/ui/suministros-font.py unity/Bastion/Assets/Bastion/UI/Fonts/BastionSuministros.ttf
import struct
import sys

out = sys.argv[1]
# Sized with the figures (Barlow's are 700 tall), the seam and latches thick enough to show at 13 px.
X0, X1, B = 60, 940, 40
TOP, LID, SEAM = 640, 430, 375
LATCHES = [215, 500, 785]
HALF, REACH = 58, 45
FEET = [(300, 390), (610, 700)]
FOOT = 60

def lid(pen):
    pts = [(X0, LID), (X0, TOP - B), (X0 + B, TOP), (X1 - B, TOP), (X1, TOP - B), (X1, LID)]
    for c in reversed(LATCHES):
        pts += [(c + HALF, LID), (c + HALF, LID + REACH), (c - HALF, LID + REACH), (c - HALF, LID)]
    draw(pen, pts)

def body(pen):
    pts = [(X0, B), (X0, SEAM)]
    for c in LATCHES:
        pts += [(c - HALF, SEAM), (c - HALF, SEAM - REACH), (c + HALF, SEAM - REACH), (c + HALF, SEAM)]
    pts += [(X1, SEAM), (X1, B), (X1 - B, 0)]
    for a, b in reversed(FEET):
        pts += [(b, 0), (b, FOOT), (a, FOOT), (a, 0)]
    pts += [(X0 + B, 0)]
    draw(pen, pts)

def draw(pen, pts):
    pen.moveTo(pts[0])
    for p in pts[1:]:
        pen.lineTo(p)
    pen.closePath()

class Outline:
    """A pen that keeps the contours as lists of points (fontTools' pen calls)."""

    def __init__(self):
        self.contours = []

    def moveTo(self, p):
        self.contours.append([p])

    def lineTo(self, p):
        self.contours[-1].append(p)

    def closePath(self):
        pass


crate = Outline()
lid(crate)
body(crate)
ADVANCE = X1 + X0


def with_fonttools():
    from fontTools.fontBuilder import FontBuilder
    from fontTools.pens.ttGlyphPen import TTGlyphPen

    fb = FontBuilder(1000, isTTF=True)
    fb.setupGlyphOrder(['.notdef', 'section'])
    fb.setupCharacterMap({0xA7: 'section'})
    empty = TTGlyphPen(None).glyph()
    pen = TTGlyphPen(None)
    for contour in crate.contours:
        draw(pen, contour)
    fb.setupGlyf({'.notdef': empty, 'section': pen.glyph()})
    fb.setupHorizontalMetrics({'.notdef': (500, 0), 'section': (ADVANCE, X0)})
    fb.setupHorizontalHeader(ascent=800, descent=-200)
    fb.setupNameTable({'familyName': 'Bastion Suministros', 'styleName': 'Regular'})
    fb.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
    fb.setupPost()
    if out.endswith('.woff2'):
        fb.font.flavor = 'woff2'
    fb.save(out)


def checksum(data):
    data += b'\0' * (-len(data) % 4)
    return sum(struct.unpack(f'>{len(data) // 4}I', data)) & 0xFFFFFFFF


def ttf_by_hand():
    """The same font with struct alone: two glyphs (.notdef empty, the crate), one cmap entry, the tables a TTF needs."""
    xs = [x for c in crate.contours for x, _ in c]
    ys = [y for c in crate.contours for _, y in c]
    box = (min(xs), min(ys), max(xs), max(ys))
    points = sum(len(c) for c in crate.contours)
    # The crate: its contours' ends, no instructions, every point on the curve, coordinates as deltas (int16 each).
    glyph = struct.pack('>hhhhh', len(crate.contours), *box)
    end = -1
    for c in crate.contours:
        end += len(c)
        glyph += struct.pack('>H', end)
    glyph += struct.pack('>H', 0) + bytes([1]) * points
    flat = [p for c in crate.contours for p in c]
    for axis in (0, 1):
        last = 0
        for p in flat:
            glyph += struct.pack('>h', p[axis] - last)
            last = p[axis]
    glyph += b'\0' * (-len(glyph) % 4)
    tables = {}
    tables[b'glyf'] = glyph
    tables[b'loca'] = struct.pack('>III', 0, 0, len(glyph))
    tables[b'head'] = struct.pack('>IIIIHHqqhhhhHHhhh', 0x00010000, 0x00010000, 0, 0x5F0F3CF5, 0x000B, 1000, 0, 0, *box, 0, 8, 2, 1, 0)
    tables[b'hhea'] = struct.pack('>Ihhh' + 'Hhhh' + 'hhh' + 'hhhh' + 'hH', 0x00010000, 800, -200, 0,
                                  ADVANCE, 0, X0, box[2], 1, 0, 0, 0, 0, 0, 0, 0, 2)
    tables[b'hmtx'] = struct.pack('>HhHh', 500, 0, ADVANCE, X0)
    tables[b'maxp'] = struct.pack('>IHHHHHHHHHHHHHH', 0x00010000, 2, points, len(crate.contours), 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0)
    # cmap: format 4, «§» to glyph 1 and the closing segment, under Unicode (0, 3) and Windows (3, 1).
    segments = struct.pack('>HH', 0xA7, 0xFFFF) + b'\0\0' + struct.pack('>HH', 0xA7, 0xFFFF) + struct.pack('>hh', 1 - 0xA7, 1) + struct.pack('>HH', 0, 0)
    # Two segments: segCountX2 4, searchRange 4, entrySelector 1, rangeShift 0.
    sub = struct.pack('>HHHHHHH', 4, 14 + len(segments), 0, 4, 4, 1, 0) + segments
    tables[b'cmap'] = struct.pack('>HHHHIHHI', 0, 2, 0, 3, 20, 3, 1, 20) + sub
    names = [(1, 'Bastion Suministros'), (2, 'Regular'), (4, 'Bastion Suministros'), (6, 'BastionSuministros-Regular')]
    strings = b''
    records = b''
    for name_id, text in names:
        data = text.encode('utf-16-be')
        records += struct.pack('>HHHHHH', 3, 1, 0x409, name_id, len(data), len(strings))
        strings += data
    tables[b'name'] = struct.pack('>HHH', 0, len(names), 6 + 12 * len(names)) + records + strings
    tables[b'OS/2'] = struct.pack('>HhHHH' + 'hhhhhhhhhh' + 'h' + '10s' + 'IIII' + '4s' + 'HHH' + 'hhh' + 'HH' + 'II' + 'hhHHH',
                                  4, ADVANCE, 400, 5, 0,
                                  650, 600, 0, 75, 650, 600, 0, 350, 50, 300,
                                  0, b'\0' * 10, 0b10, 0, 0, 0, b'BSTN',
                                  0x40, 0xA7, 0xA7, 800, -200, 0, 800, 200, 1, 0,
                                  500, 700, 0, 32, 1)
    tables[b'post'] = struct.pack('>IIhhIIIII', 0x00030000, 0, -100, 50, 0, 0, 0, 0, 0)
    # The sfnt: its header, the directory by tag, each table on 4 bytes; then head's checksum adjustment.
    tags = sorted(tables)
    n = len(tags)
    power = 1 << (n.bit_length() - 1)
    header = struct.pack('>IHHHH', 0x00010000, n, power * 16, power.bit_length() - 1, n * 16 - power * 16)
    offset = 12 + 16 * n
    directory = b''
    body = b''
    where = {}
    for tag in tags:
        data = tables[tag]
        where[tag] = offset + len(body)
        directory += struct.pack('>4sIII', tag, checksum(data), where[tag], len(data))
        body += data + b'\0' * (-len(data) % 4)
    font = bytearray(header + directory + body)
    # checkSumAdjustment: the third field of head.
    struct.pack_into('>I', font, where[b'head'] + 8, (0xB1B0AFBA - checksum(bytes(font))) & 0xFFFFFFFF)
    with open(out, 'wb') as f:
        f.write(font)


try:
    with_fonttools()
except ImportError:
    if not out.endswith('.ttf'):
        sys.exit('fontTools is needed for ' + out + ' (pip install fonttools brotli); a .ttf is written without it')
    ttf_by_hand()
print('ok', out)
