//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/EnemyStatusUI.cs
//
// 模块：Presentation System
//
// 职责：
// 1. 显示绑定在敌人模型上的轻量状态 UI。
// 2. 只展示费用、生命条与 Buff 图标。
// 3. 暴露整块状态 UI 的 Hover/Input 区域，详细 Tooltip 由 TooltipPresenter 统一处理。
//
// 不负责：
// × 修改敌人生命、费用或 Buff。
// × 判断技能、Trigger 或伤害结算。
// × Boss 专属顶部 UI。
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// 绑定在普通敌人模型附近的透明状态 UI。
///
/// 该组件只读取 <see cref="BattleUnit"/> 的展示数据并刷新控件，
/// 不持有任何战斗规则，后续可直接被正式 EnemyPresenter 复用。
/// </summary>
public partial class EnemyStatusUI : Control
{
    private const int ManaCircleSize = 42;
    private const int HpBarWidth = 220;
    private const int HpBarHeight = 26;
    private const int BuffIconSize = 24;
    private const int BuffSpacing = 6;
    private const int HoverPadding = 10;

    private EnemyManaOrb? _manaOrb;
    private ProgressBar? _hpBar;
    private Label? _hpLabel;
    private HBoxContainer? _buffRow;
    private BattleUnit? _unit;
    private Tween? _highlightTween;
    private Color _highlightColor = new(0.22f, 0.88f, 1.00f);
    private float _highlightAlpha;

    /// <summary>当前绑定的战斗单位，用于 SelectionPresenter 查询高亮颜色。</summary>
    public BattleUnit? Unit => _unit;

    /// <summary>
    /// 状态 UI 的稳定 Hover 区域。
    ///
    /// Godot 的 Control 在 Node2D 层级下经过动态定位后，视觉子控件与父控件
    /// 的实际鼠标命中区域容易出现细小偏差；几何兜底统一使用这个带 padding
    /// 的矩形，避免第二、三章多敌人战斗中必须精确移到某几个像素才触发 Hover。
    /// </summary>
    public Rect2 GlobalHoverRect
    {
        get
        {
            var rect = GetGlobalRect();
            rect.Position -= new Vector2(HoverPadding, HoverPadding);
            rect.Size += new Vector2(HoverPadding * 2, HoverPadding * 2);
            return rect;
        }
    }

