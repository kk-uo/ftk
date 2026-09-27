//////////////////////////////////////////////////////////
// 文件：Scripts/EventController.cs
//
// 模块：Event System
//
// 职责：
// 1. 承载地图事件、事件选项与事件奖励相关代码。
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
using System.Linq;

/// <summary>
/// Event System 的公开类：EventController。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class EventController : Control
{
    [Signal]
    public delegate void EventClosedEventHandler();

    [Signal]
    public delegate void ShopRequestedEventHandler();

    [Signal]
    public delegate void VehicleShopRequestedEventHandler();

    [Signal]
    public delegate void WitchShopRequestedEventHandler();

    [Signal]
    public delegate void BlackMarketShopRequestedEventHandler();

    [Signal]
    public delegate void SpecialBattleRequestedEventHandler();

    [Signal]
    public delegate void EpicEquipmentChoiceRequestedEventHandler(int bonusGold);

    [Signal]
    public delegate void LegendaryEquipmentChoiceRequestedEventHandler();

    [Signal]
    public delegate void LegendaryWeaponChoiceRequestedEventHandler();

    [Signal]
    public delegate void RareOrEpicEquipmentChoiceRequestedEventHandler();

    [Signal]
    public delegate void ChipDoubleChoiceRequestedEventHandler();

    [Signal]
    public delegate void ChipChoiceRequestedEventHandler();

    [Signal]
    public delegate void SkillChoiceRequestedEventHandler();

    [Signal]
    public delegate void SacrificeEquipmentForUpgradeRequestedEventHandler();

    [Signal]
    public delegate void ElfElementChoiceRequestedEventHandler();

    [Signal]
    public delegate void ElfEquipmentReforgeRequestedEventHandler();

    [Signal]
    public delegate void ModificationShopWeaponReforgeRequestedEventHandler();

    [Export]
    public string NodeId { get; set; } = string.Empty;

    private EventData? _eventData;
    private Label? _titleLabel;
    private RichTextLabel? _descriptionLabel;
    private VBoxContainer? _optionsContainer;
    private Label? _feedbackLabel;
    private EventOption? _pendingShopOption;

    /// <summary>
    /// Event System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        TextureFilter = TextureFilterEnum.Nearest;
        BuildLayout();

        // 整合式教程：只在教学事件节点上叠加一句"事件会提供不同选择"的说明，
        // 不高亮任何具体控件（选项按钮本身已经是真实、完整可点的事件界面）。
        if (IntegratedTutorialFlow.IsActive && NodeId == IntegratedTutorialFlow.TutorialEventNodeId)
        {
            var tutorialLayer = new TutorialHighlightLayer();
            AddChild(tutorialLayer);
            tutorialLayer.ShowStep(
                Localization.Get("tutorial.integrated.event.title"),
                Localization.Get("tutorial.integrated.event.desc"),
                string.Empty,
                string.Empty,
                showContinueButton: false);
        }
    }

    private void BuildLayout()
    {
        var node = GameManager.GetNode(NodeId);
        if (node == null)
        {
            EmitSignal(SignalName.EventClosed);
            return;
        }

        _eventData = EventManager.GetEventForNode(node);
        if (_eventData == null)
        {
            GameManager.MarkCurrentNodeCleared();
            EmitSignal(SignalName.EventClosed);
            return;
        }

        CodexService.RecordEventEncounter(_eventData.Id);

        var background = new ColorRect
        {
            Color = new Color(0.09f, 0.10f, 0.11f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        // 事件选择同样会消耗/获得局内资源，因此左上角复用地图的真实状态面板。
        // 组件读取同一份 GameManager 状态，避免事件里显示的电量、金币或全局效果
        // 与返回地图后不一致。
        var runStatusPanel = MapController.CreateRunStatusPanel(out _);
        runStatusPanel.SetAnchorsPreset(LayoutPreset.TopLeft);
        runStatusPanel.Position = new Vector2(24, 24);
        runStatusPanel.ZIndex = 10;
        AddChild(runStatusPanel);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(1180, 860)
        };
        panel.AddThemeStyleboxOverride("panel", UIResourceDatabase.CreateEventFrameStyle());
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_top", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_bottom", 28);
        panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 20);
        margin.AddChild(root);

        var title = new Label
        {
            Text = _eventData.DisplayName,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 52);
        var titlePanel = new PanelContainer();
        titlePanel.AddThemeStyleboxOverride("panel", UIResourceDatabase.CreateHeaderStyle());
        titlePanel.AddChild(title);
        root.AddChild(titlePanel);
        _titleLabel = title;

        root.AddChild(UIResourceDatabase.CreateDivider());

        _descriptionLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            Text = ResolveEventDescription(_eventData.DisplayDescription)
        };
        _descriptionLabel.AddThemeFontSizeOverride("normal_font_size", 28);
        var descriptionPanel = new PanelContainer();
        descriptionPanel.AddThemeStyleboxOverride("panel", UIResourceDatabase.CreateDescriptionStyle(eventStyle: true));
        descriptionPanel.AddChild(_descriptionLabel);
        root.AddChild(descriptionPanel);

        _feedbackLabel = new Label
        {
            Text = string.Empty,
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false
        };
        _feedbackLabel.AddThemeFontSizeOverride("font_size", 22);
        _feedbackLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.45f, 0.40f));
        root.AddChild(_feedbackLabel);

        _optionsContainer = new VBoxContainer();
        _optionsContainer.AddThemeConstantOverride("separation", 14);
        root.AddChild(_optionsContainer);

        RenderOptionButtons();
    }

    // 通用动态文本补丁：当选项的奖励类型是 StinkyMushroomEquipmentForOption（或未来其它同类
    // "结果由本局随机分配决定"的奖励类型）时，把最终会获得的具体装备名称追加到描述文本里，
    // 让玩家在选择前就能看到自己实际会拿到哪件装备。判断依据是奖励 Type，不依赖事件Id/名称，
    // 因此可以直接被未来复用同一奖励类型的其它事件复用。
    private static string BuildOptionDisplayDescription(EventOption option)
    {
        var text = option.DisplayDescription;
        foreach (var reward in option.Rewards)
        {
            if (reward.Type != EventRewardType.StinkyMushroomEquipmentForOption)
            {
                continue;
            }

            var equipId = GameManager.GetStinkyMushroomColorForOption(reward.Amount);
            var definition = EquipmentDatabase.GetEquipment(equipId);
            if (definition != null)
            {
                text += "\n" + Localization.GetFmt(
                    "event.reward.gain_equipment_fmt",
                    Localization.GetName(definition));
            }
        }

        return text;
    }

    private void RenderOptionButtons()
    {
        if (_optionsContainer == null || _eventData == null)
        {
            return;
        }

        ClearOptionButtons();

        foreach (var option in EventManager.GetVisibleOptions(_eventData))
        {
            var button = new Button
            {
                Text = $"{option.DisplayName}  {BuildOptionDisplayDescription(option)}",
                CustomMinimumSize = new Vector2(0, 56),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            button.AddThemeFontSizeOverride("font_size", 26);
            UIResourceDatabase.ApplyEventOptionButton(button);
            if (!EventManager.CanAffordOption(option, out var disabledReason))
            {
                button.Text = $"{button.Text}\n（{disabledReason}）";
                button.Disabled = true;
            }
            else if (HasModificationShopWeaponReforgeReward(option) && !HasReforgeableOwnedWeapon())
            {
                button.Text = $"{button.Text}\n（{Localization.Get("event.modification_shop.no_weapon")}）";
                button.Disabled = true;
            }
            else if (HasSacrificeEquipmentForUpgradeReward(option) && !HasUpgradeableOwnedEquipment())
            {
                button.Text = $"{button.Text}\n（{Localization.Get("equipment.reforge.no_candidate")}）";
                button.Disabled = true;
            }
            else
            {
                button.Pressed += () => OnOptionPressed(option);
            }

            // 阵营命运·改命：只有事件数据显式标记 CanFactionReroll 且当前命中改命命运时才显示。
            // 当前没有任何事件数据设置这个字段，这里只保证机制打通，不追求视觉打磨。
            if (option.CanFactionReroll && FactionFateManager.CurrentFateId == FactionFateIds.Reroll)
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                row.AddChild(button);

                var rerollButton = new Button
                {
                    Text = Localization.Get("factionfate.reforge_refresh"),
                    CustomMinimumSize = new Vector2(90, 56),
                    Disabled = FactionFateManager.RerollRemaining <= 0
                };
                rerollButton.AddThemeFontSizeOverride("font_size", 20);
                var capturedOption = option;
                rerollButton.Pressed += () => OnRerollOptionPressed(capturedOption);
                row.AddChild(rerollButton);

                _optionsContainer.AddChild(row);
            }
            else
            {
                _optionsContainer.AddChild(button);
            }
        }
    }

    /// <summary>
    /// 阵营命运·改命 的事件选项刷新：当前只支持 RefreshPoolId 命中
    /// "rare_equipment"/"legendary_equipment" 这两个字符串（对应稀有/传说装备池重抽），
    /// 其它 RefreshPoolId 值先返回不可刷新——因为当前没有任何事件数据实际使用这个机制，
    /// 保持最小实现即可，不做过度设计。
    /// </summary>
    private void OnRerollOptionPressed(EventOption option)
    {
        var rarity = option.RefreshPoolId switch
        {
            "rare_equipment" => EquipmentRarity.Rare,
            "legendary_equipment" => EquipmentRarity.Legendary,
            _ => (EquipmentRarity?)null
        };

        if (rarity == null)
        {
            ShowFeedback(Localization.Get("factionfate.no_reforge_candidate"));
            return;
        }

        var rarityValue = rarity.Value;
        var succeeded = FactionFateManager.TryRerollEventOption(
            option,
            () => new EquipmentChoiceProvider { Count = 1, Rarities = new[] { rarityValue } }
                .CreateChoices()
                .FirstOrDefault(),
            out var replacement);

        if (!succeeded || replacement?.Payload is not EquipmentDefinition newDefinition)
        {
            ShowFeedback(Localization.Get("factionfate.no_reforge_candidate"));
            return;
        }

        // 替换该选项原有的装备类奖励，改为发放新抽到的装备；不影响该选项其它类型的奖励。
        option.Rewards.RemoveAll(r => r.Type is EventRewardType.Equipment or EventRewardType.RandomEquipmentByRarity);
        option.Rewards.Add(new EventReward { Type = EventRewardType.Equipment, StringValue = newDefinition.Id });

        RenderOptionButtons();
    }

    private void ClearOptionButtons()
    {
        if (_optionsContainer == null)
        {
            return;
        }

        // RemoveChild 会立即让 VBoxContainer 重新计算剩余子项布局；QueueFree 只负责
        // 安全释放节点。这样切换事件或条件集合时不会短暂保留空白占位。
        foreach (var child in _optionsContainer.GetChildren())
        {
            _optionsContainer.RemoveChild(child);
            child.QueueFree();
        }
    }

    private void OnOptionPressed(EventOption option)
    {
        if (_eventData == null)
        {
            EmitSignal(SignalName.EventClosed);
            return;
        }

        // 武器候选必须在扣款前验证；否则空背包会先失去75金币，再进入无法完成的选择界面。
        if (HasModificationShopWeaponReforgeReward(option) && !HasReforgeableOwnedWeapon())
        {
            ShowFeedback(Localization.Get("event.modification_shop.no_weapon"));
            return;
        }

        if (HasSacrificeEquipmentForUpgradeReward(option) && !HasUpgradeableOwnedEquipment())
        {
            ShowFeedback(Localization.Get("equipment.reforge.no_candidate"));
            return;
        }

        if (!EventManager.TryResolveOption(_eventData, option, out var resultText))
        {
            ShowFeedback(resultText);
            return;
        }

        GameManager.MarkCurrentNodeCleared();

        if (HasOpenShopReward(option))
        {
            _pendingShopOption = option;
            EmitSignal(SignalName.ShopRequested);
            return;
        }

        if (HasOpenVehicleShopReward(option))
        {
            EmitSignal(SignalName.VehicleShopRequested);
            return;
        }

        if (HasOpenWitchShopReward(option))
        {
            EmitSignal(SignalName.WitchShopRequested);
            return;
        }

        if (HasOpenBlackMarketShopReward(option))
        {
            EmitSignal(SignalName.BlackMarketShopRequested);
            return;
        }

        if (HasTreasurePavilionBattleReward(option) || GameManager.HasActiveSpecialBattle)
        {
            EmitSignal(SignalName.SpecialBattleRequested);
            return;
        }

        var epicChoiceBonusGold = GetEpicEquipmentChoiceBonusGold(option);
        if (epicChoiceBonusGold >= 0)
        {
            EmitSignal(SignalName.EpicEquipmentChoiceRequested, epicChoiceBonusGold);
            return;
        }

        if (HasLegendaryWeaponChoiceReward(option))
        {
            EmitSignal(SignalName.LegendaryWeaponChoiceRequested);
            return;
        }

        if (HasLegendaryEquipmentChoiceReward(option))
        {
            EmitSignal(SignalName.LegendaryEquipmentChoiceRequested);
            return;
        }

        if (HasRareOrEpicEquipmentChoiceReward(option))
        {
            EmitSignal(SignalName.RareOrEpicEquipmentChoiceRequested);
            return;
        }

        if (HasChipDoubleChoiceReward(option))
        {
            EmitSignal(SignalName.ChipDoubleChoiceRequested);
            return;
        }

        if (HasChipChoiceReward(option))
        {
            EmitSignal(SignalName.ChipChoiceRequested);
            return;
        }

        if (HasSkillChoiceReward(option))
        {
            EmitSignal(SignalName.SkillChoiceRequested);
            return;
        }

        if (HasSacrificeEquipmentForUpgradeReward(option))
        {
            EmitSignal(SignalName.SacrificeEquipmentForUpgradeRequested);
            return;
        }

        if (HasElfElementChoiceReward(option))
        {
            EmitSignal(SignalName.ElfElementChoiceRequested);
            return;
        }

        if (HasElfEquipmentReforgeReward(option))
        {
            EmitSignal(SignalName.ElfEquipmentReforgeRequested);
            return;
        }

        if (HasModificationShopWeaponReforgeReward(option))
        {
            EmitSignal(SignalName.ModificationShopWeaponReforgeRequested);
            return;
        }

        if (HasTriggerEventReward(option, out var targetEventId))
        {
            var targetEvent = EventDatabase.GetEvent(targetEventId);
            if (targetEvent != null)
            {
                LoadEvent(targetEvent);
                return;
            }

            EmitSignal(SignalName.EventClosed);
            return;
        }

        if (HasTriggerRandomChapterEventReward(option, out var targetChapter))
        {
            var targetEvent = EventManager.GetRandomNonShopEventForChapter(targetChapter);
            if (targetEvent != null)
            {
                LoadEvent(targetEvent);
                return;
            }

            EmitSignal(SignalName.EventClosed);
            return;
        }

        // 某些事件把全部后果直接写在选项中，不应再额外显示结算结果页。
        // 此项为事件级配置，避免奖励产生的动态文本意外改变该类事件的交互流程。
        if (_eventData.SuppressResultPresentation)
        {
            EmitSignal(SignalName.EventClosed);
            return;
        }

        if (string.IsNullOrEmpty(resultText))
        {
            EmitSignal(SignalName.EventClosed);
            return;
        }

        ShowResult(resultText);
    }

    // 隐藏选项区域并展示结算结果文本 + 单个确认按钮，玩家点击后才真正关闭事件返回地图。
    // 仅在选项产生了文本（动态结算文案或选项自身预设的 ResultText）时才会进入该状态；
    // 没有结果文本的既有事件保持原来的“选择即关闭”行为，不受影响。
    private void ShowResult(string resultText)
    {
        if (_descriptionLabel == null || _optionsContainer == null || _feedbackLabel == null)
        {
            EmitSignal(SignalName.EventClosed);
            return;
        }

        _feedbackLabel.Visible = false;
        _descriptionLabel.Text = resultText;

        ClearOptionButtons();

        var confirmButton = new Button
        {
            Text = Localization.Get("event.confirm"),
            CustomMinimumSize = new Vector2(0, 56)
        };
        confirmButton.AddThemeFontSizeOverride("font_size", 26);
        UIResourceDatabase.ApplyEventOptionButton(confirmButton);
        confirmButton.Pressed += () => EmitSignal(SignalName.EventClosed);
        _optionsContainer.AddChild(confirmButton);
    }

    private void ShowFeedback(string message)
    {
        if (_feedbackLabel == null)
        {
            return;
        }

        _feedbackLabel.Text = message;
        _feedbackLabel.Visible = !string.IsNullOrEmpty(message);
    }

    private void LoadEvent(EventData eventData)
    {
        _eventData = eventData;
        if (_titleLabel != null)
        {
            _titleLabel.Text = eventData.DisplayName;
        }

        if (_descriptionLabel != null)
        {
            _descriptionLabel.Text = ResolveEventDescription(eventData.DisplayDescription);
        }

        if (_feedbackLabel != null)
        {
            _feedbackLabel.Text = string.Empty;
            _feedbackLabel.Visible = false;
        }

        RenderOptionButtons();
    }

    private static bool HasTriggerEventReward(EventOption option, out string targetEventId)
    {
        if (RewardManager.HasJumpEvent(option.RewardSequence, out targetEventId))
        {
            return true;
        }

        foreach (var reward in option.Rewards)
        {
            if (reward.Type == EventRewardType.TriggerEvent)
            {
                targetEventId = reward.StringValue;
                return true;
            }
        }

        targetEventId = string.Empty;
        return false;
    }

    private static bool HasTriggerRandomChapterEventReward(EventOption option, out int targetChapter)
    {
        foreach (var reward in option.Rewards)
        {
            if (reward.Type == EventRewardType.TriggerRandomChapterNonShopEvent)
            {
                targetChapter = reward.Amount;
                return true;
            }
        }

        targetChapter = -1;
        return false;
    }

    private static bool HasOpenShopReward(EventOption option)
    {
        foreach (var reward in option.Rewards)
        {
            if (reward.Type == EventRewardType.OpenShopUI)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasOpenBlackMarketShopReward(EventOption option)
    {
        foreach (var reward in option.Rewards)
        {
            if (reward.Type == EventRewardType.OpenBlackMarketShop)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasOpenVehicleShopReward(EventOption option)
    {
        foreach (var reward in option.Rewards)
        {
            if (reward.Type == EventRewardType.OpenVehicleShop)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasOpenWitchShopReward(EventOption option)
    {
        foreach (var reward in option.Rewards)
        {
            if (reward.Type == EventRewardType.OpenWitchShop)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasTreasurePavilionBattleReward(EventOption option)
    {
        if (RewardManager.HasJumpBattle(option.RewardSequence))
        {
            return true;
        }

        foreach (var reward in option.Rewards)
        {
            if (reward.Type == EventRewardType.StartTreasurePavilionBattle)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLegendaryEquipmentChoiceReward(EventOption option)
    {
        foreach (var reward in option.Rewards)
        {
            if (reward.Type == EventRewardType.ChooseOneLegendaryEquipment)
                return true;
        }
        return false;
    }

    private static bool HasLegendaryWeaponChoiceReward(EventOption option)
    {
        foreach (var reward in option.Rewards)
        {
            if (reward.Type == EventRewardType.ChooseOneLegendaryWeaponEquipment)
                return true;
        }
        return false;
    }

    private static bool HasRareOrEpicEquipmentChoiceReward(EventOption option)
    {
        foreach (var reward in option.Rewards)
        {
            if (reward.Type == EventRewardType.ChooseOneRareOrEpicEquipment)
                return true;
        }
        return false;
    }

    private static bool HasChipDoubleChoiceReward(EventOption option)
    {
        foreach (var reward in option.Rewards)
        {
            if (reward.Type == EventRewardType.ChooseOneChipTwice)
                return true;
        }
        return false;
    }

    private static bool HasChipChoiceReward(EventOption option)
    {
        if (RewardManager.HasChoiceRequest(option.RewardSequence, RewardChoiceType.Chip))
        {
            return true;
        }

        foreach (var reward in option.Rewards)
        {
            if (reward.Type == EventRewardType.ChooseOneChipOnce)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasSkillChoiceReward(EventOption option)
        => RewardManager.HasChoiceRequest(option.RewardSequence, RewardChoiceType.Skill);

    private static bool HasSacrificeEquipmentForUpgradeReward(EventOption option)
    {
        foreach (var reward in option.Rewards)
        {
            if (reward.Type == EventRewardType.SacrificeOneEquipmentForUpgrade)
                return true;
        }
        return false;
    }

    private static bool HasElfElementChoiceReward(EventOption option)
    {
        if (RewardManager.HasChoiceRequest(option.RewardSequence, RewardChoiceType.Element))
        {
            return true;
        }

        foreach (var reward in option.Rewards)
        {
            if (reward.Type == EventRewardType.ChooseElfElementBlessing)
                return true;
        }
        return false;
    }

    private static bool HasElfEquipmentReforgeReward(EventOption option)
    {
        foreach (var reward in option.Rewards)
        {
            if (reward.Type == EventRewardType.ElfEquipmentReforge)
                return true;
        }
        return false;
    }

    private static bool HasModificationShopWeaponReforgeReward(EventOption option)
    {
        foreach (var reward in option.Rewards)
        {
            if (reward.Type == EventRewardType.ModificationShopWeaponReforge)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasReforgeableOwnedWeapon()
    {
        foreach (var item in InventoryManager.GetAllOwned())
        {
            if (InventoryManager.GetSlotCategory(item.Definition) == EquipmentSlotCategory.Weapon
                && InventoryManager.GetReforgeCandidates(item.Definition).Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasUpgradeableOwnedEquipment()
    {
        foreach (var item in InventoryManager.GetAllOwned())
        {
            if (RewardManager.GetEquipmentUpgradeCandidates(item.Definition).Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static int GetEpicEquipmentChoiceBonusGold(EventOption option)
    {
        foreach (var reward in option.Rewards)
        {
            if (reward.Type == EventRewardType.ChooseOneEpicEquipment)
            {
                return reward.Amount;
            }
        }

        return -1;
    }

    private static string ResolveEventDescription(string description)
    {
        return description.Replace("{chapter2_boss_name}", GameManager.GetCurrentChapterBossDisplayName(2), System.StringComparison.Ordinal);
    }
}
