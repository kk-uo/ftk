# Scripts Core System

## 模块职责

负责：

- 承载项目主要 C# 代码。
- 组织核心数据、战斗逻辑、事件、商店、背包、奖励、本地化和 UI Controller。
- 为 Godot 场景提供稳定脚本入口。

不负责：

- 保存美术资源。
- 定义 Godot 场景结构。
- 绕过子系统直接处理所有业务。

## 模块结构

```text
Scripts
  |
  +-- Battle
  |
  +-- Damage
  |
  +-- Equipment
  |
  +-- Presentation
  |
  +-- Reactions
  |
  +-- SkillEffects
  |
  +-- Skills
  |
  +-- Core Managers
      |
      +-- GameManager
      +-- EventManager
      +-- RewardManager
      +-- InventoryManager
      +-- ShopManager
      +-- Localization
```

## 生命周期

```text
GameManager
  |
  v
Map / Event / Shop / Battle
  |
  v
Reward / Inventory / RunBuff
  |
  v
Localization / Presentation / UI Refresh
```

## 对外接口

常用公开入口：

- `GameManager`
- `BattleManager`
- `EventManager`
- `RewardManager`
- `InventoryManager`
- `ShopManager`
- `Localization`
- `ChoicePanel`
- `ChoiceProvider`

## 调用关系

```text
Scene Controller
  |
  v
Manager
  |
  +-- Database
  |
  +-- Trigger
  |
  +-- Reward
  |
  +-- Localization
  |
  v
UI Refresh
```

## 新增功能应该放哪里

- 新增战斗阶段规则：`Battle/` 或 `TriggerManager` 注册的效果。
- 新增伤害规则：`Damage/`。
- 新增装备效果：`Equipment/`。
- 新增图片、动画、音效、特效和 UI 表现：`Presentation/`。
- 新增技能效果：`Skills/` 或 `SkillEffects/`。
- 新增实时响应：`Reactions/`。
- 新增事件：`EventDatabase`、`EventSystem`。
- 新增奖励：`RewardSystem`。
- 新增本地化：`Localization` 资源与 Key。

## 注意事项

- `Scripts/` 根目录包含多个历史核心系统，不代表所有逻辑都应该继续堆在根目录。
- 新增跨系统能力时，先判断是否应该进入 Trigger、Reward、Choice、Localization。
- UI Controller 可以调用 Manager，但不应该直接修改核心数据结构的内部细节。
