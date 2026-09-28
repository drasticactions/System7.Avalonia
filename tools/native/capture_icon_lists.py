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
    parser = argparse.ArgumentParser(description='Run the original LDEF 19 icon list through Mini vMac TCP.')
    parser.add_argument('--image', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--port', type=int, default=6817)
    parser.add_argument('--selection-only', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.image.read_bytes())
    system = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    app = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['Controls'].rsrc))
    for kind in (b'ICN#', b'icl4', b'icl8'):
        for rid, resource in app[kind].items():
            (args.output / f'{kind.decode()}-{rid}.bin').write_bytes(resource.data)
    with MiniVMac(args.port) as mac:
        def read(address, length):
            return base64.b64decode(mac.command('memory', address=address & 0xffffff, length=length)['data'], validate=True)

        def pointer(address):
            return int.from_bytes(read(address, 4), 'big') & 0xffffff

        mac.command('trace_stop')
        mac.command('resume')
        mac.wait(5)
        mac.press(8)
        mac.wait(1)
        for _ in range(20):
            mac.command('pause')
            try:
                status = mac.command('status')
                size = status['ram_size']
                ram = b''.join(read(i, min(65536, size-i)) for i in range(0, size, 65536))
            finally:
                mac.command('resume')
            marker = ram.find(b'S7ICONS!')
            if marker >= 0 and ram[marker+56:marker+58] == b'\0\1' and ram[0x910:0x919] == b'\x08Controls':
                break
            mac.mouse(10, 320)
            mac.click(232, 94)
            mac.click(232, 94)
            mac.wait(2)
        else:
            mac.frame(args.output / 'startup.ppm')
            raise AssertionError('The native icon list did not become ready.')
        record = pointer(pointer(marker+12))
        code = system[b'LDEF'][19].data
        address = pointer(pointer(record+64))
        if read(address, len(code)) != code:
            raise AssertionError('The running icon list does not match the original LDEF 19.')
        offsets = struct.unpack('>7H', read(record+86, 14))
        cells = pointer(pointer(record+80))
        for index, label in enumerate((b'System Folder', b'Applications', b'A very long icon application label', b'Utilities', None, b'')):
            start, end = offsets[index] & 0x7fff, offsets[index+1] & 0x7fff
            payload = read(cells+start, end-start)
            if label is None:
                valid = len(payload) == 4
            else:
                valid = payload[4:] == struct.pack('>hhhB', 0, 0, 12, len(label)) + label
            if not valid:
                raise AssertionError(f'The native icon cell {index} has invalid data.')
        extension = '.ppm' if status.get('depth', 1) > 1 else '.pbm'

        def frame(name, selected, active=True):
            data = read(record, 98)
            actual = [i for i in range(6) if data[86+i*2] & 128]
            if (actual, bool(data[37])) != (selected, active):
                raise AssertionError(f'{name}: expected {(selected, active)}, got {(actual, bool(data[37]))}.')
            mac.wait(.08)
            mac.frame(args.output / (name+extension))

        def click(index, width=112):
            mac.click(60+(index % 2)*width+width//2, 102+(index//2)*64)

        mac.press(0)
        mac.press(13)
        mac.press(15)
        if args.selection_only:
            pack = system[b'PACK'][0].data
            pack_address = ram.find(pack)
            if pack_address < 0:
                raise AssertionError('The running List Manager does not match PACK 0.')
            mac.command('trace', count=4096, low=pack_address, high=pack_address+len(pack))

            def shift(index):
                mac.key(56, True)
                try:
                    click(index)
                finally:
                    mac.key(56, False)

            click(0)
            shift(4)
            frame('selection-column', [0, 2, 4])
            mac.press(15)
            click(1)
            shift(4)
            frame('selection-rectangle', list(range(6)))
            shift(2)
            frame('selection-shrink', [0, 2])
            mac.press(15)
            click(2)
            shift(1)
            frame('selection-reverse', [0, 1, 2, 3])
            shift(4)
            frame('selection-extend', [0, 2, 4])
            mac.press(15)
            click(1)
            shift(5)
            frame('selection-second-column', [1, 3, 5])
            shift(0)
            frame('selection-reverse-columns', list(range(6)))
            mac.press(15)
            mac.key(55, True)
            mac.mouse(116, 102, True)
            mac.wait(.1)
            mac.mouse(228, 230, True)
            mac.wait(.1)
            frame('selection-command-drag', list(range(6)))
            mac.mouse(228, 230, False)
            mac.key(55, False)
            mac.press(15)
            mac.mouse(116, 102, True)
            mac.wait(.1)
            mac.mouse(228, 230, True)
            mac.wait(.1)
            frame('selection-drag', [5])
            mac.mouse(228, 230, False)
            mac.command('trace_stop')
            trace = mac.command('trace_get')
            if not trace['pcs']:
                raise AssertionError('The grid selection trace is empty.')
            (args.output / 'PACK0-grid-selection.trace').write_text(trace['disassembly'])
            mac.command('pause')
            print(f'Native grid selection: nine cases passed; {len(trace["pcs"])} PACK 0 instructions.')
            return
        frame('icons-idle', [])
        mac.command('trace', count=4096, low=address, high=address+len(code))
        for index in range(6):
            click(index)
            frame(f'icons-selected-{index}', [index])
        mac.press(2)
        frame('icons-inactive', [5], False)
        mac.press(0)
        frame('icons-reactivated', [5])
        mac.press(15)
        mac.key(55, True)
        try:
            click(0)
            click(3)
        finally:
            mac.key(55, False)
        frame('icons-command', [0, 3])
        mac.press(15)
        mac.press(45)
        frame('icons-narrow', [])
        for index in range(6):
            click(index, 48)
            frame(f'icons-narrow-selected-{index}', [index])
        mac.command('trace_stop')
        trace = mac.command('trace_get')
        if not trace['pcs'] or any(trap not in trace['disassembly'] for trap in ('$ABC9', '$A884', '$A8A4', '$A8B5')):
            raise AssertionError('The icon list trace lacks icon, text, highlight, or truncation calls.')
        (args.output / 'LDEF-19.trace').write_text(trace['disassembly'])
        if status.get('depth') == 8:
            globals_address = pointer(pointer(pointer(0x2b6)+0x174))
            palette = bytearray()
            for index, count in ((16, 16), (17, 16), (0, 256), (1, 256)):
                table = pointer(pointer(globals_address+0x120+index*4))
                if int.from_bytes(read(table+6, 2), 'big')+1 != count:
                    raise AssertionError('The native icon color table has an invalid length.')
                data = read(table+8, count*8)
                for entry in range(count):
                    palette.extend(data[entry*8+2:entry*8+8])
            (args.output / 'icon-family-colors.bin').write_bytes(palette)
        mac.command('pause')
        print(f'LDEF 19: {len(trace["pcs"])} instructions, {len(set(trace["pcs"]))} unique addresses; 17 frames and native selection passed.')


if __name__ == '__main__':
    main()
