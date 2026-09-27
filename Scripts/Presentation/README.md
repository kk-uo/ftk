# Presentation System

## 角色技能触发表现

`SkillTriggerPresentationRequest` 是规则层与 UI 之间的强类型边界。技能必须在效果
已经实际写入战斗状态后调用 `BattleContext.ReportPlayerCharacterSkillTriggered()`；
UI 不读取 Combat Log，也不按中文技能名判断。

`SkillTriggerToastQueue` 位于 `Battle.tscn/SkillTriggerPresentationLayer`。它顺序播放
请求，并以 `ResolutionChainId + ActorId + SkillId` 合并同一结算链中的重复上报。
不同结算链中的独立触发不会被吞掉。战斗重开、结束和场景退出时必须调用 `Clear()`。

默认视觉由 `CyberSkillTriggerVisualEffect` 提供：低饱和青色扫描线、品红边缘脉冲、
短线段和技能名称故障残影都位于独立 Overlay，不参与 HUD Container 布局。

后续增加角色纹章、专属粒子、屏幕边缘响应或音效同步时，实现
`ISkillTriggerVisualEffect`，并通过 `SkillTriggerToastQueue.RegisterVisualEffect()` 注册。
接口会收到 `SkillTriggerVisualEffectContext`，其中包含原始技能请求、显示尺寸、表现模式
以及进入/停留/退出时长。扩展表现只能管理自身节点，禁止反向修改 BattleContext。

新增角色技能时：

1. 在技能效果确认生效后上报，不要在条件检查或注册阶段上报。
2. 不要在技能文件内创建 Tween、Label 或 CanvasLayer。
3. 多目标技能只上报一次；无法避免循环调用时复用同一 Trigger 结算链。
4. 装备、Buff、卡牌与事件效果不得调用角色技能表现入口。
5. 需要专属触发特效时注册 `ISkillTriggerVisualEffect`，不要修改通用队列的技能判定。

## Presentation 为什么存在

Battle、Damage、Trigger、Reward 等系统负责游戏规则。

它们知道：

- 谁使用了卡牌。
- 谁命中了目标。
- 造成了多少伤害。
- 谁获得了 Buff。
- 谁死亡或胜利。

它们不应该知道：

- 播放哪张图片。
- 播放哪段动画。
- 使用哪把武器视觉。
- 音效资源路径是什么。
- 屏幕震动参数是什么。
- Floating Text 如何生成。

Presentation 存在的原因，是把“规则结果”转换成“玩家可见的表现”。

```text
Battle:
AttackResolved

Presentation:
播放攻击动作
播放武器轨迹
播放命中特效
播放音效
播放浮动伤害数字
```

禁止把它写成：

```text
Battle:
PlayAttackAnimation()
LoadSwordSprite()
PlayHitSound()
ShakeCamera()
```

这样会让战斗规则和表现资源互相依赖，后续替换动画方案、接入 Spine、增加 Shader 或调整 UI 时都会影响战斗代码。

## 整体架构

```text
Database
  |
  v
Battle / Event / Reward / UI Logic
  |
  v
PresentationEvent
  |
  v
PresentationManager
  |
  +-- CharacterPresenter
  |
  +-- WeaponPresenter
  |
  +-- EffectPresenter
  |
  +-- InteractionPresenter
  |
  +-- UIPresenter
  |
  v
Godot UI / Animation / Audio / Effects
```

当前阶段只建立框架，不播放真实动画。

未来接入动画时，逻辑层只负责发送 `PresentationEvent`，表现层根据事件类型和资源数据库决定如何呈现。

## 模块职责

### PresentationManager

负责：

- 作为表现层唯一调度中心。
- 接收 `PresentationEvent`。
- 未来根据事件类型分发到具体 Presenter。
- 统一持有 `PresentationConfig`。
- 暴露 `PlayAttack`、`PlayHit`、`PlayBlock`、`PlayHeal`、`PlayBuff`、`PlayDeath`、`PlayVictory` 等高层入口。

不负责：

- 计算伤害。
- 判断卡牌是否合法。
- 修改 HP、Buff、金币、背包。
- 直接写战斗规则。

### CharacterPresenter

负责：

- 角色入场。
- Idle。
- Attack。
- Hit。
- Heal。
- Death。
- 后续角色立绘、头像、动作节点的表现控制。

不负责：

- HP。
- Damage。
- Trigger。
- AI。
- 胜负判定。

Battle 不应直接控制角色动画。

### WeaponPresenter

负责：

- 武器显示。
- 武器挂点。
- 武器攻击动作。
- 武器轨迹。
- 武器隐藏和回收。

不负责：

- 装备属性。
- 装备随机池。
- 装备触发效果。
- 伤害加成。

装备什么武器由逻辑层决定，武器如何显示由 WeaponPresenter 和 `WeaponVisualDatabase` 决定。

