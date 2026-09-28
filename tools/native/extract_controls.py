import argparse
import hashlib
import io
from pathlib import Path
import struct

import machfs
import rsrcfork


def main():
    parser = argparse.ArgumentParser(description='Extract monochrome scrollbar bitmaps from System 7 CDEF 1.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--window-output', type=Path)
    parser.add_argument('--window-color-output', type=Path)
    parser.add_argument('--rounded-window-output', type=Path)
    parser.add_argument('--rounded-window-rom', type=Path)
    parser.add_argument('--menu-output', type=Path)
    parser.add_argument('--menu-color-output', type=Path)
    parser.add_argument('--menu-color-system', type=Path)
    parser.add_argument('--menu-slopes-output', type=Path)
    parser.add_argument('--menu-slopes-rom', type=Path)
    parser.add_argument('--color-output', type=Path)
    args = parser.parse_args()
    if args.rounded_window_output and not args.rounded_window_rom:
        parser.error('--rounded-window-output requires --rounded-window-rom for the native color bitmap overflow bytes.')
    if args.menu_color_output and not args.menu_color_system:
        parser.error('--menu-color-output requires the original color System installer image.')
    if args.menu_slopes_output and not args.menu_slopes_rom:
        parser.error('--menu-slopes-output requires the verified Mac Plus ROM.')
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    code = resources[b'CDEF'][1].data
    if len(code) != 3946 or code[0xaa8:0xab4] != bytes.fromhex('e34045fa0306d4f200007400'):
        raise ValueError('The CDEF 1 bitmap lookup does not match System 7.0.1.')
    bitmaps = bytearray()
    for index in range(10):
        address = 0xdb2 + struct.unpack_from('>h', code, 0xdb2 + index * 2)[0]
        bounds = struct.unpack_from('>Hhhhh', code, address)
        if bounds != (2, 0, 0, 16, 16):
            raise ValueError(f'Unexpected bitmap bounds at index {index}: {bounds}.')
        bitmaps.extend(code[address + 10:address + 42])
    args.output.write_bytes(bitmaps)
    if args.color_output:
        color = bytearray(code[0xc62:0xdb0])
        for resource_id in range(-10208, -10198):
            pixels = resources[b'pixs'][resource_id].data
            stride, top, left, bottom, right, depth = struct.unpack_from('>HhhhhH', pixels)
            if top or left or depth != 1 or len(pixels) != 12 + (stride & 0x7fff) * bottom:
                raise ValueError(f'Unexpected color scrollbar bitmap {resource_id}.')
            color.extend(struct.pack('>H', len(pixels)))
            color.extend(pixels)
        args.color_output.write_bytes(color)
    if args.window_output:
        code = resources[b'WDEF'][0].data
        if len(code) != 3928 or code[0xd9a:0xda4] != bytes.fromhex('45fa012ad4f200007400'):
            raise ValueError('The WDEF 0 bitmap lookup does not match System 7.0.1.')
        bitmaps = bytearray()
        for index in range(5):
            address = 0xec6 + struct.unpack_from('>h', code, 0xec6 + index * 2)[0]
            size = 15 if index == 4 else 11
            if struct.unpack_from('>Hhhhh', code, address) != (2, 0, 0, size, size):
                raise ValueError(f'Unexpected window bitmap bounds at index {index}.')
            bitmaps.extend(code[address + 10:address + 10 + size * 2])
        args.window_output.write_bytes(bitmaps)
    if args.window_color_output:
        code = resources[b'WDEF'][0].data
        if len(code) != 3928 or code[0xd56:0xd5a] != bytes.fromhex('45fa0076'):
            raise ValueError('The WDEF 0 color bitmap lookup does not match System 7.0.1.')
        color = bytearray(code[0xc52:0xc78] + code[0xdd8:0xec6])
        for rid in (-14335, -14334):
            picture = resources[b'pixs'][rid].data
            if len(picture) != 78 or struct.unpack_from('>HhhhhH', picture) != (0x8006, 0, 0, 11, 11, 4):
                raise ValueError(f'Unexpected color window bitmap {rid}.')
            color.extend(struct.pack('>H', len(picture)))
            color.extend(picture)
        args.window_color_output.write_bytes(color)
    if args.rounded_window_output:
        code = resources[b'WDEF'][1].data
        if len(code) != 1270 or code[0x260:0x264] != bytes.fromhex('43fa0254'):
            raise ValueError('The WDEF 1 bitmap lookup does not match System 7.0.1.')
        bitmaps = code[0x4b6:0x4f6]
        if args.rounded_window_rom:
            rom = args.rounded_window_rom.read_bytes()
            if hashlib.sha256(rom).hexdigest() != '79fae48e2d5cfde68520e46616503963f8c16430903f410514b62c1379af20cb' or rom[0x32a12:0x32a52] != bitmaps:
                raise ValueError('The rounded-window bitmaps differ from the verified Mac II ROM.')
            bitmaps += rom[0x32a52:0x32a56]
        args.rounded_window_output.write_bytes(bitmaps)
    if args.menu_output:
        code = resources[b'MDEF'][0].data
        if len(code) != 4502 or code[0x7aa:0x7ae] != bytes.fromhex('49fa09aa') or code[0x7c8:0x7cc] != bytes.fromhex('49fa09ac'):
            raise ValueError('The MDEF 0 scroll arrow lookup does not match System 7.0.1.')
        args.menu_output.write_bytes(code[0x1136:0x1196])
    if args.menu_slopes_output:
        rom = args.menu_slopes_rom.read_bytes()
        if hashlib.sha256(rom).hexdigest() != 'dd908e2b65772a6b1f0c859c24e9a0d3dcde17b1c6a24f4abd8955846d7895e7':
            raise ValueError('The submenu slope table requires the verified Mac Plus ROM.')
        slopes = []
        for angle in range(91):
            high = 0 if angle < 45 else 1 if angle < 64 else rom[0xb7e1 + angle]
            if high & 0x80:
                high |= 0x7f00
            slopes.append(high * 65536 + struct.unpack_from('>H', rom, 0xb83c + angle * 2)[0])
        args.menu_slopes_output.write_bytes(struct.pack('>91I', *slopes))
    if args.menu_color_output:
        installer = machfs.Volume()
        installer.read(args.menu_color_system.read_bytes())
        color_resources = rsrcfork.ResourceFile(io.BytesIO(installer['System'].rsrc))
        if color_resources[b'MBDF'][0].data != resources[b'MBDF'][0].data:
            raise ValueError('The installer menu bar definition differs from the source System.')
        icon = bytearray()
        for kind, length in [(b'ics#', 64), (b'ics4', 128), (b'ics8', 256)]:
            data = color_resources[kind][-16386].data
            if len(data) != length:
                raise ValueError(f'The Apple menu {kind!r} icon has an invalid length.')
            icon.extend(data)
        args.menu_color_output.write_bytes(icon)


if __name__ == '__main__':
    main()
