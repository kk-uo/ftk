# Render Layer System

以后所有图片、动画、特效、UI 都通过统一的 `RenderLayer` 决定渲染顺序，
不在各个 Presenter/UI 代码里写死 `ZIndex = xx` 或 `CanvasLayer.Layer = xx`。

## 文件结构

```
Scripts/Presentation/RenderLayer/
├── RenderLayer.cs         // 统一渲染层级枚举
├── RenderLayerManager.cs  // RenderLayer → 共享 CanvasLayer / Godot Layer 数值
└── README.md              // 本文件
```

## 一、有哪些 Render Layer

按从底到顶排列（数值越大越靠前/越靠上）：

| Layer | 数值 | 用途 |
|---|---|---|
| `Background` | 0 | 固定背景图 |
| `Ground` | 10 | 地面/地形装饰 |
| `Character` | 20 | 角色本体 |
| `Weapon` | 30 | 武器本体 |
| `WeaponTrail` | 40 | 武器挥砍拖尾 |
| `SkillEffect` | 50 | 技能/元素特效（斩击、火焰、雷电……） |
| `HitEffect` | 60 | 命中反馈特效 |
| `WorldPopup` | 70 | 世界空间浮动文字/图标（伤害数字、"E 进入"提示） |
| `WorldUI` | 80 | 世界空间交互式 UI（地图节点标签等） |
| `HUD` | 90 | 常规屏幕 HUD（资源、Buff 面板、按钮） |
| `Tooltip` | 100 | Tooltip 悬浮提示 |
| `Modal` | 110 | 模态弹窗（确认框、调试窗口） |
| `ScreenEffect` | 120 | 局部全屏反馈（震屏暗角等） |
| `FullscreenEffect` | 130 | 完整全屏演出（Boss 大招、过场动画） |
| `Fade` | 140 | 转场淡入淡出 |
| `Debug` | 150 | 调试信息，永远最上层 |

## 二、什么时候应该用哪个 Layer

- **UI 不是永远最高层**：`HUD`/`Tooltip`/`Modal`（90~110）明确排在
  `SkillEffect`/`HitEffect`（50~60）之上——普通特效不会盖住 UI，这是常规情况。
- **但大型演出可以盖住 UI**：`ScreenEffect`/`FullscreenEffect`/`Fade`（120~140）
  明确排在所有常规 UI 之上。Boss 大招、剧情/过场动画应该使用
  `RenderLayer.FullscreenEffect`（或以后需要更强的边缘反馈时用
  `RenderLayer.ScreenEffect`），而不是 `RenderLayer.SkillEffect`——
  只是换一个 Layer 参数，不需要额外的特殊代码。
- 地图上的世界内容用 `Ground`（装饰）/`WorldUI`（可交互节点）/`WorldPopup`
  （浮动提示），不要和战斗里的 `Character`/`Weapon`/`SkillEffect` 混用。

## 三、以后如何新增 Layer

1. 在 `RenderLayer.cs` 里新增一个枚举值，选一个和相邻层不冲突的数值
   （建议保持 10 的间距，方便以后再插入）。
2. 不需要修改 `RenderLayerManager.cs`——`ToGodotLayer`/`GetOrCreateCanvasLayer`
   对所有枚举值使用同一套逻辑。
3. 在需要用到新 Layer 的 `XxxDefinition`（`EffectDefinition`/
   `AnimationDefinition`/`WeaponVisualDefinition`/`CharacterVisualDefinition`/
   `MapNodeVisualDefinition`）的具体注册处，把对应的 Layer 参数改成新枚举值。

## 四、RenderLayerManager 怎么用

```csharp
var overlay = RenderLayerManager.GetOrCreateCanvasLayer(tree, RenderLayer.SkillEffect);
overlay.AddChild(sprite);
```

同一个 `RenderLayer` 在同一棵场景树里只会创建一个共享 `CanvasLayer`，
多个 Presenter 用同一个 Layer 时会自动复用，不会各自建一份、也不会互相冲突。
场景切换导致旧节点失效时会自动重新创建，调用方不需要自己判断。

## 五、目前已经支持 Render Layer 的系统

