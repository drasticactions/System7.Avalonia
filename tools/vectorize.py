import argparse
from pathlib import Path

from PIL import Image

THEME_ASSETS = Path(__file__).resolve().parent.parent / 'src/System7.Avalonia/Assets'


def path_data(mask):
    """Cover the set pixels of a row-major boolean mask with rectangles and return SVG path data."""
    height = len(mask)
    width = len(mask[0]) if height else 0
    runs = {}
    for y in range(height):
        x = 0
        while x < width:
            if not mask[y][x]:
                x += 1
                continue
            start = x
            while x < width and mask[y][x]:
                x += 1
            runs.setdefault(y, []).append((start, x))
    rects = []
    open_rects = {}
    for y in range(height + 1):
        row = set(runs.get(y, []))
        for run in list(open_rects):
            if run not in row:
                top = open_rects.pop(run)
                rects.append((run[0], top, run[1] - run[0], y - top))
        for run in row:
            open_rects.setdefault(run, y)
    rects.sort(key=lambda r: (r[1], r[0]))
    return ' '.join(f'M{x},{y}h{w}v{h}h-{w}z' for x, y, w, h in rects)


def bits(data, offset, width, height, stride):
    return [[bool(data[offset + y * stride + x // 8] & (128 >> (x % 8))) for x in range(width)] for y in range(height)]


def entry(key, mask):
    return f'  <StreamGeometry x:Key="{key}">{path_data(mask) or "M0,0"}</StreamGeometry>'


def image(key, width, height, layers):
    """A multi-color picture as a DrawingImage whose bounds are the whole picture, one GeometryDrawing per color."""
    lines = [f'  <DrawingImage x:Key="{key}">', '    <DrawingGroup>',
             f'      <GeometryDrawing Brush="Transparent" Geometry="M0,0h{width}v{height}h-{width}z" />']
    for color, layer in layers:
        lines.append(f'      <GeometryDrawing Brush="{color}" Geometry="{path_data(layer)}" />')
    lines += ['    </DrawingGroup>', '  </DrawingImage>']
    return '\n'.join(lines)


def speaker(assets):
    data = (assets / 'System7Speaker.bin').read_bytes()
    for frame in range(6):
        yield entry(f'System7SpeakerFrame{frame}Geometry', bits(data, frame * 128, 32, 32, 4))


def byte_mask(data, width, height):
    return [[bool(data[y * width + x]) for x in range(width)] for y in range(height)]


def sliders(assets):
    for name, sizes in [('Memory', {'Track': (144, 13), 'Thumb': (15, 18)}), ('Sound', {'Track': (24, 104), 'Thumb': (23, 11)}),
                        ('Network', {'Track': (100, 10), 'Thumb': (11, 18)})]:
        for part, (width, height) in sizes.items():
            data = (assets / f'{name}Slider{part}.bin').read_bytes()
            if len(data) != width * height:
                raise ValueError(f'{name}Slider{part}.bin has an unexpected size.')
            yield entry(f'System7{name}Slider{part}Geometry', byte_mask(data, width, height))


def scrollbars(assets):
    data = (assets / 'ScrollBarMonochrome.bin').read_bytes()
    names = ['Up', 'UpPressed', 'Down', 'DownPressed', 'Left', 'LeftPressed', 'Right', 'RightPressed', 'VerticalThumb', 'HorizontalThumb']
    for index, name in enumerate(names):
        yield entry(f'System7ScrollBar{name}Geometry', bits(data, index * 32, 16, 16, 2))
    color = (assets / 'ScrollBarColor.bin').read_bytes()
    word = lambda offset: int.from_bytes(color[offset:offset + 2], 'big')
    # CDEF 1 blends pairs of its colors into color-table entries 16 to 37.
    blends = ' '.join(f'{word(202 + i * 6)},{word(204 + i * 6)},{word(206 + i * 6)}' for i in range(22))
    yield f'  <x:String x:Key="System7ScrollBarBlendRecipes">{blends}</x:String>'
    offset = 334
    for index in range(10):
        length = int.from_bytes(color[offset:offset + 2], 'big')
        picture = color[offset + 2:offset + 2 + length]
        stride = int.from_bytes(picture[0:2], 'big') & 0x7fff
        height = int.from_bytes(picture[6:8], 'big')
        width = int.from_bytes(picture[8:10], 'big')
        yield entry(f'System7ColorScrollBarPicture{index}Geometry', bits(picture, 12, width, height, stride))
        offset += length + 2


def palette(path):
    """The 16-bit RGB entries of a native System 7 color table."""
    data = path.read_bytes()
    count = 16 if path.name == 'System7Color16.bin' else 256
    return [tuple(int.from_bytes(data[i * 6 + c * 2:i * 6 + c * 2 + 2], 'big') for c in range(3)) for i in range(count)]


def screen_color(rgb, depth):
    """A color-table entry as a screen of this depth shows it: 16-bit screens keep five bits per channel."""
    def expand(value):
        bits = value >> 11
        return (bits << 11) | (bits << 6) | (bits << 1) | (bits >> 4)
    return '#{:02X}{:02X}{:02X}'.format(*((expand(v) if depth == 16 else v) >> 8 for v in rgb))


def indexed(key, pixels, mask, table, depth):
    """A picture drawn through a color table on a screen of this depth, leaving out pixels outside its mask."""
    height, width = len(pixels), len(pixels[0])
    indices = sorted({pixels[y][x] for y in range(height) for x in range(width) if mask[y][x]})
    return image(key, width, height, [(screen_color(table[index], depth),
                                       [[mask[y][x] and pixels[y][x] == index for x in range(width)] for y in range(height)])
                                      for index in indices])


def menus(assets):
    data = (assets / 'MenuMonochrome.bin').read_bytes()
    for index, name in enumerate(['SubmenuArrow', 'ScrollDown', 'ScrollUp']):
        yield entry(f'System7Menu{name}Geometry', bits(data, index * 32, 16, 16, 2))
    # The color Apple menu title: a cicn with 4-bit and 8-bit members over a 1-bit mask.
    apple = (assets / 'MenuColor.bin').read_bytes()
    mask = bits(apple, 32, 16, 16, 2)
    small = palette(THEME_ASSETS / 'System7Color16.bin')
    large = palette(THEME_ASSETS / 'System7Color256.bin')
    # The 16-color screen draws the 4-bit member; deeper screens draw the 8-bit member.
    yield indexed('System7MenuApple4Image', [[apple[64 + y * 8 + x // 2] >> (4 if x % 2 == 0 else 0) & 15 for x in range(16)] for y in range(16)], mask, small, 4)
    for depth in [8, 16, 32]:
        yield indexed(f'System7MenuApple{depth}Image', [[apple[192 + y * 16 + x] for x in range(16)] for y in range(16)], mask, large, depth)


def windows(assets):
    data = (assets / 'WindowMonochrome.bin').read_bytes()
    for index, name in enumerate(['Zoom', 'Close', 'ZoomPressed', 'ClosePressed']):
        yield entry(f'System7Window{name}Geometry', bits(data, index * 22, 11, 11, 2))
    yield entry('System7WindowGrowGeometry', bits(data, 88, 15, 15, 2))
    rounded = (assets / 'RoundedWindowMonochrome.bin').read_bytes()
    def raw(offset, x, y):
        return bool(rounded[offset + y * 2 + x // 8] & (128 >> (x & 7)))
    for suffix, sample in [('', lambda offset, x, y: raw(offset, x, y)),
                           # The 8-bit Mac II trace inherits WDEF 0's (-1, -1) temporary bitmap origin; its last
                           # sample runs one row and one byte past the 16 by 16 image, into the bytes that follow.
                           ('Indexed8', lambda offset, x, y: raw(offset, ((2 * x + 1) * 17 + 1) // 32, (2 * y + 1) * 17 // 32))]:
        box = [[sample(0, x, y) for x in range(16)] for y in range(16)]
        toggle = [[sample(32, x, y) for x in range(16)] for y in range(16)]
        yield entry(f'System7RoundedWindowClose{suffix}Geometry', box)
        # Pressing the box XORs a second bitmap over it.
        yield entry(f'System7RoundedWindowClosePressed{suffix}Geometry', [[box[y][x] != toggle[y][x] for x in range(16)] for y in range(16)])
    color = (assets / 'WindowColor.bin').read_bytes()
    word = lambda offset: int.from_bytes(color[offset:offset + 2], 'big')
    # The normal box picture is one solid color, index 10; the pressed picture has a layer per color-table index.
    offset = 358
    stride = word(offset) & 0x7fff
    picture = [[color[offset + 12 + y * stride + x // 2] >> (4 if x % 2 == 0 else 0) & 15 for x in range(11)] for y in range(11)]
    for index in sorted({value for row in picture for value in row}):
        yield entry(f'System7ColorWindowBoxHighlight{index}Geometry', [[value == index for value in row] for row in picture])
    # WDEF 0 blends pairs of its window colors into color-table entries 16 to 36.
    blends = ' '.join(f'{word(116 + i * 6)},{word(118 + i * 6)},{word(120 + i * 6)}' for i in range(21))
    yield f'  <x:String x:Key="System7WindowBlendRecipes">{blends}</x:String>'


def colored(key, path):
    """A captured picture in its final colors."""
    picture = Image.open(path).convert('RGBA')
    width, height = picture.size
    pixels = [[picture.getpixel((x, y)) for x in range(width)] for y in range(height)]
    return image(key, width, height, [('#{:02X}{:02X}{:02X}'.format(*color[:3]), [[p == color for p in row] for row in pixels])
                                      for color in sorted({p for row in pixels for p in row if p[3] == 255})])


def speech(assets):
    # Speech control panel icm# -4045, first 24 bytes of its 16 by 12 image.
    icon = bytes.fromhex('0110030807087F247F147F547F147F240708030801100000')
    yield entry('System7SpeechSpeakerIconGeometry', bits(icon, 0, 16, 12, 2))
    # The speech rate slider, captured from the Speech control panel at each depth.
    for depth in ['Monochrome', 'Indexed2', 'Indexed4', 'Indexed8']:
        for part in ['Track', 'Thumb']:
            yield colored(f'System7SpeechSlider{depth}{part}Image', assets / f'SpeechSlider{depth}{part}.png')


def media(assets):
    for name in ['Play', 'Pause', 'Stop', 'Record']:
        yield colored(f'System7Media{name}Image', assets / f'System7Media{name}.png')


GROUPS = [speaker, speech, media, sliders, scrollbars, menus, windows]


def main():
    parser = argparse.ArgumentParser(description='Write the System 7 artwork as XAML geometry and drawing resources.')
    parser.add_argument('assets', type=Path)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    lines = ['<!-- Generated by tools/vectorize.py from tools/art: one unit per pixel. -->',
             '<ResourceDictionary xmlns="https://github.com/avaloniaui"',
             '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">']
    for group in GROUPS:
        lines.extend(group(args.assets))
    lines.append('</ResourceDictionary>')
    args.output.write_text('\n'.join(lines) + '\n')


if __name__ == '__main__':
    main()
