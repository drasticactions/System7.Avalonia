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
    parser = argparse.ArgumentParser(description='Trace native picture buttons through Mini vMac TCP.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--port', type=int, default=6807)
    parser.add_argument('--launch', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    code = resources[b'CDEF'][61].data
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
        marker = ram.find(b'S7PICT!!')
        if marker < 0:
            raise AssertionError('The native picture probe is not running.')
        state = marker + 8
        handles = [pointer(state + 4 + i * 4) for i in range(8)]
        code_address = pointer(pointer(pointer(handles[0]) + 24))
        if read(code_address, len(code)) != code:
            raise AssertionError('The running picture CDEF differs from the System resource.')

        def word(address):
            return struct.unpack('>h', read(address, 2))[0]

        def expect(hilite, index=None):
            for i, handle in enumerate(handles):
                expected = hilite if index is None or index == i else 0
                if read(pointer(handle) + 17, 1)[0] != expected:
                    raise AssertionError(f'Picture button {i} has an unexpected hilite state.')

        def frame(name):
            mac.wait(0.05)
            mac.frame(args.output / (name + '.pbm'))

        def blocked_click():
            clicks = read(state + 64, 4)
            mac.click(95, 80)
            if word(state + 60) != 0 or read(state + 64, 4) != clicks:
                raise AssertionError('An unavailable picture button accepted a click.')

        def trace(name, required):
            mac.command('trace_stop')
            result = mac.command('trace_get')
            if not result['pcs'] or any(op not in result['disassembly'] for op in required):
                raise AssertionError(f'The {name} trace is missing required instructions.')
            (args.output / (name + '.trace')).write_text(result['disassembly'])
            print(f'{name}: {len(result["pcs"])} executed instructions, {len(set(result["pcs"]))} unique addresses.')

        mac.press(0)
        mac.press(17)
        mac.press(1)
        mac.press(13)
        mac.press(14)
        expect(0)
        frame('pictures-idle')
        mac.command('trace', count=4096, low=code_address, high=code_address + len(code))
        mac.press(2)
        expect(255)
        frame('pictures-disabled')
        blocked_click()
        mac.press(34)
        expect(254)
        frame('pictures-inactive')
        blocked_click()
        mac.press(0)
        expect(0)
        frame('pictures-active')
        trace('CDEF61-picture-drawing', ('$A8B2', '$A8F6', '$A8B0', '$A8B1'))
        mac.command('trace', count=4096, low=code_address, high=code_address + len(code))
        for index, x, y in [(0, 95, 80), (1, 205, 80), (3, 80, 168), (7, 345, 80)]:
            clicks = int.from_bytes(read(state + 64, 4), 'big')
            mac.mouse(x, y, True)
            mac.wait(0.1)
            expect(10, index)
            if word(state + 60) != 10:
                raise AssertionError('The picture button returned the wrong hit part.')
            frame(f'pictures-pressed-{index}')
            mac.mouse(x, y - 30, True)
            mac.wait(0.1)
            expect(0)
            frame(f'pictures-outside-{index}')
            mac.mouse(x, y, True)
            mac.wait(0.1)
            expect(10, index)
            frame(f'pictures-returned-{index}')
            mac.mouse(x, y, False)
            mac.wait(0.1)
            expect(0)
            if word(state + 62) != 10 or int.from_bytes(read(state + 64, 4), 'big') != clicks + 1:
                raise AssertionError('The picture button did not commit the tracked click.')
            frame(f'pictures-released-{index}')
        trace('CDEF61-picture-tracking', ('$A8AD', '$A8B3'))
        mac.mouse(95, 80, True)
        mac.wait(0.1)
        mac.mouse(95, 50, True)
        mac.wait(0.1)
        mac.mouse(95, 50, False)
        mac.wait(0.1)
        if word(state + 62) != 0:
            raise AssertionError('The picture button committed a cancelled click.')
        frame('pictures-cancelled')
        mac.mouse(95, 80, True)
        mac.wait(0.1)
        clicks = read(state + 64, 4)
        for key in [49, 36, 53]:
            mac.press(key)
            expect(10, 0)
            if read(state + 64, 4) != clicks:
                raise AssertionError('A keyboard event ended native mouse tracking.')
        frame('pictures-keys-held')
        mac.mouse(95, 80, False)
        mac.wait(0.1)
        mac.mouse(60, 70, True)
        mac.wait(0.1)
        expect(10, 0)
        frame('pictures-corner-pressed')
        mac.mouse(60, 70, False)
        mac.wait(0.1)
        if word(state + 62) != 10:
            raise AssertionError('The rectangular hit region excluded a rounded corner.')
        mac.press(5)
        frame('pictures-checker')
        mac.press(2)
        frame('pictures-checker-disabled')
        mac.press(0)
        mac.mouse(95, 80, True)
        mac.wait(0.1)
        frame('pictures-checker-pressed')
        mac.mouse(95, 80, False)
        mac.press(17)
        mac.press(4)
        frame('pictures-hidden')
        mac.press(1)
        frame('pictures-shown')
        mac.press(45)
        frame('pictures-narrow')
        mac.press(13)
        frame('pictures-wide')
        mac.press(2)
        mac.press(4)
        frame('pictures-disabled-hidden')
        mac.press(1)
        frame('pictures-disabled-shown')
        mac.press(45)
        frame('pictures-disabled-narrow')
        mac.press(13)
        frame('pictures-disabled-wide')
        mac.press(0)
        mac.press(31)
        frame('pictures-odd')
        mac.press(2)
        frame('pictures-odd-disabled')
        mac.press(0)
        mac.mouse(95, 80, True)
        mac.wait(0.1)
        frame('pictures-odd-pressed')
        mac.mouse(95, 80, False)
        mac.press(14)
        print('Native picture drawing, disabled states, offsets, clipping, and tracking passed.')


if __name__ == '__main__':
    main()
