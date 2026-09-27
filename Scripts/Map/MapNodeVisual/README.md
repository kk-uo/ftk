# MapNodeVisual

`Scripts/Map/MapNodeVisual/` 是地图 Presentation 的统一入口：地图上的
战斗/事件/精英/商店/Boss/藏宝阁节点应该长什么样、能不能交互、交互后
发生什么信号，全部在这里定义，地图探索场景（`MapExplorationView`）
不需要为每一种节点类型写判断分支。

## 三个类的职责

- **`MapNodeVisual`**（Area2D）：地图上真正的世界对象。
  - 用 `CollisionShape2D`（圆形，点击/悬停检测半径 46px）+
    `InputPickable = true` 检测鼠标点击/悬停，不再检测任何物理体进入/离开
    （地图上没有可移动的角色）。
  - 鼠标悬停且节点 `Interactable = true` 时显示"点击进入"提示；
    鼠标移开自动隐藏；鼠标左键点击时（`Interactable = true` 才生效）
    发出 `Interacted(nodeId)` 信号——**点击一次直接进入，不需要额外确认**。
  - 节点下方常驻显示一个标签（沿用原按钮版地图的"标记+类型"文本，
    例如 "☠\n第3关Boss"），保证从按钮切换到 2D 探索后不丢失这部分信息。
  - 外观完全从 `MapNodeVisualDatabase.Resolve(NodeData)` 查询得到，
    本类不写死任何"如果是 Boss 就……"之类的判断，也不直接按 `NodeType` 查表。
  - 持有对既有 `MapNode`（`Scripts/EventSystem.cs`）数据的**引用**
    （`NodeData` 属性，来自 `GameManager.Nodes` 里的同一个对象，不是复制
    出来的另一份数据）——**注意**：Godot 节点类之所以叫 `MapNodeVisual`
    而不是 `MapNode`，是因为 `MapNode` 这个名字已经被
    `Scripts/EventSystem.cs` 里的地图数据类占用，两者不是一回事：
    `MapNode` = 数据（Id/Type/StageIndex/FixedEventId/NextNodeIds...），
    `MapNodeVisual` = 这份数据在地图上对应的可交互世界节点。

- **`MapNodeVisualDefinition`**：一个 VisualId 的完整显示参数集合，包含
  `NodeIcon`/`IdleAnimation`/`InteractAnimation`/`GlowEffect`/`LightEffect`
  （均为预留，当前未接入真实资源/动画/特效系统）、`Scale`、`Offset`、
  `ZIndex`、`Description`，以及两个额外的占位字段
  `PlaceholderGlyph`/`PlaceholderColor`（`NodeIcon` 没有真实贴图时，
  用这两个字段画一个可辨识的占位圆形，保证节点不会因为缺资源而不可见）。

- **`MapNodeVisualDatabase`**：按 **VisualId（字符串）** 注册/查询
  `MapNodeVisualDefinition` 的静态注册表，**不是**按 `MapNodeType` 注册。
  核心入口是 `Resolve(MapNode node)`，按优先级依次尝试：
  1. `node.FixedEventId`——具体事件自己的专属外观（藏宝阁、赤壁残骸、
     统治者雕像、载具店等以后都可以用各自的 `FixedEventId` 注册专属外观）。
  2. `node.Id`——单个节点实例的专属外观（比 FixedEventId 更细粒度，很少用到）。
  3. `DefaultVisualIdForType(node.Type)`——类型默认外观（"type_default_battle"
     这种统一命名约定生成的 key），当前六种既有类型
     （Battle/Event/Elite/Shop/Boss/Treasure）都各注册了一份占位默认值。

  这三层全部是普通的字典查询，`Resolve` 方法内部没有任何针对具体类型的
  if/switch 分支——`FixedEventId`/`Id`/类型默认值只是三个不同优先级的
  查询 key，不是"根据类型决定外观"的特殊代码路径。

## 交互流程

```
鼠标悬停在 MapNodeVisual 的 Area2D 上
        ↓
显示"点击进入"（仅当 Interactable = true）
        ↓
玩家鼠标左键点击（Area2D.InputEvent 信号，InputEventMouseButton）
        ↓
MapNodeVisual 发出 Interacted(nodeId)
        ↓
MapExplorationView 转发成 NodeSelected(nodeId)
        ↓
MapController 转发成自己的 NodeSelected(nodeId)（签名不变）
        ↓
MainFlow.OnNodeSelected（未改动）
```

`Interactable` 由 `MapExplorationView` 在生成节点时设置，直接沿用既有判断
（`GameManager.IsNodeUnlocked` 且未 `AllStagesCleared` 且未 `IsNodeCleared`），
`MapNodeVisual` 本身不重新计算这些条件。

## 以后如何新增一个地图节点外观

### 给某个具体事件配专属外观（推荐用法）

例如"统治者雕像"事件的 `FixedEventId` 是 `"event_ruler_statue"`（示例 ID，
以实际 EventDatabase 里的 ID 为准）：

```csharp
MapNodeVisualDatabase.Register("event_ruler_statue", new MapNodeVisualDefinition(
    nodeIcon: "map_ruler_statue_icon",   // 需要先在 SpriteDatabase 注册这张贴图
    glowEffect: "ruler_statue_glow",     // 预留字段，接入 Effect System 后生效
    scale: 1.3f,
    placeholderGlyph: "🗿",
    placeholderColor: new Color(0.6f, 0.6f, 0.65f),
    description: "统治者雕像事件，专属外观"));
```

只要某个 `MapNode` 的 `FixedEventId` 等于 `"event_ruler_statue"`，
`MapNodeVisual` 就会自动使用这份定义，即使它的 `Type` 是普通的 `Event`。

### 只想改某个类型的默认外观

```csharp
MapNodeVisualDatabase.Register(
    MapNodeVisualDatabase.DefaultVisualIdForType(MapNodeType.Shop),
    new MapNodeVisualDefinition(placeholderGlyph: "🛍️", description: "商店默认外观微调"));
```

不需要修改 `MapNodeVisual.cs`、`MapNodeVisualDatabase.Resolve`
或 `MapExplorationView.cs`——注册新的 VisualId 就会自动生效。

## 后续扩展（预留，这次不实现）

`MapNodeVisualDefinition` 已经为以下能力预留了字段，当前 `MapNodeVisual`
不会读取/播放它们：

- `IdleAnimation` / `InteractAnimation`（待机/交互动画）
- `GlowEffect` / `LightEffect`（发光/光照，例如 Boss Aura、藏宝阁光效）
- Particle / Shader / Floating Icon（尚未有对应字段，接入时再扩展）

真正接入时，应该在 `MapNodeVisual._Ready()`/`_Draw()` 里读取这些已经存在的
字段来播放，而不是重新设计一遍数据结构。
