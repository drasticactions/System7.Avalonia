import argparse
import io
from pathlib import Path
import struct

import machfs
from PIL import Image
import rsrcfork


def main():
    parser = argparse.ArgumentParser(description='Render System 7 PICT -6046 using its bitmap and Chicago glyphs.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    data = resources[b'PICT'][-6046].data
    size, top, left, bottom, right = struct.unpack_from('>Hhhhh', data)
    if size != len(data):
        raise ValueError('Unexpected picture size.')
    fond = resources[b'FOND'][0].data
    associations = [struct.unpack_from('>HHH', fond, 54 + i * 6)
                    for i in range(struct.unpack_from('>H', fond, 52)[0] + 1)]
    font_id = next(rid for size, style, rid in associations if size == 12 and style == 0)
    font = resources[b'NFNT'][font_id].data
    _, first, _, _, kern, _, _, height, offset, ascent, _, _, row_words = struct.unpack_from('>13h', font)
    stride = row_words * 2
    locations = 26 + stride * height
    widths = 16 + offset * 2
    image = Image.new('RGBA', (right - left, bottom - top))

    def ink(x, y):
        if left <= x < right and top <= y < bottom:
            image.putpixel((x - left, y - top), (0, 0, 0, 255))

    position = 10
    pen_x = pen_y = 0
    while position < len(data):
        opcode = data[position]
        position += 1
        if opcode == 0x11:
            if data[position] != 1:
                raise ValueError('Expected a version 1 picture.')
            position += 1
        elif opcode == 0x01:
            region_size = struct.unpack_from('>H', data, position)[0]
            if region_size != 10 or struct.unpack_from('>hhhh', data, position + 2) != (top, left, bottom, right):
                raise ValueError('Unexpected picture clipping region.')
            position += region_size
        elif opcode == 0x0a:
            position += 8
        elif opcode == 0x0d:
            if struct.unpack_from('>H', data, position)[0] != 12:
                raise ValueError('Unexpected picture font size.')
            position += 2
        elif opcode == 0x2b:
            dx, dy, count = data[position:position + 3]
            position += 3
            pen_x += dx
            pen_y += dy
            for code in data[position:position + count]:
                index = code - first
                start, end = struct.unpack_from('>HH', font, locations + index * 2)
                bearing, advance = struct.unpack_from('>bB', font, widths + index * 2)
                for y in range(height):
                    for bit in range(start, end):
                        if font[26 + y * stride + bit // 8] & (128 >> (bit & 7)):
                            ink(pen_x + bit - start + bearing + kern, pen_y - ascent + y)
                pen_x += advance
            position += count
        elif opcode == 0x90:
            row_bytes, bt, bl, bb, br, st, sl, sb, sr, dt, dl, db, dr, mode = struct.unpack_from('>H13h', data, position)
            position += 28
            if mode != 1 or (sb - st, sr - sl) != (db - dt, dr - dl):
                raise ValueError('Expected an unscaled srcOr bitmap.')
            for y in range(st, sb):
                for x in range(sl, sr):
                    if data[position + (y - bt) * row_bytes + (x - bl) // 8] & (128 >> ((x - bl) & 7)):
                        ink(dl + x - sl, dt + y - st)
            position += row_bytes * (bb - bt)
        elif opcode == 0xff:
            break
        else:
            raise ValueError(f'Unexpected picture opcode {opcode:#x}.')
    if position != len(data):
        raise ValueError('The picture has trailing data.')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    image.save(args.output)
    print(f'Rendered PICT -6046 at {image.width} by {image.height} pixels.')


if __name__ == '__main__':
    main()
