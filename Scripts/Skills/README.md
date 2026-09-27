# Skills System

## 模块职责

负责：

- 角色技能和 Boss 技能的战斗侧实现。
- 事件技能或特殊技能的 Trigger 效果。
- 技能状态的跨回合推进。

不负责：

- 技能 UI 展示。
- 技能静态文本。
- 装备效果。
- 事件奖励执行。

## 模块结构

```text
Skill
  |
  v
Player Owned Skills
  |
  v
Skills/*.cs
  |
  v
IBattleEffect
  |
  v
TriggerManager
```

## 生命周期

```text
Skill acquired
  |
  v
Battle starts / Trigger timing
  |
  v
Effect condition check
  |
  v
Effect applies
  |
  v
Temporary state cleanup
```

## 对外接口

主要公开入口：

- `Skill`
- `SkillDatabase`
- `SkillIds`
- `SkillSource`
- 各技能 `IBattleEffect.Execute(...)`

## 调用关系

```text
BattleContext
  |
  v
Owned Skills
  |
  v
TriggerManager
  |
  v
Skill Effects
```

## 新增功能应该放哪里

- 新增角色技能：`Scripts/Skills/`。
- 新增通用技能效果：可放入 `Scripts/SkillEffects/`。
- 新增反应技能：配合 `Scripts/Reactions/`。
- 新增技能描述：Localization。

## 注意事项

- 不要在技能中直接刷新 UI。
- 不要绕过 TriggerTiming。
- 如果技能需要跨回合状态，必须明确清理时机。
