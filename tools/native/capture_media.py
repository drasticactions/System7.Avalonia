import argparse
import base64
import io
from pathlib import Path
import struct
import sys

import machfs
import rsrcfork

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'minivmac'))
from client import MiniVMac, frame_rgb


def main():
    parser = argparse.ArgumentParser(description='Run and trace the original CDEF 62 media controls.')
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
            return base64.b64decode(mac.command('memory', address=address & 0xffffff, length=length)['data'], validate=True)

        def number(address, length=2):
            return int.from_bytes(read(address, length), 'big')

        def pointer(address):
            return number(address, 4) & 0xffffff

        mac.command('pause')
        try:
            status = mac.command('status')
            size = status['ram_size']
            ram = b''.join(read(i, min(65536, size - i)) for i in range(0, size, 65536))
        finally:
            mac.command('resume')
        marker = ram.find(b'S7MEDIA!')
        if marker < 0:
            raise AssertionError('The native media probe is not running.')
        state = marker + 8
        handles = [pointer(state + 4 + i * 4) for i in range(14)]
        definition = pointer(pointer(pointer(handles[0]) + 24))
        if read(definition, len(code)) != code:
            raise AssertionError('The executing CDEF 62 differs from the original System resource.')
        if status['depth'] > 1 and number(pointer(state) + 6) & 0xc000 != 0xc000:
            raise AssertionError('The media probe needs a native color window.')
        print(f'CDEF 62: all {len(code)} loaded bytes match; display depth {status["depth"]}.')

        def frame(name):
            mac.wait(0.2)
            mac.command('pause')
            try:
                reply = mac.command('frame')
                data = frame_rgb(reply)
                (args.output / (name + '.ppm')).write_bytes(f'P6\n{reply["width"]} {reply["height"]}\n255\n'.encode() + data)
            finally:
                mac.command('resume')

        def start(offset=0, end=None):
            mac.command('trace', count=4096, low=definition + offset, high=definition + (end or len(code)))

        def trace(name, required):
            mac.command('trace_stop')
            result = mac.command('trace_get')
            pcs = {pc & 0xffffff for pc in result['pcs']}
            (args.output / (name + '.trace')).write_text(result['disassembly'])
            missing = [hex(offset) for offset in required if definition + offset not in pcs]
            if not pcs or missing:
                raise AssertionError(f'The {name} trace missed native branches: {missing}.')
            print(f'{name}: {len(result["pcs"])} instructions, {len(pcs)} unique addresses.')

        def values(expected):
            actual = [number(pointer(handle) + 18) for handle in handles[7:13]]
            if actual != expected:
                raise AssertionError(f'The native speaker values differ: {actual}, expected {expected}.')

        mac.mouse(10, 320)
        frame('media-idle')
        start()
        mac.press(11)
        trace('media-drawing', (0x14a, 0x1d2, 0x336, 0x38a, 0x82))
        frame('media-redrawn')
        for index in range(7):
            x = 74 + index * 40
            if index == 6:
                x = 320
            before = number(state + 92, 4)
            start(0xd8, 0x14a)
            mac.mouse(x, 84, True)
            frame(f'media-pressed-{index}')
            mac.mouse(x, 110, True)
            frame(f'media-outside-{index}')
            mac.mouse(x, 84, True)
            frame(f'media-returned-{index}')
            mac.mouse(x, 84, False)
            frame(f'media-released-{index}')
            if number(state + 92, 4) != before + 1 or number(state + 90) != 10:
                raise AssertionError('The native media button did not commit its tracked click.')
            trace(f'media-tracking-{index}', (0x116, 0x124, 0xd8, 0xee))
        before = number(state + 92, 4)
        mac.mouse(74, 84, True)
        for key in (49, 36, 53):
            mac.press(key)
        frame('media-keys-held')
        mac.mouse(74, 110, True)
        mac.wait(0.1)
        mac.mouse(74, 110, False)
        frame('media-cancelled')
        if number(state + 92, 4) != before + 1 or number(state + 90) != 0:
            raise AssertionError('The native media button did not cancel release outside its bounds.')
        before = number(state + 92, 4)
        start()
        for x, y in [(76, 156), (326, 156), (76, 216)]:
            mac.click(x, y)
            if number(state + 88) != 0 or number(state + 92, 4) != before:
                raise AssertionError('A display-only CDEF 62 variant accepted a mouse hit.')
        trace('media-display-only-hit', (0x116, 0x118))
        frame('media-display-only-clicked')
        start(0x336)
        for key, value in [(29, 0), (18, 16), (19, 17), (20, 33), (21, 34), (23, 50), (22, 66), (26, 67), (28, 99), (25, 100)]:
            mac.press(key)
            values([value] * 6)
            frame(f'speaker-{value}')
        trace('speaker-drawing', (0x362, 0x378, 0x37a, 0x38a, 0x38c))
        mac.press(15)
        frame('speaker-reset')
        mac.press(2)
        frame('media-disabled')
        before = number(state + 92, 4)
        mac.click(74, 84)
        if number(state + 92, 4) != before:
            raise AssertionError('The disabled native media button accepted a click.')
        mac.press(26)
        values([67] * 6)
        frame('media-disabled-update')
        mac.press(34)
        frame('media-inactive')
        mac.press(23)
        frame('media-inactive-update')
        mac.press(0)
        frame('media-reactivated')
        mac.press(15)
        mac.press(4)
        frame('media-hidden')
        mac.press(1)
        frame('media-shown')
        mac.press(45)
        frame('speaker-small')
        mac.press(13)
        frame('speaker-normal')
        mac.press(5)
        frame('media-checker')
        mac.press(2)
        frame('media-checker-disabled')
        mac.press(0)
        mac.press(35)
        frame('media-shifted')
        mac.press(2)
        frame('media-shifted-disabled')
        mac.press(0)
        mac.press(14)
        mac.press(17)
        print('Media tracking, keyboard hold, cancellation, speaker values, disabled state, and display-only input checks passed.')


if __name__ == '__main__':
    main()
