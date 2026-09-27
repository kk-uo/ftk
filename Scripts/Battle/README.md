# Battle System

## 模块职责

负责：

- 战斗生命周期。
- BattlePhase 切换。
- 战斗前、战斗中、战斗后的触发点。
- 协调卡牌结算、状态清理和战斗结束。

不负责：

- 具体伤害计算。
- 具体技能实现。
- 具体装备实现。
- 动画播放。
- 长期背包、商店、事件数据。

## 模块结构

```text
BattleManager
  |
  v
BattleContext
  |
  v
TriggerManager
  |
  v
BattleLifecycleEffects
  |
  v
BattleResolver
  |
  v
Damage System
```

## 生命周期

```text
StartBattle
  |
  v
OnGameStart
  |
  v
StartPhase
  |
  v
BattlePrePhase
  |
  v
BattlePhase
  |
  v
BattlePostPhase
  |
  v
EndPhase
  |
  v
Next Round / BattleEnd
```

## 对外接口

主要公开入口：

- `BattleManager.RestartBattle()`
- `BattleManager.SelectTarget(...)`
- `BattleContext`
- `BattlePhaseResolutionEffect.Execute(...)`
- `BattlePrePhaseEffect.Execute(...)`
- `BattlePostPhaseEffect.Execute(...)`
- `BattleEndEffect.Execute(...)`

## 调用关系

```text
Player Input
  |
  v
BattleManager
  |
  +-- BattleHotkeySystem
  |
  +-- TriggerManager
  |
  +-- DamageEffects
  |
  +-- ReactionQueue
  |
  v
Battle UI Refresh
```

## 新增功能应该放哪里

- 新增战斗阶段：先确认能否复用 `TriggerTiming`。
- 新增出牌规则：优先修改 `BattleRules` 或 ActionSlot 相关代码。
- 行动栏固定保留 11 个 `ActionSlot`；前 10 槽绑定 `1` 至 `0`，第 11 槽使用鼠标点击。
- 永久行动牌超过 11 张时，由统一 `ChoicePanel` 强制选择一张移除；不要在渲染层静默丢弃溢出牌。
- 新增战斗表现：放到 Presentation 或 BattleManager 的表现 partial。
- 新增结算规则：实现 `IBattleEffect` 并注册到 Trigger。

## 注意事项

- Battle 不应该直接播放复杂动画。
- Battle 不应该写装备、技能、角色专属硬编码。
- 所有可扩展战斗效果优先走 `TriggerManager`。
- 反应类技能必须复用 Reaction Window。
