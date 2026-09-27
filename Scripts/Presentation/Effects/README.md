# Effect System

`Scripts/Presentation/Effects/` 是 Presentation Framework 里专门负责
**"播放什么"** 的部分，和 `AnimationDatabase`（负责"怎么动"）是两件独立的事：

- **Animation**（`AnimationDatabase` / `AnimationDefinition`）：一段挥砍/位移/旋转
  过程应该怎么动——角度、位移、时长。
- **Effect**（本目录）：一次表现应该播放什么东西——挥砍特效、火焰、闪屏、震屏、
  受击反馈、音效……以及要不要跟随目标、要不要循环。

以后普通杀 / 火杀 / 雷杀 / 冰杀 / 桃 / 酒 / Buff / Debuff / Boss 技能，全部通过
这一套 Effect System 播放，区别只在于注册的 `EffectDefinition` 数据不同。

## 文件结构

```
Scripts/Presentation/Effects/
├── EffectDatabase.cs    // 注册表：EffectId/PresetId → 定义
├── EffectDefinition.cs  // 单个效果的完整参数
├── EffectPlayer.cs      // 唯一负责真正播放效果的地方
├── EffectType.cs        // 效果大类枚举
├── EffectPreset.cs       // 多个效果的组合（例如"火杀 = 挥砍+火焰+受击+震屏"）
├── BlockEffectVisual.cs // 完全格挡的程序化护盾瞬时特效
├── DodgeShieldScreenBorder.cs // 玩家【闪】成功时的全屏边缘蜂巢护盾
├── ArrowBarrageEffectVisual.cs // 万箭齐发的大范围曲线箭雨
└── README.md
```

## EffectDatabase 的职责

- 注册所有 `EffectDefinition`（`Register` / `Get` / `GetAll`）。
- 注册所有 `EffectPreset`（`RegisterPreset` / `GetPreset`）。
- 目前只注册了一个效果：`EffectDatabase.SlashDefaultEffectId`（`"slash_default"`），
  作为整个 Effect System 的第一条验证链路。它的 `SpriteId` 是空的——项目里还没有
  专门的斩击特效贴图，`EffectPlayer` 会在贴图缺失时静默跳过，不会报错，也不会
  影响任何已经完成的表现（默认武器、Trail、AnimationDatabase 等）。
- 火 / 雷 / 冰 / 桃 / 酒 / Buff / Debuff / Boss 技能等效果**故意没有注册**，
  按计划留到验证完成后再逐步添加。

## EffectPlayer 的职责

`EffectPlayer` 是 Effect System 里唯一真正实例化 Godot 节点（`Sprite2D`、
未来的粒子/Shader/音频）的地方。调用方式：

```csharp
EffectPlayer.Play(EffectDatabase.SlashDefaultEffectId, anchor);   // 播放单个效果
EffectPlayer.PlayPreset("fire_kill_combo", anchor);               // 播放一组效果
```

`anchor` 是一个 `Control`（通常是角色状态卡片），用作定位参考；不需要位置的
全局效果（例如以后的全屏闪白）可以不传。

`WeaponPresenter`、`CharacterPresenter`、`UIPresenter` 以后都不应该自己创建
特效节点，统一调用 `EffectPlayer.Play(effectId)`。**本次任务明确要求不修改
`WeaponPresenter`，所以这次没有把 `WeaponPresenter` 接进来**——它目前仍然完全
自己处理武器挥砍和 Trail，`EffectPlayer` 是完全独立的一套新增能力，不影响它。

`EffectPlayer` 内部按 `EffectType` 分支：

- `Slash` / `Fire` / `Thunder` / `Ice` / `Heal` / `Shield` / `Buff` / `Debuff`：
  统一走 `PlaySpriteEffect`——只要注册了 `SpriteId`，就会生成一个
  `Sprite2D`，按 `Duration` 停留、按 `FadeIn`/`FadeOut` 淡入淡出，播完自动销毁。
- `UI` / `Screen` / `Camera` / `Particle` / `Shader` / `Custom`：
  当前只是保留分支，不做任何事——这些需要接入具体的相机、UI 或粒子系统，
  留到真正需要时再实现，现在不会报错也不会误播放任何东西。

`CameraShake` / `HitStop` / `FlashScreen` 这三个字段目前**只存数据、不生效**，
因为它们分别需要引用具体的镜头节点、控制引擎时间缩放、或者一个全屏遮罩节点，
这些都超出了"先建立架构、先验证默认 Slash"的范围。以后接入时直接读取这三个
已经存在的字段即可，不需要再扩展 `EffectDefinition`。

## 程序化格挡特效

完全格挡由 `PresentationManager.PlayBlock` 转交给
`EffectPlayer.PlayBlock`，再由 `BlockEffectVisual` 在目标模型中心生成护盾轮廓、
短促冲击环与少量碎光。这套效果不依赖贴图，会按目标包围盒自动缩放，
所以玩家、普通敌人和 Boss 都可共用。它只消费已经确认的格挡结果，
不参与伤害或护盾规则计算。

