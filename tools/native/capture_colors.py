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
from client import MiniVMac, frame_data


def main():
    parser = argparse.ArgumentParser(description='Trace native color controls and retain their raw framebuffer and palette.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--port', type=int, default=6817)
    parser.add_argument('--launch', action='store_true')
    parser.add_argument('--scrollbars', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    with MiniVMac(args.port) as mac:
        mac.command('trace_stop')
        if args.launch:
            mac.click(232, 94)
            mac.click(232, 94)
            mac.wait(1)

        def read(address, length):
            return base64.b64decode(mac.command('memory', address=address & 0xffffff, length=length)['data'], validate=True)

        def pointer(address):
            return int.from_bytes(read(address, 4), 'big') & 0xffffff

        def number(address, length=2):
            return int.from_bytes(read(address, length), 'big')

        mac.command('pause')
        try:
            status = mac.command('status')
            size = status['ram_size']
            ram = b''.join(read(i, min(65536, size - i)) for i in range(0, size, 65536))
        finally:
            mac.command('resume')
        marker = ram.find(b'S7COLOR!')
        if marker < 0:
            raise AssertionError('The native color probe is not running.')
        gray_prefix = bytes.fromhex('4e56ffe42f0a422e00142d780cc8fffc')
        gray_start = ram.find(gray_prefix)
        if gray_start < 0 or ram.find(gray_prefix, gray_start + 1) >= 0:
            raise AssertionError('The native System 7 GetGray routine is missing or ambiguous.')
        if ram[gray_start + 0xd0:gray_start + 0xda] != bytes.fromhex('245f4e5e70004e74000c'):
            raise AssertionError('The native GetGray routine has a different return sequence.')
        state = marker + 8
        dialog = pointer(state)
        if number(dialog + 6) & 0xc000 != 0xc000 or status['depth'] == 1:
            raise AssertionError('The native dialog needs a color port and an active color display.')
        handles = [pointer(state + 20 + i * 4) for i in range(14)]
        definitions = {}
        for resource_id, item in ((0, 1), (1, 11)):
            code = resources[b'CDEF'][resource_id].data
            address = pointer(pointer(handles[item - 1]) + 24)
            address = pointer(address)
            if read(address, len(code)) != code:
                raise AssertionError(f'The executing CDEF {resource_id} differs from the source disk.')
            definitions[resource_id] = (address, len(code))
            print(f'CDEF {resource_id}: all {len(code)} loaded bytes match the original resource at {address:#x}.')
        print(f'Color port {dialog:#x}, native depth {status["depth"]}, {status["width"]} by {status["height"]}.')

        def value(item):
            return number(pointer(handles[item - 1]) + 18)

        def expect(item, expected):
            actual = value(item)
            if actual != expected:
                raise AssertionError(f'Item {item}: expected {expected}, got {actual}.')

        def redraw_key(code):
            before = number(state + 82, 4)
            mac.press(code)
            deadline = time.monotonic() + 10
            while number(state + 82, 4) == before:
                if time.monotonic() >= deadline:
                    raise AssertionError('The native dialog did not complete its redraw.')
                mac.wait(0.01)

        def frame(name):
            mac.wait(0.3)
            mac.command('pause')
            try:
                reply = mac.frame(args.output / (name + '.ppm'))
                raw = frame_data(reply)
                (args.output / (name + '.pixels')).write_bytes(raw)
                if 'palette' in reply:
                    palette = b''.join(struct.pack('>HHH', *rgb) for rgb in reply['palette'])
                    (args.output / (name + '.palette')).write_bytes(palette)
            finally:
                mac.command('resume')

        def start_trace(resource_id=0, start=0):
            address, length = definitions[resource_id]
            mac.command('trace', count=4096, low=address + start, high=address + length)

        def trace(name, resource_id=0, required=()):
            mac.command('trace_stop')
            result = mac.command('trace_get')
            address, length = definitions[resource_id]
            pcs = {pc & 0xffffff for pc in result['pcs']}
            if not pcs or any(not address <= pc < address + length for pc in pcs):
                raise AssertionError(f'{name} missed the original renderer.')
            if any(address + offset not in pcs for offset in required):
                raise AssertionError(f'{name} missed a required color branch.')
            (args.output / (name + '.trace')).write_text(result['disassembly'])
            print(f'{name}: {len(result["pcs"])} executed instructions, {len(pcs)} unique addresses.')

        def scrollbars(name):
            def save(suffix):
                frame(name + '-scroll-' + suffix)

            def reset():
                mac.click(345, 80)
                expect(11, 50)
                expect(12, 50)
                mac.mouse(10, 350)

            reset()
            start_trace(1)
            redraw_key(15)
            trace(name + '-scroll-drawing', 1, (0x862, 0x8a8) if status['depth'] >= 4 else (0x862, 0x2d0))
            save('enabled')
            for label, point, outside in [('up', (438, 78), (460, 78)), ('down', (438, 262), (460, 262)),
                                          ('left', (68, 268), (68, 245)), ('right', (402, 268), (402, 245))]:
                start_trace(1)
                mac.mouse(*point, True)
                save(label + '-pressed')
                mac.mouse(*outside, True)
                save(label + '-outside')
                mac.mouse(*point, True)
                save(label + '-returned')
                mac.mouse(*point, False)
                save(label + '-released')
                expect(11, 50)
                expect(12, 50)
                trace(name + '-scroll-' + label + '-tracking', 1, (0x472,))
            for label, item, start, moved, outside, expected in [
                    ('vertical', 11, (438, 169), (438, 205), (470, 205), 74),
                    ('horizontal', 12, (235, 268), (271, 268), (271, 300), 62)]:
                reset()
                start_trace(1)
                mac.mouse(*start, True)
                save(label + '-thumb-down')
                mac.mouse(*moved, True)
                save(label + '-thumb-drag')
                expect(item, 50)
                mac.mouse(*outside, True)
                save(label + '-thumb-outside')
                mac.mouse(*moved, True)
                save(label + '-thumb-return')
                moved = (moved[0] + (item == 12), moved[1] + (item == 11))
                mac.mouse(*moved, True)
                save(label + '-thumb-return-moved')
                mac.mouse(*moved, False)
                save(label + '-thumb-release')
                expect(item, expected)
                trace(name + '-scroll-' + label + '-thumb-tracking', 1, (0x7b4, 0xbf2))
                reset()
                mac.mouse(*start, True)
                mac.mouse(*outside, True)
                mac.mouse(*outside, False)
                save(label + '-thumb-cancel')
                expect(item, 50)
            reset()
            redraw_key(2)
            save('disabled')
            mac.click(438, 78)
            expect(11, 50)
            save('disabled-click')
            redraw_key(34)
            if number(pointer(handles[10]) + 16) & 255 != 254:
                raise AssertionError('The native scrollbar did not enter its inactive state.')
            save('inactive')
            redraw_key(14)
            save('reenabled')

        mac.mouse(10, 350)
        for name, key, table in [('default', 6, 129), ('custom', 8, 128)]:
            redraw_key(key)
            if number(state + 80) != table:
                raise AssertionError('The native color table did not change.')
            redraw_key(14)
            if args.scrollbars:
                scrollbars(name)
                continue
            expect(4, 0)
            expect(5, 1)
            expect(7, 0)
            expect(8, 1)
            start_trace()
            redraw_key(15)
            trace(name + '-drawing', required=(0x54, 0x5a, 0x17c, 0x2c8, 0x308))
            frame(name + '-enabled')
            start_trace()
            mac.mouse(100, 80, True)
            frame(name + '-button-pressed')
            mac.mouse(100, 100, True)
            frame(name + '-button-outside')
            mac.mouse(100, 80, True)
            frame(name + '-button-returned')
            mac.mouse(100, 80, False)
            frame(name + '-button-released')
            trace(name + '-button-tracking', required=(0x2ae, 0x17c))
            start_trace(start=0x468)
            mac.mouse(100, 125, True)
            frame(name + '-check-pressed')
            mac.mouse(100, 125, False)
            frame(name + '-check-selected')
            expect(4, 1)
            mac.mouse(100, 125, True)
            frame(name + '-check-selected-pressed')
            mac.mouse(100, 100, True)
            frame(name + '-check-outside')
            mac.mouse(100, 100, False)
            mac.wait(0.15)
            expect(4, 1)
            mac.click(100, 125)
            expect(4, 0)
            trace(name + '-check-tracking', required=(0x472, 0x4ea, 0x4f4, 0x516))
            start_trace(start=0x468)
            mac.mouse(300, 125, True)
            frame(name + '-radio-pressed')
            mac.mouse(300, 125, False)
            frame(name + '-radio-selected')
            expect(7, 1)
            expect(8, 0)
            mac.click(300, 155)
            expect(7, 0)
            expect(8, 1)
            trace(name + '-radio-tracking', required=(0x4f8, 0x53e))
            start_trace()
            redraw_key(2)
            trace(name + '-disabled-drawing', required=(0x2be, 0x2c8, 0x326))
            mac.command('trace', count=4096, low=gray_start, high=gray_start + 0xda)
            redraw_key(15)
            mac.command('trace_stop')
            gray_trace = mac.command('trace_get')
            gray_pcs = [pc & 0xffffff for pc in gray_trace['pcs']]
            if any(gray_start + offset not in gray_pcs for offset in (0x54, 0xb6, 0xd0)):
                raise AssertionError('Disabled text did not execute the native GetGray color comparison.')
            (args.output / (name + '-getgray.trace')).write_text(gray_trace['disassembly'])
            print(f'{name}-getgray: {len(gray_pcs)} executed instructions, '
                  f'{gray_pcs.count(gray_start + 0xbe)} solid-color results.')
            frame(name + '-disabled')
            for item, point in ((4, (100, 125)), (7, (300, 125))):
                mac.click(*point)
                expect(item, 0)
            frame(name + '-disabled-click')
            redraw_key(14)
            frame(name + '-reenabled')
            redraw_key(35)
            if number(pointer(handles[0]) + 10) != 21:
                raise AssertionError('The native control did not move one pixel to the right.')
            frame(name + '-shifted-enabled')
            redraw_key(2)
            frame(name + '-shifted-disabled')
            redraw_key(35)
            if number(pointer(handles[0]) + 10) != 20:
                raise AssertionError('The native control did not return to its original position.')
            redraw_key(14)
        print('Native color table changes, press tracking, drag cancellation, selection, and disabled input checks passed.')


if __name__ == '__main__':
    main()
