# Cards 卡牌资源

## 存放内容

这里存放卡牌相关图片。

- `Artwork/`：卡牌完整插画。
- `Icons/`：卡牌小图标、类型图标、费用图标。

卡牌规则由代码和数据定义，卡牌图片只负责显示。

## 命名规范

```text
card_slay.png
card_fire_slay.png
card_shadow_slay.png
icon_card_attack.png
icon_card_recovery.png
```

## 推荐尺寸

- 卡牌插画：`768 x 1024` 或 `1024 x 1024`
- 卡牌图标：`128 x 128` 或 `256 x 256`

## 透明背景

卡牌插画可以不透明。

卡牌图标必须透明背景，方便放在卡面、Tooltip 和图鉴中。

## 引用系统

未来由以下系统引用：

- 卡牌 UI
- `SpriteDatabase`
- `EffectDatabase`
- 图鉴
- 教学界面

不要把卡牌插画路径写入战斗规则。
