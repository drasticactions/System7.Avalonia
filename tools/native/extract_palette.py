import argparse
import base64
from pathlib import Path
import struct
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'minivmac'))
from client import MiniVMac


def main():
    parser = argparse.ArgumentParser(description='Extract the native indexed palette and QuickDraw inverse table.')
    parser.add_argument('--port', type=int, default=6817)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    with MiniVMac(args.port) as mac:
        def read(address, length):
            return base64.b64decode(mac.command('memory', address=address & 0xffffff, length=length)['data'], validate=True)

        def pointer(address):
            return int.from_bytes(read(address, 4), 'big') & 0xffffff

        mac.command('pause')
        try:
            frame = mac.command('frame')
            if frame['depth'] not in (2, 4, 8):
                raise AssertionError('The current native display must use 4, 16, or 256 colors.')
            count = 1 << frame['depth']
            device = pointer(pointer(0x8a4))
            pixmap = pointer(pointer(device + 22))
            table = pointer(pointer(pixmap + 42))
            inverse = pointer(pointer(device + 6))
            header = read(inverse, 6)
            colors = read(table, 8 + count * 8)
            if header[:4] != colors[:4] or header[4:] != b'\0\x04' or int.from_bytes(colors[6:8], 'big') != count - 1:
                raise AssertionError('The native color and inverse tables do not describe the indexed device.')
            chain = read(inverse + 6 + 4096, 6 + max(count, 256))
            grayscale = chain[:6] == bytes.fromhex('000080000000')
            if not grayscale and chain[:6 + count] != bytes(6) + bytes(range(count)):
                raise AssertionError('The inverse table requires an unsupported collision search.')
            if grayscale != (frame['depth'] == 2):
                raise AssertionError('The native palette does not use the expected grayscale search.')
            palette = [struct.unpack_from('>HHH', colors, 10 + index * 8) for index in range(count)]
            if palette != [tuple(rgb) for rgb in frame['palette']]:
                raise AssertionError('QuickDraw and the emulated video card have different palettes.')
            lookup = chain[6:262] if grayscale else read(inverse + 6, 4096)
            if any(index >= count for index in lookup):
                raise AssertionError('The inverse table contains an index outside the native palette.')
            data = b''.join(struct.pack('>HHH', *rgb) for rgb in palette) + lookup
        finally:
            mac.command('resume')
    args.output.write_bytes(data)
    print(f'{len(palette)} native RGB colors and {len(lookup)} QuickDraw inverse entries: {args.output}')


if __name__ == '__main__':
    main()
