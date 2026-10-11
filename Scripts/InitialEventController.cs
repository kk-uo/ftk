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
    private const float OptionCardMinHeight = 560f;
    private const float OptionDescriptionMinHeight = 280f;

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
        Localization.LanguageChanged += RefreshLanguage;
    }

    public override void _ExitTree() => Localization.LanguageChanged -= RefreshLanguage;

    private void RefreshLanguage()
    {
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        BuildLayout(); // 仅重建显示，绝不重新抽取命运。
    }

    private void BuildLayout()
    {
        Theme = new Theme { DefaultFont = GD.Load<Font>("res://Assets/Fonts/NotoSerifCJKsc-Black.otf") };
        var background = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://Assets/Backgrounds/Events/initial_fate_junkyard.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            TextureFilter = TextureFilterEnum.Nearest
        };
        AddChild(background);
        background.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(new MainMenuCrtFilter { Name = "BackgroundCrtFilter" });

        var titleFrame = new FateTerminalFrame
        {
            AnchorLeft = .03f, AnchorTop = .035f, AnchorRight = .30f, AnchorBottom = .14f,
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(titleFrame);

        var title = new Label
        {
            Text = Localization.Get("initial_event.title"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 72);
        title.AddThemeColorOverride("font_color", new Color("f52b61"));
        titleFrame.AddChild(title);

        var subtitle = new Label
        {
            Text = Localization.Get("initial_event.subtitle"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            AnchorLeft = .165f, AnchorTop = .15f, AnchorRight = .835f, AnchorBottom = .22f
        };
        subtitle.AddThemeFontSizeOverride("font_size", 34);
        subtitle.AddThemeColorOverride("font_shadow_color", Colors.Black);
        subtitle.AddThemeConstantOverride("shadow_offset_x", 2);
        subtitle.AddThemeConstantOverride("shadow_offset_y", 3);
        AddChild(subtitle);

        _optionRow = new HBoxContainer
        {
            // 左右、上下锚点关于画面中心对称，整组选项保持居中。
            AnchorLeft = .165f, AnchorTop = .23f, AnchorRight = .835f, AnchorBottom = .77f
        };
        _optionRow.AddThemeConstantOverride("separation", 28);
        AddChild(_optionRow);

        RebuildOptionCards();
    }

    private Control BuildOptionCard(InitialEventDefinition entry, int index)
    {
        var accent = new Color(new[] { "f22c62", "27c7d2", "f6b43d" }[index % 3]);
        var panel = new FateTerminalFrame
        {
            Accent = accent,
            CustomMinimumSize = new Vector2(0, OptionCardMinHeight),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        panel.MouseEntered += () => TooltipManager.Show(entry.DisplayEffectDetail, panel, TooltipManager.BattleUnitTextWidth);
        panel.MouseExited += () => TooltipManager.Hide();

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 36);
        margin.AddThemeConstantOverride("margin_top", 40);
        margin.AddThemeConstantOverride("margin_right", 36);
        margin.AddThemeConstantOverride("margin_bottom", 36);
        panel.AddChild(margin);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 28);
        margin.AddChild(col);

        var code = new Label { Text = $"DATA://{index + 1:D2}" };
        code.AddThemeFontSizeOverride("font_size", 20);
        code.AddThemeColorOverride("font_color", accent);
        col.AddChild(code);
        var heading = new Label
        {
            Text = string.Format(Localization.Get("initial_event.fate_option_fmt"), index + 1),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        heading.AddThemeFontSizeOverride("font_size", 36);
        heading.AddThemeColorOverride("font_color", accent);
        col.AddChild(heading);
        var rule = new ColorRect { Color = new Color(accent,.5f), CustomMinimumSize = new Vector2(0,2), MouseFilter = MouseFilterEnum.Ignore };
        col.AddChild(rule);

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
        descLabel.AddThemeFontSizeOverride("normal_font_size", 32);
        col.AddChild(descLabel);

        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        col.AddChild(actions);

        var chooseBtn = new FateSelectButton
        {
            Accent = accent,
            CustomMinimumSize = new Vector2(0, 88),
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

        for (var index = 0; index < _options.Count; index++)
        {
            _optionRow.AddChild(BuildOptionCard(_options[index], index));
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
