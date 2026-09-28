import argparse
import base64
import io
from pathlib import Path
import sys

import machfs
import rsrcfork

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'minivmac'))
from client import MiniVMac


def main():
    parser = argparse.ArgumentParser(description='Trace the original CDEF 0 control variants 9 and 10.')
    parser.add_argument('--image', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--port', type=int, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.image.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    code = resources[b'CDEF'][0].data
    with MiniVMac(args.port) as mac:
        def read(address, length):
            return base64.b64decode(mac.command('memory', address=address & 0xffffff, length=length)['data'], validate=True)

        mac.command('trace_stop')
        mac.command('resume')
        mac.wait(5)
        mac.press(8)
        mac.wait(1)
        for _ in range(20):
            mac.command('pause')
            try:
                size = mac.command('status')['ram_size']
                ram = b''.join(read(i, min(65536, size-i)) for i in range(0, size, 65536))
            finally:
                mac.command('resume')
            if b'S7PROBE!' in ram and ram[0x910:0x919] == b'\x08Controls':
                break
            mac.mouse(10, 320)
            mac.click(232, 94)
            mac.click(232, 94)
            mac.wait(2)
        else:
            mac.frame(args.output / 'startup.ppm')
            raise AssertionError('The native control variants did not launch.')
        address = ram.find(code)
        if address < 0 or read(address, len(code)) != code:
            raise AssertionError('The running CDEF 0 differs from the original System resource.')
        extension = '.ppm' if mac.command('status')['depth'] > 1 else '.pbm'

        def frame(name):
            mac.wait(.1)
            mac.frame(args.output / (name + extension))

        def trace(name, action):
            mac.command('trace', count=4096, low=address, high=address + len(code))
            action()
            mac.command('trace_stop')
            result = mac.command('trace_get')
            if not result['pcs'] or any(not address <= (pc & 0xffffff) < address + len(code) for pc in result['pcs']):
                raise AssertionError(f'{name} did not execute the original CDEF 0.')
            (args.output / f'CDEF0-{name}.trace').write_text(result['disassembly'])
            print(f'{name}: {len(result["pcs"])} native instructions, {len(set(result["pcs"]))} unique addresses.')

        mac.mouse(20, 320)
        frame('variants-idle')
        for name, x in [('variant-10-unselected', 100), ('variant-10-selected', 225), ('variant-9-selected', 350)]:
            def action():
                mac.mouse(x, 228, True)
                frame(name + '-pressed')
                mac.mouse(x, 228, False)
                frame(name + '-released')
            trace(name, action)
        mac.command('pause')


if __name__ == '__main__':
    main()
