//////////////////////////////////////////////////////////
// 文件：Scripts/CharacterStatusCard.cs
//
// 模块：Character System
//
// 职责：
// 1. 承载角色定义、角色选择与角色展示相关代码。
// 2. 为其它模块提供清晰、稳定的调用边界。
// 3. 保持本文件内的状态变化可追踪、可调试。
//
// 不负责：
// × 处理无关模块的业务规则。
// × 绕过既有 Manager 或 Trigger 流程直接改写跨系统状态。
// × 在数据定义层混入表现层细节。
//
// 主要依赖：
// Godot / C# Runtime
// 项目内对应 Manager、Database 与 Trigger 系统
//////////////////////////////////////////////////////////

using Godot;
using System.Collections.Generic;

// Unified display card for all battle units (player, enemy, boss).
// Internal layout is defined here — BattleManager may only call data methods.
// To change any visual: edit the constants at the top or ApplyStyle(); never set layout from BattleManager.
/// <summary>
/// Character System 的公开类：CharacterStatusCard。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class CharacterStatusCard : PanelContainer
{
    // ── Layout constants ────────────────────────────────────────────────────
    private const int ManaCircleSize = 70;
    private const int ColumnSeparation = 8;
    private const int NameFontSize = 17;
    private const int HpFontSize = 30;
    private const int ManaFontSize = 34;
    private const int StateFontSize = 12;
    private const int BadgeSize = 24;
    private const int BadgeFontSize = 13;
    private const int BadgeSeparation = 6;
    // ────────────────────────────────────────────────────────────────────────

    private PanelContainer? _avatarPanel;
    private Label? _avatarLabel;
    private TextureRect? _avatarTexture;
    private Label? _nameLabel;
    private Label? _hpLabel;
    private ProgressBar? _hpBar;
    private Label? _manaLabel;
    private Label? _stateLabel;
    private HBoxContainer? _statusArea;
    private Color _linkedHighlightColor = new(1f, 0.22f, 0.20f);
    private float _linkedHighlightAlpha;
    private Tween? _linkedHighlightTween;

    public BattleUnit? Unit { get; private set; }
    public BattleTeam Team { get; private set; }
    public int MaxStatusBadges { get; set; } = 6;
    public bool EnableInlineTooltips { get; set; } = true;

    // Anchors used by BattleManager.Popups for floating text
    public Control AvatarAnchor => _avatarPanel ?? this;
    public Control ManaAnchor => (Control?)_manaLabel ?? this;

    /// <summary>
    /// Character System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
        BuildLayout();
    }

    private void BuildLayout()
    {
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.MouseFilter = MouseFilterEnum.Ignore;
        root.AddThemeConstantOverride("separation", ColumnSeparation);
        AddChild(root);

        _nameLabel = MakeExpandLabel(Localization.Get("battle.unit.name_default"), NameFontSize);
        _nameLabel.MouseFilter = EnableInlineTooltips ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _nameLabel.MouseEntered += () =>
        {
            if (!EnableInlineTooltips) return;
            var t = _nameLabel.Text;
            if (!string.IsNullOrWhiteSpace(t)) TooltipManager.Show(t, _nameLabel);
        };
        _nameLabel.MouseExited += () => { if (EnableInlineTooltips) TooltipManager.Hide(); };
        root.AddChild(_nameLabel);

        _hpBar = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = 1,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 34),
            MouseFilter = MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        root.AddChild(_hpBar);

        _hpLabel = new Label
        {
            Text = FormatHpValue(0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _hpLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        _hpLabel.AddThemeFontSizeOverride("font_size", HpFontSize);
        _hpLabel.AddThemeColorOverride("font_color", Colors.White);
        _hpLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _hpLabel.AddThemeConstantOverride("outline_size", 5);
        _hpBar.AddChild(_hpLabel);

        var bottomRow = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        bottomRow.AddThemeConstantOverride("separation", 14);
        root.AddChild(bottomRow);

        _avatarPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(ManaCircleSize, ManaCircleSize),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        _avatarPanel.MouseFilter = MouseFilterEnum.Ignore;
        bottomRow.AddChild(_avatarPanel);

        _avatarLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _avatarLabel.AddThemeFontSizeOverride("font_size", ManaFontSize);
        _avatarLabel.AddThemeColorOverride("font_color", Colors.White);
        _avatarLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _avatarLabel.AddThemeConstantOverride("outline_size", 5);
        _avatarPanel.AddChild(_avatarLabel);

        // 真实头像贴图叠在 _avatarLabel 上面。所有可选玩家角色均由
        // CharacterVisualDatabase 统一提供头像；敌人继续使用 _avatarLabel 的文字/emoji。
        _avatarTexture = new TextureRect
        {
            ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Visible = false
        };
        _avatarTexture.SetAnchorsPreset(LayoutPreset.FullRect);
        _avatarTexture.MouseFilter = MouseFilterEnum.Ignore;
        _avatarPanel.AddChild(_avatarTexture);

        var statusColumn = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        statusColumn.MouseFilter = MouseFilterEnum.Ignore;
        statusColumn.AddThemeConstantOverride("separation", 6);
        bottomRow.AddChild(statusColumn);

        _manaLabel = MakeExpandLabel(Localization.GetFmt("battle.unit.mp_fmt", 0), ManaFontSize);
        _stateLabel = MakeExpandLabel(Localization.Get("battle.unit.state_default"), StateFontSize);

        _stateLabel.MouseFilter = EnableInlineTooltips ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _stateLabel.MouseEntered += () =>
        {
            if (!EnableInlineTooltips) return;
            var t = _stateLabel.Text;
            if (!string.IsNullOrWhiteSpace(t)) TooltipManager.Show(t, _stateLabel);
        };
        _stateLabel.MouseExited += () => { if (EnableInlineTooltips) TooltipManager.Hide(); };

        _manaLabel.Visible = false;
        _stateLabel.Visible = false;
        statusColumn.AddChild(_manaLabel);
        statusColumn.AddChild(_stateLabel);

        _statusArea = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Begin,
            CustomMinimumSize = new Vector2(0, BadgeSize),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter
        };
        _statusArea.MouseFilter = MouseFilterEnum.Ignore;
        _statusArea.AddThemeConstantOverride("separation", BadgeSeparation);
        statusColumn.AddChild(_statusArea);
    }

    private static Label MakeExpandLabel(string text, int fontSize)
    {
        var label = new Label
        {
            Text = text,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    // ── Public data API ─────────────────────────────────────────────────────

    /// <summary>
    /// Character System 的公开入口：Refresh。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Refresh(
        BattleUnit unit,
        BattleContext? context,
        BattleTeam team,
        bool isSelected,
        string stateText)
    {
        Unit = unit;
        Team = team;
        var isDead = unit.IsDead;

        ApplyStyle(team, isSelected, isDead);

        if (_avatarLabel != null)
            _avatarLabel.Text = isDead ? "☠" : BattleRules.FormatMana(unit.Resource);

        RefreshAvatarPortrait(unit, isDead);

        var nameText = isDead ? $"{unit.Name} ☠" : unit.Name;
        if (_nameLabel != null)
        {
            _nameLabel.Text = nameText;
        }

        if (_hpLabel != null)
            _hpLabel.Text = isDead ? Localization.Get("battle.unit.hp_dead") : FormatHpValue(unit.CurrentHP, unit.MaxHP);

        if (_hpBar != null)
        {
            _hpBar.MaxValue = System.Math.Max(1, unit.MaxHP);
            _hpBar.Value = System.Math.Clamp(unit.CurrentHP, 0, unit.MaxHP);
            _hpBar.TooltipText = isDead
                ? Localization.Get("battle.unit.hp_dead")
                : Localization.GetFmt("battle.unit.hp_fmt", unit.CurrentHP, unit.MaxHP);
        }

        if (_manaLabel != null)
            _manaLabel.Text = Localization.GetFmt("battle.unit.mp_fmt", BattleRules.FormatMana(unit.Resource));

        if (_stateLabel != null)
        {
            _stateLabel.Text = stateText;
        }

        RefreshStatusBadges(unit, context);
    }

    /// <summary>
    /// 设置与敌人模型联动的状态高亮。
    ///
    /// 只高亮血条边框和费用圆环，不高亮整张卡片。
    /// </summary>
    public void SetLinkedHighlight(Color color, bool highlighted)
    {
        _linkedHighlightColor = color;
        _linkedHighlightTween?.Kill();
        _linkedHighlightTween = CreateTween();
        _linkedHighlightTween.TweenMethod(
            Callable.From<float>(SetLinkedHighlightAlpha),
            _linkedHighlightAlpha,
            highlighted ? 1.0f : 0.0f,
            highlighted ? 0.08 : 0.14);
    }

    private void RefreshAvatarPortrait(BattleUnit unit, bool isDead)
    {
        if (_avatarTexture == null || _avatarLabel == null)
        {
            return;
        }

        _avatarTexture.Visible = false;
        _avatarLabel.Visible = true;
    }

    private static string FormatHpValue(int currentHp, int maxHp)
    {
        return $"{currentHp}/{maxHp}";
    }

    // ── Style ────────────────────────────────────────────────────────────────

    private void ApplyStyle(BattleTeam team, bool isSelected, bool isDead)
    {
        var panelTint = isDead
            ? new Color(0.52f, 0.52f, 0.56f, 0.72f)
            : isSelected ? new Color(1.0f, 0.93f, 0.68f) : Colors.White;
        AddThemeStyleboxOverride("panel", BattleUiSkin.CreatePanelStyle(12, panelTint));

        if (_avatarPanel != null)
        {
            var ringColor = GetLinkedOrDefaultBorderColor(team, isSelected);
            var ringAlpha = _linkedHighlightAlpha > 0.01f || isSelected ? 1f : 0f;
            _avatarPanel.AddThemeStyleboxOverride(
                "panel",
                BattleUiSkin.CreateManaCircleStyle(ringColor, ringAlpha));
        }

        if (_hpBar != null)
        {
            _hpBar.AddThemeStyleboxOverride(
                "background",
                BattleUiSkin.CreateHpBackgroundStyle(_linkedHighlightColor, _linkedHighlightAlpha));
            _hpBar.AddThemeStyleboxOverride("fill", BattleUiSkin.CreateHpFillStyle(isDead));
        }
    }

    private void SetLinkedHighlightAlpha(float alpha)
    {
        _linkedHighlightAlpha = alpha;
        ApplyStyle(Team, false, Unit?.IsDead == true);
    }

    private Color GetLinkedOrDefaultBorderColor(BattleTeam team, bool isSelected)
    {
        if (_linkedHighlightAlpha > 0.01f)
        {
            return new Color(_linkedHighlightColor.R, _linkedHighlightColor.G, _linkedHighlightColor.B, _linkedHighlightAlpha);
        }

        return team == BattleTeam.Enemy
            ? (isSelected ? new Color(0.98f, 0.84f, 0.32f, 0.95f) : new Color(0.72f, 0.18f, 0.18f, 0.55f))
            : new Color(0.25f, 0.78f, 0.50f, 0.62f);
    }

    // ── Status badges ────────────────────────────────────────────────────────

    private void RefreshStatusBadges(BattleUnit unit, BattleContext? context)
    {
        if (_statusArea == null) return;
        if (EnableInlineTooltips)
        {
            TooltipManager.HideImmediate();
        }
        foreach (var child in _statusArea.GetChildren())
        {
            // 同一帧可能因伤害、费用和出牌栏更新连续刷新多次。QueueFree 只会在
            // 帧末释放节点，因此必须先移出 HBoxContainer，避免旧图标继续撑大 HUD。
            _statusArea.RemoveChild(child);
            child.QueueFree();
        }

        var statuses = BattleUnitUiFormatter.GetStatuses(unit, context);
        var equipments = BattleUnitUiFormatter.GetEquipments(unit);
        var max = MaxStatusBadges;
        var total = statuses.Count + equipments.Count;
        var visibleItemLimit = total > max ? System.Math.Max(0, max - 1) : max;
        var shown = 0;

        foreach (var entry in statuses)
        {
            if (shown >= visibleItemLimit) break;
            _statusArea.AddChild(MakeStatusBadge(entry, EnableInlineTooltips));
            shown++;
        }
        foreach (var eq in equipments)
        {
            if (shown >= visibleItemLimit) break;
            _statusArea.AddChild(MakeEquipmentBadge(eq, EnableInlineTooltips));
            shown++;
        }

        if (total > max)
        {
            var statShown = System.Math.Min(visibleItemLimit, statuses.Count);
            var eqShown = shown - statShown;
            var overflow = BattleUnitUiFormatter.BuildOverflowTooltip(
                statuses, equipments, statShown, eqShown, total - visibleItemLimit);
            _statusArea.AddChild(MakeMoreBadge(total - visibleItemLimit, overflow, EnableInlineTooltips));
        }
    }

    private static Control MakeStatusBadge(BattleUnitStatusEntry entry, bool enableTooltip)
    {
        // 图标内容统一由 StatusIconDatabase 提供（贴图/文字回退、普通档位尺寸）；
        // 这里只负责套皮肤外框和 Hover Tooltip（文案不变，仍显示名称/描述/持续时间）。
        var tt = BattleUnitUiFormatter.BuildStatusTooltip(entry);
        var frame = BattleUiSkin.CreateIconFrame(StatusIconDatabase.GetPixelSize(StatusIconVariant.Normal));
        frame.MouseFilter = enableTooltip ? MouseFilterEnum.Pass : MouseFilterEnum.Ignore;
        frame.MouseEntered += () => { if (enableTooltip) TooltipManager.Show(tt, frame); };
        frame.MouseExited += () => { if (enableTooltip) TooltipManager.Hide(); };
        var content = StatusIconDatabase.CreateIconControl(entry, StatusIconVariant.Normal);
        content.SetAnchorsPreset(LayoutPreset.FullRect);
        frame.AddChild(content);
        return frame;
    }

    private static Control MakeEquipmentBadge(
        (string Name, string Rarity, string Type, string Description) eq,
        bool enableTooltip)
    {
        var icon = eq.Name.Length > 0 ? eq.Name[..1] : "装";
        var tt = $"[{eq.Rarity}] {eq.Name}\n{eq.Type}\n{eq.Description}";
        var frame = BattleUiSkin.CreateIconFrame(BadgeSize);
        frame.MouseFilter = enableTooltip ? MouseFilterEnum.Pass : MouseFilterEnum.Ignore;
        var label = new Label
        {
            Text = icon,
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.SetAnchorsPreset(LayoutPreset.FullRect);
        frame.MouseEntered += () => { if (enableTooltip) TooltipManager.Show(tt, frame); };
        frame.MouseExited += () => { if (enableTooltip) TooltipManager.Hide(); };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color(0.95f, 0.82f, 0.38f));
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 3);
        frame.AddChild(label);
        return frame;
    }

    private static Control MakeMoreBadge(int overflowCount, string tooltip, bool enableTooltip)
    {
        var frame = BattleUiSkin.CreateIconFrame(BadgeSize);
        frame.MouseFilter = enableTooltip ? MouseFilterEnum.Pass : MouseFilterEnum.Ignore;
        var label = new Label
        {
            Text = "…",
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.SetAnchorsPreset(LayoutPreset.FullRect);
        frame.MouseEntered += () => { if (enableTooltip) TooltipManager.Show(tooltip, frame); };
        frame.MouseExited += () => { if (enableTooltip) TooltipManager.Hide(); };
        label.AddThemeFontSizeOverride("font_size", BadgeFontSize);
        label.AddThemeColorOverride("font_color", new Color(0.70f, 0.72f, 0.78f));
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 3);
        frame.AddChild(label);
        return frame;
    }
}
