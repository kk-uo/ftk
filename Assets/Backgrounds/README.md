# Backgrounds 背景资源

## 存放内容

这里存放大场景背景。

- `Battle/`：战斗背景。
- `Map/`：地图背景。

背景资源通常不透明，负责提供章节氛围和场景识别。

## 命名规范

```text
bg_battle_chapter1.png
bg_battle_palace.png
bg_map_chapter1.png
bg_chibi.png
```

## 推荐尺寸

- 标准背景：`2560 x 1440`
- 兼容背景：`1920 x 1080`
- 像素地图背景：`1280 x 720`，运行时使用 Nearest 整数放大至 `2560 x 1440`

尽量按项目默认分辨率 `2560 x 1440` 制作，避免放大后模糊。
对于像素地图，可以使用 `1280 x 720` 控制纹理内存，但必须保持 16:9，
并在表现节点上启用 Nearest 过滤。

## 章节地图背景

- `Map/bg_map_chapter1_ruined_frontier.png`：第一章荒城边境。
- `Map/bg_map_chapter2_city_route.png`：第二章城市路线。
- `Map/bg_map_chapter2_sewer_route.png`：第二章下水道路线。
- `Map/bg_map_chapter3_imperial_route.png`：第三章皇宫路线。
- `Map/bg_map_chapter4_abyss_route.png`：第四章废土星环/深渊裂隙。

以上像素地图均为 `1280 x 720`、不透明、无内嵌节点或 UI 的独立背景，
由 `MapExplorationView` 按章节与路线选择，并以 Nearest 精确放大两倍。

## 透明背景

背景不需要透明背景。

如果背景需要叠加前景雾效、光效、装饰层，应拆到 `Effects/` 或对应 UI 目录。

## 引用系统

未来由以下系统引用：

- 地图 UI
- 战斗 UI
- 事件 UI
- `SpriteDatabase`
- 章节演出系统
