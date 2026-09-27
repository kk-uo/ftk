# Map System（点击节点进入）

地图采用节点选择模式：玩家点击已解锁的节点即可直接进入，不需要控制任何角色
移动、不需要按键。地图节点的数据来源、解锁/通关判断完全沿用既有的
`GameManager`/`Scripts/EventSystem.cs`，这次改动只涉及地图怎么显示、怎么选中
节点，不改动地图生成算法本身。

历史说明：项目里一度存在"控制地图探索角色走动、靠近节点按 E 进入"的玩法
（`MapPlayer` + WASD 移动 + 碰撞 + E 键交互），后来经用户确认改回点击节点
即可进入——`MapPlayer.cs` 已删除，`MapNodeVisual` 的交互方式从"物理体进入
检测 + E 键"改成了"鼠标点击/悬停检测"，地图背景/城墙/节点图片等美术表现
保持不变。

## 整体设计

- 地图是整个屏幕：`MapController` 里地图铺满全屏（`FullRect` 锚点，100%），
  不再缩在小窗口里；返回菜单/调试/Run Buff/背包等 UI 以半透明浮层形式盖在
  地图上方，而不是地图嵌在 UI 布局流里。
- 地图区域固定尺寸（`MapExplorationView.ViewportWidth` ×
  `MapExplorationView.ViewportHeight`，和 project.godot 的基准分辨率一致），
  不跟随 Camera、不滚动。
- 地图上的战斗/事件/精英/商店/Boss/藏宝阁节点全部是 `MapNodeVisual`
  （`Scripts/Map/MapNodeVisual/`，详见该目录下的 README），外观通过
  VisualId 查询，不按 MapNodeType 写死判断；点击已解锁的节点直接发出
  `Interacted` 信号进入关卡。

## 文件结构

```
Scripts/
├── MapRunState.cs           // 地图运行状态：当前节点/访问与完成状态
└── Map/
    ├── MapExplorationView.cs    // 地图场景根节点：生成背景、地图节点
    ├── README.md                // 本文件
    └── MapNodeVisual/
        ├── MapNodeVisual.cs             // 地图节点世界对象：Area2D + 鼠标点击交互
        ├── MapNodeVisualDefinition.cs   // 单个节点类型的显示参数
        ├── MapNodeVisualDatabase.cs     // 节点类型 → 显示参数 注册表
        └── README.md                    // MapNodeVisual 子系统说明
```

配套场景：`Scenes/MapExploration.tscn`（`Node2D` 根节点，挂
`MapExplorationView.cs`）。

## MapRunState：地图运行状态

`Scripts/MapRunState.cs` 是一个静态类，保存"这一局地图进展到哪了"，
退出地图去打战斗/事件/商店/Boss 后，回到地图能恢复这些状态，而不是
重新开始。它不是场景节点、不是 UI 控件的临时字段，也不是第二套地图数据：

- `ChapterId`：当前地图所属章节。
- `CurrentNodeId`：**只读转发** `GameManager.CurrentNodeId`（MainFlow 早就
  通过 `GameManager.SetCurrentNode` 维护了这份权威状态，这里不重新存一份）。
- `VisitedNodeIds`：玩家曾经进入过的节点集合——全新状态，`GameManager`
  里没有对应概念，只属于地图表现层。
- `CompletedNodeIds`/`IsCompleted(nodeId)`：**只读视图**，实时从
  `GameManager.Nodes` + `GameManager.IsNodeCleared` 计算得到，不是独立维护
  的第二份"是否通关"数据——避免出现两份可能互相不一致的完成状态。

**不再记录任何地图坐标**——地图上没有可移动的角色，`PlayerPositionX`/
`PlayerPositionY`/`SavePlayerPosition`/`TryGetPlayerPosition` 这些字段/方法
已经随 `MapPlayer` 一并移除。

`MapExplorationView.OnNodeInteracted`（玩家点击节点真正进入时）会调用
`MapRunState.EnterNode(nodeId)`。`GameManager.ResetRunData`/
`AdvanceToNextChapter`/`DebugGoToChapter` 分别调用
`MapRunState.ResetForNewRun()`/`ResetForNewChapter(chapter)`，保证新 Run/
新章节不继承上一局的地图进度。

## MapNode（既有数据，未改动）

地图节点的类型（`MapNodeType`）和数据（`MapNode`：Id/Name/Type/StageIndex/
NextNodeIds 等）定义在 `Scripts/EventSystem.cs`，节点列表和解锁/通关判断
（`GameManager.Nodes`/`IsNodeUnlocked`/`IsNodeCleared`/`AllStagesCleared`）
在 `Scripts/GameManager.cs`——**这次改动完全没有修改这两个文件的地图生成/
解锁逻辑**，`MapExplorationView` 只是读取这些既有数据，为每个 `MapNode`
生成一个对应的 `MapNodeVisual` 世界对象。

## 与既有地图 UI（MapController）的整合

`Scripts/MapController.cs` 的 `BuildLayout()` 结构：

1. `BuildExplorationViewport()`——一个铺满全屏（`FullRect` 锚点）的
   `SubViewportContainer` + `SubViewport`，加载 `Scenes/MapExploration.tscn`，
   作为地图本体，第一个 `AddChild`，天然在最底层。
2. 一个 `MouseFilter = Ignore` 的 HUD 悬浮层，压在地图上方，包含：
   左上角信息面板（标题/当前角色/资源状态/Run Buff 面板，半透明背景）、
   右上角按钮（返回主菜单/调试）、底部居中提示（选择节点提示/全部通关提示+
   返回按钮）、右下角背包按钮。

`MapExplorationView` 的 `NodeSelected(nodeId)` 信号被原样转发成
`MapController.NodeSelected`，签名和原来完全一致，所以
`MainFlow.OnNodeSelected` **不需要任何改动**。

`MapController` 原有的调试窗口（解锁全部/变体/路线/Boss 方案）、
Run Buff 面板、返回主菜单、背包按钮、语言切换等功能全部保留，
只是从"占一整行的布局流"变成了"盖在地图上方的浮层"。

## 以后如何新增一种地图节点外观

不需要改这个目录下任何一个 `.cs` 文件，只需要在
`MapNodeVisualDatabase` 里注册一份新的 `MapNodeVisualDefinition`
（详见 `Scripts/Map/MapNodeVisual/README.md`）。

## 后续预留、这次不实现

- 地图背景当前是纯色 `ColorRect` 兜底 + 真实背景图片（`bg_map_city_ruins.png`
  等），以后有更多章节美术时按 `GetMapBackgroundTexturePath()` 的模式扩展即可。
