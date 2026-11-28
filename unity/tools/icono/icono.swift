import AppKit
import CoreText

// The app's icon, its version's number over all of it (Arturo, 28/11, note 1, with his picture: «el "13.1" grande,
// centrado y en relieve, ocupando casi todo el icono», «rojo con degradado o textura sucia, de estética de terror»,
// «un fondo negro texturizado, esquinas redondeadas y un borde metálico fino»). All of it drawn here, the dirt too (a
// noise of ours, the same at every run: no picture from anywhere). AppBuild runs it before every build (AppIcon):
//
//   swift unity/tools/icono/icono.swift <base.png> <font.ttf> <number> <out dir> <mac|windows|ios> <size>…
//
// <number> is the version without its «0.» («14.15»: AppIcon takes it off). <base.png> is no longer drawn (it was the
// bus's, till the 0.14.14): only its folder is read, for Oswald-Bold.ttf beside it (OFL; <font.ttf>, the game's
// SemiBold, if it isn't there). The tile is on macOS's grid, 823 px of 1024 and 100 in from the edge; Windows fills
// more of the square (963), so it reads on the taskbar; iOS's is the tile alone filling the square, opaque (iOS rounds
// the corners itself: Arturo, 7/11). Each size is drawn at its own pixels, not shrunk from 1024: under 128 px without
// the scratches, and at 32 px or less the number in two rows («14» over «15», no dot), each digit twice as big, so
// that something of it is read in the Finder's lists. Writes <out dir>/icono-<size>.png.

let args = CommandLine.arguments

func fail(_ why: String) -> Never {
    FileHandle.standardError.write("icono: \(why)\n".data(using: .utf8)!)
    exit(1)
}

guard args.count >= 7 else { fail("uso: icono.swift <base.png> <fuente.ttf> <número> <carpeta> <mac|windows|ios> <tamaño>…") }
let bold = URL(fileURLWithPath: args[1]).deletingLastPathComponent().appendingPathComponent("Oswald-Bold.ttf").path
let fontFile = FileManager.default.fileExists(atPath: bold) ? bold : args[2]
guard let fontData = NSData(contentsOfFile: fontFile), let provider = CGDataProvider(data: fontData), let face = CGFont(provider) else {
    fail("no se lee \(fontFile)")
}
let version = args[3]
let out = args[4]
let windows = args[5] == "windows"
let ios = args[5] == "ios"
let sizes = args[6...].map { Int($0) ?? 0 }
if sizes.contains(where: { $0 < 16 || $0 > 1024 }) { fail("tamaños de 16 a 1024: \(args[6...].joined(separator: " "))") }

let ink = NSColor(calibratedRed: 0.05, green: 0.05, blue: 0.055, alpha: 1)

/// The tile, of 1024 and from the top: macOS's margin; on Windows, less.
let macTile = CGRect(x: 100, y: 100, width: 823, height: 823)
let windowsTile = CGRect(x: 30, y: 30, width: 963, height: 963)

/// The same numbers at every run (an icon made twice is the same file).
struct Dice {
    var state: UInt64
    mutating func next() -> CGFloat {
        state = state &* 6364136223846793005 &+ 1442695040888963407
        return CGFloat((state >> 33) & 0xFFFFFF) / CGFloat(0xFFFFFF)
    }
}

/// A grey noise `cells` across, each cell a random grey: drawn stretched and smoothed over one another at several
/// sizes, it is the dirt.
func noise(_ cells: Int, _ seed: UInt64) -> CGImage {
    var dice = Dice(state: seed)
    var bytes = [UInt8](repeating: 0, count: cells * cells)
    for k in 0..<bytes.count { bytes[k] = UInt8(dice.next() * 255) }
    let data = CGDataProvider(data: Data(bytes) as CFData)!
    return CGImage(width: cells, height: cells, bitsPerComponent: 8, bitsPerPixel: 8, bytesPerRow: cells, space: CGColorSpaceCreateDeviceGray(),
                   bitmapInfo: CGBitmapInfo(rawValue: CGImageAlphaInfo.none.rawValue), provider: data, decode: nil, shouldInterpolate: true, intent: .defaultIntent)!
}

