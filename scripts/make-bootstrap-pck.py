#!/usr/bin/env python3
"""Build bootstrap.pck — launcher UI (GDScript)."""

import hashlib
import os
import struct

MAGIC = 0x43504447  # "GDPC"
FORMAT_VERSION = 3
GODOT_MAJOR = 4
GODOT_MINOR = 5
GODOT_PATCH = 1
PACK_REL_FILEBASE = 0x02
ALIGNMENT = 32
HEADER_SIZE = 4 + 4 + 4 + 4 + 4 + 4 + 8 + 8 + (16 * 4)  # 104 bytes


PROJECT_GODOT = """\
; STS2 Linux Launcher — GDScript bootstrap.
config_version=5

[application]

config/name="STS2 Linux Launcher"
config/features=PackedStringArray("4.5", "Forward Plus")
run/main_scene="res://bootstrap.tscn"

[display]

window/size/viewport_width=1280
window/size/viewport_height=720
window/stretch/mode="disabled"
window/stretch/aspect="ignore"
window/handheld/orientation=0
window/dpi/allow_hidpi=false

[rendering]

; Bright red background so we can confirm at a glance that godot read this
; project.godot and is running with our settings — if the screen is red,
; godot is alive and rendering; if it's still grey/black, our pck isn't
; being loaded as the active project.
environment/defaults/default_clear_color=Color(0.85, 0.1, 0.1, 1.0)
"""

BOOTSTRAP_TSCN = """\
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://launcher_ui.gd" id="1"]

[node name="LauncherUI" type="Control"]
script = ExtResource("1")
anchor_right = 1.0
anchor_bottom = 1.0
"""


def align(offset, alignment=ALIGNMENT):
    return (offset + alignment - 1) & ~(alignment - 1)


def pad_string_len(s):
    encoded = s.encode("utf-8")
    padded = len(encoded) + ((4 - len(encoded) % 4) % 4)
    return padded, encoded


def build_dir_entry(path, data, data_offset_relative):
    padded_len, path_bytes = pad_string_len(path)
    entry = struct.pack("<I", padded_len)
    entry += path_bytes + b"\x00" * (padded_len - len(path_bytes))
    entry += struct.pack("<Q", data_offset_relative)
    entry += struct.pack("<Q", len(data))
    entry += hashlib.md5(data).digest()
    entry += struct.pack("<I", 0)  # flags
    return entry


def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    output = os.path.join(script_dir, "..", "linux", "build", "bootstrap.pck")
    ui_gd_path = os.path.join(script_dir, "..", "linux", "launcher_ui.gd")
    bg_png_path = os.path.join(script_dir, "..", "linux", "assets", "launcher_bg.png")
    zh_font_path = os.path.join(script_dir, "..", "linux", "assets", "launcher_font_zh.ttf")

    with open(ui_gd_path, "rb") as f:
        ui_gd_bytes = f.read()

    files = [
        ("res://project.godot",   PROJECT_GODOT.encode("utf-8")),
        ("res://bootstrap.tscn",  BOOTSTRAP_TSCN.encode("utf-8")),
        ("res://launcher_ui.gd",  ui_gd_bytes),
    ]

    if os.path.exists(bg_png_path):
        with open(bg_png_path, "rb") as f:
            files.append(("res://launcher_bg.png", f.read()))
    if os.path.exists(zh_font_path):
        with open(zh_font_path, "rb") as f:
            files.append(("res://launcher_font_zh.ttf", f.read()))

    file_base = align(HEADER_SIZE)
    entries = []
    file_blob = bytearray()
    cursor = 0
    for path, data in files:
        padding = align(cursor) - cursor
        file_blob.extend(b"\x00" * padding)
        cursor += padding
        offset = cursor
        file_blob.extend(data)
        cursor += len(data)
        entries.append(build_dir_entry(path, data, offset))

    file_end = file_base + len(file_blob)
    dir_base = align(file_end)

    dir_section = struct.pack("<I", len(files))
    for e in entries:
        dir_section += e

    header = struct.pack("<I", MAGIC)
    header += struct.pack("<I", FORMAT_VERSION)
    header += struct.pack("<I", GODOT_MAJOR)
    header += struct.pack("<I", GODOT_MINOR)
    header += struct.pack("<I", GODOT_PATCH)
    header += struct.pack("<I", PACK_REL_FILEBASE)
    header += struct.pack("<Q", file_base)
    header += struct.pack("<Q", dir_base)
    header += b"\x00" * (16 * 4)
    assert len(header) == HEADER_SIZE

    os.makedirs(os.path.dirname(output), exist_ok=True)
    with open(output, "wb") as f:
        f.write(header)
        f.write(b"\x00" * (file_base - HEADER_SIZE))
        f.write(file_blob)
        f.write(b"\x00" * (dir_base - file_end))
        f.write(dir_section)

    size = os.path.getsize(output)
    print(f"Created bootstrap PCK: {output} ({size} bytes)")
    for path, data in files:
        print(f"    - {path}: {len(data)} B")


if __name__ == "__main__":
    main()
