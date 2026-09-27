//////////////////////////////////////////////////////////
// 文件：Scripts/ShopSlotUI.cs
//
// 模块：Shop System
//
// 职责：
// 1. 承载商店商品池、购买流程与商店界面相关代码。
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
using System;

// 商店中的一个商品槽位（共4个，2x2排列）。售出后保持空白，不会自动补货，没有刷新按钮。
/// <summary>
/// Shop System 的公开类：ShopSlotUI。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class ShopSlotUI : PanelContainer
{
    private VBoxContainer? _box;
    private Label? _nameLabel;
    private Label? _rarityLabel;
    private Label? _priceLabel;
    private Button? _buyButton;
    private ShopOffer? _currentOffer;
    private bool _hovered;

    public int SlotIndex { get; private set; }
    public Action<int>? OnBuyPressed;
    public Action<ShopOffer?>? OnHoverChanged;

    /// <summary>
    /// Shop System 的公开入口：Initialize。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Initialize(int slotIndex)
    {
        SlotIndex = slotIndex;
        BuildLayout();
    }

    /// <summary>
    /// Shop System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        MouseEntered += () =>
        {
            _hovered = true;
            ApplyStyle();
            OnHoverChanged?.Invoke(_currentOffer);
        };
        MouseExited += () =>
        {
            _hovered = false;
            ApplyStyle();
            OnHoverChanged?.Invoke(null);
        };
    }

    /// <summary>
    /// Shop System 的公开入口：SetOffer。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void SetOffer(ShopOffer? offer)
    {
        _currentOffer = offer;

        if (_nameLabel == null || _rarityLabel == null || _priceLabel == null || _buyButton == null)
        {
            return;
        }

        if (offer == null)
        {
            _nameLabel.Text = Localization.Get("shop.slot.empty");
            _rarityLabel.Text = string.Empty;
            _priceLabel.Text = string.Empty;
            _buyButton.Visible = false;
            ApplyStyle();
            return;
        }

        _nameLabel.Text = Localization.GetName(offer.Definition);
        _rarityLabel.Text = GetRarityDisplayName(offer.Definition.Rarity);
        _priceLabel.Text = ShopManager.GetOfferPriceText(offer);
        _buyButton.Visible = true;
        _buyButton.Disabled = !ShopManager.CanBuy(offer);
        ApplyStyle();
    }

    private void BuildLayout()
    {
        CustomMinimumSize = new Vector2(220, 170);

        _box = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        _box.AddThemeConstantOverride("separation", 8);
        AddChild(_box);

        _nameLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _nameLabel.AddThemeFontSizeOverride("font_size", 22);
        _box.AddChild(_nameLabel);

        _rarityLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _rarityLabel.AddThemeFontSizeOverride("font_size", 18);
        _rarityLabel.AddThemeColorOverride("font_color", new Color(0.74f, 0.78f, 0.84f));
        _box.AddChild(_rarityLabel);

        _priceLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _priceLabel.AddThemeFontSizeOverride("font_size", 20);
        _priceLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.78f, 0.30f));
        var pricePanel = new PanelContainer();
        // 价格条不截获鼠标事件，让父级商品槽仍能收到悬停并更新右侧详情。
        pricePanel.MouseFilter = MouseFilterEnum.Ignore;
        pricePanel.AddThemeStyleboxOverride("panel", UIResourceDatabase.CreatePricePanelStyle());
        pricePanel.AddChild(_priceLabel);
        _box.AddChild(pricePanel);

        _buyButton = new Button
        {
            Text = Localization.Get("shop.slot.buy_button"),
            CustomMinimumSize = new Vector2(0, 44)
        };
        _buyButton.AddThemeFontSizeOverride("font_size", 20);
        UIResourceDatabase.ApplyCommonButton(_buyButton);
        _buyButton.Pressed += () => OnBuyPressed?.Invoke(SlotIndex);
        _buyButton.MouseEntered += () =>
        {
            if (_buyButton.Disabled && _currentOffer != null)
                TooltipManager.Show(ShopManager.GetOfferDisabledReason(_currentOffer), _buyButton);
        };
        _buyButton.MouseExited += () => TooltipManager.Hide();
        _box.AddChild(_buyButton);

        ApplyStyle();
    }

    private void ApplyStyle()
    {
        var disabled = _currentOffer == null || (_buyButton?.Disabled ?? false);
        AddThemeStyleboxOverride("panel", UIResourceDatabase.CreateShopItemStyle(_hovered, disabled: disabled));
    }

    private static string GetRarityDisplayName(EquipmentRarity rarity)
        => Localization.GetRarityName(rarity);
}
