# Presentation Presenters

## 模块职责

Presenters 是 Presentation Framework 的具体表现能力边界。

它们负责：

- 接收 `PresentationEvent`。
- 控制未来 Godot 节点、动画、特效、音效和 UI 表现。
- 隔离 Battle 与具体表现实现。

它们不负责：

- 伤害计算。
- Trigger 顺序。
- Buff 生命周期。
- 奖励执行。
- 背包、商店、事件数据。

## Presenter 结构

```text
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
```

## CharacterPresenter

负责：

- Idle。
- Enter。
- Attack。
- Hit。
- Heal。
- Death。
- 头像、战斗立绘、角色动作。

什么时候扩展：

- 新增角色动作系统。
- 接入角色 Spine。
- 接入角色受击闪白。
- 接入死亡消失或倒地动画。

什么时候不要修改：

- 只新增武器轨迹时，不改 CharacterPresenter。
- 只新增伤害数字时，不改 CharacterPresenter。
- 只修改伤害规则时，不改 CharacterPresenter。

## WeaponPresenter

负责：

- 武器显示。
- 武器挂点。
- 武器挥动。
- 武器拖尾。
- 武器隐藏或回收。

什么时候扩展：

- 新增武器图片。
- 新增武器挥动动画。
- 新增武器轨迹。
- 不同武器需要不同挂点或偏移。

什么时候不要修改：

- 只调整装备属性时，不改 WeaponPresenter。
- 只新增装备随机池规则时，不改 WeaponPresenter。
- 只新增 Buff 特效时，不改 WeaponPresenter。

## EffectPresenter

负责：

- 攻击特效。
- 命中特效。
- 治疗特效。
- 护盾特效。
- Buff / Debuff 特效。

什么时候扩展：

- 新增 Fire、Thunder、Ice、Poison、Shadow 等元素特效。
- 新增命中爆点。
- 新增治疗光效。
- 新增护盾破裂表现。

什么时候不要修改：

- 只新增角色动作时，不改 EffectPresenter。
- 只新增音效资源 ID 时，优先改 AudioDatabase。
- 只新增特效定义时，优先改 EffectDatabase。

## InteractionPresenter

负责：

- 攻击者和目标之间的组合演出。
- 角色动作、武器轨迹、命中特效、浮字之间的时序。
- 未来 HitPause、连击、打断、镜头节奏。

什么时候扩展：

- 一个表现需要同时协调多个 Presenter。
- 新增复杂攻击演出。
- 新增 Boss 入场或死亡收尾。
- 新增演出队列。

什么时候不要修改：

- 只新增单个图片资源时，不改 InteractionPresenter。
- 只新增单个音效时，不改 InteractionPresenter。
- 只修改 UI 提示样式时，不改 InteractionPresenter。

## UIPresenter

负责：

- Floating Text。
- 伤害数字。
- 治疗数字。
- Buff 提示。
- 资源变化提示。
- 屏幕震动。
- UI 短提示。

什么时候扩展：

- 新增浮字样式。
- 新增屏幕震动曲线。
- 新增 Buff 弹出提示。
- 新增战斗短提示。

什么时候不要修改：

- 只修改伤害数值时，不改 UIPresenter。
- 只新增角色动画时，不改 UIPresenter。
- 只新增武器资源时，不改 UIPresenter。

## 开发规则

```text
Battle / Damage / Reward
  |
  v
PresentationEvent
  |
  v
PresentationManager
  |
  v
Presenter
```

禁止：

- Presenter 修改 HP。
- Presenter 发放奖励。
- Presenter 触发 Trigger。
- Presenter 查询卡牌克制关系。
- Battle 直接调用具体 Presenter 的 Godot 节点。

允许：

- Presenter 查询表现数据库。
- Presenter 读取 `PresentationConfig`。
- Presenter 播放动画、音效、特效和 UI 反馈。
