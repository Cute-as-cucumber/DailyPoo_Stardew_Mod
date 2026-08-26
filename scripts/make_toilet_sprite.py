"""Render the toilet furniture sprite (16x32 px) to DailyPoo/assets/toilet.png.

Edit the PIXELS grid below and re-run to tweak the art:
    python3 scripts/make_toilet_sprite.py
"""

import struct
import zlib
from pathlib import Path

# fmt: off
PALETTE = {
    ".": None,                    # transparent
    "N": (9, 24, 75, 255),        # Joja navy outline
    "C": (26, 76, 171, 255),      # corporate cobalt accent
    "I": (108, 207, 239, 255),    # icy cyan highlight
    "W": (244, 250, 252, 255),    # porcelain white
    "w": (174, 207, 226, 255),    # cool porcelain shadow
    "D": (22, 38, 85, 255),       # bowl rim / hole
    "B": (28, 176, 232, 255),     # bright blue water
    "s": (0, 0, 0, 55),           # soft ground shadow
}

# 16 columns x 32 rows; front-facing toilet anchored at the bottom tile.
PIXELS = [
    "................",
    "................",
    "................",
    "....NNNNNNNN....",
    "...NCCCCCCCCN...",
    "..NCCCCCCCCCCN..",
    "..NCIIIIIIIICN..",
    "..NCIIWWWWIICN..",
    "..NCIIIIIIIICN..",
    "..NCCCCCCCCCCN..",
    "...NNNNNNNNNN...",
    ".....N....N.....",
    "....NNNNNNNN....",
    "...NWWWWWWWWN...",
    "..NWWNNNNNNWWN..",
    ".NWWNDDDDDDNWWN.",
    ".NWNDBBBBBBDNWN.",
    ".NWWNDDDDDDNWWN.",
    "..NWWNNNNNNWWN..",
    "...NWWWWWWWWN...",
    "....NNNNNNNN....",
    ".....NwwwwN.....",
    "....NwwWWwwN....",
    "....NwWWWWwN....",
    "...NwwWWWWwwN...",
    "...NwWWWWWWwN...",
    "...NwwWWWWwwN...",
    "....NwwwwwwN....",
    "....NNNNNNNN....",
    "...ss......ss...",
    "..ssssssssssss..",
    "................",
]
# fmt: on

WIDTH, HEIGHT = 16, 32


def build_png() -> bytes:
    assert len(PIXELS) == HEIGHT and all(len(row) == WIDTH for row in PIXELS)

    raw = b"".join(
        b"\x00" + b"".join(bytes(PALETTE[ch] or (0, 0, 0, 0)) for ch in row)
        for row in PIXELS
    )

    def chunk(tag: bytes, data: bytes) -> bytes:
        return (
            struct.pack(">I", len(data))
            + tag
            + data
            + struct.pack(">I", zlib.crc32(tag + data))
        )

    ihdr = struct.pack(">IIBBBBB", WIDTH, HEIGHT, 8, 6, 0, 0, 0)  # 8-bit RGBA
    return (
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", ihdr)
        + chunk(b"IDAT", zlib.compress(raw, 9))
        + chunk(b"IEND", b"")
    )


if __name__ == "__main__":
    out = Path(__file__).resolve().parent.parent / \
        "DailyPoo" / "assets" / "toilet.png"
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(build_png())
    print(f"wrote {out}")
