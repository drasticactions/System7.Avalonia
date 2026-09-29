"""Extract Assets/Cursors.bin and Assets/DesktopPatterns.bin from System 7 media.

The arrow lives in ROM, not in a resource, so it is read from the ROM image at
the offset where its CURS record sits. Every other cursor and pattern is a
resource on a System 7 volume, read with machfs and rsrcfork.

The System 7.5 Desktop Patterns control panel stores its ppat resources with
the System's 'dcmp' 3 compression, which rsrcfork does not implement. The
System's own 68k 'dcmp' 3 code is run in a Unicorn 68040 instead, called as
the Resource Manager calls it: a Pascal function of the compressed data after
the 18-byte extended header, the destination, and the header.

Needs: pip install machfs rsrcfork unicorn

Cursors.bin: u16 count, then per cursor: u8 name length, ASCII name,
u16 frame count, u16 frame interval in milliseconds, then per frame a CURS
record (32 bytes data, 32 bytes mask, s16 hot spot v, s16 hot spot h).
An acur resource carries no timing, so the watch's interval is chosen here.

DesktopPatterns.bin: u16 count, then per pattern: u8 name length, ASCII name,
the 8-byte one-bit pattern, u16 width and u16 height of the color pixmap
(both 0 when there is none), and when present u16 color count, that many
big-endian u32 ARGB colors, and width * height u8 color indices.
"""
import argparse
import io
import struct
from pathlib import Path

import machfs
import rsrcfork
from unicorn import Uc, UC_ARCH_M68K, UC_MODE_BIG_ENDIAN
from unicorn.m68k_const import UC_CPU_M68K_M68040, UC_M68K_REG_A7

ASSETS = Path(__file__).resolve().parents[2] / 'src/System7.Avalonia/Assets'
ROM_ARROW = {
    0x4D1F8172: 0x8800,
    0x9779D2C4: 0x1E550,
}
WATCH_INTERVAL_MS = 125


def volume(path):
    result = machfs.Volume()
    result.read(path.read_bytes())
    return result


def resources(file):
    return rsrcfork.ResourceFile(io.BytesIO(file.rsrc))


def rom_arrow(path):
    rom = path.read_bytes()
    checksum = struct.unpack('>I', rom[:4])[0]
    if checksum not in ROM_ARROW:
        raise SystemExit(f'No known arrow offset for a ROM with checksum {checksum:08X}.')
    record = rom[ROM_ARROW[checksum]:ROM_ARROW[checksum] + 68]
    if record[64:68] != b'\0\x01\0\x01':
        raise SystemExit('The ROM does not hold the arrow at the expected offset.')
    return record


def run_dcmp3(code, raw):
    length = struct.unpack('>I', raw[8:12])[0]
    base, source, target, back, top = 0x10000, 0x100000, 0x300000, 0xFFF0, 0x800000
    cpu = Uc(UC_ARCH_M68K, UC_MODE_BIG_ENDIAN)
    cpu.ctl_set_cpu_model(UC_CPU_M68K_M68040)
    cpu.mem_map(0, 16 << 20)
    cpu.mem_write(base, code)
    cpu.mem_write(source, raw)
    stack = top - 2
    for value in (source + 18, target, source, back):
        stack -= 4
        cpu.mem_write(stack, struct.pack('>I', value))
    cpu.reg_write(UC_M68K_REG_A7, stack)
    cpu.emu_start(base + 0x20, back, count=50_000_000)
    return bytes(cpu.mem_read(target, length))


def resource_data(resource, dcmp):
    if resource.compressed_info is None:
        return resource.data
    if resource.compressed_info.dcmp_id == 3:
        return run_dcmp3(dcmp, resource.data_raw)
    return resource.data


