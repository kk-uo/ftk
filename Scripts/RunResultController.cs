//////////////////////////////////////////////////////////
// 文件：Scripts/RunResultController.cs
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
/// Core System 的公开类：RunResultController。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class RunResultController : Control
{
    [Signal]
    public delegate void FinishedEventHandler();

    [Export]
    public string TitleText { get; set; } = string.Empty;

    [Export]
    public string SubtitleText { get; set; } = string.Empty;

    [Export]
    public float DelaySeconds { get; set; } = 1.8f;

    /// <summary>
    /// Core System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override async void _Ready()
    {
        BuildLayout();
        await ToSignal(GetTree().CreateTimer(DelaySeconds), SceneTreeTimer.SignalName.Timeout);
        EmitSignal(SignalName.Finished);
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

        var root = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        root.AddThemeConstantOverride("separation", 16);
        center.AddChild(root);

        var title = new Label
        {
            Text = TitleText,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 58);
        root.AddChild(title);

        var subtitle = new Label
        {
            Text = SubtitleText,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        subtitle.AddThemeFontSizeOverride("font_size", 24);
        subtitle.AddThemeColorOverride("font_color", new Color(0.82f, 0.84f, 0.88f));
        root.AddChild(subtitle);
    }
}
