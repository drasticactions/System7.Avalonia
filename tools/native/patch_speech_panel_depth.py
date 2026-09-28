import argparse
import io
from pathlib import Path
import struct
import subprocess
import tempfile

import machfs
from macresources import Resource, make_file
import rsrcfork


def main():
    parser = argparse.ArgumentParser(description='Force a color depth before the original Speech cdev runs.')
    parser.add_argument('--image', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--assembler', type=Path, required=True)
    parser.add_argument('--depth', type=int, choices=(16, 32), required=True)
    args = parser.parse_args()
    if args.output.exists():
        parser.error('The output image must not exist.')

    volume = machfs.Volume()
    volume.read(args.image.read_bytes())
    panel = volume['System Folder']['Control Panels']['Speech']
    original = rsrcfork.ResourceFile(io.BytesIO(panel.rsrc))
    source = Path(__file__).with_name('speech_depth_trampoline.s')
    with tempfile.TemporaryDirectory(prefix='sys7-speech-depth-') as temporary:
        binary = Path(temporary) / 'trampoline.bin'
        defines = ['-DDEPTH32'] if args.depth == 32 else []
        subprocess.run([str(args.assembler), '-Fbin', '-m68000', *defines, '-o', str(binary), str(source)], check=True)
        trampoline = bytearray(binary.read_bytes())
    code = original[b'cdev'][-4064].data_raw
    if code[:4] != bytes.fromhex('4e56ffe6'):
        raise AssertionError('The Speech cdev entry differs from the original LINK instruction.')
    if len(code) + len(trampoline) >= 32768:
        raise AssertionError('The cdev trampoline exceeds the 68k branch range.')
    struct.pack_into('>h', trampoline, len(trampoline) - 4, 6 - len(code) - len(trampoline))
    patched = struct.pack('>Hh', 0x6000, len(code) - 4) + code[4:] + trampoline
    replacements = []
    for kind in original:
        for rid, resource in original[kind].items():
            data = patched if (kind, rid) == (b'cdev', -4064) else resource.data_raw
            name = resource.name.decode('mac_roman') if resource.name is not None else None
            replacements.append(Resource(kind, rid, name=name, attribs=resource.attributes.value, data=data))
    panel.rsrc = make_file(replacements)
    check = rsrcfork.ResourceFile(io.BytesIO(panel.rsrc))
    for kind in original:
        for rid, resource in original[kind].items():
            if (kind, rid) != (b'cdev', -4064) and check[kind][rid].data_raw != resource.data_raw:
                raise AssertionError(f'The original {kind!r} {rid} resource changed.')
    if check[b'cdev'][-4064].data_raw != patched:
        raise AssertionError('The patched cdev did not round-trip through the resource fork.')
    args.output.write_bytes(volume.write(size=len(args.image.read_bytes())))
    print(f'Kept every original resource except the Speech cdev entry; selected {args.depth}-bit color.')


if __name__ == '__main__':
    main()