def parse_ppat(data):
    kind, pixmap, pixels = struct.unpack('>HII', data[:10])
    one_bit = data[20:28]
    if kind != 1:
        return one_bit, None
    row_bytes, top, left, bottom, right = struct.unpack('>Hhhhh', data[pixmap + 4:pixmap + 14])
    row_bytes &= 0x3fff
    depth = struct.unpack('>H', data[pixmap + 32:pixmap + 34])[0]
    table = struct.unpack('>I', data[pixmap + 42:pixmap + 46])[0]
    width, height = right - left, bottom - top
    flags, last = struct.unpack('>HH', data[table + 4:table + 8])
    colors = {}
    for i in range(last + 1):
        value, red, green, blue = struct.unpack('>HHHH', data[table + 8 + i * 8:table + 16 + i * 8])
        colors[i if flags & 0x8000 else value] = 0xFF000000 | (red >> 8) << 16 | (green >> 8) << 8 | blue >> 8
    if pixels + row_bytes * height > len(data):
        raise SystemExit('A ppat pixmap runs past its resource.')
    palette, indices = [], bytearray()
    for y in range(height):
        for x in range(width):
            bit = x * depth
            byte = data[pixels + y * row_bytes + bit // 8]
            argb = colors[(byte >> (8 - depth - bit % 8)) & ((1 << depth) - 1)]
            if argb not in palette:
                palette.append(argb)
            indices.append(palette.index(argb))
    return one_bit, (width, height, palette, bytes(indices))


def write_cursors(cursors, path):
    out = bytearray(struct.pack('>H', len(cursors)))
    for name, frames, interval in cursors:
        out += bytes([len(name)]) + name.encode('ascii') + struct.pack('>HH', len(frames), interval)
        for record in frames:
            if len(record) != 68:
                raise SystemExit(f'The {name} cursor is not a 68-byte CURS record.')
            out += record
    path.write_bytes(bytes(out))


def write_patterns(patterns, path):
    out = bytearray(struct.pack('>H', len(patterns)))
    for name, one_bit, pixmap in patterns:
        out += bytes([len(name)]) + name.encode('ascii') + one_bit
        if pixmap is None:
            out += struct.pack('>HH', 0, 0)
            continue
        width, height, palette, indices = pixmap
        out += struct.pack('>HHH', width, height, len(palette))
        out += b''.join(struct.pack('>I', argb) for argb in palette) + indices
    path.write_bytes(bytes(out))


def main():
    parser = argparse.ArgumentParser(description='Extract the System 7 cursors and desktop patterns.')
    parser.add_argument('--rom', required=True, type=Path, help='Mac Plus (4D1F8172) or Mac II (9779D2C4) ROM.')
    parser.add_argument('--system', required=True, type=Path, help='System 7.0.1 volume.')
    parser.add_argument('--system75', required=True, type=Path, help='System 7.5.x volume with the Desktop Patterns control panel.')
    args = parser.parse_args()

    old = volume(args.system)['System Folder']
    system = resources(old['System'])
    finder = resources(old['Finder'])
    watch = finder[b'acur'][6500].data
    count = struct.unpack('>H', watch[:2])[0]
    def frame(rid):
        return (finder if rid in finder[b'CURS'] else system)[b'CURS'][rid].data
    frames = [frame(struct.unpack('>h', watch[4 + i * 4:6 + i * 4])[0]) for i in range(count)]
    write_cursors([
        ('arrow', [rom_arrow(args.rom)], 0),
        ('ibeam', [system[b'CURS'][1].data], 0),
        ('crosshair', [system[b'CURS'][2].data], 0),
        ('plus', [system[b'CURS'][3].data], 0),
        ('watch', frames, WATCH_INTERVAL_MS),
    ], ASSETS / 'Cursors.bin')

    new = volume(args.system75)['System Folder']
    new_system = resources(new['System'])
    dcmp = new_system[b'dcmp'][3].data
    panel = resources(new['Control Panels']['Desktop Patterns'])
    patterns = []
    default_bits, default_pixmap = parse_ppat(new_system[b'ppat'][16].data)
    patterns.append(('Desktop', system[b'PAT '][16].data, default_pixmap))
    listed = system[b'PAT#'][0].data
    for i in range(struct.unpack('>H', listed[:2])[0]):
        patterns.append((f'General Controls {i + 1}', listed[2 + i * 8:10 + i * 8], None))
    for rid in sorted(panel[b'ppat'].keys()):
        one_bit, pixmap = parse_ppat(resource_data(panel[b'ppat'][rid], dcmp))
        patterns.append((f'Desktop Patterns {rid}', one_bit, pixmap))
    write_patterns(patterns, ASSETS / 'DesktopPatterns.bin')
    print(f'{len(frames) + 4} cursor frames and {len(patterns)} patterns')


if __name__ == '__main__':
    main()
