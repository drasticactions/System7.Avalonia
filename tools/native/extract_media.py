import argparse
import io
from pathlib import Path
import struct

import machfs
from PIL import Image
import rsrcfork


def picture(data):
    size, top, left, bottom, right = struct.unpack_from('>Hhhhh', data)
    if size != len(data) or (right - left, bottom - top) != (27, 27):
        raise ValueError('The native media picture has unexpected bounds.')
    image = Image.new('RGB', (27, 27), 'white')
    position = 10
    drew_bitmap = False
    while position < len(data):
        opcode = data[position]
        position += 1
        if opcode == 0x11:
            if data[position] != 1:
                raise ValueError('The media picture must use PICT version 1.')
            position += 1
        elif opcode == 0xa0:
            position += 2
        elif opcode == 1:
            region_size = struct.unpack_from('>H', data, position)[0]
            if region_size != 10:
                raise ValueError('The media picture needs a rectangular clipping region.')
            position += region_size
        elif opcode == 0x90:
            stride, bt, bl, bb, br, st, sl, sb, sr, dt, dl, db, dr, mode = struct.unpack_from('>H13h', data, position)
            position += 28
            if mode != 0 or (st, sl, sb, sr) != (top, left, bottom, right) or (dt, dl, db, dr) != (st, sl, sb, sr):
                raise ValueError('The media picture must contain an unscaled srcCopy bitmap.')
            for y in range(st, sb):
                for x in range(sl, sr):
                    if data[position + (y - bt) * stride + (x - bl) // 8] & (128 >> ((x - bl) % 8)):
                        image.putpixel((x - left, y - top), (0, 0, 0))
            position += stride * (bb - bt)
            drew_bitmap = True
        elif opcode == 0xff:
            break
        else:
            raise ValueError(f'Unexpected media picture opcode: {opcode:#x}.')
    if position != len(data) or not drew_bitmap:
        raise ValueError('The media picture is incomplete.')
    return image


def main():
    parser = argparse.ArgumentParser(description='Extract the original CDEF 62 media pictures and speaker frames.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    for resource_id, name in [(-16485, 'Pause'), (-16484, 'Play'), (-16483, 'Stop'), (-16482, 'Record')]:
        picture(resources[b'PICT'][resource_id].data).save(args.output / f'System7Media{name}.png')
    frames = b''.join(resources[b'ICON'][-16487 - index].data for index in range(6))
    if len(frames) != 6 * 128 or -16493 in resources[b'ICON']:
        raise ValueError('The native speaker icon sequence differs from System 7.0.1.')
    (args.output / 'System7Speaker.bin').write_bytes(frames)
    print('Extracted four original media pictures and six speaker frames.')


if __name__ == '__main__':
    main()
