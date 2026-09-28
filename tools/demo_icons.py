"""Draws the demo's original icons and writes them in the Macintosh resource formats the demo loads."""
import math
import struct
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent.parent
DEMO_ASSETS = ROOT / 'samples/System7.Demo/Assets'
FONT = ROOT / 'src/System7.Avalonia/Assets/Chicago12.ttf'


def canvas(size):
    return Image.new('1', (size, size), 0)


def pack(image):
    width, height = image.size
    stride = (width + 7) // 8
    data = bytearray(stride * height)
    for y in range(height):
        for x in range(width):
            if image.getpixel((x, y)):
                data[y * stride + x // 8] |= 0x80 >> (x % 8)
    return bytes(data)


def pack_indexed(pixels, width, height, bits):
    stride = width * bits // 8
    data = bytearray(stride * height)
    for y in range(height):
        for x in range(width):
            shift = 8 - bits - (x * bits) % 8
            data[y * stride + x * bits // 8] |= pixels[y * width + x] << shift
    return bytes(data)


def star(size, inset=0):
    center = (size - 1) / 2
    outer = center - inset
    inner = outer * 0.42
    points = []
    for i in range(10):
        radius = outer if i % 2 == 0 else inner
        angle = -math.pi / 2 + i * math.pi / 5
        points.append((center + radius * math.cos(angle), center + 0.5 + radius * math.sin(angle)))
    return points


def star_icon(size):
    image = canvas(size)
    draw = ImageDraw.Draw(image)
    draw.polygon(star(size), fill=1)
    return image


def star_outline(size):
    image = canvas(size)
    ImageDraw.Draw(image).polygon(star(size), outline=1)
    return image


def color_star():
    size = 32
    mask = star_icon(size)
    fill = canvas(size)
    ImageDraw.Draw(fill).polygon(star(size, 2), fill=1)
    outline = star_outline(size)
    palette = [(0xFFFF, 0xFFFF, 0xFFFF), (0xFFFF, 0xCCCC, 0x0000), (0xFFFF, 0x6666, 0x0000), (0x0000, 0x0000, 0x0000)]
    pixels = []
    for y in range(size):
        for x in range(size):
            if outline.getpixel((x, y)):
                pixels.append(3)
            elif fill.getpixel((x, y)):
                pixels.append(1 if y < 18 else 2)
            else:
                pixels.append(0)
    header = struct.pack('>IHhhhhHHIIIHHHHIII', 0, 0x8000 | size, 0, 0, size, size, 0, 0, 0, 0x00480000, 0x00480000,
                         0, 8, 1, 8, 0, 0, 0)
    mask_map = struct.pack('>IHhhhh', 0, 4, 0, 0, size, size)
    icon_map = struct.pack('>IHhhhh', 0, 4, 0, 0, size, size)
    table = struct.pack('>IHH', 0, 0, len(palette) - 1) + b''.join(struct.pack('>HHHH', i, *rgb) for i, rgb in enumerate(palette))
    return header + mask_map + icon_map + struct.pack('>I', 0) + pack(mask) + pack(star_icon(size)) + table + bytes(pixels)


def gem_shapes():
    size = 32
    outline = [(15.5, 2), (28, 12), (15.5, 29), (3, 12)]
    mask = canvas(size)
    ImageDraw.Draw(mask).polygon(outline, fill=1)
    lines = canvas(size)
    draw = ImageDraw.Draw(lines)
    draw.polygon(outline, outline=1)
    draw.line([(3, 12), (28, 12)], fill=1)
    draw.line([(10, 7), (15.5, 12), (21, 7)], fill=1)
    draw.line([(15.5, 2), (15.5, 29)], fill=1)
    return mask, lines


def gem_family():
    mask, lines = gem_shapes()
    icon = pack(lines) + pack(mask)
    # icl4 uses the 16-color system table, icl8 the 256-color one.
    light4, dark4 = 7, 6
    light8, dark8 = 3 * 36 + 0 * 6 + 0, 5 * 36 + 5 * 6 + 1
    color4, color8 = [], []
    for y in range(32):
        for x in range(32):
            if lines.getpixel((x, y)):
                color4.append(15)
                color8.append(255)
            elif mask.getpixel((x, y)):
                light = (x < 16) != (y > 12)
                color4.append(light4 if light else dark4)
                color8.append(light8 if light else dark8)
            else:
                color4.append(0)
                color8.append(0)
    return icon, pack_indexed(color4, 32, 32, 4), bytes(color8)


def small_icons():
    icons = []

    def icon(paint):
        image = canvas(16)
        paint(ImageDraw.Draw(image))
        icons.append(pack(image))

    icon(lambda d: (d.polygon([(3, 1), (10, 1), (13, 4), (13, 14), (3, 14)], outline=1), d.line([(10, 1), (10, 4), (13, 4)], fill=1)))
    icon(lambda d: (d.rectangle([1, 4, 14, 13], outline=1), d.line([(1, 4), (3, 2), (7, 2), (8, 4)], fill=1)))
    icon(lambda d: (d.ellipse([1, 1, 14, 14], outline=1), d.line([(1, 7), (14, 7)], fill=1), d.ellipse([5, 1, 10, 14], outline=1)))
    icon(lambda d: (d.rectangle([2, 2, 13, 13], outline=1), d.rectangle([5, 5, 10, 10], fill=1)))
    icon(lambda d: d.polygon(star(16), fill=1))
    icon(lambda d: (d.polygon([(8, 1), (14, 4), (14, 11), (8, 14), (2, 11), (2, 4)], outline=1), d.line([(2, 4), (8, 7), (14, 4)], fill=1), d.line([(8, 7), (8, 14)], fill=1)))
    icon(lambda d: (d.rectangle([1, 4, 14, 13], fill=1), d.line([(1, 4), (3, 2), (7, 2), (8, 4)], fill=1)))
    icon(lambda d: (d.line([(2, 3), (13, 3)], fill=1), d.rectangle([6, 1, 9, 3], outline=1), d.rectangle([3, 3, 12, 14], outline=1),
                    d.line([(6, 5), (6, 12)], fill=1), d.line([(9, 5), (9, 12)], fill=1)))
    return b''.join(icons)


def new_folder_picture():
    image = Image.new('RGBA', (80, 20), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.fontmode = '1'
    draw.text((14, 3), 'New', font=ImageFont.truetype(str(FONT), 12), fill=(0, 0, 0, 255))
    draw.rectangle([52, 6, 67, 15], outline=(0, 0, 0, 255))
    draw.line([(52, 6), (54, 4), (58, 4), (59, 6)], fill=(0, 0, 0, 255))
    draw.line([(57, 10), (62, 10)], fill=(0, 0, 0, 255))
    draw.line([(59, 8), (59, 12)], fill=(0, 0, 0, 255))
    return image


def main():
    (DEMO_ASSETS / 'MenuLarge.bin').write_bytes(pack(star_icon(32)))
    (DEMO_ASSETS / 'MenuSmall.bin').write_bytes(pack(star_icon(16)))
    (DEMO_ASSETS / 'MenuColor.bin').write_bytes(color_star())
    icon, color4, color8 = gem_family()
    (DEMO_ASSETS / 'ListIcon.bin').write_bytes(icon)
    (DEMO_ASSETS / 'ListIcon4.bin').write_bytes(color4)
    (DEMO_ASSETS / 'ListIcon8.bin').write_bytes(color8)
    (DEMO_ASSETS / 'SmallListIcons.bin').write_bytes(small_icons())
    new_folder_picture().save(DEMO_ASSETS / 'NewFolder.png')


if __name__ == '__main__':
    main()
