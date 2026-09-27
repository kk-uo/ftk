# Battle UI Skin

本目录保存从战斗 UI 参考图自动拆分出的表现层资源。运行时只通过项目内的
`res://Assets/UI/BattleSkin/` 路径加载，不依赖下载目录或其它本地绝对路径。

## 资源分类

- `Frame/`：整体边框、四角、四边与中心纹理。`battle_frame_overlay.png` 的中央区域透明。
- `Button/`：按钮 Normal、Hover、Pressed、Disabled 四种状态。
- `Panel/`：状态面板九宫格。
- `Header/`：顶部导航九宫格。
- `HPBar/`：血条底图、填充与独立外框。
- `ManaCircle/`：费用圆环外框。
- `Tooltip/`：Tooltip、技能说明与日志面板九宫格。
- `IconBackground/`：Buff、费用和状态图标的外框底图。
- `Decoration/`：可横向平铺的青色/品红扫描线。

## 拉伸规则

- `battle_frame_overlay.png` 使用 `NinePatchRect`，四角固定，四边采用 Tile Fit。
- Button、Panel、Header、HPBar、Tooltip 使用 `StyleBoxTexture` 九宫格。
- `Decoration/divider_tile.png` 仅允许横向 Tile，不按宽度直接拉伸。
- 四角与费用圆环保持等比例，不参与容器拉伸。

## 重新切片

切片脚本位于 `Tools/slice_battle_ui_reference.py`。脚本只处理美术资源，不改动
Scene、C# 或战斗逻辑。参考图变化后可以重新运行脚本生成同名资源，现有 UI 无需
修改引用。
