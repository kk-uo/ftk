# Damage System

## 模块职责

负责：

- 防御、免疫、格挡。
- 伤害加成和伤害减免。
- 实际扣血。
- 受伤后触发。
- 濒死与死亡流程。

不负责：

- 战斗阶段推进。
- 奖励发放。
- UI 动画。
- 事件分支。

## 模块结构

```text
BattleResolver
  |
  v
OnBeforeDamage
  |
  v
DefenseBeforeDamageEffect
  |
  v
OnDamage
  |
  v
DamageModifierPipeline
  |
  v
ApplyDamageEffect
  |
  v
OnDamageTaken
  |
  v
DyingEffect / DeathEffect
```

## 生命周期

```text
Create DamageEvent
  |
  v
BeforeDamage
  |
  v
Defense / Immunity
  |
  v
Damage Modifier
  |
  v
Apply HP Change
  |
  v
DamageTaken
  |
  v
OnDying
  |
  v
OnDeath
```

## 对外接口

主要公开入口：

- `DamageEvent`
- `DamageModifier`
- `DamageModifierPipeline`
- `DefenseBeforeDamageEffect.Execute(...)`
- `ApplyDamageEffect.Execute(...)`
- `DamageTakenEffect.Execute(...)`
- `DyingEffect.Execute(...)`
- `DeathEffect.Execute(...)`

## 调用关系

```text
BattleResolver
  |
  v
TriggerManager
  |
  v
Damage Effects
  |
  v
Player / Enemy HP
  |
  v
Battle Log / Popup
```

## 新增功能应该放哪里

- 新增伤害倍率：`DamageModifierPipeline` 或对应 `IBattleEffect`。
- 新增免疫：`OnBeforeDamage`。
- 新增受伤后效果：`OnDamageTaken`。
- 新增濒死救援：`OnDying`。
- 新增死亡后处理：`OnDeath`。

## 注意事项

- 免疫伤害不应触发“受到伤害后”类效果。
- 0 伤害不应消耗一次性免疫。
- 不要在装备或技能里直接扣 HP，应该通过伤害管线。
