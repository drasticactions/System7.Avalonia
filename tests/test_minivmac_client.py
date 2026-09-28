import base64
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools' / 'minivmac'))
from client import frame_data, frame_rgb


def frame(data, width, height, depth, format, palette=None):
    reply = dict(data=base64.b64encode(data).decode(), width=width, height=height,
                 depth=depth, stride=(width * depth + 7) // 8, format=format)
    if palette is not None:
        reply['palette'] = palette
    return reply


class FrameTests(unittest.TestCase):
    def test_monochrome_rows_and_padding(self):
        reply = frame(bytes([0xa0, 0x40]), 3, 2, 1, 'mono-msb-black1')
        self.assertEqual(frame_rgb(reply), bytes([0, 0, 0, 255, 255, 255, 0, 0, 0,
                                                  255, 255, 255, 0, 0, 0, 255, 255, 255]))
        del reply['depth']
        self.assertEqual(frame_data(reply), bytes([0xa0, 0x40]))

    def test_indexed_depths_and_palette_changes(self):
        for depth, data in [(2, bytes([0x1b])), (4, bytes([0x01, 0x2f])), (8, bytes([0, 1, 2, 255]))]:
            with self.subTest(depth=depth):
                palette = [[0, 0, 0] for _ in range(1 << depth)]
                palette[0] = [65535, 65535, 65535]
                palette[1] = [65535, 0, 0]
                palette[2] = [0, 0x8080, 0xffff]
                reply = frame(data, 4, 1, depth, 'indexed-msb', palette)
                self.assertEqual(frame_rgb(reply), bytes([255, 255, 255, 255, 0, 0, 0, 128, 255, 0, 0, 0]))
                palette[1] = [0, 65535, 0]
                self.assertEqual(frame_rgb(reply)[3:6], bytes([0, 255, 0]))

    def test_rgb555(self):
        reply = frame(bytes.fromhex('7fff7c0003e0001f000042108000'), 7, 1, 16, 'rgb555-be')
        self.assertEqual(frame_rgb(reply), bytes([255, 255, 255, 255, 0, 0, 0, 255, 0, 0, 0, 255,
                                                  0, 0, 0, 132, 132, 132, 0, 0, 0]))

    def test_xrgb8888(self):
        reply = frame(bytes.fromhex('00ff008099204060'), 2, 1, 32, 'xrgb8888-be')
        self.assertEqual(frame_rgb(reply), bytes.fromhex('ff0080204060'))

    def test_invalid_frames(self):
        valid = frame(bytes([0]), 8, 1, 1, 'mono-msb-black1')
        for change in [dict(width=0), dict(depth=3), dict(stride=2), dict(data=''), dict(format='rgb555-be')]:
            with self.subTest(change=change), self.assertRaises(ValueError):
                frame_rgb(valid | change)
        with self.assertRaises(ValueError):
            frame_rgb(frame(bytes([0]), 4, 1, 2, 'indexed-msb', [[0, 0, 0]]))
        with self.assertRaises(ValueError):
            frame_rgb(frame(bytes([0]), 4, 1, 2, 'indexed-msb', [[0, 0, 65536]] * 4))


if __name__ == '__main__':
    unittest.main()
