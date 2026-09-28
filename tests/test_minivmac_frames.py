import base64
from pathlib import Path
import random
import struct
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools' / 'minivmac'))
from client import frame_rgb


def reply(data, width, height, depth):
    return dict(data=base64.b64encode(data).decode(), width=width, height=height,
                stride=(width * depth + 7) // 8, depth=depth,
                format={1: 'mono-msb-black1', 2: 'indexed-msb', 4: 'indexed-msb',
                        8: 'indexed-msb', 16: 'rgb555-be', 32: 'xrgb8888-be'}[depth])


def palette(depth):
    return [[(index * 997 + channel * 13107) & 65535 for channel in range(3)]
            for index in range(1 << depth)]


class FrameDecoding(unittest.TestCase):
    def test_every_rgb555_word(self):
        data = b''.join(struct.pack('>H', value) for value in range(65536))
        expected = bytearray()
        for value in range(65536):
            for shift in (10, 5, 0):
                channel = (value >> shift) & 31
                expected.append((channel << 3) | (channel >> 2))
        self.assertEqual(frame_rgb(reply(data, 256, 256, 16)), expected)

    def test_rgb888_ignores_the_unused_byte(self):
        data = random.Random(701).randbytes(640 * 480 * 4)
        expected = b''.join(data[offset + 1:offset + 4] for offset in range(0, len(data), 4))
        self.assertEqual(frame_rgb(reply(data, 640, 480, 32)), expected)

    def test_every_indexed_byte(self):
        for depth in (1, 2, 4, 8):
            with self.subTest(depth=depth):
                self.compare_indexed(bytes(range(256)), 256 * 8 // depth, 1, depth)

    def test_padded_rows(self):
        for depth in (1, 2, 4, 8):
            for width in (1, 3, 5, 7, 9, 17):
                with self.subTest(depth=depth, width=width):
                    data = bytes((index * 37 + 19) & 255 for index in range((width * depth + 7) // 8 * 3))
                    self.compare_indexed(data, width, 3, depth)

    def test_invalid_palette(self):
        frame = reply(bytes(4), 4, 1, 8)
        frame['palette'] = palette(8)[:-1]
        with self.assertRaises(ValueError):
            frame_rgb(frame)

    def test_invalid_layout(self):
        frame = reply(bytes(8), 4, 1, 16)
        frame['stride'] = 4
        with self.assertRaises(ValueError):
            frame_rgb(frame)
        frame = reply(bytes(8), 4, 1, 16)
        frame['format'] = 'indexed-msb'
        with self.assertRaises(ValueError):
            frame_rgb(frame)

    def compare_indexed(self, data, width, height, depth):
        frame = reply(data, width, height, depth)
        frame['palette'] = palette(depth)
        colors = [b'\xff\xff\xff', b'\0\0\0'] if depth == 1 else [
            bytes(component >> 8 for component in color) for color in frame['palette']]
        expected = bytearray()
        for y in range(height):
            row = data[y * frame['stride']:(y + 1) * frame['stride']]
            bits = ''.join(f'{value:08b}' for value in row)
            for x in range(width):
                expected.extend(colors[int(bits[x * depth:(x + 1) * depth], 2)])
        self.assertEqual(frame_rgb(frame), expected)


if __name__ == '__main__':
    unittest.main()
