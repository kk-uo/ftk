#!/usr/bin/env python3
"""生成 Shop、Event、BattleLog 共用的无文字科技风 UI 切片。"""

from __future__ import annotations

from pathlib import Path

from PIL import Image, ImageDraw, ImageEnhance


ROOT = Path("Assets/UI")
CYAN = (42, 201, 207, 255)
CYAN_DIM = (21, 102, 111, 235)
MAGENTA = (221, 43, 105, 255)
MAGENTA_DIM = (111, 25, 61, 235)
DARK = (5, 12, 18, 245)
DARKER = (2, 7, 11, 250)
GRAY = (61, 69, 78, 235)


def save(image: Image.Image, relative_path: str) -> None:
    path = ROOT / relative_path
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path, optimize=True)


def panel(size: tuple[int, int], primary=CYAN_DIM, secondary=MAGENTA_DIM, background=DARK, width=2) -> Image.Image:
    w, h = size
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    cut = min(16, w // 6, h // 6)
    shape = [(cut, 1), (w - cut - 1, 1), (w - 2, cut), (w - 2, h - cut - 1),
             (w - cut - 1, h - 2), (cut, h - 2), (1, h - cut - 1), (1, cut)]
    draw.polygon(shape, fill=background)
    draw.line(shape + [shape[0]], fill=GRAY, width=width)
    draw.line((cut + 4, 3, w // 2, 3), fill=primary, width=2)
    draw.line((w // 2 + 8, h - 4, w - cut - 5, h - 4), fill=secondary, width=2)
    draw.line((3, cut + 4, 3, min(h // 2, h - cut - 4)), fill=primary, width=1)
    draw.line((w - 4, max(h // 2, cut + 4), w - 4, h - cut - 5), fill=secondary, width=1)
    return image


def button(size: tuple[int, int], primary=CYAN_DIM, secondary=MAGENTA_DIM, background=DARK) -> Image.Image:
    image = panel(size, primary, secondary, background, 1)
    draw = ImageDraw.Draw(image)
    w, h = size
    draw.line((18, h // 2, w - 18, h // 2), fill=(19, 42, 49, 110), width=1)
    return image


def disabled(image: Image.Image) -> Image.Image:
    result = ImageEnhance.Color(image).enhance(0.08)
    result = ImageEnhance.Brightness(result).enhance(0.58)
    result.putalpha(170)
    return result


def scroll_track() -> Image.Image:
    image = Image.new("RGBA", (18, 96), DARKER)
    draw = ImageDraw.Draw(image)
    draw.rectangle((4, 1, 13, 94), fill=(9, 25, 31, 245), outline=CYAN_DIM, width=1)
    draw.line((15, 14, 15, 80), fill=MAGENTA_DIM, width=1)
    return image


def scroll_grabber(color: tuple[int, int, int, int]) -> Image.Image:
    image = Image.new("RGBA", (18, 48), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((4, 2, 13, 45), radius=3, fill=color)
    draw.line((6, 7, 11, 7), fill=(198, 248, 249, 230), width=1)
    draw.line((6, 40, 11, 40), fill=MAGENTA_DIM, width=1)
    return image


def decoration_corner(flip_x: bool, flip_y: bool) -> Image.Image:
    image = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.line((2, 25, 2, 9, 9, 2, 25, 2), fill=CYAN, width=2)
    draw.line((6, 29, 15, 29), fill=MAGENTA, width=2)
    if flip_x:
        image = image.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    if flip_y:
        image = image.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    return image


def main() -> None:
    # Common：跨 Shop / Event / BattleLog 共享，不在各目录重复保存。
    save(panel((160, 112)), "Common/common_panel.png")
    save(panel((256, 64), CYAN, MAGENTA_DIM, DARKER), "Common/common_header.png")
    save(panel((144, 144), CYAN_DIM, MAGENTA_DIM, DARKER), "Common/common_card_background.png")
    save(panel((144, 48), CYAN_DIM, MAGENTA_DIM, DARKER), "Common/common_price_panel.png")

    normal = button((160, 52))
    save(normal, "Button/common_button_normal.png")
    save(ImageEnhance.Brightness(normal).enhance(1.22), "Button/common_button_hover.png")
    save(button((160, 52), MAGENTA, CYAN_DIM, DARKER), "Button/common_button_pressed.png")
    save(disabled(normal), "Button/common_button_disabled.png")

    save(panel((192, 128)), "Panel/panel_primary.png")
    save(panel((192, 128), MAGENTA_DIM, CYAN_DIM, DARKER), "Panel/panel_secondary.png")
    save(panel((256, 144), CYAN_DIM, MAGENTA_DIM, DARKER), "Panel/panel_description.png")

    save(scroll_track(), "Scroll/scroll_track.png")
    save(scroll_grabber(CYAN_DIM), "Scroll/scroll_grabber.png")
    save(scroll_grabber(CYAN), "Scroll/scroll_grabber_hover.png")
    save(scroll_grabber(MAGENTA), "Scroll/scroll_grabber_pressed.png")

    divider = Image.new("RGBA", (128, 8), (0, 0, 0, 0))
    divider_draw = ImageDraw.Draw(divider)
    divider_draw.line((0, 2, 78, 2), fill=CYAN_DIM, width=1)
    divider_draw.line((86, 5, 127, 5), fill=MAGENTA_DIM, width=1)
    save(divider, "Decoration/divider_tile.png")
    save(decoration_corner(False, False), "Decoration/corner_top_left.png")
    save(decoration_corner(True, False), "Decoration/corner_top_right.png")
    save(decoration_corner(False, True), "Decoration/corner_bottom_left.png")
    save(decoration_corner(True, True), "Decoration/corner_bottom_right.png")

    save(panel((256, 176), CYAN, MAGENTA, DARKER, 2), "Shop/shop_frame.png")
    save(panel((144, 176), MAGENTA_DIM, CYAN_DIM, DARKER), "Shop/item_panel_normal.png")
    save(panel((144, 176), CYAN, MAGENTA, (7, 23, 29, 250), 2), "Shop/item_panel_hover.png")
    save(panel((144, 176), MAGENTA, CYAN, (18, 9, 18, 250), 2), "Shop/item_panel_selected.png")
    save(disabled(panel((144, 176))), "Shop/item_panel_disabled.png")
    save(panel((128, 48), CYAN_DIM, MAGENTA_DIM, DARKER), "Shop/gold_panel.png")

    save(panel((256, 176), CYAN, MAGENTA, DARKER, 2), "Event/event_frame.png")
    save(panel((256, 112), CYAN_DIM, MAGENTA_DIM, DARKER), "Event/description_panel.png")
    event_normal = button((256, 72), CYAN_DIM, MAGENTA_DIM, DARKER)
    save(event_normal, "Event/option_normal.png")
    save(button((256, 72), CYAN, MAGENTA, (7, 23, 29, 250)), "Event/option_hover.png")
    save(button((256, 72), MAGENTA, CYAN_DIM, (18, 9, 18, 250)), "Event/option_pressed.png")
    save(disabled(event_normal), "Event/option_disabled.png")
    save(button((256, 72), CYAN_DIM, GRAY, (5, 14, 17, 210)), "Event/option_completed.png")

    save(panel((256, 176), CYAN, MAGENTA, DARKER, 2), "BattleLog/battlelog_frame.png")
    save(panel((256, 72), CYAN_DIM, MAGENTA_DIM, DARKER), "BattleLog/log_entry_panel.png")
    save(button((160, 48), CYAN_DIM, MAGENTA_DIM, DARKER), "BattleLog/category_normal.png")
    save(button((160, 48), CYAN, MAGENTA, (7, 25, 31, 250)), "BattleLog/category_selected.png")


if __name__ == "__main__":
    main()