### EffectPresenter

负责：

- Slash。
- FireSlash。
- ThunderSlash。
- IceSlash。
- PoisonSlash。
- Heal。
- Shield。
- Buff。
- Debuff。

不负责：

- 伤害倍率。
- Buff 层数。
- 元素克制。
- 技能触发条件。

### InteractionPresenter

负责：

- 攻击者与目标之间的组合演出。
- 攻击、格挡、命中、死亡、胜利等多对象表现编排。
- 未来连击、HitPause、镜头节奏、打断和演出队列。

不负责：

- 判定攻击是否命中。
- 判定谁先结算。
- 推进 BattlePhase。

### UIPresenter

负责：

- Floating Text。
- 伤害数字。
- 治疗数字。
- Buff 提示。
- 资源变化提示。
- 屏幕震动。
- UI 短提示。

不负责：

- 计算显示数值。
- 执行奖励。
- 修改玩家状态。

### SpriteDatabase

负责：

- 图片资源 ID 到路径的映射，是**全项目唯一**的图片路径入口。
- 未来图片缓存和统一加载入口。
- Character/Equipment/Skill/Buff/Event/MapNode/UI Icon 的图片最终都要从这里查询到
  `res://` 路径，任何 UI/Presenter 都不应该自己写死 `Assets/` 路径。

不负责：

- 决定图片何时显示。
- 直接处理战斗逻辑。
- 维护"某个业务 Id 对应哪个图片 Id"这种映射关系——那是各自的
  `XxxVisualDatabase`/`XxxIconDatabase`（例如下面的 `EquipmentIconDatabase`、
  已有的 `CharacterVisualDatabase`/`WeaponVisualDatabase`/`MapNodeVisualDatabase`）
  的职责，它们各自持有"业务 Id → SpriteId"，再统一调用 SpriteDatabase 解析成路径。

### EquipmentIconDatabase（UI Icon System）

负责：

- 装备 Id → IconSpriteId → SpriteDatabase → Texture2D 这条完整链路的查询入口。
- 按约定自动为每件已注册装备（只读 `EquipmentDatabase.GetAllEquipments()`）
  派生一个 `"equipment_icon_<装备Id>"` 的 IconSpriteId，不需要逐件手动注册；
  真正有美术资源时，只需要在 SpriteDatabase 里注册对应路径就会自动生效。

不负责：

- 装备属性、掉落、战斗效果——**不修改** `EquipmentDefinition`/`EquipmentDatabase`
  的任何字段或数据，图标映射完全是外挂的一层。
- 图片查不到时的兜底显示，那是 `Scripts/UI/IconLibrary.cs` 的职责
  （详见 `Scripts/UI/README.md`）。

### AnimationDatabase

负责：

- 动画资源 ID 到路径的映射。
- 为 Tween、AnimationPlayer、Shader、Spine 等未来方案提供统一 ID。

不负责：

- 播放动画。
- 决定动画时序。

### AudioDatabase

负责：

- 音效资源 ID 到路径的映射。
- 为未来音效缓存、音量分组、Audio Bus 管理预留入口。

不负责：

- 播放音效。
- 判断音效触发条件。

### EffectDatabase

负责：

- 注册表现特效定义。
- 把特效 ID 关联到 Sprite、Animation、Audio。
- 为 EffectPresenter 提供查询入口。

不负责：

- 实例化特效节点。
- 对象池。
- 伤害逻辑。

### WeaponVisualDatabase

负责：

- 注册武器视觉定义。
- 把武器或装备 ID 关联到 Sprite、Animation、Effect。

不负责：

- 装备属性。
- 装备获取。
- 装备触发。

### CharacterVisualDatabase

负责：

- 注册角色视觉定义。
- 把角色 ID 关联到头像、战斗立绘、攻击动作、受击动作、死亡动作。

不负责：

- 角色属性。
- 角色技能。
- 角色选择逻辑。

## Battle 与 Presentation 的关系

正确关系：

```text
BattleResolver
  |
  |  结算出 AttackResolved
  v
PresentationEvent
  |
  v
PresentationManager
  |
  v
InteractionPresenter / CharacterPresenter / WeaponPresenter / EffectPresenter / UIPresenter
```

Battle 发送：

```text
AttackResolved
DamageResolved
BlockResolved
HealResolved
DeathResolved
```

Presentation 播放：

```text
攻击动作
命中特效
格挡特效
治疗浮字
死亡演出
```

Battle 不应该调用：

```text
PlayAttackAnimation()
PlayHitSound()
LoadWeaponSprite()
ShakeCamera()
```

Presentation 不应该调用：

```text
ApplyDamage()
AddBuff()
LoseHp()
ResolveTrigger()
GrantReward()
```

## 新增一个攻击动画流程

