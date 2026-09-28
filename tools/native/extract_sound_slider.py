import argparse
import io
from pathlib import Path
import struct

import machfs
import rsrcfork


def picture_bits(data):
    size, top, left, bottom, right = struct.unpack_from('>Hhhhh', data)
    if size != len(data) or data[10:29] != bytes.fromhex('1101a00082a0008e01000a0000000002d00240'):
        raise ValueError('The Sound slider picture header is unexpected.')
    width, height = right - left, bottom - top
    pixels = bytearray(width * height)
    position = 29
    while data[position] == 0x90:
        position += 1
        row_bytes, bt, bl, bb, br, st, sl, sb, sr, dt, dl, db, dr, mode = struct.unpack_from('>H13h', data, position)
        position += 28
        if (sb - st, sr - sl) != (db - dt, dr - dl) or mode != 0:
            raise ValueError('The Sound slider picture uses a transformed bitmap.')
        for y in range(bb - bt):
            row = data[position:position + row_bytes]
            position += row_bytes
            for x in range(sr - sl):
                px, py = dl + x - left, dt + y - top
                if 0 <= px < width and 0 <= py < height:
                    pixels[py * width + px] = bool(row[(sl - bl + x) // 8] & (128 >> ((sl - bl + x) & 7)))
    if data[position:] != bytes.fromhex('a0008fa00083ff'):
        raise ValueError('The Sound slider picture has trailing data.')
    return width, height, bytes(pixels)


def main():
    parser = argparse.ArgumentParser(description='Extract the original Sound slider pictures.')
    parser.add_argument('--system', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['Control Panels']['Sound'].rsrc))
    args.output.mkdir(parents=True, exist_ok=True)
    for rid, name in [(-4047, 'Track'), (-4046, 'Thumb')]:
        width, height, bits = picture_bits(resources[b'PICT'][rid].data)
        (args.output / f'SoundSlider{name}.bin').write_bytes(bits)
        print(f'SoundSlider{name}: {width} by {height} pixels from PICT {rid}.')


if __name__ == '__main__':
    main()
