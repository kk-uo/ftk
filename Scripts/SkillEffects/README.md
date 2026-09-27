# Skill Effects System

## 模块职责

负责：

- 技能定义层的效果入口。
- 角色技能、Boss 技能、事件技能的可复用效果。
- 将技能描述中的能力映射到 Trigger 或 Reaction。

不负责：

- 技能静态数据库的全部维护。
- UI 技能按钮绘制。
- 战斗主流程特殊分支。

## 模块结构

```text
SkillDatabase
  |
  v
SkillEffectRegistry
  |
  v
ISkillEffect
  |
  v
IBattleEffect / Reaction
  |
  v
TriggerManager
```

## 生命周期

```text
Skill Added
  |
  v
Effect Registered
  |
  v
TriggerTiming Raised
  |
  v
Skill Effect Executes
  |
  v
BattleContext Updated
```

## 对外接口

主要公开入口：

- `ISkillEffect`
- `SkillEffectRegistry`
- 各技能效果类
- `IBattleEffect.Execute(...)`

## 调用关系

```text
Player Skills
  |
  v
SkillEffectRegistry
  |
  v
TriggerManager
  |
  v
BattleContext
```

## 新增功能应该放哪里

- 新增纯定义技能：`SkillDatabase`。
- 新增被动战斗效果：实现 `IBattleEffect`。
- 新增主动响应：实现 `IReaction` 或复用 Reaction Window。
- 新增技能文本：Localization。

## 注意事项

- 技能效果不要直接写进 `BattleManager`。
- 技能来源需要保留，方便 Debug 和事件规则识别。
- 复杂技能应拆成多个小的 Trigger 效果，避免单个类承担多个阶段。
