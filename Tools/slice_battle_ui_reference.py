#!/usr/bin/env python3
"""从战斗 UI 参考图生成可供 Godot 使用的独立 Skin 纹理。"""

from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance


REFERENCE_SIZE = (1586, 992)


def ensure_dirs(root: Path) -> dict[str, Path]:
    names = (
        "Frame",
        "Button",
        "Panel",
        "Header",
        "HPBar",
        "ManaCircle",
        "Tooltip",
        "IconBackground",
        "Decoration",
    )
    result: dict[str, Path] = {}
    for name in names:
        path = root / name
        path.mkdir(parents=True, exist_ok=True)
        result[name] = path
    return result


def save_nearest(image: Image.Image, path: Path) -> None:
    image.save(path, optimize=True)


def build_frame_assets(existing_frame: Path, dirs: dict[str, Path]) -> None:
    source = Image.open(existing_frame).convert("RGBA")
    width, height = source.size
    left_right = 180
    top_bottom = 160

    # 只保留原 Skin 的四边和四角，中央完全透明，确保战斗背景不会被覆盖。
    overlay = Image.new("RGBA", source.size, (0, 0, 0, 0))
    overlay.alpha_composite(source.crop((0, 0, width, top_bottom)), (0, 0))
    overlay.alpha_composite(source.crop((0, height - top_bottom, width, height)), (0, height - top_bottom))
    overlay.alpha_composite(source.crop((0, top_bottom, left_right, height - top_bottom)), (0, top_bottom))
    overlay.alpha_composite(
        source.crop((width - left_right, top_bottom, width, height - top_bottom)),
        (width - left_right, top_bottom),
    )
    save_nearest(overlay, dirs["Frame"] / "battle_frame_overlay.png")

    pieces = {
        "corner_top_left.png": (0, 0, left_right, top_bottom),
        "corner_top_right.png": (width - left_right, 0, width, top_bottom),
        "corner_bottom_left.png": (0, height - top_bottom, left_right, height),
        "corner_bottom_right.png": (width - left_right, height - top_bottom, width, height),
        "edge_top.png": (left_right, 0, width - left_right, top_bottom),
        "edge_bottom.png": (left_right, height - top_bottom, width - left_right, height),
        "edge_left.png": (0, top_bottom, left_right, height - top_bottom),
        "edge_right.png": (width - left_right, top_bottom, width, height - top_bottom),
    }
    for filename, box in pieces.items():
        save_nearest(overlay.crop(box), dirs["Frame"] / filename)


def build_reference_assets(reference: Path, dirs: dict[str, Path]) -> None:
    source = Image.open(reference).convert("RGBA")
    if source.size != REFERENCE_SIZE:
        source = source.resize(REFERENCE_SIZE, Image.Resampling.NEAREST)

    # 空面板样例位于参考图左侧；内部没有文字，适合作为 NinePatch。
    panel = source.crop((27, 234, 231, 344))
    save_nearest(panel, dirs["Panel"] / "panel_frame.png")

    tooltip = panel.resize((272, 160), Image.Resampling.NEAREST)
    save_nearest(tooltip, dirs["Tooltip"] / "tooltip_frame.png")

    # 顶栏使用现有边框顶部纹理，避免把参考图中的示例文字烘焙进资源。
    header = panel.resize((512, 72), Image.Resampling.NEAREST)
    save_nearest(header, dirs["Header"] / "header_frame.png")

    # 参考按钮包含示例文字。保留四周像素装饰，并用采样到的深色中心覆盖文字。
    button = source.crop((1352, 114, 1462, 153))
    button_draw = ImageDraw.Draw(button)
    button_draw.rectangle((12, 8, button.width - 13, button.height - 9), fill=(7, 15, 20, 245))
    save_nearest(button, dirs["Button"] / "button_normal.png")

    hover = ImageEnhance.Brightness(button).enhance(1.22)
    hover = ImageEnhance.Color(hover).enhance(1.18)
    save_nearest(hover, dirs["Button"] / "button_hover.png")

    pressed = ImageEnhance.Brightness(button).enhance(0.78)
    pressed_draw = ImageDraw.Draw(pressed)
    pressed_draw.line((14, pressed.height - 7, pressed.width - 14, pressed.height - 7), fill=(224, 46, 112, 255), width=2)
    save_nearest(pressed, dirs["Button"] / "button_pressed.png")

    disabled = ImageEnhance.Color(button).enhance(0.08)
    disabled = ImageEnhance.Brightness(disabled).enhance(0.58)
    disabled.putalpha(170)
    save_nearest(disabled, dirs["Button"] / "button_disabled.png")

    # 血条拆为底图、填充和透明外框，三者可独立 NinePatch 拉伸。
    hp_size = (256, 32)
    hp_background = Image.new("RGBA", hp_size, (8, 16, 22, 235))
    draw = ImageDraw.Draw(hp_background)
    draw.rounded_rectangle((1, 1, 254, 30), radius=5, outline=(31, 80, 88, 230), width=2)
    save_nearest(hp_background, dirs["HPBar"] / "hp_background.png")

    hp_fill = Image.new("RGBA", hp_size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(hp_fill)
    draw.rounded_rectangle((1, 1, 254, 30), radius=5, fill=(232, 38, 48, 255))
    draw.line((7, 4, 248, 4), fill=(255, 82, 88, 210), width=1)
    save_nearest(hp_fill, dirs["HPBar"] / "hp_fill.png")

    hp_frame = Image.new("RGBA", hp_size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(hp_frame)
    draw.rounded_rectangle((0, 0, 255, 31), radius=6, outline=(70, 185, 190, 245), width=2)
    draw.line((4, 2, 38, 2), fill=(45, 225, 224, 255), width=1)
    draw.line((218, 29, 251, 29), fill=(224, 45, 112, 255), width=1)
    save_nearest(hp_frame, dirs["HPBar"] / "hp_frame.png")

    mana = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
    draw = ImageDraw.Draw(mana)
    draw.ellipse((3, 3, 60, 60), fill=(6, 15, 20, 230), outline=(74, 195, 204, 255), width=3)
    draw.arc((0, 0, 63, 63), 205, 305, fill=(222, 45, 112, 255), width=2)
    save_nearest(mana, dirs["ManaCircle"] / "mana_circle_frame.png")

    icon = mana.resize((48, 48), Image.Resampling.NEAREST)
    save_nearest(icon, dirs["IconBackground"] / "icon_frame.png")

    divider = source.crop((19, 927, 239, 947))
    save_nearest(divider, dirs["Decoration"] / "divider_tile.png")

    center_tile = source.crop((550, 420, 614, 484))
    save_nearest(center_tile, dirs["Frame"] / "center_tile.png")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("reference", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("existing_frame", type=Path)
    args = parser.parse_args()

    dirs = ensure_dirs(args.output)
    build_frame_assets(args.existing_frame, dirs)
    build_reference_assets(args.reference, dirs)


if __name__ == "__main__":
    main()
