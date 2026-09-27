//////////////////////////////////////////////////////////
// 文件：Scripts/ChapterCompleteController.cs
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
/// Core System 的公开类：ChapterCompleteController。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class ChapterCompleteController : Control
{
    [Signal]
    public delegate void PrimaryActionEventHandler();

    [Signal]
    public delegate void SecondaryActionEventHandler();

    public string TitleText { get; set; } = string.Empty;
    public string SubtitleText { get; set; } = string.Empty;
    public string PrimaryButtonText { get; set; } = string.Empty;
    public string? SecondaryButtonText { get; set; }

    /// <summary>
    /// Core System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        Callable.From(BuildLayout).CallDeferred();
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

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(480, 0)
        };
        ApplyPanelStyle(panel);
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 40);
        margin.AddThemeConstantOverride("margin_top", 36);
        margin.AddThemeConstantOverride("margin_right", 40);
        margin.AddThemeConstantOverride("margin_bottom", 36);
        panel.AddChild(margin);

        var root = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        root.AddThemeConstantOverride("separation", 18);
        margin.AddChild(root);

        var title = new Label
        {
            Text = TitleText,
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride("font_size", 54);
        title.AddThemeColorOverride("font_color", new Color("ffe08a"));
        root.AddChild(title);

        if (!string.IsNullOrWhiteSpace(SubtitleText))
        {
            var subtitle = new Label
            {
                Text = SubtitleText,
                HorizontalAlignment = HorizontalAlignment.Center,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            subtitle.AddThemeFontSizeOverride("font_size", 24);
            subtitle.AddThemeColorOverride("font_color", new Color(0.82f, 0.84f, 0.88f));
            root.AddChild(subtitle);
        }

        var buttonRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttonRow.AddThemeConstantOverride("separation", 16);
        root.AddChild(buttonRow);

        var primaryButton = new Button
        {
            Text = PrimaryButtonText,
            CustomMinimumSize = new Vector2(220, 56),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        primaryButton.AddThemeFontSizeOverride("font_size", 24);
        primaryButton.Pressed += () => EmitSignal(SignalName.PrimaryAction);
        buttonRow.AddChild(primaryButton);

        if (!string.IsNullOrWhiteSpace(SecondaryButtonText))
        {
            var secondaryButton = new Button
            {
                Text = SecondaryButtonText,
                CustomMinimumSize = new Vector2(220, 56),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter
            };
            secondaryButton.AddThemeFontSizeOverride("font_size", 24);
            secondaryButton.Pressed += () => EmitSignal(SignalName.SecondaryAction);
            buttonRow.AddChild(secondaryButton);
        }
    }

    private static void ApplyPanelStyle(PanelContainer panel)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.11f, 0.12f, 0.14f),
            BorderColor = new Color(0.35f, 0.38f, 0.48f),
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10
        };
        panel.AddThemeStyleboxOverride("panel", style);
    }
}
