# Events 事件资源

## 存放内容

这里存放事件界面使用的图片。

- `Backgrounds/`：事件背景图。
- `Characters/`：事件中出现的人物、怪物、物件立绘。

事件资源与战斗资源分离，是为了让事件演出可以使用更宽、更叙事化的图片比例。

## 命名规范

```text
event_bg_chibi_wreck.png
event_bg_imperial_temple.png
event_char_fairy.png
event_char_boatman.png
```

## 推荐尺寸

- 事件背景：`1920 x 1080` 或 `2560 x 1440`
- 事件角色：`1024 x 1536`

## 透明背景

事件背景不需要透明背景。

事件角色、物件立绘建议使用透明背景 PNG。

## 引用系统

未来由以下系统引用：

- 事件 UI
- `SpriteDatabase`
- `PresentationManager`
- 剧情演出系统

事件逻辑只决定进入哪个事件，不直接加载图片。