/// The number's outline, `size` px high its font, its ink's bottom left at the origin.
func outline(_ text: String, _ size: CGFloat) -> CGPath {
    let font = CTFontCreateWithGraphicsFont(face, size, nil, nil)
    let line = CTLineCreateWithAttributedString(NSAttributedString(string: text, attributes: [NSAttributedString.Key(kCTFontAttributeName as String): font]))
    let path = CGMutablePath()
    for run in CTLineGetGlyphRuns(line) as! [CTRun] {
        let count = CTRunGetGlyphCount(run)
        var glyphs = [CGGlyph](repeating: 0, count: count)
        var places = [CGPoint](repeating: .zero, count: count)
        CTRunGetGlyphs(run, CFRange(location: 0, length: count), &glyphs)
        CTRunGetPositions(run, CFRange(location: 0, length: count), &places)
        for k in 0..<count {
            guard let glyph = CTFontCreatePathForGlyph(font, glyphs[k], nil) else { continue }
            path.addPath(glyph, transform: CGAffineTransform(translationX: places[k].x, y: places[k].y))
        }
    }
    let box = path.boundingBoxOfPath
    var move = CGAffineTransform(translationX: -box.minX, y: -box.minY)
    return path.copy(using: &move) ?? path
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
    let from = windows ? windowsTile : macTile
    // (AppKit draws from the bottom; the tile is square and centred, so it is where it is from the top.)
    let tile = CGRect(x: from.minX * u, y: from.minY * u, width: from.width * u, height: from.height * u)
    let t = tile.width / 823
    let corner = tile.width * 0.225
    let shape = CGPath(roundedRect: tile, cornerWidth: corner, cornerHeight: corner, transform: nil)
    let rgb = CGColorSpaceCreateDeviceRGB()

    // The tile: a shadow under it (Windows has none of its own), near black, lighter up the middle as under a lamp,
    // and the dirt over it.
    if windows {
        cg.saveGState()
        cg.setShadow(offset: CGSize(width: 0, height: -12 * u), blur: 24 * u, color: CGColor(gray: 0, alpha: 0.43))
        cg.addPath(shape)
        cg.setFillColor(ink.cgColor)
        cg.fillPath()
        cg.restoreGState()
    }
    cg.saveGState()
    cg.addPath(shape)
    cg.clip()
    cg.setFillColor(ink.cgColor)
    cg.fill(tile)
    let lamp = CGGradient(colorsSpace: rgb, colors: [CGColor(gray: 0.17, alpha: 1), CGColor(gray: 0.09, alpha: 1), CGColor(gray: 0.045, alpha: 1)] as CFArray, locations: [0, 0.55, 1])!
    cg.drawRadialGradient(lamp, startCenter: CGPoint(x: tile.midX, y: tile.maxY - tile.height * 0.22), startRadius: 0,
                          endCenter: CGPoint(x: tile.midX, y: tile.midY), endRadius: tile.width * 0.78, options: [.drawsAfterEndLocation])
    cg.interpolationQuality = .high
    for (cells, alpha, seed) in [(6, 0.14, 11), (23, 0.1, 12), (90, 0.09, 13), (340, 0.1, 14)] as [(Int, CGFloat, UInt64)] {
        if CGFloat(cells) > tile.width { continue }
        cg.setBlendMode(.softLight)
        cg.setAlpha(alpha * 2)
        cg.draw(noise(cells, seed), in: tile)
    }
    cg.setBlendMode(.normal)
    cg.setAlpha(1)
    cg.restoreGState()

    // The number: all of the tile's width but a margin; in two rows at the small sizes.
    let rows: [String]
    if n <= 32, let dot = version.firstIndex(of: "."), version.count > 3 { rows = [String(version[..<dot]), String(version[version.index(after: dot)...])] }
    else { rows = [version] }
    let across = tile.width * (n <= 32 ? 0.78 : n < 128 ? 0.84 : 0.82)
    let tall = tile.height * (rows.count > 1 ? 0.74 : 0.6)
    let gap = rows.count > 1 ? max(1, (tile.height * 0.07).rounded()) : 0
    var paths = rows.map { outline($0, 400) }
    // (The same height for every row; stretched a little, so that it fills as the picture's does.)
    let widest = paths.map { $0.boundingBoxOfPath.width }.max() ?? 1
    let high = paths.map { $0.boundingBoxOfPath.height }.max() ?? 1
    let rowTall = (tall - gap * CGFloat(rows.count - 1)) / CGFloat(rows.count)
    var k = across / widest
    var ky = rowTall / high
    // (Never taller than 1.35 its own shape, nor wider than 1.5.)
    ky = min(ky, k * 1.35)
    k = min(k, ky * 1.5)
    let stretch = ky / k
    let depth = n < 48 ? 0 : max(1, (16 * t).rounded())
    let whole = high * k * stretch * CGFloat(rows.count) + gap * CGFloat(rows.count - 1)
    var top = (tile.midY + whole / 2 + depth / 2).rounded()
    for i in 0..<paths.count {
        let box = paths[i].boundingBoxOfPath
        var place = CGAffineTransform(translationX: (tile.midX - box.width * k / 2).rounded(), y: top - high * k * stretch).scaledBy(x: k, y: k * stretch)
        paths[i] = paths[i].copy(using: &place) ?? paths[i]
        top -= high * k * stretch + gap
    }
    let number = CGMutablePath()
    for p in paths { number.addPath(p) }
    let box = number.boundingBoxOfPath

    cg.saveGState()
    cg.addPath(shape)
    cg.clip()
    // Its shadow on the tile, and its side going down from it (the relief).
    cg.saveGState()
    cg.setShadow(offset: CGSize(width: 0, height: -(depth + 10 * t)), blur: 26 * t, color: CGColor(gray: 0, alpha: 0.85))
    cg.addPath(number)
    cg.setFillColor(CGColor(red: 0.2, green: 0.02, blue: 0.01, alpha: 1))
    cg.fillPath()
    cg.restoreGState()
    var d = depth
    while d >= 1 {
        let shade = 0.16 + 0.2 * (1 - d / max(1, depth))
        cg.saveGState()
        cg.translateBy(x: 0, y: -d)
        cg.addPath(number)
        cg.setFillColor(CGColor(red: shade, green: shade * 0.1, blue: shade * 0.06, alpha: 1))
        cg.fillPath()
        cg.restoreGState()
        d -= 1
    }
    // Its face: red, brighter above, with the dirt, the blotches and the scratches.
    cg.saveGState()
    cg.addPath(number)
    cg.clip()
    let red = CGGradient(colorsSpace: rgb, colors: [CGColor(red: 0.96, green: 0.2, blue: 0.08, alpha: 1), CGColor(red: 0.82, green: 0.1, blue: 0.05, alpha: 1), CGColor(red: 0.55, green: 0.05, blue: 0.03, alpha: 1)] as CFArray, locations: [0, 0.55, 1])!
    cg.drawLinearGradient(red, start: CGPoint(x: 0, y: box.maxY), end: CGPoint(x: 0, y: box.minY), options: [.drawsBeforeStartLocation, .drawsAfterEndLocation])
    if n >= 64 {
        cg.interpolationQuality = .high
        for (cells, alpha, seed, mode) in [(9, 0.5, 21, CGBlendMode.softLight), (31, 0.3, 22, .softLight), (120, 0.3, 23, .softLight), (420, 0.3, 24, .overlay)] as [(Int, CGFloat, UInt64, CGBlendMode)] {
            if CGFloat(cells) > tile.width { continue }
            cg.setBlendMode(mode)
            cg.setAlpha(alpha)
            cg.draw(noise(cells, seed), in: tile)
        }
        cg.setBlendMode(.normal)
        cg.setAlpha(1)
    }
    if n >= 128 {
        // Scratches: short strokes in bunches, lighter than the red, and a few dark ones.
        var dice = Dice(state: 77)
        cg.setLineCap(.round)
        for _ in 0..<46 {
            let at = CGPoint(x: tile.minX + dice.next() * tile.width, y: box.minY + dice.next() * box.height)
            let turn = dice.next() * .pi
            let light = dice.next() > 0.25
            for _ in 0..<(3 + Int(dice.next() * 6)) {
                let angle = turn + (dice.next() - 0.5) * 0.9
                let long = (8 + dice.next() * 44) * t
                let a = CGPoint(x: at.x + (dice.next() - 0.5) * 30 * t, y: at.y + (dice.next() - 0.5) * 30 * t)
                cg.move(to: a)
                cg.addLine(to: CGPoint(x: a.x + cos(angle) * long, y: a.y + sin(angle) * long))
                cg.setLineWidth(max(0.6, (0.8 + dice.next() * 1.6) * t))
                cg.setStrokeColor(light ? CGColor(red: 1, green: 0.62, blue: 0.42, alpha: 0.2 + dice.next() * 0.3) : CGColor(red: 0.25, green: 0.02, blue: 0.01, alpha: 0.25 + dice.next() * 0.3))
                cg.strokePath()
            }
        }
    }
    // The bevel: light along the edges that look up, dark along the ones that look down.
    if n >= 48 {
        let edge = max(1, (7 * t).rounded())
        let around = CGMutablePath()
        around.addRect(tile.insetBy(dx: -50, dy: -50))
        around.addPath(number)
        for (dy, colour) in [(-edge, CGColor(red: 1, green: 0.72, blue: 0.5, alpha: 0.55)), (edge, CGColor(red: 0.2, green: 0, blue: 0, alpha: 0.6))] as [(CGFloat, CGColor)] {
            cg.saveGState()
            cg.translateBy(x: dy * 0.35, y: dy)
            cg.addPath(around)
            cg.setFillColor(colour)
            cg.fillPath(using: .evenOdd)
            cg.restoreGState()
        }
    }
    cg.restoreGState()
    if n >= 128 {
        cg.addPath(number)
        cg.setStrokeColor(CGColor(red: 0.16, green: 0.01, blue: 0, alpha: 0.9))
        cg.setLineWidth(max(1, 2 * t))
        cg.strokePath()
    }
    cg.restoreGState()

    // The thin metal edge, a little inside the tile: lighter above and below right, as a bent strip takes the light.
    let width = max(1, (4 * t).rounded())
    // (At the smallest it would be a tenth of the tile: none.)
    if n < 24 { NSGraphicsContext.restoreGraphicsState(); return rep }
    let inset = tile.insetBy(dx: width / 2, dy: width / 2)
    let edgeCorner = corner - width / 2
    cg.saveGState()
    cg.addPath(CGPath(roundedRect: inset, cornerWidth: edgeCorner, cornerHeight: edgeCorner, transform: nil))
    cg.setLineWidth(width)
    cg.replacePathWithStrokedPath()
    cg.clip()
    let metal = CGGradient(colorsSpace: rgb, colors: [CGColor(gray: 0.93, alpha: 1), CGColor(gray: 0.5, alpha: 1), CGColor(gray: 0.78, alpha: 1), CGColor(gray: 0.36, alpha: 1), CGColor(gray: 0.82, alpha: 1)] as CFArray, locations: [0, 0.3, 0.52, 0.78, 1])!
    cg.drawLinearGradient(metal, start: CGPoint(x: tile.minX, y: tile.maxY), end: CGPoint(x: tile.maxX, y: tile.minY), options: [])
    cg.restoreGState()

    NSGraphicsContext.restoreGraphicsState()
    return rep
}

