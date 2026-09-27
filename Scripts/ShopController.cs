//////////////////////////////////////////////////////////
// 文件：Scripts/ShopController.cs
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
using System.Collections.Generic;
using System.Linq;

// ShopScene 的控制器：4个商品槽位（2x2排列）+ 右下角【退出商店】按钮。
// 每次进入商店都会重新生成商品（GenerateSlots），售出后槽位保持空白，没有刷新按钮。
/// <summary>
/// Shop System 的公开类：ShopController。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class ShopController : Control
{
    private const int ChipChoicePrice = 150;

    [Signal]
    public delegate void ExitShopRequestedEventHandler();

    [Signal]
    public delegate void ChipChoicePurchaseRequestedEventHandler();

    private readonly PackedScene _inventoryScene = GD.Load<PackedScene>("res://Scenes/Inventory.tscn");

    public ShopType ShopType { get; set; } = ShopType.Normal;

    private readonly ShopSlotUI[] _slotUis = new ShopSlotUI[ShopManager.SlotCount];
    private Label? _goldLabel;
    private Button? _refreshButton;
    private Button? _chipChoiceButton;
    private Button? _inventoryButton;
    private Button? _exitButton;
    private TutorialHighlightLayer? _tutorialLayer;

    // 悬停信息面板中的各字段
    private Label? _infoNameLabel;
    private Label? _infoRarityLabel;
    private Label? _infoTypesLabel;
    private RichTextLabel? _infoDescriptionLabel;
    private RichTextLabel? _infoEffectsLabel;

    /// <summary>
    /// Shop System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        if (GameManager.InitialEventAllShopsBlackMarket && !IntegratedTutorialFlow.IsActive)
        {
            ShopType = ShopType.BlackMarket;
        }
        ShopManager.GenerateSlots(ShopType);

        // 整合式教程：固定把0号槽位覆盖成一件便宜的教学装备，保证"购买指定教学
        // 商品"这一步一定买得起、买的是同一件东西（不依赖随机生成结果）。
        if (IntegratedTutorialFlow.IsActive)
        {
            ShopManager.ForceTutorialOffer(0, IntegratedTutorialFlow.ShopTutorialItemId, IntegratedTutorialFlow.ShopTutorialItemPrice);
        }

        BuildLayout();
        RefreshView();

        if (IntegratedTutorialFlow.IsActive)
        {
            SetupTutorialHighlight();
        }
    }

    private void SetupTutorialHighlight()
    {
        _tutorialLayer = new TutorialHighlightLayer();
        AddChild(_tutorialLayer);
        RefreshTutorialStep();
    }

    private void RefreshTutorialStep()
    {
        if (_tutorialLayer == null) return;

        var boughtTutorialItem = InventoryManager.GetAllOwned()
            .Any(o => o.Definition.Id == IntegratedTutorialFlow.ShopTutorialItemId);

        if (!boughtTutorialItem)
        {
            _tutorialLayer.SetHighlightTarget(_slotUis.Length > 0 ? _slotUis[0] : null);
            _tutorialLayer.ShowStep(
                Localization.Get("tutorial.integrated.shop.buy.title"),
                Localization.Get("tutorial.integrated.shop.buy.desc"),
                Localization.Get("tutorial.integrated.shop.buy.objective"),
                string.Empty,
                showContinueButton: false);
            return;
        }

        TutorialIntegratedProgress.ShopPurchaseDone = true;
        _tutorialLayer.SetHighlightTarget(_inventoryButton);
        _tutorialLayer.ShowStep(
            Localization.Get("tutorial.integrated.shop.open_inventory.title"),
            Localization.Get("tutorial.integrated.shop.open_inventory.desc"),
            Localization.Get("tutorial.integrated.shop.open_inventory.objective"),
            string.Empty,
            showContinueButton: false);
    }

    private void BuildLayout()
    {
        // 背景
        var background = new ColorRect
        {
            Color = new Color(0.08f, 0.09f, 0.10f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 56);
        margin.AddThemeConstantOverride("margin_top", 48);
        margin.AddThemeConstantOverride("margin_right", 56);
        margin.AddThemeConstantOverride("margin_bottom", 48);
        AddChild(margin);

        var frame = new PanelContainer();
        frame.AddThemeStyleboxOverride("panel", UIResourceDatabase.CreateShopFrameStyle());
        margin.AddChild(frame);

        var frameMargin = new MarginContainer();
        frameMargin.AddThemeConstantOverride("margin_left", 20);
        frameMargin.AddThemeConstantOverride("margin_top", 16);
        frameMargin.AddThemeConstantOverride("margin_right", 20);
        frameMargin.AddThemeConstantOverride("margin_bottom", 72);
        frame.AddChild(frameMargin);

        var root = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        root.AddThemeConstantOverride("separation", 24);
        frameMargin.AddChild(root);

        // 标题
        var title = new Label
        {
            Text = ShopType switch
            {
                ShopType.Vehicle => Localization.Get("shop.type.vehicle"),
                ShopType.Witch => Localization.Get("shop.type.witch"),
                ShopType.BlackMarket => Localization.Get("shop.type.black_market"),
                _ => Localization.Get("shop.type.default")
            },
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 38);
        var titlePanel = new PanelContainer();
        titlePanel.AddThemeStyleboxOverride("panel", UIResourceDatabase.CreateHeaderStyle());
        titlePanel.AddChild(title);
        root.AddChild(titlePanel);
        root.AddChild(UIResourceDatabase.CreateDivider());

        // 主体内容行：左侧商品（居中）+ 右侧信息面板
        var contentRow = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        contentRow.AddThemeConstantOverride("separation", 28);
        root.AddChild(contentRow);

        // 商品格子——用 CenterContainer 包裹以确保水平居中
        var gridCenter = new CenterContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        contentRow.AddChild(gridCenter);

        var grid = new GridContainer
        {
            Columns = ShopManager.SlotCount
        };
        grid.AddThemeConstantOverride("h_separation", 20);
        grid.AddThemeConstantOverride("v_separation", 20);
        gridCenter.AddChild(grid);

        for (var i = 0; i < ShopManager.SlotCount; i++)
        {
            var slotUi = new ShopSlotUI
            {
                OnBuyPressed = OnBuyPressed,
                OnHoverChanged = OnSlotHoverChanged
            };
            slotUi.Initialize(i);
            _slotUis[i] = slotUi;
            grid.AddChild(slotUi);
        }

        // 右侧：悬停信息面板 + 固定的芯片三选一购买入口。
        contentRow.AddChild(BuildRightSidePanel());

        // 金币区域保持独立于商品布局，避免价格变化导致卡片重新排版。
        var goldPanel = new PanelContainer();
        goldPanel.AddThemeStyleboxOverride("panel", UIResourceDatabase.CreateGoldPanelStyle());
        goldPanel.SetAnchorsPreset(LayoutPreset.TopRight);
        goldPanel.OffsetLeft = -292;
        goldPanel.OffsetTop = 68;
        goldPanel.OffsetRight = -76;
        goldPanel.OffsetBottom = 118;
        AddChild(goldPanel);

        _goldLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _goldLabel.AddThemeFontSizeOverride("font_size", 22);
        _goldLabel.AddThemeColorOverride("font_color", new Color(0.86f, 0.82f, 0.64f));
        goldPanel.AddChild(_goldLabel);

        // 阵营命运 HUD：只在命中群阵营命运时非 null，纯文字块，不引入任何图片资源。
        // 独立于金币面板放置，避免影响既有金币显示的定位与尺寸计算。
        var fatePanel = FactionFateManager.BuildHudPanel();
        if (fatePanel != null)
        {
            fatePanel.SetAnchorsPreset(LayoutPreset.TopRight);
            fatePanel.OffsetLeft = -292;
            fatePanel.OffsetTop = 128;
            fatePanel.OffsetRight = -76;
            fatePanel.OffsetBottom = 210;
            AddChild(fatePanel);
        }

        // 右下角悬浮的【刷新商品】按钮（消耗50金）；载具店不可刷新，故隐藏。
        if (ShopType is not ShopType.Vehicle and not ShopType.Witch)
        {
            var refreshButton = new Button
            {
                Text = Localization.Get("shop.refresh_button"),
                CustomMinimumSize = new Vector2(140, 48),
                Disabled = GameManager.Gold < 50
            };
            refreshButton.AddThemeFontSizeOverride("font_size", 22);
            UIResourceDatabase.ApplyCommonButton(refreshButton);
            refreshButton.Pressed += OnRefreshPressed;
            refreshButton.MouseEntered += () =>
            {
                if (ShopManager.HasRefreshedCurrentShop)
                    TooltipManager.Show(Localization.Get("shop.already_refreshed"), refreshButton);
            };
            refreshButton.MouseExited += () => TooltipManager.Hide();
            refreshButton.AnchorLeft = 0.5f;
            refreshButton.AnchorRight = 0.5f;
            refreshButton.AnchorTop = 1f;
            refreshButton.AnchorBottom = 1f;
            refreshButton.OffsetLeft = -320;
            refreshButton.OffsetTop = -84;
            refreshButton.OffsetRight = -170;
            refreshButton.OffsetBottom = -30;
            _refreshButton = refreshButton;
            AddChild(refreshButton);
        }

        // 右下角悬浮的【背包】按钮
        var inventoryButton = new Button
        {
            Text = Localization.Get("ui.inventory"),
            CustomMinimumSize = new Vector2(120, 48)
        };
        inventoryButton.AddThemeFontSizeOverride("font_size", 22);
        UIResourceDatabase.ApplyCommonButton(inventoryButton);
        inventoryButton.Pressed += OnInventoryButtonPressed;
        inventoryButton.AnchorLeft = 0.5f;
        inventoryButton.AnchorRight = 0.5f;
        inventoryButton.AnchorTop = 1f;
        inventoryButton.AnchorBottom = 1f;
        inventoryButton.OffsetLeft = -155;
        inventoryButton.OffsetTop = -84;
        inventoryButton.OffsetRight = -15;
        inventoryButton.OffsetBottom = -30;
        AddChild(inventoryButton);
        _inventoryButton = inventoryButton;

        // 右下角悬浮的【退出商店】按钮
        var exitButton = new Button
        {
            Text = Localization.Get("shop.exit"),
            CustomMinimumSize = new Vector2(140, 48)
        };
        exitButton.AddThemeFontSizeOverride("font_size", 22);
        UIResourceDatabase.ApplyCommonButton(exitButton);
        exitButton.Pressed += () => EmitSignal(SignalName.ExitShopRequested);
        exitButton.AnchorLeft = 0.5f;
        exitButton.AnchorRight = 0.5f;
        exitButton.AnchorTop = 1f;
        exitButton.AnchorBottom = 1f;
        exitButton.OffsetLeft = 10;
        exitButton.OffsetTop = -84;
        exitButton.OffsetRight = 170;
        exitButton.OffsetBottom = -30;
        AddChild(exitButton);
        _exitButton = exitButton;

        // 孙尚香专属【重铸】按钮：只读取 GameManager.CurrentCharacterHasSkill，
        // 不在这里判断具体角色 Id，也不修改 ShopManager 的任何商品/定价逻辑——
        // 重铸本身完全复用既有的 InventoryManager.ReforgeEquipment。
        if (GameManager.CurrentCharacterHasSkill(SkillIds.WuKu))
        {
            var reforgeButton = new Button
            {
                Text = Localization.Get("shop.wuku_reforge"),
                CustomMinimumSize = new Vector2(140, 48)
            };
            reforgeButton.AddThemeFontSizeOverride("font_size", 22);
            UIResourceDatabase.ApplyCommonButton(reforgeButton);
            reforgeButton.Pressed += OnWuKuReforgePressed;
            reforgeButton.AnchorLeft = 0.5f;
            reforgeButton.AnchorRight = 0.5f;
            reforgeButton.AnchorTop = 1f;
            reforgeButton.AnchorBottom = 1f;
            reforgeButton.OffsetLeft = -485;
            reforgeButton.OffsetTop = -84;
            reforgeButton.OffsetRight = -335;
            reforgeButton.OffsetBottom = -30;
            AddChild(reforgeButton);
        }

        UIResourceDatabase.ApplyScrollBars(this);
    }

    private Control BuildRightSidePanel()
    {
        var rightSide = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(280, 0),
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        rightSide.AddThemeConstantOverride("separation", 18);
        rightSide.AddChild(BuildInfoPanel());
        rightSide.AddChild(BuildChipChoicePanel());
        return rightSide;
    }

    private Control BuildChipChoicePanel()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", UIResourceDatabase.CreateDescriptionStyle());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);

        var title = new Label
        {
            Text = Localization.Get("shop.chip_choice.title"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 22);
        box.AddChild(title);

        var description = new Label
        {
            Text = Localization.Get("shop.chip_choice.desc"),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        description.AddThemeFontSizeOverride("font_size", 17);
        description.AddThemeColorOverride("font_color", new Color(0.74f, 0.78f, 0.84f));
        box.AddChild(description);

        _chipChoiceButton = new Button
        {
            Text = string.Format(Localization.Get("shop.chip_choice.buy"), ChipChoicePrice),
            CustomMinimumSize = new Vector2(0, 44),
            Disabled = GameManager.Gold < ChipChoicePrice
        };
        _chipChoiceButton.AddThemeFontSizeOverride("font_size", 20);
        UIResourceDatabase.ApplyCommonButton(_chipChoiceButton);
        _chipChoiceButton.Pressed += OnChipChoicePurchasePressed;
        _chipChoiceButton.MouseEntered += () =>
        {
            if (_chipChoiceButton.Disabled)
            {
                TooltipManager.Show(Localization.Get("shop.chip_choice.insufficient_gold"), _chipChoiceButton);
            }
        };
        _chipChoiceButton.MouseExited += TooltipManager.Hide;
        box.AddChild(_chipChoiceButton);

        return panel;
    }

    private Control BuildInfoPanel()
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(280, 0),
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };

        panel.AddThemeStyleboxOverride("panel", UIResourceDatabase.CreateDescriptionStyle());

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);

        _infoNameLabel = new Label
        {
            Text = Localization.Get("shop.hover_hint"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _infoNameLabel.AddThemeFontSizeOverride("font_size", 24);
        box.AddChild(_infoNameLabel);

        _infoRarityLabel = new Label();
        _infoRarityLabel.AddThemeFontSizeOverride("font_size", 20);
        box.AddChild(_infoRarityLabel);

        _infoTypesLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _infoTypesLabel.AddThemeFontSizeOverride("font_size", 18);
        _infoTypesLabel.AddThemeColorOverride("font_color", new Color(0.74f, 0.78f, 0.84f));
        box.AddChild(_infoTypesLabel);

        var descTitle = new Label { Text = Localization.Get("shop.desc_label") };
        descTitle.AddThemeFontSizeOverride("font_size", 17);
        box.AddChild(descTitle);

        _infoDescriptionLabel = new RichTextLabel
        {
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _infoDescriptionLabel.AddThemeFontSizeOverride("normal_font_size", 17);
        box.AddChild(_infoDescriptionLabel);

        var effectTitle = new Label { Text = Localization.Get("shop.effect_label") };
        effectTitle.AddThemeFontSizeOverride("font_size", 17);
        box.AddChild(effectTitle);

        _infoEffectsLabel = new RichTextLabel
        {
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _infoEffectsLabel.AddThemeFontSizeOverride("normal_font_size", 17);
        box.AddChild(_infoEffectsLabel);

        return panel;
    }

    private void RefreshView()
    {
        if (_goldLabel != null)
        {
            _goldLabel.Text = string.Format(Localization.Get("shop.gold_fmt"), GameManager.Gold);
        }

        if (_refreshButton != null)
        {
            var alreadyRefreshed = ShopManager.HasRefreshedCurrentShop;
            var cannotAfford = GameManager.Gold < 50;
            _refreshButton.Disabled = alreadyRefreshed || cannotAfford;
        }

        if (_chipChoiceButton != null)
        {
            _chipChoiceButton.Disabled = GameManager.Gold < ChipChoicePrice;
        }

        for (var i = 0; i < ShopManager.SlotCount; i++)
        {
            _slotUis[i].SetOffer(ShopManager.CurrentSlots[i]);
        }
    }

    private void OnRefreshPressed()
    {
        if (!ShopManager.TryRefresh())
        {
            return;
        }

        RefreshView();
        ShowHoverInfo(null);
    }

    private void OnBuyPressed(int slotIndex)
    {
        ShopManager.TryBuy(slotIndex);
        RefreshView();
        // 购买后清除悬停信息（商品已消失）
        ShowHoverInfo(null);
        RefreshTutorialStep();
    }

    private void OnChipChoicePurchasePressed()
    {
        if (GameManager.Gold < ChipChoicePrice)
        {
            return;
        }

        RewardManager.Execute(new RewardSequence().Add(new LoseGoldRewardAction(ChipChoicePrice)));
        RefreshView();
        EmitSignal(SignalName.ChipChoicePurchaseRequested);
    }

    /// <summary>
    /// 供商店内叠加的芯片选择界面结算后刷新金币与按钮可用状态。
    /// </summary>
    public void RefreshAfterExternalChoice()
    {
        RefreshView();
    }

    private void OnSlotHoverChanged(ShopOffer? offer)
    {
        ShowHoverInfo(offer);
    }

    private void ShowHoverInfo(ShopOffer? offer)
    {
        if (_infoNameLabel == null || _infoRarityLabel == null || _infoTypesLabel == null
            || _infoDescriptionLabel == null || _infoEffectsLabel == null)
        {
            return;
        }

        if (offer == null)
        {
            _infoNameLabel.Text = Localization.Get("shop.hover_hint");
            _infoRarityLabel.Text = string.Empty;
            _infoTypesLabel.Text = string.Empty;
            _infoDescriptionLabel.Text = string.Empty;
            _infoEffectsLabel.Text = string.Empty;
            return;
        }

        var def = offer.Definition;
        _infoNameLabel.Text = Localization.GetName(def);
        _infoRarityLabel.Text = GetRarityDisplayName(def.Rarity);
        _infoRarityLabel.AddThemeColorOverride("font_color", GetRarityColor(def.Rarity));
        _infoTypesLabel.Text = GetTypesDisplayText(def.Types);

        _infoDescriptionLabel.Text = Localization.GetDescription(def);

        var effectLines = new List<string>();
        for (var effectIndex = 0; effectIndex < def.Effects.Count; effectIndex++)
        {
            effectLines.Add(Localization.GetEquipmentEffectDescription(def, effectIndex));
        }
        _infoEffectsLabel.Text = effectLines.Count > 0 ? string.Join("\n", effectLines) : Localization.Get("shop.no_effect");
    }

    // 点击背包按钮：将完整背包界面（含装备/出售功能）叠加在商店上方。
    private void OnInventoryButtonPressed()
    {
        var overlay = _inventoryScene.Instantiate<InventoryController>();
        overlay.ReturnButtonTextKey = "shop.return_button";
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        overlay.ReturnToMapRequested += () =>
        {
            RemoveChild(overlay);
            overlay.QueueFree();
            RefreshView();
            RefreshAfterInventoryClosed();
        };
        AddChild(overlay);
    }

    // 整合式教程：从背包叠加层返回商店后，如果已经装备好了教学装备，高亮
    // 【退出商店】按钮，提示玩家可以继续了；否则维持之前那一步的高亮不变。
    private void RefreshAfterInventoryClosed()
    {
        if (_tutorialLayer == null) return;

        if (TutorialIntegratedProgress.EquipDone)
        {
            _tutorialLayer.SetHighlightTarget(_exitButton);
            _tutorialLayer.ShowStep(
                Localization.Get("tutorial.integrated.shop.exit.title"),
                Localization.Get("tutorial.integrated.shop.exit.desc"),
                Localization.Get("tutorial.integrated.shop.exit.objective"),
                string.Empty,
                showContinueButton: false);
        }
        else
        {
            RefreshTutorialStep();
        }
    }

    // 点击【重铸】按钮：叠加一个简单的选择面板，列出背包（含已装备）里的全部装备。
    // 选中一件后直接调用既有的 InventoryManager.ReforgeEquipment——同品质随机换成
    // 另一件（该方法本身已经排除"重铸成自己"，见 InventoryManager.cs），不在这里
    // 重新实现随机池逻辑，也不修改 ShopManager 的任何商品/价格数据。
    private void OnWuKuReforgePressed()
    {
        var overlay = new PanelContainer
        {
            MouseFilter = MouseFilterEnum.Stop
        };
        overlay.AddThemeStyleboxOverride("panel", UIResourceDatabase.CreateCommonPanelStyle(18));
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 80);
        margin.AddThemeConstantOverride("margin_top", 60);
        margin.AddThemeConstantOverride("margin_right", 80);
        margin.AddThemeConstantOverride("margin_bottom", 60);
        overlay.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 16);
        margin.AddChild(root);

        var title = new Label
        {
            Text = Localization.Get("shop.wuku_reforge.title"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 30);
        root.AddChild(title);

        var hint = new Label
        {
            Text = Localization.Get("shop.wuku_reforge.hint"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        hint.AddThemeFontSizeOverride("font_size", 18);
        hint.AddThemeColorOverride("font_color", new Color(0.74f, 0.78f, 0.84f));
        root.AddChild(hint);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddChild(scroll);

        var list = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        list.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(list);

        foreach (var item in InventoryManager.GetAllOwned())
        {
            var itemButton = new Button
            {
                Text = $"[{GetRarityDisplayName(item.Definition.Rarity)}] {Localization.GetName(item.Definition)}",
                Alignment = HorizontalAlignment.Left,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            itemButton.AddThemeFontSizeOverride("font_size", 20);
            itemButton.AddThemeColorOverride("font_color", GetRarityColor(item.Definition.Rarity));
            UIResourceDatabase.ApplyCommonButton(itemButton);
            var instanceId = item.InstanceId;
            itemButton.Pressed += () =>
            {
                var reforgeResult = InventoryManager.ReforgeEquipment(instanceId);
                RemoveChild(overlay);
                overlay.QueueFree();
                RefreshView();
                ShowReforgeResult(reforgeResult.Message);
            };
            list.AddChild(itemButton);
        }

        var closeButton = new Button
        {
            Text = Localization.Get("ui.cancel"),
            CustomMinimumSize = new Vector2(140, 48),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        UIResourceDatabase.ApplyCommonButton(closeButton);
        closeButton.AddThemeFontSizeOverride("font_size", 22);
        closeButton.Pressed += () =>
        {
            RemoveChild(overlay);
            overlay.QueueFree();
        };
        root.AddChild(closeButton);
        UIResourceDatabase.ApplyScrollBar(scroll);
    }

    private void ShowReforgeResult(string message)
    {
        var dialog = new AcceptDialog
        {
            Title = Localization.Get("equipment.reforge.result_title"),
            DialogText = message
        };
        AddChild(dialog);
        dialog.Confirmed += () => dialog.QueueFree();
        dialog.PopupCentered();
    }

    private static string GetRarityDisplayName(EquipmentRarity rarity)
        => Localization.GetRarityName(rarity);

    private static Color GetRarityColor(EquipmentRarity rarity)
    {
        return rarity switch
        {
            EquipmentRarity.Common => new Color(0.78f, 0.78f, 0.78f),
            EquipmentRarity.Rare => new Color(0.30f, 0.56f, 0.92f),
            EquipmentRarity.Epic => new Color(0.80f, 0.46f, 1.0f),
            EquipmentRarity.Legendary => new Color(0.92f, 0.70f, 0.18f),
            _ => new Color(0.74f, 0.78f, 0.84f)
        };
    }

    private static string GetTypesDisplayText(System.Collections.Generic.IReadOnlyList<EquipmentType> types)
    {
        var names = new List<string>();
        foreach (var type in types)
        {
            var name = type != EquipmentType.Buff ? Localization.GetEquipmentTypeName(type) : null;
            if (name != null)
            {
                names.Add(name);
            }
        }

        return names.Count > 0 ? string.Format(Localization.Get("shop.type_fmt"), string.Join(" / ", names)) : string.Empty;
    }
}
