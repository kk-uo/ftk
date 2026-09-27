# Reaction System

## 模块职责

负责：

- 实时反应选项的数据实现。
- 龙胆、破军、击鼓等需要倒计时选择的响应。
- 将战斗后触发的可选行动统一交给 Reaction Window。

不负责：

- 普通 ActionBar 出牌。
- 常规战斗阶段推进。
- 伤害计算。
- 奖励执行。

## 模块结构

```text
IBattleEffect
  |
  v
ReactionQueue
  |
  v
IReaction
  |
  v
ReactionWindow
  |
  v
ReactionOption.Resolve
```

## 生命周期

```text
Trigger detects reaction
  |
  v
Enqueue IReaction
  |
  v
BattleManager enters ReactionMode
  |
  v
Render options
  |
  v
Player chooses / timeout
  |
  v
Resolve option
```

## 对外接口

主要公开入口：

- `IReaction`
- `ReactionOption`
- `ReactionQueue`
- `ReactionWindow`
- `LongdanReaction`
- `BreakArmyReaction`
- `JiGuReaction`

## 调用关系

```text
Skill / Equipment Effect
  |
  v
BattleContext.Reactions
  |
  v
BattleManager
  |
  v
ReactionWindow
```

## 新增功能应该放哪里

- 新增实时响应内容：新增 `IReaction` 实现。
- 新增响应卡牌可用性：优先检查 `BattleRules.CanPlayReactionCard`。
- 新增响应表现：扩展 `ReactionWindow`。

## 注意事项

- 不要新增一次性弹窗替代 Reaction Window。
- 反应选项必须有超时 fallback。
- ReactionMode 的 ActionBar 来自 `ReactionOption`，不是普通行动卡池。
