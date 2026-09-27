# Equipment System

## 模块职责

负责：

- 装备战斗效果。
- 装备触发条件。
- 装备对伤害、资源、Buff、卡牌的影响。

不负责：

- 装备静态定义总表。
- 背包装备拖拽。
- 商店购买。
- 事件奖励发放。

## 模块结构

```text
EquipmentDatabase
  |
  v
OwnedEquipment
  |
  v
Equipment Effect
  |
  v
IBattleEffect
  |
  v
TriggerManager
```

## 生命周期

```text
Equip Item
  |
  v
Battle Starts
  |
  v
TriggerTiming
  |
  v
Equipment Effect Executes
  |
  v
BattleEnd Cleanup
```

## 对外接口

主要公开入口：

- `EquipmentDefinition`
- `EquipmentDatabase`
- `EquipmentEffect`
- `EquipmentIds`
- 各装备的 `IBattleEffect.Execute(...)`

## 调用关系

```text
InventoryManager
  |
  v
Equipped Items
  |
  v
TriggerManager
  |
  v
Equipment Effects
  |
  v
BattleContext
```

## 新增功能应该放哪里

- 新增装备定义：`EquipmentDatabase`。
- 新增装备 ID：`EquipmentIds`。
- 新增装备战斗效果：`Scripts/Equipment/`。
- 新增随机池规则：优先使用 `EquipmentDefinition` 字段。
- 新增本地化：Localization。

装备文本必须拆分为两类：

- 外观与用法：`equipment.<id>.flavor`，通过 `Localization.GetEquipmentFlavorDescription(...)` 读取。
- 实际功能：`equipment.<id>.effect.N`，通过 `Localization.GetEquipmentEffectDescription(...)` 逐条读取。

没有专属 `flavor` 时会按武器、护甲、饰品、载具等主槽位生成本地化回退文本；新增正式装备时仍建议提供独立文案，使图鉴描述能够准确反映其造型和使用方式。

## 注意事项

- 不要使用硬编码 ID 排除随机池装备。
- 一次性战斗效果应在战斗 Buff 或上下文状态中记录，不要修改永久角色数据。
- 当前 `OnBattleEnd` 在每个战斗回合结算后都会抬起；只应在整场胜利后生效的装备必须同时校验正式胜利状态，并使用战斗 ID 做幂等保护。`TreasureDonkeyBattleEndEffect` 是这一模式的参考实现。
- 装备效果不应该直接操作 UI。
- 不要把数值、倍率或触发条件写进 `flavor`；图鉴与背包会把它和功能列表分开展示。
