//////////////////////////////////////////////////////////
// 文件：Scripts/PoJunWindow.cs
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
using System.Threading.Tasks;

/// <summary>
/// Core System 的公开类：PoJunWindow。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed partial class PoJunWindow : Control
{
    private Label? _damageLabel;
    private Label? _multiplierLabel;
    private Label? _manaLabel;
    private Button? _spendButton;
    private TaskCompletionSource<bool>? _tcs;

    /// <summary>
    /// Core System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        var bg = new ColorRect
        {
            Color = new Color(0f, 0f, 0f, 0.55f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(440, 300)
        };
        center.AddChild(panel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 28);
        margin.AddThemeConstantOverride("margin_top", 28);
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_bottom", 28);
        panel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 18);
        margin.AddChild(vbox);

        var title = new Label
        {
            Text = Localization.Get("reaction.pojun.window_title"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 32);
        vbox.AddChild(title);

        _damageLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _damageLabel.AddThemeFontSizeOverride("font_size", 26);
        vbox.AddChild(_damageLabel);

        _multiplierLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _multiplierLabel.AddThemeFontSizeOverride("font_size", 26);
        vbox.AddChild(_multiplierLabel);

        _manaLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _manaLabel.AddThemeFontSizeOverride("font_size", 24);
        vbox.AddChild(_manaLabel);

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 16);
        vbox.AddChild(hbox);

        _spendButton = new Button
        {
            Text = Localization.Get("reaction.pojun.spend"),
            CustomMinimumSize = new Vector2(0, 52),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _spendButton.AddThemeFontSizeOverride("font_size", 22);
        _spendButton.Pressed += OnSpendPressed;
        hbox.AddChild(_spendButton);

        var stopButton = new Button
        {
            Text = Localization.Get("reaction.pojun.stop"),
            CustomMinimumSize = new Vector2(0, 52),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        stopButton.AddThemeFontSizeOverride("font_size", 22);
        stopButton.Pressed += OnStopPressed;
        hbox.AddChild(stopButton);
    }

    /// <summary>
    /// Core System 的公开入口：RefreshDisplay。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void RefreshDisplay(int baseDamage, int clicks, double mana)
    {
        if (_damageLabel == null) return;
        _damageLabel.Text = Localization.GetFmt("reaction.pojun.damage_fmt", baseDamage * (1 + clicks));
        _multiplierLabel!.Text = Localization.GetFmt("reaction.pojun.multiplier_fmt", 1 + clicks);
        _manaLabel!.Text = Localization.GetFmt("reaction.pojun.mana_remaining_fmt", BattleRules.FormatMana(mana));
        if (_spendButton != null) _spendButton.Disabled = mana < 0.5;
    }

    /// <summary>
    /// Core System 的公开入口：WaitForDecision。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public Task<bool> WaitForDecision()
    {
        _tcs = new TaskCompletionSource<bool>();
        return _tcs.Task;
    }

    private void OnSpendPressed()
    {
        _tcs?.TrySetResult(true);
        _tcs = null;
    }

    private void OnStopPressed()
    {
        _tcs?.TrySetResult(false);
        _tcs = null;
    }
}
