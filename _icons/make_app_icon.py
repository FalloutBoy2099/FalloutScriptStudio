"""Builds the application icon from the two source pictures of this folder.

    icon.jpg     is used for the small frames (16, 24, 32, 48, 64 px),
    icon_hd.jpg  is used for the large frames (128, 256 px of the .ico and the 512 px PNG).

Output (relative to the repository root):
    FalloutScriptStudio/Assets/Icons/app.ico      frames 16, 24, 32, 48, 64, 128, 256 (PNG compressed)
    FalloutScriptStudio/Assets/Icons/app_64.png   the 64 px picture of the About window (WPF would pick any frame of an .ico)
    FalloutScriptStudio/Assets/Icons/app_512.png  the 512 px picture (an .ico cannot hold more than 256 px)

Usage: python _icons/make_app_icon.py   (needs Pillow)
"""
import io
import os
import struct

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
TARGET = os.path.join(ROOT, "FalloutScriptStudio", "Assets", "Icons")

SMALL = [16, 24, 32, 48, 64]
LARGE = [128, 256]
PNG_ONLY = 512


def resize(source: Image.Image, size: int) -> Image.Image:
    return source.convert("RGBA").resize((size, size), Image.LANCZOS)


def png_bytes(image: Image.Image) -> bytes:
    buffer = io.BytesIO()
    image.save(buffer, format="PNG", optimize=True)
    return buffer.getvalue()


def write_ico(path: str, frames: list) -> None:
    """Writes an .ico file whose frames are PNG pictures: [(size, png bytes), ...]."""
    header = struct.pack("<HHH", 0, 1, len(frames))
    offset = 6 + 16 * len(frames)
    entries = b""
    data = b""
    for size, content in frames:
        dimension = 0 if size >= 256 else size
        entries += struct.pack("<BBBBHHII", dimension, dimension, 0, 0, 1, 32, len(content), offset + len(data))
        data += content
    with open(path, "wb") as stream:
        stream.write(header + entries + data)


def main() -> None:
    small = Image.open(os.path.join(HERE, "icon.jpg"))
    large = Image.open(os.path.join(HERE, "icon_hd.jpg"))

    frames = [(size, png_bytes(resize(small, size))) for size in SMALL]
    frames += [(size, png_bytes(resize(large, size))) for size in LARGE]
    write_ico(os.path.join(TARGET, "app.ico"), frames)

    resize(small, 64).save(os.path.join(TARGET, "app_64.png"), format="PNG", optimize=True)
    resize(large, PNG_ONLY).save(os.path.join(TARGET, "app_512.png"), format="PNG", optimize=True)
    print("app.ico:", ", ".join(str(size) for size, _ in frames), "| app_64.png | app_512.png")


if __name__ == "__main__":
    main()
