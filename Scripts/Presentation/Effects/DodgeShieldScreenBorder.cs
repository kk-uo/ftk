//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/Effects/DodgeShieldScreenBorder.cs
//
// 模块：Presentation System / Effect System
//
// 职责：
// 1. 玩家使用【闪】成功抵挡攻击时绘制全屏边缘护盾。
// 2. 使用低透明六边形单元表现护盾结构，并保持屏幕中央透明。
// 3. 复用单一节点和 Tween，避免连续格挡时叠加特效。
//
// 不负责：
// × 判断伤害是否被【闪】抵挡。
// × 修改伤害、护盾或战斗状态。
// × 播放其它来源的通用格挡表现。
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// 玩家【闪】成功时使用的全屏边缘护盾表现。
///
/// 节点固定在独立 CanvasLayer 中，只绘制屏幕边缘的半透明六边形，
/// 不跟随 Camera、不参与 HUD 布局，也不拦截鼠标输入。
/// </summary>
public sealed partial class DodgeShieldScreenBorder : Control
{
    private const float FadeInDuration = 0.10f;
    private const float HoldDuration = 0.08f;
    private const float FadeOutDuration = 0.88f;
    private const float MaximumIntensity = 1.0f;

    private float _intensity;
    private Tween? _tween;

    /// <summary>
    /// 初始化全屏输入穿透节点。
    /// </summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        OffsetLeft = 0;
        OffsetTop = 0;
        OffsetRight = 0;
        OffsetBottom = 0;
        Visible = false;
    }

    /// <summary>
    /// 播放一次边缘护盾脉冲。
    ///
    /// 连续触发时从当前强度平滑恢复，不创建额外节点或叠加透明度。
    /// </summary>
    public void ShowShield()
    {
        Visible = true;
        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenMethod(
                Callable.From<float>(SetIntensity),
                _intensity,
                MaximumIntensity,
                FadeInDuration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        _tween.TweenInterval(HoldDuration);
        _tween.TweenMethod(
                Callable.From<float>(SetIntensity),
                MaximumIntensity,
                0.0f,
                FadeOutDuration)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        _tween.TweenCallback(Callable.From(() => Visible = false));
    }

    /// <summary>
    /// 返回当前护盾强度，供回归测试确认节点被复用且动画能够归零。
    /// </summary>
    public float DebugIntensity => _intensity;

    private void SetIntensity(float value)
    {
        _intensity = Mathf.Clamp(value, 0.0f, MaximumIntensity);
        QueueRedraw();
    }

    /// <summary>
    /// 绘制屏幕边缘的蜂巢护盾单元。
    ///
    /// 三层六边形向内覆盖更宽的外围区域，中央仍不覆盖颜色，避免影响卡牌、模型和结算文字。
    /// </summary>
    public override void _Draw()
    {
        if (_intensity <= 0.001f || Size.X <= 0f || Size.Y <= 0f)
        {
            return;
        }

        var radius = Mathf.Clamp(Mathf.Min(Size.X, Size.Y) * 0.026f, 24f, 38f);
        DrawHorizontalShieldRows(radius);
        DrawVerticalShieldColumns(radius);
        DrawCornerAccents(radius);
    }

    private void DrawHorizontalShieldRows(float radius)
    {
        var spacing = radius * 1.72f;
        var columns = Mathf.CeilToInt(Size.X / spacing) + 2;

        for (var row = 0; row < 3; row++)
        {
            var inset = radius * (0.72f + row * 1.48f);
            var alphaScale = row switch
            {
                0 => 1.0f,
                1 => 0.62f,
                _ => 0.30f
            };
            var offset = row % 2 == 0 ? 0f : spacing * 0.5f;

            for (var column = -1; column < columns; column++)
            {
                var x = column * spacing + offset;
                DrawShieldCell(new Vector2(x, inset), radius, alphaScale);
                DrawShieldCell(new Vector2(x, Size.Y - inset), radius, alphaScale);
            }
        }
    }

    private void DrawVerticalShieldColumns(float radius)
    {
        var spacing = radius * 1.72f;
        var rows = Mathf.CeilToInt(Size.Y / spacing) + 2;

        for (var column = 0; column < 3; column++)
        {
            var inset = radius * (0.72f + column * 1.48f);
            var alphaScale = column switch
            {
                0 => 1.0f,
                1 => 0.62f,
                _ => 0.30f
            };
            var offset = column % 2 == 0 ? spacing * 0.5f : 0f;

            for (var row = -1; row < rows; row++)
            {
                var y = row * spacing + offset;
                DrawShieldCell(new Vector2(inset, y), radius, alphaScale);
                DrawShieldCell(new Vector2(Size.X - inset, y), radius, alphaScale);
            }
        }
    }

    private void DrawShieldCell(Vector2 center, float radius, float alphaScale)
    {
        var points = CreateHexagon(center, radius);
        var fill = new Color(0.22f, 0.72f, 0.78f, 0.085f * _intensity * alphaScale);
        var edge = new Color(0.46f, 0.91f, 0.92f, 0.40f * _intensity * alphaScale);

        DrawColoredPolygon(points, fill);
        DrawPolyline(ClosePolygon(points), edge, 2.0f, false);
    }

    private void DrawCornerAccents(float radius)
    {
        var color = new Color(0.76f, 0.88f, 0.68f, 0.24f * _intensity);
        var length = radius * 2.2f;
        var margin = radius * 0.55f;

        DrawLine(new Vector2(margin, margin), new Vector2(margin + length, margin), color, 3f, false);
        DrawLine(new Vector2(margin, margin), new Vector2(margin, margin + length), color, 3f, false);
        DrawLine(new Vector2(Size.X - margin, margin), new Vector2(Size.X - margin - length, margin), color, 3f, false);
        DrawLine(new Vector2(Size.X - margin, margin), new Vector2(Size.X - margin, margin + length), color, 3f, false);
        DrawLine(new Vector2(margin, Size.Y - margin), new Vector2(margin + length, Size.Y - margin), color, 3f, false);
        DrawLine(new Vector2(margin, Size.Y - margin), new Vector2(margin, Size.Y - margin - length), color, 3f, false);
        DrawLine(new Vector2(Size.X - margin, Size.Y - margin), new Vector2(Size.X - margin - length, Size.Y - margin), color, 3f, false);
        DrawLine(new Vector2(Size.X - margin, Size.Y - margin), new Vector2(Size.X - margin, Size.Y - margin - length), color, 3f, false);
    }

    private static Vector2[] CreateHexagon(Vector2 center, float radius)
    {
        var points = new Vector2[6];
        for (var index = 0; index < points.Length; index++)
        {
            var angle = Mathf.DegToRad(60f * index + 30f);
            points[index] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        return points;
    }

    private static Vector2[] ClosePolygon(Vector2[] points)
    {
        var closed = new Vector2[points.Length + 1];
        for (var index = 0; index < points.Length; index++)
        {
            closed[index] = points[index];
        }

        closed[^1] = points[0];
        return closed;
    }
}
