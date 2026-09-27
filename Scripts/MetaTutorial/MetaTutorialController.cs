//////////////////////////////////////////////////////////
// 文件：Scripts/MetaTutorial/MetaTutorialController.cs
//
// 模块：Tutorial System（战斗外教程）
//
// 为什么存在：
// "战斗外教程"演示地图结构/电量/阵营命运/初始事件/章节结构等系统，但不应该
// 触碰任何真实 Run 状态（GameManager.Nodes/Power/CurrentChapter 等）——全部
// 演示数据都是本类的实例字段（_tutorialPower 等），场景销毁（节点从场景树
// 移除、_ExitTree 触发）即完成全部清理，不需要额外收尾逻辑，也不会污染任何
// 静态 Manager 的真实状态。
//
// 演示内容全部取自游戏里真实存在的数据（而不是教程自造的占位文本）：
// - 地图节点链：第一章真实节点顺序 initial_event → battle_1 → event_2 →
//   battle_3 → event_4 → battle_5(精英) → boss_1（见 GameManager.BuildDefaultMap）。
// - 战斗节点示例敌人：EnemyDatabase 里真实的 "scavenger"（拾荒者，第一章常见敌人）。
// - 精英之后的分支：真实对应 ContinueExploreManager 生成的"自由探索"批次
//   （战斗/事件/商店，外加恒定可进入的 boss_1），并注明魏·双线征伐命中时才会
//   额外出现的可选Boss，不再把它画成一个恒定分支。
// - 初始事件三选一：直接引用 InitialEventPool 里真实存在的三条词条文本。
// - 阵营命运：每个阵营各举一个真实存在的命运名称与效果说明（FactionFateDefinition）。
// - 章节/路线/变体卡片：真实的章节/路线/变体展示名 + 一张如实标注"尚未实装"的
//   第四章占位卡（TotalChapters=3，第四章数据存在但不可游玩，不是凭空编造）。
//
// 地图节点与高亮框改用纯 Control（Panel+Label 组成的小部件）实现，替换掉旧版
// SubViewport+Node2D+MapNodeVisual(Area2D) 的方案——旧方案里高亮框只能用
// "视口局部坐标 → 容器全局矩形"的比例换算来估算位置，天然不精确；现在每个
// 节点本身就是 Control，直接用 Control.GetGlobalRect() 取真实的全局矩形，
// 高亮框与节点严格对齐。节点的图标/颜色仍然复用真实的 MapNodeVisualDatabase
// （与正式地图上的节点外观定义完全一致），保证观感不失真。
//
// 职责：
// 1. 构建一套完全独立于 TutorialOverlay 的教程 chrome（遮罩/高亮框/文字面板/
//    上一步-下一步-跳过按钮 + 跳过二次确认弹窗），订阅 TutorialManager.StepChanged
//    /TutorialCompleted 驱动显示。
// 2. 构建"真实第一章节点链演示""精英后自由探索演示""电量演示""阵营命运预览"
//    "初始事件预览""章节/路线/变体卡片"六个演示区域——数据取自真实游戏内容，
//    但全部渲染在教程自己新建的 Control 树上，不读取/不写入任何真实 GameManager
//    状态字段。
//
// 不负责：
// × 真实地图生成/解锁判断（不读 GameManager.Nodes/IsNodeUnlocked）。
// × 真实电量增减（_tutorialPower 是本类独立字段）。
// × 真实阵营命运/初始事件逻辑（FactionFateManager/EventController 的状态变更方法
//   完全不被调用）。
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Collections.Generic;

public partial class MetaTutorialController : Control
{
    /// <summary>教程结束（完成或跳过）后，通知调用方（MainFlow）返回教程二级选择界面。</summary>
    public event Action? ReturnToSelectRequested;

    private int _tutorialPower = GameManager.InitialMaxPower;
    private Label? _powerWidgetLabel;
    private Tween? _powerWidgetTween;

    private Control? _mapSection;
    private Label? _chapterNameLabel;
    private HBoxContainer? _mainChainRow;
    private HBoxContainer? _branchRow;
    private readonly List<Button> _branchButtons = new();
    private readonly Dictionary<string, Control> _nodeWidgets = new();

    private Control? _powerSection;
    private Button? _energyCostButton;
    private HBoxContainer? _insufficientRow;
    private Button? _insufficientButton;
    private Button? _freeButton;

    private Control? _factionSection;
    private Label? _factionInfoLabel;

    private Control? _eventSection;

    private Control? _chapterCardsSection;

