import AppKit
import CoreText

// The app's icon with its version: the yellow badge top right that Arturo chose (1/11, .dev-snapshots/gameplay/
// icono-version/b-insignia.png), inside the tile, drawn at each size the system asks for. AppBuild runs it before every
// build (AppIcon):
//
//   swift unity/tools/icono/icono.swift <base.png> <font.ttf> <version> <out dir> <mac|windows> <size>…
//
// The base is the 0.10.1's (packages/desktop/scripts/icon.py, on macOS's grid: the tile 823 px of 1024, 100 in from
// the edge). Windows fills more of the square, as icon.py did (its margin 0.03), so it reads on the taskbar. Under 256
// px the badge grows, up to the tile's whole width, so the number still reads in the Dock and the Finder; each size is
// drawn at its own pixels, not shrunk from 1024. Writes <out dir>/icono-<size>.png.

let args = CommandLine.arguments

func fail(_ why: String) -> Never {
    FileHandle.standardError.write("icono: \(why)\n".data(using: .utf8)!)
    exit(1)
}

guard args.count >= 7 else { fail("uso: icono.swift <base.png> <fuente.ttf> <versión> <carpeta> <mac|windows> <tamaño>…") }
guard let base = NSImage(contentsOfFile: args[1]) else { fail("no se lee \(args[1])") }
guard let fontData = NSData(contentsOfFile: args[2]), let provider = CGDataProvider(data: fontData), let face = CGFont(provider) else {
    fail("no se lee \(args[2])")
}
let version = args[3]
let out = args[4]
let windows = args[5] == "windows"
let sizes = args[6...].map { Int($0) ?? 0 }
if sizes.contains(where: { $0 < 16 || $0 > 1024 }) { fail("tamaños de 16 a 1024: \(args[6...].joined(separator: " "))") }

let yellow = NSColor(calibratedRed: 0.95, green: 0.77, blue: 0.25, alpha: 1)
let ink = NSColor(calibratedRed: 0.08, green: 0.09, blue: 0.10, alpha: 1)

/// The base's tile, of 1024 and from the top: icon.py's macOS margin 0.098; on Windows, its 0.03.
let baseTile = CGRect(x: 100, y: 100, width: 823, height: 823)
let windowsTile = CGRect(x: 30, y: 30, width: 963, height: 963)

/// How much bigger than at 1024 the badge is drawn at `n` px: as chosen from 256 up, growing below.
func growth(_ n: Int) -> CGFloat {
    n >= 256 ? 1 : min(2.4, 1 + 0.45 * log2(256 / CGFloat(n)))
}

/// How much of its room either side of the number the badge keeps at `n` px: less when small, where the width is the
/// number's.
func room(_ n: Int) -> CGFloat {
    n >= 128 ? 1 : n >= 64 ? 0.6 : 0.3
}

