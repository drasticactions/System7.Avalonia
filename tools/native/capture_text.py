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
    parser = argparse.ArgumentParser(description='Capture native TextEdit pixels and state through Mini vMac TCP.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--port', type=int, default=6807)
    parser.add_argument('--launch', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    code = resources[b'lpch'][15].data
    with MiniVMac(args.port) as mac:
        if args.launch:
            mac.click(232, 94)
            mac.click(232, 94)
            mac.wait(1)

        def read(address, length):
            return base64.b64decode(mac.command('memory', address=address & 0xffffff, length=length)['data'])

        def pointer(address):
            return struct.unpack('>I', read(address, 4))[0] & 0xffffff

        mac.command('pause')
        try:
            size = mac.command('status')['ram_size']
            ram = b''.join(read(i, min(65536, size - i)) for i in range(0, size, 65536))
        finally:
            mac.command('resume')
        marker = ram.find(b'S7TEXT!!')
        if marker < 0:
            raise RuntimeError('The native text probe is not running.')
        dialog = pointer(marker + 8)
        click_address = pointer(0xc00 + (0xa9d4 & 0x3ff) * 4)
        signature = read(click_address, 16)
        offset = code.find(signature)
        if offset < 0 or code.find(signature, offset + 1) >= 0:
            raise AssertionError('TEClick does not match the System TextEdit patch resource.')
        code_address = click_address - offset

        def record():
            return read(pointer(pointer(dialog + 160)), 100)

        def expect(text, start, end):
            data = record()
            length = struct.unpack_from('>h', data, 60)[0]
            handle = struct.unpack_from('>I', data, 62)[0]
            actual_text = read(pointer(handle), length).decode('mac_roman')
            selection = struct.unpack_from('>hh', data, 32)
            if (actual_text, selection) != (text, (start, end)):
                raise AssertionError(f'Native text: expected {(text, (start, end))}, got {(actual_text, selection)}.')

        def frame(name, caret=None):
            deadline = time.monotonic() + 3
            while time.monotonic() < deadline:
                state = struct.unpack_from('>h', record(), 56)[0]
                if caret is not None and state != (-1 if caret else 255):
                    mac.wait(0.01)
                    continue
                # The exported display buffer follows CPU drawing at the next screen refresh.
                mac.wait(0.04)
                mac.command('pause')
                try:
                    state = struct.unpack_from('>h', record(), 56)[0]
                    if caret is None or state == (-1 if caret else 255):
                        mac.frame(args.output / (name + '.pbm'))
                        return
                finally:
                    mac.command('resume')
                mac.wait(0.01)
            raise AssertionError(f'The native caret did not reach the requested state for {name}.')

        def trace_start():
            mac.command('trace', count=4096, low=code_address, high=code_address + len(code))

        def trace_end(name):
            mac.command('trace_stop')
            trace = mac.command('trace_get')
            if not trace['pcs'] or not all(code_address <= (pc & 0xffffff) < code_address + len(code) for pc in trace['pcs']):
                raise AssertionError('The executed TextEdit trace is empty or outside its patch resource.')
            (args.output / (name + '.trace')).write_text(trace['disassembly'])
            print(f'{name}: {len(trace["pcs"])} executed instructions, {len(set(trace["pcs"]))} unique addresses.')

        def drag(left, right, y):
            mac.mouse(left, y, True)
            mac.wait(0.12)
            mac.mouse(right, y, True)
            mac.wait(0.12)
            mac.mouse(right, y, False)
            mac.wait(0.12)

        mac.click(100, 280)
        expect('Macintosh', 0, 0)
        frame('text-idle', False)
        trace_start()
        frame('text-caret', True)
        frame('text-caret-off', False)
        trace_end('TextEdit-caret')
        trace_start()
        drag(60, 87, 77)
        expect('Macintosh', 0, 3)
        frame('text-selected')
        mac.key(56, True)
        try:
            mac.click(120, 77)
        finally:
            mac.key(56, False)
        expect('Macintosh', 0, 8)
        frame('text-shift-click')
        trace_end('TextEdit-mouse')
        trace_start()
        mac.press(124)
        expect('Macintosh', 9, 9)
        frame('text-right', True)
        mac.key(56, True)
        try:
            mac.press(123)
        finally:
            mac.key(56, False)
        expect('Macintosh', 8, 8)
        frame('text-shift-left', True)
        mac.wait(0.6)
        drag(60, 87, 77)
        expect('Macintosh', 0, 3)
        mac.press(7)
        expect('xintosh', 1, 1)
        frame('text-replaced', True)
        mac.press(51)
        expect('intosh', 0, 0)
        frame('text-backspace', True)
        trace_end('TextEdit-keyboard')
        mac.press(48)
        expect('System 7', 0, 8)
        frame('text-tab')
        mac.press(48)
        expect('Select and edit this text.\rA second line.', 0, 41)
        frame('text-multiline-selected')
        mac.click(95, 183)
        expect('Select and edit this text.\rA second line.', 32, 32)
        frame('text-line-click', True)
        for name, key, position in [('up', 126, 5), ('down', 125, 32), ('end', 125, 41), ('up-from-end', 126, 13)]:
            mac.press(key)
            expect('Select and edit this text.\rA second line.', position, position)
            frame('text-line-' + name, True)
        mac.click(270, 248)
        expect('Another', 7, 7)
        frame('text-field-click', True)
        mac.click(100, 280)
        expect('Macintosh', 0, 0)
        draw_signature = bytes.fromhex('2f083f003f01a8854e7561001464610001b061001010')
        draw_offset = code.find(draw_signature)
        if draw_offset < 0 or read(code_address + draw_offset, len(draw_signature)) != draw_signature:
            raise AssertionError('The TextEdit drawing entry does not match its patch resource.')
        mac.command('trace', count=4096, low=code_address + draw_offset, high=code_address + draw_offset + 0x144)
        mac.press(7)
        expect('xMacintosh', 1, 1)
        mac.command('trace_stop')
        trace = mac.command('trace_get')
        if '$A893' not in trace['disassembly'] or '$A885' not in trace['disassembly']:
            raise AssertionError('The TextEdit trace did not execute MoveTo and DrawText.')
        (args.output / 'TextEdit-drawing.trace').write_text(trace['disassembly'])
        print(f'TextEdit drawing: {len(trace["pcs"])} executed instructions, including MoveTo and DrawText.')
        mac.click(100, 280)
        expect('Macintosh', 0, 0)
        frame('text-restored', False)
        print('Native text selection, Shift-click, arrow keys, replacement, deletion, Tab, and field focus passed.')


if __name__ == '__main__':
    main()
