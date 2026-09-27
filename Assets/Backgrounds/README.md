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

尽量按项目默认分辨率 `2560 x 1440` 制作，避免放大后模糊。

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
