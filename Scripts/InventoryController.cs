//////////////////////////////////////////////////////////
// 文件：Scripts/InventoryController.cs
//
// 模块：Inventory System
//
// 职责：
// 1. 承载背包、装备槽位与装备实例管理相关代码。
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
using System.Collections.Generic;

// InventoryScene 的控制器：背包 + 装备槽界面。
// 仅负责装备的“装备/卸下/出售”操作，战斗系统的数值读取仍统一通过 GameManager（其内部已委托给 InventoryManager）。
/// <summary>
/// Inventory System 的公开类：InventoryController。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class InventoryController : Control
{
    [Signal]
    public delegate void ReturnToMapRequestedEventHandler();

    // 只读模式：商店中打开背包时使用，隐藏出售区并禁用拖拽。
    public bool ReadOnly { get; set; } = false;
    public string ReturnButtonTextKey { get; set; } = "ui.back_to_map";
    private string _returnButtonLabel = string.Empty;
    // 兼容旧调用方；优先使用 ReturnButtonTextKey。
    public string ReturnButtonLabel
    {
        get => _returnButtonLabel;
        set
        {
            _returnButtonLabel = value;
            ReturnButtonTextKey = string.Empty;
        }
    }

    private static readonly EquipmentSlot[] SlotOrder =
    {
        EquipmentSlot.Weapon,
        EquipmentSlot.Armor,
        EquipmentSlot.Vehicle,
        EquipmentSlot.Accessory1,
        EquipmentSlot.Accessory2,
        EquipmentSlot.Accessory3,
        EquipmentSlot.Accessory4, // 吴·多宝架 命中时才出现的第4个饰品槽；渲染时按命运是否命中过滤，见 BuildSlotRow/RefreshLocalization 的消费点
        EquipmentSlot.Accessory5, // 爆炸果实事件永久获得的"普通饰品槽"；渲染时按 GameManager.HasExplosiveFruitAccessorySlot 过滤
        EquipmentSlot.Universal   // 万能槽：仅在扩容芯片已装备时显示
    };

    /// <summary>
    /// Accessory4/Accessory5 是否应当在本次UI渲染中出现——Accessory4 只在 吴·多宝架 命中时显示，
    /// Accessory5 只在爆炸果实事件"碰一下"选项被选择过之后显示；其余情况下和游戏今天的行为
    /// 完全一致（3个饰品槽）。只在"消费" SlotOrder 构建实际UI的地方过滤，SlotOrder 本身仍然是
    /// 一份固定的静态数据，不因命运/事件是否触发而变化。
    /// </summary>
    private static bool IsSlotVisibleForCurrentRun(EquipmentSlot slot)
    {
        if (slot == EquipmentSlot.Accessory4)
        {
            return FactionFateManager.CurrentFateId == FactionFateIds.WuAccessorySlot;
        }

        if (slot == EquipmentSlot.Accessory5)
        {
            return GameManager.HasExplosiveFruitAccessorySlot;
        }

        return true;
    }

    private readonly Dictionary<EquipmentSlot, EquipmentSlotUI> _slotUis = new();

    private Label? _goldLabel;
    private Label? _titleLabel;
    private Button? _debugButton;
    private Button? _returnButton;
    private Label? _chipStatsTitleLabel;
    private Label? _descriptionTitleLabel;
    private Label? _effectsTitleLabel;
    private Label? _sellTitleLabel;
    private SellDropZone? _sellZone;
    private FlowContainer? _backpackFlow;
    private Label? _defenseChipLabel;
    private Label? _attackChipLabel;
    private Label? _knowledgeChipLabel;
    private Label? _infoNameLabel;
    private Label? _infoRarityLabel;
    private Label? _infoTypesLabel;
    private RichTextLabel? _infoDescriptionLabel;
    private RichTextLabel? _infoEffectsLabel;
    private Label? _infoSellPriceLabel;
    private AudioStreamPlayer? _swapAudioPlayer;
    private TutorialHighlightLayer? _tutorialLayer;

    /// <summary>
    /// Inventory System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        Localization.LanguageChanged += OnLanguageChanged;
        BuildLayout();
        RefreshView();

        if (IntegratedTutorialFlow.IsActive && !ReadOnly)
        {
            _tutorialLayer = new TutorialHighlightLayer();
            AddChild(_tutorialLayer);
            RefreshTutorialStep();
        }
    }

    /// <summary>
    /// 整合式教程：高亮"要拖去装备的那件教学装备"+对应槽位，装备完成后（无论是
    /// 拖到槽位、还是拖到槽位里已有装备的图标上触发交换——两条路径最终都会调用
    /// InventoryManager.EquipToSlot/SwapEquipment，见 OnEquipDropped/OnSwapDropped）
    /// 展示"效果已生效"提示。买到手的教学装备是武器类，对应 EquipmentSlot.Weapon。
    /// </summary>
    private void RefreshTutorialStep()
    {
        if (_tutorialLayer == null) return;

        var equippedTutorialItem = InventoryManager.GetSlotItem(EquipmentSlot.Weapon)?.Definition.Id
            == IntegratedTutorialFlow.ShopTutorialItemId;

        if (equippedTutorialItem)
        {
            TutorialIntegratedProgress.EquipDone = true;
            _tutorialLayer.SetHighlightTarget(_slotUis.TryGetValue(EquipmentSlot.Weapon, out var equippedSlotUi) ? equippedSlotUi : null);
            _tutorialLayer.ShowStep(
                Localization.Get("tutorial.integrated.inventory.equipped.title"),
                Localization.Get("tutorial.integrated.inventory.equipped.desc"),
                string.Empty,
                string.Empty,
                showContinueButton: false);
            return;
        }

        InventoryItemUI? backpackTarget = null;
        if (_backpackFlow != null)
        {
            foreach (var child in _backpackFlow.GetChildren())
            {
                if (child is InventoryItemUI itemUi && itemUi.Item?.Definition.Id == IntegratedTutorialFlow.ShopTutorialItemId)
                {
                    backpackTarget = itemUi;
                    break;
                }
            }
        }

        Control? tutorialTarget = backpackTarget;
        if (tutorialTarget == null && _slotUis.TryGetValue(EquipmentSlot.Weapon, out var slotUi))
        {
            tutorialTarget = slotUi;
        }
        _tutorialLayer.SetHighlightTarget(tutorialTarget);
        _tutorialLayer.ShowStep(
            Localization.Get("tutorial.integrated.inventory.equip.title"),
            Localization.Get("tutorial.integrated.inventory.equip.desc"),
            Localization.Get("tutorial.integrated.inventory.equip.objective"),
            string.Empty,
            showContinueButton: false);
    }

    /// <summary>
    /// Inventory System 的公开入口：_ExitTree。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _ExitTree()
    {
        Localization.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        RefreshStaticText();
        RefreshView();
        ShowHoverInfo(null);
    }

    private void BuildLayout()
    {
        var background = new ColorRect
        {
            Color = new Color(0.08f, 0.09f, 0.10f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 36);
        margin.AddThemeConstantOverride("margin_top", 28);
        margin.AddThemeConstantOverride("margin_right", 36);
        margin.AddThemeConstantOverride("margin_bottom", 28);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 18);
        margin.AddChild(root);

        root.AddChild(BuildTopBar());
        root.AddChild(BuildSlotRow());

        var mainRow = new HBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        mainRow.AddThemeConstantOverride("separation", 20);
        root.AddChild(mainRow);

        mainRow.AddChild(BuildInfoPanel());
        mainRow.AddChild(BuildBackpackArea());
        mainRow.AddChild(BuildRightColumn());
        UIResourceDatabase.ApplyScrollBars(this);
    }

    private Control BuildTopBar()
    {
        var topBar = new HBoxContainer();
        topBar.AddThemeConstantOverride("separation", 16);

        var title = new Label
        {
            Text = Localization.Get("inventory.title")
        };
        _titleLabel = title;
        title.AddThemeFontSizeOverride("font_size", 36);
        topBar.AddChild(title);

        _goldLabel = new Label();
        _goldLabel.AddThemeFontSizeOverride("font_size", 22);
        _goldLabel.AddThemeColorOverride("font_color", new Color(0.86f, 0.82f, 0.64f));
        topBar.AddChild(_goldLabel);

        // 阵营命运 HUD：只在命中群阵营命运时非 null，纯文字块，不引入任何图片资源。
        var fatePanel = FactionFateManager.BuildHudPanel();
        if (fatePanel != null)
        {
            topBar.AddChild(fatePanel);
        }

        var spacer = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        topBar.AddChild(spacer);

        if (!ReadOnly && GameManager.DebugMapEnabled)
        {
            var debugButton = new Button
            {
                Text = Localization.Get("inventory.debug"),
                CustomMinimumSize = new Vector2(100, 46)
            };
            _debugButton = debugButton;
            debugButton.AddThemeFontSizeOverride("font_size", 20);
            debugButton.AddThemeColorOverride("font_color", new Color(0.55f, 0.90f, 0.55f));
            debugButton.Pressed += OpenDebugPanel;
            topBar.AddChild(debugButton);
        }

        var returnButton = new Button
        {
            Text = GetReturnButtonText(),
            CustomMinimumSize = new Vector2(140, 46)
        };
        _returnButton = returnButton;
        returnButton.AddThemeFontSizeOverride("font_size", 20);
        returnButton.Pressed += () => EmitSignal(SignalName.ReturnToMapRequested);
        topBar.AddChild(returnButton);

        return topBar;
    }

    private Control BuildSlotRow()
    {
        var row = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        row.AddThemeConstantOverride("separation", 14);

        foreach (var slot in SlotOrder)
        {
            var slotUi = new EquipmentSlotUI
            {
                OnEquipDropped = ReadOnly ? null : OnEquipDropped,
                OnHoverChanged = OnHoverChanged,
                CanSwapDropped = ReadOnly ? null : InventoryManager.CanSwapEquipment,
                OnSwapDropped = ReadOnly ? null : OnSwapDropped,
                ReadOnly = ReadOnly
            };
            slotUi.Initialize(slot);
            if (slot is EquipmentSlot.Accessory4 or EquipmentSlot.Accessory5)
            {
                slotUi.Visible = IsSlotVisibleForCurrentRun(slot);
            }
            _slotUis[slot] = slotUi;
            row.AddChild(slotUi);
        }

        return row;
    }

    // 右侧纵列：出售区（上）+ 芯片统计（下）。
    // 宽度固定 210px，不参与弹性分配，防止侧栏宽度因内容变化而影响背包格子区域。
    private Control BuildRightColumn()
    {
        var column = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(210, 0),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        column.AddThemeConstantOverride("separation", 14);

        if (!ReadOnly)
        {
            var sellZone = new SellDropZone
            {
                CustomMinimumSize = new Vector2(210, 180),
                SizeFlagsVertical = SizeFlags.ExpandFill,
                OnItemSold = OnSellDropped
            };
            sellZone.BuildLayout();
            _sellZone = sellZone;
            column.AddChild(sellZone);
        }

        column.AddChild(BuildChipStatsPanel());
        return column;
    }

    // 芯片数量面板：展示防护/攻击/知识三类芯片的当前累计数量以及对应加成。
    private Control BuildChipStatsPanel()
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(210, 0),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };

        var styleBox = new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.11f, 0.16f),
            BorderColor = new Color(0.35f, 0.40f, 0.55f),
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6
        };
        panel.AddThemeStyleboxOverride("panel", styleBox);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 14);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 14);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        margin.AddChild(box);

        var title = new Label { Text = Localization.Get("inventory.chip_stats") };
        _chipStatsTitleLabel = title;
        title.AddThemeFontSizeOverride("font_size", 22);
        title.AddThemeColorOverride("font_color", new Color(0.75f, 0.82f, 1.0f));
        box.AddChild(title);

        var separator = new HSeparator();
        box.AddChild(separator);

        _defenseChipLabel = new Label();
        _defenseChipLabel.AddThemeFontSizeOverride("font_size", 18);
        _defenseChipLabel.AddThemeColorOverride("font_color", new Color(0.56f, 0.84f, 0.72f));
        box.AddChild(_defenseChipLabel);

        _attackChipLabel = new Label();
        _attackChipLabel.AddThemeFontSizeOverride("font_size", 18);
        _attackChipLabel.AddThemeColorOverride("font_color", new Color(0.90f, 0.56f, 0.50f));
        box.AddChild(_attackChipLabel);

        _knowledgeChipLabel = new Label();
        _knowledgeChipLabel.AddThemeFontSizeOverride("font_size", 18);
        _knowledgeChipLabel.AddThemeColorOverride("font_color", new Color(0.84f, 0.78f, 0.45f));
        box.AddChild(_knowledgeChipLabel);

        return panel;
    }

    private Control BuildInfoPanel()
    {
        // 宽度固定为 320px，不参与 HBox 的弹性分配，防止悬停时内容变化引起布局抖动。
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(320, 0),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

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
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 38)
        };
        _infoNameLabel.AddThemeFontSizeOverride("font_size", 26);
        box.AddChild(_infoNameLabel);

        _infoRarityLabel = new Label
        {
            CustomMinimumSize = new Vector2(0, 28)
        };
        _infoRarityLabel.AddThemeFontSizeOverride("font_size", 20);
        box.AddChild(_infoRarityLabel);

        _infoTypesLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 28)
        };
        _infoTypesLabel.AddThemeFontSizeOverride("font_size", 18);
        _infoTypesLabel.AddThemeColorOverride("font_color", new Color(0.74f, 0.78f, 0.84f));
        box.AddChild(_infoTypesLabel);

        var descriptionTitle = new Label { Text = Localization.Get("inventory.description_label") };
        _descriptionTitleLabel = descriptionTitle;
        descriptionTitle.AddThemeFontSizeOverride("font_size", 18);
        box.AddChild(descriptionTitle);

        _infoDescriptionLabel = new RichTextLabel
        {
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _infoDescriptionLabel.AddThemeFontSizeOverride("normal_font_size", 18);
        box.AddChild(_infoDescriptionLabel);

        var effectsTitle = new Label { Text = Localization.Get("inventory.effects_label") };
        _effectsTitleLabel = effectsTitle;
        effectsTitle.AddThemeFontSizeOverride("font_size", 18);
        box.AddChild(effectsTitle);

        _infoEffectsLabel = new RichTextLabel
        {
            BbcodeEnabled = false,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _infoEffectsLabel.AddThemeFontSizeOverride("normal_font_size", 18);
        box.AddChild(_infoEffectsLabel);

        var sellTitle = new Label { Text = Localization.Get("inventory.sell_price_label") };
        _sellTitleLabel = sellTitle;
        sellTitle.AddThemeFontSizeOverride("font_size", 18);
        box.AddChild(sellTitle);

        _infoSellPriceLabel = new Label();
        _infoSellPriceLabel.AddThemeFontSizeOverride("font_size", 20);
        _infoSellPriceLabel.AddThemeColorOverride("font_color", new Color(0.92f, 0.78f, 0.30f));
        box.AddChild(_infoSellPriceLabel);

        ShowHoverInfo(null);

        return panel;
    }

    private Control BuildBackpackArea()
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };

        var dropZone = new BackpackDropZone
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            OnItemDropped = ReadOnly ? null : OnUnequipDropped
        };
        dropZone.BuildLayout();
        scroll.AddChild(dropZone);

        _backpackFlow = dropZone.Flow;

        return scroll;
    }

    private void RefreshView()
    {
        RefreshStaticText();

        if (_goldLabel != null)
        {
            _goldLabel.Text = Localization.GetFmt("inventory.gold_fmt", GameManager.Gold);
        }

        if (_defenseChipLabel != null)
            _defenseChipLabel.Text = Localization.GetFmt(
                "inventory.defense_chip_fmt",
                GameManager.DefenseChipCount,
                GameManager.DefenseChipCount * 10 * FactionFateManager.GetChipEffectMultiplier());
        if (_attackChipLabel != null)
            _attackChipLabel.Text = Localization.GetFmt(
                "inventory.attack_chip_fmt",
                GameManager.AttackChipCount,
                GameManager.AttackChipCount * 3 * FactionFateManager.GetChipEffectMultiplier());
        if (_knowledgeChipLabel != null)
            _knowledgeChipLabel.Text = Localization.GetFmt(
                "inventory.knowledge_chip_fmt",
                GameManager.KnowledgeChipCount,
                GameManager.KnowledgeChipCount * 5 * FactionFateManager.GetChipEffectMultiplier());

        var universalUnlocked = InventoryManager.IsUniversalSlotUnlocked();
        foreach (var slot in SlotOrder)
        {
            if (slot == EquipmentSlot.Universal)
            {
                _slotUis[slot].Visible = universalUnlocked;
            }
            else if (slot is EquipmentSlot.Accessory4 or EquipmentSlot.Accessory5)
            {
                // 吴·多宝架：只有命中该命运时才显示第4个饰品槽；爆炸果实"普通饰品槽"
                // 只有事件触发后才显示；其余情况下保持和今天完全一致的3槽外观。
                _slotUis[slot].Visible = IsSlotVisibleForCurrentRun(slot);
            }
            _slotUis[slot].RefreshLocalization();
            _slotUis[slot].SetEquippedItem(InventoryManager.GetSlotItem(slot));
        }

        if (_backpackFlow != null)
        {
            foreach (var child in _backpackFlow.GetChildren())
            {
                _backpackFlow.RemoveChild(child);
                child.QueueFree();
            }

            foreach (var item in InventoryManager.GetBackpackItems())
            {
                var itemUi = new InventoryItemUI
                {
                    OnHoverChanged = OnHoverChanged,
                    CanSwapDropped = ReadOnly ? null : InventoryManager.CanSwapEquipment,
                    OnSwapDropped = ReadOnly ? null : OnSwapDropped,
                    ReadOnly = ReadOnly
                };
                _backpackFlow.AddChild(itemUi);
                itemUi.SetItem(item);
            }
        }
    }

    private void OnEquipDropped(Guid instanceId, EquipmentSlot slot)
    {
        var displacedItemId = InventoryManager.GetSlotItem(slot)?.InstanceId;
        if (!InventoryManager.EquipToSlot(instanceId, slot))
        {
            var existing = InventoryManager.GetSlotItem(slot);
            if (existing?.Definition.Id == EquipmentIds.ExpansionChip
                && InventoryManager.GetSlotItem(EquipmentSlot.Universal) != null)
            {
                ShowHint(Localization.Get("inventory.hint.unequip_universal_first"));
            }
            return;
        }
        RefreshView();
        if (displacedItemId.HasValue && displacedItemId.Value != instanceId)
        {
            PlaySwapFeedbackAfterRefresh(instanceId, displacedItemId.Value);
        }
        if (IntegratedTutorialFlow.IsActive) RefreshTutorialStep();
    }

    private void OnSwapDropped(Guid firstInstanceId, Guid secondInstanceId)
    {
        if (!InventoryManager.SwapEquipment(firstInstanceId, secondInstanceId))
        {
            return;
        }

        RefreshView();
        PlaySwapFeedbackAfterRefresh(firstInstanceId, secondInstanceId);
        if (IntegratedTutorialFlow.IsActive) RefreshTutorialStep();
    }

    private void PlaySwapFeedbackAfterRefresh(Guid firstInstanceId, Guid secondInstanceId)
    {
        PlaySwapSound();
        CallDeferred(nameof(PlaySwapFeedback), firstInstanceId.ToString(), secondInstanceId.ToString());
    }

    private void PlaySwapFeedback(string firstInstanceId, string secondInstanceId)
    {
        if (!Guid.TryParse(firstInstanceId, out var firstId)
            || !Guid.TryParse(secondInstanceId, out var secondId))
        {
            return;
        }

        AnimateSwappedItem(FindItemControl(firstId));
        AnimateSwappedItem(FindItemControl(secondId));
    }

    private InventoryItemUI? FindItemControl(Guid instanceId)
    {
        foreach (var slotUi in _slotUis.Values)
        {
            var itemUi = slotUi.GetItemControl();
            if (itemUi?.Item?.InstanceId == instanceId)
            {
                return itemUi;
            }
        }

        if (_backpackFlow != null)
        {
            foreach (var child in _backpackFlow.GetChildren())
            {
                if (child is InventoryItemUI itemUi && itemUi.Item?.InstanceId == instanceId)
                {
                    return itemUi;
                }
            }
        }

        return null;
    }

    private static void AnimateSwappedItem(Control? itemUi)
    {
        if (itemUi == null)
        {
            return;
        }

        itemUi.PivotOffset = itemUi.Size * 0.5f;
        itemUi.Scale = new Vector2(0.92f, 0.92f);
        itemUi.Modulate = new Color(0.75f, 0.95f, 1.0f, 0.82f);
        var tween = itemUi.CreateTween().SetParallel();
        tween.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tween.TweenProperty(itemUi, "scale", Vector2.One, 0.18);
        tween.TweenProperty(itemUi, "modulate", Colors.White, 0.18);
    }

    private void PlaySwapSound()
    {
        if (_swapAudioPlayer == null)
        {
            _swapAudioPlayer = new AudioStreamPlayer
            {
                Stream = CreateSwapSound()
            };
            AddChild(_swapAudioPlayer);
        }

        _swapAudioPlayer.Play();
    }

    // 项目当前没有可复用的背包音效资源。这里生成一次很短的低音量机械点击并缓存，
    // 避免一次交换被拆成“卸下 + 装备”两次声音；以后有正式SFX时只需替换Stream。
    private static AudioStreamWav CreateSwapSound()
    {
        const int sampleRate = 22050;
        const int sampleCount = 1764; // 约80ms
        var data = new byte[sampleCount * 2];
        for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            var time = sampleIndex / (double)sampleRate;
            var envelope = 1.0 - sampleIndex / (double)sampleCount;
            var wave = Math.Sin(2.0 * Math.PI * 230.0 * time)
                + 0.35 * Math.Sin(2.0 * Math.PI * 460.0 * time);
            var value = (short)(wave * envelope * 2200.0);
            data[sampleIndex * 2] = (byte)(value & 0xff);
            data[sampleIndex * 2 + 1] = (byte)((value >> 8) & 0xff);
        }

        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = sampleRate,
            Stereo = false,
            Data = data
        };
    }

    private void OnUnequipDropped(Guid instanceId)
    {
        if (!InventoryManager.UnequipToBackpack(instanceId))
        {
            ShowHint(Localization.Get("inventory.hint.unequip_universal_first"));
            return;
        }
        RefreshView();
    }

    private void OnSellDropped(Guid instanceId)
    {
        // 阵营命运·倒手重铸：本 Run 前3次出售装备时，允许玩家放弃金币、换成同品质的
        // 另一件未拥有装备。只有命中这个命运、次数未用完、且装备本身可出售时才弹出二选一，
        // 否则直接走原有的正常出售流程，不改变既有行为。
        var owned = InventoryManager.FindOwned(instanceId);
        // 【重铸器】的下一次出售是强制同品质替换，优先于阵营命运的“出售/重铸二选一”。
        // 否则会先弹出命运面板，导致重铸器的自动效果无法落到这次出售上。
        var reforgerSaleWasArmed = InventoryManager.IsReforgerSaleArmed;
        if (owned != null
            && !reforgerSaleWasArmed
            && FactionFateManager.ShouldOfferReforgeChoice(owned))
        {
            var hasCandidate = FactionFateManager.TryFindReforgeCandidate(owned.Definition, out var candidate);
            if (!hasCandidate)
            {
                // 没有任何合法重铸候选：跳过弹窗，直接按正常出售处理，但仍然算作消耗了一次机会。
                var sellResult = InventoryManager.SellItem(instanceId);
                FactionFateManager.ResolveSaleOpportunity();
                if (sellResult < 0)
                {
                    ShowHint(sellResult == -2
                        ? Localization.Get("inventory.hint.unsellable")
                        : Localization.Get("inventory.hint.unequip_universal_first"));
                    return;
                }
                RefreshView();
                return;
            }

            ShowReforgeOrSellDialog(instanceId, owned, candidate!);
            return;
        }

        var result = InventoryManager.SellItem(instanceId);
        if (result < 0)
        {
            ShowHint(result == -2
                ? Localization.Get("inventory.hint.unsellable")
                : Localization.Get("inventory.hint.unequip_universal_first"));
            return;
        }
        if (reforgerSaleWasArmed)
        {
            FactionFateManager.ResolveSaleOpportunity();
        }
        RefreshView();
        if (InventoryManager.LastSaleUsedReforger)
        {
            ShowReforgeResult(
                InventoryManager.LastSaleReforgeOriginal,
                InventoryManager.LastSaleReforgeReplacement);
        }
    }

    /// <summary>
    /// 阵营命运·倒手重铸 的二选一确认弹窗：项目里没有现成的通用二选一弹窗组件，用 Godot 内置
    /// ConfirmationDialog 承载。"重铸"必须是通过 AddButton 挂的独立自定义按钮（CustomAction 信号），
    /// 不能复用 Cancel 按钮——ConfirmationDialog 关闭窗口（点X/按Esc）也会触发 Canceled 信号，
    /// 如果把"重铸"绑在 Canceled 上，玩家想单纯关掉弹窗、放弃这次出售，也会被误判成"选择重铸"，
    /// 导致原装备被误删。真正的"关闭/放弃"分支（Canceled）不做任何数据变更、也不消耗次数，
    /// 因为玩家还没有做出任何选择。
    /// </summary>
    private void ShowReforgeOrSellDialog(Guid instanceId, OwnedEquipment owned, EquipmentDefinition candidate)
    {
        const string ReforgeAction = "faction_fate_reforge";
        var dialog = new ConfirmationDialog
        {
            DialogText = string.Format(
                Localization.Get("factionfate.reforge_prompt_fmt"),
                Localization.GetName(owned.Definition),
                Localization.GetName(candidate)),
            OkButtonText = Localization.Get("factionfate.sell_normal")
        };
        dialog.AddButton(Localization.Get("factionfate.reforge"), true, ReforgeAction);
        AddChild(dialog);

        dialog.Confirmed += () =>
        {
            InventoryManager.SellItem(instanceId);
            FactionFateManager.ResolveSaleOpportunity();
            dialog.QueueFree();
            RefreshView();
        };
        dialog.CustomAction += action =>
        {
            if (action != ReforgeAction) return;
            var reforged = InventoryManager.ReforgeWithoutSelling(instanceId, candidate);
            FactionFateManager.ResolveSaleOpportunity();
            dialog.QueueFree();
            RefreshView();
            ShowHint(reforged
                ? Localization.GetFmt(
                    "equipment.reforge.success_fmt",
                    Localization.GetName(owned.Definition),
                    Localization.GetName(candidate))
                : Localization.Get("equipment.reforge.not_found"));
        };
        dialog.Canceled += dialog.QueueFree; // 单纯关闭弹窗：不出售、不重铸、不消耗次数。
        dialog.PopupCentered();
    }

    private void ShowHint(string message)
    {
        var dialog = new AcceptDialog { DialogText = message };
        AddChild(dialog);
        dialog.Confirmed += () => dialog.QueueFree();
        dialog.PopupCentered();
    }

    /// <summary>
    /// 展示重铸器触发后的确切替换结果。不能只显示“同品质装备”的泛化提示，
    /// 否则玩家无法得知出售的装备实际被替换成了什么。
    /// </summary>
    private void ShowReforgeResult(
        EquipmentDefinition? original,
        EquipmentDefinition? replacement)
    {
        var message = original != null && replacement != null
            ? Localization.GetFmt(
                "equipment.reforge.success_fmt",
                Localization.GetName(original),
                Localization.GetName(replacement))
            : "重铸器已触发，但未能读取重铸结果。";

        var dialog = new AcceptDialog
        {
            Title = Localization.Get("equipment.reforge.result_title"),
            DialogText = message,
            MinSize = new Vector2I(520, 0)
        };
        AddChild(dialog);
        dialog.Confirmed += () => dialog.QueueFree();
        dialog.PopupCentered();
    }

    private void OnHoverChanged(OwnedEquipment? item)
    {
        ShowHoverInfo(item);
    }

    private void ShowHoverInfo(OwnedEquipment? item)
    {
        if (_infoNameLabel == null || _infoRarityLabel == null || _infoTypesLabel == null
            || _infoDescriptionLabel == null || _infoEffectsLabel == null || _infoSellPriceLabel == null)
        {
            return;
        }

        if (item == null)
        {
            _infoNameLabel.Text = Localization.Get("inventory.hover_hint");
            _infoRarityLabel.Text = string.Empty;
            _infoTypesLabel.Text = string.Empty;
            _infoDescriptionLabel.Text = string.Empty;
            _infoEffectsLabel.Text = string.Empty;
            _infoSellPriceLabel.Text = string.Empty;
            return;
        }

        var definition = item.Definition;
        _infoNameLabel.Text = Localization.GetName(definition);
        _infoRarityLabel.Text = GetRarityDisplayName(definition.Rarity);
        _infoTypesLabel.Text = GetTypesDisplayText(definition.Types);
        _infoDescriptionLabel.Text = Localization.GetEquipmentFlavorDescription(definition);

        var effectLines = new List<string>();
        for (var i = 0; i < definition.Effects.Count; i++)
        {
            effectLines.Add(GetEquipmentEffectText(definition, i));
        }
        _infoEffectsLabel.Text = effectLines.Count > 0 ? string.Join("\n", effectLines) : Localization.Get("inventory.none");

        _infoSellPriceLabel.Text = InventoryManager.GetSellPrice(definition.Rarity).ToString();
    }

    private static string GetRarityDisplayName(EquipmentRarity rarity)
        => Localization.GetRarityName(rarity);

    private static string GetEquipmentEffectText(EquipmentDefinition definition, int index)
    {
        return Localization.GetEquipmentEffectDescription(definition, index);
    }

    private void RefreshStaticText()
    {
        if (_titleLabel != null) _titleLabel.Text = Localization.Get("inventory.title");
        if (_debugButton != null) _debugButton.Text = Localization.Get("inventory.debug");
        if (_returnButton != null) _returnButton.Text = GetReturnButtonText();
        if (_chipStatsTitleLabel != null) _chipStatsTitleLabel.Text = Localization.Get("inventory.chip_stats");
        if (_descriptionTitleLabel != null) _descriptionTitleLabel.Text = Localization.Get("inventory.description_label");
        if (_effectsTitleLabel != null) _effectsTitleLabel.Text = Localization.Get("inventory.effects_label");
        if (_sellTitleLabel != null) _sellTitleLabel.Text = Localization.Get("inventory.sell_price_label");
        _sellZone?.RefreshLocalization();
    }

    private string GetReturnButtonText()
    {
        if (!string.IsNullOrWhiteSpace(ReturnButtonTextKey))
        {
            return Localization.Get(ReturnButtonTextKey);
        }

        return string.IsNullOrWhiteSpace(ReturnButtonLabel)
            ? Localization.Get("ui.back_to_map")
            : ReturnButtonLabel;
    }

    // 打开调试装备面板（覆盖层），面板关闭后自动移除；仅在 DebugMapEnabled 时可触达。
    private void OpenDebugPanel()
    {
        var panel = new DebugInventoryPanel
        {
            OnInventoryChanged = RefreshView
        };
        panel.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(panel);
    }

    /// <summary>
    /// 装备类型的展示文本；internal 是为了让 InventoryItemUI 的悬停 Tooltip
    /// 复用同一份格式，不用再写一份重复逻辑。
    /// </summary>
    internal static string GetTypesDisplayText(IReadOnlyList<EquipmentType> types)
    {
        var names = new List<string>();
        foreach (var type in types)
        {
            var name = type switch
            {
                EquipmentType.Buff => Localization.GetEquipmentTypeName(type),
                EquipmentType.Weapon => Localization.GetEquipmentTypeName(type),
                EquipmentType.Armor => Localization.GetEquipmentTypeName(type),
                EquipmentType.Vehicle => Localization.GetEquipmentTypeName(type),
                EquipmentType.Mount => Localization.GetEquipmentTypeName(type),
                EquipmentType.Defense => Localization.GetEquipmentTypeName(type),
                EquipmentType.Attack => Localization.GetEquipmentTypeName(type),
                EquipmentType.Accessory => Localization.GetEquipmentTypeName(type),
                _ => null
            };

            if (name != null)
            {
                names.Add(name);
            }
        }

        return names.Count > 0 ? string.Join(" / ", names) : string.Empty;
    }
}

