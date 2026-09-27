//////////////////////////////////////////////////////////
// 文件：Scripts/MapController.cs
//
// 模块：Map System
//
// 职责：
// 1. 承载地图节点、章节路线与地图 UI相关代码。
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
using System.Text;

/// <summary>
/// Map System 的公开类：MapController。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class MapController : Control
{
    [Signal]
    public delegate void NodeSelectedEventHandler(string nodeId);

    [Signal]
    public delegate void ReturnToMenuRequestedEventHandler();

    [Signal]
    public delegate void InventoryRequestedEventHandler();

    private const string ExplorationScenePath = "res://Scenes/MapExploration.tscn";

    private Label? _messageLabel;
    private Label? _powerHudLabel;
    private Tween? _powerHudTween;
    private Button? _returnToMenuButton;
    private Button? _debugMapButton;
    private bool _nodeSelectionForwardQueued;
    private Window? _debugWindow;
    private CheckBox? _unlockAllCheckBox;
    private OptionButton? _variantOptionButton;
    private OptionButton? _routeOptionButton;
    private OptionButton? _bossPlanOptionButton;
    private Button? _advanceChapterButton;

    /// <summary>
    /// Map System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        DeveloperModeManager.ModeChanged += RefreshDevModeUi;
        Localization.LanguageChanged += RefreshMapView;
        BuildLayout();
    }

    /// <summary>
    /// Map System 的公开入口：_ExitTree。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _ExitTree()
    {
        DeveloperModeManager.ModeChanged -= RefreshDevModeUi;
        Localization.LanguageChanged -= RefreshMapView;
    }

    private void RefreshDevModeUi()
    {
        if (_debugMapButton != null)
            _debugMapButton.Visible = DeveloperModeManager.IsDeveloperMode;
    }

    private void RefreshMapView()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        BuildLayout();
    }

    private void BuildLayout()
    {
        // 地图本体：铺满整个屏幕（100% ≥ 要求的 90%），不再缩在小窗口里。
        // 这是本方法第一个 AddChild，天然排在最底层，其余 UI 都盖在它上面。
        var explorationViewport = BuildExplorationViewport();
        explorationViewport.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(explorationViewport);
        if (GameManager.CurrentChapterVariant == ChapterVariant.Rainstorm)
            AddChild(new RainMapOverlay());

        // HUD 悬浮层：本身不拦截点击（MouseFilter=Ignore），真正的按钮/面板
        // 各自处理自己范围内的点击，空白区域的点击/按键会穿透到下面的地图。
        var hud = new Control
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        hud.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(hud);

        BuildTopLeftInfoPanel(hud);
        InitializePowerHudFeedback();
        BuildTopRightButtons(hud);
        BuildBottomCenterMessage(hud);
        BuildInventoryButton(hud);

        CreateDebugWindow();
    }

    /// <summary>
    /// 左上角地图 HUD 的电量变化反馈。实际标签由可复用的
    /// <see cref="CreateRunStatusPanel"/> 创建并挂入状态面板，不能使用屏幕绝对
    /// 坐标：全局效果列表会随 Buff 数量增长，固定坐标会与其文字重叠。
    /// 地图屏幕本身展示期间电量不会变化（电量只在 MainFlow.OnNodeSelected 里
    /// TrySpendPower 时改变，发生在离开地图屏幕之前），所以脉冲只能靠"进入这个
    /// 地图屏幕前后的电量对比"触发一次——见 GameManager.PowerBeforeLastNode。
    /// </summary>
    private void InitializePowerHudFeedback()
    {
        if (GameManager.Power > GameManager.PowerBeforeLastNode)
        {
            PlayPowerPulse(increase: true);
        }
        else if (GameManager.Power < GameManager.PowerBeforeLastNode)
        {
            PlayPowerPulse(increase: false);
        }

        GameManager.PowerBeforeLastNode = GameManager.Power;
    }

    private void PlayPowerPulse(bool increase)
    {
        if (_powerHudLabel == null)
        {
            return;
        }

        _powerHudTween?.Kill();
        _powerHudTween = CreateTween();
        _powerHudLabel.Modulate = increase ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.9f, 0.3f);
        var targetScale = increase ? new Vector2(1.25f, 1.25f) : new Vector2(0.85f, 0.85f);
        _powerHudTween.TweenProperty(_powerHudLabel, "scale", targetScale, 0.12)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _powerHudTween.Chain().TweenProperty(_powerHudLabel, "scale", Vector2.One, 0.15);
        _powerHudTween.Parallel().TweenProperty(_powerHudLabel, "modulate", Colors.White, 0.3).SetDelay(0.15);
    }

    /// <summary>
    /// 左上角悬浮信息面板：标题、当前角色、资源状态、Run Buff 面板。
    /// 半透明背景，压在地图上方，不再占用一整条独立的布局行。
    /// </summary>
    private void BuildTopLeftInfoPanel(Control hud)
    {
        var panel = CreateRunStatusPanel(out var powerHudLabel);
        _powerHudLabel = powerHudLabel;
        panel.SetAnchorsPreset(LayoutPreset.TopLeft);
        panel.Position = new Vector2(24, 24);
        hud.AddChild(panel);
    }

    /// <summary>
    /// 构建地图、事件等战斗外界面共用的局内状态面板。所有内容均从真实
    /// <see cref="GameManager"/> / <see cref="FactionFateManager"/> 读取，确保玩家
    /// 在事件做取舍时看到的粮草、金币、电量和全局效果与地图完全一致。
    /// </summary>
    public static PanelContainer CreateRunStatusPanel(out Label powerHudLabel)
    {
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.10f, 0.72f),
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            ContentMarginLeft = 20, ContentMarginRight = 20,
            ContentMarginTop = 16, ContentMarginBottom = 16
        });

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        panel.AddChild(root);

        var title = new Label
        {
            Text = string.Format(Localization.Get("map.title.fmt"), GameManager.GetChapterDisplayName(GameManager.CurrentChapter))
        };
        title.AddThemeFontSizeOverride("font_size", 34);
        root.AddChild(title);

        var subtitle = new Label
        {
            Text = GameManager.CurrentCharacter != null
                ? string.Format(Localization.Get("map.current_character"), Localization.GetName(GameManager.CurrentCharacter))
                : Localization.Get("map.no_character_selected")
        };
        subtitle.AddThemeFontSizeOverride("font_size", 20);
        root.AddChild(subtitle);

        var runInfo = new Label
        {
            Text = BuildRunInfoText()
        };
        runInfo.AddThemeFontSizeOverride("font_size", 18);
        runInfo.AddThemeColorOverride("font_color", new Color(0.86f, 0.82f, 0.64f));
        root.AddChild(runInfo);

        powerHudLabel = new Label
        {
            Text = string.Format(Localization.Get("map.hud.power.fmt"), GameManager.Power, GameManager.MaxPower)
        };
        powerHudLabel.AddThemeFontSizeOverride("font_size", 24);
        powerHudLabel.AddThemeColorOverride("font_color", Colors.White);
        powerHudLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        powerHudLabel.AddThemeConstantOverride("outline_size", 4);
        root.AddChild(powerHudLabel);

        root.AddChild(BuildRunBuffPanel());

        // 阵营命运 HUD：只在命中群阵营命运时非 null，纯文字块，不引入任何图片资源。
        var fatePanel = FactionFateManager.BuildHudPanel();
        if (fatePanel != null)
        {
            root.AddChild(fatePanel);
        }

        return panel;
    }

    /// <summary>
    /// 右上角悬浮按钮：返回主菜单 + 调试（仅开发者模式）。
    /// </summary>
    private void BuildTopRightButtons(Control hud)
    {
        var topBar = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        topBar.AddThemeConstantOverride("separation", 12);
        // 锚点收缩到右上角一个点，向左/向下展开到内容实际大小，不依赖布局前的 Size。
        topBar.SetAnchorsPreset(LayoutPreset.TopRight);
        topBar.GrowHorizontal = GrowDirection.Begin;
        topBar.GrowVertical = GrowDirection.End;
        topBar.OffsetRight = -24;
        topBar.OffsetTop = 24;
        hud.AddChild(topBar);

        var topReturnButton = new Button
        {
            Text = Localization.Get("ui.return_to_menu"),
            CustomMinimumSize = new Vector2(150, 46)
        };
        topReturnButton.AddThemeFontSizeOverride("font_size", 22);
        topReturnButton.Pressed += () => EmitSignal(SignalName.ReturnToMenuRequested);
        topBar.AddChild(topReturnButton);

        _debugMapButton = new Button
        {
            Text = Localization.Get("map.debug.title"),
            CustomMinimumSize = new Vector2(150, 46),
            Visible = DeveloperModeManager.IsDeveloperMode
        };
        _debugMapButton.AddThemeFontSizeOverride("font_size", 22);
        _debugMapButton.Pressed += ShowDebugWindow;
        topBar.AddChild(_debugMapButton);
    }

    /// <summary>
    /// 底部居中悬浮提示：选择节点提示 / 全部通关提示 + 返回主菜单按钮
    /// （只有全部通关时显示，和原来的行为一致）。
    /// </summary>
    private void BuildBottomCenterMessage(Control hud)
    {
        var root = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        root.AddThemeConstantOverride("separation", 12);
        // 锚点收缩到底部中心一个点，再用 GrowHorizontal/GrowVertical 从这个点向
        // 两侧/向上展开到内容实际需要的大小——不依赖布局前还不存在的 Size 数值。
        root.SetAnchorsPreset(LayoutPreset.CenterBottom);
        root.GrowHorizontal = GrowDirection.Both;
        root.GrowVertical = GrowDirection.Begin;
        root.OffsetBottom = -90;
        hud.AddChild(root);

        _messageLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _messageLabel.AddThemeFontSizeOverride("font_size", 28);
        _messageLabel.AddThemeColorOverride("font_color", Colors.White);
        _messageLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _messageLabel.AddThemeConstantOverride("outline_size", 4);
        _messageLabel.Text = GameManager.AllStagesCleared()
            ? Localization.Get("map.congratulations")
            : Localization.Get("map.select_node_hint");
        root.AddChild(_messageLabel);

        _returnToMenuButton = new Button
        {
            Text = Localization.Get("ui.return_to_menu"),
            CustomMinimumSize = new Vector2(180, 52),
            Visible = GameManager.AllStagesCleared(),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        _returnToMenuButton.AddThemeFontSizeOverride("font_size", 24);
        _returnToMenuButton.Pressed += () => EmitSignal(SignalName.ReturnToMenuRequested);
        root.AddChild(_returnToMenuButton);
    }

    /// <summary>右下角悬浮的【背包】按钮，位置和原来完全一致。</summary>
    private void BuildInventoryButton(Control hud)
    {
        var inventoryButton = new Button
        {
            Text = Localization.Get("ui.inventory"),
            CustomMinimumSize = new Vector2(120, 48)
        };
        inventoryButton.AddThemeFontSizeOverride("font_size", 22);
        inventoryButton.Pressed += () => EmitSignal(SignalName.InventoryRequested);
        inventoryButton.SetAnchorsPreset(LayoutPreset.BottomRight);
        inventoryButton.OffsetLeft = -140;
        inventoryButton.OffsetTop = -68;
        inventoryButton.OffsetRight = -24;
        inventoryButton.OffsetBottom = -24;
        hud.AddChild(inventoryButton);
    }

    /// <summary>
    /// 把 2D 地图探索场景（MapExploration.tscn）作为整个地图的唯一表现层：
    /// 不再是嵌在 UI 中间的小窗口，而是铺满全屏的 SubViewportContainer + SubViewport，
    /// 其余 UI（顶栏/调试窗口/Run Buff 面板/背包按钮）以浮层形式盖在它上面。
    ///
    /// MapExplorationView.NodeSelected 会被原样转发成本类的 NodeSelected 信号，
    /// 因此 MainFlow.OnNodeSelected 不需要任何改动。
    /// </summary>
    private Control BuildExplorationViewport()
    {
        var container = new SubViewportContainer
        {
            Stretch = true
        };

        var viewport = new SubViewport
        {
            Size = new Vector2I(MapExplorationView.ViewportWidth, MapExplorationView.ViewportHeight),
            TransparentBg = false,
            PhysicsObjectPicking = true,
            HandleInputLocally = false
        };
        container.AddChild(viewport);

        var explorationScene = GD.Load<PackedScene>(ExplorationScenePath);
        if (explorationScene?.Instantiate() is MapExplorationView explorationView)
        {
            explorationView.NodeSelected += OnExplorationNodeSelected;
            viewport.AddChild(explorationView);
        }

        return container;
    }

    private void OnExplorationNodeSelected(string nodeId)
    {
        if (_nodeSelectionForwardQueued)
        {
            return;
        }

        _nodeSelectionForwardQueued = true;
        CallDeferred(nameof(EmitNodeSelectedDeferred), nodeId);
    }

    private void EmitNodeSelectedDeferred(string nodeId)
    {
        EmitSignal(SignalName.NodeSelected, nodeId);
    }

    /// <summary>
    /// 生成一个节点的"标记+类型"文本（例如 "☠\n第3关Boss"）。
    ///
    /// 原来给按钮版地图用；现在同时也给 <see cref="MapExplorationView"/> 里的
    /// MapNodeVisual 显示常驻标签用，保证从按钮切换到 2D 探索后不丢失这部分信息。
    /// </summary>
    public static string GetNodeText(MapNode node)
    {
        // 魏·双线征伐 的额外Boss节点：同为 MapNodeType.Boss，但需要区别于主Boss的展示
        // （独立标签 + 真实电量消耗，而不是普通Boss固定显示"免费"），单独判定，
        // 不影响任何既有 Boss 节点（主Boss节点 Id 都是 "boss_1"/"boss_2"/"boss_3" 等，不带这个前缀）。
        // 电量角标统一走 GameManager.GetNodeEnergyCost——和实际扣费、可负担性检查
        // 完全同源，不允许在UI这里另算一遍数字（避免"显示15、实际扣10"之类的分裂）。
        var cost = GameManager.GetNodeEnergyCost(node);

        if (node.Id.StartsWith("boss_extra_ch", StringComparison.Ordinal))
        {
            var extraMarker = GameManager.IsNodeCleared(node.Id) ? "✔" : "☠";
            var extraPowerText = "\n" + string.Format(Localization.Get("map.node.power.cost_fmt"), cost);
            return $"{extraMarker}\n{Localization.Get("factionfate.extra_boss_label")}{extraPowerText}";
        }

        var marker = node.Type switch
        {
            MapNodeType.Boss => GameManager.IsNodeCleared(node.Id) ? "✔" : "☠",
            MapNodeType.Elite => GameManager.IsNodeCleared(node.Id) ? "✔" : "◆",
            MapNodeType.Event => GameManager.IsNodeCleared(node.Id) ? "✔" : "？",
            MapNodeType.Shop => GameManager.IsNodeCleared(node.Id) ? "✔" : "🛒",
            _ => GameManager.IsNodeCleared(node.Id) ? "✔" : "○"
        };
        var typeText = node.Type switch
        {
            MapNodeType.Battle => string.Format(Localization.Get("map.node.battle.fmt"), node.StageIndex + 1),
            MapNodeType.Event => string.Format(Localization.Get("map.node.event.fmt"), node.StageIndex + 1),
            MapNodeType.Elite => string.Format(Localization.Get("map.node.elite.fmt"), node.StageIndex + 1),
            MapNodeType.Shop => Localization.Get("map.node.shop"),
            MapNodeType.Boss => string.Format(Localization.Get("map.node.boss.fmt"), node.StageIndex + 1),
            MapNodeType.Treasure => Localization.Get("map.node.treasure"),
            _ => node.Name
        };
        // 电量角标直接读取统一接口的计算结果；事件品质不同也能显示各自费用，
        // Boss固定显示0，只有初始抉择等明确的零费用节点显示免费。
        var powerText = node.Type switch
        {
            MapNodeType.Boss => "\n" + string.Format(Localization.Get("map.node.power.zero_fmt"), 0),
            _ when cost > 0 => "\n" + string.Format(Localization.Get("map.node.power.cost_fmt"), cost),
            _ => "\n" + Localization.Get("map.node.power.free")
        };
        return $"{marker}\n{typeText}{powerText}";
    }

    private static string GetCurrentStageText()
    {
        return GameManager.CurrentStage >= 0 ? string.Format(Localization.Get("map.stage.fmt"), GameManager.CurrentStage + 1) : Localization.Get("map.stage.not_entered");
    }

    private static string BuildRunInfoText()
    {
        var text = string.Format(Localization.Get("map.status.fmt"), GameManager.Forage, GameManager.Gold, GetCurrentStageText());
        if (GameManager.CurrentChapterVariant != ChapterVariant.None)
        {
            text += "    " + string.Format(Localization.Get("map.chapter_variant.fmt"), GameManager.GetChapterVariantDisplayName(GameManager.CurrentChapterVariant));
        }

        return text;
    }

    private static Control BuildRunBuffPanel()
    {
        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_top", 14);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_bottom", 14);
        panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        margin.AddChild(root);

        var title = new Label
        {
            Text = Localization.Get("map.global_effect.title"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 24);
        root.AddChild(title);

        var entries = BuildRunBuffDisplayEntries();
        if (entries.Count == 0)
        {
            var empty = new Label
            {
                Text = Localization.Get("map.run_buff.empty"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            empty.AddThemeFontSizeOverride("font_size", 20);
            empty.AddThemeColorOverride("font_color", new Color(0.72f, 0.74f, 0.78f));
            root.AddChild(empty);
            return panel;
        }

        // 动态高度：内容撑开，超出上限时出现滚动条（约25%地图区域高度）。
        const float PerItemPx = 30f;
        const float MaxScrollPx = 160f;
        var estimatedPx = entries.Count * PerItemPx;
        var scrollMinH = System.Math.Min(estimatedPx, MaxScrollPx);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            CustomMinimumSize = new Vector2(250, scrollMinH),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        root.AddChild(scroll);

        var buffList = new VBoxContainer();
        buffList.AddThemeConstantOverride("separation", 4);
        buffList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(buffList);

        foreach (var buff in entries)
        {
            var tt = buff.TooltipText;
            var label = new Label
            {
                Text = buff.DisplayText,
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = MouseFilterEnum.Stop
            };
            label.MouseEntered += () => TooltipManager.Show(tt, label);
            label.MouseExited += () => TooltipManager.Hide();
            label.AddThemeFontSizeOverride("font_size", 20);
            buffList.AddChild(label);
        }

        return panel;
    }

    private static List<RunBuffDisplayEntry> BuildRunBuffDisplayEntries()
    {
        var result = new List<RunBuffDisplayEntry>();
        var merged = new Dictionary<string, RunBuffDisplayEntry>();

        if (GameManager.InitialEventUpgradeFirstEquipIsSelected)
        {
            var statusKey = GameManager.InitialEventUpgradeFirstEquipHasTriggered
                ? "initial_event.quality_upgrade.status.triggered"
                : GameManager.InitialEventUpgradeFirstEquipIsArmed
                    ? "initial_event.quality_upgrade.status.armed"
                    : "initial_event.quality_upgrade.status.waiting_stage";
            result.Add(new RunBuffDisplayEntry(
                Localization.Get("initial_event.quality_upgrade.name"),
                Localization.Get(statusKey)));
        }

        foreach (var buff in GameManager.ActiveRunBuffs)
        {
            if (!buff.Stackable)
            {
                var remainingText = FormatRunBuffRemaining(buff.RemainingBattles);
                var buffName = Localization.GetName(buff.Definition);
                var tooltip = new StringBuilder()
                    .Append(buffName)
                    .Append("\n").Append(string.Format(Localization.Get("map.run_buff.tooltip.remaining_fmt"), remainingText))
                    .Append("\n").Append(Localization.GetDescription(buff.Definition))
                    .ToString();
                result.Add(new RunBuffDisplayEntry(buffName, tooltip));
                continue;
            }

            if (!merged.TryGetValue(buff.BuffId, out var entry))
            {
                entry = new RunBuffDisplayEntry(buff.BuffId, Localization.GetName(buff.Definition), Localization.GetDescription(buff.Definition), 0, buff.RemainingBattles);
            }

            entry.StackCount += 1;
            entry.RemainingBattles = MergeRunBuffRemaining(entry.RemainingBattles, buff.RemainingBattles);
            merged[buff.BuffId] = entry;
        }

        foreach (var pair in merged)
        {
            result.Add(CreateMergedRunBuffEntry(pair.Value));
        }

        return result;
    }

    private static RunBuffDisplayEntry CreateMergedRunBuffEntry(RunBuffDisplayEntry entry)
    {
        var displayText = entry.StackCount > 1
            ? $"{entry.BuffName} ×{entry.StackCount}"
            : entry.BuffName;

        var tooltipText = new StringBuilder();
        tooltipText.Append(entry.BuffName)
            .Append("\n").Append(string.Format(Localization.Get("map.run_buff.tooltip.stacks_fmt"), entry.StackCount));

        if (entry.RemainingBattles != 0)
        {
            tooltipText.Append("\n").Append(string.Format(Localization.Get("map.run_buff.tooltip.remaining_fmt"), FormatRunBuffRemaining(entry.RemainingBattles)));
        }

        tooltipText.Append("\n").Append(string.Format(Localization.Get("map.run_buff.tooltip.per_stack_effect_fmt"), entry.Description));

        var totalEffectText = GetRunBuffTotalEffectText(entry.BuffId, entry.StackCount);
        if (!string.IsNullOrEmpty(totalEffectText))
        {
            tooltipText.Append("\n").Append(string.Format(Localization.Get("map.run_buff.tooltip.total_effect_fmt"), totalEffectText));
        }

        return new RunBuffDisplayEntry(displayText, tooltipText.ToString());
    }

    private static int MergeRunBuffRemaining(int current, int next)
    {
        if (current < 0 || next < 0)
        {
            return -1;
        }

        return System.Math.Max(current, next);
    }

    private static string FormatRunBuffRemaining(int remainingBattles)
    {
        return remainingBattles < 0 ? Localization.Get("map.buff.permanent") : string.Format(Localization.Get("map.buff.battles_fmt"), remainingBattles);
    }

    private static string GetRunBuffTotalEffectText(string buffId, int stackCount)
    {
        return buffId switch
        {
            RunBuffIds.Curse => string.Format(Localization.Get("map.buff.curse_desc_fmt"), (stackCount * 0.1).ToString("0.##"), stackCount),
            RunBuffIds.AncestorBlessing => string.Format(Localization.Get("map.buff.ancestor_bless_fmt"), stackCount),
            _ => string.Empty
        };
    }

    private struct RunBuffDisplayEntry
    {
        /// <summary>
        /// Map System 的公开入口：RunBuffDisplayEntry。
        ///
        /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
        /// </summary>
        public RunBuffDisplayEntry(string displayText, string tooltipText)
        {
            BuffId = string.Empty;
            BuffName = string.Empty;
            Description = string.Empty;
            DisplayText = displayText;
            TooltipText = tooltipText;
            StackCount = 0;
            RemainingBattles = 0;
        }

        /// <summary>
        /// Map System 的公开入口：RunBuffDisplayEntry。
        ///
        /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
        /// </summary>
        public RunBuffDisplayEntry(string buffId, string buffName, string description, int stackCount, int remainingBattles)
        {
            BuffId = buffId;
            BuffName = buffName;
            Description = description;
            DisplayText = string.Empty;
            TooltipText = string.Empty;
            StackCount = stackCount;
            RemainingBattles = remainingBattles;
        }

        public string BuffId { get; set; }
        public string BuffName { get; set; }
        public string Description { get; set; }
        public string DisplayText { get; set; }
        public string TooltipText { get; set; }
        public int StackCount { get; set; }
        public int RemainingBattles { get; set; }
    }

    private void CreateDebugWindow()
    {
        _debugWindow = new Window
        {
            Title = Localization.Get("map.debug.title"),
            InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen,
            Size = new Vector2I(360, 400),
            Visible = false
        };
        // 标题栏 × 按钮：直接隐藏窗口，不销毁（可复用）。
        _debugWindow.CloseRequested += () => _debugWindow.Hide();
        AddChild(_debugWindow);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        _debugWindow.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 18);
        margin.AddChild(root);

        var title = new Label
        {
            Text = Localization.Get("map.debug.title"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 28);
        root.AddChild(title);

        _unlockAllCheckBox = new CheckBox
        {
            Text = Localization.Get("map.debug.unlock_all"),
            ButtonPressed = GameManager.DebugMapEnabled
        };
        _unlockAllCheckBox.AddThemeFontSizeOverride("font_size", 22);
        _unlockAllCheckBox.Toggled += OnDebugUnlockAllToggled;
        root.AddChild(_unlockAllCheckBox);

        var variantRow = new HBoxContainer();
        variantRow.AddThemeConstantOverride("separation", 12);
        root.AddChild(variantRow);

        var variantLabel = new Label { Text = Localization.Get("map.debug.variant_label") };
        variantLabel.AddThemeFontSizeOverride("font_size", 22);
        variantRow.AddChild(variantLabel);

        _variantOptionButton = new OptionButton();
        _variantOptionButton.AddThemeFontSizeOverride("font_size", 22);
        _variantOptionButton.AddItem(Localization.Get("chapter.variant.none"));
        _variantOptionButton.AddItem(Localization.Get("chapter.variant.curse_night"));
        _variantOptionButton.AddItem(Localization.Get("chapter.variant.blood_moon"));
        _variantOptionButton.AddItem(Localization.Get("chapter.variant.rainstorm"));
        _variantOptionButton.ItemSelected += OnDebugVariantSelected;
        variantRow.AddChild(_variantOptionButton);

        var routeRow = new HBoxContainer();
        routeRow.AddThemeConstantOverride("separation", 12);
        root.AddChild(routeRow);

        var routeLabel = new Label { Text = Localization.Get("map.debug.route_label") };
        routeLabel.AddThemeFontSizeOverride("font_size", 22);
        routeRow.AddChild(routeLabel);

        _routeOptionButton = new OptionButton();
        _routeOptionButton.AddThemeFontSizeOverride("font_size", 22);
        _routeOptionButton.AddItem(Localization.Get("chapter.route.default"));
        _routeOptionButton.AddItem(Localization.Get("chapter.route.sewer"));
        _routeOptionButton.AddItem(Localization.Get("chapter.route.imperial"));
        _routeOptionButton.ItemSelected += OnDebugRouteSelected;
        routeRow.AddChild(_routeOptionButton);

        var bossRow = new HBoxContainer();
        bossRow.AddThemeConstantOverride("separation", 12);
        root.AddChild(bossRow);

        var bossLabel = new Label { Text = Localization.Get("map.debug.boss_plan_label") };
        bossLabel.AddThemeFontSizeOverride("font_size", 22);
        bossRow.AddChild(bossLabel);

        _bossPlanOptionButton = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _bossPlanOptionButton.AddThemeFontSizeOverride("font_size", 22);
        _bossPlanOptionButton.ItemSelected += OnDebugBossPlanSelected;
        bossRow.AddChild(_bossPlanOptionButton);

        _advanceChapterButton = new Button
        {
            Text = Localization.Get("map.advance_chapter"),
            CustomMinimumSize = new Vector2(180, 48),
            Disabled = GameManager.IsFinalChapter
        };
        _advanceChapterButton.AddThemeFontSizeOverride("font_size", 22);
        _advanceChapterButton.Pressed += OnDebugAdvanceChapterPressed;
        root.AddChild(_advanceChapterButton);
    }

    private void ShowDebugWindow()
    {
        if (_debugWindow == null)
        {
            return;
        }

        // 再次点击【调试】按钮时自动关闭已开启的窗口（toggle 行为）。
        if (_debugWindow.Visible)
        {
            _debugWindow.Hide();
            return;
        }

        if (_unlockAllCheckBox != null)
        {
            _unlockAllCheckBox.SetPressedNoSignal(GameManager.DebugMapEnabled);
        }

        if (_variantOptionButton != null)
        {
            var idx = GameManager.CurrentChapterVariant switch
            {
                ChapterVariant.CurseNight => 1,
                ChapterVariant.BloodMoon => 2,
                ChapterVariant.Rainstorm => 3,
                _ => 0
            };
            _variantOptionButton.Select(idx);
        }

        if (_routeOptionButton != null)
        {
            var idx = GameManager.CurrentChapterRoute switch
            {
                ChapterRoute.Sewer => 1,
                ChapterRoute.Imperial => 2,
                _ => 0
            };
            _routeOptionButton.Select(idx);
        }

        RefreshBossPlanOptions();

        if (_advanceChapterButton != null)
        {
            _advanceChapterButton.Disabled = GameManager.IsFinalChapter;
            _advanceChapterButton.Text = GameManager.IsFinalChapter ? Localization.Get("map.final_chapter") : Localization.Get("map.advance_chapter");
        }

        _debugWindow.PopupCentered();
    }

    private void OnDebugUnlockAllToggled(bool enabled)
    {
        GD.Print($"[MapDebug] UnlockAllStages={enabled}");
        GameManager.SetDebugMapEnabled(enabled);
        RefreshMapView();
        if (_debugWindow != null)
        {
            _debugWindow.PopupCentered();
        }
    }

    private void OnDebugVariantSelected(long index)
    {
        var variant = index switch
        {
            1 => ChapterVariant.CurseNight,
            2 => ChapterVariant.BloodMoon,
            3 => ChapterVariant.Rainstorm,
            _ => ChapterVariant.None
        };
        GD.Print($"[MapDebug] SetChapterVariant={variant}");
        GameManager.SetDebugChapterVariant(variant);
        RefreshMapView();
        if (_debugWindow != null)
        {
            _debugWindow.PopupCentered();
        }
    }

    private void OnDebugRouteSelected(long index)
    {
        var route = index switch
        {
            1 => ChapterRoute.Sewer,
            2 => ChapterRoute.Imperial,
            _ => ChapterRoute.Default
        };

        GD.Print($"[MapDebug] SetChapterRoute={route}");
        GameManager.SetChapterRoute(route);
        RefreshMapView();
        if (_debugWindow != null)
        {
            _debugWindow.PopupCentered();
        }
    }

    private void OnDebugAdvanceChapterPressed()
    {
        if (GameManager.IsFinalChapter)
        {
            return;
        }

        GD.Print($"[MapDebug] AdvanceToNextChapter: {GameManager.CurrentChapter} -> {GameManager.CurrentChapter + 1}");
        GameManager.AdvanceToNextChapter();
        RefreshMapView();
        if (_debugWindow != null)
        {
            _debugWindow.PopupCentered();
        }
    }

    private void OnDebugBossPlanSelected(long index)
    {
        var plans = GameManager.GetAvailableBossEncounterPlans(GameManager.CurrentChapter);
        if (index < 0 || index >= plans.Count)
        {
            return;
        }

        var selectedPlan = plans[(int)index];
        GD.Print($"[MapDebug] SetBossPlan={GameManager.BuildBossEncounterDisplayName(selectedPlan)}");
        GameManager.SetDebugBossEncounterPlan(GameManager.CurrentChapter, selectedPlan);
    }

    private void RefreshBossPlanOptions()
    {
        if (_bossPlanOptionButton == null)
        {
            return;
        }

        _bossPlanOptionButton.Clear();
        var plans = GameManager.GetAvailableBossEncounterPlans(GameManager.CurrentChapter);
        if (plans.Count == 0)
        {
            _bossPlanOptionButton.AddItem(Localization.Get("map.debug.no_boss_plan"));
            _bossPlanOptionButton.Disabled = true;
            return;
        }

        _bossPlanOptionButton.Disabled = false;
        var currentPlan = GameManager.GetOrCreateChapterBossEncounterEnemyIds(GameManager.CurrentChapter);
        var selectedIndex = 0;
        for (var i = 0; i < plans.Count; i++)
        {
            var label = GameManager.BuildBossEncounterDisplayName(plans[i]);
            _bossPlanOptionButton.AddItem(label);
            if (AreSameEnemyPlan(plans[i], currentPlan))
            {
                selectedIndex = i;
            }
        }

        _bossPlanOptionButton.Select(selectedIndex);
    }

    private static bool AreSameEnemyPlan(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], System.StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
