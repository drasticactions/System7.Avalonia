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
    parser = argparse.ArgumentParser(description='Trace native List Manager drawing and selection through Mini vMac TCP.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--port', type=int, default=6807)
    parser.add_argument('--launch', action='store_true')
    parser.add_argument('--highlights-only', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    with MiniVMac(args.port) as mac:
        mac.command('trace_stop')
        mac.command('resume')
        mac.press(8)

        def read(address, length):
            return base64.b64decode(mac.command('memory', address=address & 0xffffff, length=length)['data'], validate=True)

        def pointer(address):
            return int.from_bytes(read(address, 4), 'big') & 0xffffff

        for _ in range(20 if args.launch else 1):
            mac.command('pause')
            try:
                status = mac.command('status')
                size = status['ram_size']
                ram = b''.join(read(i, min(65536, size - i)) for i in range(0, size, 65536))
            finally:
                mac.command('resume')
            marker = ram.find(b'S7LISTS!')
            if marker >= 0 and ram[marker + 56:marker + 58] == b'\0\x01' and ram[0x910:0x919] == b'\x08Controls':
                break
            if args.launch:
                mac.mouse(10, 320)
                mac.wait(.2)
                mac.click(232, 94)
                mac.click(232, 94)
                mac.wait(1)
        else:
            raise AssertionError('The foreground native list probe is not ready.')
        color = status.get('depth', 1) > 1
        extension = '.ppm' if color else '.pbm'
        if color:
            (args.output / 'hilite-rgb.bin').write_bytes(read(0xda0, 6))
        record = pointer(pointer(marker + 12))
        ldef = resources[b'LDEF'][0].data
        ldef_address = pointer(pointer(record + 64))
        pack = resources[b'PACK'][0].data
        pack_address = ram.find(pack)
        if read(ldef_address, len(ldef)) != ldef or pack_address < 0:
            raise AssertionError('The running list routines do not match the System resources.')

        def expect(selected, scroll=0, active=True):
            data = read(record, 112)
            actual = [i for i in range(12) if data[86 + i * 2] & 128]
            first = struct.unpack_from('>h', data, 20)[0]
            if (actual, first, bool(data[37])) != (selected, scroll, active):
                raise AssertionError(f'Native list: expected {(selected, scroll, active)}, got {(actual, first, bool(data[37]))}.')

        def frame(name):
            mac.wait(0.05)
            mac.frame(args.output / (name + extension))

        def trace_start(address, code):
            mac.command('trace', count=4096, low=address, high=address + len(code))

        def trace_end(name, required=()):
            mac.command('trace_stop')
            trace = mac.command('trace_get')
            if not trace['pcs'] or any(trap not in trace['disassembly'] for trap in required):
                raise AssertionError(f'The {name} trace is missing required drawing calls.')
            (args.output / (name + '.trace')).write_text(trace['disassembly'])
            print(f'{name}: {len(trace["pcs"])} executed instructions, {len(set(trace["pcs"]))} unique addresses.')

        def click(row, modifier=None):
            if modifier is not None:
                mac.key(modifier, True)
            try:
                mac.click(100, 78 + row * 16)
            finally:
                if modifier is not None:
                    mac.key(modifier, False)

        mac.press(0)
        mac.press(46)
        mac.press(13)
        mac.mouse(318, 76, True)
        mac.wait(0.5)
        mac.mouse(318, 76, False)
        mac.wait(0.1)
        mac.press(15)
        expect([1])
        if args.highlights_only:
            if not color:
                raise AssertionError('The highlight probe requires a color graphics port.')
            state = marker + 8
            def change(code):
                before = pointer(state + 54)
                mac.press(code)
                deadline = time.monotonic() + 5
                while pointer(state + 54) == before:
                    if time.monotonic() >= deadline:
                        raise AssertionError('The list did not process its color key.')
                    mac.wait(.02)
            while int.from_bytes(read(state + 50, 2), 'big'):
                change(4)
            while int.from_bytes(read(state + 52, 2), 'big'):
                change(35)
            for palette in range(3):
                for highlight in range(6):
                    name = f'palette-{palette}-highlight-{highlight}'
                    trace_start(ldef_address, ldef)
                    frame(name + '-active')
                    mac.press(2)
                    expect([1], active=False)
                    frame(name + '-inactive')
                    mac.press(0)
                    expect([1])
                    frame(name + '-reactivated')
                    trace_end(name + '-LDEF', ('$A8A4',))
                    change(4)
                change(35)
            mac.command('pause')
            print('Native highlight colors, port colors, and activation passed.', flush=True)
            return
        frame('list-idle')
        trace_start(ldef_address, ldef)
        mac.press(2)
        expect([1], active=False)
        frame('list-inactive')
        mac.press(0)
        expect([1])
        frame('list-reactivated')
        trace_end('LDEF-activation', ('$A8A4',))
        trace_start(pack_address, pack)
        click(3)
        expect([3])
        frame('list-click')
        click(6, 56)
        expect([3, 4, 5, 6])
        frame('list-shift-click')
        click(1, 55)
        expect([1, 3, 4, 5, 6])
        frame('list-command-click')
        click(4, 55)
        expect([1, 3, 5, 6])
        frame('list-command-toggle')
        trace_end('PACK0-selection')
        mac.mouse(100, 110, True)
        mac.wait(0.1)
        mac.mouse(100, 158, True)
        mac.wait(0.1)
        expect([5])
        frame('list-drag-held')
        mac.mouse(100, 158, False)
        mac.wait(0.1)
        expect([5])
        frame('list-drag-released')
        mac.key(56, True)
        try:
            mac.mouse(100, 126, True)
            mac.wait(0.1)
            mac.mouse(100, 174, True)
            mac.wait(0.1)
            expect([5, 6])
            frame('list-shift-drag')
            mac.mouse(100, 174, False)
        finally:
            mac.key(56, False)
        mac.mouse(318, 207, True)
        mac.wait(0.03)
        mac.mouse(318, 207, False)
        mac.wait(0.1)
        expect([5, 6], scroll=1)
        frame('list-scroll-one')
        mac.click(318, 180)
        expect([5, 6], scroll=3)
        frame('list-scroll-bottom')
        mac.mouse(318, 76, True)
        mac.wait(0.5)
        mac.mouse(318, 76, False)
        mac.wait(0.1)
        click(1)
        expect([1])
        trace_start(ldef_address, ldef)
        mac.press(45)
        frame('list-narrow')
        trace_end('LDEF-condensed-text', ('$A888', '$A8B5', '$A885'))
        mac.press(8)
        frame('list-condensed')
        mac.press(13)
        frame('list-restored')
        mac.press(15)
        mac.key(55, True)
        try:
            mac.mouse(100, 126, True)
            mac.wait(0.1)
            mac.mouse(100, 174, True)
            mac.wait(0.1)
            expect([1, 3, 4, 5, 6])
            frame('list-command-drag')
            mac.mouse(100, 142, True)
            mac.wait(0.1)
            expect([1, 3, 4, 5, 6])
            frame('list-command-drag-back')
            mac.mouse(100, 142, False)
        finally:
            mac.key(55, False)
        click(3)
        expect([1, 3, 4, 5, 6])
        frame('list-selected-click')
        mac.mouse(100, 126, True)
        mac.wait(0.1)
        mac.mouse(100, 206, True)
        mac.wait(0.1)
        expect([1, 4, 5, 6, 8])
        frame('list-selected-drag')
        mac.mouse(100, 206, False)
        mac.press(15)
        click(3)
        click(6, 56)
        mac.key(55, True)
        try:
            mac.mouse(100, 142, True)
            mac.wait(0.1)
            mac.mouse(100, 190, True)
            mac.wait(0.1)
            expect([3])
            frame('list-command-remove-drag')
            mac.mouse(100, 190, False)
        finally:
            mac.key(55, False)
        mac.press(15)
        click(3)
        click(6, 55)
        click(1, 56)
        expect([1, 2, 3, 4, 5, 6])
        frame('list-shift-before')
        click(5, 56)
        expect([1, 2, 3, 4, 5])
        frame('list-shift-within')
        mac.press(1)
        mac.press(15)
        click(6, 56)
        expect([6])
        frame('list-single-shift')
        click(4, 55)
        expect([4])
        frame('list-single-command')
        mac.press(46)
        mac.press(15)
        delay_signature = bytes.fromhex('426f000e4a6f000467067003720c600470007209')
        delay_address = ram.find(delay_signature)
        if delay_address < 0:
            raise AssertionError('The native autoscroll delay routine is missing.')
        mac.command('trace', count=4096, low=delay_address, high=delay_address + 72)
        mac.mouse(100, 94, True)
        mac.wait(0.1)
        mac.mouse(100, 228, True)
        deadline = time.monotonic() + 3
        while time.monotonic() < deadline:
            data = read(record, 112)
            if struct.unpack_from('>h', data, 20)[0] == 3 and data[108] & 128:
                break
            mac.wait(0.02)
        expect([11], scroll=3)
        frame('list-autoscroll-held')
        mac.mouse(100, 228, False)
        mac.wait(0.1)
        expect([11], scroll=3)
        frame('list-autoscroll-released')
        trace_end('List-autoscroll-delay', ('DIVU', '$A975'))
        mac.mouse(318, 76, True)
        mac.wait(0.5)
        mac.mouse(318, 76, False)
        mac.wait(0.1)
        mac.press(15)
        mac.mouse(100, 94, True)
        mac.wait(0.1)
        mac.mouse(100, 228, True)
        mac.wait(0.03)
        mac.mouse(100, 228, False)
        mac.wait(0.1)
        expect([9], scroll=1)
        frame('list-autoscroll-early-release')
        print('Native activation, selection, modifiers, dragging, scrolling, and condensed truncation passed.')
        mac.command('pause')


if __name__ == '__main__':
    main()
