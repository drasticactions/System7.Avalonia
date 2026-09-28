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
    parser = argparse.ArgumentParser(description='Trace the original Speech panel CDEF 5 controls.')
    parser.add_argument('--image', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--port', type=int, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.image.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['Controls'].rsrc))
    code = resources[b'CDEF'][13].data
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
                ram = b''.join(read(i, min(65536, size - i)) for i in range(0, size, 65536))
            finally:
                mac.command('resume')
            marker = ram.find(b'S7PROBE!')
            if marker >= 0 and ram[0x910:0x919] == b'\x08Controls':
                break
            mac.mouse(10, 320)
            for _ in range(3):
                mac.click(232, 92)
            mac.wait(2)
        else:
            mac.frame(args.output / 'startup.ppm')
            raise AssertionError('The Speech button probe did not launch.')
        address = ram.find(code)
        if address < 0 or read(address, len(code)) != code:
            raise AssertionError('The executing CDEF differs from the Speech panel CDEF 5 resource.')
        records = {}
        for item in [10, 11]:
            handle = int.from_bytes(read(marker + 8 + 20 + (item - 1) * 4, 4), 'big') & 0xffffff
            record = pointer(handle)
            records[item] = record
            definition = pointer(pointer(record + 24))
            if read(definition, len(code)) != code:
                raise AssertionError(f'Speech control {item} did not load the original CDEF 5 bytes.')
        extension = '.ppm' if mac.command('status')['depth'] > 1 else '.pbm'

        def frame(name):
            mac.wait(.1)
            mac.frame(args.output / (name + extension))
            print(name)

        mac.mouse(10, 320)
        frame('speech-buttons-idle')
        mac.command('trace', count=4096, low=address, high=address + len(code))
        for x, name, item in [(75, 'variant-9', 10), (230, 'variant-10', 11)]:
            mac.mouse(x, 228, True)
            mac.wait(.5)
            print(name, 'pressed hilite/value', read(records[item] + 17, 1).hex(), int.from_bytes(read(records[item] + 8, 2), 'big'))
            frame(f'speech-buttons-{name}-pressed')
            mac.mouse(x, 228, False)
            frame(f'speech-buttons-{name}-released')
        for index, value in enumerate([1, 50, 100, 0], 1):
            for _ in range(3):
                mac.click(90, 80)
                if int.from_bytes(read(marker + 90, 2), 'big') == index % 4:
                    break
            else:
                raise AssertionError(f'The Speech controls did not reach value {value}.')
            frame(f'speech-buttons-value-{value}')
            if value == 1:
                for x, name in [(75, 'variant-9'), (230, 'variant-10')]:
                    mac.mouse(x, 228, True)
                    frame(f'speech-buttons-value-1-{name}-pressed')
                    mac.mouse(x, 228, False)
        mac.click(350, 80)
        if int.from_bytes(read(marker + 88, 2), 'big') != 255:
            raise AssertionError('The Speech buttons did not become disabled.')
        frame('speech-buttons-disabled')
        mac.mouse(75, 228, True)
        frame('speech-buttons-disabled-pressed')
        mac.mouse(75, 228, False)
        for _ in range(3):
            mac.click(350, 80)
            if int.from_bytes(read(marker + 88, 2), 'big') == 0:
                break
        else:
            raise AssertionError('The Speech buttons did not become enabled.')
        frame('speech-buttons-reactivated')
        mac.command('trace_stop')
        trace = mac.command('trace_get')
        if not trace['pcs'] or any(not address <= (pc & 0xffffff) < address + len(code) for pc in trace['pcs']):
            raise AssertionError('The original Speech CDEF 5 did not execute.')
        (args.output / 'Speech-CDEF5.trace').write_text(trace['disassembly'])
        print(f'{len(trace["pcs"])} original Speech CDEF 5 instructions, {len(set(trace["pcs"]))} unique addresses.')
        mac.command('pause')


if __name__ == '__main__':
    main()
