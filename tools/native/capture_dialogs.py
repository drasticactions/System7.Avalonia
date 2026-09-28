import argparse
import base64
import hashlib
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
    parser = argparse.ArgumentParser(description='Trace System 7 default button drawing and the standard modal dialog filter.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--rom', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--port', type=int, default=6807)
    parser.add_argument('--launch', action='store_true')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    patch = resources[b'lpch'][31].data
    rom = args.rom.read_bytes()
    if hashlib.sha256(rom).hexdigest() != 'dd908e2b65772a6b1f0c859c24e9a0d3dcde17b1c6a24f4abd8955846d7895e7':
        raise AssertionError('This probe requires the Mac Plus ROM default-button drawing routine.')
    with MiniVMac(args.port) as mac:
        mac.command('trace_stop')
        if args.launch:
            mac.wait(3)
            mac.click(232, 94)
            mac.click(232, 94)
            mac.wait(1)

        def read(address, length):
            return base64.b64decode(mac.command('memory', address=address & 0xffffff, length=length)['data'])

        def pointer(address):
            return int.from_bytes(read(address, 4), 'big') & 0xffffff

        def number(address, length=2):
            return int.from_bytes(read(address, length), 'big')

        mac.command('pause')
        try:
            size = mac.command('status')['ram_size']
            ram = b''.join(read(i, min(65536, size - i)) for i in range(0, size, 65536))
        finally:
            mac.command('resume')
        marker = ram.find(b'S7DIALOG')
        if marker < 0:
            raise AssertionError('The native dialog probe is not running.')
        state = marker + 8
        standard = pointer(state + 20)
        handles = [pointer(state + 36 + i * 4) for i in range(4)]
        actual = read(standard, 0x248)
        offset = patch.find(actual[0x3a:0x6a]) - 0x3a
        if offset < 0:
            raise AssertionError('The standard filter was not found in System lpch 31.')
        expected = bytearray(patch[offset:offset + len(actual)])
        for relocation, target in [(0x12, 0x414b48), (0x1be, 0x415508)]:
            if actual[relocation:relocation + 4] != struct.pack('>I', target):
                raise AssertionError('The standard filter has an unexpected ROM relocation.')
            expected[relocation:relocation + 4] = struct.pack('>I', target)
        if actual != expected:
            raise AssertionError('The running standard filter differs from the relocated System resource.')
        print(f'Standard filter {standard:#x}: {len(actual)} bytes match lpch 31 at {offset:#x}, including ROM relocations.')

        def frame(name):
            mac.wait(0.1)
            mac.frame(args.output / (name + '.pbm'))

        def trace(name, required, addresses=()):
            mac.command('trace_stop')
            result = mac.command('trace_get')
            pcs = {pc & 0xffffff for pc in result['pcs']}
            if not pcs or any(op not in result['disassembly'] for op in required) or any(pc not in pcs for pc in addresses):
                raise AssertionError(f'The {name} trace missed a required native branch.')
            (args.output / (name + '.trace')).write_text(result['disassembly'])
            print(f'{name}: {len(result["pcs"])} instructions, {len(pcs)} unique addresses.')

        def hits():
            return number(state + 30, 4)

        def keyboard(name, key, item, command=False):
            before = hits()
            mac.command('trace', count=4096, low=standard + 0x3a, high=standard + 0xc2)
            if command:
                mac.key(55, True)
            mac.key(key, True)
            deadline = time.monotonic() + 1
            while standard + 0x7a not in {pc & 0xffffff for pc in mac.command('trace_get')['pcs']}:
                if time.monotonic() >= deadline:
                    raise AssertionError(f'{name} did not enter the native highlight delay.')
                mac.wait(0.002)
            mac.command('pause')
            try:
                if number(pointer(handles[item - 1]) + 17, 1) != 1:
                    raise AssertionError(f'{name} ended its native highlight before capture.')
                mac.frame(args.output / (name + '-pressed.pbm'))
                pressed_tick = number(0x16a, 4)
            finally:
                mac.command('resume')
            mac.key(key, False)
            if command:
                mac.key(55, False)
            mac.wait(0.2)
            if hits() != before + 1 or number(state + 34) != item:
                raise AssertionError(f'{name} returned an unexpected dialog item.')
            delay = number(state + 52, 4) - pressed_tick
            if not 6 <= delay <= 9:
                raise AssertionError(f'{name} used an unexpected highlight delay: {delay} ticks.')
            trace(name, ('$A95D', '$A03B'), (standard + 0x7a,))
            print(f'{name}: item {item}, {delay} ticks from observed press to return.')
            frame(name + '-released')

        mac.press(18)
        mac.press(14)
        mac.press(15)
        frame('dialog-default-1')
        mac.command('trace', count=4096, low=0x415508, high=0x41552a)
        mac.press(15)
        trace('Dialog-default-ring', ('$A89B', '$A8A9', '$A8B0'), (0x415508, 0x415528))
        mac.command('trace', count=4096, low=standard + 0x146, high=standard + 0x1d4)
        mac.press(15)
        trace('StdFilter-default-ring', (), (standard + 0x1b2, standard + 0x1b4, standard + 0x1b6))
        for index, key in [(2, 19), (3, 20), (4, 21)]:
            mac.press(key)
            frame(f'dialog-default-{index}')
        mac.press(2)
        frame('dialog-default-disabled')
        before = hits()
        mac.press(36)
        if hits() != before:
            raise AssertionError('Return selected the disabled default button.')
        frame('dialog-disabled-return')
        keyboard('dialog-disabled-cancel', 53, 2)
        mac.press(14)
        frame('dialog-default-enabled')
        keyboard('dialog-odd-return', 36, 4)
        mac.press(18)
        keyboard('dialog-return', 36, 1)
        keyboard('dialog-enter', 76, 1)
        keyboard('dialog-escape', 53, 2)
        keyboard('dialog-command-period', 47, 2, command=True)
        mac.press(4)
        frame('dialog-default-hidden')
        keyboard('dialog-hidden-return', 36, 1)
        mac.press(1)
        frame('dialog-default-shown')
        before = hits()
        for key in (49, 48, 47):
            mac.press(key)
        if hits() != before:
            raise AssertionError('Space, Tab, or period activated a dialog button.')
        frame('dialog-unmapped-keys')
        mac.mouse(100, 80, True)
        frame('dialog-mouse-pressed')
        mac.mouse(100, 55, True)
        frame('dialog-mouse-outside')
        mac.mouse(100, 80, True)
        frame('dialog-mouse-returned')
        mac.mouse(100, 80, False)
        mac.wait(0.15)
        if hits() != before + 1 or number(state + 34) != 1:
            raise AssertionError('Mouse tracking did not select the default button.')
        frame('dialog-mouse-released')
        mac.mouse(100, 80, True)
        mac.wait(0.05)
        mac.mouse(100, 55, True)
        mac.wait(0.05)
        mac.mouse(100, 55, False)
        mac.wait(0.15)
        if hits() != before + 1:
            raise AssertionError('Dragging outside failed to cancel the default button.')
        frame('dialog-mouse-cancelled')
        mac.click(100, 66)
        if hits() != before + 1:
            raise AssertionError('The default ring accepted a mouse hit outside the control rectangle.')
        frame('dialog-ring-click')
        before = hits()
        mac.key(36, True)
        mac.wait(0.75)
        mac.key(36, False)
        mac.wait(0.15)
        if hits() != before + 1:
            raise AssertionError('Auto-repeat activated the native default button.')
        frame('dialog-return-held')
        before = hits()
        mac.key(36, True)
        mac.wait(0.03)
        mac.key(36, False)
        mac.key(53, True)
        mac.wait(0.03)
        mac.key(53, False)
        mac.wait(0.4)
        if hits() != before + 2 or number(state + 34) != 2:
            raise AssertionError('The native filter did not preserve queued Return and Escape events.')
        frame('dialog-queued-keys')
        before = hits()
        mac.mouse(100, 80, True)
        mac.wait(0.05)
        mac.press(36)
        if hits() != before:
            raise AssertionError('Return interrupted native mouse tracking.')
        frame('dialog-mouse-key-held')
        mac.mouse(100, 80, False)
        mac.wait(0.35)
        if hits() != before + 2 or number(state + 34) != 1:
            raise AssertionError('The mouse click or queued Return event was lost after mouse release.')
        frame('dialog-mouse-key-released')
        print('Native default rings, disabled input, modal keyboard aliases, highlight timing, and mouse tracking passed.')


if __name__ == '__main__':
    main()
