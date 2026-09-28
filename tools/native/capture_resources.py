import argparse
import base64
import io
from pathlib import Path
import struct
import sys
import time

import machfs
import rsrcfork

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'minivmac'))
from client import MiniVMac


def main():
    parser = argparse.ArgumentParser(description='Run the SimpleText resource decompressor in the native 68k probe.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--port', type=int, default=6807)
    parser.add_argument('--launch', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['SimpleText'].rsrc))
    code = resources[b'dcmp'][3].data
    with MiniVMac(args.port) as mac:
        mac.command('trace_stop')
        if args.launch:
            mac.wait(5)
            mac.click(232, 94)
            mac.click(232, 94)
            mac.wait(1)

        def read(address, length):
            return base64.b64decode(mac.command('memory', address=address & 0xffffff, length=length)['data'])

        def number(address, length=4):
            return int.from_bytes(read(address, length), 'big')

        def pointer(address):
            return number(address) & 0xffffff

        mac.command('pause')
        try:
            size = mac.command('status')['ram_size']
            ram = b''.join(read(i, min(65536, size - i)) for i in range(0, size, 65536))
        finally:
            mac.command('resume')
        marker = ram.find(b'S7RSRCS!')
        if marker < 0:
            raise AssertionError('The native resource probe is not running.')
        state = marker + 8
        if number(state + 20, 2):
            raise AssertionError('Restart the probe before tracing its first decompression call.')
        address = pointer(pointer(state))
        if read(address, len(code)) != code:
            raise AssertionError('The native decompressor differs from the SimpleText resource.')
        print(f'Native dcmp 3 at {address:#x}: all {len(code)} bytes match SimpleText.')
        mac.command('trace', count=4096, low=address, high=address + len(code))
        mac.press(36)
        deadline = time.monotonic() + 5
        while not number(state + 20, 2):
            if time.monotonic() > deadline:
                raise AssertionError('The native decompression calls did not finish.')
            mac.wait(0.02)
        mac.command('trace_stop')
        trace = mac.command('trace_get')
        if not trace['pcs']:
            raise AssertionError('The probe did not execute the native decompressor.')
        (args.output / 'SimpleText-dcmp3.trace').write_text(trace['disassembly'])
        print(f'Native decompression: {len(trace["pcs"])} traced instructions.')
        for rid, offset in [(601, 4), (129, 12)]:
            handle = pointer(state + offset)
            length = number(state + offset + 4)
            if not handle or length != resources[b'DITL'][rid].compressed_info.decompressed_length:
                raise AssertionError(f'DITL {rid} has an incorrect decompressed length.')
            data = read(pointer(handle), length)
            count = struct.unpack_from('>h', data)[0] + 1
            position = 2
            for _ in range(count):
                if position + 14 > length:
                    raise AssertionError(f'DITL {rid} has a truncated item.')
                position = (position + 14 + data[position + 13] + 1) & ~1
            if position != length:
                raise AssertionError(f'DITL {rid} has an incorrect item boundary.')
            (args.output / f'SimpleText-DITL-{rid}.bin').write_bytes(data)
            print(f'SimpleText DITL {rid}: {length} decompressed bytes, {count} complete items.')


if __name__ == '__main__':
    main()
