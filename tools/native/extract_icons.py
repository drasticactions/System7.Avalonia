"""Extract Assets/GenericIcons.bin: the System's generic icon families and alert icons.

Reads ICN#, icl4, icl8, ics#, ics4 and ics8 from a System 7 volume's System file
with machfs and rsrcfork. System 7.0.1's generic icons are one-bit, which is how
the Finder drew them at every depth; a colour member is kept wherever one exists.
The alert ICONs 0, 1 and 2 (stop, note, caution) carry no mask, so they get a
full one.

Needs: pip install machfs rsrcfork

GenericIcons.bin: u16 count, then per icon: u8 name length, ASCII name,
u8 member flags (1 icl4, 2 icl8, 4 ics#, 8 ics4, 16 ics8), the 256-byte ICN#,
then each member the flags name, in that order.
"""
import argparse
import io
import struct
from pathlib import Path

import machfs
import rsrcfork

ASSETS = Path(__file__).resolve().parents[2] / 'src/System7.Avalonia/Assets'
ICONS = [
    ('document', -4000),
    ('folder', -3999),
    ('floppy', -3998),
    ('application', -3996),
    ('private-folder', -3994),
    ('trash', -3993),
    ('desk-accessory', -3991),
    ('stationery', -3985),
    ('trash-full', -3984),
    ('system-folder', -3983),
    ('apple-menu-folder', -3982),
    ('control-panels-folder', -3980),
    ('extensions-folder', -3979),
    ('preferences-folder', -3977),
    ('hard-disk', -16502),
    ('macintosh', 3),
]
ALERT_ICONS = [('stop', 0), ('note', 1), ('caution', 2)]
MEMBERS = [(b'icl4', 1, 512), (b'icl8', 2, 1024), (b'ics#', 4, 64), (b'ics4', 8, 128), (b'ics8', 16, 256)]


def main():
    parser = argparse.ArgumentParser(description='Extract the System 7 generic icons.')
    parser.add_argument('--system', required=True, type=Path, help='System 7.0.1 volume.')
    args = parser.parse_args()
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    system = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    out = bytearray(struct.pack('>H', len(ICONS) + len(ALERT_ICONS)))
    for name, rid in ICONS:
        icon = system[b'ICN#'][rid].data
        if len(icon) != 256:
            raise SystemExit(f'ICN# {rid} is not 256 bytes')
        flags, members = 0, b''
        for kind, bit, length in MEMBERS:
            if kind in system and rid in system[kind]:
                data = system[kind][rid].data
                if len(data) != length:
                    raise SystemExit(f'{kind.decode()} {rid} is not {length} bytes')
                flags |= bit
                members += data
        out += bytes([len(name)]) + name.encode('ascii') + bytes([flags]) + icon + members
    for name, rid in ALERT_ICONS:
        icon = system[b'ICON'][rid].data
        if len(icon) != 128:
            raise SystemExit(f'ICON {rid} is not 128 bytes')
        out += bytes([len(name)]) + name.encode('ascii') + bytes([0]) + icon + b'\xff' * 128
    (ASSETS / 'GenericIcons.bin').write_bytes(bytes(out))
    print(f'{len(ICONS) + len(ALERT_ICONS)} icons')


if __name__ == '__main__':
    main()