玩家主动使用【闪】并且确实挡住杀、火杀或万箭齐发时，会改走
`DodgeShieldScreenBorder`。该节点固定在独立的全屏 CanvasLayer，仅在屏幕外围
绘制两层低透明六边形护盾单元，中央完全透明；它不会播放局部斩光、局部护盾或
【格挡】飘字。桃盾、酒盾、青囊和装备免疫仍使用上面的通用格挡效果，避免仅凭
“伤害为 0”误播放闪专属边框。

## 万箭齐发特效

`ArrowBarrageEffectVisual` 接收 Battle UI 提供的施法端和一个或多个目标端坐标，
在 `BattleStageLayer` 中生成一组低像素箭矢。每支箭沿二次贝塞尔曲线飞行，
因此玩家施放时会从玩家一侧射向敌人，敌人施放时则反向射向玩家；多目标战斗会把
箭群分配到所有目标，但会限制总箭数，避免遮挡血条、出招信息和卡牌。

该特效位于敌人模型之上、状态 UI 之下，鼠标输入始终忽略。它只消费已经完成结算的
卡牌表现请求，不读取或修改伤害、命中、AI、Trigger 等战斗规则。

## 火杀与雷杀

火杀和雷杀继续复用普通杀当前装备的武器贴图与挥砍动画，不创建元素专属武器。
`CardVisualProfileDatabase` 仅为两张卡指定不同的 `EffectId`：

- `fire_slash` → `Assets/Effects/Fire/effect_fire_slash.png`
- `thunder_slash` → `Assets/Effects/Thunder/effect_thunder_slash.png`

`WeaponPresenter` 在武器实际 Battle Stage 落点调用 `EffectPlayer.PlayAt`，确保元素层
与武器重合，而不是跟随右下角玩家状态 UI。两张贴图使用透明背景和 Nearest Filter。

## EffectDefinition 每个字段含义

| 字段 | 含义 |
|---|---|
| `EffectId` | 效果稳定 ID，例如 `slash_default`。 |
| `EffectType` | 效果大类，决定 EffectPlayer 走哪条播放分支。 |
| `SpriteId` | 贴图资源 ID，交给 SpriteDatabase 解析；留空表示暂无贴图。 |
| `ParticleId` | 粒子资源 ID（预留，未接入）。 |
| `ShaderId` | Shader 资源 ID（预留，未接入）。 |
| `AudioId` | 音效资源 ID（预留，未接入）。 |
| `CameraShake` | 震屏强度（预留，未接入真实镜头）。 |
| `HitStop` | 顿帧时长（预留，未接入真实时间缩放）。 |
| `FlashScreen` | 是否全屏闪白（预留，未接入真实闪屏节点）。 |
| `Duration` | 效果停留时间（秒）。 |
| `Scale` | 贴图统一缩放。 |
| `Rotation` | 贴图固定旋转角度（度），效果本身不描述旋转过程。 |
| `Offset` | 相对锚点的位置偏移。 |
| `FollowTarget` | true = 挂在锚点下随其移动；false = 用独立 CanvasLayer 定位一次。 |
| `Loop` | 是否循环播放（贴图类效果支持，其余大类留待实现）。 |
| `FadeIn` | 淡入时长（秒），0 表示立即完全不透明。 |
| `FadeOut` | 淡出时长（秒），0 表示 Duration 结束后直接销毁。 |
| `Layer` | 独立 CanvasLayer 的 Layer 值（FollowTarget = false 时使用）。 |
| `ZIndex` | 效果节点自身的 ZIndex。 |
| `Notes` | 备注，方便以后维护，不参与播放逻辑。 |

## 以后如何新增一个 Effect

以火杀的火焰效果举例：

```csharp
EffectDatabase.Register(new EffectDefinition(
    effectId: "fire_default",
    effectType: EffectType.Fire,
    spriteId: "effect_fire_sprite",   // 需要先在 SpriteDatabase 里注册这张贴图
    duration: 0.20f,
    scale: 1.0f,
    fadeIn: 0.05f,
    fadeOut: 0.15f,
    notes: "火杀命中特效"));
```

只要 `EffectType` 是 `Slash/Fire/Thunder/Ice/Heal/Shield/Buff/Debuff` 之一，
`EffectPlayer` 不需要任何改动就能播放它。只有引入全新大类（例如真正的粒子系统）
时，才需要在 `EffectPlayer.Play` 的 switch 里补一个新分支。

## 以后如何组合多个 Effect

```csharp
EffectDatabase.RegisterPreset(new EffectPreset(
    "fire_kill_combo",
    new[] { "slash_default", "fire_default", "hit_default", "camera_shake_default" }));
```

调用 `EffectPlayer.PlayPreset("fire_kill_combo", anchor)` 会按顺序依次播放
列表里的每一个效果。Boss 技能同理，例如
`["thunder_default", "screen_flash_default", "hit_stop_default", "camera_shake_default"]`，
全部只是注册数据，不需要写任何"先播 A 再播 B"的组合代码。
