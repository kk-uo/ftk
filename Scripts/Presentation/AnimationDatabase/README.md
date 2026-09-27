# AnimationDatabase

`Scripts/Presentation/AnimationDatabase.cs` 是 Presentation Framework 的一部分，
职责只有一个：**把“动画到底怎么演”这件事变成数据，而不是代码**。

## 职责

- 用一个稳定的 `AnimationId`（字符串），把一份完整的动画参数
  （`AnimationDefinition`）注册进数据库。
- 给 `WeaponPresenter`、`CharacterPresenter` 等 Presenter 提供统一查询入口：
  `AnimationDatabase.GetAnimation(id)`。
- 让新增动画（普通杀 / 火杀 / 雷杀 / 桃 / 酒 / 技能 / Boss）只需要
  “注册一条新的 `AnimationDefinition`”，不需要改任何 Presenter 的播放代码。

它**不负责**：
- 播放动画（由 Presenter 负责创建 Sprite2D、Tween）。
- 决定动画什么时候触发（由 Battle / PresentationManager 负责）。
- 战斗结算、伤害计算（AnimationDatabase 完全不知道战斗规则）。

`AnimationDatabase` 同时保留了原来的 `Register(string id, string path)` /
`GetPath(string id)` 接口（ID → 资源路径），供未来接入真正的 `AnimationPlayer`
资源、Spine 动画等场景使用；这一套和 `AnimationDefinition` 的参数注册表完全独立，
互不影响。

## AnimationDefinition 字段含义

| 字段 | 类型 | 含义 |
|---|---|---|
| `AnimationId` | string | 动画稳定 ID，例如 `weapon_default_attack`、`weapon_fire_attack`。 |
| `Duration` | float | 挥动主体阶段（旋转+位移）的持续时间（秒）。 |
| `StartRotation` | float | 起始旋转角度（度）。 |
| `EndRotation` | float | 结束旋转角度（度）。 |
| `StartOffset` | Vector2 | 起始位置相对锚点（角色卡片）右边缘的额外偏移。 |
| `EndOffset` | Vector2 | 结束位置相对锚点右边缘的额外偏移，和 `StartOffset` 同一参照系。 |
| `Scale` | float | 武器精灵的统一缩放（等比例，不拉伸）。 |
| `FadeInTime` | float | 淡入时长（秒），0 表示一开始就完全不透明。 |
| `FadeOutTime` | float | 停留结束后，武器（和 Trail）淡出所用的时间。 |
| `HoldDuration` | float | 挥动结束、淡出开始前的停留时间（秒）。 |
| `TrailEnabled` | bool | 是否启用 Trail；为 false 时即使武器配置了 Trail 贴图也不显示。 |
| `TrailFadeTime` | float | Trail 淡出所用时间，可以和 `FadeOutTime` 不同。 |
| `TrailScale` | float | Trail 相对武器的缩放比例（Trail 是武器子节点，最终缩放 = 武器 Scale × 该值）。 |
| `TrailAlpha` | float | Trail 起始透明度（0~1）。 |
| `WeaponVisible` | bool | 该动画播放时武器本体是否可见（预留：未来"只播 Trail/特效"场景）。 |
| `PlayOnTop` | bool | 是否渲染在最上层（对应更高的 ZIndex）。 |
| `Loop` | bool | 是否循环播放（当前 Presenter 实现未消费，预留）。 |
| `AnimationCurve` | string | 缓动曲线 ID（预留，当前 Presenter 统一使用线性过渡）。 |

## 以后如何新增一个动画

以火杀（Fire Kill）举例，假设武器视觉 ID 是 `weapon_fire`：

```csharp
AnimationDatabase.RegisterAnimation(new AnimationDefinition(
    animationId: "weapon_fire_attack",   // 约定：武器 ID + "_attack"
    duration: 0.30f,
    startRotation: -90f,
    endRotation: 30f,
    startOffset: new Vector2(18f, 0f),
    endOffset: new Vector2(38f, 0f),
    scale: 1.0f,
    fadeOutTime: 0.12f,
    holdDuration: 0.08f,
    trailEnabled: true,
    trailFadeTime: 0.12f,
    trailScale: 1f,
    trailAlpha: 1f));
```

`WeaponPresenter` 会在武器 ID 为 `weapon_fire` 时自动拼出 `weapon_fire_attack`
并查询这条定义，不需要修改任何播放逻辑。桃、酒、技能、Boss 动画同理，
只是换一个 `AnimationId` 和一组参数。

## 以后如何修改动画参数

不要去 `WeaponPresenter.cs` 里找数字改——那里已经没有任何写死的角度、时长、
位移。只需要找到对应 `AnimationId` 注册的地方（默认武器在
`WeaponPresenter` 构造函数里），改 `AnimationDefinition` 构造参数即可，
所有使用这个 `AnimationId` 的地方会自动生效。
