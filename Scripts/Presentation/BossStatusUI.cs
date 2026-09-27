//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/BossStatusUI.cs
//
// 模块：Presentation System
//
// 职责：
// 1. 显示固定在战斗顶部中央的 Boss 状态 UI。
// 2. 展示 Boss 血条、费用与 Buff 图标。
// 3. 暴露整块 UI 的 Hover/Input 区域，由 TooltipPresenter 与 SelectionPresenter 统一处理。
//
// 不负责：
// × Boss 模型摆放。
// × 普通敌人模型绑定状态条。
// × 修改生命、费用、Buff 或任何战斗规则。
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// Battle 顶部 Boss 状态 UI。
///
/// Boss 的状态 UI 与模型完全解耦，始终固定在屏幕顶部中央；
/// 普通敌人仍使用绑定在模型附近的 <see cref="EnemyStatusUI"/>。
/// </summary>
public partial class BossStatusUI : Control
{
    private const int HpBarWidth = 920;
    private const int HpBarHeight = 34;
    private const int ManaCircleSize = 48;
    private const int BuffIconSize = 24;
    private const int BuffSpacing = 6;

    private ProgressBar? _hpBar;
    private Label? _hpLabel;
    private EnemyManaOrb? _manaOrb;
    private HBoxContainer? _buffRow;
    private BattleUnit? _unit;
    private Tween? _highlightTween;
    private Color _highlightColor = new(1.00f, 0.92f, 0.12f);
    private float _highlightAlpha;
    // 生命值超过上限（如临时生命值效果）时为 true：血条不再只显示"满血"，
    // 改用醒目的溢出色，具体数值仍由 _hpLabel 的原始文本（未做上限裁剪）呈现。
    private bool _isOverflowing;

    /// <summary>当前绑定的 Boss 单位。</summary>
    public BattleUnit? Unit => _unit;

    /// <summary>
    /// 构建 Boss 状态 UI。
    ///
    /// 这里不使用 Panel 背景，避免遮挡 Battle Stage 的 UI Skin。
    /// </summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(HpBarWidth, 112);
        Size = CustomMinimumSize;
        BuildLayout();
    }

    /// <summary>
    /// 用当前 Boss 状态刷新血条、费用与 Buff。
    /// </summary>
    public void Refresh(BattleUnit unit, BattleContext? context)
    {
        _unit = unit;

        if (_hpBar != null)
        {
            _hpBar.MaxValue = System.Math.Max(1, unit.MaxHP);
            _hpBar.Value = System.Math.Clamp(unit.CurrentHP, 0, unit.MaxHP);
            _isOverflowing = unit.CurrentHP > unit.MaxHP;
            ApplyHpBarStyle();
        }

        if (_hpLabel != null)
        {
            _hpLabel.Text = $"{unit.CurrentHP} / {unit.MaxHP}";
        }

        _manaOrb?.SetValue(BattleRules.FormatMana(unit.Resource));
        RefreshBuffIcons(unit, context);
    }

    /// <summary>
    /// 设置 Boss 目标高亮。
    ///
    /// 高亮只作用于血条边框与费用圆环，不高亮整块区域。
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
        _hpBar = new ProgressBar
        {
            Position = new Vector2(0, 8),
            Size = new Vector2(HpBarWidth, HpBarHeight),
            MinValue = 0,
            MaxValue = 1,
            Value = 1,
            ShowPercentage = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _hpBar.AddThemeStyleboxOverride("fill", BattleUiSkin.CreateEnemyHpFillStyle());
        AddChild(_hpBar);
        var pixelSegments = new PixelHpSegmentOverlay { SegmentCount = 16 };
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
        _hpLabel.AddThemeFontSizeOverride("font_size", 24);
        _hpLabel.AddThemeColorOverride("font_color", Colors.White);
        _hpLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _hpLabel.AddThemeConstantOverride("outline_size", 4);
        _hpBar.AddChild(_hpLabel);

        _manaOrb = new EnemyManaOrb
        {
            Position = new Vector2((HpBarWidth - ManaCircleSize) * 0.5f, 52),
            Size = new Vector2(ManaCircleSize, ManaCircleSize),
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(_manaOrb);

        _buffRow = new HBoxContainer
        {
            Position = new Vector2((HpBarWidth - 360) * 0.5f, 86),
            Size = new Vector2(360, BuffIconSize),
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center
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
        // 图标内容统一由 StatusIconDatabase 提供（贴图/文字回退、Boss档位尺寸），
        // 这里只负责套上战斗皮肤的科技外框。
        var frame = BattleUiSkin.CreateIconFrame(StatusIconDatabase.GetPixelSize(StatusIconVariant.Boss));
        var content = StatusIconDatabase.CreateIconControl(status, StatusIconVariant.Boss);
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

        // 溢出血量（如临时生命值效果）：血条不再显示为普通红色满血，
        // 改用醒目的金色，提示玩家当前生命值已超过上限（具体数值看 _hpLabel）。
        _hpBar.AddThemeStyleboxOverride(
            "fill",
            BattleUiSkin.CreateEnemyHpFillStyle(_isOverflowing));
    }
}
