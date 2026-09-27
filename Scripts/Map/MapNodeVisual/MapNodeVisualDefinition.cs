//////////////////////////////////////////////////////////
// 文件：Scripts/Map/MapNodeVisual/MapNodeVisualDefinition.cs
//
// 模块：Map System
//
// 为什么存在：
// 地图上的战斗/事件/商店/Boss/藏宝阁看起来应该长什么样，是表现层的事，
// 不应该写死在地图探索的场景代码里。MapNodeVisualDefinition 把这些数据
// 收敛成一份配置，MapNodeVisual 只负责“读取配置 + 显示”。
//
// 职责：
// 1. 描述一个地图节点类型对应的全部显示参数。
// 2. 让新增节点类型（藏宝阁/新 Boss/新事件）变成“新增一条 Definition”，
//    不需要修改 MapNodeVisual 或地图探索场景代码。
//
// 不负责：
// × 判断节点是否解锁/已通关（由 GameManager 负责）。
// × 决定进入哪个关卡（由 MainFlow/GameManager 负责）。
// × 实例化 Godot 节点（由 MapNodeVisual 负责）。
//
// 主要依赖：
// Godot.Vector2 / Godot.Color
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// 一个地图节点类型的完整显示参数集合。
///
/// 新增节点类型只需要新增一份 <see cref="MapNodeVisualDefinition"/> 并注册到
/// <see cref="MapNodeVisualDatabase"/>，不需要修改 <see cref="MapNodeVisual"/>
/// 或地图探索场景的任何代码。
/// </summary>
public sealed class MapNodeVisualDefinition
{
    /// <summary>
    /// 创建一份地图节点显示参数定义。
    /// </summary>
    public MapNodeVisualDefinition(
        string nodeIcon = "",
        string idleAnimation = "",
        string interactAnimation = "",
        string glowEffect = "",
        string lightEffect = "",
        float scale = 1f,
        Vector2 offset = default,
        int zIndex = 0,
        string description = "",
        string placeholderGlyph = "○",
        Color? placeholderColor = null,
        RenderLayer renderLayer = RenderLayer.WorldUI)
    {
        NodeIcon = nodeIcon;
        IdleAnimation = idleAnimation;
        InteractAnimation = interactAnimation;
        GlowEffect = glowEffect;
        LightEffect = lightEffect;
        Scale = scale;
        Offset = offset;
        ZIndex = zIndex;
        Description = description;
        PlaceholderGlyph = placeholderGlyph;
        PlaceholderColor = placeholderColor ?? new Color(0.7f, 0.7f, 0.7f);
        RenderLayer = renderLayer;
    }

    /// <summary>
    /// 节点图标的贴图资源 ID，应由 SpriteDatabase 解析。
    ///
    /// 留空时 <see cref="MapNodeVisual"/> 会退回到 <see cref="PlaceholderGlyph"/>/
    /// <see cref="PlaceholderColor"/> 画一个占位图形，不会导致节点不可见。
    /// </summary>
    public string NodeIcon { get; }

    /// <summary>待机动画 ID（预留，当前未接入真实动画系统）。</summary>
    public string IdleAnimation { get; }

    /// <summary>交互动画 ID（预留，当前未接入真实动画系统）。</summary>
    public string InteractAnimation { get; }

    /// <summary>发光效果 ID（预留，例如藏宝阁/Boss 光效，当前未接入）。</summary>
    public string GlowEffect { get; }

    /// <summary>光照效果 ID（预留，例如 Light2D 资源引用，当前未接入）。</summary>
    public string LightEffect { get; }

    /// <summary>节点图标的统一缩放（等比例）。</summary>
    public float Scale { get; }

    /// <summary>节点图标相对节点世界坐标的偏移。</summary>
    public Vector2 Offset { get; }

    /// <summary>
    /// 节点图标在其所属 RenderLayer 内部的相对 ZIndex 偏移（不是最终绝对值）。
    /// </summary>
    public int ZIndex { get; }

    /// <summary>
    /// 该节点所在的统一 Render Layer，默认 <see cref="global::RenderLayer.WorldUI"/>。
    ///
    /// 例如 Boss 雕像可以指定更高的层级压住普通地图装饰
    /// （装饰通常用 <see cref="global::RenderLayer.Ground"/>），不需要为地图节点
    /// 写死"如果是 Boss 就……"的特殊判断。
    /// </summary>
    public RenderLayer RenderLayer { get; }

    /// <summary>备注，方便在编辑器/代码里快速理解这条定义的用途，不参与显示逻辑。</summary>
    public string Description { get; }

    /// <summary>
    /// 占位符号：<see cref="NodeIcon"/> 没有对应贴图资源时显示的文字符号
    /// （例如 Boss 用 "☠"，事件用 "？"），保证没有美术资源时节点依然可辨识。
    /// </summary>
    public string PlaceholderGlyph { get; }

    /// <summary>
    /// 占位颜色：<see cref="NodeIcon"/> 没有对应贴图资源时使用的圆形填色。
    /// </summary>
    public Color PlaceholderColor { get; }
}