    private ColorRect? _dimOverlay;
    private Panel? _highlightBox;
    private PanelContainer? _textPanel;
    private Label? _stepIndexLabel;
    private Label? _titleLabel;
    private RichTextLabel? _descLabel;
    private VBoxContainer? _objRow;
    private Label? _objLabel;
    private VBoxContainer? _tipsRow;
    private Label? _tipsLabel;
    private Button? _nextButton;
    private Button? _prevButton;
    private Button? _skipButton;

    private Control? _skipConfirmPopup;

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        BuildLayout();

        TutorialManager.StepChanged += OnStepChanged;
        TutorialManager.TutorialCompleted += OnTutorialCompleted;

        if (TutorialManager.IsActive && TutorialManager.CurrentStep != null)
            OnStepChanged(TutorialManager.CurrentStep);
    }

    public override void _ExitTree()
    {
        TutorialManager.StepChanged -= OnStepChanged;
        TutorialManager.TutorialCompleted -= OnTutorialCompleted;
    }

    // ─── Layout ─────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        var background = new ColorRect { Color = new Color(0.08f, 0.09f, 0.11f), MouseFilter = MouseFilterEnum.Ignore };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var content = new Control { MouseFilter = MouseFilterEnum.Ignore };
        content.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(content);

        BuildMapSection(content);
        BuildPowerSection(content);
        BuildFactionSection(content);
        BuildEventSection(content);
        BuildChapterCardsSection(content);

        BuildChrome();
        BuildSkipConfirmPopup();
    }

    // ─── 真实第一章节点链 + 精英后自由探索演示 ──────────────────────────

    private void BuildMapSection(Control parent)
    {
        var section = new Control { MouseFilter = MouseFilterEnum.Ignore };
        section.SetAnchorsPreset(LayoutPreset.TopWide);
        section.OffsetTop = 140;
        section.OffsetBottom = 620;
        parent.AddChild(section);
        _mapSection = section;

        _chapterNameLabel = new Label
        {
            Text = string.Format(Localization.Get("tutorial.meta.map.chapter_name_fmt"), GameManager.GetChapterDisplayName(1)),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _chapterNameLabel.AddThemeFontSizeOverride("font_size", 30);
        _chapterNameLabel.SetAnchorsPreset(LayoutPreset.TopWide);
        _chapterNameLabel.OffsetTop = 0;
        _chapterNameLabel.OffsetBottom = 50;
        section.AddChild(_chapterNameLabel);

        // 主链：第一章真实节点顺序（GameManager.BuildDefaultMap）——
        // initial_event → battle_1 → event_2 → battle_3 → event_4 → battle_5(精英)。
        // battle_1 额外标注一个真实存在的示例敌人（EnemyDatabase 的 scavenger/拾荒者）。
        var scavengerName = EnemyDatabase.GetEnemy("scavenger")?.Name ?? string.Empty;
        var mainSpecs = new (string NodeId, MapNodeType Type, string LabelText)[]
        {
            ("node_initial_event", MapNodeType.Event, Localization.Get("tutorial.meta.node.initial_event")),
            ("node_battle_1", MapNodeType.Battle, string.IsNullOrEmpty(scavengerName)
                ? Localization.Get("tutorial.meta.node.battle_1")
                : string.Format(Localization.Get("tutorial.meta.node.battle_1_example_fmt"), Localization.Get("tutorial.meta.node.battle_1"), scavengerName)),
            ("node_event_2", MapNodeType.Event, Localization.Get("tutorial.meta.node.event_2")),
            ("node_battle_3", MapNodeType.Battle, Localization.Get("tutorial.meta.node.battle_3")),
            ("node_event_4", MapNodeType.Event, Localization.Get("tutorial.meta.node.event_4")),
            ("node_elite", MapNodeType.Elite, Localization.Get("tutorial.meta.node.elite"))
        };

        var chainWrap = new Control { MouseFilter = MouseFilterEnum.Ignore };
        chainWrap.SetAnchorsPreset(LayoutPreset.TopWide);
        chainWrap.OffsetTop = 60;
        chainWrap.OffsetBottom = 260;
        section.AddChild(chainWrap);

        var mainRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        mainRow.AddThemeConstantOverride("separation", 4);
        mainRow.SetAnchorsPreset(LayoutPreset.FullRect);
        chainWrap.AddChild(mainRow);
        _mainChainRow = mainRow;

        for (var i = 0; i < mainSpecs.Length; i++)
        {
            var (nodeId, type, labelText) = mainSpecs[i];
            var widget = BuildNodeWidget(type, labelText);
            mainRow.AddChild(widget);
            _nodeWidgets[nodeId] = widget;

            if (i < mainSpecs.Length - 1)
            {
                var arrow = new Label { Text = "→", VerticalAlignment = VerticalAlignment.Center };
                arrow.AddThemeFontSizeOverride("font_size", 28);
                mainRow.AddChild(arrow);
            }
        }

        // 精英节点之后的真实分支：与 ContinueExploreManager 实际生成的自由探索批次
        // 一致（战斗/事件/商店，恒定可进入的 boss_1），不再画一个恒定的"额外Boss"分支
        // ——那只在魏·双线征伐命中时才会出现，本演示用文案单独注明，不当成常规分支画出来。
        // 注意：这里故意不用 Visible=false 来隐藏这个容器——Godot 对隐藏的 Control
        // 不会持续重新计算基于锚点的尺寸，实测会导致它在重新显示时宽度仍停留在0
        // （这正是之前"高亮框显示错误"的根因之一）。改用 Modulate 透明度 + MouseFilter
        // 来控制可见/可交互，容器本身始终保持 Visible=true、锚点尺寸始终有效，
        // 这样 GetGlobalRect() 才能在需要高亮它时随时返回正确的矩形。
        var branchWrap = new Control { MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1f, 1f, 1f, 0f) };
        branchWrap.SetAnchorsPreset(LayoutPreset.TopWide);
        branchWrap.OffsetTop = 280;
        branchWrap.OffsetBottom = 470;
        section.AddChild(branchWrap);

        var branchRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        branchRow.AddThemeConstantOverride("separation", 24);
        branchRow.SetAnchorsPreset(LayoutPreset.FullRect);
        branchWrap.AddChild(branchRow);
        _branchRow = branchRow;
        // 高亮目标注册 branchRow（HBoxContainer，按子节点内容自然求宽）而不是外层
        // 纯定位用的 branchWrap（空 Control，没有自身内容，一旦锚点链条宽度异常
        // 就会退化成 0 宽）——这与 node_chain 高亮用 _mainChainRow 而不是 chainWrap
        // 是同一个道理，保证高亮框始终能取到与实际渲染内容一致的矩形。
        _nodeWidgets["post_elite_branches"] = branchRow;

        var branchSpecs = new (MapNodeType Type, string LabelKey)[]
        {
            (MapNodeType.Battle, "tutorial.meta.node.continue_battle"),
            (MapNodeType.Event,  "tutorial.meta.node.continue_event"),
            (MapNodeType.Shop,   "tutorial.meta.node.continue_shop")
        };

        foreach (var (type, labelKey) in branchSpecs)
        {
            var button = BuildClickableNodeWidget(type, Localization.Get(labelKey));
            button.Pressed += OnBranchNodeClicked;
            branchRow.AddChild(button);
            _branchButtons.Add(button);
        }

        var bossButton = BuildClickableNodeWidget(MapNodeType.Boss, Localization.Get("tutorial.meta.node.boss_1"));
        bossButton.Pressed += OnBranchNodeClicked;
        branchRow.AddChild(bossButton);
        _branchButtons.Add(bossButton);

        // 【修复】"魏·双线征伐"这个具体命运名字之前会在阵营命运正式介绍（第7步
        // FactionDestiny）之前，就在这一步（第4步 AfterElite）先被提到——玩家
        // 会先看到一个还没学过的专有名词。现在把这条说明整体挪到 FactionDestiny
        // 步骤的 Tips 里（见 tutorial.meta.step.meta_faction_destiny.tips），
        // 这里不再单独渲染这个提示标签。
    }

    private Control? _branchVisible_weiNote;

    /// <summary>
    /// 构建一个纯展示用的地图节点小部件：圆形色块（颜色/符号取自真实
    /// MapNodeVisualDatabase.Resolve 的 PlaceholderColor/PlaceholderGlyph，
    /// 与正式地图节点外观定义完全一致）+ 名称文本，全部是 Control，
    /// 因此 GetGlobalRect() 得到的高亮矩形是精确值，不需要任何坐标换算。
    /// </summary>
    private static Control BuildNodeWidgetContent(MapNodeType type, string labelText)
    {
        var fakeNode = new MapNode { Id = Guid.NewGuid().ToString("N"), Type = type };
        var def = MapNodeVisualDatabase.Resolve(fakeNode);

        var vbox = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(118, 100),
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        vbox.AddThemeConstantOverride("separation", 6);

        var circleWrap = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        vbox.AddChild(circleWrap);

        var circle = new PanelContainer { CustomMinimumSize = new Vector2(60, 60), MouseFilter = MouseFilterEnum.Ignore };
        circle.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = def.PlaceholderColor,
            CornerRadiusTopLeft = 30, CornerRadiusTopRight = 30,
            CornerRadiusBottomLeft = 30, CornerRadiusBottomRight = 30
        });
        circleWrap.AddChild(circle);

        var glyph = new Label
        {
            Text = def.PlaceholderGlyph,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        glyph.AddThemeFontSizeOverride("font_size", 24);
        glyph.SetAnchorsPreset(LayoutPreset.FullRect);
        circle.AddChild(glyph);

        var nameLabel = new Label
        {
            Text = labelText,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(nameLabel);

        return vbox;
    }

    private static Control BuildNodeWidget(MapNodeType type, string labelText)
    {
        return BuildNodeWidgetContent(type, labelText);
    }

    private static Button BuildClickableNodeWidget(MapNodeType type, string labelText)
    {
        var button = new Button
        {
            Flat = true,
            CustomMinimumSize = new Vector2(118, 100)
        };
        var content = BuildNodeWidgetContent(type, labelText);
        content.SetAnchorsPreset(LayoutPreset.FullRect);
        content.MouseFilter = MouseFilterEnum.Ignore;
        button.AddChild(content);
        return button;
    }

    private void OnBranchNodeClicked()
    {
        if (TutorialManager.CurrentStep?.Id == MetaTutorialDatabase.StepIds.AfterElite)
        {
            TutorialManager.AdvanceToNext();
        }
    }

    // ─── 电量演示 ───────────────────────────────────────────────────────

    private void BuildPowerSection(Control parent)
    {
        var center = new CenterContainer { Visible = false };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        parent.AddChild(center);
        _powerSection = center;

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 20);
        center.AddChild(vbox);

        _powerWidgetLabel = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _powerWidgetLabel.AddThemeFontSizeOverride("font_size", 40);
        vbox.AddChild(_powerWidgetLabel);

        _energyCostButton = new Button
        {
            Text = string.Format(Localization.Get("tutorial.meta.power.cost_button_fmt"), ContinueExploreConfig.CombatEnergyCost),
            CustomMinimumSize = new Vector2(320, 60)
        };
        _energyCostButton.Pressed += OnEnergyCostButtonPressed;
        vbox.AddChild(_energyCostButton);

        _insufficientRow = new HBoxContainer { Visible = false };
        _insufficientRow.AddThemeConstantOverride("separation", 16);
        vbox.AddChild(_insufficientRow);

        _insufficientButton = new Button
        {
            Text = string.Format(Localization.Get("tutorial.meta.power.insufficient_button_fmt"), ContinueExploreConfig.CombatEnergyCost),
            Disabled = true,
            CustomMinimumSize = new Vector2(320, 60)
        };
        _insufficientRow.AddChild(_insufficientButton);

        _freeButton = new Button
        {
            Text = Localization.Get("tutorial.meta.power.free_button"),
            CustomMinimumSize = new Vector2(220, 60)
        };
        _freeButton.Pressed += () => TutorialManager.AdvanceToNext();
        _insufficientRow.AddChild(_freeButton);

        RefreshPowerLabel();
    }

    private void OnEnergyCostButtonPressed()
    {
        _tutorialPower -= ContinueExploreConfig.CombatEnergyCost;
        RefreshPowerLabel();
        PlayPowerPulse(increase: false);
        if (_energyCostButton != null) _energyCostButton.Disabled = true;

        var timer = GetTree().CreateTimer(0.5);
        timer.Timeout += () =>
        {
            if (TutorialManager.CurrentStep?.Id == MetaTutorialDatabase.StepIds.Energy)
                TutorialManager.AdvanceToNext();
        };
    }

    private void RefreshPowerLabel()
    {
        if (_powerWidgetLabel != null)
            _powerWidgetLabel.Text = string.Format(Localization.Get("tutorial.meta.power.label_fmt"), _tutorialPower);
    }

    /// <summary>
    /// 电量脉冲动画：照搬 MapController.PlayPowerPulse 的 Tween 手法（缩放冲击 + 颜色闪烁），
    /// 但只操作本类自己的 _powerWidgetLabel/_powerWidgetTween，不调用 MapController 本身。
    /// </summary>
    private void PlayPowerPulse(bool increase)
    {
        if (_powerWidgetLabel == null) return;

        _powerWidgetTween?.Kill();
        _powerWidgetTween = CreateTween();
        _powerWidgetLabel.Modulate = increase ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.9f, 0.3f);
        var targetScale = increase ? new Vector2(1.25f, 1.25f) : new Vector2(0.85f, 0.85f);
        _powerWidgetTween.TweenProperty(_powerWidgetLabel, "scale", targetScale, 0.12)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        _powerWidgetTween.Chain().TweenProperty(_powerWidgetLabel, "scale", Vector2.One, 0.15);
        _powerWidgetTween.Parallel().TweenProperty(_powerWidgetLabel, "modulate", Colors.White, 0.3).SetDelay(0.15);
    }

    // ─── 阵营命运预览（每个阵营各举一个真实存在的命运） ─────────────────

    private void BuildFactionSection(Control parent)
    {
        var center = new CenterContainer { Visible = false };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        parent.AddChild(center);
        _factionSection = center;

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        center.AddChild(vbox);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 20);
        vbox.AddChild(row);

        // 每个阵营在真实游戏里都有多个可能命中的命运，这里各举一个真实存在的例子
        // （FactionFateDefinition.cs），而不是自造的"资源/锦囊/装备/随机"泛泛标签。
        var factions = new[]
        {
            ("tutorial.meta.faction.wei.name", "tutorial.meta.faction.wei.example_name", "tutorial.meta.faction.wei.example_desc"),
            ("tutorial.meta.faction.shu.name", "tutorial.meta.faction.shu.example_name", "tutorial.meta.faction.shu.example_desc"),
            ("tutorial.meta.faction.wu.name", "tutorial.meta.faction.wu.example_name", "tutorial.meta.faction.wu.example_desc"),
            ("tutorial.meta.faction.qun.name", "tutorial.meta.faction.qun.example_name", "tutorial.meta.faction.qun.example_desc")
        };

        foreach (var (nameKey, exampleNameKey, exampleDescKey) in factions)
        {
            var button = new Button
            {
                Text = $"{Localization.Get(nameKey)}\n【{Localization.Get(exampleNameKey)}】",
                CustomMinimumSize = new Vector2(220, 130),
                TooltipText = Localization.Get(exampleDescKey)
            };
            UIResourceDatabase.ApplyEventOptionButton(button);
            var desc = Localization.Get(exampleDescKey);
            button.Pressed += () => OnFactionCardClicked(desc);
            row.AddChild(button);
        }

        _factionInfoLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _factionInfoLabel.AddThemeFontSizeOverride("font_size", 18);
        _factionInfoLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        vbox.AddChild(_factionInfoLabel);
    }

    private void OnFactionCardClicked(string exampleDesc)
    {
        if (_factionInfoLabel != null)
            _factionInfoLabel.Text = exampleDesc;

        var timer = GetTree().CreateTimer(0.6);
        timer.Timeout += () =>
        {
            if (TutorialManager.CurrentStep?.Id == MetaTutorialDatabase.StepIds.FactionDestiny)
                TutorialManager.AdvanceToNext();
        };
    }

    // ─── 初始事件预览（真实存在的三条初始抉择词条） ─────────────────────

    private void BuildEventSection(Control parent)
    {
        var center = new CenterContainer { Visible = false };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        parent.AddChild(center);
        _eventSection = center;

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);
        center.AddChild(vbox);

        // 直接引用 InitialEventPool 里真实存在的三条词条（ie_01/ie_04/ie_07），
        // 而不是教程自造的"获得装备/金币但失去生命/特殊效果但担风险"泛泛描述。
        // 真实抽取规则是从更大的题库中随机三选一组合，这里展示其中一种真实组合。
        foreach (var key in new[] { "tutorial.meta.event.real_option1", "tutorial.meta.event.real_option2", "tutorial.meta.event.real_option3" })
        {
            var button = new Button
            {
                Text = Localization.Get(key),
                CustomMinimumSize = new Vector2(560, 70),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            UIResourceDatabase.ApplyEventOptionButton(button);
            button.Pressed += () => TutorialManager.AdvanceToNext();
            vbox.AddChild(button);
        }
    }

    // ─── 章节/路线/变体卡片（真实展示名 + 如实标注的未实装内容） ─────────

    private void BuildChapterCardsSection(Control parent)
    {
        var center = new CenterContainer { Visible = false };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        parent.AddChild(center);
        _chapterCardsSection = center;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 20);
        center.AddChild(row);

        row.AddChild(BuildChapterCard(GameManager.GetChapterDisplayName(1), Localization.Get("tutorial.meta.chapter_card.implemented"), true));
        row.AddChild(BuildChapterCard(GameManager.GetChapterRouteDisplayName(ChapterRoute.Sewer), Localization.Get("tutorial.meta.chapter_card.implemented"), true));
        row.AddChild(BuildChapterCard(GameManager.GetChapterVariantDisplayName(ChapterVariant.CurseNight), Localization.Get("tutorial.meta.chapter_card.implemented"), true));
        // 第四章流程已接入（GameManager.TotalChapters=4），但敌人/事件/Boss仍是占位内容，
        // 如实标注为"已实装（占位内容）"，而不是笼统标成"已实装"或维持旧的"尚未实装"。
        row.AddChild(BuildChapterCard(GameManager.GetChapterDisplayName(4), Localization.Get("tutorial.meta.chapter_card.placeholder"), true));
    }

    private static PanelContainer BuildChapterCard(string title, string statusText, bool implemented)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(220, 150) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = implemented ? new Color(0.12f, 0.30f, 0.16f) : new Color(0.20f, 0.20f, 0.20f),
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
            ContentMarginLeft = 16, ContentMarginRight = 16,
            ContentMarginTop = 16, ContentMarginBottom = 16
        });

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);
        panel.AddChild(vbox);

        var titleLabel = new Label { Text = title, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        titleLabel.AddThemeFontSizeOverride("font_size", 20);
        vbox.AddChild(titleLabel);

        var statusLabel = new Label { Text = statusText };
        statusLabel.AddThemeColorOverride("font_color", implemented ? new Color(0.5f, 1f, 0.6f) : new Color(0.65f, 0.65f, 0.65f));
        vbox.AddChild(statusLabel);

        return panel;
    }

    // ─── 教程 Chrome：遮罩/高亮/文字面板/按钮 ──────────────────────────

    private void BuildChrome()
    {
        _dimOverlay = new ColorRect { Color = new Color(0f, 0f, 0f, 0.35f), MouseFilter = MouseFilterEnum.Ignore };
        _dimOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_dimOverlay);

        _highlightBox = new Panel { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        _highlightBox.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(1f, 0.87f, 0.2f, 0.12f),
            BorderColor = new Color(1f, 0.87f, 0.2f, 0.9f),
            BorderWidthTop = 3, BorderWidthBottom = 3, BorderWidthLeft = 3, BorderWidthRight = 3
        });
        AddChild(_highlightBox);

        _textPanel = new PanelContainer();
        _textPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.09f, 0.10f, 0.92f),
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12
        });
        _textPanel.SetAnchorsPreset(LayoutPreset.BottomWide);
        _textPanel.OffsetTop = -260;
        AddChild(_textPanel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 32);
        margin.AddThemeConstantOverride("margin_right", 32);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        _textPanel.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vbox);

        _stepIndexLabel = new Label();
        _stepIndexLabel.AddThemeFontSizeOverride("font_size", 17);
        _stepIndexLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f));
        vbox.AddChild(_stepIndexLabel);

        _titleLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _titleLabel.AddThemeFontSizeOverride("font_size", 27);
        _titleLabel.AddThemeColorOverride("font_color", Colors.White);
        vbox.AddChild(_titleLabel);

        _descLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 50)
        };
        _descLabel.AddThemeFontSizeOverride("normal_font_size", 21);
        vbox.AddChild(_descLabel);

        _objRow = new VBoxContainer();
        vbox.AddChild(_objRow);
        var objHeader = new Label { Text = Localization.Get("tutorial.objective") };
        objHeader.AddThemeFontSizeOverride("font_size", 16);
        objHeader.AddThemeColorOverride("font_color", new Color(0.3f, 1f, 0.4f));
        _objRow.AddChild(objHeader);
        _objLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _objLabel.AddThemeFontSizeOverride("font_size", 20);
        _objRow.AddChild(_objLabel);

        _tipsRow = new VBoxContainer();
        vbox.AddChild(_tipsRow);
        var tipsHeader = new Label { Text = Localization.Get("tutorial.tips") };
        tipsHeader.AddThemeFontSizeOverride("font_size", 16);
        tipsHeader.AddThemeColorOverride("font_color", new Color(0.6f, 0.82f, 1f));
        _tipsRow.AddChild(tipsHeader);
        _tipsLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _tipsLabel.AddThemeFontSizeOverride("font_size", 19);
        _tipsRow.AddChild(_tipsLabel);

        var buttonRow = new HBoxContainer();
        buttonRow.AddThemeConstantOverride("separation", 12);
        vbox.AddChild(buttonRow);

        _prevButton = new Button { Text = "◀", CustomMinimumSize = new Vector2(60, 48) };
        _prevButton.Pressed += () => TutorialManager.PreviousStep();
        buttonRow.AddChild(_prevButton);

        _skipButton = new Button { Text = Localization.Get("tutorial.meta.btn.skip"), CustomMinimumSize = new Vector2(150, 48) };
        _skipButton.Pressed += () =>
        {
            if (_skipConfirmPopup != null) _skipConfirmPopup.Visible = true;
        };
        buttonRow.AddChild(_skipButton);

        _nextButton = new Button
        {
            CustomMinimumSize = new Vector2(180, 48),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _nextButton.Pressed += () => TutorialManager.AdvanceToNext();
        buttonRow.AddChild(_nextButton);
    }

    private void BuildSkipConfirmPopup()
    {
        _skipConfirmPopup = new Control { Visible = false, MouseFilter = MouseFilterEnum.Stop };
        _skipConfirmPopup.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_skipConfirmPopup);

        var backdrop = new ColorRect { Color = new Color(0f, 0f, 0f, 0.6f), MouseFilter = MouseFilterEnum.Stop };
        backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
        _skipConfirmPopup.AddChild(backdrop);

        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        _skipConfirmPopup.AddChild(center);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(420, 0) };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.10f, 0.13f, 0.98f),
            BorderColor = new Color(1f, 0.85f, 0.3f),
            BorderWidthTop = 2, BorderWidthBottom = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
            CornerRadiusTopLeft = 14, CornerRadiusTopRight = 14,
            CornerRadiusBottomLeft = 14, CornerRadiusBottomRight = 14,
            ContentMarginLeft = 24, ContentMarginRight = 24,
            ContentMarginTop = 22, ContentMarginBottom = 22
        });
        center.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 16);
        panel.AddChild(vbox);

        var title = new Label
        {
            Text = Localization.Get("tutorial.meta.skip_confirm.title"),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        title.AddThemeFontSizeOverride("font_size", 22);
        vbox.AddChild(title);

        var continueButton = new Button { Text = Localization.Get("tutorial.meta.skip_confirm.continue"), CustomMinimumSize = new Vector2(0, 48) };
        continueButton.Pressed += () =>
        {
            if (_skipConfirmPopup != null) _skipConfirmPopup.Visible = false;
        };
        vbox.AddChild(continueButton);

        var confirmButton = new Button { Text = Localization.Get("tutorial.meta.skip_confirm.confirm"), CustomMinimumSize = new Vector2(0, 48) };
        confirmButton.Pressed += () =>
        {
            if (_skipConfirmPopup != null) _skipConfirmPopup.Visible = false;
            TutorialManager.SkipTutorial();
            ReturnToSelectRequested?.Invoke();
        };
        vbox.AddChild(confirmButton);
    }

    // ─── 步骤变化驱动 ───────────────────────────────────────────────────

    private void OnStepChanged(TutorialStep? step)
    {
        if (step == null) return;

        Visible = true;
        UpdateSectionVisibility(step.Id);
        UpdateTextPanel(step);
        CallDeferred(nameof(ApplyHighlightDeferred), step.Highlight?.TargetId ?? string.Empty);
    }

    private void OnTutorialCompleted()
    {
        ReturnToSelectRequested?.Invoke();
    }

    private void UpdateSectionVisibility(string stepId)
    {
        var showMap = stepId is MetaTutorialDatabase.StepIds.Intro
            or MetaTutorialDatabase.StepIds.MapStructure
            or MetaTutorialDatabase.StepIds.NodeTypes
            or MetaTutorialDatabase.StepIds.AfterElite
            or MetaTutorialDatabase.StepIds.RunGoal
            or MetaTutorialDatabase.StepIds.Complete;
        if (_mapSection != null) _mapSection.Visible = showMap;

        var showBranches = stepId == MetaTutorialDatabase.StepIds.AfterElite;
        if (_branchRow?.GetParent() is Control branchWrap)
        {
            // 保持 Visible=true（锚点尺寸持续有效，GetGlobalRect() 才会一直准确），
            // 用透明度 + MouseFilter 控制实际的可见/可交互。
            branchWrap.Modulate = showBranches ? Colors.White : new Color(1f, 1f, 1f, 0f);
            branchWrap.MouseFilter = showBranches ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        }
        foreach (var button in _branchButtons)
        {
            button.MouseFilter = showBranches ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
            button.Disabled = !showBranches;
        }
        if (_branchVisible_weiNote != null) _branchVisible_weiNote.Visible = showBranches;

        var showPower = stepId is MetaTutorialDatabase.StepIds.Energy or MetaTutorialDatabase.StepIds.EnergyInsufficient;
        if (_powerSection != null) _powerSection.Visible = showPower;
        if (showPower) ConfigurePowerStep(stepId);

        var showFaction = stepId == MetaTutorialDatabase.StepIds.FactionDestiny;
        if (_factionSection != null) _factionSection.Visible = showFaction;
        if (showFaction && _factionInfoLabel != null) _factionInfoLabel.Text = string.Empty;

        var showEvent = stepId == MetaTutorialDatabase.StepIds.InitialEvent;
        if (_eventSection != null) _eventSection.Visible = showEvent;

        var showChapterCards = stepId == MetaTutorialDatabase.StepIds.VariantChapter;
        if (_chapterCardsSection != null) _chapterCardsSection.Visible = showChapterCards;
    }

    private void ConfigurePowerStep(string stepId)
    {
        if (stepId == MetaTutorialDatabase.StepIds.Energy)
        {
            _tutorialPower = GameManager.InitialMaxPower;
            RefreshPowerLabel();
            if (_energyCostButton != null) { _energyCostButton.Visible = true; _energyCostButton.Disabled = false; }
            if (_insufficientRow != null) _insufficientRow.Visible = false;
        }
        else if (stepId == MetaTutorialDatabase.StepIds.EnergyInsufficient)
        {
            _tutorialPower = ContinueExploreConfig.CombatEnergyCost - 5;
            RefreshPowerLabel();
            if (_energyCostButton != null) _energyCostButton.Visible = false;
            if (_insufficientRow != null) _insufficientRow.Visible = true;
        }
    }

    private void UpdateTextPanel(TutorialStep step)
    {
        if (_stepIndexLabel != null)
            _stepIndexLabel.Text = string.Format(Localization.Get("tutorial.step_fmt"), TutorialManager.CurrentStepIndex + 1, TutorialManager.TotalSteps);

        if (_titleLabel != null) _titleLabel.Text = step.DisplayTitle;
        if (_descLabel != null) _descLabel.Text = step.DisplayDescription;

        var obj = step.DisplayObjective;
        if (_objRow != null) _objRow.Visible = !string.IsNullOrWhiteSpace(obj);
        if (_objLabel != null) _objLabel.Text = obj;

        var tips = step.DisplayTips;
        if (_tipsRow != null) _tipsRow.Visible = !string.IsNullOrWhiteSpace(tips);
        if (_tipsLabel != null) _tipsLabel.Text = tips;

        if (_nextButton != null)
        {
            _nextButton.Visible = step.AdvanceMode == TutorialAdvanceMode.Button;
            _nextButton.Text = step.IsLastStep
                ? Localization.Get("tutorial.meta.btn.return_select")
                : Localization.Get("tutorial.btn.continue");
        }

        if (_prevButton != null) _prevButton.Disabled = !TutorialManager.CanGoPrevious;
    }

    // ─── 高亮框定位 ─────────────────────────────────────────────────────

    private void ApplyHighlightDeferred(string targetId)
    {
        if (_highlightBox == null) return;

        if (string.IsNullOrEmpty(targetId))
        {
            _highlightBox.Visible = false;
            return;
        }

        var rect = ComputeHighlightRect(targetId);
        if (rect.Size == Vector2.Zero)
        {
            _highlightBox.Visible = false;
            return;
        }

        _highlightBox.Visible = true;
        _highlightBox.GlobalPosition = rect.Position;
        _highlightBox.Size = rect.Size;
    }

    /// <summary>
    /// 所有演示节点/区域都是真正的 Control，直接用 Control.GetGlobalRect() 取得
    /// 精确的全局矩形——不再需要任何"局部坐标 → 全局坐标"的比例换算，
    /// 高亮框与实际渲染的节点/按钮严格重合。
    /// </summary>
    private Rect2 ComputeHighlightRect(string targetId)
    {
        switch (targetId)
        {
            case "chapter_name":
                return _chapterNameLabel?.GetGlobalRect() ?? default;
            case "full_map":
            case "node_chain":
                return _mainChainRow?.GetGlobalRect() ?? default;
            case "power_bar":
                return _powerSection?.GetGlobalRect() ?? default;
            case "power_insufficient_node":
                return _insufficientButton?.GetGlobalRect() ?? default;
            case "power_free_node":
                return _freeButton?.GetGlobalRect() ?? default;
            case "faction_fate_preview":
                return _factionSection?.GetGlobalRect() ?? default;
            case "initial_event_preview":
                return _eventSection?.GetGlobalRect() ?? default;
            case "chapter_cards":
                return _chapterCardsSection?.GetGlobalRect() ?? default;
            default:
                return _nodeWidgets.TryGetValue(targetId, out var widget) ? widget.GetGlobalRect() : default;
        }
    }
}
