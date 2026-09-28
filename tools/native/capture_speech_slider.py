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
    parser = argparse.ArgumentParser(description='Trace the original Speech panel CDEF 2 slider.')
    parser.add_argument('--image', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--port', type=int, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.image.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['Controls'].rsrc))
    code = resources[b'CDEF'][14].data
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
            raise AssertionError('The Speech slider probe did not launch.')
        address = ram.find(code)
        if address < 0 or read(address, len(code)) != code:
            raise AssertionError('The executing CDEF 2 differs from the Speech control panel resource.')
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
        frame('speech-slider-idle')
        mac.command('trace', count=4096, low=address, high=address + len(code))
        for expected in [100, 200, 300, 400, 20]:
            for _ in range(3):
                mac.click(350, 80)
                if value() == expected:
                    break
            else:
                raise AssertionError(f'The Speech slider value is {value()}, expected {expected}.')
            frame(f'speech-slider-value-{expected}')
        for x, name in [(64, 'left'), (119, 'middle'), (178, 'right')]:
            mac.mouse(x, 232, True)
            frame(f'speech-slider-{name}-pressed')
            mac.mouse(x, 232, False)
            frame(f'speech-slider-{name}-released')
        mac.command('trace_stop')
        trace = mac.command('trace_get')
        if not trace['pcs'] or any(not address <= (pc & 0xffffff) < address + len(code) for pc in trace['pcs']):
            raise AssertionError('The original Speech CDEF 2 did not execute.')
        (args.output / 'Speech-CDEF2.trace').write_text(trace['disassembly'])
        print(f'{len(trace["pcs"])} native CDEF 2 instructions, {len(set(trace["pcs"]))} unique addresses.')
        mac.command('pause')


if __name__ == '__main__':
    main()
