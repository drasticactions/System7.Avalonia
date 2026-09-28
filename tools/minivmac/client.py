import argparse
from array import array
import base64
from functools import lru_cache
import json
from pathlib import Path
import socket
import sys
import time


def frame_data(reply):
    data = base64.b64decode(reply['data'], validate=True)
    width, height, stride = reply['width'], reply['height'], reply['stride']
    depth = reply.get('depth', 1)
    if width <= 0 or height <= 0 or depth not in (1, 2, 4, 8, 16, 32):
        raise ValueError('The framebuffer dimensions or pixel depth are invalid.')
    if stride != (width * depth + 7) // 8 or len(data) != stride * height:
        raise ValueError('The framebuffer length or row stride is invalid.')
    return data


@lru_cache(maxsize=1)
def rgb555_colors():
    levels = tuple((value << 3) | (value >> 2) for value in range(32))
    return tuple(bytes((levels[(value >> 10) & 31], levels[(value >> 5) & 31], levels[value & 31]))
                 for value in range(65536))


def frame_rgb(reply):
    data = frame_data(reply)
    width, height, stride = reply['width'], reply['height'], reply['stride']
    depth = reply.get('depth', 1)
    format = reply['format']
    if format == 'mono-msb-black1' and depth == 1:
        palette = (b'\xff\xff\xff', b'\0\0\0')
    elif format == 'indexed-msb' and depth in (2, 4, 8):
        colors = reply['palette']
        if len(colors) != 1 << depth or any(len(color) != 3 or any(not isinstance(c, int) or not 0 <= c <= 65535 for c in color) for color in colors):
            raise ValueError('The framebuffer palette is invalid.')
        palette = tuple(bytes(component >> 8 for component in color) for color in colors)
    elif format == 'rgb555-be' and depth == 16 or format == 'xrgb8888-be' and depth == 32:
        palette = None
    else:
        raise ValueError('The framebuffer format does not match its pixel depth.')
    if palette is not None and width * depth % 8 == 0:
        mask = (1 << depth) - 1
        chunks = palette if depth == 8 else tuple(
            b''.join(palette[(value >> shift) & mask] for shift in range(8 - depth, -1, -depth))
            for value in range(256))
        return b''.join(chunks[value] for value in data)
    if depth == 16:
        words = array('H', data)
        if sys.byteorder == 'little':
            words.byteswap()
        colors = rgb555_colors()
        return b''.join(colors[value] for value in words)
    result = bytearray(width * height * 3)
    if depth == 32:
        result[0::3] = data[1::4]
        result[1::3] = data[2::4]
        result[2::3] = data[3::4]
        return bytes(result)
    for y in range(height):
        for x in range(width):
            target = (y * width + x) * 3
            bit = x * depth
            value = (data[y * stride + bit // 8] >> (8 - depth - bit % 8)) & ((1 << depth) - 1)
            result[target:target + 3] = palette[value]
    return bytes(result)


class MiniVMac:
    def __init__(self, port=6807):
        self.socket = socket.create_connection(("127.0.0.1", port), timeout=5)
        self.stream = self.socket.makefile("rwb")

    def __enter__(self):
        return self

    def __exit__(self, *args):
        self.stream.close()
        self.socket.close()

    def command(self, command, **arguments):
        self.stream.write(json.dumps(dict(command=command, **arguments)).encode() + b"\n")
        self.stream.flush()
        line = self.stream.readline(4 * 1024 * 1024)
        if not line.endswith(b"\n"):
            raise ConnectionError("The emulator closed the connection or exceeded the reply limit.")
        reply = json.loads(line)
        if not reply.get("ok"):
            raise RuntimeError(reply.get("error", "The emulator rejected the command."))
        return reply

    def wait(self, seconds):
        time.sleep(seconds)

    def mouse(self, x, y, down=False):
        return self.command("mouse", x=x, y=y, down=int(down))

    def click(self, x, y):
        self.mouse(x, y, True)
        self.wait(0.1)
        self.mouse(x, y, False)
        self.wait(0.15)

    def key(self, code, down):
        return self.command("key", code=code, down=int(down))

    def press(self, code):
        self.key(code, True)
        self.wait(0.1)
        self.key(code, False)
        self.wait(0.15)

    def frame(self, path):
        reply = self.command("frame")
        data = frame_data(reply)
        if reply['format'] == 'mono-msb-black1':
            Path(path).write_bytes(f'P4\n{reply["width"]} {reply["height"]}\n'.encode() + data)
        else:
            Path(path).write_bytes(f'P6\n{reply["width"]} {reply["height"]}\n255\n'.encode() + frame_rgb(reply))
        return reply


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Send JSON commands to Mini vMac over localhost TCP.")
    parser.add_argument("request", nargs="?", default='{"command":"status"}')
    parser.add_argument("--port", type=int, default=6807)
    parser.add_argument("--frame", type=Path, help="Save the native framebuffer as PBM (monochrome) or PPM (color).")
    args = parser.parse_args()
    with MiniVMac(args.port) as mac:
        if args.frame:
            reply = mac.frame(args.frame)
            del reply["data"]
        else:
            request = json.loads(args.request)
            reply = mac.command(**request)
        print(json.dumps(reply))
