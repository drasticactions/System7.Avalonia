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
from client import MiniVMac, frame_data


def main():
    parser = argparse.ArgumentParser(description='Capture native Menu Manager states through Mini vMac TCP.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--port', type=int, default=6807)
    parser.add_argument('--launch', action='store_true')
    parser.add_argument('--rom', type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    code = resources[b'MDEF'][0].data
    bar_code = resources[b'MBDF'][0].data
    with MiniVMac(args.port) as mac:
        mac.command('trace_stop')
        mac.command('resume')
        # Mini vMac's startup message consumes keyboard input until C acknowledges it.
        mac.press(8)
        marker = -1
        for _ in range(20 if args.launch else 1):
            mac.command('pause')
            try:
                size = mac.command('status')['ram_size']
                ram = b''.join(base64.b64decode(mac.command('memory', address=i, length=min(65536, size-i))['data'])
                               for i in range(0, size, 65536))
            finally:
                mac.command('resume')
            marker = ram.find(b'S7MENUS!')
            address = ram.find(code)
            bar_address = ram.find(bar_code)
            ready = marker >= 0 and ram[marker + 52:marker + 54] == b'\0\x01'
            foreground = ram[0x910:0x919] == b'\x08Controls'
            if ready and foreground and address >= 0 and bar_address >= 0:
                break
            if args.launch:
                if ready and foreground:
                    mac.click(50, 10)
                else:
                    mac.mouse(10, 320)
                    mac.wait(.2)
                    mac.click(232, 94)
                    mac.click(232, 94)
                mac.wait(1)
        if not ready or not foreground or address < 0 or bar_address < 0:
            raise RuntimeError('The foreground menu probe or native MDEF is missing from RAM.')
        mac.mouse(50, 10, True)
        mac.wait(.2)
        mac.mouse(200, 170, True)
        mac.wait(.2)
        mac.mouse(200, 170, False)
        mac.wait(.5)
        color = mac.command('status').get('depth', 1) > 1
        state = marker + 8

        def result():
            data = base64.b64decode(mac.command('memory', address=state + 32, length=8)['data'])
            return struct.unpack('>II', data)

        def wait_result(expected):
            deadline = time.monotonic() + 5
            while time.monotonic() < deadline:
                actual = result()
                if actual == expected:
                    return
                mac.wait(0.05)
            raise AssertionError(f'Native menu command: expected {expected}, got {actual}.')

        def frame(name):
            mac.wait(0.15)
            if base64.b64decode(mac.command('memory', address=0x910, length=9)['data']) != b'\x08Controls':
                raise AssertionError('The menu probe lost foreground activation.')
            previous = None
            deadline = time.monotonic() + 5
            while time.monotonic() < deadline:
                reply = mac.command('frame')
                if reply['data'] == previous:
                    break
                previous = reply['data']
                mac.wait(.05)
            else:
                raise AssertionError(f'The native {name} frame did not settle.')
            return mac.frame(args.output / (name + ('.ppm' if color else '.pbm')))

        def start_trace(low=address, length=len(code)):
            mac.command('trace', count=4096, low=low, high=low + length)

        def end_trace(name, low=address, length=len(code)):
            mac.command('trace_stop')
            trace = mac.command('trace_get')
            if not trace['pcs'] or not all(low <= (pc & 0xffffff) < low + length for pc in trace['pcs']):
                raise AssertionError('The menu trace is empty or outside its definition resource.')
            (args.output / (name + '.trace')).write_text(trace['disassembly'])
            print(f'{name}: {len(trace["pcs"])} executed instructions, {len(set(trace["pcs"]))} unique addresses.')

        initial_count = result()[1]
        print(f'Initial native menu command count: {initial_count}.', flush=True)
        closed_frame = frame('menu-bar')
        start_trace(bar_address, len(bar_code))
        mac.mouse(20, 10, True)
        frame('menu-apple-open')
        mac.mouse(70, 28, True)
        frame('menu-apple-about')
        mac.mouse(200, 170, True)
        mac.mouse(200, 170, False)
        mac.wait(.2)
        end_trace('MBDF0-apple', bar_address, len(bar_code))
        start_trace()
        mac.mouse(50, 10, True)
        opened_frame = frame('menu-file-open')
        end_trace('MDEF0-file-draw')
        start_trace()
        for name, x, y in [
            ('new', 70, 28), ('separator', 70, 60), ('disabled', 70, 92),
            ('checked', 70, 108), ('outside', 200, 170), ('return', 70, 44),
        ]:
            mac.mouse(x, y, True)
            frame('menu-file-' + name)
        mac.mouse(70, 44, False)
        mac.wait(0.3)
        wait_result((0x00810002, initial_count + 1))
        end_trace('MDEF0-file-tracking')
        mac.mouse(50, 10, True)
        mac.wait(0.15)
        mac.mouse(70, 92, True)
        mac.wait(0.15)
        mac.mouse(70, 92, False)
        mac.wait(0.15)
        wait_result((0, initial_count + 1))
        start_trace()
        mac.mouse(50, 10, True)
        mac.wait(0.15)
        mac.mouse(130, 10, True)
        frame('menu-disabled-title')
        mac.mouse(90, 10, True)
        frame('menu-edit-open')
        mac.mouse(100, 28, True)
        frame('menu-edit-disabled')
        mac.mouse(100, 76, True)
        frame('menu-edit-copy')
        mac.mouse(200, 170, True)
        mac.wait(0.15)
        mac.mouse(200, 170, False)
        mac.wait(0.15)
        wait_result((0, initial_count + 1))
        end_trace('MDEF0-edit-tracking')
        mac.press(45)
        if result()[1] != initial_count + 1:
            raise AssertionError('A menu shortcut fired without Command.')
        mac.key(55, True)
        try:
            mac.press(45)
            wait_result((0x00810001, initial_count + 2))
            mac.press(8)
            wait_result((0x00820004, initial_count + 3))
            mac.press(6)
            wait_result((0, initial_count + 3))
        finally:
            mac.key(55, False)
        frame('menu-restored')
        start_trace(bar_address, len(bar_code))
        mac.mouse(50, 10, True)
        mac.wait(0.15)
        mac.mouse(90, 10, True)
        mac.wait(0.15)
        mac.mouse(200, 170, True)
        mac.wait(0.15)
        mac.mouse(200, 170, False)
        mac.wait(0.15)
        end_trace('MBDF0-titles', bar_address, len(bar_code))
        flash_count = struct.unpack_from('>H', ram, 0xa24)[0]
        mac.mouse(50, 10, True)
        mac.wait(0.15)
        mac.mouse(70, 44, True)
        mac.wait(0.15)
        selected_frame = mac.command('frame')
        def pixel(reply, x, y):
            data = frame_data(reply)
            depth = reply.get('depth', 1)
            if depth <= 8:
                bit = x * depth
                return (data[y * reply['stride'] + bit // 8] >> (8 - depth - bit % 8)) & ((1 << depth) - 1)
            start = y * reply['stride'] + x * depth // 8
            return int.from_bytes(data[start:start + depth // 8], 'big')
        selected_pixel = pixel(selected_frame, 34, 36)
        border_pixel = pixel(opened_frame, 126, 40)
        if selected_pixel == pixel(opened_frame, 34, 36) or border_pixel == pixel(closed_frame, 126, 40):
            raise AssertionError('The native flash sample pixels have no contrast.')
        start_trace()
        mac.mouse(70, 44, False)
        transitions = []
        deadline = time.monotonic() + 3
        while time.monotonic() < deadline:
            pixels = mac.command('frame')
            tick = mac.command('status')['ticks']
            phase = (int(pixel(pixels, 34, 36) == selected_pixel), int(pixel(pixels, 126, 40) == border_pixel))
            if not transitions or transitions[-1][1] != phase:
                transitions.append((tick, phase))
            if phase[1] == 0:
                break
            mac.wait(0.003)
        end_trace('MDEF0-flash')
        if transitions and transitions[0][1] == (1, 1):
            transitions.pop(0)
        phases = [phase for _, phase in transitions]
        expected = [(0, 1), (1, 1)] * flash_count + [(0, 0)]
        if phases != expected:
            raise AssertionError(f'Native menu flash phases: expected {expected}, got {phases}.')
        wait_result((0x00810002, initial_count + 4))
        print(f'Native menu flash transitions (ticks, selection, popup): {transitions}.')
        delay_code = bytes.fromhex('307c0003a03b4e75')
        delay_memory = ram
        delay_base = 0
        if delay_code not in ram and args.rom:
            delay_memory = args.rom.read_bytes()
            delay_base = 0x800000 if color else 0x400000
        delay_offset = delay_memory.find(delay_code)
        if delay_offset < 24 or delay_memory.find(delay_code, delay_offset + 1) >= 0:
            raise AssertionError('The native three-tick menu delay is missing or ambiguous.')
        delay_address = delay_base + delay_offset
        entry_distance = 26 if delay_base == 0x800000 else 24
        flash_address = delay_address - entry_distance
        flash_length = entry_distance + len(delay_code)
        if delay_memory[delay_offset - entry_distance:delay_offset - entry_distance + 4] != bytes.fromhex('3f3c0001'):
            raise AssertionError('The native menu flash entry does not match the disassembly.')
        mac.mouse(50, 10, True)
        mac.wait(0.15)
        mac.mouse(70, 44, True)
        mac.wait(0.15)
        start_trace(flash_address, flash_length)
        mac.mouse(70, 44, False)
        wait_result((0x00810002, initial_count + 5))
        mac.command('trace_stop')
        trace = mac.command('trace_get')
        delays = sum((pc & 0xffffff) == delay_address + 4 for pc in trace['pcs'])
        if delays != flash_count * 2:
            raise AssertionError(f'Native menu flash: expected {flash_count * 2} delays, got {delays}.')
        end_trace('MenuSelect-flash-delay', flash_address, flash_length)
        print('Native menu selection, disabled items, cancellation, flash cycles, and Command shortcuts passed.')
        mac.command('pause')


if __name__ == '__main__':
    main()
