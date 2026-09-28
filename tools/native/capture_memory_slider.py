import argparse
import base64
import io
from pathlib import Path
import struct
import sys

import machfs
import rsrcfork

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'minivmac'))
from client import MiniVMac


def main():
    parser = argparse.ArgumentParser(description='Trace an original control panel CDEF 3 slider.')
    parser.add_argument('--image', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--port', type=int, required=True)
    parser.add_argument('--kind', choices=['memory', 'sound', 'network'], default='memory')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.image.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['Controls'].rsrc))
    code = resources[b'CDEF'][10 if args.kind == 'network' else 3].data
    with MiniVMac(args.port) as mac:
        def read(address, length):
            return base64.b64decode(mac.command('memory', address=address & 0xffffff, length=length)['data'], validate=True)

        def pointer(address):
            return int.from_bytes(read(address, 4), 'big') & 0xffffff

        mac.command('trace_stop')
        mac.command('resume')
        mac.wait(5)
        mac.press(8)
        mac.wait(1)
        for _ in range(60):
            mac.command('pause')
            try:
                size = mac.command('status')['ram_size']
                ram = b''.join(read(i, min(65536, size-i)) for i in range(0, size, 65536))
            finally:
                mac.command('resume')
            marker = ram.find(b'S7PROBE!')
            if marker >= 0 and ram[0x910:0x919] == b'\x08Controls':
                break
            mac.mouse(10, 320)
            mac.click(232, 92)
            mac.click(232, 92)
            mac.click(232, 92)
            mac.wait(2)
        else:
            mac.frame(args.output / 'startup.ppm')
            raise AssertionError(f'The {args.kind} slider probe did not launch.')
        address = ram.find(code)
        if address < 0 or read(address, len(code)) != code:
            raise AssertionError(f'The executing CDEF 3 differs from the {args.kind} control panel resource.')
        handle = int.from_bytes(read(marker + 8 + 20 + 9 * 4, 4), 'big') & 0xffffff
        record = pointer(handle)
        if not record:
            raise AssertionError('The native slider control handle is invalid.')
        extension = '.ppm' if mac.command('status')['depth'] > 1 else '.pbm'

        def value():
            return struct.unpack('>h', read(record + 18, 2))[0]

        def frame(name):
            mac.wait(.1)
            mac.frame(args.output / (name + extension))
            print(f'{name}: native value {value()}')

        mac.mouse(10, 320)
        frame('slider-idle')
        mac.command('trace', count=4096, low=address, high=address + len(code))
        if args.kind == 'memory':
            states = [(100, 'left', 64), (150, 'middle', 126), (100, 'reset', 1)]
        elif args.kind == 'sound':
            states = [(70, f'value-{value}', value) for value in [1, 2, 3, 4, 5, 6, 7, 0]]
        else:
            states = [(150 if 0 < value <= 5 else 70, f'value-{value}', value) for value in [*range(1, 11), 0]]
        for x, name, expected in states:
            y = 190 if args.kind == 'sound' else 230
            mac.mouse(x, y, True)
            frame(f'slider-{name}-pressed')
            mac.mouse(x, y, False)
            frame(f'slider-{name}-released')
            if value() != expected:
                raise AssertionError(f'The native slider value is {value()}, expected {expected}.')
        mac.command('trace_stop')
        trace = mac.command('trace_get')
        if not trace['pcs'] or any(not address <= (pc & 0xffffff) < address + len(code) for pc in trace['pcs']):
            raise AssertionError('The original Memory CDEF 3 did not execute.')
        (args.output / f'{args.kind.title()}-CDEF3.trace').write_text(trace['disassembly'])
        print(f'{len(trace["pcs"])} native CDEF 3 instructions, {len(set(trace["pcs"]))} unique addresses.')
        mac.command('pause')


if __name__ == '__main__':
    main()
