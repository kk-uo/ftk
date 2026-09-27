//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/Effects/BlockEffectVisual.cs
//
// 模块：Presentation System / Effect System
//
// 为什么存在：
// 完全格挡需要比文字更直接的瞬时反馈，但不值得为初版效果引入额外贴图、
// 粒子资源或逐武器配置。本组件用 Godot 自绘生成克制的护盾闪光，并保持
// 与具体格挡来源无关，让桃、技能和装备可以共享同一套表现。
//
// 职责：
// 1. 在受击目标中心绘制护盾轮廓、冲击环和少量碎光。
// 2. 播放短促的缩放、旋转与淡出动画。
// 3. 动画结束后自动释放临时节点。
//
// 不负责：
// × 判断伤害是否被完全格挡。
// × 修改生命、护盾、Buff 或战斗结果。
// × 显示格挡文字或战斗日志。
//
// 主要依赖：
// Godot Control / Tween
// RenderLayerManager
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// 完全格挡的程序化瞬时特效。
///
/// 特效挂在统一 SkillEffect 层，不参与目标节点布局，也不会拦截鼠标输入。
/// 使用自绘而不是固定尺寸贴图，可以根据玩家或敌人模型的包围盒自动缩放。
/// </summary>
internal sealed partial class BlockEffectVisual : Control
{
    private const float MinimumDiameter = 150f;
    private const float MaximumDiameter = 310f;
    private const float TargetScaleRatio = 1.08f;
    private const float FadeInDuration = 0.06f;
    private const float ImpactDuration = 0.09f;
    private const float HoldDuration = 0.11f;
    private const float FadeOutDuration = 0.16f;

    private static readonly Color ShieldFillColor = new(0.18f, 0.72f, 0.92f, 0.10f);
    private static readonly Color ShieldEdgeColor = new(0.50f, 0.94f, 1.00f, 0.94f);
    private static readonly Color ShieldInnerColor = new(0.20f, 0.70f, 0.90f, 0.72f);
    private static readonly Color SparkColor = new(0.68f, 0.96f, 1.00f, 0.82f);

    /// <summary>
    /// 在目标锚点上播放一次格挡特效。
    ///
    /// 锚点无效或已经离开场景树时静默返回，确保表现缺失不会影响战斗结算。
    /// </summary>
    internal static void Play(Control? anchor)
    {
        if (anchor == null || !GodotObject.IsInstanceValid(anchor) || !anchor.IsInsideTree())
        {
            return;
        }

        var tree = anchor.GetTree();
        if (tree == null)
        {
            return;
        }

        var targetRect = anchor.GetGlobalRect();
        var targetExtent = Mathf.Max(targetRect.Size.X, targetRect.Size.Y);
        var diameter = Mathf.Clamp(targetExtent * TargetScaleRatio, MinimumDiameter, MaximumDiameter);
        var center = targetRect.Size.LengthSquared() > 1f
            ? targetRect.GetCenter()
            : anchor.GlobalPosition;

        var visual = new BlockEffectVisual
        {
            Name = "BlockEffectVisual",
            MouseFilter = MouseFilterEnum.Ignore,
            Size = new Vector2(diameter, diameter),
            PivotOffset = new Vector2(diameter, diameter) * 0.5f,
            Scale = new Vector2(0.72f, 0.72f),
            RotationDegrees = -4f,
            Modulate = new Color(1f, 1f, 1f, 0f)
        };

        var layer = RenderLayerManager.GetOrCreateCanvasLayer(tree, RenderLayer.SkillEffect);
        layer.AddChild(visual);
        visual.GlobalPosition = center - visual.Size * 0.5f;

        var tween = tree.CreateTween();
        tween.SetParallel(true);
        tween.TweenProperty(visual, "modulate:a", 1.0f, FadeInDuration);
        tween.TweenProperty(visual, "scale", new Vector2(1.06f, 1.06f), ImpactDuration)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        tween.TweenProperty(visual, "rotation_degrees", 2.0f, ImpactDuration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);

        tween.SetParallel(false);
        tween.TweenProperty(visual, "scale", Vector2.One, 0.05f)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        tween.TweenInterval(HoldDuration);

        tween.SetParallel(true);
        tween.TweenProperty(visual, "modulate:a", 0.0f, FadeOutDuration);
        tween.TweenProperty(visual, "scale", new Vector2(1.18f, 1.18f), FadeOutDuration)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);

        tween.Finished += () =>
        {
            if (GodotObject.IsInstanceValid(visual))
            {
                visual.QueueFree();
            }
        };
    }

    /// <summary>
    /// 绘制低透明度护盾面、双层轮廓和离散碎光。
    ///
    /// 线段刻意保持短小，避免覆盖伤害数字、敌人血条和出招结果。
    /// </summary>
    public override void _Draw()
    {
        var center = Size * 0.5f;
        var radius = Mathf.Min(Size.X, Size.Y) * 0.36f;
        var shield = new[]
        {
            center + new Vector2(0f, -radius),
            center + new Vector2(radius * 0.78f, -radius * 0.56f),
            center + new Vector2(radius * 0.68f, radius * 0.35f),
            center + new Vector2(0f, radius),
            center + new Vector2(-radius * 0.68f, radius * 0.35f),
            center + new Vector2(-radius * 0.78f, -radius * 0.56f)
        };

        DrawColoredPolygon(shield, ShieldFillColor);
        DrawPolyline(ClosePolygon(shield), ShieldEdgeColor, 4f, false);

        var innerRadius = radius * 0.72f;
        DrawArc(center, innerRadius, -2.64f, -0.50f, 18, ShieldInnerColor, 3f, false);
        DrawArc(center, innerRadius, 0.50f, 2.64f, 18, ShieldInnerColor, 3f, false);
        DrawLine(
            center + new Vector2(-innerRadius * 0.36f, 0f),
            center + new Vector2(-innerRadius * 0.08f, innerRadius * 0.26f),
            ShieldEdgeColor,
            4f,
            false);
        DrawLine(
            center + new Vector2(-innerRadius * 0.08f, innerRadius * 0.26f),
            center + new Vector2(innerRadius * 0.42f, -innerRadius * 0.30f),
            ShieldEdgeColor,
            4f,
            false);

        DrawArc(center, radius * 1.18f, -2.78f, -1.88f, 8, SparkColor, 3f, false);
        DrawArc(center, radius * 1.18f, -1.22f, -0.32f, 8, SparkColor, 3f, false);
        DrawArc(center, radius * 1.18f, 0.36f, 1.02f, 6, SparkColor, 3f, false);
        DrawArc(center, radius * 1.18f, 2.12f, 2.74f, 6, SparkColor, 3f, false);

        DrawCircle(center + new Vector2(-radius * 1.15f, -radius * 0.18f), 3f, SparkColor);
        DrawCircle(center + new Vector2(radius * 1.12f, radius * 0.12f), 3f, SparkColor);
        DrawCircle(center + new Vector2(radius * 0.42f, -radius * 1.16f), 2.5f, SparkColor);
    }

    private static Vector2[] ClosePolygon(Vector2[] polygon)
    {
        var closed = new Vector2[polygon.Length + 1];
        for (var index = 0; index < polygon.Length; index++)
        {
            closed[index] = polygon[index];
        }

        if (polygon.Length > 0)
        {
            closed[^1] = polygon[0];
        }

        return closed;
    }
}
