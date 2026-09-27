//////////////////////////////////////////////////////////
// 文件：Scripts/Map/MapExplorationView.cs
//
// 模块：Map System
//
// 为什么存在：
// 地图采用"点击已解锁节点直接进入"的节点选择模式：地图节点的数据来源
// （哪些节点、什么类型、是否解锁/通关）完全来自既有的
// GameManager.Nodes/IsNodeUnlocked/IsNodeCleared/AllStagesCleared，不应该
// 重新实现一遍地图生成或解锁判断。这个类只负责把既有数据摆到一个 2D 场景里，
// 生成一排 MapNodeVisual，并把"点击选中了哪个节点"对外暴露成和原来
// MapController 完全一样的 NodeSelected(nodeId) 信号。
//
// 职责：
// 1. 读取 GameManager.Nodes，为每个节点生成一个 MapNodeVisual（不改动
//    GameManager 的节点生成/解锁逻辑，只读取结果）。
// 2. 绘制铺满整个视口的地图背景美术，保持"大地图"表现。
// 3. 把 MapNodeVisual.Interacted 转发成 NodeSelected(nodeId)，供
//    MapController 转发给 MainFlow，签名和原来的按钮版本完全一致。
//
// 不负责：
// × 地图节点生成算法、解锁/通关判断（完全交给 GameManager）。
// × 决定进入关卡后具体发生什么（由 MainFlow.OnNodeSelected 负责，未改动）。
// × 地图周边的调试窗口、Run Buff 面板等 UI（仍由 MapController 负责，
//   这个类只负责被嵌入的 2D 探索区域本身）。
// × 地图角色移动/碰撞——这次改动已经整体移除，地图不再有可移动的玩家角色。
//
// 主要依赖：
// MapNodeVisual / MapNodeVisualDatabase / GameManager
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// 2D 地图场景的根节点脚本。
///
/// 被 <see cref="MapController"/> 通过 SubViewport 嵌入到既有地图 UI 中间；
/// 对外只暴露一个和原来按钮版本签名完全相同的 <see cref="NodeSelected"/> 信号，
/// 因此 MainFlow.OnNodeSelected 不需要任何改动。
/// </summary>
public partial class MapExplorationView : Node2D
{
    [Signal]
    public delegate void NodeSelectedEventHandler(string nodeId);

    /// <summary>
    /// 地图区域宽高（像素），和 project.godot 里配置的基准视口分辨率一致
    /// （window/size/viewport_width/height = 2560x1440），保证地图世界坐标
    /// 和其它全屏 Control UI 使用同一套坐标假设。
    /// </summary>
    public const int ViewportWidth = 2560;

    /// <summary>地图区域高度（像素）。</summary>
    public const int ViewportHeight = 1440;

    private const float NodeSpacing = 320f;
    private const float NodeZigzagY = 140f;
    private const string DefaultMapBackgroundTexturePath = "res://Assets/Backgrounds/Map/bg_map_city_ruins.png";
    private const string ChapterTwoMapBackgroundTexturePath = "res://Assets/Backgrounds/Map/bg_map_chapter2_city.png";
    private bool _nodeSelectionQueued;

    public override void _Ready()
    {
        BuildBackground();
        SpawnNodes();
    }

    private void BuildBackground()
    {
        // 纯色底板保留为图片加载失败时的兜底；真实背景放在其上方，
        // 使用 Cover 规则等比例铺满视口，允许边缘被 SubViewport 裁掉。
        var background = new ColorRect
        {
            Color = new Color(0.10f, 0.12f, 0.15f),
            Size = new Vector2(ViewportWidth, ViewportHeight),
            Position = Vector2.Zero,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = -100
        };
        AddChild(background);

        var backgroundPath = GetMapBackgroundTexturePath();
        var texture = LoadMapBackgroundTexture(backgroundPath);
        if (texture == null)
        {
            GD.PushWarning($"地图背景加载失败：{backgroundPath}");
            return;
        }

        var coverScale = System.MathF.Max(
            ViewportWidth / (float)texture.GetWidth(),
            ViewportHeight / (float)texture.GetHeight());
        var imageBackground = new Sprite2D
        {
            Name = "MapBackgroundImage",
            Texture = texture,
            Centered = true,
            Position = new Vector2(ViewportWidth / 2f, ViewportHeight / 2f),
            Scale = new Vector2(coverScale, coverScale),
            ZIndex = -99
        };
        AddChild(imageBackground);
    }

    private static Texture2D? LoadMapBackgroundTexture(string texturePath)
    {
        var texture = GD.Load<Texture2D>(texturePath);
        if (texture != null)
        {
            return texture;
        }

        return null;
    }

    private static string GetMapBackgroundTexturePath()
    {
        return GameManager.CurrentChapter == 2
            ? ChapterTwoMapBackgroundTexturePath
            : DefaultMapBackgroundTexturePath;
    }

    private const float HorizontalMargin = 220f;
    private const float BossColumnWidth = 260f;

