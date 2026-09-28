import argparse
import io
from pathlib import Path
import struct

import machfs
import rsrcfork


def packed_row(data, position, row_bytes):
    count = data[position]
    position += 1
    end = position + count
    row = bytearray()
    while position < end:
        control = data[position]
        position += 1
        if control <= 127:
            length = control + 1
            row.extend(data[position:position + length])
            position += length
        elif control > 128:
            length = 257 - control
            row.extend(data[position:position + 1] * length)
            position += 1
    if position != end or len(row) != row_bytes:
        raise ValueError('The Network slider picture has a malformed packed row.')
    return row, position


def picture_bits(data):
    size, top, left, bottom, right = struct.unpack_from('>Hhhhh', data)
    if size != len(data) or data[10:26] != bytes.fromhex('1101a0008201000a0000000002d00240'):
        raise ValueError('The Network slider picture header is unexpected.')
    width, height = right - left, bottom - top
    pixels = bytearray(width * height)
    position = 26
    while data[position] in (0x90, 0x98):
        opcode = data[position]
        position += 1
        row_bytes, bt, bl, bb, br, st, sl, sb, sr, dt, dl, db, dr, mode = struct.unpack_from('>H13h', data, position)
        position += 28
        if (sb - st, sr - sl) != (db - dt, dr - dl) or mode != 0:
            raise ValueError('The Network slider picture uses a transformed bitmap.')
        for y in range(bb - bt):
            if opcode == 0x98:
                row, position = packed_row(data, position, row_bytes)
            else:
                row = data[position:position + row_bytes]
                position += row_bytes
            for x in range(sr - sl):
                px, py = dl + x - left, dt + y - top
                if 0 <= px < width and 0 <= py < height:
                    pixels[py * width + px] = bool(row[(sl - bl + x) // 8] & (128 >> ((sl - bl + x) & 7)))
    if data[position:] != bytes.fromhex('a00083ff'):
        raise ValueError('The Network slider picture has trailing data.')
    return width, height, bytes(pixels)


def main():
    parser = argparse.ArgumentParser(description='Extract the original Network slider pictures.')
    parser.add_argument('--system', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['Extensions']['Network Extension'].rsrc))
    args.output.mkdir(parents=True, exist_ok=True)
    for rid, name in [(1, 'Track'), (2, 'Thumb')]:
        width, height, bits = picture_bits(resources[b'PICT'][rid].data)
        (args.output / f'NetworkSlider{name}.bin').write_bytes(bits)
        print(f'NetworkSlider{name}: {width} by {height} pixels from PICT {rid}.')


if __name__ == '__main__':
    main()
