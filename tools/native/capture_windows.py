import argparse
import base64
import io
import hashlib
from pathlib import Path
import struct
import sys

import machfs
import rsrcfork

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'minivmac'))
from client import MiniVMac


def main():
    parser = argparse.ArgumentParser(description='Capture the native WDEF window probe through Mini vMac TCP.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--rom', type=Path, help='Mac Plus v3 ROM for Window Manager trace checks.')
    parser.add_argument('--port', type=int, default=6807)
    parser.add_argument('--launch', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    code = resources[b'WDEF'][0].data
    if args.rom and hashlib.sha256(args.rom.read_bytes()).hexdigest() != 'dd908e2b65772a6b1f0c859c24e9a0d3dcde17b1c6a24f4abd8955846d7895e7':
        raise ValueError('Window Manager traces require the Mac Plus v3 ROM.')
    with MiniVMac(args.port) as mac:
        if args.launch:
            mac.click(232, 94)
            mac.click(232, 94)
            mac.wait(1)

        def read(address, length):
            return base64.b64decode(mac.command('memory', address=address & 0xffffff, length=length)['data'], validate=True)

        mac.command('pause')
        try:
            size = mac.command('status')['ram_size']
            ram = b''.join(read(address, min(65536, size - address)) for address in range(0, size, 65536))
        finally:
            mac.command('resume')
        marker = ram.find(b'S7WINDOW')
        code_address = ram.find(code)
        if marker < 0 or code_address < 0:
            raise RuntimeError('The window probe or its native WDEF is missing from RAM.')
        state = marker + 8
        window = struct.unpack_from('>I', ram, state)[0] & 0xffffff

        def bounds(expected):
            record = read(window, 24)
            origin = struct.unpack_from('>hh', record, 8)
            local = struct.unpack_from('>hhhh', record, 16)
            actual = tuple(local[i] - origin[i % 2] for i in range(4))
            if actual != expected:
                raise AssertionError(f'Native window bounds: expected {expected}, got {actual}.')

        def closes():
            return struct.unpack('>H', read(state + 30, 2))[0]

        def frame(name):
            mac.wait(0.15)
            mac.frame(args.output / (name + '.pbm'))

        def trace_start(draw_only=False):
            start = {'pc': code_address + 0x256} if draw_only else {}
            mac.command('trace', count=4096, low=code_address, high=code_address + len(code), **start)

        def trace_end(name):
            mac.command('trace_stop')
            trace = mac.command('trace_get')
            if not trace['pcs'] or not all(code_address <= (pc & 0xffffff) < code_address + len(code) for pc in trace['pcs']):
                raise AssertionError('The executed trace is empty or outside WDEF 0.')
            (args.output / (name + '.trace')).write_text(trace['disassembly'])
            print(f'{name}: {len(trace["pcs"])} executed instructions, {len(set(trace["pcs"]))} unique addresses.')

        original = (80, 45, 275, 325)
        mac.press(1)
        bounds(original)
        frame('window-active')
        mac.press(4)
        frame('window-background')
        trace_start()
        mac.press(1)
        frame('window-restored')
        trace_end('WDEF0-draw')
        initial_closes = closes()
        trace_start()
        mac.mouse(59, 70, True)
        frame('window-close')
        mac.mouse(90, 70, True)
        frame('window-close-outside')
        mac.mouse(90, 70, False)
        mac.wait(0.15)
        if closes() != initial_closes:
            raise AssertionError('Native close tracking accepted release outside its box.')
        mac.click(59, 70)
        if closes() != initial_closes + 1:
            raise AssertionError('Native close tracking did not accept release inside its box.')
        trace_end('WDEF0-close')
        trace_start()
        mac.mouse(311, 70, True)
        frame('window-zoom')
        mac.mouse(290, 70, True)
        frame('window-zoom-outside')
        mac.mouse(290, 70, False)
        mac.wait(0.15)
        bounds(original)
        trace_end('WDEF0-zoom-tracking')
        trace_start()
        mac.click(420, 150)
        frame('window-inactive')
        if read(window + 111, 1) != b'\0':
            raise AssertionError('The native window did not deactivate.')
        mac.click(100, 120)
        frame('window-reactivated')
        trace_end('WDEF0-activation')
        trace_start(draw_only=True)
        mac.mouse(317, 267, True)
        mac.wait(0.15)
        mac.mouse(347, 287, True)
        frame('window-grow-outline')
        mac.mouse(347, 287, False)
        frame('window-grown')
        bounds((80, 45, 295, 355))
        mac.mouse(347, 287, True)
        mac.wait(0.15)
        mac.mouse(317, 267, True)
        mac.wait(0.15)
        mac.mouse(317, 267, False)
        mac.wait(0.15)
        bounds(original)
        trace_end('WDEF0-grow')
        trace_start(draw_only=True)
        mac.click(311, 70)
        frame('window-maximized')
        bounds((42, 3, 339, 509))
        mac.click(495, 33)
        mac.wait(0.15)
        bounds(original)
        trace_end('WDEF0-zoom')
        trace_start(draw_only=True)
        mac.mouse(100, 70, True)
        mac.wait(0.15)
        mac.mouse(120, 90, True)
        frame('window-drag-outline')
        mac.mouse(120, 90, False)
        frame('window-moved')
        bounds((100, 65, 295, 345))
        mac.mouse(120, 90, True)
        mac.wait(0.15)
        mac.mouse(100, 70, True)
        mac.wait(0.15)
        mac.mouse(100, 70, False)
        mac.wait(0.15)
        bounds(original)
        trace_end('WDEF0-drag')
        mac.mouse(100, 70, True)
        mac.wait(0.15)
        mac.mouse(0, 0, True)
        frame('window-drag-outside')
        mac.mouse(0, 0, False)
        mac.wait(0.15)
        bounds(original)
        mac.mouse(317, 267, True)
        mac.wait(0.15)
        mac.mouse(0, 0, True)
        frame('window-grow-minimum-outline')
        mac.mouse(0, 0, False)
        frame('window-minimum')
        bounds((80, 45, 160, 165))
        mac.mouse(157, 152, True)
        mac.wait(0.15)
        mac.mouse(317, 267, True)
        mac.wait(0.15)
        mac.mouse(317, 267, False)
        mac.wait(0.15)
        bounds(original)
        mac.mouse(317, 267, True)
        mac.wait(0.15)
        mac.mouse(511, 341, True)
        mac.mouse(511, 341, False)
        mac.wait(0.15)
        bounds((80, 45, 349, 504))
        mac.mouse(496, 341, True)
        mac.wait(0.15)
        mac.mouse(317, 267, True)
        mac.wait(0.15)
        mac.mouse(317, 267, False)
        mac.wait(0.15)
        bounds(original)
        mac.click(420, 150)
        mac.key(55, True)
        try:
            mac.mouse(100, 70, True)
            mac.wait(0.15)
            mac.mouse(120, 90, True)
            frame('window-command-drag-outline')
            mac.mouse(120, 90, False)
            frame('window-command-moved')
            bounds((100, 65, 295, 345))
            if read(window + 111, 1) != b'\0':
                raise AssertionError('Command-drag activated the background window.')
            mac.mouse(120, 90, True)
            mac.wait(0.15)
            mac.mouse(100, 70, True)
            mac.wait(0.15)
            mac.mouse(100, 70, False)
            mac.wait(0.15)
            bounds(original)
        finally:
            mac.key(55, False)
        mac.click(100, 120)
        if args.rom:
            for name, low, high, start, end in [
                ('DragWindow', 0x411d9c, 0x411e52, (100, 70), (120, 90)),
                ('GrowWindow', 0x411ff4, 0x4120f6, (337, 287), (357, 297)),
            ]:
                mac.command('trace', count=4096, pc=low, low=low, high=high)
                mac.mouse(*start, True)
                mac.wait(0.15)
                mac.mouse(*end, True)
                mac.wait(0.15)
                mac.mouse(*end, False)
                mac.wait(0.15)
                mac.command('trace_stop')
                trace = mac.command('trace_get')
                if not trace['pcs'] or not all(low <= (pc & 0xffffff) < high for pc in trace['pcs']):
                    raise AssertionError('The Window Manager trace is empty or outside its ROM routine.')
                (args.output / (name + '.trace')).write_text(trace['disassembly'])
                print(f'{name}: {len(trace["pcs"])} executed instructions.')
            mac.mouse(357, 297, True)
            mac.wait(0.15)
            mac.mouse(337, 287, True)
            mac.wait(0.15)
            mac.mouse(337, 287, False)
            mac.wait(0.15)
            mac.mouse(120, 90, True)
            mac.wait(0.15)
            mac.mouse(100, 70, True)
            mac.wait(0.15)
            mac.mouse(100, 70, False)
            mac.wait(0.15)
            bounds(original)
        print('Native close cancellation, activation, resize limits, zoom, movement, and Command-drag assertions passed.')


if __name__ == '__main__':
    main()