    /// <summary>
    /// 固定阶段（战斗①②→事件①②→精英战）：显示目前为止的固定小链条，
    /// 行为不变（节点数量本来就不多，不会无限增长）。
    /// 自由探索阶段：只显示当前这一批（RegenerateChoices 已经把旧的一批整体移除了，
    /// 这里的 IsContinueExploring 过滤主要是防御性的，语义更清晰）+ 固定的 Boss。
    /// </summary>
    private static System.Collections.Generic.List<MapNode> GetVisibleNodes()
    {
        var all = GameManager.Nodes;
        if (!ContinueExploreManager.IsActive)
        {
            return new System.Collections.Generic.List<MapNode>(all);
        }

        var visible = new System.Collections.Generic.List<MapNode>();
        foreach (var n in all)
        {
            if (n.IsContinueExploring) visible.Add(n);
        }

        // 注意：不能在找到第一个 Boss 节点后 break——魏·双线征伐 会让同一章节出现两个
        // Type==Boss 的节点（主Boss + 额外Boss），如果 break，只有列表里排在前面的那个
        // 会显示，另一个在自由探索阶段会彻底不可见。这里改成把所有 Boss 节点都加入 visible。
        foreach (var n in all)
        {
            if (n.Type == MapNodeType.Boss) visible.Add(n);
        }

        return visible;
    }

    private void SpawnNodes()
    {
        var nodes = GetVisibleNodes();
        if (nodes.Count == 0)
        {
            return;
        }

        // Boss 始终固定在地图最右侧、独立于其它节点数量之外排布；其余节点按
        // 原有横向铺满+之字形偏移的规则排布，在自由探索阶段只有 WindowSize 个。
        //
        // 魏·双线征伐 会让同一章节里出现两个 Type==Boss 的节点（主Boss + 独立的额外Boss，
        // Id 形如 "boss_extra_ch1"）。这段原本假设"每章至多一个Boss节点"，用单个变量
        // 反复覆盖——如果不特判，先遍历到的那个Boss节点会被直接覆盖丢弃、完全不会渲染，
        // 而不是像非Boss节点那样进入 rowNodes。这里把"额外Boss"节点按普通节点一样
        // 加入 rowNodes（仍然是 Type==Boss，只是不占用最右侧的专属Boss位），
        // 只把真正的主Boss节点（非额外Boss Id）放进右侧专属位——不影响原有"每章一个主Boss"
        // 场景的渲染结果。
        var rowNodes = new System.Collections.Generic.List<MapNode>();
        MapNode? bossNode = null;
        foreach (var n in nodes)
        {
            if (n.Type == MapNodeType.Boss && !n.Id.StartsWith("boss_extra_ch", System.StringComparison.Ordinal))
                bossNode = n;
            else
                rowNodes.Add(n);
        }

        var available = ViewportWidth - HorizontalMargin * 2f - (bossNode != null ? BossColumnWidth : 0f);
        var spacing = rowNodes.Count > 1 ? System.Math.Min(NodeSpacing, available / (rowNodes.Count - 1)) : 0f;
        var centerY = ViewportHeight / 2f;

        for (var i = 0; i < rowNodes.Count; i++)
        {
            var x = HorizontalMargin + i * spacing;
            var y = centerY + (i % 2 == 0 ? -NodeZigzagY : NodeZigzagY);
            SpawnOneNode(rowNodes[i], new Vector2(x, y));
        }

        if (bossNode != null)
        {
            SpawnOneNode(bossNode, new Vector2(ViewportWidth - HorizontalMargin, centerY));
        }
    }

    private void SpawnOneNode(MapNode nodeData, Vector2 position)
    {
        var progressUnlocked = GameManager.IsNodeUnlocked(nodeData.Id)
                                && !GameManager.AllStagesCleared()
                                && !GameManager.IsNodeCleared(nodeData.Id);
        // 可负担性检查同样必须走 GameManager.GetNodeEnergyCost，与UI显示、实际扣费同源，
        // 避免"点击前显示能进，点击后却因为价格算法不同而被拦下/扣错"。
        var canAffordPower = GameManager.CanAffordPower(GameManager.GetNodeEnergyCost(nodeData));
        var interactable = progressUnlocked && canAffordPower;
        var insufficientPower = progressUnlocked && !canAffordPower;

        var visual = new MapNodeVisual();
        visual.Initialize(nodeData, interactable, MapController.GetNodeText(nodeData), insufficientPower);
        visual.Interacted += OnNodeInteracted;
        AddChild(visual);
        visual.Position = position;

        if (nodeData.Type == MapNodeType.Boss)
        {
            MainFlow.TryShowFirstTimeHint(FirstTimeHintManager.HintIds.FirstBossSighting);
        }

        // 只在自由探索阶段刷新节点时播放淡入：固定阶段每次回到地图都重建整条
        // 小链条，全部淡入会显得很吵，不需要动画。
        if (ContinueExploreManager.IsActive)
        {
            visual.PlaySpawnFadeIn();
        }
    }

    private void OnNodeInteracted(string nodeId)
    {
        if (_nodeSelectionQueued)
        {
            return;
        }

        _nodeSelectionQueued = true;

        // 记录这个节点被进入过（VisitedNodeIds），供以后可能的"走过的节点"展示复用；
        // CurrentNodeId 本身不在这里设置——MainFlow 收到 NodeSelected 后会调用
        // GameManager.SetCurrentNode，MapRunState.CurrentNodeId 直接读取那份数据。
        // 地图不再有可移动的玩家角色，这里不需要也不再保存任何地图坐标。
        MapRunState.EnterNode(nodeId);

        // Area2D 的 InputEvent 发生在 Viewport 拾取流程中；如果这里同步转场，
        // MainFlow.SwitchTo 会释放当前地图/SubViewport，Godot 仍在处理 picking 时
        // 访问已释放对象会触发原生崩溃。延后一帧只改变转发时机，不改变地图逻辑。
        CallDeferred(nameof(EmitNodeSelectedDeferred), nodeId);
    }

    private void EmitNodeSelectedDeferred(string nodeId)
    {
        EmitSignal(SignalName.NodeSelected, nodeId);
    }
}
