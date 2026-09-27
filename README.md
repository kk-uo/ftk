# 项目简介

本项目是一个基于 Godot 4.6 .NET 与 C# 的桌面端卡牌战斗原型。

核心体验是：

- 回合制卡牌对抗
- 角色、技能、装备、Buff、事件共同驱动局内成长
- 中文 UI 与本地化文本
- 通过 Trigger、Reward、Damage、Presentation 分层降低系统耦合

项目默认分辨率为 `2560 x 1440`，主要 UI 使用 `NotoSerifCJKsc-Black.otf`。

## 整体架构

```text
Data
  |
  v
Logic
  |
  v
Presentation
  |
  v
UI
```

- `Data`：角色、敌人、装备、技能、事件、Buff、关卡等静态定义。
- `Logic`：战斗、伤害、奖励、触发器、商店、背包、事件等规则。
- `Presentation`：战斗提示、浮动数字、日志、选择面板等表现协调。
- `UI`：Godot Control 节点、场景、按钮、卡片、面板。

规则层不能直接承担表现层职责。新增功能时优先判断它属于数据、逻辑、表现还是 UI，再放入对应模块。

## 项目目录说明

```text
Assets/
  字体、图片等资源。

Scenes/
  Godot 场景文件。

Scripts/
  C# 业务代码与 UI 控制脚本。

Scripts/Battle/
  战斗生命周期与结算效果。

Scripts/Damage/
  伤害、防御、濒死、死亡与伤害修正效果。

Scripts/Equipment/
  装备触发效果。

Scripts/Presentation/
  图片、动画、武器、特效、音效和 UI 表现框架。

Scripts/Reactions/
  实时反应窗口选项。

Scripts/SkillEffects/
  技能定义层效果。

Scripts/Skills/
  角色、Boss、事件技能的战斗效果。
```

## 各系统介绍

### Battle

负责战斗生命周期、阶段推进、出牌栏、目标选择、日志与表现协调。

主要入口：

- `BattleManager`
- `BattleContext`
- `BattleResolver`
- `BattleLifecycleEffects`

### Trigger

负责把技能、装备、Buff、战斗生命周期效果统一挂接到 `TriggerTiming`。

主要入口：

- `TriggerManager`
- `IBattleEffect`
- `EffectQueue`
- `TriggerTiming`
- `EffectPriority`

### Damage

负责伤害实例、防御窗口、伤害修正、实际扣血、濒死与死亡。

主要入口：

- `DamageEffects`
- `DamageModifierPipeline`
- `DamageType`

### Reward

负责统一执行奖励动作，避免事件、商店、Debug 直接修改玩家状态。

主要入口：

- `RewardManager`
- `RewardSequence`
- `RewardAction`

### Localization

负责中文、英文文本查询、语言切换、名称和描述获取。

主要入口：

- `Localization`
- `LocalizationGlossary`
- `LocalizationValidator`

### Presentation

负责把逻辑结果转换为玩家可见反馈。

核心入口：

- `PresentationManager`
- `PresentationEvent`
- `PresentationConfig`
- `CharacterPresenter`
- `WeaponPresenter`
- `EffectPresenter`
- `InteractionPresenter`
- `UIPresenter`

新增动画和演出时，优先增加 Presentation 层接口，不要让 Battle 或 Damage 直接播放动画。

### Tutorial

负责新手引导步骤、触发条件、遮罩箭头和教学战斗。

主要入口：

- `TutorialManager`
- `TutorialDatabase`
- `TutorialStep`
- `TutorialBattleScene`

### Inventory

负责装备持有、背包、已装备槽位、出售、拖拽和装备详情。

主要入口：

- `InventoryManager`
- `InventoryController`
- `InventoryItemUI`
- `EquipmentSlotUI`

### Shop

负责商店商品生成、价格、购买和商品 UI。

主要入口：

- `ShopManager`
- `ShopController`
- `ShopSlotUI`

### Event

负责地图事件、事件选项、事件条件、事件奖励与事件跳转。

主要入口：

- `EventManager`
- `EventDatabase`
- `EventController`
- `EventData`

## 新增一个角色需要修改哪些地方

1. 在 `CharacterDatabase` 增加角色数据。
2. 在 Localization 中增加角色名称、描述、阵营等文本。
3. 如果角色有默认技能，在 `SkillDatabase` 增加技能定义。
4. 如果技能有战斗效果，在 `Skills/` 或 `SkillEffects/` 增加对应效果。
5. 通过 `TriggerManager` 注册战斗效果，不要在 `BattleManager` 写角色专属判断。
6. 更新图鉴、角色选择或测试入口。

## 新增一个装备需要修改哪些地方

1. 在 `EquipmentIds` 增加稳定 ID。
2. 在 `EquipmentDatabase` 增加装备定义。
3. 在 Localization 中增加装备名称和描述。
4. 如果装备有战斗效果，在 `Scripts/Equipment/` 增加 `IBattleEffect`。
5. 如需随机出现，确认 `CanAppearInRandomPool`、品质、类型、获取方式。
6. 如需商店出现，确认商店池筛选规则。

## 新增一个事件需要修改哪些地方

1. 在 `EventDatabase` 或对应事件工厂增加事件定义。
2. 事件奖励优先使用 `RewardSequence` 和 `RewardAction`。
3. 需要选择时使用 `ChoicePanel` 与 `ChoiceProvider`。
4. 事件文本必须进入 Localization。
5. RunOnce、章节、稀有度、隐藏条件在事件数据中表达。
6. 不要在事件选项里直接改玩家数据。

## 新增一个技能需要修改哪些地方

1. 在 `SkillIds` 增加稳定 ID。
2. 在 `SkillDatabase` 增加技能定义。
3. 在 Localization 中增加技能名称和描述。
4. 被动或战斗效果实现为 `IBattleEffect`。
5. 主动、卡牌或展示型技能实现对应 UI 和触发入口。
6. 通过 `TriggerTiming` 挂接，不要在战斗主流程里写特殊分支。

## 新增一个 Buff 需要修改哪些地方

1. 在 `RunBuffIds` 或相关 Buff 定义处增加稳定 ID。
2. 在 `RunBuffDatabase` 增加定义。
3. 在 Localization 中增加名称、描述、Tooltip。
4. 战斗中生效的 Buff 通过 `IBattleEffect` 或现有 RunBuff 管线接入。
5. 明确生命周期：本场战斗、下一场战斗、整个 Run、永久数据。

## 文档维护要求

以后新增代码必须同步更新：

- 文件头
- XML Documentation
- 模块 README
- Localization Key
- 架构说明或贡献规范中对应的规则

不要让 README 描述已经失效的系统流程。
