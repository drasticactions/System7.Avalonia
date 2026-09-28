import argparse
from collections import Counter
from pathlib import Path

from PIL import Image


TARGETS = (20, 100, 200, 300, 400)
VALUES = (20, 92, 191, 294, 392)
BOUNDS = (218, 126, 342, 155)


def main():
    parser = argparse.ArgumentParser(description='Extract the original Speech slider track and thumb.')
    parser.add_argument('--frames', type=Path, required=True)
    parser.add_argument('--prefix', type=Path, required=True)
    parser.add_argument('--mono', action='store_true')
    args = parser.parse_args()
    extension = 'pbm' if args.mono else 'ppm'
    frames = [Image.open(args.frames / f'slider-target-{target}-released.{extension}').convert('RGB')
              for target in TARGETS]
    anchors = [222 + round((value - 20) * 100 / 380) for value in VALUES]
    left, top, right, bottom = BOUNDS
    background = {(x, y): Counter(frame.getpixel((x, y)) for frame in frames).most_common(1)[0][0]
                  for y in range(top, bottom) for x in range(left, right)}
    thumb = {}
    for y in range(128, 144):
        for dx in range(15):
            colors = {frame.getpixel((anchor + dx, y)) for anchor, frame in zip(anchors, frames)
                      if frame.getpixel((anchor + dx, y)) != background[anchor + dx, y]}
            if len(colors) > 1:
                raise AssertionError(f'The Speech thumb changes color at {dx}, {y}.')
            if colors:
                thumb[dx, y] = colors.pop()
    for anchor, frame in zip(anchors, frames):
        for y in range(top, bottom):
            for x in range(left, right):
                if thumb.get((x - anchor, y), background[x, y]) != frame.getpixel((x, y)):
                    raise AssertionError(f'The slider sprites differ from the native frame at {x}, {y}.')

    track_image = Image.new('RGB', (right - left, bottom - top))
    for (x, y), color in background.items():
        track_image.putpixel((x - left, y - top), color)
    thumb_image = Image.new('RGBA', (15, 16))
    for (dx, y), color in thumb.items():
        thumb_image.putpixel((dx, y - 128), (*color, 255))
    track_image.save(str(args.prefix) + 'Track.png')
    thumb_image.save(str(args.prefix) + 'Thumb.png')
    print(f'Extracted {len(thumb)} native thumb pixels from five verified frames.')


if __name__ == '__main__':
    main()
