//////////////////////////////////////////////////////////
// 文件：Scripts/TutorialSelectController.cs
//
// 模块：Tutorial System
//
// 为什么存在：
// "新手教程"入口原来直接进入战斗教程；现在拆分为"战斗教程"/"战斗外教程"
// 两个独立模块，需要一个二级选择界面：展示两个模块各自的名称/简介/完成状态，
// 并各自提供"进入"入口，外加一个"返回"按钮回到主菜单。
//
// 不负责：
// × 教程步骤推进逻辑（由 TutorialManager 负责）。
// × 教程完成状态的持久化（由 TutorialProgress 负责，本类只读取其状态展示）。
//////////////////////////////////////////////////////////

using Godot;

public partial class TutorialSelectController : Control
{
    [Signal]
    public delegate void CombatTutorialRequestedEventHandler();

    [Signal]
    public delegate void MetaTutorialRequestedEventHandler();

    [Signal]
    public delegate void IntegratedTutorialRequestedEventHandler();

    [Signal]
    public delegate void ReturnToMenuRequestedEventHandler();

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        BuildLayout();
    }

    private void BuildLayout()
    {
        var background = new ColorRect { Color = new Color(0.07f, 0.08f, 0.10f) };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 28);
        center.AddChild(vbox);

        var title = new Label
        {
            Text = Localization.Get("tutorial.select.title"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 40);
        vbox.AddChild(title);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 24);
        vbox.AddChild(row);

        row.AddChild(BuildCard(
            Localization.Get("tutorial.select.integrated_tutorial.name"),
            Localization.Get("tutorial.select.integrated_tutorial.desc"),
            TutorialIntegratedProgress.IsCompleted,
            () => EmitSignal(SignalName.IntegratedTutorialRequested)));

        row.AddChild(BuildCard(
            Localization.Get("tutorial.select.combat_tutorial.name"),
            Localization.Get("tutorial.select.combat_tutorial.desc"),
            TutorialProgress.IsCombatCompleted,
            () => EmitSignal(SignalName.CombatTutorialRequested)));

        row.AddChild(BuildCard(
            Localization.Get("tutorial.select.meta_tutorial.name"),
            Localization.Get("tutorial.select.meta_tutorial.desc"),
            TutorialProgress.IsMetaCompleted,
            () => EmitSignal(SignalName.MetaTutorialRequested)));

        var bottomRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        bottomRow.AddThemeConstantOverride("separation", 16);
        vbox.AddChild(bottomRow);

        var hintLogButton = new Button
        {
            Text = Localization.Get("tutorial.select.hint_log"),
            CustomMinimumSize = new Vector2(200, 56),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        hintLogButton.AddThemeFontSizeOverride("font_size", 22);
        hintLogButton.Pressed += ShowHintLog;
        bottomRow.AddChild(hintLogButton);

        var backButton = new Button
        {
            Text = Localization.Get("tutorial.select.back"),
            CustomMinimumSize = new Vector2(200, 56),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        backButton.AddThemeFontSizeOverride("font_size", 22);
        backButton.Pressed += () => EmitSignal(SignalName.ReturnToMenuRequested);
        bottomRow.AddChild(backButton);
    }

    /// <summary>
    /// "提示记录"：列出已经解锁（看过）的首次提示标题+文案，供玩家随时回看
    /// （对应用户§11"可以在规则手册中重新查看"——项目没有独立的规则手册
    /// 系统，复用本界面已有的卡片式布局承载这个列表，不新建整套系统）。
    /// </summary>
    private void ShowHintLog()
    {
        var overlay = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        overlay.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.11f, 0.97f)
        });
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
            Text = Localization.Get("tutorial.select.hint_log"),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        title.AddThemeFontSizeOverride("font_size", 30);
        root.AddChild(title);

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        root.AddChild(scroll);

        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(list);

        var anySeen = false;
        foreach (var hintId in FirstTimeHintManager.AllHintIds)
        {
            if (!FirstTimeHintManager.HasSeen(hintId)) continue;
            anySeen = true;

            var entry = new PanelContainer();
            entry.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = new Color(0.13f, 0.14f, 0.17f, 0.9f),
                ContentMarginLeft = 14, ContentMarginRight = 14,
                ContentMarginTop = 10, ContentMarginBottom = 10
            });
            var entryBox = new VBoxContainer();
            entryBox.AddThemeConstantOverride("separation", 4);
            entry.AddChild(entryBox);

            var entryTitle = new Label { Text = Localization.Get($"first_time_hint.{hintId}.title") };
            entryTitle.AddThemeFontSizeOverride("font_size", 22);
            entryTitle.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
            entryBox.AddChild(entryTitle);

            var entryDesc = new Label
            {
                Text = Localization.Get($"first_time_hint.{hintId}.desc"),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            entryDesc.AddThemeFontSizeOverride("font_size", 18);
            entryDesc.AddThemeColorOverride("font_color", new Color(0.82f, 0.82f, 0.86f));
            entryBox.AddChild(entryDesc);

            list.AddChild(entry);
        }

        if (!anySeen)
        {
            var emptyLabel = new Label
            {
                Text = Localization.Get("tutorial.select.hint_log_empty"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", 18);
            list.AddChild(emptyLabel);
        }

        var closeButton = new Button
        {
            Text = Localization.Get("ui.cancel"),
            CustomMinimumSize = new Vector2(160, 52),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        closeButton.AddThemeFontSizeOverride("font_size", 20);
        closeButton.Pressed += () =>
        {
            RemoveChild(overlay);
            overlay.QueueFree();
        };
        root.AddChild(closeButton);
    }

    private static PanelContainer BuildCard(string name, string description, bool completed, System.Action onEnter)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(360, 260) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.13f, 0.16f, 0.94f),
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
            ContentMarginLeft = 22, ContentMarginRight = 22,
            ContentMarginTop = 22, ContentMarginBottom = 22
        });

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        panel.AddChild(vbox);

        var nameLabel = new Label { Text = name, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        nameLabel.AddThemeFontSizeOverride("font_size", 26);
        vbox.AddChild(nameLabel);

        var descLabel = new Label
        {
            Text = description,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        descLabel.AddThemeFontSizeOverride("font_size", 19);
        descLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.82f, 0.86f));
        vbox.AddChild(descLabel);

        var statusLabel = new Label
        {
            Text = Localization.Get(completed ? "tutorial.select.completed" : "tutorial.select.not_completed")
        };
        statusLabel.AddThemeFontSizeOverride("font_size", 19);
        statusLabel.AddThemeColorOverride("font_color", completed ? new Color(0.5f, 1f, 0.6f) : new Color(0.7f, 0.72f, 0.78f));
        vbox.AddChild(statusLabel);

        var enterButton = new Button
        {
            Text = Localization.Get("tutorial.select.enter"),
            CustomMinimumSize = new Vector2(0, 52)
        };
        enterButton.AddThemeFontSizeOverride("font_size", 20);
        enterButton.Pressed += () => onEnter();
        vbox.AddChild(enterButton);

        return panel;
    }
}
