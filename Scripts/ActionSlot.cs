//////////////////////////////////////////////////////////
// 文件：Scripts/ActionSlot.cs
//
// 模块：Core System
//
// 职责：
// 1. 承载核心数据结构、通用规则与跨模块协作相关代码。
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
/// Core System 的公开类：ActionSlot。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class ActionSlot : Control
{
    // ======================================================
    // 固定槽位快捷键模型
    // ======================================================
    // ActionBar 始终拥有 11 个 Slot，前10槽的快捷键只执行 Slot.Execute()；
    // 第11槽没有数字键，仍可直接点击。
    // RenderActionCards 只替换 Slot 内部的 Card，不销毁 Slot，也不重新绑定快捷键。
    //
    // 设计原因：
    // - 技能、装备、事件动态增删卡牌时，快捷键仍然绑定固定位置。
    // - 数字提示属于 Slot，不属于 CardUI，卡牌刷新不会让提示丢失。
    // - 空槽可以稳定存在，Developer Mode 能直接展示 Slot -> Card 映射。
    [Signal]
    public delegate void ActionRequestedEventHandler(CardUI cardUi);

    private const float CardWidth = 152f;
    private const float CardHeight = 246f;

    private CardUI? _cardUi;
    private Label? _shortcutLabel;

    public int SlotIndex { get; private set; }
    public Card? Card => _cardUi?.Visible == true ? _cardUi.CardData : null;
    public CardUI? CardUi => _cardUi?.Visible == true ? _cardUi : null;
    public bool Empty => Card == null;

    /// <summary>
    /// Core System 的公开入口：Initialize。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Initialize(int slotIndex)
    {
        SlotIndex = slotIndex;
        Name = $"Slot{slotIndex}";
        ClipContents = false;
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(CardWidth, CardHeight);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;

        _cardUi = new CardUI
        {
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _cardUi.SetAnchorsPreset(LayoutPreset.FullRect);
        _cardUi.CardPressed += _ => Execute();
        AddChild(_cardUi);

        _shortcutLabel = new Label
        {
            Name = "SlotShortcutLabel",
            Text = BattleHotkeySystem.GetDisplayTextForActionIndex(slotIndex),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
            ZIndex = 200
        };
        _shortcutLabel.AnchorLeft = 0.5f;
        _shortcutLabel.AnchorRight = 0.5f;
        _shortcutLabel.AnchorTop = 0f;
        _shortcutLabel.AnchorBottom = 0f;
        _shortcutLabel.OffsetLeft = -24;
        _shortcutLabel.OffsetRight = 24;
        _shortcutLabel.OffsetTop = -42;
        _shortcutLabel.OffsetBottom = -14;
        _shortcutLabel.AddThemeColorOverride("font_color", Colors.White);
        _shortcutLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _shortcutLabel.AddThemeFontSizeOverride("font_size", 22);
        _shortcutLabel.AddThemeConstantOverride("outline_size", 4);
        AddChild(_shortcutLabel);
    }

    /// <summary>
    /// Core System 的公开入口：SetCard。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void SetCard(Card card, bool playable, string? costTextOverride = null, string? descriptionTextOverride = null, DamagePreviewResult? damagePreview = null)
    {
        if (_cardUi == null)
        {
            return;
        }

        _cardUi.Visible = true;
        _cardUi.SetCard(card);
        _cardUi.SetHotkeyText(string.Empty);
        if (costTextOverride != null || descriptionTextOverride != null)
        {
            _cardUi.SetDisplayOverride(costTextOverride, descriptionTextOverride);
        }
        _cardUi.SetPlayable(playable);
        _cardUi.SetDamagePreview(damagePreview);
    }

    /// <summary>
    /// Core System 的公开入口：Clear。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Clear()
    {
        if (_cardUi == null)
        {
            return;
        }

        _cardUi.Visible = false;
        _cardUi.SetPlayable(false);
        _cardUi.SetHotkeyText(string.Empty);
        _cardUi.SetDamagePreview(null);
    }

    /// <summary>
    /// Core System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute()
    {
        if (_cardUi is not { Visible: true, IsPlayable: true, CardData: not null })
        {
            return;
        }

        EmitSignal(SignalName.ActionRequested, _cardUi);
    }
}
