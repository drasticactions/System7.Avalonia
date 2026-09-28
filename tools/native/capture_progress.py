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
    parser = argparse.ArgumentParser(description='Trace CDEF 62 progress drawing and tracking through Mini vMac TCP.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--port', type=int, default=6807)
    parser.add_argument('--launch', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    code = resources[b'CDEF'][62].data
    with MiniVMac(args.port) as mac:
        mac.command('trace_stop')
        if args.launch:
            mac.click(232, 94)
            mac.click(232, 94)
            mac.wait(1)

        def read(address, length):
            return base64.b64decode(mac.command('memory', address=address & 0xffffff, length=length)['data'])

        def pointer(address):
            return int.from_bytes(read(address, 4), 'big') & 0xffffff

        mac.command('pause')
        try:
            size = mac.command('status')['ram_size']
            ram = b''.join(read(i, min(65536, size - i)) for i in range(0, size, 65536))
        finally:
            mac.command('resume')
        marker = ram.find(b'S7PROGR!')
        if marker < 0:
            raise AssertionError('The native progress probe is not running.')
        state = marker + 8
        handles = [pointer(state + 4 + i * 4) for i in range(8)]
        code_address = pointer(pointer(pointer(handles[0]) + 24))
        if read(code_address, len(code)) != code:
            raise AssertionError('The running progress CDEF does not match the System resource.')

        def expect(values, hilite=0):
            for index, handle in enumerate(handles):
                record = pointer(handle)
                actual = struct.unpack('>h', read(record + 18, 2))[0]
                if actual != values[index] or read(record + 17, 1)[0] != hilite:
                    raise AssertionError(f'Progress {index} has an unexpected value or hilite state.')

        def frame(name):
            mac.wait(0.05)
            mac.frame(args.output / (name + '.pbm'))

        def trace(name, required, offsets=()):
            mac.command('trace_stop')
            result = mac.command('trace_get')
            if not result['pcs'] or any(op not in result['disassembly'] for op in required):
                raise AssertionError(f'The {name} trace is missing required instructions.')
            addresses = {pc & 0xffffff for pc in result['pcs']}
            if any(code_address + offset not in addresses for offset in offsets):
                raise AssertionError(f'The {name} trace missed a required drawing branch.')
            (args.output / (name + '.trace')).write_text(result['disassembly'])
            print(f'{name}: {len(result["pcs"])} executed instructions, {len(set(result["pcs"]))} unique addresses.')

        initial = [0, 25, 50, 100, 25, 50, 50, 75]
        mac.press(0)
        mac.press(17)
        mac.press(1)
        mac.press(13)
        mac.press(15)
        expect(initial)
        frame('progress-idle')
        mac.command('trace', count=4096, low=code_address, high=code_address + len(code))
        for key, value in [(29, 0), (18, 1), (19, 25), (21, 29), (20, 33), (23, 50), (26, 75), (25, 99), (3, 100)]:
            mac.press(key)
            expect([value] * 8)
            frame(f'progress-{value}')
        trace('CDEF62-progress-drawing', ('$A8A1', '$A8A9', '$A8A5'), (0x25c, 0x2c4))
        mac.press(15)
        mac.press(2)
        expect(initial, 255)
        frame('progress-disabled')
        mac.press(23)
        expect([50] * 8, 255)
        frame('progress-disabled-update')
        mac.press(34)
        expect([50] * 8, 254)
        frame('progress-inactive')
        mac.press(20)
        expect([33] * 8, 254)
        frame('progress-inactive-update')
        mac.press(0)
        expect([33] * 8)
        frame('progress-enabled-update')
        mac.press(15)
        expect(initial)
        frame('progress-enabled')
        mac.command('trace', count=4096, low=code_address, high=code_address + len(code))
        mac.mouse(120, 78, True)
        mac.wait(0.15)
        if read(pointer(handles[0]) + 17, 1)[0] != 0 or struct.unpack('>h', read(state + 60, 2))[0] != 0:
            raise AssertionError('The display-only progress indicator accepted a mouse hit.')
        frame('progress-pressed')
        mac.mouse(120, 55, True)
        mac.wait(0.1)
        frame('progress-outside')
        mac.mouse(120, 78, True)
        mac.wait(0.1)
        frame('progress-returned')
        mac.mouse(120, 78, False)
        mac.wait(0.1)
        if int.from_bytes(read(state + 64, 4), 'big') != 0:
            raise AssertionError('The progress indicator entered mouse tracking.')
        expect(initial)
        frame('progress-released')
        trace('CDEF62-progress-hit-test', ('TST.W D7',))
        mac.press(4)
        frame('progress-hidden')
        mac.press(1)
        frame('progress-shown')
        mac.press(2)
        mac.press(4)
        frame('progress-disabled-hidden')
        mac.press(1)
        frame('progress-disabled-shown')
        mac.press(0)
        frame('progress-restored')
        mac.press(23)
        mac.press(45)
        frame('progress-narrow')
        mac.press(13)
        frame('progress-wide')
        mac.press(2)
        mac.press(45)
        frame('progress-disabled-narrow')
        mac.press(13)
        frame('progress-disabled-wide')
        mac.press(0)
        frame('progress-size-enabled')
        mac.press(5)
        frame('progress-checker')
        mac.press(25)
        frame('progress-checker-99')
        mac.press(17)
        mac.press(21)
        mac.press(45)
        frame('progress-rounding-29')
        mac.press(13)
        print('Native progress values, horizontal and vertical drawing, visibility, and mouse tracking passed.')


if __name__ == '__main__':
    main()
