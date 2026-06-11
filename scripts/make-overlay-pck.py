#!/usr/bin/env python3
"""Build port_compat.pck — Mali-friendly shader overlay pack."""
import hashlib
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC_SHADERS = ROOT / "shaders" / "mobile_compat"
OUT_PCK = ROOT / "linux" / "build" / "port_compat.pck"

MAGIC = 0x43504447  # GDPC
FORMAT_VERSION = 3
GODOT_MAJOR, GODOT_MINOR, GODOT_PATCH = 4, 5, 1
PACK_REL_FILEBASE = 0x02
ALIGNMENT = 32
HEADER_SIZE = 4 + 4 + 4 + 4 + 4 + 4 + 8 + 8 + (16 * 4)


def align(offset, alignment=ALIGNMENT):
    return (offset + alignment - 1) & ~(alignment - 1)


def pad_string(path):
    data = path.encode("utf-8")
    padded_len = len(data) + ((4 - len(data) % 4) % 4)
    return padded_len, data + b"\0" * (padded_len - len(data))


def collect_files():
    if not SRC_SHADERS.is_dir():
        sys.exit(f"shaders dir missing: {SRC_SHADERS}")
    entries = []
    for path in sorted(SRC_SHADERS.iterdir()):
        if not path.is_file() or not path.name.endswith(".gdshader"):
            continue
        data = path.read_bytes()
        res_path = f"res://shaders/mobile_compat/{path.name}"
        entries.append((res_path, data, hashlib.md5(data).digest()))
    if not entries:
        sys.exit(f"no .gdshader files under {SRC_SHADERS}")
    return entries


def main():
    entries = collect_files()

    file_base = align(HEADER_SIZE)
    file_offsets = []
    cursor = file_base
    for _, data, _ in entries:
        file_offsets.append(cursor - file_base)
        cursor += len(data)
        cursor = align(cursor)
    dir_base = align(cursor)

    dir_section = bytearray()
    dir_section += struct.pack("<I", len(entries))
    for (res_path, data, md5), rel_offset in zip(entries, file_offsets):
        padded_len, padded_path = pad_string(res_path)
        dir_section += struct.pack("<I", padded_len)
        dir_section += padded_path
        dir_section += struct.pack("<Q", rel_offset)
        dir_section += struct.pack("<Q", len(data))
        dir_section += md5
        dir_section += struct.pack("<I", 0)

    header = bytearray()
    header += struct.pack("<I", MAGIC)
    header += struct.pack("<I", FORMAT_VERSION)
    header += struct.pack("<I", GODOT_MAJOR)
    header += struct.pack("<I", GODOT_MINOR)
    header += struct.pack("<I", GODOT_PATCH)
    header += struct.pack("<I", PACK_REL_FILEBASE)
    header += struct.pack("<Q", file_base)
    header += struct.pack("<Q", dir_base)
    header += b"\0" * (16 * 4)
    assert len(header) == HEADER_SIZE

    OUT_PCK.parent.mkdir(parents=True, exist_ok=True)
    with OUT_PCK.open("wb") as fh:
        fh.write(header)
        fh.write(b"\0" * (file_base - HEADER_SIZE))
        cursor = file_base
        for (_, data, _), rel_offset in zip(entries, file_offsets):
            absolute_offset = file_base + rel_offset
            if cursor < absolute_offset:
                fh.write(b"\0" * (absolute_offset - cursor))
                cursor = absolute_offset
            fh.write(data)
            cursor += len(data)
            aligned = align(cursor)
            if cursor < aligned:
                fh.write(b"\0" * (aligned - cursor))
                cursor = aligned
        if cursor < dir_base:
            fh.write(b"\0" * (dir_base - cursor))
        fh.write(dir_section)

    print(f"Wrote {OUT_PCK} ({OUT_PCK.stat().st_size} bytes, {len(entries)} shaders)")


if __name__ == "__main__":
    main()