// 背包放置区：接受“卸下装备”的拖放（来自任意装备槽），将其放回背包。
/// <summary>
/// Inventory System 的公开类：BackpackDropZone。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class BackpackDropZone : PanelContainer
{
    public Action<Guid>? OnItemDropped;
    public FlowContainer? Flow { get; private set; }

    /// <summary>
    /// Inventory System 的公开入口：BuildLayout。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void BuildLayout()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.11f, 0.12f, 0.14f),
            ContentMarginBottom = 12,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 12
        };
        AddThemeStyleboxOverride("panel", style);

        Flow = new HFlowContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        Flow.AddThemeConstantOverride("h_separation", 10);
        Flow.AddThemeConstantOverride("v_separation", 10);
        AddChild(Flow);
    }

    /// <summary>
    /// Inventory System 的公开入口：_CanDropData。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return InventoryDragPayload.TryParse(data, out _);
    }

    /// <summary>
    /// Inventory System 的公开入口：_DropData。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (InventoryDragPayload.TryParse(data, out var instanceId))
        {
            OnItemDropped?.Invoke(instanceId);
        }
    }
}

// 出售区：放在界面顶部，接受任意装备的拖放（已装备装备会先自动卸下再出售）。
/// <summary>
/// Inventory System 的公开类：SellDropZone。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class SellDropZone : PanelContainer
{
    public Action<Guid>? OnItemSold;
    private Label? _label;

    /// <summary>
    /// Inventory System 的公开入口：BuildLayout。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void BuildLayout()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.30f, 0.16f, 0.16f),
            BorderColor = new Color(0.70f, 0.30f, 0.26f),
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8
        };
        AddThemeStyleboxOverride("panel", style);

        var label = new Label
        {
            Text = Localization.Get("inventory.sell_zone"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _label = label;
        label.AddThemeFontSizeOverride("font_size", 22);
        AddChild(label);
    }

    /// <summary>
    /// Inventory System 的公开入口：RefreshLocalization。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void RefreshLocalization()
    {
        if (_label != null)
        {
            _label.Text = Localization.Get("inventory.sell_zone");
        }
    }

    /// <summary>
    /// Inventory System 的公开入口：_CanDropData。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return InventoryDragPayload.TryParse(data, out _);
    }

    /// <summary>
    /// Inventory System 的公开入口：_DropData。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (InventoryDragPayload.TryParse(data, out var instanceId))
        {
            OnItemSold?.Invoke(instanceId);
        }
    }
}
