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
    parser = argparse.ArgumentParser(description='Trace native styled, hierarchical, scrolling, and icon menus.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--port', type=int, default=6807)
    parser.add_argument('--launch', action='store_true')
    parser.add_argument('--styles-only', action='store_true')
    parser.add_argument('--trace-only', action='store_true')
    parser.add_argument('--interaction-only', action='store_true')
    parser.add_argument('--hierarchy-only', action='store_true')
    parser.add_argument('--icons-only', action='store_true')
    parser.add_argument('--probe-image', type=Path)
    args = parser.parse_args()
    if args.icons_only and not args.probe_image:
        parser.error('--icons-only requires --probe-image for the icon resource bytes.')
    args.output.mkdir(parents=True, exist_ok=False)
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    resources = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    code = resources[b'MDEF'][0].data
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
                size = mac.command('status')['ram_size']
                ram = b''.join(read(i, min(65536, size - i)) for i in range(0, size, 65536))
            finally:
                mac.command('resume')
            marker = ram.find(b'S7MITEMS')
            address = ram.find(code)
            ready = marker >= 0 and ram[marker + 68:marker + 70] == b'\0\x01'
            foreground = ram[0x910:0x919] == b'\x08Controls'
            if ready and foreground and address >= 0:
                break
            if args.launch:
                if ready and foreground:
                    mac.click(25, 10)
                else:
                    mac.mouse(10, 320)
                    mac.wait(.2)
                    mac.click(232, 94)
                    mac.click(232, 94)
                mac.wait(1)
        else:
            raise AssertionError('The foreground menu item probe or original MDEF is missing.')
        state = marker + 8
        color = mac.command('status').get('depth', 1) > 1
        extension = '.ppm' if color else '.pbm'
        handles = [pointer(state + index * 4) for index in range(8)]
        metrics = read(state + 72, 1280)
        page_size = int.from_bytes(read(state + 62, 2), 'big')
        if page_size != 8 and not (args.trace_only or args.interaction_only):
            raise AssertionError('The menu probe must use eight items per style page.')
        (args.output / 'font-metrics.bin').write_bytes(metrics)
        for style in [0, 1, 2, 4, 8, 16, 32, 64, 127]:
            print(f'Style {style}: ascent, descent, width, leading, string width = {struct.unpack_from(">5h", metrics, style * 10)}.', flush=True)

        def menu_left(index):
            return int.from_bytes(read(pointer(pointer(0xa1c)) + 10 + index * 6, 2), 'big')

        def bounds(index):
            return struct.unpack('>hh', read(pointer(handles[index]) + 2, 4))

        def frame(name):
            mac.wait(.15)
            if read(0x910, 9) != b'\x08Controls':
                raise AssertionError('The menu probe lost foreground activation.')
            previous = None
            for _ in range(50):
                reply = mac.command('frame')
                if reply['data'] == previous:
                    return mac.frame(args.output / (name + extension))
                previous = reply['data']
                mac.wait(.05)
            raise AssertionError(f'The native {name} frame did not settle.')

        def trace(name, action, start=0, length=None):
            mac.command('trace', count=4096, low=address + start, high=address + (length or len(code)))
            action()
            mac.command('trace_stop')
            result = mac.command('trace_get')
            if not result['pcs'] or any(not address + start <= (pc & 0xffffff) < address + (length or len(code)) for pc in result['pcs']):
                raise AssertionError(f'The {name} trace missed MDEF 0.')
            (args.output / (name + '.trace')).write_text(result['disassembly'])
            print(f'{name}: {len(result["pcs"])} executed instructions.', flush=True)

        def open_menu(index):
            mac.mouse(menu_left(index) + 10, 10, True)
            mac.wait(.2)

        def cancel():
            mac.mouse(490, 330, True)
            mac.wait(.1)
            mac.mouse(490, 330, False)
            mac.wait(.2)

        def key(code):
            before = pointer(state + 68)
            mac.press(code)
            deadline = time.monotonic() + 5
            while pointer(state + 68) == before:
                if time.monotonic() > deadline:
                    raise AssertionError('The native menu did not process the style key.')
                mac.wait(.02)

        open_menu(0)
        cancel()
        frame('menu-bar')
        if args.icons_only:
            probe = machfs.Volume()
            probe.read(args.probe_image.read_bytes())
            icons = rsrcfork.ResourceFile(io.BytesIO(probe['System Folder']['Controls'].rsrc))
            for kind, rid, name in [(b'ICON', 257, 'icon-large'), (b'SICN', 257, 'icon-small'), (b'cicn', 258, 'icon-color')]:
                (args.output / (name + '.bin')).write_bytes(icons[kind][rid].data)
            heights = [34, 18, 18, 34, 18, 18, 34, 16, 34, 34]
            if read(state + 9804, 2) != bytes(2):
                key(34)
            for disabled in [False, True]:
                if disabled:
                    key(34)
                prefix = 'disabled-' if disabled else ''
                trace(prefix + 'MDEF-icons', lambda: open_menu(5), start=0x95c, length=0xabe)
                if bounds(5)[1] != sum(heights):
                    raise AssertionError('The native icon menu has unexpected row heights.')
                frame(prefix + 'icons-open')
                (args.output / 'icons.bounds').write_bytes(struct.pack('>4h', menu_left(5), 20, *bounds(5)))
                cancel()
                top = 20
                for index, height in enumerate(heights):
                    before = pointer(state + 52)
                    open_menu(5)
                    x, y = menu_left(5) + 50, top + height // 2
                    mac.mouse(x, y, True)
                    frame(prefix + f'icons-{index}-selected')
                    mac.mouse(x, y, False)
                    mac.wait(.5)
                    expected = 0 if disabled else (133 << 16) | (index + 1)
                    if pointer(state + 48) != expected or pointer(state + 52) != before + (0 if disabled else 1):
                        raise AssertionError(f'The native icon item {index} did not track its mouse command.')
                    top += height
                for index, code_key in [(9, 40), (10, 1)]:
                    before = pointer(state + 52)
                    mac.key(55, True)
                    try:
                        mac.press(code_key)
                    finally:
                        mac.key(55, False)
                    mac.wait(.2)
                    expected = 0 if disabled else (133 << 16) | index
                    if pointer(state + 48) != expected or pointer(state + 52) != before + (0 if disabled else 1):
                        raise AssertionError(f'The native icon item {index} did not track its keyboard command.')
                print(f'Ten native icon rows: mouse and keyboard commands passed; disabled={disabled}.', flush=True)
            key(34)
            mac.command('pause')
            return
        if args.hierarchy_only:
            (args.output / 'slope-angles.bin').write_bytes(read(state + 1354, 8450))
            if int.from_bytes(read(state + 1352, 2), 'big'):
                key(15)
            def menu_state():
                save = pointer(pointer(0xb5c))
                length = int.from_bytes(read(save, 2), 'big')
                if length % 28 or length > 140:
                    raise AssertionError('The native menu save stack has an invalid size.')
                records = read(save + 28, length) if length else b''
                menus = []
                menu_list = pointer(pointer(0xa1c))
                for offset in range(0, length, 28):
                    top, left, bottom, right = struct.unpack_from('>4h', records, offset)
                    flags, list_offset = struct.unpack_from('>2H', records, offset + 12)
                    menu_id = int.from_bytes(read(pointer(pointer(menu_list + list_offset)), 2), 'big')
                    content_top, content_bottom = struct.unpack_from('>2h', records, offset + 20)
                    menus.append((menu_id, top, left, bottom, right, content_top, content_bottom, flags))
                return menus

            def save(name):
                frame(name)
                mac.command('pause')
                try:
                    mac.frame(args.output / (name + extension))
                    menus = menu_state()
                    (args.output / (name + '.menus')).write_bytes(struct.pack('>H', len(menus))
                        + b''.join(struct.pack('>8h', *menu) for menu in menus))
                finally:
                    mac.command('resume')
                print(name, menus, flush=True)
                return menus

            def wait_depth(count):
                deadline = time.monotonic() + 3
                while len(menu_state()) != count:
                    if time.monotonic() > deadline:
                        raise AssertionError(f'The native menu stack did not reach depth {count}.')
                    mac.wait(.01)

            def hover(menu, item, x=25):
                mac.mouse(menu[2] + x, menu[1] + (item - 1) * 16 + 8, True)

            def tick():
                return int.from_bytes(read(0x16a, 4), 'big')

            save_pointer = pointer(pointer(0xb5c))
            delays = read(save_pointer + 14, 2)
            (args.output / 'submenu-delays.bin').write_bytes(delays)
            print(f'Native submenu delay: {delays[0]} ticks; diagonal tracking: {delays[1]} ticks.', flush=True)
            save('hierarchy-bar')
            trace('MDEF-hierarchy', lambda: open_menu(2))
            parent = save('hierarchy-open')[0]
            hover(parent, 3)
            mac.wait(.3)
            if len(save('hierarchy-disabled')) != 1:
                raise AssertionError('A disabled native submenu opened.')
            started = tick()
            hover(parent, 2)
            wait_depth(2)
            print(f'Native submenu appeared after {tick() - started} ticks.', flush=True)
            nested = save('hierarchy-nested')[1]
            hover(nested, 1)
            save('hierarchy-child')
            hover(nested, 3)
            save('hierarchy-child-disabled')
            hover(nested, 2)
            wait_depth(3)
            deep = save('hierarchy-deep')[2]
            hover(deep, 2)
            save('hierarchy-checked')
            hover(parent, 2)
            wait_depth(2)
            save('hierarchy-return')
            hover(parent, 1)
            wait_depth(1)
            save('hierarchy-leaf')
            hover(parent, 2)
            wait_depth(2)
            nested = menu_state()[1]
            before = pointer(state + 52)
            hover(nested, 2)
            wait_depth(3)
            deep = menu_state()[2]
            hover(deep, 2)
            mac.wait(.1)
            mac.mouse(deep[2] + 25, deep[1] + 24, False)
            mac.wait(.6)
            if pointer(state + 48) != (135 << 16) | 2 or pointer(state + 52) != before + 1:
                raise AssertionError('The native nested command did not select Checked.')
            save('hierarchy-command')
            open_menu(2)
            parent = menu_state()[0]
            hover(parent, 2)
            wait_depth(2)
            nested = menu_state()[1]
            mac.mouse(nested[2] - 25, parent[1] + 40, True)
            if len(save('hierarchy-diagonal')) != 2:
                raise AssertionError('The native submenu closed during diagonal tracking.')
            wait_depth(1)
            save('hierarchy-diagonal-expired')
            cancel()
            key(15)
            save('hierarchy-right-bar')
            open_menu(5)
            parent = save('hierarchy-right-open')[0]
            hover(parent, 2)
            wait_depth(2)
            nested = save('hierarchy-right-nested')[1]
            hover(nested, 2)
            wait_depth(3)
            deep = save('hierarchy-right-deep')[2]
            hover(deep, 2)
            save('hierarchy-right-checked')
            cancel()
            key(15)
            open_menu(3)
            parent = save('scroll-open')[0]
            hover(parent, 26 if color else 18)
            wait_depth(2)
            nested = save('hierarchy-bottom')[1]
            hover(nested, 2)
            wait_depth(3)
            save('hierarchy-bottom-deep')
            cancel()
            open_menu(3)
            parent = menu_state()[0]

            def content_top():
                return int.from_bytes(read(0xa0a, 2), 'big', signed=True)

            def scroll_to(name, target, down):
                y = parent[3] - 12 if down else parent[1] + 12
                mac.mouse(parent[2] + 20, y, True)
                deadline = time.monotonic() + 12
                while (down and content_top() > target) or (not down and content_top() < target):
                    if time.monotonic() > deadline:
                        raise AssertionError(f'The native {name} scroll did not reach {target}.')
                    mac.wait(.005)
                mac.mouse(parent[2] - 10, parent[1] + 80, True)
                menus = save(name)
                if menus[0][5] != target:
                    raise AssertionError(f'The native {name} scrolled to {menus[0][5]}, expected {target}.')

            scroll_to('scroll-down-first', 4, True)
            scroll_to('scroll-middle', 20 - 160, True)
            bottom_top = parent[3] - 640
            scroll_to('scroll-end', bottom_top, True)
            scroll_to('scroll-up-first', bottom_top + 16, False)
            scroll_to('scroll-start', 20, False)
            cancel()
            mac.command('pause')
            return
        font_address = ram.find(bytes.fromhex('4242142c00066014206efdb8'))
        if font_address < 0:
            raise AssertionError('The System 7 StdText style routine is missing.')
        font_code = read(font_address, 1024)
        (args.output / 'QuickDraw-styles.bin').write_bytes(font_code)
        underline = font_code.index(bytes.fromhex('4a2c000a'))
        underline_end = font_code.index(bytes.fromhex('3d7c000a'), underline)
        outline = font_code.index(bytes.fromhex('4243162c000b'), underline_end)
        for name, start, end in [('bold', 0, 32), ('italic', 32, underline),
                                 ('underline', underline, underline_end), ('outline', outline, outline + 260)]:
            mac.command('trace', count=4096, low=font_address + start, high=font_address + end)
            open_menu(0)
            mac.command('trace_stop')
            result = mac.command('trace_get')
            if not result['pcs']:
                raise AssertionError(f'The native {name} font routine did not execute.')
            (args.output / ('QuickDraw-' + name + '.trace')).write_text(result['disassembly'])
            print(f'QuickDraw {name}: {len(result["pcs"])} executed instructions.', flush=True)
            cancel()
        if args.trace_only:
            mac.command('pause')
            return
        if args.interaction_only:
            styles = [0, 1, 2, 4, 8, 16, 32, 64, 127]
            for disabled in [False, True]:
                if disabled:
                    key(7 if page_size == 8 else 2)
                for index, code_key in enumerate([0, 11, 8, 2, 14, 3, 5, 4, 34]):
                    before = pointer(state + 52)
                    mac.key(55, True)
                    try:
                        mac.press(code_key)
                    finally:
                        mac.key(55, False)
                    mac.wait(.2)
                    expected = 0 if disabled else (128 << 16) | (index + 1)
                    if pointer(state + 48) != expected or pointer(state + 52) != before + (0 if disabled else 1):
                        raise AssertionError(f'The native style {styles[index]} keyboard command differs.')
                top = 20
                for index, style in enumerate(styles):
                    before = pointer(state + 52)
                    ascent, descent, _, leading, _ = struct.unpack_from('>5h', metrics, style * 10)
                    height = ascent + descent + leading
                    open_menu(0)
                    mac.mouse(menu_left(0) + 25, top + height // 2, True)
                    mac.wait(.1)
                    mac.mouse(menu_left(0) + 25, top + height // 2, False)
                    mac.wait(.6)
                    expected = 0 if disabled else (128 << 16) | (index + 1)
                    if pointer(state + 48) != expected or pointer(state + 52) != before + (0 if disabled else 1):
                        raise AssertionError(f'The native style {style} mouse command differs.')
                    top += height
                print(f'Nine native styles: keyboard and mouse commands passed; disabled={disabled}.', flush=True)
            key(7 if page_size == 8 else 2)
            mac.command('pause')
            return
        for disabled in [False, True]:
            if disabled:
                key(7)
            prefix = 'disabled-' if disabled else ''
            trace(prefix + 'MDEF-styles', lambda: open_menu(0))
            frame(prefix + 'styles-open')
            (args.output / (prefix + 'styles.bounds')).write_bytes(struct.pack('>4h', menu_left(0), 20, *bounds(0)))
            top = 20
            for index, style in enumerate([0, 1, 2, 4, 8, 16, 32, 64, 127]):
                ascent, descent, _, leading, _ = struct.unpack_from('>5h', metrics, style * 10)
                height = ascent + descent + leading
                mac.mouse(menu_left(0) + 50, top + height // 2, True)
                frame(prefix + f'styles-{index}')
                top += height
            cancel()
            for page, code_key in enumerate([29, 18, 19, 20, 21, 23, 22, 26, 28, 25, 0, 11, 8, 2, 14, 3]):
                key(code_key)
                trace(prefix + f'MDEF-mixed-{page}', lambda: open_menu(1))
                frame(prefix + f'mixed-{page}-open')
                (args.output / (prefix + f'mixed-{page}.bounds')).write_bytes(struct.pack('>4h', menu_left(1), 20, *bounds(1)))
                top = 20
                for index in range(page_size):
                    style = page * page_size + index
                    ascent, descent, _, leading, _ = struct.unpack_from('>5h', metrics, style * 10)
                    height = ascent + descent + leading
                    mac.mouse(menu_left(1) + 35, top + height // 2, True)
                    frame(prefix + f'mixed-{style}-selected')
                    top += height
                cancel()
        key(7)
        if not args.styles_only:
            for index, name in [(2, 'more'), (3, 'scroll'), (4, 'marks'), (5, 'icons')]:
                trace('MDEF-' + name, lambda index=index: open_menu(index))
                frame(name + '-open')
                (args.output / (name + '.bounds')).write_bytes(struct.pack('>4h', menu_left(index), 20, *bounds(index)))
                cancel()
        mac.command('pause')
        print('All 128 native font styles were captured in enabled and disabled menus.', flush=True)


if __name__ == '__main__':
    main()
