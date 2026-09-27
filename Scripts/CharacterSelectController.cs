//////////////////////////////////////////////////////////
// 文件：Scripts/CharacterSelectController.cs
//
// 模块：Character System
//
// 职责：
// 1. 承载角色定义、角色选择与角色展示相关代码。
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
using System.Linq;

/// <summary>
/// Character System 的公开类：CharacterSelectController。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class CharacterSelectController : Control
{
    [Signal]
    public delegate void CharacterChosenEventHandler(string characterId);

    [Signal]
    public delegate void ReturnToMenuRequestedEventHandler();

    [Export]
    public bool DebugModeSelection { get; set; }

    private Label? _nameLabel;
    private Label? _genderLabel;
    private Label? _factionLabel;
    private Label? _hpLabel;
    private RichTextLabel? _skillsLabel;
    private Button? _returnToMenuButton;
    private PanelContainer? _infoPanel;
    private GridContainer? _characterFlow;
    private Button? _awakenButton;
    private string? _selectedCharacterId;
    private readonly Dictionary<string, Button> _characterButtons = new();
    private TutorialHighlightLayer? _tutorialLayer;

    /// <summary>
    /// Character System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        BuildLayout();
        ShowCharacterInfo(CharacterDatabase.GetAllCharacters()[0]);

        // Hero Unlock System：每次打开角色选择界面时，把积压的"新英雄解锁"公告
        // 逐个弹出。解锁本身可能发生在战斗/事件/地图等任何界面（不修改那些界面），
        // 这里是玩家下一次回到角色选择时第一个能够安全展示公告的地方。
        ShowNextPendingUnlockAnnouncement();

        // 整合式教程：角色选择是流程的第一屏。这里只叠加一层高亮+说明，不改变
        // 角色按钮只负责选中；由右下角【开始】明确确认并开始，避免误触直接开局。
        if (IntegratedTutorialFlow.IsActive)
        {
            SetupTutorialHighlight();
        }
    }

    private void SetupTutorialHighlight()
    {
        _tutorialLayer = new TutorialHighlightLayer();
        AddChild(_tutorialLayer);
        _tutorialLayer.SetHighlightTarget(_infoPanel);
        _tutorialLayer.ShowStep(
            Localization.Get("tutorial.integrated.char_select.title"),
            Localization.Get("tutorial.integrated.char_select.desc"),
            Localization.Get("tutorial.integrated.char_select.objective"),
            string.Empty,
            showContinueButton: false);
    }

    private void BuildLayout()
    {
        Theme = new Theme { DefaultFont = GD.Load<Font>("res://Assets/Fonts/NotoSerifCJKsc-Black.otf") };
        var city = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://Assets/Backgrounds/Map/bg_map_chapter2_city.png"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore, TextureFilter = TextureFilterEnum.Nearest
        };
        AddChild(city);
        city.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var background = new ColorRect
        {
            Color = new Color(0.015f, 0.025f, 0.045f, .86f),
            MouseFilter = MouseFilterEnum.Ignore
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 56);
        margin.AddThemeConstantOverride("margin_top", 40);
        margin.AddThemeConstantOverride("margin_right", 56);
        margin.AddThemeConstantOverride("margin_bottom", 40);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 24);
        margin.AddChild(root);

        var pageHeaderRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center
        };
        pageHeaderRow.AddThemeConstantOverride("separation", 18);
        root.AddChild(pageHeaderRow);

        var title = new Label
        {
            Text = DebugModeSelection ? "选择数据芯片 / 调试终端" : "选择数据芯片",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride("font_size", 52);
        title.AddThemeColorOverride("font_color", new Color("ff4876"));
        pageHeaderRow.AddChild(title);

        _returnToMenuButton = new Button
        {
            Text = Localization.Get("ui.return_to_menu"),
            CustomMinimumSize = new Vector2(190, 48),
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd
        };
        _returnToMenuButton.AddThemeFontSizeOverride("font_size", 22);
        _returnToMenuButton.Pressed += () => EmitSignal(SignalName.ReturnToMenuRequested);
        pageHeaderRow.AddChild(_returnToMenuButton);

        var factionTabs = new HBoxContainer();
        factionTabs.AddThemeConstantOverride("separation", 14);
        root.AddChild(factionTabs);
        foreach (var entry in new (string Text, Faction? Faction)[] { ("全部芯片", null), ("吴", Faction.Wu), ("蜀", Faction.Shu), ("魏", Faction.Wei), ("群", Faction.Qun) })
        {
            var tab = new Button { Text = entry.Text, CustomMinimumSize = new Vector2(150, 52), ToggleMode = true, ButtonPressed = entry.Faction == null };
            tab.AddThemeFontSizeOverride("font_size", 24);
            var accent = entry.Faction.HasValue ? HeroChipButton.FactionColor(entry.Faction.Value) : new Color("f02a63");
            var normal = CreateTerminalStyle();
            var selected = CreateTerminalStyle();
            selected.BorderColor = accent;
            selected.BgColor = new Color(accent.R*.16f,accent.G*.16f,accent.B*.16f,1);
            tab.AddThemeStyleboxOverride("normal", normal);
            tab.AddThemeStyleboxOverride("pressed", selected);
            tab.AddThemeStyleboxOverride("hover", selected);
            tab.AddThemeColorOverride("font_pressed_color", accent);
            tab.Pressed += () =>
            {
                foreach (var sibling in factionTabs.GetChildren().OfType<Button>()) sibling.SetPressedNoSignal(sibling == tab);
                foreach (var chip in _characterButtons.Values.OfType<HeroChipButton>())
                    chip.Visible = entry.Faction == null || chip.Hero.Faction == entry.Faction;
            };
            factionTabs.AddChild(tab);
        }

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 28);
        body.SizeFlagsVertical = SizeFlags.ExpandFill;
        root.AddChild(body);

        var buttonPanel = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        body.AddChild(buttonPanel);
        buttonPanel.AddThemeStyleboxOverride("panel", CreateTerminalStyle());

        var buttonMargin = new MarginContainer();
        buttonMargin.AddThemeConstantOverride("margin_left", 18);
        buttonMargin.AddThemeConstantOverride("margin_top", 18);
        buttonMargin.AddThemeConstantOverride("margin_right", 18);
        buttonMargin.AddThemeConstantOverride("margin_bottom", 18);
        buttonPanel.AddChild(buttonMargin);

        var scroll = new ScrollContainer
        {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        buttonMargin.AddChild(scroll);
        var flow = new GridContainer
        {
            Columns = 5,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkBegin
        };
        flow.AddThemeConstantOverride("h_separation", 16);
        flow.AddThemeConstantOverride("v_separation", 16);
        scroll.AddChild(flow);
        // 固定芯片尺寸，只随可用宽度调整列数，避免芯片被网格拉成大立绘。
        scroll.Resized += () => flow.Columns = Mathf.Max(1, (int)((scroll.Size.X - 20 + 16) / (HeroChipButton.ChipWidth + 16)));
        _characterFlow = flow;

        var serial = 0;
        foreach (var character in CharacterDatabase.GetAllCharacters().OrderByDescending(c => HeroUnlockProgress.IsHeroUnlocked(c.Id)))
        {
            var unlocked = HeroUnlockProgress.IsHeroUnlocked(character.Id);

            var button = new HeroChipButton
            {
                Hero = character, Unlocked = unlocked, Serial = ++serial
            };
            button.AddThemeFontSizeOverride("font_size", 24);
            if (!unlocked)
            {
                // 未解锁：整个按钮变灰，只作为"悬停查看解锁条件"的入口，不允许选中进入游戏。
                button.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.52f));
                button.AddThemeColorOverride("font_color_hover", new Color(0.6f, 0.6f, 0.62f));
            }

            button.MouseEntered += () =>
            {
                ShowCharacterInfo(character);
                if (IntegratedTutorialFlow.IsActive) IntegratedTutorialFlow.HasViewedAnyCharacterDetail = true;
            };
            button.FocusEntered += () =>
            {
                ShowCharacterInfo(character);
                if (IntegratedTutorialFlow.IsActive) IntegratedTutorialFlow.HasViewedAnyCharacterDetail = true;
            };
            button.MouseExited += () =>
            {
                var selected = CharacterDatabase.GetAllCharacters().FirstOrDefault(c => c.Id == _selectedCharacterId);
                if (selected != null) ShowCharacterInfo(selected);
            };
            if (unlocked)
            {
                button.Pressed += () => SelectCharacter(character);
            }
            _characterButtons[character.Id] = button;
            flow.AddChild(button);
        }

        var infoPanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(700, 0),
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        body.AddChild(infoPanel);
        infoPanel.AddThemeStyleboxOverride("panel", CreateTerminalStyle());
        _infoPanel = infoPanel;

        var infoMargin = new MarginContainer();
        infoMargin.AddThemeConstantOverride("margin_left", 20);
        infoMargin.AddThemeConstantOverride("margin_top", 20);
        infoMargin.AddThemeConstantOverride("margin_right", 20);
        infoMargin.AddThemeConstantOverride("margin_bottom", 20);
        infoPanel.AddChild(infoMargin);

        var infoRoot = new VBoxContainer();
        infoRoot.AddThemeConstantOverride("separation", 12);
        infoMargin.AddChild(infoRoot);
        var moduleTitle = CreateInfoLabel(18);
        moduleTitle.Text = "英雄数据终端  /  芯片读取";
        moduleTitle.AddThemeColorOverride("font_color", new Color("35b7cf"));
        infoRoot.AddChild(moduleTitle);

        var headerRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Begin
        };
        headerRow.AddThemeConstantOverride("separation", 18);
        infoRoot.AddChild(headerRow);

        _nameLabel = CreateInfoLabel(48);
        _nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _nameLabel.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        headerRow.AddChild(_nameLabel);

        _genderLabel = CreateInfoLabel(24);
        _factionLabel = CreateInfoLabel(24);
        _hpLabel = CreateInfoLabel(24);
        _skillsLabel = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            ScrollActive = true,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _skillsLabel.AddThemeFontSizeOverride("normal_font_size", 22);

        infoRoot.AddChild(_genderLabel);
        infoRoot.AddChild(_factionLabel);
        infoRoot.AddChild(_hpLabel);
        infoRoot.AddChild(_skillsLabel);

        var actionRow = new HBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.End,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        root.AddChild(actionRow);
        _awakenButton = new Button
        {
            Text = "插入芯片  ››",
            Disabled = true,
            CustomMinimumSize = new Vector2(440, 108),
            TooltipText = Localization.Get("char.select.start_hint")
        };
        _awakenButton.AddThemeFontSizeOverride("font_size", 40);
        ApplyStartButtonStyle(_awakenButton);
        _awakenButton.Pressed += ConfirmAwaken;
        infoRoot.AddChild(_awakenButton);

        // Hero Unlock System：开发者模式专属按钮，正式游戏（非开发者模式）完全不可见，
        // 不影响正常存档——只是调用 HeroUnlockProgress 的调试入口再重建一次界面。
        if (DeveloperModeManager.IsDeveloperMode)
        {
            var devRow = new HBoxContainer();
            devRow.AddThemeConstantOverride("separation", 12);
            root.AddChild(devRow);

            var unlockAllButton = new Button
            {
                Text = Localization.Get("char.select.debug_unlock_all"),
                CustomMinimumSize = new Vector2(200, 44)
            };
            unlockAllButton.AddThemeFontSizeOverride("font_size", 20);
            unlockAllButton.Pressed += () =>
            {
                HeroUnlockProgress.UnlockAllForDebug();
                RefreshLayout();
            };
            devRow.AddChild(unlockAllButton);

            var resetUnlockButton = new Button
            {
                Text = Localization.Get("char.select.debug_reset_unlock"),
                CustomMinimumSize = new Vector2(200, 44)
            };
            resetUnlockButton.AddThemeFontSizeOverride("font_size", 20);
            resetUnlockButton.Pressed += () =>
            {
                HeroUnlockProgress.ResetAll();
                RefreshLayout();
            };
            devRow.AddChild(resetUnlockButton);
        }
    }

    // 开发者模式解锁/重置按钮之后重建整个界面：最简单、最不容易出错的刷新方式，
    // 和既有代码里语言切换后重建地图界面（RefreshMapView）用的是同一个思路。
    private void RefreshLayout()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        _selectedCharacterId = null;
        _characterButtons.Clear();
        BuildLayout();
        ShowCharacterInfo(CharacterDatabase.GetAllCharacters()[0]);
    }

    private void SelectCharacter(CharacterData character)
    {
        if (!HeroUnlockProgress.IsHeroUnlocked(character.Id)) return;
        _selectedCharacterId = character.Id;
        ShowCharacterInfo(character);
        if (IntegratedTutorialFlow.IsActive) IntegratedTutorialFlow.HasViewedAnyCharacterDetail = true;

        foreach (var (id, button) in _characterButtons)
        {
            if (button is HeroChipButton chip) chip.SetSelected(id == character.Id);
        }

        if (_awakenButton != null)
        {
            _awakenButton.Disabled = false;
        }
    }

    private void ConfirmAwaken()
    {
        if (string.IsNullOrEmpty(_selectedCharacterId)) return;
        EmitSignal(SignalName.CharacterChosen, _selectedCharacterId);
    }

    private static StyleBoxFlat CreateTerminalStyle()
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(.018f, .035f, .05f, .97f),
            BorderColor = new Color("24566c"),
            ShadowColor = new Color(0,0,0,.65f), ShadowSize = 12
        };
        style.SetBorderWidthAll(3);
        style.SetCornerRadiusAll(12);
        return style;
    }

    private static void ApplyStartButtonStyle(Button button)
    {
        button.AddThemeColorOverride("font_color", new Color(0.06f, 0.08f, 0.10f));
        button.AddThemeColorOverride("font_hover_color", new Color(0.03f, 0.05f, 0.06f));
        button.AddThemeColorOverride("font_disabled_color", new Color(0.42f, 0.44f, 0.48f));
        button.AddThemeStyleboxOverride("normal", CreateStartButtonStyle(new Color(0.98f, 0.72f, 0.12f), new Color(1f, 0.90f, 0.42f)));
        button.AddThemeStyleboxOverride("hover", CreateStartButtonStyle(new Color(1f, 0.84f, 0.22f), new Color(1f, 0.96f, 0.60f)));
        button.AddThemeStyleboxOverride("pressed", CreateStartButtonStyle(new Color(0.80f, 0.52f, 0.05f), new Color(1f, 0.80f, 0.20f)));
        button.AddThemeStyleboxOverride("disabled", CreateStartButtonStyle(new Color(0.16f, 0.17f, 0.20f), new Color(0.30f, 0.32f, 0.37f)));
    }

    private static StyleBoxFlat CreateStartButtonStyle(Color background, Color border)
    {
        var style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            ShadowColor = new Color(border, 0.42f),
            ShadowSize = 10,
            ShadowOffset = new Vector2(0, 3),
            ContentMarginLeft = 36,
            ContentMarginRight = 36,
            ContentMarginTop = 14,
            ContentMarginBottom = 14
        };
        style.SetBorderWidthAll(3);
        style.SetCornerRadiusAll(14);
        return style;
    }

    private void ShowCharacterInfo(CharacterData character)
    {
        if (_nameLabel == null || _genderLabel == null || _factionLabel == null || _hpLabel == null || _skillsLabel == null)
        {
            return;
        }

        var unlocked = HeroUnlockProgress.IsHeroUnlocked(character.Id);

        // 悬停预览另一枚芯片时不能确认旧角色，避免显示详情与实际开局角色不一致。
        if (_awakenButton != null) _awakenButton.Disabled = !unlocked || _selectedCharacterId != character.Id;

        // 未解锁：姓名/阵营/技能全部替换成"？？？"或解锁条件+进度，不剧透真实内容。
        _nameLabel.Text = unlocked ? character.Name : Localization.Get("char.locked_placeholder");
        _nameLabel.AddThemeColorOverride("font_color", unlocked ? HeroChipButton.FactionColor(character.Faction) : new Color("63717c"));
        _genderLabel.Text = unlocked
            ? string.Format(Localization.Get("char.gender_fmt"), GetGenderText(character.Gender))
            : string.Empty;
        _factionLabel.Text = unlocked
            ? string.Format(Localization.Get("char.faction_fmt"), GetFactionText(character.Faction))
            : string.Empty;
        _hpLabel.Text = unlocked ? string.Format(Localization.Get("char.hp_fmt"), character.MaxHp) : string.Empty;

        if (!unlocked)
        {
            _skillsLabel.Text = BuildUnlockConditionText(character);
            return;
        }

        _skillsLabel.Text = BuildSkillsText(character);

    }

    /// <summary>
    /// 未解锁角色的说明文案：解锁方式 + 当前进度（例如"装备：8 / 15"），
    /// 保证玩家始终知道自己距离解锁还差多少。三种条件类型统一走
    /// <see cref="HeroUnlockProgress.EvaluateCondition"/>，这里不重复判断逻辑。
    /// </summary>
    private static string BuildUnlockConditionText(CharacterData character)
    {
        var condition = HeroUnlockDatabase.Get(character.Id);
        if (condition == null)
        {
            // 理论上不会出现：IsHeroUnlocked 对没有注册条件的角色总是返回 true，
            // 走不到"未解锁"分支。这里只是兜底，不让界面崩溃。
            return Localization.Get("char.locked_placeholder");
        }

        var (_, current, required) = HeroUnlockProgress.EvaluateCondition(condition);
        var progressText = string.Format(Localization.Get("char.locked_progress_fmt"), current, required);

        return $"[b]{Localization.Get("char.locked_condition_label")}[/b]\n{condition.DisplayDescription}\n\n[b]{Localization.Get("char.locked_progress_label")}[/b]\n{progressText}";
    }

    private static Label CreateInfoLabel(int fontSize)
    {
        var label = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    private static string GetGenderText(Gender gender)
        => gender == Gender.Female ? Localization.Get("char.gender.female") : Localization.Get("char.gender.male");

    private static string GetFactionText(Faction faction)
    {
        return faction switch
        {
            Faction.Wei => Localization.Get("char.faction.wei"),
            Faction.Shu => Localization.Get("char.faction.shu"),
            Faction.Wu => Localization.Get("char.faction.wu"),
            Faction.Qun => Localization.Get("char.faction.qun"),
            _ => string.Empty
        };
    }

    private static string BuildSkillsText(CharacterData character)
    {
        if (character.SkillIds.Count == 0)
        {
            return $"[b]{Localization.Get("char.skills_label")}[/b]\n{Localization.Get("char.no_skills")}";
        }

        var sb = new System.Text.StringBuilder();
        sb.Append($"[b]{Localization.Get("char.skills_label")}[/b]");

        foreach (var skillId in character.SkillIds)
        {
            var skill = SkillDatabase.GetSkill(skillId);
            var name = skill is null ? skillId : Localization.GetName(skill);
            var desc = skill is null ? string.Empty : Localization.GetDescription(skill);

            sb.Append($"\n\n[b]{name}[/b]");
            if (!string.IsNullOrEmpty(desc))
            {
                sb.Append($"\n{desc}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// 弹出一条"新英雄已解锁"公告（如果有积压的话）。一条公告可能包含多个角色
    /// （例如刘备/关羽/张飞共用同一个解锁条件、同时达成时只合并弹一次，标题变成
    /// "蜀汉三兄弟已解锁"，而不是连续弹三个独立窗口），单人解锁时展示方式和之前
    /// 完全一样。确认后继续检查是否还有下一条积压公告，并刷新一次角色列表，
    /// 让刚解锁的英雄立刻以正常样式出现在选择界面里。
    /// </summary>
    private void ShowNextPendingUnlockAnnouncement()
    {
        var characterIds = HeroUnlockProgress.DequeuePendingUnlockAnnouncement();
        if (characterIds == null || characterIds.Count == 0)
        {
            return;
        }

        var characters = characterIds
            .Select(CharacterDatabase.GetCharacter)
            .Where(character => character != null)
            .Select(character => character!)
            .ToList();

        var title = characters.Count > 1
            ? HeroUnlockDatabase.Get(characterIds[0])?.DisplayGroupAnnouncementTitle
                ?? Localization.GetOrFallback("char.unlock.announcement.title", "新英雄已解锁")
            : Localization.GetOrFallback("char.unlock.announcement.title", "新英雄已解锁");

        var bodyText = characters.Count == 0
            ? string.Join("、", characterIds)
            : string.Join("\n\n", characters.Select(BuildUnlockAnnouncementEntryText));

        var dialog = new AcceptDialog
        {
            Title = title,
            DialogText = bodyText
        };
        AddChild(dialog);

        if (characters.Count > 0)
        {
            var portraitRow = new HBoxContainer();
            portraitRow.AddThemeConstantOverride("separation", 12);
            foreach (var character in characters)
            {
                var portrait = new TextureRect
                {
                    Texture = CharacterVisualDatabase.TryGetPortraitTexture(character.Id) ?? IconLibrary.GetDefaultIcon(),
                    ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    CustomMinimumSize = new Vector2(140, 140)
                };
                portraitRow.AddChild(portrait);
            }

            dialog.AddChild(portraitRow);
        }

        dialog.Confirmed += () =>
        {
            dialog.QueueFree();
            RefreshLayout();
            ShowNextPendingUnlockAnnouncement();
        };
        dialog.Canceled += () =>
        {
            dialog.QueueFree();
            RefreshLayout();
            ShowNextPendingUnlockAnnouncement();
        };
        dialog.PopupCentered();
    }

    private static string BuildUnlockAnnouncementEntryText(CharacterData character)
    {
        var skillNames = character.SkillIds.Count == 0
            ? Localization.Get("char.no_skills")
            : string.Join(
                "、",
                character.SkillIds
                    .Select(SkillDatabase.GetSkill)
                    .Where(skill => skill != null)
                    .Select(skill => Localization.GetName(skill!)));

        return $"{character.Name}\n{string.Format(Localization.Get("char.faction_fmt"), GetFactionText(character.Faction))}\n{skillNames}";
    }
}
