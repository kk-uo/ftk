//////////////////////////////////////////////////////////
// 文件：Scripts/RewardController.cs
//
// 模块：Reward System
//
// 职责：
// 1. 承载奖励动作、奖励序列与奖励执行相关代码。
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
/// Reward System 的公开类：RewardController。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class RewardController : Control
{
    [Signal]
    public delegate void RewardClaimedEventHandler();

    public string TitleText { get; set; } = string.Empty;
    public RewardData Reward { get; set; } = new();
    public bool RewardAlreadyClaimed { get; set; }

    /// <summary>
    /// Reward System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        // Defer so SetAnchorsPreset(FullRect) on the parent fires first, giving this
        // node its correct full-screen size before CenterContainer is built.
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

        // PanelContainer 收缩至内容尺寸后被 CenterContainer 居中（水平+垂直）。
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

        var vbox = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        vbox.AddThemeConstantOverride("separation", 18);
        margin.AddChild(vbox);

        var title = new Label
        {
            Text = TitleText,
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride("font_size", 54);
        title.AddThemeColorOverride("font_color", new Color("ffe08a"));
        vbox.AddChild(title);

        if (!RewardAlreadyClaimed)
        {
            var rewardTitle = new Label
            {
                Text = Localization.Get("reward.claim_header"),
                HorizontalAlignment = HorizontalAlignment.Center,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            rewardTitle.AddThemeFontSizeOverride("font_size", 28);
            vbox.AddChild(rewardTitle);
        }

        var rewardText = new Label
        {
            Text = RewardAlreadyClaimed ? Localization.Get("reward.already_claimed") : BuildRewardText(),
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        rewardText.AddThemeFontSizeOverride("font_size", 30);
        rewardText.AddThemeColorOverride("font_color", new Color(0.90f, 0.92f, 0.96f));
        vbox.AddChild(rewardText);

        var buttonRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttonRow.AddThemeConstantOverride("separation", 0);
        vbox.AddChild(buttonRow);

        var button = new Button
        {
            Text = Localization.Get("reward.claim_button"),
            CustomMinimumSize = new Vector2(200, 56),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        button.AddThemeFontSizeOverride("font_size", 24);
        button.Pressed += () => EmitSignal(SignalName.RewardClaimed);
        buttonRow.AddChild(button);
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

    private string BuildRewardText()
    {
        var lines = new List<string>();
        if (Reward.Gold > 0)
        {
            lines.Add(string.Format(Localization.Get("reward.gold_fmt"), Reward.Gold));
        }

        foreach (var skillId in Reward.SkillIds)
        {
            var skillDef = SkillDatabase.GetSkill(skillId);
            var skillName = skillDef is null ? skillId : Localization.GetName(skillDef);
            lines.Add(string.Format(Localization.Get("reward.skill_fmt"), skillName));
        }

        for (var i = 0; i < Reward.EquipmentNames.Count; i++)
        {
            lines.Add(string.Format(Localization.Get("reward.equip_fmt"), Reward.EquipmentNames[i]));
        }

        return lines.Count > 0 ? string.Join("\n", lines) : Localization.Get("reward.none");
    }
}
