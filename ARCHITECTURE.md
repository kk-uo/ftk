# 架构文档

## 设计目标

项目的核心架构目标是让卡牌战斗规则持续扩展时仍然可维护。

目前系统已经包含角色、敌人、卡牌、技能、装备、Buff、事件、商店、背包、奖励、教学和日志。如果所有规则都直接写进 `BattleManager`，后续任何新增内容都会变成跨系统修改。因此项目采用分层设计。

## 分层结构

```text
Data
  |
  |  读取定义
  v
Logic
  |
  |  产生结果
  v
Presentation
  |
  |  显示反馈
  v
UI
```

### Data

数据层描述“有什么”。

包括：

- 角色定义
- 敌人定义
- 卡牌定义
- 技能定义
- 装备定义
- 事件定义
- Buff 定义
- 本地化文本

数据层不应该播放动画、修改 UI、处理战斗阶段。

### Logic

逻辑层描述“规则如何运行”。

包括：

- `BattleManager`
- `TriggerManager`
- `DamageEffects`
- `RewardManager`
- `InventoryManager`
- `ShopManager`
- `EventManager`

逻辑层可以修改游戏状态，但不应该直接承担演出细节。

### Presentation

表现层描述“结果如何呈现”。

包括：

- `PresentationManager`
- `PresentationEvent`
- `PresentationConfig`
- Character / Weapon / Effect / Interaction / UI Presenter
- 浮动伤害数字
- 战斗日志
- 卡牌高亮
- 选择面板
- 事件演出
- 后续动画系统

表现层应消费逻辑层产出的状态或事件，不应该反向决定伤害、奖励或触发顺序。

### UI

UI 层描述“玩家如何操作”。

包括：

- Godot 场景
- Control 节点
- Button
- Label
- Panel
- Tooltip

UI 可以调用公开接口，但不应该绕过 Manager 直接改核心状态。

## 模块通信

```text
Player Input
  |
  v
UI Controller
  |
  v
Manager / System
  |
  v
Trigger / Reward / Damage
  |
  v
Presentation Event
  |
  v
UI Refresh
```

跨模块通信优先使用：

- Manager 公开方法
- TriggerTiming
- RewardAction
- ChoiceResult
- Localization Key
- Godot Signal 或 C# event

避免：

- UI 直接修改战斗内部字段
- 事件直接 `player.Gold += 100`
- 装备直接播放动画
- 技能直接改 UI 文本
- 使用字符串硬编码判断特殊装备

## 为什么 Battle 不负责动画

Battle 的职责是推进战斗阶段和协调结算。

如果 Battle 直接播放动画，会出现三个问题：

1. 战斗规则会依赖具体 UI 节点，测试和复用困难。
2. 动画时序会干扰 Trigger、Damage、Reaction 的规则顺序。
3. 后续替换演出系统时必须修改战斗核心代码。

因此 Battle 应只产生结算结果，再交给 Presentation 或 UI Controller 显示。

## Presentation Framework

Presentation Framework 是项目未来所有图片、动画、武器、特效、音效和 UI 表现的统一入口。

```text
Logic Result
  |
  v
PresentationEvent
  |
  v
PresentationManager
  |
  +-- CharacterPresenter
  +-- WeaponPresenter
  +-- EffectPresenter
  +-- InteractionPresenter
  +-- UIPresenter
  |
  v
Sprite / Animation / Audio / Visual Database
```

规则：

- Battle 不直接播放动画。
- Damage 不直接创建特效。
- Reward 不直接播放音效。
- UI Controller 不直接 `Load()` 表现资源。
- 所有表现资源通过 Presentation Database 统一查询。

当前框架只建立接口和数据入口，不播放真实动画。

## 为什么 Reward 使用 RewardAction

奖励来源很多：

- 事件
- Boss
- 商店
- Debug
- 初始事件
- 技能
- 装备
- Buff

如果每个来源都直接修改玩家状态，会导致扣金币、加装备、加 Buff、打开选择界面等逻辑分散在各处。

`RewardAction` 的价值是：

- 统一执行入口
- 统一日志
- 统一测试路径
- 统一 ChoicePanel 触发
- 便于组合多个奖励动作

```text
Event Option
  |
  v
RewardSequence
  |
  v
RewardManager
  |
  v
Player / Inventory / RunBuff / ChoicePanel
```

新增奖励类型时，应优先新增一个 `RewardAction`，而不是修改事件系统。

## 为什么 Trigger 独立

技能、装备、Buff 都需要在战斗不同时间点生效。

Trigger 独立后，战斗主流程只需要广播：

- `OnTurnStart`
- `OnBattlePrePhase`
- `OnBeforeDamage`
- `OnDamage`
- `OnDamageTaken`
- `OnDying`
- `OnDeath`
- `OnTurnEnd`

具体效果由 `IBattleEffect` 决定是否响应。

```text
Battle Phase
  |
  v
TriggerManager.RaiseTrigger(timing)
  |
  v
Collect IBattleEffect
  |
  v
Sort by EffectPriority
  |
  v
EffectQueue.Execute()
```

这样新增技能或装备时不需要修改 `BattleManager`。

## 伤害管线

伤害结算遵循固定顺序：

```text
Card Interaction
  |
  v
OnBeforeDamage
  |
  v
Defense / Immunity / Block
  |
  v
OnDamage
  |
  v
DamageModifierPipeline
  |
  v
Apply Damage
  |
  v
OnDamageTaken
  |
  v
OnDying / OnDeath
```

新增伤害加成、减免、免疫时，应挂接到对应 Trigger，不要直接改最终扣血逻辑。

## ChoicePanel 架构

ChoicePanel 只负责展示与返回玩家选择，不负责生成候选项，也不处理选择结果。

```text
ChoiceProvider
  |
  v
ChoiceRequest
  |
  v
ChoicePanel
  |
  v
ChoiceResult
  |
  v
Caller / RewardAction
```

新增选择类型时优先新增 Provider。

## 本地化架构

所有玩家可见文本必须通过 Localization 获取。

规则：

- 名称使用 `Localization.GetName(definition)`
- 描述使用 `Localization.GetDescription(definition)`
- UI 固定文本使用 `Localization.Get(key)`
- 切换语言后 UI 必须监听并刷新

不要在 UI 中混用 `Name`、`DisplayName`、`Description`、`DisplayDescription`。

## 可维护性原则

1. 新规则优先通过 Trigger 接入。
2. 新奖励优先通过 RewardAction 接入。
3. 新选择优先通过 ChoiceProvider 接入。
4. 新 UI 文本必须进入 Localization。
5. 新演出优先进入 Presentation 层。
6. 新装备和技能不要污染 BattleManager。
7. 新事件不要直接修改玩家状态。
