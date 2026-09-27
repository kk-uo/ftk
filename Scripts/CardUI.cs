//////////////////////////////////////////////////////////
// 文件：Scripts/CardUI.cs
//
// 模块：Card System
//
// 职责：
// 1. 承载卡牌定义、卡牌 UI 与卡牌规则相关代码。
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

/// <summary>
/// Card System 的公开类：CardUI。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class CardUI : PanelContainer
{
    [Signal]
    public delegate void CardPressedEventHandler(CardUI cardUi);

    private const int CardTitleFontSize = 24;
    private const int CardDescriptionFontSize = 18;
    // 名称最多2行：配合 AutowrapMode 让过长名称换行而不是把卡牌撑宽，超过2行截断加省略号。
    // 描述不限行数，但放在一个裁剪容器里自动换行，超出卡牌剩余空间的部分被裁掉，
    // 从而保证所有卡牌（含英文/角色专属技能卡）的宽高完全一致。
    private const int NameMaxLines = 2;

    private Control? _contentRoot;
    private Label? _nameLabel;
    private Label? _hotkeyLabel;
    private Label? _costLabel;
    private Label? _typeLabel;
    private Label? _descriptionLabel;
    private Label? _damageBadgeLabel;
    private Color _accentColor = new(0.78f, 0.20f, 0.18f);
    private bool _isPlayable = true;
    private string? _costTextOverride;
    private string? _descriptionTextOverride;
    private string _hotkeyText = string.Empty;
    private DamagePreviewResult? _damagePreview;

    public Card? CardData { get; private set; }
    public bool IsPlayable => _isPlayable;

    /// <summary>
    /// Card System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        BuildLayout();
        Refresh();
    }

    /// <summary>
    /// Card System 的公开入口：_GuiInput。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _GuiInput(InputEvent @event)
    {
        if (!_isPlayable)
        {
            return;
        }

        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            EmitSignal(SignalName.CardPressed, this);
            AcceptEvent();
        }
    }

    /// <summary>
    /// Card System 的公开入口：SetCard。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void SetCard(Card card)
    {
        CardData = card;
        _costTextOverride = null;
        _descriptionTextOverride = null;
        _accentColor = card.Type switch
        {
            CardType.Kill => new Color(0.78f, 0.16f, 0.13f),
            CardType.FireKill => new Color(0.92f, 0.34f, 0.10f),
            CardType.ThunderKill => new Color(0.58f, 0.26f, 0.92f),
            CardType.SureKill => new Color(0.88f, 0.18f, 0.26f),
            CardType.ArrowBarrage => new Color(0.72f, 0.40f, 0.12f),
            CardType.NanmanInvasion => new Color(0.80f, 0.24f, 0.10f),
            CardType.Tuxi => new Color(0.18f, 0.62f, 0.68f),
            CardType.Dodge => new Color(0.10f, 0.42f, 0.78f),
            CardType.Peach => new Color(0.86f, 0.30f, 0.50f),
            CardType.Wine => new Color(0.62f, 0.16f, 0.18f),
            CardType.Steal => new Color(0.80f, 0.58f, 0.18f),
            CardType.Unassailable => new Color(0.20f, 0.56f, 0.82f),
            CardType.Fee => new Color(0.10f, 0.52f, 0.28f),
            _ => Colors.Black
        };

        Refresh();
    }

    /// <summary>
    /// Card System 的公开入口：SetDisplayOverride。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void SetDisplayOverride(string? costText, string? descriptionText)
    {
        _costTextOverride = costText;
        _descriptionTextOverride = descriptionText;
        Refresh();
    }

    /// <summary>
    /// Card System 的公开入口：SetPlayable。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void SetPlayable(bool playable)
    {
        _isPlayable = playable;
        MouseDefaultCursorShape = playable ? CursorShape.PointingHand : CursorShape.Arrow;
        MouseFilter = playable ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        Modulate = Colors.White;
        if (_contentRoot != null)
        {
            _contentRoot.Modulate = playable ? Colors.White : new Color(1f, 1f, 1f, 0.52f);
        }
        Refresh();
    }

    /// <summary>
    /// Card System 的公开入口：SetHotkeyText。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void SetHotkeyText(string text)
    {
        _hotkeyText = text;
        if (_hotkeyLabel != null)
        {
            _hotkeyLabel.Text = _hotkeyText;
            _hotkeyLabel.Visible = !string.IsNullOrWhiteSpace(_hotkeyText);
        }
    }

    // 四行固定布局：名称 / 费用 / 类型 / 效果。所有基础卡牌使用同一份布局与同一个尺寸，禁止任何卡牌单独定制宽度。
    private void BuildLayout()
    {
        CustomMinimumSize = new Vector2(152, 246);
        ClipContents = false;
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;

        var box = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Begin,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _contentRoot = box;
        _contentRoot.Modulate = _isPlayable ? Colors.White : new Color(1f, 1f, 1f, 0.52f);
        box.AddThemeConstantOverride("separation", 9);
        AddChild(box);

        CreateShortcutOverlay();
        CreateDamageBadge();

        _nameLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MaxLinesVisible = NameMaxLines,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        _nameLabel.AddThemeColorOverride("font_color", Colors.Black);
        _nameLabel.AddThemeFontSizeOverride("font_size", CardTitleFontSize);
        box.AddChild(_nameLabel);

        _costLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _costLabel.AddThemeColorOverride("font_color", Colors.Black);
        _costLabel.AddThemeFontSizeOverride("font_size", CardDescriptionFontSize);
        box.AddChild(_costLabel);

        var divider = new ColorRect
        {
            CustomMinimumSize = new Vector2(0, 4)
        };
        divider.SetMeta("is_divider", true);
        box.AddChild(divider);

        _typeLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _typeLabel.AddThemeColorOverride("font_color", Colors.Black);
        _typeLabel.AddThemeFontSizeOverride("font_size", CardDescriptionFontSize);
        box.AddChild(_typeLabel);

        // 描述放进一个普通 Control 裁剪区：Control 不会把锚定子节点的最小尺寸上报给
        // VBoxContainer，所以再长的描述也不会把卡牌撑高，超出部分直接被裁掉。
        var descriptionArea = new Control
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ClipContents = true,
            MouseFilter = MouseFilterEnum.Ignore
        };
        box.AddChild(descriptionArea);

        _descriptionLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _descriptionLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        _descriptionLabel.AddThemeColorOverride("font_color", Colors.Black);
        _descriptionLabel.AddThemeFontSizeOverride("font_size", CardDescriptionFontSize);
        descriptionArea.AddChild(_descriptionLabel);
    }

    private void CreateShortcutOverlay()
    {
        var overlay = new Control
        {
            Name = "ShortcutOverlay",
            MouseFilter = MouseFilterEnum.Ignore,
            ClipContents = false,
            ZIndex = 100,
            CustomMinimumSize = Vector2.Zero
        };
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        _hotkeyLabel = new Label
        {
            Name = "ShortcutLabel",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 101,
            Visible = !string.IsNullOrWhiteSpace(_hotkeyText)
        };
        _hotkeyLabel.AnchorLeft = 0.5f;
        _hotkeyLabel.AnchorRight = 0.5f;
        _hotkeyLabel.AnchorTop = 0f;
        _hotkeyLabel.AnchorBottom = 0f;
        _hotkeyLabel.OffsetLeft = -24;
        _hotkeyLabel.OffsetRight = 24;
        _hotkeyLabel.OffsetTop = -42;
        _hotkeyLabel.OffsetBottom = -14;
        _hotkeyLabel.Text = _hotkeyText;
        _hotkeyLabel.AddThemeColorOverride("font_color", Colors.White);
        _hotkeyLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _hotkeyLabel.AddThemeFontSizeOverride("font_size", 22);
        _hotkeyLabel.AddThemeConstantOverride("outline_size", 4);
        overlay.AddChild(_hotkeyLabel);
    }

    // 卡面右上角悬浮的最终伤害角标：MouseFilter=Pass 既能独立于卡牌可打出状态接收
    // 悬停事件（不可打出的卡也要能看预估），又会把点击事件继续传给卡牌根节点，
    // 不会挡住正常出牌点击。
    private void CreateDamageBadge()
    {
        var overlay = new Control
        {
            Name = "DamageBadgeOverlay",
            MouseFilter = MouseFilterEnum.Pass,
            ClipContents = false,
            ZIndex = 102,
            Visible = false
        };
        overlay.AnchorLeft = 1f;
        overlay.AnchorRight = 1f;
        overlay.AnchorTop = 0f;
        overlay.AnchorBottom = 0f;
        overlay.OffsetLeft = -46;
        overlay.OffsetRight = -2;
        overlay.OffsetTop = 2;
        overlay.OffsetBottom = 26;
        AddChild(overlay);

        overlay.MouseEntered += OnDamageBadgeMouseEntered;
        overlay.MouseExited += OnDamageBadgeMouseExited;

        _damageBadgeLabel = new Label
        {
            Name = "DamageBadgeLabel",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _damageBadgeLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        _damageBadgeLabel.AddThemeFontSizeOverride("font_size", 20);
        overlay.AddChild(_damageBadgeLabel);
    }

    /// <summary>
    /// Card System 的公开入口：SetDamagePreview。
    ///
    /// 由 BattleManager.Rendering 在每次刷新手牌时调用，传入 DamagePreviewService
    /// 的结果。角标始终展示计算后的最终伤害数值；非伤害牌或功能关闭时传 null，角标隐藏。
    /// </summary>
    public void SetDamagePreview(DamagePreviewResult? result)
    {
        _damagePreview = result;
        if (_damageBadgeLabel?.GetParent() is not Control overlay)
        {
            return;
        }

        if (result == null || !result.IsDamageCard)
        {
            overlay.Visible = false;
            return;
        }

        overlay.Visible = true;
        // PredictedFinalDamage 已经过完整的伤害修正管线计算（技能、装备、Buff、目标减伤等）。
        // 卡面只给出清晰的最终数值；命中前的卡牌对抗说明保留在悬停详情中，不用问号污染卡面。
        // 最终伤害角标只展示可直接阅读的数字。统一黑色避免被卡牌属性色、
        // 禁用透明度或 Emoji 字形影响辨识，也不再使用剑形装饰符号。
        _damageBadgeLabel.Text = result.PredictedFinalDamage.ToString();
        _damageBadgeLabel.AddThemeColorOverride("font_color", Colors.Black);
    }

    private void OnDamageBadgeMouseEntered()
    {
        if (_damagePreview == null || !_damagePreview.IsDamageCard || _damageBadgeLabel?.GetParent() is not Control overlay)
        {
            return;
        }

        TooltipManager.ShowRich(BuildDamagePreviewTooltip(_damagePreview), overlay);
    }

    private void OnDamageBadgeMouseExited()
    {
        TooltipManager.Hide();
    }

    private static TooltipContent BuildDamagePreviewTooltip(DamagePreviewResult result)
    {
        var lines = new System.Collections.Generic.List<string>();
        if (result.IsMultiTarget)
        {
            foreach (var target in result.TargetBreakdown)
            {
                var line = $"{target.TargetName}：{target.Amount}";
                if (!string.IsNullOrEmpty(target.Note))
                {
                    line += $"（{target.Note}）";
                }
                lines.Add(line);
            }
            lines.Add(string.Empty);
        }

        lines.AddRange(result.Breakdown);

        if (result.FollowUpDamage != null)
        {
            lines.Add($"{result.FollowUpDamage.SourceLabel}：{result.FollowUpDamage.Amount}");
        }

        foreach (var warning in result.Warnings)
        {
            lines.Add(warning);
        }

        lines.Add(Localization.Get("damage_preview.tooltip.uncertain_note"));

        return new TooltipContent(
            string.Join("\n", lines),
            title: Localization.Get("damage_preview.tooltip.title"));
    }

    private void Refresh()
    {
        ApplyPanelStyle();

        if (_nameLabel == null || _costLabel == null || _typeLabel == null || _descriptionLabel == null || CardData == null)
        {
            return;
        }

        // 名称统一走 AutowrapMode 自动换行（上限 NameMaxLines 行），任何语言/任何
        // 专属技能卡都不再需要按卡种写特殊分行逻辑，卡牌尺寸保持完全一致。
        _nameLabel.Text = CardData.Name;

        _costLabel.Text = _costTextOverride ?? string.Format(Localization.Get("card.ui.cost_fmt"), BattleRules.FormatMana(CardData.Cost));
        _typeLabel.Text = CardData.DisplayTypeLabel;
        _descriptionLabel.Text = _descriptionTextOverride ?? CardData.Description;
        if (_hotkeyLabel != null)
        {
            _hotkeyLabel.Text = _hotkeyText;
            _hotkeyLabel.Visible = !string.IsNullOrWhiteSpace(_hotkeyText);
        }

        foreach (var child in GetChildren())
        {
            UpdateDividerColor(child);
        }
    }

    private void ApplyPanelStyle()
    {
        var background = _isPlayable
            ? new Color(1.0f, 0.97f, 0.86f)
            : new Color(0.83f, 0.82f, 0.78f);

        var panel = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = _accentColor,
            BorderWidthBottom = 4,
            BorderWidthLeft = 4,
            BorderWidthRight = 4,
            BorderWidthTop = 4,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            ContentMarginBottom = 14,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 14
        };
        AddThemeStyleboxOverride("panel", panel);
    }

    private void UpdateDividerColor(Node node)
    {
        if (node is ColorRect divider && divider.HasMeta("is_divider"))
        {
            divider.Color = _accentColor;
        }

        foreach (var child in node.GetChildren())
        {
            UpdateDividerColor(child);
        }
    }
}
