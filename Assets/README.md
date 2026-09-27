# Assets 资源目录

## 目录用途

`Assets/` 是项目所有美术、音频、字体和 UI 资源的统一入口。

这里的目录结构服务于两个目标：

- 让开发者可以快速判断资源应该放在哪里。
- 让未来 `SpriteDatabase`、`AnimationDatabase`、`AudioDatabase` 可以按稳定规则引用资源。

## 一级分类

- `Characters/`：玩家角色头像、战斗立绘、角色图标。
- `Bosses/`：Boss 头像和战斗立绘。
- `Weapons/`：武器图片和武器轨迹。
- `Cards/`：卡牌插画和卡牌图标。
- `Effects/`：攻击、治疗、护盾、Buff、Debuff 等特效图片。
- `Events/`：事件背景和事件角色图。
- `Backgrounds/`：战斗背景和地图背景。
- `UI/`：按钮、面板、边框、图标、鼠标指针。
- `Fonts/`：字体文件。
- `Audio/`：BGM、音效、语音。
- `Sprites/`：历史保留目录，不建议继续新增资源。

## 总体命名规范

统一使用：

```text
分类_名称_用途.png
```

示例：

```text
portrait_zhaoyun.png
weapon_qinggang.png
card_fire_slay.png
effect_thunder_hit.png
ui_button_blue.png
bg_chapter1.png
sfx_attack.wav
```

## 透明背景要求

需要透明背景：

- 角色头像
- 战斗立绘
- 武器
- 卡牌图标
- 装备图标
- Buff 图标
- UI 图标
- 特效切片

不需要透明背景：

- 战斗背景
- 地图背景
- 事件背景
- 卡牌完整插画

## 引用规则

业务代码不要直接 `Load()` 资源路径。

未来资源引用应通过：

- `SpriteDatabase`
- `AnimationDatabase`
- `AudioDatabase`
- `EffectDatabase`
- `WeaponVisualDatabase`
- `CharacterVisualDatabase`

这样做可以避免资源路径散落在代码里，方便后续替换美术、做缓存或切换动画方案。
