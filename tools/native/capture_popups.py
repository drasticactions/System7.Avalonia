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
    parser = argparse.ArgumentParser(description='Trace native pop-up controls and capture their pixels through Mini vMac TCP.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--port', type=int, default=6807)
    parser.add_argument('--launch', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    code = resources[b'CDEF'][63].data
    with MiniVMac(args.port) as mac:
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
        marker = ram.find(b'S7POPUP!')
        if marker < 0:
            raise AssertionError('The native pop-up probe is not running.')
        state = marker + 8
        handles = [pointer(state + 4 + i * 4) for i in range(6)]
        code_address = pointer(pointer(pointer(handles[0]) + 24))
        if read(code_address, len(code)) != code:
            raise AssertionError('The running pop-up CDEF does not match the System resource.')
        private = pointer(pointer(pointer(handles[0]) + 28))
        menu = pointer(pointer(private))
        mdef_address = pointer(pointer(menu + 6))
        mdef = resources[b'MDEF'][0].data
        if read(mdef_address, len(mdef)) != mdef:
            raise AssertionError('The running pop-up MDEF does not match the System resource.')

        def expect(value, index=0):
            actual = struct.unpack('>h', read(pointer(handles[index]) + 18, 2))[0]
            if actual != value:
                raise AssertionError(f'Pop-up {index}: expected value {value}, got {actual}.')

        def frame(name):
            mac.wait(0.05)
            mac.frame(args.output / (name + '.pbm'))

        def start():
            mac.command('trace', count=4096, low=code_address, high=code_address + len(code))

        def end(name, required):
            mac.command('trace_stop')
            trace = mac.command('trace_get')
            if not trace['pcs'] or any(trap not in trace['disassembly'] for trap in required):
                raise AssertionError(f'The {name} trace is missing required drawing calls.')
            (args.output / (name + '.trace')).write_text(trace['disassembly'])
            print(f'{name}: {len(trace["pcs"])} executed instructions, {len(set(trace["pcs"]))} unique addresses.')

        def open_at(x=140, y=79):
            mac.mouse(x, y, True)
            mac.wait(0.2)

        def move(x, y):
            mac.mouse(x, y, True)
            mac.wait(0.12)

        def release(x, y):
            mac.mouse(x, y, False)
            mac.wait(0.4)

        mac.press(0)
        mac.press(15)
        open_at(80, 259)
        move(80, 340)
        menu_top = struct.unpack('>h', read(0xa0a, 2))[0]
        move(80, menu_top + 88)
        release(80, menu_top + 88)
        expect(6, 4)
        expect(1)
        frame('popup-idle')
        start()
        mac.press(14)
        frame('popup-disabled')
        mac.press(2)
        frame('popup-inactive')
        mac.press(0)
        frame('popup-enabled')
        end('CDEF63-drawing', ('$A8A1', '$A893', '$A891', '$A8C7'))
        start()
        open_at()
        frame('popup-open')
        before_keys = mac.command('frame')['data']
        for key in (125, 126, 49, 36, 53):
            mac.press(key)
            if mac.command('frame')['data'] != before_keys:
                raise AssertionError(f'Key {key} changed the native mouse-tracked pop-up.')
        expect(1)
        frame('popup-held-keyboard')
        move(140, 111)
        expect(1)
        frame('popup-third-held')
        move(140, 127)
        frame('popup-disabled-item')
        move(140, 143)
        frame('popup-separator')
        move(20, 240)
        frame('popup-outside')
        release(20, 240)
        expect(1)
        frame('popup-cancelled')
        end('CDEF63-tracking', ('$A80B',))
        open_at()
        move(140, 111)
        release(140, 111)
        expect(3)
        frame('popup-third-selected')
        open_at()
        frame('popup-third-open')
        move(140, 126)
        release(140, 126)
        expect(6)
        frame('popup-long-selected')
        mac.command('trace', count=4096, low=mdef_address + 0x7b0, high=mdef_address + 0x878)
        open_at()
        frame('popup-clamped-open')
        end('MDEF-popup-scroll-arrow', ('$ABC9',))
        mac.command('trace', count=4096, low=mdef_address + 0x362, high=mdef_address + 0x3a6)
        mac.mouse(140, 34, True)
        mac.wait(0.05)
        mac.frame(args.output / 'popup-scroll-wait.pbm')
        mac.wait(0.4)
        frame('popup-scrolled-slow')
        end('MDEF-popup-scroll-delay', ('$A975', '$A972'))
        move(20, 240)
        release(20, 240)
        open_at()
        move(140, 30)
        frame('popup-scrolled-up')
        expect(6)
        move(20, 240)
        release(20, 240)
        expect(6)
        mac.press(15)
        open_at(80, 124)
        frame('popup-untitled-open')
        move(20, 240)
        release(20, 240)
        expect(2, 1)
        open_at(80, 259)
        frame('popup-narrow-open')
        move(20, 240)
        release(20, 240)
        expect(6, 4)
        mac.click(80, 294)
        expect(0, 5)
        frame('popup-empty-click')
        mac.click(140, 169)
        expect(1, 2)
        frame('popup-disabled-click')
        open_at(80, 259)
        move(80, 195)
        release(80, 195)
        expect(2, 4)
        open_at(80, 259)
        move(80, 323)
        mac.command('trace', count=4096, low=mdef_address + 0x10ee, high=mdef_address + 0x1136)
        release(80, 323)
        expect(6, 4)
        end('MDEF-popup-truncation', ('$A88C', '$A8B5'))
        open_at(80, 259)
        move(80, 178)
        release(80, 178)
        expect(1, 4)
        frame('popup-bottom-selected')
        mac.command('trace', count=4096, low=mdef_address + 0xe24, high=mdef_address + 0xf68)
        open_at(80, 259)
        frame('popup-bottom-open')
        end('MDEF-popup-placement', ())
        move(80, 333)
        frame('popup-bottom-down')
        move(20, 240)
        release(20, 240)
        expect(1, 4)
        print('Native pop-up drawing, alignment, selection, cancellation, disabled items, and empty controls passed.')


if __name__ == '__main__':
    main()
