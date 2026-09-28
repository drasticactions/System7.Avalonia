import argparse
from pathlib import Path
import shutil
import struct
import subprocess


REVISION = '6860a9a7f372c461b25dc42974b191a453ebcfb2'
MODELS = {
    'Plus': (131072, {0x4d1f8172}, 'vMac.ROM'),
    'II': (262144, {0x9779d2c4, 0x97221136}, 'MacII.ROM'),
    'IIx': (262144, {0x97221136}, 'MacIIx.ROM'),
}


def run(*args, cwd=None, stdout=None):
    subprocess.run(args, cwd=cwd, stdout=stdout, check=True)


def main():
    parser = argparse.ArgumentParser(description='Build Mini vMac with the local TCP interface on macOS.')
    parser.add_argument('--workdir', required=True, type=Path, help='A new directory outside the project.')
    parser.add_argument('--rom', required=True, type=Path, help='ROM matching the selected Macintosh model.')
    parser.add_argument('--disk', required=True, type=Path, help='System disk to copy for emulation.')
    parser.add_argument('--model', choices=MODELS, default='Plus')
    parser.add_argument('--depth', type=int, choices=(1, 2, 4, 8, 16, 32), help='Color pixel depth. Defaults to 1 for Plus, 8 for II/IIx.')
    args = parser.parse_args()
    depth = args.depth or (1 if args.model == 'Plus' else 8)
    if args.model == 'Plus' and depth != 1:
        parser.error('The Mac Plus supports only the monochrome depth.')
    rom_size, checksums, rom_name = MODELS[args.model]
    rom = args.rom.read_bytes()
    if len(rom) != rom_size:
        parser.error(f'The {args.model} ROM must contain {rom_size} bytes.')
    stored = struct.unpack('>I', rom[:4])[0]
    checksum = sum(struct.unpack(f'>{(rom_size - 4) // 2}H', rom[4:])) & 0xffffffff
    if stored not in checksums or checksum != stored:
        parser.error(f'The {args.model} ROM checksum is invalid.')
    if not args.disk.is_file():
        parser.error('The disk image does not exist.')
    workdir = args.workdir.resolve()
    if workdir.exists():
        parser.error('The work directory must not exist.')
    patch = Path(__file__).with_name('automation.patch').resolve()
    run('git', 'clone', 'https://github.com/minivmac/minivmac.git', str(workdir))
    run('git', 'checkout', '--detach', REVISION, cwd=workdir)
    run('git', 'apply', '--check', str(patch), cwd=workdir)
    run('git', 'apply', str(patch), cwd=workdir)
    run('clang', '-o', 'setup_t', 'setup/tool.c', cwd=workdir)
    with (workdir / 'setup.sh').open('w') as output:
        run('./setup_t', '-n', 'sys7-minivmac', '-t', 'mcar', '-e', 'xcd', '-m', args.model,
            '-depth', str(depth.bit_length() - 1), '-api', 'cco', '-log', '1', '-dis', '1', cwd=workdir, stdout=output)
    run('sh', 'setup.sh', cwd=workdir)
    run('xcodebuild', '-quiet', 'CODE_SIGNING_ALLOWED=NO', 'MACOSX_DEPLOYMENT_TARGET=15.0', cwd=workdir)
    (workdir / rom_name).write_bytes(rom)
    shutil.copyfile(args.disk, workdir / 'disk1.dsk')
    print(f'Built: {workdir / "minivmac.app/Contents/MacOS/minivmac"}')
    print('Set MINIVMAC_TCP_PORT=6807 when you launch the executable.')


if __name__ == '__main__':
    main()
