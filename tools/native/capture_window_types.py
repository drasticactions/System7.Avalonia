import argparse
import base64
import hashlib
import io
from pathlib import Path
import struct
import sys

import machfs
import rsrcfork

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'minivmac'))
from client import MiniVMac


VARIANTS = [1, 2, 3, 5, 13, 0, 4, 8, 12]
KEYS = [18, 19, 20, 21, 23, 22, 26, 28, 25]


def main():
    parser = argparse.ArgumentParser(description='Trace native WDEF 0 dialog and document variants.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--port', type=int, default=6807)
    parser.add_argument('--rom', type=Path)
    parser.add_argument('--variants', type=int, nargs='+', choices=VARIANTS, default=VARIANTS)
    parser.add_argument('--launch', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    code = resources[b'WDEF'][0].data
    rom = args.rom.read_bytes() if args.rom else None
    plus_rom = rom is not None and hashlib.sha256(rom).hexdigest() == 'dd908e2b65772a6b1f0c859c24e9a0d3dcde17b1c6a24f4abd8955846d7895e7'
    if rom and not plus_rom and hashlib.sha256(rom).hexdigest() != '79fae48e2d5cfde68520e46616503963f8c16430903f410514b62c1379af20cb':
        raise ValueError('The window trace requires the verified Mac Plus or Mac II ROM.')
    with MiniVMac(args.port) as mac:
        mac.command('trace_stop')
        mac.command('resume')

        def read(address, length):
            base = 0x400000 if plus_rom else 0x800000
            if rom and base <= address < base + len(rom):
                return rom[address - base:address - base + length]
            return base64.b64decode(mac.command('memory', address=address & 0xffffff, length=length)['data'])

        def pointer(address):
            return int.from_bytes(read(address, 4), 'big') & 0xffffff

        marker = -1
        for _ in range(20 if args.launch else 1):
            if args.launch:
                mac.mouse(10, 320)
                mac.wait(.2)
                mac.click(232, 94)
                mac.click(232, 94)
                mac.wait(1)
            mac.command('pause')
            try:
                size = mac.command('status')['ram_size']
                ram = b''.join(read(i, min(65536, size - i)) for i in range(0, size, 65536))
            finally:
                mac.command('resume')
            marker = ram.find(b'S7WTYPES')
            if marker >= 0:
                break
        if marker < 0:
            raise AssertionError('The window variant probe is not running.')
        state = marker + 8
        color = mac.command('status')['depth'] > 1
        gray_region = pointer(pointer(0x9ee))
        region_size = int.from_bytes(read(gray_region, 2), 'big')
        (args.output / 'desktop.rgn').write_bytes(read(gray_region, region_size))

        def frame(name):
            previous = None
            for _ in range(20):
                mac.wait(.15)
                mac.command('pause')
                try:
                    reply = mac.command('frame')
                    if reply['data'] == previous:
                        mac.frame(args.output / (name + ('.ppm' if color else '.pbm')))
                        return
                    previous = reply['data']
                finally:
                    mac.command('resume')
            raise AssertionError(f'The native window frame did not settle: {name}.')

        for variant, key in zip(VARIANTS, KEYS):
            if variant not in args.variants:
                continue
            mac.press(key)
            if struct.unpack('>h', read(state + 36, 2))[0] != 128 + VARIANTS.index(variant):
                raise AssertionError('The requested window variant was not created.')
            window = pointer(state)
            code_address = pointer(pointer(window + 126))
            if read(code_address, len(code)) != code:
                raise AssertionError('The running WDEF differs from the System resource.')
            bounds = struct.unpack('>hhhh', read(pointer(pointer(window + 114)) + 2, 8))
            print(f'Variant {variant}: structure {bounds}, close flag {read(window + 112, 1)[0]}.', flush=True)
            frame(f'variant-{variant}-active')
            mac.click(450, 150)
            if read(window + 111, 1) != b'\0':
                raise AssertionError('The window did not deactivate.')
            frame(f'variant-{variant}-inactive')
            code_address = pointer(pointer(window + 126))
            if read(code_address, len(code)) != code:
                raise AssertionError('The settled WDEF differs from the System resource.')
            mac.command('trace', count=4096, low=code_address, high=code_address + len(code))
            mac.click(200, 160)
            if read(window + 111, 1) == b'\0':
                raise AssertionError('The window did not activate.')
            frame(f'variant-{variant}-reactivated')
            mac.press(4)
            frame(f'variant-{variant}-hidden')
            mac.press(1)
            frame(f'variant-{variant}-shown')
            mac.command('trace_stop')
            trace = mac.command('trace_get')
            (args.output / f'WDEF0-variant-{variant}.trace').write_text(trace['disassembly'])
            if not trace['pcs'] or '$A8A1' not in trace['disassembly']:
                raise AssertionError(f'The window trace did not execute frame drawing: {len(trace["pcs"])} instructions at {code_address:#x}.')
            print(f'Variant {variant}: {len(trace["pcs"])} executed instructions, {len(set(trace["pcs"]))} unique addresses.', flush=True)
            top, left, bottom, right = bounds
            for label, x, y in [
                ('top', 200, top), ('left', left, 160), ('bottom', 200, bottom - 1),
                ('right', right - 1, 160), ('title', 200, top + 10), ('content', 200, 100),
                ('top-right', right - 1, top), ('bottom-left', left, bottom - 1),
            ]:
                mac.mouse(x, y)
                mac.press(35)
                part = struct.unpack('>h', read(state + 28, 2))[0]
                own = pointer(state + 24) == window
                excluded = variant in (0, 3, 4, 8, 12) and label in ('top-right', 'bottom-left')
                title = variant in (0, 4, 5, 8, 12, 13) and label in ('top', 'title', 'top-right')
                if own == excluded or not excluded and part != (4 if title else 3):
                    raise AssertionError('The window hit result differs from its native structure.')
                print(f'Variant {variant} hit {label} at {x},{y}: {part}, target={own}.', flush=True)
            if variant in (0, 4, 5, 8, 12, 13):
                mac.mouse(200, top + 10, True)
                mac.wait(0.12)
                mac.mouse(215, top + 20, True)
                frame(f'variant-{variant}-drag-outline')
                mac.mouse(215, top + 20, False)
                frame(f'variant-{variant}-moved')
                moved = struct.unpack('>hhhh', read(pointer(pointer(window + 114)) + 2, 8))
                if moved != (top + 10, left + 15, bottom + 10, right + 15):
                    raise AssertionError('The window moved by an unexpected amount.')
            mac.press(15)
            if variant in (5, 13):
                window = pointer(state)
                mac.click(450, 150)
                mac.key(55, True)
                try:
                    if plus_rom:
                        mac.command('trace', count=4096, low=0x411974, high=0x411a20, pc=0x411974)
                    mac.mouse(200, top + 10, True)
                    mac.wait(0.12)
                    mac.mouse(215, top + 20, True)
                    frame(f'variant-{variant}-command-outline')
                    mac.mouse(215, top + 20, False)
                    frame(f'variant-{variant}-command-moved')
                    if read(window + 111, 1) != b'\0':
                        raise AssertionError('Command-drag activated the dialog.')
                    if plus_rom:
                        mac.command('trace_stop')
                        trace = mac.command('trace_get')
                        if 0x4119d6 not in [pc & 0xffffff for pc in trace['pcs']] or '$A8EC' not in trace['disassembly']:
                            raise AssertionError('MoveWindow did not execute its native CopyBits call.')
                        (args.output / f'MoveWindow-variant-{variant}.trace').write_text(trace['disassembly'])
                        print(f'MoveWindow {variant}: {len(trace["pcs"])} instructions; native CopyBits executed.', flush=True)
                finally:
                    mac.key(55, False)
                mac.command('trace', count=4096, low=code_address, high=code_address + len(code))
                mac.press(17)
                frame(f'variant-{variant}-command-retitled')
                mac.command('trace_stop')
                trace = mac.command('trace_get')
                if not trace['pcs'] or '$A8A1' not in trace['disassembly'] or '$A884' not in trace['disassembly']:
                    raise AssertionError('Retitling did not draw the native frame and text.')
                (args.output / f'WDEF0-variant-{variant}-retitle.trace').write_text(trace['disassembly'])
                mac.press(15)
            if variant in (8, 12, 13):
                window = pointer(state)
                x, y = right - 16, top + 10
                mac.mouse(x, y, True)
                mac.wait(0.12)
                if struct.unpack('>h', read(state + 28, 2))[0] != 8:
                    raise AssertionError('The zoom box returned an unexpected part.')
                frame(f'variant-{variant}-zoom-pressed')
                mac.mouse(x - 30, y, True)
                frame(f'variant-{variant}-zoom-outside')
                mac.mouse(x, y, True)
                frame(f'variant-{variant}-zoom-returned')
                mac.mouse(x, y, False)
                frame(f'variant-{variant}-maximized')
                zoomed = struct.unpack('>hhhh', read(pointer(pointer(window + 114)) + 2, 8))
                print(f'Variant {variant}: zoomed structure {zoomed}.', flush=True)
                mac.click(zoomed[3] - 16, zoomed[0] + 10)
                for _ in range(100):
                    restored = struct.unpack('>hhhh', read(pointer(pointer(window + 114)) + 2, 8))
                    if restored == bounds:
                        break
                    mac.wait(.05)
                else:
                    raise AssertionError(f'The zoom box did not restore the window: {restored}.')
                frame(f'variant-{variant}-zoom-restored')
            if variant in (0, 4, 8, 12):
                window = pointer(state)
                count = struct.unpack('>h', read(state + 30, 2))[0]
                x, y = left + 14, top + 9
                mac.command('trace', count=4096, low=code_address, high=code_address + len(code))
                mac.mouse(x, y, True)
                frame(f'variant-{variant}-close-pressed')
                for key in (36, 53, 48, 49):
                    mac.press(key)
                frame(f'variant-{variant}-close-keys')
                mac.mouse(x + 30, y, True)
                frame(f'variant-{variant}-close-outside')
                mac.mouse(x, y, True)
                frame(f'variant-{variant}-close-returned')
                mac.mouse(x + 30, y, False)
                frame(f'variant-{variant}-close-canceled')
                if struct.unpack('>h', read(state + 30, 2))[0] != count:
                    raise AssertionError('Releasing outside the close box accepted the click.')
                mac.click(x, y)
                frame(f'variant-{variant}-close-released')
                if struct.unpack('>h', read(state + 30, 2))[0] != count + 1:
                    raise AssertionError('Releasing inside the close box did not accept the click.')
                mac.command('trace_stop')
                trace = mac.command('trace_get')
                if '$A8EC' not in trace['disassembly']:
                    raise AssertionError('Close tracking did not execute the native bitmap drawing.')
                (args.output / f'WDEF0-variant-{variant}-close.trace').write_text(trace['disassembly'])
            if variant in (0, 8):
                x, y = right - 8, bottom - 8
                mac.command('trace', count=4096, low=code_address, high=code_address + len(code))
                mac.mouse(x, y, True)
                frame(f'variant-{variant}-grow-pressed')
                for label, px, py in [('outline', x + 20, y + 12), ('minimum', 0, 0),
                                      ('maximum', 639 if color else 511, 479 if color else 341), ('returned', x + 20, y + 12)]:
                    mac.mouse(px, py, True)
                    frame(f'variant-{variant}-grow-{label}')
                mac.mouse(x + 20, y + 12, False)
                frame(f'variant-{variant}-grown')
                grown = struct.unpack('>hhhh', read(pointer(pointer(window + 114)) + 2, 8))
                if grown != (top, left, bottom + 12, right + 20):
                    raise AssertionError(f'The grow box returned unexpected bounds: {grown}.')
                mac.command('trace_stop')
                trace = mac.command('trace_get')
                if '$A8A1' not in trace['disassembly']:
                    raise AssertionError('The grow box did not execute native outline drawing.')
                (args.output / f'WDEF0-variant-{variant}-grow.trace').write_text(trace['disassembly'])
                mac.press(15)
            for key, name in [(45, 'compact'), (46, 'tiny')]:
                mac.press(key)
                frame(f'variant-{variant}-{name}')
            mac.press(15)
        print('Native window variant drawing, activation, visibility, hit testing, and dragging passed.')


if __name__ == '__main__':
    main()