    /// <summary>
    /// Presentation System 的公开入口：_Ready。
    ///
    /// 构建透明状态 UI；这里不创建 Panel/ColorRect，避免污染 Battle Stage。
    /// </summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(ManaCircleSize + 10 + HpBarWidth, 74);
        Size = CustomMinimumSize;
        BuildLayout();
    }

    /// <summary>
    /// 按当前战斗单位状态刷新费用、血条与 Buff 图标。
    ///
    /// 调用者负责决定这个 UI 绑定到哪个模型、放在什么位置。
    /// </summary>
    public void Refresh(BattleUnit unit, BattleContext? context)
    {
        _unit = unit;

        if (_manaOrb != null)
        {
            _manaOrb.SetValue(BattleRules.FormatMana(unit.Resource));
        }

        if (_hpBar != null)
        {
            _hpBar.MaxValue = System.Math.Max(1, unit.MaxHP);
            _hpBar.Value = System.Math.Clamp(unit.CurrentHP, 0, unit.MaxHP);
            ApplyHpBarStyle();
        }

        if (_hpLabel != null)
        {
            _hpLabel.Text = $"{unit.CurrentHP} / {unit.MaxHP}";
        }

        RefreshBuffIcons(unit, context);
    }

    /// <summary>
    /// 设置状态 UI 高亮。
    ///
    /// 高亮只作用于费用圆环与血条边框，Buff 图标保持原样。
    /// </summary>
    public void SetHighlighted(Color color, bool highlighted)
    {
        _highlightColor = color;
        _highlightTween?.Kill();
        _highlightTween = CreateTween();
        _highlightTween.TweenMethod(
            Callable.From<float>(SetHighlightAlpha),
            _highlightAlpha,
            highlighted ? 1.0f : 0.0f,
            highlighted ? 0.08 : 0.14);
    }

    private void BuildLayout()
    {
        _manaOrb = new EnemyManaOrb
        {
            Position = Vector2.Zero,
            Size = new Vector2(ManaCircleSize, ManaCircleSize),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_manaOrb);

        _hpBar = new ProgressBar
        {
            Position = new Vector2(ManaCircleSize + 10, 8),
            Size = new Vector2(HpBarWidth, HpBarHeight),
            MinValue = 0,
            MaxValue = 1,
            Value = 1,
            ShowPercentage = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _hpBar.AddThemeStyleboxOverride("fill", BattleUiSkin.CreateEnemyHpFillStyle());
        AddChild(_hpBar);
        var pixelSegments = new PixelHpSegmentOverlay();
        pixelSegments.SetAnchorsPreset(LayoutPreset.FullRect);
        _hpBar.AddChild(pixelSegments);
        ApplyHpBarStyle();

        _hpLabel = new Label
        {
            Text = "0 / 0",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _hpLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        _hpLabel.AddThemeFontSizeOverride("font_size", 18);
        _hpLabel.AddThemeColorOverride("font_color", Colors.White);
        _hpLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _hpLabel.AddThemeConstantOverride("outline_size", 3);
        _hpBar.AddChild(_hpLabel);

        _buffRow = new HBoxContainer
        {
            Position = new Vector2(ManaCircleSize + 10, 42),
            Size = new Vector2(HpBarWidth, BuffIconSize),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _buffRow.AddThemeConstantOverride("separation", BuffSpacing);
        AddChild(_buffRow);
    }

    private void RefreshBuffIcons(BattleUnit unit, BattleContext? context)
    {
        if (_buffRow == null)
        {
            return;
        }

        foreach (var child in _buffRow.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var status in BattleUnitUiFormatter.GetStatuses(unit, context))
        {
            _buffRow.AddChild(CreateBuffIcon(status));
        }
    }

    private static Control CreateBuffIcon(BattleUnitStatusEntry status)
    {
        // 图标内容统一由 StatusIconDatabase 提供（贴图/文字回退、普通档位尺寸），
        // 这里只负责套上战斗皮肤的科技外框。
        var frame = BattleUiSkin.CreateIconFrame(StatusIconDatabase.GetPixelSize(StatusIconVariant.Normal));
        var content = StatusIconDatabase.CreateIconControl(status, StatusIconVariant.Normal);
        content.SetAnchorsPreset(LayoutPreset.FullRect);
        frame.AddChild(content);
        return frame;
    }

    private void SetHighlightAlpha(float alpha)
    {
        _highlightAlpha = alpha;
        ApplyHpBarStyle();
        _manaOrb?.SetHighlight(_highlightColor, _highlightAlpha);
    }

    private void ApplyHpBarStyle()
    {
        if (_hpBar == null)
        {
            return;
        }

        _hpBar.AddThemeStyleboxOverride(
            "background",
            BattleUiSkin.CreateEnemyHpBackgroundStyle(_highlightColor, _highlightAlpha));
    }
}

/// <summary>
/// 敌人状态 UI 的费用圆形图标。
///
/// 使用自绘圆形避免 Panel/ColorRect，让 Battle Stage 只出现必要信息。
/// </summary>
public partial class EnemyManaOrb : Control
{
    private string _value = "0";
    private Color _highlightColor = new(0.48f, 0.82f, 1f, 0.95f);
    private float _highlightAlpha;

    /// <summary>
    /// 设置圆形资源图标内显示的费用数值。
    /// </summary>
    public void SetValue(string value)
    {
        _value = value;
        QueueRedraw();
    }

    /// <summary>
    /// 设置费用圆环高亮颜色与强度。
    /// </summary>
    public void SetHighlight(Color color, float alpha)
    {
        _highlightColor = color;
        _highlightAlpha = alpha;
        QueueRedraw();
    }

    /// <summary>
    /// 绘制费用圆与居中数字。
    /// </summary>
    public override void _Draw()
    {
        var center = Size * 0.5f;
        var radius = System.MathF.Min(Size.X, Size.Y) * 0.5f - 2f;
        DrawCircle(center, radius, new Color(0.04f, 0.07f, 0.09f, 0.78f));
        DrawArc(center, radius, 0, Mathf.Tau, 48, new Color(0.48f, 0.82f, 1f, 0.95f), 2.5f);
        if (_highlightAlpha > 0.01f)
        {
            DrawArc(
                center,
                radius + 2f,
                0,
                Mathf.Tau,
                56,
                new Color(_highlightColor.R, _highlightColor.G, _highlightColor.B, _highlightAlpha),
                4.0f);
        }

        var font = ThemeDB.FallbackFont;
        const int fontSize = 22;
        var textSize = font.GetStringSize(_value, HorizontalAlignment.Center, -1, fontSize);
        DrawString(
            font,
            center + new Vector2(-textSize.X * 0.5f, textSize.Y * 0.35f),
            _value,
            HorizontalAlignment.Center,
            -1,
            fontSize,
            Colors.White);
    }
}
