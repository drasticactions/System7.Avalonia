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
    parser = argparse.ArgumentParser(description='Capture Speech CDEF 2 from the running original control panel.')
    parser.add_argument('--image', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--port', type=int, required=True)
    parser.add_argument('--thumb-left', type=int, default=222)
    parser.add_argument('--thumb-right', type=int, default=322)
    parser.add_argument('--thumb-y', type=int, default=135)
    args = parser.parse_args()
    volume = machfs.Volume()
    volume.read(args.image.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['Control Panels']['Speech'].rsrc))
    code = resources[b'CDEF'][2].data

    with MiniVMac(args.port) as mac:
        def read(address, length):
            reply = mac.command('memory', address=address & 0xffffff, length=length)
            return base64.b64decode(reply['data'], validate=True)

        def pointer(address):
            return int.from_bytes(read(address, 4), 'big') & 0xffffff

        mac.command('pause')
        try:
            size = mac.command('status')['ram_size']
            ram = b''.join(read(address, min(65536, size - address)) for address in range(0, size, 65536))
            matches = []
            bounds = struct.pack('>hhhh', 68, 206, 93, 329)
            offset = 0
            while (offset := ram.find(bounds, offset)) >= 0:
                record = offset - 8
                if record >= 0 and struct.unpack_from('>hh', ram, record + 20) == (20, 400):
                    definition = struct.unpack_from('>I', ram, record + 24)[0] & 0xffffff
                    address = struct.unpack_from('>I', ram, definition)[0] & 0xffffff
                    if ram[address:address + len(code)] == code:
                        matches.append((record, address))
                offset += 1
        finally:
            mac.command('resume')
        if len(matches) != 1:
            raise AssertionError(f'Expected one live Speech slider with original CDEF 2; found {len(matches)}.')
        record, address = matches[0]
        if not pointer(record + 28):
            raise AssertionError('The Speech panel did not initialize the slider data handle.')

        args.output.mkdir(parents=True, exist_ok=False)

        def value():
            return struct.unpack('>h', read(record + 18, 2))[0]

        def thumb_x():
            return args.thumb_left + round((value() - 20) * (args.thumb_right - args.thumb_left) / 380)

        extension = '.ppm' if mac.command('status')['depth'] > 1 else '.pbm'
        mac.command('trace_stop')
        mac.command('trace', count=4096, low=address, high=address + len(code))
        for target in [20, 100, 200, 300, 400]:
            start = thumb_x()
            finish = args.thumb_left + round((target - 20) * (args.thumb_right - args.thumb_left) / 380)
            mac.mouse(start, args.thumb_y, True)
            mac.wait(.1)
            for step in range(1, 9):
                mac.mouse(start + round((finish - start) * step / 8), args.thumb_y, True)
                mac.wait(.06)
            mac.frame(args.output / f'slider-target-{target}-pressed{extension}')
            mac.mouse(finish, args.thumb_y, False)
            mac.mouse(10, 320)
            mac.wait(.15)
            mac.frame(args.output / f'slider-target-{target}-released{extension}')
            print(f'target {target}: native value {value()}')
        mac.command('trace_stop')
        trace = mac.command('trace_get')
        if not trace['pcs'] or any(not address <= (pc & 0xffffff) < address + len(code) for pc in trace['pcs']):
            raise AssertionError('The original Speech CDEF 2 did not execute.')
        (args.output / 'Speech-CDEF2.trace').write_text(trace['disassembly'])
        print(f'{len(trace["pcs"])} original CDEF 2 instructions, {len(set(trace["pcs"]))} unique addresses.')


if __name__ == '__main__':
    main()
