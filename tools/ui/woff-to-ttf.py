# WOFF (1.0) to TTF with nothing but the standard library (docs/UNITY-UI.md §4: Unity's Font Assets are made from
# TTF or OTF, and the game's two OFL families, Oswald and Barlow, are in the repo only as @fontsource's .woff files).
# WOFF 1.0 is the same sfnt with each table zlib-compressed: undone table by table, with the sfnt header and the
# table directory written again (offsets, padding to 4 bytes; the checksums are kept as they came).
#
#   python3 tools/ui/woff-to-ttf.py node_modules/@fontsource/oswald/files/oswald-latin-600-normal.woff Oswald-SemiBold.ttf
import struct
import sys
import zlib


def woff_to_ttf(data: bytes) -> bytes:
    signature, flavor, _length, num_tables = struct.unpack('>4sIIH', data[:14])
    if signature != b'wOFF':
        raise SystemExit('not a WOFF 1.0 file (WOFF2 needs fontTools)')
    tables = []
    for i in range(num_tables):
        tag, offset, comp_length, orig_length, checksum = struct.unpack('>4sIIII', data[44 + i * 20 : 64 + i * 20])
        raw = data[offset : offset + comp_length]
        table = zlib.decompress(raw) if comp_length < orig_length else raw
        if len(table) != orig_length:
            raise SystemExit(f'table {tag!r}: {len(table)} bytes, expected {orig_length}')
        tables.append((tag, checksum, table))
    tables.sort(key=lambda t: t[0])
    # The sfnt header's search fields, from the number of tables.
    power = 1
    exponent = 0
    while power * 2 <= num_tables:
        power *= 2
        exponent += 1
    search_range = power * 16
    header = struct.pack('>IHHHH', flavor, num_tables, search_range, exponent, num_tables * 16 - search_range)
    offset = 12 + num_tables * 16
    directory = b''
    body = b''
    for tag, checksum, table in tables:
        directory += struct.pack('>4sIII', tag, checksum, offset + len(body), len(table))
        body += table + b'\0' * (-len(table) % 4)
    return header + directory + body


if __name__ == '__main__':
    src, dst = sys.argv[1], sys.argv[2]
    with open(src, 'rb') as f:
        ttf = woff_to_ttf(f.read())
    with open(dst, 'wb') as f:
        f.write(ttf)
    print('ok', dst, len(ttf), 'bytes')
