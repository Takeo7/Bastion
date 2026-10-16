# The supplies' sign (playtest 16/10 #4, Arturo: «el dinero no parece dinero»): a font of one glyph, U+00A7
# «§» drawn as the counter's crate (our own drawing, after mh-crate's silhouette: a lid and a body, three
# latches across the seam, two feet). ui/themes/mixta.css puts this family first for that character only
# (unicode-range), so every «§» in a text, a button or the log reads as the crate.
#
#   pip install fonttools brotli
#   python tools/ui/suministros-font.py packages/client/src/ui/themes/suministros.woff2
import sys
from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen

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

fb = FontBuilder(1000, isTTF=True)
fb.setupGlyphOrder(['.notdef', 'section'])
fb.setupCharacterMap({0xA7: 'section'})
empty = TTGlyphPen(None).glyph()
pen = TTGlyphPen(None)
lid(pen)
body(pen)
fb.setupGlyf({'.notdef': empty, 'section': pen.glyph()})
fb.setupHorizontalMetrics({'.notdef': (500, 0), 'section': (X1 + X0, X0)})
fb.setupHorizontalHeader(ascent=800, descent=-200)
fb.setupNameTable({'familyName': 'Bastion Suministros', 'styleName': 'Regular'})
fb.setupOS2(sTypoAscender=800, sTypoDescender=-200, usWinAscent=800, usWinDescent=200)
fb.setupPost()
fb.font.flavor = 'woff2'
fb.save(out)
print('ok', out)
