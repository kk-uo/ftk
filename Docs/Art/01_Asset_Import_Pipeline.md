# 01 Asset Import Pipeline

## 资源导入总流程

```text
AI 生成图片 / 制作资源
  |
  v
导出 PNG / OGG / WAV / OTF / TTF
  |
  v
放入正确 Assets 目录
  |
  v
Godot Import
  |
  v
检查导入参数
  |
  v
注册 Database（未来）
  |
  v
游戏测试
```

这套流程存在的原因，是避免资源路径、命名和格式在项目后期失控。所有资源先分类，再导入，再由未来 Database 统一引用。

## 角色立绘

- 放置目录：`Assets/Characters/BattlePortraits/`
- 推荐格式：PNG
- 推荐尺寸：`1024 x 1536`
- 命名规范：`battle_zhaoyun.png`
- 透明背景：需要
- 引用系统：`CharacterVisualDatabase`、战斗 UI、角色选择 UI

不要把角色立绘放入背景目录。角色立绘需要透明背景，方便战斗、事件、图鉴复用。

## 角色头像

- 放置目录：`Assets/Characters/Portraits/`
- 推荐格式：PNG
- 推荐尺寸：`512 x 512`
- 命名规范：`portrait_zhaoyun.png`
- 透明背景：需要
- 引用系统：`CharacterVisualDatabase`、角色选择 UI、状态面板

头像应构图清晰，避免角色面部过小。

## Boss 立绘

- 放置目录：`Assets/Bosses/BattlePortraits/`
- 推荐格式：PNG
- 推荐尺寸：`1536 x 1536` 或按 Boss 体型调整
- 命名规范：`battle_boss_doctor.png`
- 透明背景：建议需要
- 引用系统：`CharacterVisualDatabase`、Boss 战斗 UI、Boss 入场演出

Boss 多阶段资源使用 `phase2`、`phase3` 后缀。

## Boss 头像

- 放置目录：`Assets/Bosses/Portraits/`
- 推荐格式：PNG
- 推荐尺寸：`512 x 512`
- 命名规范：`boss_doctor.png`
- 透明背景：需要
- 引用系统：Boss 图鉴、Boss 状态面板、章节 UI

## 武器图片

- 放置目录：`Assets/Weapons/Sprites/`
- 推荐格式：PNG
- 推荐尺寸：`256 x 256` 到 `512 x 512`
- 命名规范：`weapon_qinggang.png`
- 透明背景：需要
- 引用系统：`WeaponVisualDatabase`、`WeaponPresenter`、装备 UI

武器图片不要包含背景，也不要把角色手部画进武器图。

## 武器轨迹

- 放置目录：`Assets/Weapons/Trails/`
- 推荐格式：PNG
- 推荐尺寸：`512 x 256` 或 `1024 x 256`
- 命名规范：`trail_shadow_blade.png`
- 透明背景：需要
- 引用系统：`WeaponVisualDatabase`、`EffectPresenter`

轨迹图需要半透明边缘，避免播放时出现硬边。

## 卡牌插画

- 放置目录：`Assets/Cards/Artwork/`
- 推荐格式：PNG
- 推荐尺寸：`768 x 1024` 或 `1024 x 1024`
- 命名规范：`card_fire_slay.png`
- 透明背景：不需要
- 引用系统：卡牌 UI、图鉴、教学

卡牌插画可以带背景，但主体必须清晰。

## 卡牌图标

- 放置目录：`Assets/Cards/Icons/`
- 推荐格式：PNG
- 推荐尺寸：`128 x 128` 或 `256 x 256`
- 命名规范：`icon_card_attack.png`
- 透明背景：需要
- 引用系统：卡牌 UI、Tooltip、图鉴

## 装备图标

- 放置目录：优先 `Assets/UI/Icons/`，如果未来细分可新增 `Assets/Equipment/Icons/`
- 推荐格式：PNG
- 推荐尺寸：`128 x 128` 或 `256 x 256`
- 命名规范：`icon_equipment_qinggang.png`
- 透明背景：需要
- 引用系统：背包、商店、装备详情、图鉴

当前不新增 `Equipment` 目录，是为了避免过早拆分；如果装备图标规模变大，再建立专门目录。

## Buff 图标

- 放置目录：`Assets/UI/Icons/`
- 推荐格式：PNG
- 推荐尺寸：`128 x 128`
- 命名规范：`icon_buff_poison.png`
- 透明背景：需要
- 引用系统：Buff 栏、Tooltip、战斗 UI

Buff 图标要避免和装备图标混淆，命名必须带 `buff`。

## UI 图片

- 放置目录：`Assets/UI/`
- 推荐格式：PNG
- 推荐尺寸：按 UI 类型决定
- 命名规范：`ui_button_blue.png`、`ui_panel_common.png`
- 透明背景：按钮、图标、边框需要；面板按设计决定
- 引用系统：Godot Theme、UI Controller、`UIPresenter`

UI 图片要优先保证可读性，不要使用过强纹理影响文字。

## 事件背景

- 放置目录：`Assets/Events/Backgrounds/`
- 推荐格式：PNG
- 推荐尺寸：`1920 x 1080` 或 `2560 x 1440`
- 命名规范：`event_bg_chibi_wreck.png`
- 透明背景：不需要
- 引用系统：事件 UI、剧情演出、`SpriteDatabase`

事件背景应服务叙事，不要混入角色立绘。

## 地图背景

- 放置目录：`Assets/Backgrounds/Map/`
- 推荐格式：PNG
- 推荐尺寸：`2560 x 1440`
- 命名规范：`bg_map_chapter1.png`
- 透明背景：不需要
- 引用系统：地图 UI、章节界面

## 战斗背景

- 放置目录：`Assets/Backgrounds/Battle/`
- 推荐格式：PNG
- 推荐尺寸：`2560 x 1440`
- 命名规范：`bg_battle_palace.png`
- 透明背景：不需要
- 引用系统：战斗 UI、Presentation Framework

## 字体

- 放置目录：`Assets/Fonts/`
- 推荐格式：OTF 或 TTF
- 命名规范：保留字体原始名称
- 透明背景：不适用
- 引用系统：Godot Theme、所有 UI 文本

当前默认字体为 `NotoSerifCJKsc-Black.otf`。替换字体前必须确认简体中文完整显示。

## 音效

- 放置目录：`Assets/Audio/SFX/`
- 推荐格式：WAV 或 OGG
- 命名规范：`sfx_attack.wav`
- 透明背景：不适用
- 引用系统：`AudioDatabase`、`PresentationManager`

短音效应避免过长尾音，否则多次触发会堆叠混乱。

## BGM

- 放置目录：`Assets/Audio/BGM/`
- 推荐格式：OGG
- 命名规范：`bgm_chapter1.ogg`
- 透明背景：不适用
- 引用系统：音频管理、章节、战斗、菜单

BGM 应提前处理循环点。

## Godot Import 检查

图片导入后检查：

- 是否识别为 Texture。
- 是否保留透明通道。
- 是否出现异常压缩或发白。
- UI 图标是否清晰。
- 背景是否按正确分辨率显示。

音频导入后检查：

- BGM 是否可以循环。
- SFX 是否延迟过高。
- 音量是否过大或过小。

字体导入后检查：

- 中文是否完整显示。
- 英文、数字、符号是否正常。
- 是否出现缺字或乱码。
