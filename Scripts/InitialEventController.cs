//////////////////////////////////////////////////////////
// 文件：Scripts/InitialEventController.cs
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
using System.Collections.Generic;

/// <summary>
/// Event System 的公开类：InitialEventController。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class InitialEventController : Control
{
    private const float PanelWidth = 1680f;
    private const float PanelHeight = 860f;
    private const float OptionCardMinHeight = 560f;
    private const float OptionDescriptionMinHeight = 430f;

    [Signal]
    public delegate void InitialEventCompletedEventHandler();

    private List<InitialEventDefinition> _options = new();
    private HBoxContainer? _optionRow;

    /// <summary>
    /// Event System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        _options = InitialEventManager.Pick3();
        Callable.From(BuildLayout).CallDeferred();
    }

    private void BuildLayout()
    {
        var background = new ColorRect
        {
            Color = new Color(0.08f, 0.09f, 0.11f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(PanelWidth, PanelHeight)
        };
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 36);
        margin.AddThemeConstantOverride("margin_top", 32);
        margin.AddThemeConstantOverride("margin_right", 36);
        margin.AddThemeConstantOverride("margin_bottom", 32);
        panel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 20);
        margin.AddChild(root);

        var title = new Label
        {
            Text = Localization.Get("initial_event.title"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 52);
        root.AddChild(title);

        var subtitle = new Label
        {
            Text = Localization.Get("initial_event.subtitle"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subtitle.AddThemeFontSizeOverride("font_size", 20);
        root.AddChild(subtitle);

        _optionRow = new HBoxContainer();
        _optionRow.SizeFlagsVertical = SizeFlags.ExpandFill;
        _optionRow.AddThemeConstantOverride("separation", 16);
        root.AddChild(_optionRow);

        RebuildOptionCards();
    }

    private Control BuildOptionCard(InitialEventDefinition entry)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, OptionCardMinHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        panel.MouseEntered += () => TooltipManager.Show(entry.DisplayEffectDetail, panel, TooltipManager.BattleUnitTextWidth);
        panel.MouseExited += () => TooltipManager.Hide();

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 20);
        margin.AddThemeConstantOverride("margin_top", 20);
        margin.AddThemeConstantOverride("margin_right", 20);
        margin.AddThemeConstantOverride("margin_bottom", 20);
        panel.AddChild(margin);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 12);
        margin.AddChild(col);

        var descLabel = new RichTextLabel
        {
            Text = entry.DisplayDescription,
            BbcodeEnabled = false,
            FitContent = false,
            ScrollActive = true,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, OptionDescriptionMinHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        descLabel.AddThemeFontSizeOverride("normal_font_size", 25);
        col.AddChild(descLabel);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        col.AddChild(actions);

        var chooseBtn = new Button
        {
            Text = Localization.Get("initial_event.choose_button"),
            CustomMinimumSize = new Vector2(0, 56),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        chooseBtn.AddThemeFontSizeOverride("font_size", 26);
        var captured = entry;
        chooseBtn.Pressed += () => OnChosen(captured);
        actions.AddChild(chooseBtn);

        if (FactionFateManager.CurrentFateId == FactionFateIds.Reroll)
        {
            var rerollButton = new Button
            {
                Text = Localization.Get("factionfate.reforge_refresh"),
                CustomMinimumSize = new Vector2(116, 56),
                Disabled = FactionFateManager.RerollRemaining <= 0
            };
            rerollButton.AddThemeFontSizeOverride("font_size", 22);
            rerollButton.Pressed += () => OnRerollPressed(captured);
            actions.AddChild(rerollButton);
        }

        return panel;
    }

    private void RebuildOptionCards()
    {
        if (_optionRow == null)
        {
            return;
        }

        foreach (var child in _optionRow.GetChildren())
        {
            _optionRow.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var entry in _options)
        {
            _optionRow.AddChild(BuildOptionCard(entry));
        }
    }

    private void OnRerollPressed(InitialEventDefinition current)
    {
        if (!FactionFateManager.TryRerollInitialEventOption(current, _options, out var replacement)
            || replacement == null)
        {
            return;
        }

        var index = _options.FindIndex(entry => entry.Id == current.Id);
        if (index < 0)
        {
            return;
        }

        _options[index] = replacement;
        TooltipManager.Hide();
        RebuildOptionCards();
    }

    private void OnChosen(InitialEventDefinition entry)
    {
        entry.Apply();
        GameManager.MarkCurrentNodeCleared();
        EmitSignal(SignalName.InitialEventCompleted);
    }
}
