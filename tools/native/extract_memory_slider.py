import argparse
import io
from pathlib import Path
import struct

import machfs
import rsrcfork


def picture_bits(data):
    size, top, left, bottom, right = struct.unpack_from('>Hhhhh', data)
    if size != len(data) or data[10:14] != b'\x11\x01\xa0\x00' or data[14:16] != b'\x82\x01':
        raise ValueError('The Memory slider picture header is unexpected.')
    clip_size = struct.unpack_from('>H', data, 16)[0]
    if clip_size != 10:
        raise ValueError('The Memory slider picture clip is unexpected.')
    position = 18 + clip_size - 2
    opcode = data[position]
    if opcode not in (0x90, 0x98):
        raise ValueError('The Memory slider picture has an unsupported bitmap opcode.')
    position += 1
    row_bytes, bt, bl, bb, br, st, sl, sb, sr, dt, dl, db, dr, mode = struct.unpack_from('>H13h', data, position)
    position += 28
    if (bt, bl, bb, br) != (top, left, bottom, right) and (bb - bt != bottom - top or br - bl < right - left):
        raise ValueError('The Memory slider bitmap bounds are inconsistent.')
    if (st, sl, sb, sr) != (dt, dl, db, dr) or mode != 0:
        raise ValueError('The Memory slider picture uses a transformed bitmap.')
    rows = []
    for _ in range(bb - bt):
        if opcode == 0x90:
            row = data[position:position + row_bytes]
            position += row_bytes
        else:
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
            if position != end:
                raise ValueError('The packed Memory slider row is malformed.')
        if len(row) != row_bytes:
            raise ValueError('The Memory slider row has the wrong width.')
        rows.append(row)
    if data[position:] != b'\xa0\x00\x83\xff':
        raise ValueError('The Memory slider picture has trailing data.')
    bits = bytearray((right - left) * (bottom - top))
    for y in range(top, bottom):
        for x in range(left, right):
            bits[(y - top) * (right - left) + x - left] = bool(rows[y - bt][(x - bl) // 8] & (128 >> ((x - bl) & 7)))
    return right - left, bottom - top, bytes(bits)


def main():
    parser = argparse.ArgumentParser(description='Extract the original Memory slider picture bitmaps.')
    parser.add_argument('--system', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['Control Panels']['Memory'].rsrc))
    args.output.mkdir(parents=True, exist_ok=True)
    for rid, name in [(-4035, 'Track'), (-4034, 'Thumb')]:
        width, height, bits = picture_bits(resources[b'PICT'][rid].data)
        (args.output / f'MemorySlider{name}.bin').write_bytes(bits)
        print(f'MemorySlider{name}: {width} by {height} pixels from PICT {rid}.')


if __name__ == '__main__':
    main()
