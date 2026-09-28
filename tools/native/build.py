import argparse
import io
from pathlib import Path
import struct
import subprocess
import tempfile

import machfs
import rsrcfork
from macresources import Resource, make_file


def word(value):
    return struct.pack('>H', value)


def rect(top, left, bottom, right):
    return struct.pack('>hhhh', top, left, bottom, right)


def main():
    parser = argparse.ArgumentParser(description='Build a native 68k control probe on a separate System 7 disk.')
    parser.add_argument('--assembler', required=True, type=Path)
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    parser.add_argument('--probe', choices=['controls', 'control_variants', 'memory_slider', 'sound_slider', 'network_slider', 'speech_custom', 'speech_buttons', 'speech_slider', 'colors', 'windows', 'window_types', 'round_windows', 'dialogs', 'menus', 'menu_items', 'text', 'lists', 'icon_lists', 'sicn_lists', 'popups', 'progress', 'pictures', 'media', 'resources'], default='controls')
    parser.add_argument('--color-system', type=Path, help='System 7.0.1 Install 1 image supplying the Mac II system patches.')
    parser.add_argument('--window-palette', choices=['accent', 'flat'])
    parser.add_argument('--menu-palette', choices=['accent'])
    args = parser.parse_args()
    if args.window_palette and (args.probe != 'window_types' or not args.color_system):
        parser.error('--window-palette requires the color window_types probe.')
    if args.menu_palette and (args.probe != 'menus' or not args.color_system):
        parser.error('--menu-palette requires the color menus probe.')
    if args.output.exists():
        parser.error('The output path must not exist.')
    source = Path(__file__).with_name(('window_types' if args.probe == 'round_windows' else 'controls' if args.probe in ('control_variants', 'memory_slider', 'sound_slider', 'network_slider', 'speech_custom', 'speech_buttons', 'speech_slider') else 'lists' if args.probe in ('icon_lists', 'sicn_lists') else args.probe) + '.s')
    with tempfile.TemporaryDirectory(prefix='sys7-68k-') as temporary:
        code = Path(temporary) / 'controls.bin'
        defines = ['-DCOLOR'] if args.color_system else []
        if args.probe == 'icon_lists':
            defines.append('-DICON_LISTS')
        if args.probe == 'sicn_lists':
            defines.append('-DSICN_LISTS')
        if args.probe in ('control_variants', 'memory_slider', 'sound_slider', 'network_slider', 'speech_custom', 'speech_buttons', 'speech_slider'):
            defines.append('-DCONTROL_VARIANTS')
        if args.probe == 'memory_slider':
            defines.append('-DMEMORY_SLIDER')
        if args.probe == 'sound_slider':
            defines.append('-DSOUND_SLIDER')
        if args.probe == 'network_slider':
            defines.append('-DNETWORK_SLIDER')
        if args.probe == 'speech_custom':
            defines.append('-DSPEECH_CUSTOM')
        if args.probe == 'speech_buttons':
            defines.append('-DSPEECH_BUTTONS')
        if args.probe == 'speech_slider':
            defines.append('-DSPEECH_SLIDER')
        if args.menu_palette:
            defines.append('-DMENU_PALETTE')
        if args.probe == 'round_windows':
            defines.append('-DROUND_WINDOWS')
        if args.probe == 'round_windows' or args.probe == 'window_types' and args.color_system:
            defines.append('-DWINDOW_BACKDROP')
        subprocess.run([str(args.assembler), '-Fbin', '-m68000', *defines, '-o', str(code), str(source)], check=True)
        code1 = code.read_bytes()
    items = [
        ((20, 20, 40, 120), 4, b'Button'),
        ((20, 145, 40, 245), 132, b'Button'),
        ((20, 270, 40, 370), 4, b'Button'),
        ((65, 20, 85, 160), 5, b'Check box'),
        ((95, 20, 115, 160), 5, b'Check box'),
        ((125, 20, 145, 160), 133, b'Check box'),
        ((65, 220, 85, 360), 6, b'Radio button'),
        ((95, 220, 115, 360), 6, b'Radio button'),
        ((125, 220, 145, 360), 134, b'Radio button'),
        ((170, 20, 190, 370), 16, b'Text'),
        ((20, 390, 220, 406), 7, word(129)),
        ((210, 20, 226, 370), 7, word(130)),
        ((20, 372, 220, 388), 7, word(131)),
        ((232, 20, 248, 370), 7, word(132)),
    ]
    if args.probe == 'control_variants':
        items[9] = ((170, 20, 186, 120), 7, word(133))
        items.extend([
            ((170, 145, 186, 245), 7, word(134)),
            ((170, 270, 186, 370), 7, word(135)),
        ])
    if args.probe == 'memory_slider':
        items[9] = ((170, 20, 190, 164), 7, word(133))
    if args.probe == 'sound_slider':
        items = [((280, 20, 300, 120), kind, text) for _, kind, text in items]
        items[9] = ((100, 20, 204, 44), 7, word(133))
    if args.probe == 'network_slider':
        items[9] = ((170, 20, 190, 120), 7, word(133))
    if args.probe == 'speech_custom':
        items[9] = ((170, 20, 187, 39), 7, word(133))
        items[10] = ((170, 145, 187, 164), 7, word(134))
    if args.probe == 'speech_buttons':
        items[9] = ((170, 20, 186, 130), 7, word(133))
        items[10] = ((170, 175, 186, 343), 7, word(134))
    if args.probe == 'speech_slider':
        items[9] = ((170, 20, 195, 143), 7, word(133))
    if args.probe == 'dialogs':
        items = [
            ((20, 20, 40, 120), 4, b'OK'),
            ((20, 150, 40, 250), 4, b'Cancel'),
            ((75, 20, 105, 120), 4, b'Other'),
            ((75, 150, 96, 251), 4, b'Odd'),
        ]
    if args.probe == 'text':
        items = [
            ((220, 20, 240, 100), 4, b'Reset'),
            ((20, 20, 40, 300), 16, b'Macintosh'),
            ((65, 20, 85, 300), 16, b'System 7'),
            ((110, 20, 174, 300), 16, b'Select and edit this text.\rA second line.'),
            ((190, 160, 210, 390), 16, b'Another'),
            ((190, 20, 210, 150), 8, b'Name:'),
        ]
    ditl = bytearray(word(len(items) - 1))
    for bounds, kind, text in items:
        ditl += bytes(4) + rect(*bounds) + bytes([kind, len(text)]) + text
        if len(ditl) & 1:
            ditl += b'\0'
    dlog = rect(50, 40, 310, 470) + struct.pack('>hBBBBIhB', 1, 1, 0, 0, 0, 0, 128, 0)
    controls = [
        Resource(b'CNTL', rid, data=rect(*bounds) + struct.pack('>hBBhhhIB', value, 1, 0, maximum, 0, 16, 0, 0))
        for rid, bounds, value, maximum in [
            (129, (20, 390, 220, 406), 50, 100),
            (130, (210, 20, 226, 370), 50, 100),
            (131, (20, 372, 220, 388), 50, 100),
            (132, (232, 20, 248, 370), 0, 0),
        ]
    ]
    if args.probe == 'control_variants':
        controls.extend(Resource(b'CNTL', rid, data=rect(*bounds) + struct.pack('>hBBhhhIB', value, 1, 0, 1, 0, procedure, 0, len(title)) + title)
                        for rid, bounds, value, procedure, title in [
                            (133, (170, 20, 186, 120), 0, 10, b'12hr.'),
                            (134, (170, 145, 186, 245), 1, 10, b'24hr.'),
                            (135, (170, 270, 186, 370), 1, 9, b'Use sound'),
                        ])
    menus = []
    if args.probe == 'menu_items':
        def entry(text, style=0, command=0, mark=0, icon=0):
            text = text.encode('mac_roman')
            return bytes([len(text)]) + text + bytes([icon, command, mark, style])

        specs = [
            (128, b'Style', [entry(label + ' Agj', style, ord('A') + index, 0x12)
                for index, (label, style) in enumerate([('Plain', 0), ('Bold', 1), ('Italic', 2), ('Underline', 4),
                    ('Outline', 8), ('Shadow', 16), ('Condensed', 32), ('Extended', 64), ('All', 127)])]),
            (129, b'Mixed', [entry('Agj pq _/', index, ord('T'), 0x12) for index in range(8)]),
            (130, b'More', [entry('Leaf'), entry('Submenu', command=0x1b, mark=134), entry('Disabled', command=0x1b, mark=135), entry('Last')]),
            (131, b'Scroll', [entry(f'Item {index:02}', command=0x1b if index in (18, 26) else 0,
                mark=134 if index in (18, 26) else 0) for index in range(1, 41)]),
            (132, b'Marks', [entry('Marked', mark=mark) for mark in [0x12, 0x13, 0xa5, ord('*'), ord('+')]]),
            (133, b'Icons', [entry('Large', icon=1), entry('Reduced', icon=1, command=0x1d), entry('Small', icon=1, command=0x1e),
                entry('Color', icon=2), entry('Color reduced', icon=2, command=0x1d), entry('Color small', icon=2, command=0x1e),
                entry('Missing', icon=3), entry('Plain'), entry('Marked', icon=1, command=ord('K'), mark=0x12),
                entry('Styled', icon=1, style=127, command=ord('S'), mark=0x12)]),
            (134, b'Nested', [entry('Child'), entry('Deep', command=0x1b, mark=135), entry('Disabled'), entry('Last')]),
            (135, b'Deep', [entry('First'), entry('Checked', mark=0x12), entry('Last')]),
        ]
        for rid, title, entries in specs:
            flags = 0xfffffff7 if rid in (130, 134) else 0xffffffff
            data = struct.pack('>hhhII', rid, 0, 0, 0, flags) + bytes([len(title)]) + title + b''.join(entries) + b'\0'
            menus.append(Resource(b'MENU', rid, data=data))
    if args.menu_palette:
        entries = [
            (0, 0, [0x332266, 0xffeedd, 0x003366, 0xeeddff]),
            (129, 0, [0x005522, 0, 0x550011, 0xddffee]),
            (129, 1, [0x006600, 0x000099, 0x990000, 0xffffff]),
            (129, 5, [0x000099, 0x660066, 0x000099, 0xffffff]),
            (129, 6, [0x990000, 0x005500, 0x000099, 0xffffff]),
        ]
        table = word(len(entries))
        for menu_id, item, palette in entries:
            table += struct.pack('>hh', menu_id, item)
            table += b''.join(struct.pack('>HHH', (rgb >> 16) * 257, ((rgb >> 8) & 255) * 257, (rgb & 255) * 257) for rgb in palette)
            table += bytes(2)
        menus.append(Resource(b'mclr', 128, data=table))
    pictures = []
    if args.probe == 'media':
        specs = [
            ((20, 20, 49, 49), 992, -16485, 0), ((20, 60, 49, 89), 992, -16484, 0),
            ((20, 100, 49, 129), 992, -16483, 0), ((20, 140, 49, 169), 992, -16482, 0),
            ((20, 180, 49, 209), 992, 32767, 0), ((20, 220, 40, 240), 992, -16484, 0),
            ((20, 260, 60, 320), 992, -16484, 0),
            *[((90, 20 + i * 50, 122, 52 + i * 50), 994, -16487, value)
              for i, value in enumerate([0, 16, 17, 50, 99, 100])],
            ((150, 20, 182, 52), 995, 0, 0),
        ]
        controls = [Resource(b'CNTL', 128 + index, data=rect(*bounds)
            + struct.pack('>hBBhhhIB', value, 1, 0, 100, 0, proc, ref & 0xffffffff, 0))
            for index, (bounds, proc, ref, value) in enumerate(specs)]
    if args.probe == 'pictures':
        controls = [
            Resource(b'CNTL', 128 + index, data=rect(*bounds) + struct.pack('>hBBhhhIB', 0, 1, 0, 1, 0, proc, ref & 0xffffffff, 0))
            for index, (bounds, proc, ref) in enumerate([
                ((20, 20, 40, 100), 976, -6046), ((20, 130, 40, 210), 977, -6046),
                ((60, 20, 90, 120), 976, 128), ((110, 20, 126, 80), 976, -6046),
                ((150, 20, 190, 120), 976, 128), ((200, 20, 221, 120), 976, -6046),
                ((201, 150, 222, 251), 977, -6046), ((20, 270, 40, 350), 976, 129),
            ])
        ]
        pictures = [
            Resource(b'picb', rid, data=struct.pack('>hhhhI', 1, -6046, y, x, 0))
            for rid, x, y in [(128, 10, 5), (129, -10, -5)]
        ]
    if args.probe == 'progress':
        controls = [
            Resource(b'CNTL', 128 + index, data=rect(*bounds) + struct.pack('>hBBhhhIB', value, 1, 0, 100, 0, 993, 0, 0))
            for index, (bounds, value) in enumerate([
                ((20, 20, 36, 280), 0), ((50, 20, 67, 281), 25),
                ((85, 20, 105, 280), 50), ((125, 20, 145, 280), 100),
                ((20, 330, 210, 346), 25), ((20, 360, 211, 377), 50),
                ((170, 20, 210, 60), 50), ((220, 20, 226, 280), 75),
            ])
        ]
    if args.probe == 'popups':
        labels = [b'Chicago', b'Geneva', b'Monaco', b'Disabled', b'-', b'A very long font name']
        menu = struct.pack('>hhhII', 128, 0, 0, 0, 0xffffffef) + bytes([4]) + b'Font'
        for label in labels:
            menu += bytes([len(label)]) + label + bytes(4)
        menus.append(Resource(b'MENU', 128, data=menu + b'\0'))
        menus.append(Resource(b'MENU', 129, data=struct.pack('>hhhII', 129, 0, 0, 0, 0xffffffff) + b'\0\0'))
        controls = [
            Resource(b'CNTL', rid, data=rect(*bounds) + struct.pack('>hBBhhhI', 0, 1, 0, title_width, menu_id, proc, 0) + bytes([len(title)]) + title)
            for rid, bounds, title, title_width, proc, menu_id in [
                (128, (20, 20, 40, 250), b'Font:', 50, 1009, 128),
                (129, (65, 20, 85, 150), b'', 0, 1009, 128),
                (130, (110, 20, 130, 250), b'Font:', 50, 1009, 128),
                (131, (155, 20, 175, 320), b'Font:', 50, 1008, 128),
                (132, (200, 20, 220, 110), b'', 0, 1009, 128),
                (133, (235, 20, 255, 150), b'', 0, 1009, 129),
            ]
        ]
    windows = [
        Resource(b'WIND', rid, data=rect(*bounds) + struct.pack('>hBBBBI', proc, 1, 0, 1, 0, 0) + bytes([len(title)]) + title)
        for rid, bounds, proc, title in [
            (128, (50, 40, 310, 470) if args.probe in ('lists', 'icon_lists', 'sicn_lists', 'popups', 'progress', 'pictures', 'media') else (80, 45, 275, 325), 8,
             b'Lists' if args.probe in ('lists', 'icon_lists', 'sicn_lists') else b'Pop-ups' if args.probe == 'popups' else b'Progress' if args.probe == 'progress' else b'Pictures' if args.probe == 'pictures' else b'System 7'),
            (129, (100, 360, 240, 490), 0, b'Other'),
        ]
    ]
    if args.probe in ('menus', 'menu_items'):
        windows.append(Resource(b'WIND', 141, data=rect(20, 0, 480 if args.color_system else 342, 640 if args.color_system else 512)
            + struct.pack('>hBBBBI', 2, 1, 0, 0, 0, 0) + b'\0'))
    app = machfs.File()
    if args.probe in ('window_types', 'round_windows'):
        variants = [16, 18, 20, 22, 24, 26, 28, 30, 17] if args.probe == 'round_windows' else [1, 2, 3, 5, 13, 0, 4, 8, 12]
        windows = [
            Resource(b'WIND', 128 + index, data=rect(80, 80, 260, 400) + struct.pack('>hBBBBI', proc, 1, 0, 1, 0, 0) + b'\x06Dialog')
            for index, proc in enumerate(variants)
        ]
        windows.append(Resource(b'WIND', 140, data=rect(100, 425, 260, 505) + struct.pack('>hBBBBI', 0, 1, 0, 1, 0, 0) + b'\x05Other'))
        if args.probe == 'round_windows' or args.color_system:
            windows.append(Resource(b'WIND', 141, data=rect(20, 0, 480, 640) + struct.pack('>hBBBBI', 2, 1, 0, 0, 0, 0) + b'\0'))
        if args.window_palette:
            palette = [0xffeecc, 0x112244, 0x330055, 0, 0xffffff, 0xffcc99, 0x331100,
                       0xeeffcc, 0x003311, 0xccffdd, 0x114422, 0xffdd77, 0x442200]
            if args.window_palette == 'flat':
                palette = [0xffffff, 0xff0000, 0x0000ff, 0, 0xffffff] + [0x66aa88] * 8
            table = struct.pack('>IHH', 0, 0, 12) + b''.join(
                struct.pack('>HHHH', index, (rgb >> 16) * 257, ((rgb >> 8) & 255) * 257, (rgb & 255) * 257)
                for index, rgb in enumerate(palette))
            windows.extend(Resource(b'wctb', 128 + index, data=table) for index in range(len(variants)))
    app.type = b'APPL'
    app.creator = b'S7UI'
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    if args.color_system:
        installer = machfs.Volume()
        installer.read(args.color_system.read_bytes())
        original = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
        replacement = rsrcfork.ResourceFile(io.BytesIO(installer['System'].rsrc))
        for kind in (b'vers', b'CDEF', b'WDEF', b'MDEF', b'MBDF', b'LDEF'):
            if set(original[kind]) != set(replacement[kind]) or any(
                    original[kind][rid].data != replacement[kind][rid].data for rid in original[kind]):
                parser.error(f'The installer has different {kind.decode()} resources from the source disk.')
        volume['System Folder']['System'] = installer['System']
    special_definitions = []
    if args.probe == 'memory_slider':
        memory = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['Control Panels']['Memory'].rsrc))
        slider = bytearray(memory[b'CNTL'][-4040].data)
        slider[:8] = rect(170, 20, 190, 164)
        controls.append(Resource(b'CNTL', 133, data=bytes(slider)))
        special_definitions.append(Resource(b'CDEF', 3, data=memory[b'CDEF'][3].data))
        special_definitions.append(Resource(b'sldr', -4040, data=memory[b'sldr'][-4040].data))
        special_definitions.extend(Resource(b'PICT', rid, data=resource.data)
                                   for rid, resource in memory[b'PICT'].items())
    if args.probe == 'sound_slider':
        sound = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['Control Panels']['Sound'].rsrc))
        slider = bytearray(sound[b'CNTL'][-4048].data)
        slider[:8] = rect(100, 20, 204, 44)
        controls.append(Resource(b'CNTL', 133, data=bytes(slider)))
        special_definitions.append(Resource(b'CDEF', 3, data=sound[b'CDEF'][3].data))
        special_definitions.append(Resource(b'sldr', -4048, data=sound[b'sldr'][-4048].data))
        special_definitions.extend(Resource(b'PICT', rid, data=resource.data)
                                   for rid, resource in sound[b'PICT'].items())
    if args.probe == 'network_slider':
        network = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['Extensions']['Network Extension'].rsrc))
        slider = bytearray(network[b'CNTL'][24500].data)
        slider[:8] = rect(170, 20, 190, 120)
        # A unique CDEF ID prevents a cached Memory slider CDEF 3 from taking this control.
        slider[16:18] = word(160)
        controls.append(Resource(b'CNTL', 133, data=bytes(slider)))
        special_definitions.append(Resource(b'CDEF', 10, data=network[b'CDEF'][3].data))
        special_definitions.append(Resource(b'sldr', 24500, data=network[b'sldr'][24500].data))
        special_definitions.extend(Resource(b'PICT', rid, data=resource.data)
                                   for rid, resource in network[b'PICT'].items())
    if args.probe == 'speech_custom':
        speech = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['Control Panels']['Speech'].rsrc))
        for rid, bounds in [(133, (170, 20, 187, 39)), (134, (170, 145, 187, 164))]:
            original = speech[b'CNTL'][-4038 if rid == 133 else -4037]
            control = bytearray(original.data)
            control[:8] = rect(*bounds)
            control[16:18] = word(192)
            controls.append(Resource(b'CNTL', rid, data=bytes(control)))
        special_definitions.append(Resource(b'CDEF', 12, data=speech[b'CDEF'][4].data))
        for kind in [b'PICT', b'icm#', b'icm4', b'icm8', b'ics8']:
            special_definitions.extend(Resource(kind, rid, data=resource.data)
                                       for rid, resource in speech[kind].items())
    if args.probe == 'speech_buttons':
        speech = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['Control Panels']['Speech'].rsrc))
        for rid, original_id, bounds, procedure in [
            (133, -4034, (170, 20, 186, 130), 217),
            (134, -4031, (170, 175, 186, 343), 218),
        ]:
            control = bytearray(speech[b'CNTL'][original_id].data)
            control[:8] = rect(*bounds)
            control[16:18] = word(procedure)
            controls.append(Resource(b'CNTL', rid, data=bytes(control)))
        special_definitions.append(Resource(b'CDEF', 13, data=speech[b'CDEF'][5].data))
    if args.probe == 'speech_slider':
        speech = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['Control Panels']['Speech'].rsrc))
        slider = bytearray(speech[b'CNTL'][-4042].data)
        slider[:8] = rect(170, 20, 195, 143)
        slider[16:18] = word(226)
        controls.append(Resource(b'CNTL', 133, data=bytes(slider)))
        special_definitions.append(Resource(b'CDEF', 14, data=speech[b'CDEF'][2].data))
        special_definitions.extend(Resource(b'PICT', rid, data=resource.data)
                                   for rid, resource in speech[b'PICT'].items())
    if args.probe == 'icon_lists':
        system = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
        icon = system[b'ICN#'][-32512].data
        for index in range(6):
            pictures.append(Resource(b'ICN#', 256 + index, data=icon))
            pixels = [((x // 4 + y // 4 * 8) % 16) for y in range(32) for x in range(32)]
            pictures.append(Resource(b'icl4', 256 + index, data=bytes(pixels[i] * 16 + pixels[i+1] for i in range(0,1024,2))))
            if index >= 3:
                pictures.append(Resource(b'icl8', 256 + index, data=bytes((x // 2 + y // 2 * 16) for y in range(32) for x in range(32))))
    if args.probe == 'sicn_lists':
        system = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
        for index in range(8):
            pictures.append(Resource(b'SICN', 256 + index, data=system[b'SICN'][-4000+index].data))
    if args.probe == 'menu_items':
        system = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
        pictures.extend([Resource(b'ICON', 257, data=system[b'ICON'][0].data), Resource(b'SICN', 257, data=system[b'SICN'][0].data)])
        pictures.extend([Resource(b'ICON', 258, data=system[b'ICON'][0].data), Resource(b'SICN', 258, data=system[b'SICN'][0].data)])
        pixmap = struct.pack('>IH4hHHIIIHHHHIII', 0, 0x8020, 0, 0, 32, 32, 0, 0, 0,
            72 << 16, 72 << 16, 0, 8, 1, 8, 0, 0, 0)
        bitmap = struct.pack('>IH4h', 0, 4, 0, 0, 32, 32)
        mask = bytearray(128)
        pixels = bytearray(1024)
        for y in range(32):
            for x in range(32):
                if 2 <= x < 30 and 2 <= y < 30:
                    mask[y * 4 + x // 8] |= 128 >> (x & 7)
                pixels[y * 32 + x] = (x // 8 + y // 8 * 4) % 8
                if y >= 16:
                    pixels[y * 32 + x] = ((x if x < 16 else x // 2) + 3 * y) % 8
        palette = [(65535, 65535, 65535), (0, 0, 0), (65535, 0, 0), (0, 65535, 0),
            (0, 0, 65535), (65535, 65535, 0), (0, 65535, 65535), (65535, 0, 65535)]
        table = struct.pack('>IHH', 0, 0, 7) + b''.join(struct.pack('>HHHH', i, *rgb) for i, rgb in enumerate(palette))
        pictures.append(Resource(b'cicn', 258, data=pixmap + bitmap + bitmap + bytes(4)
            + mask + system[b'ICON'][0].data + table + pixels))
    colors = []
    if args.probe == 'colors':
        system = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
        default = system[b'cctb'][0].data
        custom = bytearray(default)
        for index, rgb in enumerate(((0, 0, 0x9999), (0xffff, 0xcccc, 0x9999), (0x9999, 0, 0), (0, 0xcccc, 0xcccc))):
            struct.pack_into('>HHH', custom, 10 + index * 8, *rgb)
        colors = [Resource(b'dctb', 128, data=bytes.fromhex('000000000000ffff')),
                  Resource(b'cctb', 128, data=bytes(custom)), Resource(b'cctb', 129, data=default)]
    compressed = []
    if args.probe == 'resources':
        resources = rsrcfork.ResourceFile(io.BytesIO(volume['SimpleText'].rsrc))
        for kind, rid in [(b'dcmp', 3), (b'DITL', 601), (b'DITL', 129)]:
            resource = resources[kind][rid]
            # The probe calls the original decompressor directly, using the intact compressed bytes.
            raw_kind = b'cmpD' if kind == b'DITL' else kind
            compressed.append(Resource(raw_kind, rid, attribs=resource.attributes.value & ~1, data=resource.data_raw))
    partition_size = 2097152 if args.probe == 'colors' or args.color_system else 524288
    app.rsrc = make_file([
        Resource(b'CODE', 0, attribs=16, data=struct.pack('>IIIIHHHH', 4096, 4096, 8, 32, 0, 0x3f3c, 1, 0xa9f0)),
        Resource(b'CODE', 1, attribs=20, data=code1),
        Resource(b'DLOG', 128, data=dlog), Resource(b'DITL', 128, data=ditl),
        *controls,
        *special_definitions,
        *menus,
        *pictures,
        *windows,
        *compressed,
        *colors,
        Resource(b'SIZE', -1, data=struct.pack('>HII', 0, partition_size, partition_size)),
    ])
    volume['System Folder']['Controls'] = app
    args.output.write_bytes(volume.write(size=16 * 1024 * 1024, startapp=('System Folder', 'Controls')))


if __name__ == '__main__':
    main()