func draw(_ n: Int) -> NSBitmapImageRep {
    let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: n, pixelsHigh: n, bitsPerSample: 8, samplesPerPixel: 4,
                               hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
    rep.size = NSSize(width: n, height: n)
    NSGraphicsContext.saveGraphicsState()
    let context = NSGraphicsContext(bitmapImageRep: rep)!
    NSGraphicsContext.current = context
    context.imageInterpolation = .high
    let cg = context.cgContext
    let size = CGFloat(n)
    let u = size / 1024
    // AppKit draws from the bottom; the layout above is from the top.
    func flip(_ r: CGRect) -> CGRect { CGRect(x: r.minX, y: size - r.maxY, width: r.width, height: r.height) }
    let tile: CGRect
    if windows {
        // The base's tile alone, bigger, with a shadow under it as icon.py drew.
        tile = CGRect(x: windowsTile.minX * u, y: windowsTile.minY * u, width: windowsTile.width * u, height: windowsTile.height * u)
        let to = flip(tile)
        let corner = to.width * 0.225
        NSGraphicsContext.saveGraphicsState()
        let shadow = NSShadow()
        shadow.shadowOffset = NSSize(width: 0, height: -12 * u)
        shadow.shadowBlurRadius = 24 * u
        shadow.shadowColor = NSColor(white: 0, alpha: 0.43)
        shadow.set()
        ink.setFill()
        NSBezierPath(roundedRect: to, xRadius: corner, yRadius: corner).fill()
        NSGraphicsContext.restoreGraphicsState()
        NSGraphicsContext.saveGraphicsState()
        NSBezierPath(roundedRect: to, xRadius: corner, yRadius: corner).addClip()
        let k = base.size.width / 1024
        let from = CGRect(x: baseTile.minX * k, y: (1024 - baseTile.maxY) * k, width: baseTile.width * k, height: baseTile.height * k)
        base.draw(in: to, from: from, operation: .sourceOver, fraction: 1)
        NSGraphicsContext.restoreGraphicsState()
    } else {
        tile = CGRect(x: baseTile.minX * u, y: baseTile.minY * u, width: baseTile.width * u, height: baseTile.height * u)
        base.draw(in: CGRect(x: 0, y: 0, width: size, height: size))
    }

    // The badge, inside the tile's top right corner: the mock-up's stood out over the corner, and macOS 26 shows an icon
    // whose shape isn't its tile shrunk inside a grey frame (1/11). At 1024, 170 high with a 118 px number, 34 either side
    // of it, 56 in from the tile's top and right (clear of its rounded corner). Grown under 256, nearer the edges, and
    // shrunk to fit the tile if it then doesn't; clipped to the tile all the same.
    var g = growth(n) * u
    let line = { (scale: CGFloat) -> CTLine in
        let font = CTFontCreateWithGraphicsFont(face, 118 * scale, nil, nil)
        let text = NSAttributedString(string: version, attributes: [
            NSAttributedString.Key(kCTFontAttributeName as String): font,
            NSAttributedString.Key(kCTForegroundColorAttributeName as String): ink.cgColor,
        ])
        return CTLineCreateWithAttributedString(text)
    }
    var number = line(g)
    let stroke = max(1, (10 * g).rounded())
    let side = n >= 256 ? 56 * u : max(1, 0.04 * tile.width)
    let above = n >= 256 ? 56 * u : 0.1 * tile.width
    let fits = tile.width - 2 * side - stroke
    let width = { (l: CTLine, scale: CGFloat) in CGFloat(CTLineGetTypographicBounds(l, nil, nil, nil)) + 2 * 34 * room(n) * scale }
    if width(number, g) > fits {
        g *= fits / width(number, g)
        number = line(g)
    }
    let w = width(number, g)
    let h = 170 * g
    // Whole pixels, so its edges and the number's foot fall sharp at the small sizes.
    let badge = flip(CGRect(x: (tile.maxX - side - stroke / 2 - w).rounded(), y: (tile.minY + above + stroke / 2).rounded(), width: w.rounded(), height: h.rounded()))
    let to = flip(tile)
    NSBezierPath(roundedRect: to, xRadius: to.width * 0.225, yRadius: to.width * 0.225).addClip()
    let corner = 44 * g
    let shape = NSBezierPath(roundedRect: badge, xRadius: corner, yRadius: corner)
    yellow.setFill()
    shape.fill()
    ink.setStroke()
    shape.lineWidth = stroke
    shape.stroke()
    // The number centred by its ink, not by its line: the digits have no descenders.
    cg.textMatrix = .identity
    let inked = CTLineGetImageBounds(number, cg)
    cg.textPosition = CGPoint(x: (badge.midX - inked.midX).rounded(), y: (badge.midY - inked.midY).rounded())
    CTLineDraw(number, cg)

    NSGraphicsContext.restoreGraphicsState()
    return rep
}

for n in sizes {
    let file = URL(fileURLWithPath: out).appendingPathComponent("icono-\(n).png")
    guard let png = draw(n).representation(using: .png, properties: [:]) else { fail("no sale el png de \(n)") }
    do {
        try png.write(to: file)
    } catch {
        fail("no se escribe \(file.path): \(error)")
    }
}