示例：新增 `Shadow Slay` 攻击表现。

### 第一步：注册图片

在 `SpriteDatabase` 注册影杀相关图片资源 ID。

```text
shadow_slay_trail
shadow_slay_impact
```

### 第二步：注册动画

在 `AnimationDatabase` 注册影杀动作或特效动画 ID。

```text
shadow_slay_attack
shadow_slay_hit
```

### 第三步：注册音效

在 `AudioDatabase` 注册影杀音效 ID。

```text
shadow_slay_cast
shadow_slay_hit
```

### 第四步：注册特效

在 `EffectDatabase` 增加：

```text
shadow_slash
```

该特效定义引用：

```text
SpriteId: shadow_slay_trail
AnimationId: shadow_slay_attack
AudioId: shadow_slay_cast
```

### 第五步：注册武器或角色表现

如果 Shadow Slay 需要特殊武器轨迹：

```text
WeaponVisualDatabase
  |
  v
shadow_blade_visual
```

如果 Shadow Slay 需要特殊角色动作：

```text
CharacterVisualDatabase
  |
  v
shadow_attack_animation
```

### 第六步：PresentationManager 分发

未来接入真实动画时，`PresentationManager` 根据：

```text
PresentationEventType.AttackResolved
EffectId: shadow_slash
WeaponId: shadow_blade_visual
```

分发到：

```text
InteractionPresenter
  |
  +-- CharacterPresenter
  +-- WeaponPresenter
  +-- EffectPresenter
  +-- UIPresenter
```

### 第七步：测试

测试重点：

- Battle 只发送 `PresentationEvent`。
- 没有 Battle 代码直接加载资源。
- Shadow Slay 动画资源缺失时不会影响伤害结算。
- 跳过动画时战斗逻辑仍然正确。

## 开发规范

不要：

- Battle 播放动画。
- Damage 播放音效。
- Trigger 创建特效节点。
- Reward 震动屏幕。
- Presenter 修改 HP。
- Presenter 计算伤害。
- Database 处理逻辑。
- UI Controller 直接 `Load()` 图片、动画、音频。

必须：

- 用 `PresentationEvent` 表达表现请求。
- 用 `PresentationManager` 作为表现调度入口。
- 用 Database 管理资源 ID。
- 用 Presenter 隔离具体 Godot 节点和动画方案。
- 在 README 和 XML Documentation 中同步说明新增表现能力。

## 未来扩展路线

```text
Phase 1
框架
  |
  v
Phase 2
武器图片与角色图片
  |
  v
Phase 3
攻击动画与命中特效
  |
  v
Phase 4
Shader、拖尾、镜头、HitPause
  |
  v
Phase 5
Spine 或其它复杂角色动画方案
  |
  v
Phase 6
演出队列、跳过动画、加速播放、回放支持
```

## Learning Path

第一次阅读 Presentation，建议顺序：

```text
README
  |
  v
PresentationManager
  |
  v
PresentationEvent
  |
  v
PresentationEventType
  |
  v
PresentationConfig
  |
  v
CharacterPresenter
  |
  v
WeaponPresenter
  |
  v
EffectPresenter
  |
  v
InteractionPresenter
  |
  v
UIPresenter
  |
  v
Database
  |
  v
Battle 如何发送事件
```

阅读重点：

- `PresentationEvent` 是逻辑层和表现层之间的数据边界。
- `PresentationManager` 是唯一调度入口。
- Presenter 负责表现，不负责规则。
- Database 负责资源 ID，不负责播放。

## Developer Example：如何新增一个新的攻击表现

目标：新增 `Shadow Slay`。

```text
1. AnimationDatabase
   注册 shadow_slay_attack、shadow_slay_hit。

2. SpriteDatabase
   注册 shadow_slay_trail、shadow_slay_impact。

3. AudioDatabase
   注册 shadow_slay_cast、shadow_slay_hit。

4. EffectDatabase
   新增 shadow_slash，并关联 Sprite / Animation / Audio ID。

5. WeaponVisualDatabase
   如果需要特殊武器，新增 shadow_blade_visual。

6. PresentationManager
   后续真实接入时，把 AttackResolved + shadow_slash 分发给 InteractionPresenter。

7. Presenter
   InteractionPresenter 编排整体流程。
   CharacterPresenter 播放角色动作。
   WeaponPresenter 播放武器轨迹。
   EffectPresenter 播放影杀特效。
   UIPresenter 播放伤害数字和屏幕反馈。

8. 测试
   验证资源缺失不影响 Battle。
   验证跳过动画不影响 Damage。
   验证 Battle 没有直接 Load 图片或播放动画。
```

完成标准：

- 战斗逻辑没有改动。
- 表现资源都通过 Database 查询。
- 表现播放都通过 Presenter。
- README 和 XML Documentation 同步更新。
