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
    parser = argparse.ArgumentParser(description='Trace original System LDEF -4000 icon rows through Mini vMac TCP.')
    parser.add_argument('--image', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--port', type=int, default=6817)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.image.read_bytes())
    system = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    app = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['Controls'].rsrc))
    for rid, resource in app[b'SICN'].items():
        (args.output / f'SICN-{rid}.bin').write_bytes(resource.data)
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
            marker = ram.find(b'S7SICN!!')
            if marker >= 0 and ram[marker+56:marker+58] == b'\0\1' and ram[0x910:0x919] == b'\x08Controls':
                break
            mac.mouse(10, 320)
            mac.click(232, 94)
            mac.click(232, 94)
            mac.wait(2)
        else:
            mac.frame(args.output / 'startup.ppm')
            raise AssertionError('The native SICN list did not become ready.')
        record = pointer(pointer(marker+12))
        code = system[b'LDEF'][-4000].data
        address = pointer(pointer(record+64))
        if read(address, len(code)) != code:
            raise AssertionError('The running list renderer differs from original System LDEF -4000.')
        offsets = struct.unpack('>9H', read(record+86, 18))
        cells = pointer(pointer(record+80))
        for index, label in enumerate((b'Applications', b'Utilities', b'Network', b'Controls', b'Disabled', b'Styled', b'System Folder', b'Color icons')):
            start, end = offsets[index] & 0x7fff, offsets[index+1] & 0x7fff
            payload = read(cells+start, end-start)
            if len(payload) != 31+len(label) or payload[16:18] != (256+index).to_bytes(2, 'big') or payload[30:] != bytes([len(label)])+label:
                raise AssertionError(f'The native SICN row {index} has invalid data.')
        color = status.get('depth', 1) > 1
        extension = '.ppm' if color else '.pbm'

        def expect(selected, active=True):
            data = read(record, 104)
            actual = [index for index in range(8) if data[86+index*2] & 128]
            if (actual, bool(data[37])) != (selected, active):
                raise AssertionError(f'The native selection differs: expected {(selected, active)}, got {(actual, bool(data[37]))}.')

        def frame(name, selected, active=True):
            mac.wait(.08)
            expect(selected, active)
            mac.frame(args.output / (name+extension))

        def click(index):
            mac.click(100, 78+index*20)

        frame('sicn-idle', [1])
        mac.command('trace', count=4096, low=address, high=address+len(code))
        for index in range(8):
            click(index)
            frame(f'sicn-selected-{index}', [index])
        mac.press(2)
        frame('sicn-inactive', [7], False)
        mac.press(0)
        frame('sicn-reactivated', [7])
        mac.command('trace_stop')
        trace = mac.command('trace_get')
        if not trace['pcs'] or any(trap not in trace['disassembly'] for trap in ('$A8EC', '$A884', '$A8A4')):
            raise AssertionError('The renderer trace lacks icon, text, or highlight calls.')
        (args.output / 'LDEF-minus-4000.trace').write_text(trace['disassembly'])
        mac.command('pause')
        print(f'LDEF -4000: {len(trace["pcs"])} instructions, {len(set(trace["pcs"]))} unique addresses; 11 native frames and selection passed.')


if __name__ == '__main__':
    main()