/// iOS's at `n` px: the Mac's drawn bigger, so that its tile comes out at `n`, and the tile cut out over ink, so no corner
/// is left see-through (iOS's mask is the tile's own rounding, near enough).
func fullBleed(_ n: Int) -> NSBitmapImageRep {
    let m = Int((CGFloat(n) * 1024 / macTile.width).rounded())
    let mac = draw(m)
    let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: n, pixelsHigh: n, bitsPerSample: 8, samplesPerPixel: 4,
                               hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
    rep.size = NSSize(width: n, height: n)
    NSGraphicsContext.saveGraphicsState()
    let context = NSGraphicsContext(bitmapImageRep: rep)!
    NSGraphicsContext.current = context
    context.imageInterpolation = .high
    ink.setFill()
    NSRect(x: 0, y: 0, width: n, height: n).fill()
    let k = CGFloat(m) / 1024
    let from = NSRect(x: macTile.minX * k, y: macTile.minY * k, width: macTile.width * k, height: macTile.height * k)
    mac.draw(in: NSRect(x: 0, y: 0, width: n, height: n), from: from, operation: .sourceOver, fraction: 1, respectFlipped: false, hints: nil)
    NSGraphicsContext.restoreGraphicsState()
    return rep
}

for n in sizes {
    let file = URL(fileURLWithPath: out).appendingPathComponent("icono-\(n).png")
    guard let png = (ios ? fullBleed(n) : draw(n)).representation(using: .png, properties: [:]) else { fail("no sale el png de \(n)") }
    do {
        try png.write(to: file)
    } catch {
        fail("no se escribe \(file.path): \(error)")
    }
}
