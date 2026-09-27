# UI 界面资源

## 存放内容

这里存放所有 UI 资源。

- `Button/`：商店、事件、日志共用的按钮四态。
- `Panel/`：通用大面板、说明面板。
- `Shop/`：商店框架、商品卡片与金币区域。
- `Event/`：事件框架、描述框与选项四态。
- `BattleLog/`：日志框架、分类按钮与日志条目。
- `Common/`：多个界面共享的标题、卡片和价格底板。
- `Scroll/`：Inventory、Shop、BattleLog、Developer、Codex 共用滚动条。
- `Decoration/`：固定尺寸角标与可平铺分隔线。
- `BattleSkin/`：战斗主界面专用 Skin。
- `Buttons/`：旧版按钮资源，保留给尚未迁移的界面。
- `Panels/`：旧版面板资源，保留给尚未迁移的界面。
- `Frames/`：边框、卡框、头像框。
- `Icons/`：通用 UI 图标。
- `Cursor/`：鼠标指针。

UI 资源必须保持高可读性，不要和战斗插画混放。

## 命名规范

```text
ui_button_blue.png
ui_button_disabled.png
ui_panel_common.png
ui_frame_rare.png
ui_icon_gold.png
ui_cursor_default.png
```

## 推荐尺寸

- 按钮九宫格源图：根据 UI 设计，一般 `128 x 64` 或更大。
- 图标：`64 x 64`、`128 x 128` 或 `256 x 256`。
- 面板：按实际 UI 设计，尽量支持九宫格或可缩放。

## 透明背景

按钮、边框、图标、鼠标指针必须透明背景。

面板可以不透明，也可以半透明，取决于 UI 设计。

## 引用系统

未来由以下系统引用：

- Godot Theme
- UI Controller
- `SpriteDatabase`
- `UIPresenter`
- Tooltip
- Inventory / Shop / Event / Battle UI

UI 资源不要放在卡牌或特效目录中。

## 拉伸规则

- `Shop/`、`Event/`、`BattleLog/`、`Panel/` 中的大面板由 `UIResourceDatabase` 以 NinePatch 方式使用。
- `Button/`、事件选项、商品卡片同样使用 NinePatch，文字长度变化不会拉坏四角。
- `Decoration/divider_tile.png` 只允许横向 Tile，不进行比例拉伸。
- `Decoration/corner_*.png` 为固定尺寸装饰，不参与拉伸。
- 所有动态文本、装备图标、事件图片和日志内容都由原系统提供，切片资源不得包含示例文字或虚构数据。
