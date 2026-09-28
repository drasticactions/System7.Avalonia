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
    parser = argparse.ArgumentParser(description='Capture native controls and executed CDEF instructions through Mini vMac TCP.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--port', type=int, default=6807)
    parser.add_argument('--launch', action='store_true', help='Open Controls from the initial Finder window.')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
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
        marker = ram.find(b'S7PROBE!')
        if marker < 0:
            raise RuntimeError('The native Controls application is not running.')
        state = marker + 8
        handles = struct.unpack_from('>14I', ram, state + 20)
        if not all(handles):
            raise RuntimeError('The probe has not initialized its dialog items.')

        def value(item):
            pointer = struct.unpack('>I', read(handles[item - 1], 4))[0]
            return struct.unpack('>h', read(pointer + 18, 2))[0]

        def expect(item, expected):
            actual = value(item)
            if actual != expected:
                raise AssertionError(f'Native item {item}: expected {expected}, got {actual}.')

        def frame(name):
            mac.wait(0.12)
            mac.frame(args.output / (name + '.pbm'))

        def trace_start(resource_id):
            code = resources[b'CDEF'][resource_id].data
            address = ram.find(code)
            if address < 0:
                raise RuntimeError(f'CDEF {resource_id} does not match any executed RAM resource.')
            mac.command('trace', count=4096, low=address, high=address + len(code))
            return address, len(code)

        def trace_end(resource_id, bounds, label):
            mac.command('trace_stop')
            trace = mac.command('trace_get')
            address, length = bounds
            if not trace['pcs'] or not all(address <= (pc & 0xffffff) < address + length for pc in trace['pcs']):
                raise AssertionError('The executed trace is empty or outside the native CDEF.')
            (args.output / f'CDEF{resource_id}-{label}.trace').write_text(trace['disassembly'])
            print(f'CDEF {resource_id} {label}: {len(trace["pcs"])} executed instructions, {len(set(trace["pcs"]))} unique addresses.')

        expect(4, 0)
        expect(5, 1)
        expect(7, 0)
        expect(8, 1)
        expect(11, 50)
        expect(12, 50)
        expect(13, 50)
        expect(14, 0)
        mac.mouse(20, 320)
        frame('idle')
        bounds = trace_start(0)
        mac.mouse(100, 80, True)
        frame('pressed')
        mac.mouse(100, 100, True)
        frame('button-drag-out')
        mac.mouse(100, 100, False)
        trace_end(0, bounds, 'button')
        bounds = trace_start(0)
        mac.mouse(100, 125, True)
        frame('checkbox-pressed')
        mac.mouse(100, 125, False)
        mac.wait(0.15)
        expect(4, 1)
        frame('checkbox-selected')
        mac.click(100, 125)
        expect(4, 0)
        mac.click(100, 185)
        expect(6, 1)
        trace_end(0, bounds, 'checkbox')
        bounds = trace_start(0)
        mac.mouse(300, 125, True)
        frame('radio-pressed')
        mac.mouse(300, 125, False)
        mac.wait(0.15)
        expect(7, 1)
        expect(8, 0)
        mac.click(300, 155)
        expect(7, 0)
        expect(8, 1)
        trace_end(0, bounds, 'radio')

        bounds = trace_start(1)
        mac.mouse(438, 78, True)
        frame('scroll-up')
        mac.mouse(438, 78, False)
        mac.wait(0.15)
        expect(11, 50)
        trace_end(1, bounds, 'arrow')
        bounds = trace_start(1)
        mac.mouse(438, 169, True)
        frame('scroll-thumb-down')
        mac.mouse(438, 205, True)
        frame('scroll-thumb-drag')
        expect(11, 50)
        mac.mouse(438, 205, False)
        frame('scroll-thumb-release')
        expect(11, 74)
        trace_end(1, bounds, 'thumb')
        mac.click(345, 80)
        expect(11, 50)
        mac.mouse(438, 169, True)
        mac.wait(0.12)
        mac.mouse(438, 205, True)
        mac.wait(0.12)
        mac.mouse(470, 205, True)
        frame('scroll-thumb-outside')
        mac.mouse(438, 205, True)
        frame('scroll-thumb-return')
        mac.mouse(438, 206, True)
        frame('scroll-thumb-return-moved')
        mac.mouse(470, 206, True)
        mac.wait(0.12)
        mac.mouse(470, 206, False)
        frame('scroll-thumb-cancel')
        expect(11, 50)
        print('Native checkbox, radio group, disabled checkbox, and deferred thumb assertions passed.')
        bounds = trace_start(1)
        mac.mouse(68, 268, True)
        frame('scroll-horizontal-left')
        mac.mouse(68, 268, False)
        mac.wait(0.12)
        mac.mouse(402, 268, True)
        frame('scroll-horizontal-right')
        mac.mouse(402, 268, False)
        mac.wait(0.12)
        trace_end(1, bounds, 'horizontal-arrows')
        bounds = trace_start(1)
        mac.mouse(235, 268, True)
        frame('scroll-horizontal-thumb-down')
        mac.mouse(271, 268, True)
        frame('scroll-horizontal-thumb-drag')
        expect(12, 50)
        mac.mouse(271, 268, False)
        frame('scroll-horizontal-thumb-release')
        expect(12, 62)
        trace_end(1, bounds, 'horizontal-thumb')
        mac.click(420, 78)
        expect(13, 50)
        mac.click(68, 290)
        expect(14, 0)
        print('Native horizontal thumb, disabled scrollbar, and empty range assertions passed.')


if __name__ == '__main__':
    main()
