import argparse
import io
from pathlib import Path
import struct

from fontTools.fontBuilder import FontBuilder
from fontTools.pens.ttGlyphPen import TTGlyphPen
import machfs
import rsrcfork


def main():
    parser = argparse.ArgumentParser(description='Extract Chicago 12 from the supplied System 7 disk.')
    parser.add_argument('image', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    volume = machfs.Volume()
    volume.read(args.image.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    fond = resources[b'FOND'][0].data
    associations = [struct.unpack_from('>HHH', fond, 54 + 6 * i)
                    for i in range(struct.unpack_from('>H', fond, 52)[0] + 1)]
    font_id = next(resource for size, style, resource in associations if size == 12 and style == 0)
    data = resources[b'NFNT'][font_id].data
    _, first, last, _, kern, _, _, height, offset, ascent, descent, leading, row_words = struct.unpack_from('>13h', data)
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / 'Chicago12.nfnt').write_bytes(data)
    stride = row_words * 2
    locations = 26 + stride * height
    widths = 16 + offset * 2
    font = FontBuilder(768, isTTF=True)
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
            for x in range(left, right):
                if data[26 + y * stride + x // 8] & (128 >> (x % 8)):
                    x0 = (x - left + bearing + kern) * 64
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
    font.setupGlyphOrder(list(glyphs))
    font.setupCharacterMap(cmap)
    font.setupGlyf(glyphs)
    font.setupHorizontalMetrics(metrics)
    font.setupHorizontalHeader(ascent=ascent * 64, descent=-descent * 64, lineGap=leading * 64)
    font.setupNameTable({'familyName': 'System Seven Chicago', 'styleName': 'Regular',
                        'uniqueFontIdentifier': 'System Seven Chicago 12',
                        'fullName': 'System Seven Chicago', 'psName': 'SystemSevenChicago'})
    font.setupOS2(sTypoAscender=ascent * 64, sTypoDescender=-descent * 64,
                 sTypoLineGap=leading * 64, usWinAscent=ascent * 64, usWinDescent=descent * 64)
    font.setupPost()
    font.setupMaxp()
    font.save(args.output / 'Chicago12.ttf')
    print(f'Extracted NFNT {font_id}: ascent {ascent}, descent {descent}, leading {leading}.')


if __name__ == '__main__':
    main()
