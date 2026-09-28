import argparse
from collections import Counter, defaultdict
import hashlib
import io
from pathlib import Path
import struct

import machfs
import rsrcfork


DEFINITIONS = {b'CDEF', b'WDEF', b'LDEF', b'MDEF', b'MBDF'}
TEMPLATES = {b'CNTL', b'WIND', b'DLOG', b'DITL', b'ALRT', b'MENU'}
COLORS = {b'cctb', b'wctb', b'mctb', b'dctb', b'actb', b'ictb', b'clut', b'pltt', b'ppat',
          b'cicn', b'crsr', b'icl4', b'icl8', b'ics4', b'ics8'}


def files(folder, path=''):
    for name, entry in folder.items():
        full_path = path + '/' + name
        if isinstance(entry, machfs.Folder):
            yield from files(entry, full_path)
        elif entry.rsrc:
            yield full_path, entry


def items(data):
    count = struct.unpack_from('>h', data)[0] + 1
    position = 2
    if count < 0:
        raise ValueError('A dialog item count is negative.')
    for _ in range(count):
        if position + 14 > len(data):
            raise ValueError('A dialog item header is truncated.')
        kind, length = data[position + 12:position + 14]
        end = position + 14 + length
        if end > len(data):
            raise ValueError('A dialog item body is truncated.')
        yield kind & 127, data[position + 14:end]
        position = (end + 1) & ~1
    if position != len(data):
        raise ValueError('The dialog item list has trailing bytes.')


def main():
    parser = argparse.ArgumentParser(description='Inventory every UI definition and dialog item list in the System 7 disk image.')
    parser.add_argument('--system', required=True, type=Path)
    parser.add_argument('--native-resources', type=Path, help='Output from the native SimpleText decompressor probe.')
    args = parser.parse_args()
    volume = machfs.Volume()
    volume.read(args.system.read_bytes())
    counts = Counter()
    item_counts = Counter()
    variants = defaultdict(set)
    errors = []
    definitions = []
    resource_files = 0
    decoded_lists = 0
    native_lists = 0
    for path, entry in files(volume):
        resource_files += 1
        resources = rsrcfork.ResourceFile(io.BytesIO(entry.rsrc))
        for kind, group in resources.items():
            counts[kind] += len(group)
            if kind not in DEFINITIONS | {b'CNTL', b'WIND', b'DLOG', b'DITL'}:
                continue
            for rid, resource in group.items():
                try:
                    try:
                        data = resource.data
                    except rsrcfork.compress.DecompressError:
                        if path != '/SimpleText' or kind != b'DITL' or rid not in (601, 129) or args.native_resources is None:
                            raise
                        data = (args.native_resources / f'SimpleText-DITL-{rid}.bin').read_bytes()
                        if len(data) != resource.compressed_info.decompressed_length:
                            raise ValueError('The native decompressed resource has an incorrect length.')
                        native_lists += 1
                    if kind in DEFINITIONS:
                        definitions.append((path, kind.decode(), rid, len(data), hashlib.sha256(data).hexdigest()))
                    elif kind in (b'CNTL', b'WIND', b'DLOG'):
                        procedure = struct.unpack_from('>h', data, 16 if kind == b'CNTL' else 8)[0]
                        variants[(path, 'CDEF' if kind == b'CNTL' else 'WDEF', procedure >> 4)].add(procedure & 15)
                    else:
                        item_counts.update(kind for kind, _ in items(data))
                        decoded_lists += 1
                except (ValueError, OSError, struct.error, rsrcfork.compress.DecompressError) as error:
                    errors.append(f'{path} {kind.decode()} {rid}: {error}')
    print(f'Resource-bearing files scanned: {resource_files}')
    print(f'Renderer definitions decoded: {len(definitions)}')
    for path, kind, rid, length, digest in sorted(definitions):
        print(f'{path}: {kind} {rid}, {length} bytes, SHA-256 {digest}')
    print('Procedure variants referenced by control, window, and dialog templates:')
    for (path, kind, rid), values in sorted(variants.items()):
        print(f'{path}: {kind} {rid}, variants {", ".join(map(str, sorted(values)))}')
    print('Resource counts:')
    for kind in sorted(DEFINITIONS | TEMPLATES | COLORS):
        print(f'{kind.decode()}: {counts[kind]}')
    print(f'Dialog item lists decoded: {decoded_lists}/{counts[b"DITL"]}, including {native_lists} from native execution')
    print('Dialog item kinds: ' + ', '.join(f'{kind}={count}' for kind, count in sorted(item_counts.items())))
    print(f'Unresolved resource reads: {len(errors)}')
    for error in errors:
        print(error)
    return 1 if errors else 0


if __name__ == '__main__':
    raise SystemExit(main())
