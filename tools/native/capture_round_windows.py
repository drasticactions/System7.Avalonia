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
from client import MiniVMac, frame_rgb


VARIANTS = [16, 18, 20, 22, 24, 26, 28, 30, 17]
KEYS = [18, 19, 20, 21, 23, 22, 26, 28, 25]


def main():
    parser = argparse.ArgumentParser(description='Run and trace every rounded-window shape in the original WDEF 1.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--port', type=int, default=6807)
    parser.add_argument('--rom', type=Path)
    parser.add_argument('--launch', action='store_true')
    parser.add_argument('--hit-tests-only', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=args.hit_tests_only)
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    code = resources[b'WDEF'][1].data
    rom = args.rom.read_bytes() if args.rom else None
    if rom and hashlib.sha256(rom).hexdigest() != '79fae48e2d5cfde68520e46616503963f8c16430903f410514b62c1379af20cb':
        raise ValueError('The rounded-window ROM trace requires the verified Mac II ROM.')
    with MiniVMac(args.port) as mac:
        mac.command('trace_stop')
        mac.command('resume')
        def read(address, length):
            return base64.b64decode(mac.command('memory', address=address & 0xffffff, length=length)['data'], validate=True)

        def number(address, length=2):
            return int.from_bytes(read(address, length), 'big')

        def pointer(address):
            return number(address, 4) & 0xffffff

        def verify_definition(address):
            if address < 0x800000:
                return read(address, len(code)) == code
            if rom is None:
                raise ValueError('Pass --rom for the WDEF 1 code that the Mac II executes from ROM.')
            actual = bytearray(rom[address - 0x800000:address - 0x800000 + len(code)])
            # The ROM uses ADDA.W where the disk resource uses equivalent LEA instructions.
            for offset, source, target in [(0xde, b'\xde\xfc', b'\x4f\xef'),
                                            (0x264, b'\xd2\xfc', b'\x43\xe9'), (0x3cc, b'\xde\xfc', b'\x4f\xef')]:
                if actual[offset:offset + 2] != source:
                    return False
                actual[offset:offset + 2] = target
            return actual == code

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
            raise AssertionError('The native window probe is not running.')
        state = marker + 8

        def frame(name, regions=False):
            previous = None
            for _ in range(20):
                mac.wait(.15)
                mac.command('pause')
                try:
                    reply = mac.command('frame')
                    if reply['data'] == previous:
                        (args.output / (name + '.ppm')).write_bytes(f'P6\n{reply["width"]} {reply["height"]}\n255\n'.encode() + frame_rgb(reply))
                        if regions:
                            window = pointer(state)
                            for label, offset in [('structure', 114), ('content', 118)]:
                                data = pointer(pointer(window + offset))
                                (args.output / (name + '-' + label + '.rgn')).write_bytes(read(data, number(data)))
                        return
                    previous = reply['data']
                finally:
                    mac.command('resume')
            raise AssertionError(f'The native frame did not settle: {name}.')

        def trace(name, required):
            mac.command('trace_stop')
            result = mac.command('trace_get')
            (args.output / (name + '.trace')).write_text(result['disassembly'])
            pcs = {pc & 0xffffff for pc in result['pcs']}
            missing = [hex(offset) for offset in required if definition + offset not in pcs]
            if missing:
                raise AssertionError(f'The {name} trace missed native branches: {missing}.')
            print(f'{name}: {len(result["pcs"])} instructions, {len(pcs)} unique addresses.', flush=True)

        for index, (variant, key) in enumerate(zip(VARIANTS, KEYS)):
            mac.press(key)
            window = pointer(state)
            definition = pointer(pointer(window + 126))
            if number(state + 36) != 128 + index or not verify_definition(definition):
                raise AssertionError('The running rounded-window definition or variant differs from System 7.0.1.')
            prefix = f'round-{variant}'
            if args.hit_tests_only:
                mac.command('trace', count=4096, low=definition + 0x3d2, high=definition + 0x45e)
                for label, width, height, active in [('active', 322, 200, True), ('inactive', 322, 200, False),
                                                       ('tiny', 26, 28, True), ('tiny-inactive', 26, 28, False)]:
                    if label == 'tiny':
                        mac.click(200, 160)
                        mac.press(46)
                    if not active:
                        mac.click(450, 150)
                    hits = bytearray()
                    for x, y in [(0, 0), (1, 4), (8, 0), (9, 5), (19, 15), (20, 15), (8, 5), (9, 4),
                                 (1, 19), (0, 19), (1, 27), (width - 2, 27), (width - 1, 27),
                                 (width // 2, 0), (width // 2, 18), (width // 2, 19), (width // 2, height - 1),
                                 (1, height - 3), (2, height - 2), (width - 2, height - 2), (width // 2, height - 2)]:
                        mac.mouse(79 + x, 61 + y)
                        mac.key(35, True)
                        mac.wait(.05)
                        mac.key(35, False)
                        mac.wait(.05)
                        part = number(state + 28) if pointer(state + 24) == window else 0
                        hits.extend(struct.pack('>hhh', x, y, part))
                    (args.output / f'{prefix}-{label}.hits').write_bytes(hits)
                trace(prefix + '-hit-testing', (0x3d2, 0x3e6, 0x452))
                continue
            mac.mouse(10, 320)
            frame(prefix + '-active', True)
            mac.command('trace', count=4096, low=definition + 0x160, high=definition + 0x320)
            mac.click(450, 150)
            if read(window + 111, 1) != b'\0':
                raise AssertionError('The rounded window did not deactivate.')
            frame(prefix + '-inactive')
            mac.click(200, 160)
            frame(prefix + '-reactivated')
            trace(prefix + '-drawing', (0x160, 0x1b2, 0x1f0, 0x234, 0x2be))
            before = number(state + 30)
            mac.command('trace', count=4096, low=definition + 0x23c, high=definition + 0x4b6)
            mac.mouse(93, 71, True)
            frame(prefix + '-close-pressed')
            mac.mouse(130, 71, True)
            frame(prefix + '-close-outside')
            mac.mouse(93, 71, True)
            frame(prefix + '-close-returned')
            mac.mouse(93, 71, False)
            frame(prefix + '-close-released')
            if number(state + 30) != before + 1:
                raise AssertionError('TrackGoAway did not accept the rounded close button.')
            trace(prefix + '-close-tracking', (0x25e, 0x290, 0x3d2, 0x452))
            mac.mouse(93, 71, True)
            mac.wait(.15)
            for key in (49, 36, 53):
                mac.press(key)
            frame(prefix + '-close-keys-held')
            mac.mouse(130, 71, True)
            mac.wait(.15)
            mac.mouse(130, 71, False)
            frame(prefix + '-close-cancelled')
            if number(state + 30) != before + 1:
                raise AssertionError('A cancelled rounded close button committed its action.')
            mac.mouse(200, 71, True)
            mac.wait(.15)
            mac.mouse(215, 81, True)
            frame(prefix + '-drag-outline')
            mac.mouse(215, 81, False)
            frame(prefix + '-moved', True)
            mac.press(15)
            window = pointer(state)
            definition = pointer(pointer(window + 126))
            mac.press(4)
            frame(prefix + '-hidden')
            mac.press(1)
            frame(prefix + '-shown')
            mac.click(450, 150)
            mac.key(55, True)
            try:
                mac.mouse(200, 71, True)
                mac.wait(.15)
                mac.mouse(215, 81, True)
                frame(prefix + '-command-outline')
                mac.mouse(215, 81, False)
                frame(prefix + '-command-moved')
            finally:
                mac.key(55, False)
            if read(window + 111, 1) != b'\0':
                raise AssertionError('Command-drag activated the rounded window.')
            mac.press(17)
            frame(prefix + '-command-retitled')
            mac.press(15)
            window = pointer(state)
            definition = pointer(pointer(window + 126))
            mac.command('trace', count=4096, low=definition + 0x320, high=definition + 0x4b6)
            mac.press(45)
            frame(prefix + '-compact', True)
            mac.press(46)
            frame(prefix + '-tiny', True)
            trace(prefix + '-regions', (0x346, 0x364, 0x38e, 0x45e, 0x4a0))
            mac.press(17)
            frame(prefix + '-tiny-retitled')
            mac.click(450, 150)
            frame(prefix + '-tiny-inactive')
            print(f'Variant {variant}: drawing, regions, close tracking, keyboard hold, cancellation, and window drag passed.', flush=True)


if __name__ == '__main__':
    main()
