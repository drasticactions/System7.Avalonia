"""Extract Assets/AlertSounds.bin from the System file of a System 7.0.1 volume.

The alert sounds are the named 'snd ' resources in the System file: Simple Beep,
Quack, Droplet, Indigo, Wild Eep and Sosumi. System 7.5 keeps only Simple Beep.

Every sound but Simple Beep is format 1 on the sampled synthesizer: one
bufferCmd whose sound header holds 8-bit offset-binary samples and their rate.
Those samples are copied as they are.

Simple Beep is format 1 on the square-wave synthesizer: a timbreCmd, then an
ampCmd, a freqCmd with a MIDI note, and waitCmds and ampCmds that step the
amplitude down. There are no samples to copy, so it is rendered here at the
22254.54 Hz rate: the tone blends a sine into a square wave by timbre / 255,
the amplitude is ampCmd / 255 and changes at each ampCmd, and a waitCmd waits
param1 half-milliseconds. The square-wave synthesizer's own waveform table is
in ROM and is not reproduced, so the timbre is an approximation.

Needs: pip install machfs rsrcfork

AlertSounds.bin: u16 count, then per sound: u8 name length, MacRoman name,
u32 sample rate as 16.16 fixed point, u32 sample count, and that many u8
offset-binary samples.
"""

import argparse
import io
import math
import struct
from pathlib import Path

import machfs
import rsrcfork

ASSETS = Path(__file__).resolve().parents[2] / 'src/System7.Avalonia/Assets'
ORDER = ['Simple Beep', 'Droplet', 'Indigo', 'Quack', 'Sosumi', 'Wild Eep']
RATE_22K = 0x56EE8BA3

NULL_CMD, WAIT_CMD, AMP_CMD, FREQ_CMD, TIMBRE_CMD, BUFFER_CMD, SOUND_CMD = 0, 10, 43, 42, 44, 81, 80


def commands(data):
    fmt = struct.unpack('>H', data[:2])[0]
    if fmt != 1:
        raise SystemExit(f'Only format 1 snd resources are read, not format {fmt}.')
    count = struct.unpack('>H', data[2:4])[0]
    offset = 4
    synths = []
    for _ in range(count):
        synths.append(struct.unpack('>H', data[offset:offset + 2])[0])
        offset += 6
    count = struct.unpack('>H', data[offset:offset + 2])[0]
    offset += 2
    result = []
    for _ in range(count):
        cmd, param1, param2 = struct.unpack('>HhI', data[offset:offset + 8])
        result.append((cmd, param1, param2))
        offset += 8
    return synths, result


def sampled(data, cmds):
    for cmd, _, param2 in cmds:
        if cmd & 0x7FFF in (BUFFER_CMD, SOUND_CMD):
            header = param2
            _, length, rate, _, _, encode, _ = struct.unpack('>IIIIIBB', data[header:header + 22])
            if encode != 0:
                raise SystemExit(f'Sound header encoding {encode} is not a standard header.')
            return rate, data[header + 22:header + 22 + length]
    raise SystemExit('A sampled sound has no bufferCmd.')


def square_wave(cmds):
    rate = RATE_22K / 65536
    timbre, amplitude, frequency, phase = 0.0, 0.0, 0.0, 0.0
    samples = bytearray()
    for cmd, param1, param2 in cmds:
        if cmd == TIMBRE_CMD:
            timbre = param1 / 255
        elif cmd == AMP_CMD:
            amplitude = param1 / 255
        elif cmd == FREQ_CMD:
            frequency = 440 * 2 ** (((param2 & 0x7F) - 69) / 12)
        elif cmd == WAIT_CMD:
            for _ in range(round(param1 / 2000 * rate)):
                sine = math.sin(2 * math.pi * phase)
                square = 1.0 if phase < 0.5 else -1.0
                value = amplitude * ((1 - timbre) * sine + timbre * square)
                samples.append(max(0, min(255, round(128 + value * 127))))
                phase = (phase + frequency / rate) % 1.0
    return RATE_22K, bytes(samples)


def main():
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('volume', type=Path, help='an installed System 7.0.1 HFS volume')
    parser.add_argument('--out', type=Path, default=ASSETS / 'AlertSounds.bin')
    args = parser.parse_args()

    volume = machfs.Volume()
    volume.read(args.volume.read_bytes())
    system = rsrcfork.ResourceFile(io.BytesIO(volume['System Folder']['System'].rsrc))
    named = {res.name.decode('mac_roman'): res.data for res in system[b'snd '].values() if res.name}

    out = bytearray(struct.pack('>H', len(ORDER)))
    for name in ORDER:
        if name not in named:
            raise SystemExit(f'The System file has no snd named {name}.')
        synths, cmds = commands(named[name])
        rate, samples = square_wave(cmds) if synths == [1] else sampled(named[name], cmds)
        encoded = name.encode('mac_roman')
        out += struct.pack('>B', len(encoded)) + encoded + struct.pack('>II', rate, len(samples)) + samples
        print(f'{name}: {len(samples)} samples at {rate / 65536:.2f} Hz')
    args.out.write_bytes(out)
    print(f'wrote {args.out} ({len(out)} bytes)')


if __name__ == '__main__':
    main()
