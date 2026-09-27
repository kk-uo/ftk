//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/PlayerDamageBorder.cs
//
// 模块：Presentation System
//
// 职责：
// 1. 绘制玩家实际受伤时的暗红屏幕边框。
// 2. 复用单一节点和单一 Tween，避免连续受伤时叠加闪屏。
// 3. 保持全屏覆盖、输入穿透、独立于 HUD 布局。
//
// 不负责：
// × 判断伤害是否成立。
// × 修改玩家生命值。
// × 控制 Camera、音效或战斗流程。
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// 玩家受伤屏幕边框。
///
/// 该控件只消费已经确认的实际扣血数值，绘制低亮度、低饱和的像素风暗红边缘；
/// 中央区域保持透明，不遮挡卡牌、敌人模型和主要战斗信息。
/// </summary>
public partial class PlayerDamageBorder : Control
{
    private const float MinAlpha = 0.20f;
    private const float LightDamageAlpha = 0.22f;
    private const float NormalDamageAlpha = 0.28f;
    private const float HeavyDamageAlpha = 0.36f;
    private const float MaxAlpha = 0.38f;

    private float _alpha;
    private Tween? _tween;

    /// <summary>
    /// 初始化全屏输入穿透控件。
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
    /// 播放一次玩家实际受伤边框脉冲。
    ///
    /// 连续受伤会从当前透明度继续提升并重启淡出，不会创建额外节点或叠加 Alpha。
    /// </summary>
    public void ShowDamage(int actualDamage, int maxHp)
    {
        if (actualDamage <= 0 || maxHp <= 0)
        {
            return;
        }

        var targetAlpha = Mathf.Max(_alpha, CalculateTargetAlpha(actualDamage, maxHp));
        Visible = true;
        _tween?.Kill();
        _tween = CreateTween();
        _tween.TweenMethod(
                Callable.From<float>(SetAlpha),
                _alpha,
                targetAlpha,
                0.10)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        _tween.TweenInterval(0.07);
        _tween.TweenMethod(
                Callable.From<float>(SetAlpha),
                targetAlpha,
                0.0f,
                0.42)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
        _tween.TweenCallback(Callable.From(() => Visible = false));
    }

    /// <summary>
    /// 返回当前绘制透明度，供回归测试确认动画结束后可归零。
    /// </summary>
    public float DebugAlpha => _alpha;

    private static float CalculateTargetAlpha(int actualDamage, int maxHp)
    {
        var ratio = actualDamage / (float)maxHp;
        var alpha = ratio < 0.10f
            ? LightDamageAlpha
            : ratio < 0.25f ? NormalDamageAlpha : HeavyDamageAlpha;
        return Mathf.Clamp(alpha, MinAlpha, MaxAlpha);
    }

    private void SetAlpha(float value)
    {
        _alpha = Mathf.Clamp(value, 0.0f, MaxAlpha);
        QueueRedraw();
    }

    /// <summary>
    /// 绘制像素块边框。
    ///
    /// 使用多层矩形而不是整屏遮罩，保证屏幕中心和主要 UI 保持可读。
    /// </summary>
    public override void _Draw()
    {
        if (_alpha <= 0.001f || Size.X <= 0 || Size.Y <= 0)
        {
            return;
        }

        var thickness = Mathf.Clamp(Mathf.Min(Size.X, Size.Y) * 0.12f, 86f, 150f);
        DrawSoftEdgeBands(thickness);
        DrawDarkCorners(thickness);
        DrawPixelBreaks(thickness);
        DrawSparseScanlines(thickness);
    }

    private void DrawSoftEdgeBands(float thickness)
    {
        const int bandCount = 7;
        for (var i = 0; i < bandCount; i++)
        {
            var t0 = i / (float)bandCount;
            var t1 = (i + 1) / (float)bandCount;
            var band = thickness / bandCount;
            var alphaScale = Mathf.Pow(1.0f - t0, 1.55f);
            var color = new Color(0.29f, 0.047f, 0.070f, _alpha * 0.72f * alphaScale);

            DrawRect(new Rect2(0, thickness * t0, Size.X, band), color);
            DrawRect(new Rect2(0, Size.Y - thickness * t1, Size.X, band), color);
            DrawRect(new Rect2(thickness * t0, 0, band, Size.Y), color);
            DrawRect(new Rect2(Size.X - thickness * t1, 0, band, Size.Y), color);
        }
    }

    private void DrawDarkCorners(float thickness)
    {
        var color = new Color(0.15f, 0.027f, 0.039f, _alpha * 0.95f);
        var size = thickness * 1.12f;

        DrawRect(new Rect2(0, 0, size, size), color);
        DrawRect(new Rect2(Size.X - size, 0, size, size), color);
        DrawRect(new Rect2(0, Size.Y - size, size, size), color);
        DrawRect(new Rect2(Size.X - size, Size.Y - size, size, size), color);
    }

    private void DrawPixelBreaks(float thickness)
    {
        var accent = new Color(0.44f, 0.082f, 0.105f, _alpha * 0.82f);
        var dark = new Color(0.15f, 0.027f, 0.039f, _alpha * 0.68f);
        var block = Mathf.Max(8f, Mathf.Round(Size.X / 160f) * 2f);

        DrawRect(new Rect2(Size.X * 0.08f, 18f, block * 6f, block), accent);
        DrawRect(new Rect2(Size.X * 0.19f, 34f, block * 3f, block), dark);
        DrawRect(new Rect2(Size.X * 0.77f, 22f, block * 8f, block), accent);
        DrawRect(new Rect2(Size.X * 0.66f, Size.Y - 32f, block * 5f, block), accent);
        DrawRect(new Rect2(Size.X * 0.14f, Size.Y - 48f, block * 7f, block), dark);

        DrawRect(new Rect2(22f, Size.Y * 0.18f, block, block * 7f), accent);
        DrawRect(new Rect2(38f, Size.Y * 0.63f, block, block * 5f), dark);
        DrawRect(new Rect2(Size.X - 30f, Size.Y * 0.28f, block, block * 8f), accent);
        DrawRect(new Rect2(Size.X - 46f, Size.Y * 0.68f, block, block * 5f), dark);
    }

    private void DrawSparseScanlines(float thickness)
    {
        var color = new Color(0.44f, 0.082f, 0.105f, _alpha * 0.26f);
        for (var y = 14f; y < thickness; y += 24f)
        {
            DrawLine(new Vector2(0, y), new Vector2(Size.X, y), color, 1f);
            DrawLine(new Vector2(0, Size.Y - y), new Vector2(Size.X, Size.Y - y), color, 1f);
        }
    }
}
