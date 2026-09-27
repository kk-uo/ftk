//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/LowHealthScreenBorder.cs
//
// 模块：Presentation System
//
// 职责：
// 1. 在玩家低血量时绘制全屏赛博朋克风格警戒边框。
// 2. 使用纯表现层动画提示危险状态。
// 3. 保持 MouseFilter.Ignore，不拦截任何战斗输入。
//
// 不负责：
// × 判断战斗胜负。
// × 修改玩家生命值。
// × 修改 BattleResolver、Trigger 或伤害结算。
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// 低血量屏幕边框表现。
///
/// 该控件只根据外部传入的低血量状态绘制视觉反馈，不持有任何战斗规则。
/// </summary>
public partial class LowHealthScreenBorder : Control
{
    private const float FadeSpeed = 7.5f;
    private const float BorderThickness = 4.0f;
    private const float InnerBorderInset = 18.0f;
    private const float CornerLength = 92.0f;
    private const float ScanlineSpacing = 18.0f;

    private float _targetIntensity;
    private float _intensity;
    private float _time;

    /// <summary>
    /// Presentation System 的公开入口：_Ready。
    ///
    /// 初始化为全屏、输入穿透的纯绘制控件。
    /// </summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
        Visible = false;
    }

    /// <summary>
    /// 根据玩家生命值更新警戒强度。
    ///
    /// 10 点及以下开始显示；生命越低，边框越明显。
    /// </summary>
    public void SetPlayerHealth(int currentHealth)
    {
        if (currentHealth <= 0 || currentHealth > 10)
        {
            _targetIntensity = 0.0f;
            return;
        }

        var danger = 1.0f - currentHealth / 10.0f;
        _targetIntensity = Mathf.Lerp(0.14f, 0.28f, danger);
        Visible = true;
    }

    /// <summary>
    /// 平滑更新低血量边框淡入淡出和扫描线脉冲。
    /// </summary>
    public override void _Process(double delta)
    {
        _time += (float)delta;
        _intensity = Mathf.Lerp(_intensity, _targetIntensity, (float)delta * FadeSpeed);

        if (_intensity <= 0.01f && _targetIntensity <= 0.0f)
        {
            Visible = false;
            return;
        }

        Visible = true;
        QueueRedraw();
    }

    /// <summary>
    /// 绘制外框、角标、科技感断线与边缘扫描光。
    /// </summary>
    public override void _Draw()
    {
        if (_intensity <= 0.01f)
        {
            return;
        }

        var rect = new Rect2(Vector2.Zero, Size);
        var pulse = 0.86f + 0.14f * Mathf.Sin(_time * 3.2f);
        var red = new Color(0.29f, 0.047f, 0.070f, 0.24f * _intensity * pulse);
        var hotRed = new Color(0.44f, 0.082f, 0.105f, 0.48f * _intensity * pulse);
        var magenta = new Color(0.55f, 0.06f, 0.22f, 0.22f * _intensity);
        var cyan = new Color(0.10f, 0.62f, 0.68f, 0.16f * _intensity);

        DrawRect(rect, red, false, BorderThickness);

        var inner = rect.Grow(-InnerBorderInset);
        DrawCornerBrackets(inner, hotRed);
        DrawTechBreaks(inner, magenta, cyan);
        DrawEdgeScanlines(rect, hotRed, cyan);
    }

    private void DrawCornerBrackets(Rect2 rect, Color color)
    {
        var left = rect.Position.X;
        var right = rect.End.X;
        var top = rect.Position.Y;
        var bottom = rect.End.Y;

        DrawLine(new Vector2(left, top), new Vector2(left + CornerLength, top), color, BorderThickness);
        DrawLine(new Vector2(left, top), new Vector2(left, top + CornerLength), color, BorderThickness);

        DrawLine(new Vector2(right, top), new Vector2(right - CornerLength, top), color, BorderThickness);
        DrawLine(new Vector2(right, top), new Vector2(right, top + CornerLength), color, BorderThickness);

        DrawLine(new Vector2(left, bottom), new Vector2(left + CornerLength, bottom), color, BorderThickness);
        DrawLine(new Vector2(left, bottom), new Vector2(left, bottom - CornerLength), color, BorderThickness);

        DrawLine(new Vector2(right, bottom), new Vector2(right - CornerLength, bottom), color, BorderThickness);
        DrawLine(new Vector2(right, bottom), new Vector2(right, bottom - CornerLength), color, BorderThickness);
    }

    private void DrawTechBreaks(Rect2 rect, Color magenta, Color cyan)
    {
        var yTop = rect.Position.Y + 10f;
        var yBottom = rect.End.Y - 10f;
        var xLeft = rect.Position.X + 10f;
        var xRight = rect.End.X - 10f;

        DrawLine(new Vector2(rect.Position.X + 160f, yTop), new Vector2(rect.Position.X + 250f, yTop), cyan, 2f);
        DrawLine(new Vector2(rect.End.X - 250f, yTop), new Vector2(rect.End.X - 160f, yTop), magenta, 2f);
        DrawLine(new Vector2(rect.Position.X + 220f, yBottom), new Vector2(rect.Position.X + 330f, yBottom), magenta, 2f);
        DrawLine(new Vector2(rect.End.X - 330f, yBottom), new Vector2(rect.End.X - 220f, yBottom), cyan, 2f);

        DrawLine(new Vector2(xLeft, rect.Position.Y + 180f), new Vector2(xLeft, rect.Position.Y + 250f), cyan, 2f);
        DrawLine(new Vector2(xRight, rect.End.Y - 250f), new Vector2(xRight, rect.End.Y - 180f), magenta, 2f);
    }

    private void DrawEdgeScanlines(Rect2 rect, Color red, Color cyan)
    {
        var offset = Mathf.PosMod(_time * 72f, ScanlineSpacing);
        var topHeight = 80f;
        var bottomStart = rect.Size.Y - 80f;

        for (var y = -ScanlineSpacing + offset; y < topHeight; y += ScanlineSpacing)
        {
            var alphaScale = 1f - y / topHeight;
            DrawLine(new Vector2(0, y), new Vector2(rect.Size.X, y), new Color(red.R, red.G, red.B, red.A * alphaScale), 1f);
        }

        for (var y = bottomStart + offset; y < rect.Size.Y + ScanlineSpacing; y += ScanlineSpacing)
        {
            var alphaScale = (y - bottomStart) / 80f;
            DrawLine(new Vector2(0, y), new Vector2(rect.Size.X, y), new Color(cyan.R, cyan.G, cyan.B, cyan.A * alphaScale), 1f);
        }
    }
}