- **EffectDefinition**（`Scripts/Presentation/Effects/EffectDefinition.cs`）：
  新增 `RenderLayer` 字段（默认 `SkillEffect`），`EffectPlayer` 已经改用
  `RenderLayerManager` 取得 CanvasLayer，不再有自己的私有 `EffectPlayerOverlay`
  和写死的 `Layer = 60`。原有的 `Layer`（int）字段还在，仅作为向后兼容保留，
  新代码应该用 `RenderLayer`。
- **AnimationDefinition**（`Scripts/Presentation/AnimationDefinition.cs`）：
  新增 `RenderLayer` 字段（默认 `Weapon`），为角色/Boss 演出类动画预留；
  当前 `WeaponPresenter` 的层级选择仍然读 `WeaponVisualDefinition.WeaponLayer`
  （武器动画天然挂在武器上），这个字段留给未来不挂在武器上的动画使用。
- **WeaponVisualDefinition**（`Scripts/Presentation/WeaponVisualDefinition.cs`）：
  新增 `WeaponLayer`（默认 `Weapon`）/`TrailLayer`（默认 `WeaponTrail`）。
  `WeaponPresenter` 已经改用 `RenderLayerManager.GetOrCreateCanvasLayer(tree,
  visual.WeaponLayer)` 取得武器的 CanvasLayer，不再有写死的
  `OverlayCanvasLayer = 50` 常量。**已知限制**：Trail 目前仍然是武器
  Sprite2D 的子节点（保证完全跟随武器、不单独算位置），所以它实际显示的层级
  始终等于 `WeaponLayer`；`TrailLayer` 只有和 `WeaponLayer` 相同时才符合实际
  效果，配成不同层这个能力先留作预留，真正需要独立层级的 Trail 需要额外的
  位置同步，属于以后的扩展。
- **CharacterVisualDefinition**（`Scripts/Presentation/CharacterVisualDefinition.cs`）：
  新增 `CharacterLayer` 字段（默认 `Character`），以后 Boss 可以指定更高的
  层级压住普通角色/特效。同时也补齐了 `IdleAnimationId`/`DefenseAnimationId`/
  `VictoryAnimationId`/`SkillAnimationId`，配合新增的 `CharacterAnimationState`
  枚举和 `CharacterVisualDatabase.ResolveAnimationId(characterId, state)`
  统一解析角色动作对应的 AnimationId，查不到时自动回退到
  `"character_default_<state>"` 这个命名约定。
- **MapNodeVisualDefinition**（`Scripts/Map/MapNodeVisual/MapNodeVisualDefinition.cs`）：
  新增 `RenderLayer` 字段（默认 `WorldUI`）。`MapNodeVisual` 的最终 ZIndex
  = `RenderLayerManager.ToGodotLayer(definition.RenderLayer) + definition.ZIndex`
  ——例如 Boss 雕像可以配置比普通地图装饰（`Ground`）更高的 Layer，
  自然压住它们，不需要为地图节点写死"如果是 Boss 就……"的判断。
- **PresentationManager**：本身从来没有直接设置过任何 ZIndex（它只是事件分发
  的空壳，真正实例化节点的是各个 Presenter），这次补充了文档说明，
  没有代码改动。

## 六、Boss 全屏技能怎么盖住 HUD

只需要在这次技能对应的 `EffectDefinition`（或未来的全屏演出专属数据）里，
把 `RenderLayer` 设成 `RenderLayer.FullscreenEffect`（130）：

```csharp
EffectDatabase.Register(new EffectDefinition(
    effectId: "boss_ultimate_fullscreen",
    effectType: EffectType.Screen,
    renderLayer: RenderLayer.FullscreenEffect,
    duration: 1.2f,
    fadeIn: 0.15f,
    fadeOut: 0.25f));
```

因为 `FullscreenEffect`（130）> `Modal`（110）> `Tooltip`（100）> `HUD`（90），
这个效果播放时会自然盖住 HUD/Tooltip/Modal，不需要临时隐藏 UI 或者写
"播放大招时把 HUD.Visible 设为 false"这类特殊逻辑。
