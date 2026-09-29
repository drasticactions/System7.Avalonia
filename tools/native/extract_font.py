import argparse
import io
from pathlib import Path
import struct

from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
import machfs
import rsrcfork


def main():
    parser = argparse.ArgumentParser(description='Extract one bitmap font size from the supplied System 7 disk.')
    parser.add_argument('image', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--family', default='Chicago', help='FOND name, for example Chicago or Monaco')
    parser.add_argument('--size', type=int, default=12, help='point size of the plain NFNT')
    parser.add_argument('--name', help='family name in the TrueType file')
    parser.add_argument('--bold', action='store_true', help='also write a Bold face, overstruck one pixel right at the same advance')
    args = parser.parse_args()
    volume = machfs.Volume()
    volume.read(args.image.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    fond = next(resource.data for resource in resources[b'FOND'].values()
                if resource.name == args.family.encode('mac_roman'))
    associations = [struct.unpack_from('>HHH', fond, 54 + 6 * i)
                    for i in range(struct.unpack_from('>H', fond, 52)[0] + 1)]
    font_id = next(resource for size, style, resource in associations if size == args.size and style == 0)
    data = resources[b'NFNT'][font_id].data
    _, first, last, _, kern, _, _, height, offset, ascent, descent, leading, row_words = struct.unpack_from('>13h', data)
    stem = f'{args.family}{args.size}'
    family = args.name or ('System Seven Chicago' if stem == 'Chicago12' else f'System Seven {args.family} {args.size}')
    postscript = family.replace(' ', '')
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / f'{stem}.nfnt').write_bytes(data)
    stride = row_words * 2
    locations = 26 + stride * height
    widths = 16 + offset * 2
    for bold in [False, True] if args.bold else [False]:
        font = FontBuilder(args.size * 64, isTTF=True)
        glyphs, metrics, cmap = {}, {}, {}
        for code in [last + 1] + list(range(first, last + 1)):
            index = code - first
            left, right = struct.unpack_from('>HH', data, locations + index * 2)
            bearing, advance = struct.unpack_from('>bB', data, widths + index * 2)
            if advance == 255:
                continue
            name = '.notdef' if code == last + 1 else f'mac{code:02x}'
            pen = TTGlyphPen(None)
            for y in range(height):
                row = [bool(data[26 + y * stride + x // 8] & (128 >> (x % 8))) for x in range(left, right)]
                if bold:
                    row = [on or (i > 0 and row[i - 1]) for i, on in enumerate(row + [False])]
                for x, on in enumerate(row):
                    if on:
                        x0 = (x + bearing + kern) * 64
                        y0 = (ascent - y - 1) * 64
                        pen.moveTo((x0, y0))
                        pen.lineTo((x0, y0 + 64))
                        pen.lineTo((x0 + 64, y0 + 64))
                        pen.lineTo((x0 + 64, y0))
                        pen.closePath()
            glyphs[name] = pen.glyph()
            metrics[name] = (advance * 64, (bearing + kern) * 64)
            if code <= last:
                cmap[ord(bytes([code]).decode('mac_roman'))] = name
        style = 'Bold' if bold else 'Regular'
        font.setupGlyphOrder(list(glyphs))
        font.setupCharacterMap(cmap)
        font.setupGlyf(glyphs)
        font.setupHorizontalMetrics(metrics)
        font.setupHorizontalHeader(ascent=ascent * 64, descent=-descent * 64, lineGap=leading * 64)
        font.setupNameTable({'familyName': family, 'styleName': style,
                            'uniqueFontIdentifier': f'{family} {args.size}' if stem == 'Chicago12' else f'{family} {style}',
                            'fullName': family if not bold else f'{family} Bold', 'psName': postscript + ('-Bold' if bold else '')})
        font.setupOS2(sTypoAscender=ascent * 64, sTypoDescender=-descent * 64,
                     sTypoLineGap=leading * 64, usWinAscent=ascent * 64, usWinDescent=descent * 64,
                     usWeightClass=700 if bold else 400, fsSelection=0x20 if bold else 0x40)
        font.setupPost(underlinePosition=-96, underlineThickness=64)
        font.updateHead(macStyle=1 if bold else 0)
        font.setupMaxp()
        font.save(args.output / f'{stem}{style if bold else ""}.ttf')
    print(f'Extracted NFNT {font_id}: ascent {ascent}, descent {descent}, leading {leading}.')


if __name__ == '__main__':
    main()
