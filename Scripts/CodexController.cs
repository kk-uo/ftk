//////////////////////////////////////////////////////////
// 文件：Scripts/CodexController.cs
//
// 模块：Codex System
//
// 职责：
// 1. 承载图鉴数据聚合与图鉴界面相关代码。
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
using System.Linq;

/// <summary>
/// Codex System 的公开类：CodexController。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class CodexController : Control
{
    [Signal]
    public delegate void ReturnToMenuRequestedEventHandler();

    private enum CodexCategory
    {
        Overview,
        Card,
        Equipment,
        Enemy,
        Buff,
        Event,
        Boss,
        Route
    }

    private sealed class CodexEntryModel
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public CodexCategory Category { get; init; }
        public string IconPath { get; init; } = string.Empty;
        public string TypeText { get; init; } = string.Empty;
        public string RarityText { get; init; } = string.Empty;
        public string CostText { get; init; } = string.Empty;
        public string ChapterText { get; init; } = string.Empty;
        public string DangerReason { get; init; } = string.Empty;
        public string CodeText { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string DetailDescription { get; init; } = string.Empty;
        public List<(string Label, string Value)> BasicInfo { get; init; } = new();
        /// <summary>未发现内容默认为true（Buff/Route等尚未接入永久图鉴发现规则的分类）；
        /// 卡牌/敌人/装备/事件的具体取值见各自 Build*Entries。</summary>
        public bool Discovered { get; init; } = true;
    }

    private static readonly (CodexCategory Category, string LabelKey)[] CategoryOrder =
    {
        (CodexCategory.Overview, "codex.cat.overview"),
        (CodexCategory.Card, "codex.cat.card"),
        (CodexCategory.Equipment, "codex.cat.equipment"),
        (CodexCategory.Enemy, "codex.cat.enemy"),
        (CodexCategory.Buff, "codex.cat.buff"),
        (CodexCategory.Event, "codex.cat.event"),
        (CodexCategory.Boss, "codex.cat.boss"),
        (CodexCategory.Route, "codex.cat.route")
    };

    private CodexCategory _currentCategory = CodexCategory.Overview;
    private string _searchText = string.Empty;
    private string _selectedEntryId = string.Empty;
    private readonly Dictionary<CodexCategory, Button> _categoryButtons = new();
    private readonly Dictionary<string, Button> _entryButtons = new();

    private Button? _backButton;
    private Label? _topTitleLabel;
    private LineEdit? _searchField;
    private Label? _filterTitleLabel;
    private Label? _entryListTitleLabel;
    private VBoxContainer? _entryListContainer;
    private Label? _entryCountLabel;
    private Label? _detailTitleLabel;
    private Label? _basicInfoTitleLabel;
    private Label? _introTitleLabel;
    private Label? _detailNameLabel;
    private Label? _detailTypeLabel;
    private Label? _detailRarityLabel;
    private Label? _detailCostLabel;
    private Label? _detailChapterLabel;
    private Label? _detailDangerLabel;
    private VBoxContainer? _detailBasicInfoContainer;
    private Label? _detailDescriptionLabel;
    private Button? _detailToggleButton;
    private RichTextLabel? _detailDescriptionRichText;
    private PanelContainer? _imagePanel;
    private Label? _imagePlaceholderLabel;
    private bool _detailExpanded;

    /// <summary>
    /// Codex System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        BuildLayout();
        RefreshLocalizedText();
        RefreshCategoryButtons();
        RefreshEntryList();
        Localization.LanguageChanged += OnLanguageChanged;
    }

    /// <summary>
    /// Codex System 的公开入口：_ExitTree。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _ExitTree()
    {
        Localization.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        RefreshLocalizedText();
        RefreshEntryList();
    }

    private void RefreshLocalizedText()
    {
        if (_backButton != null) _backButton.Text = Localization.Get("codex.return_menu");
        if (_topTitleLabel != null) _topTitleLabel.Text = Localization.Get("codex.title");
        if (_searchField != null) _searchField.PlaceholderText = Localization.Get("codex.search_placeholder");
        if (_filterTitleLabel != null) _filterTitleLabel.Text = Localization.Get("codex.filter_label");
        if (_entryListTitleLabel != null) _entryListTitleLabel.Text = Localization.Get("codex.entry_list_title");
        if (_detailTitleLabel != null) _detailTitleLabel.Text = Localization.Get("codex.detail_title");
        if (_basicInfoTitleLabel != null) _basicInfoTitleLabel.Text = Localization.Get("codex.info.basic_title");
        if (_introTitleLabel != null) _introTitleLabel.Text = Localization.Get("codex.info.intro_title");
        foreach (var (category, key) in CategoryOrder)
            if (_categoryButtons.TryGetValue(category, out var btn))
                btn.Text = Localization.Get(key);
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

        var rootMargin = new MarginContainer();
        rootMargin.SetAnchorsPreset(LayoutPreset.FullRect);
        rootMargin.AddThemeConstantOverride("margin_left", 40);
        rootMargin.AddThemeConstantOverride("margin_top", 28);
        rootMargin.AddThemeConstantOverride("margin_right", 40);
        rootMargin.AddThemeConstantOverride("margin_bottom", 28);
        AddChild(rootMargin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 16);
        rootMargin.AddChild(root);

        root.AddChild(BuildTopBar());

        var body = new HBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        body.AddThemeConstantOverride("separation", 18);
        root.AddChild(body);

        body.AddChild(BuildCategoryPanel());
        body.AddChild(BuildEntryListPanel());
        body.AddChild(BuildDetailPanel());
        UIResourceDatabase.ApplyScrollBars(this);
    }

    private Control BuildTopBar()
    {
        var topBar = new HBoxContainer();
        topBar.AddThemeConstantOverride("separation", 12);

        _backButton = new Button
        {
            CustomMinimumSize = new Vector2(160, 48)
        };
        _backButton.AddThemeFontSizeOverride("font_size", 22);
        _backButton.Pressed += () => EmitSignal(SignalName.ReturnToMenuRequested);
        topBar.AddChild(_backButton);

        _topTitleLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _topTitleLabel.AddThemeFontSizeOverride("font_size", 38);
        topBar.AddChild(_topTitleLabel);

        _searchField = new LineEdit
        {
            CustomMinimumSize = new Vector2(280, 48)
        };
        _searchField.AddThemeFontSizeOverride("font_size", 20);
        _searchField.TextChanged += text =>
        {
            _searchText = text ?? string.Empty;
            RefreshEntryList();
        };
        topBar.AddChild(_searchField);

        return topBar;
    }

    private Control BuildCategoryPanel()
    {
        var panel = CreatePanel(new Vector2(180, 0));
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var margin = CreateMargin(panel, 14, 16, 14, 16);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        margin.AddChild(root);

        _filterTitleLabel = new Label();
        _filterTitleLabel.AddThemeFontSizeOverride("font_size", 24);
        root.AddChild(_filterTitleLabel);

        foreach (var (category, key) in CategoryOrder)
        {
            var button = new Button
            {
                Text = Localization.Get(key),
                CustomMinimumSize = new Vector2(0, 50)
            };
            button.AddThemeFontSizeOverride("font_size", 21);
            button.Pressed += () =>
            {
                _currentCategory = category;
                RefreshCategoryButtons();
                RefreshEntryList();
            };
            root.AddChild(button);
            _categoryButtons[category] = button;
        }

        root.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill });
        return panel;
    }

    private Control BuildEntryListPanel()
    {
        var panel = CreatePanel(new Vector2(320, 0));
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var margin = CreateMargin(panel, 16, 16, 16, 16);
        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);

        _entryListTitleLabel = new Label();
        _entryListTitleLabel.AddThemeFontSizeOverride("font_size", 24);
        header.AddChild(_entryListTitleLabel);

        header.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _entryCountLabel = new Label();
        _entryCountLabel.AddThemeFontSizeOverride("font_size", 18);
        _entryCountLabel.AddThemeColorOverride("font_color", new Color(0.76f, 0.80f, 0.88f));
        header.AddChild(_entryCountLabel);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddChild(scroll);

        _entryListContainer = new VBoxContainer();
        _entryListContainer.AddThemeConstantOverride("separation", 8);
        _entryListContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_entryListContainer);

        return panel;
    }

    private Control BuildDetailPanel()
    {
        var panel = CreatePanel(Vector2.Zero);
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.SizeFlagsVertical = SizeFlags.ExpandFill;

        var margin = CreateMargin(panel, 18, 18, 18, 18);
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        margin.AddChild(scroll);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 14);
        root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(root);

        _detailTitleLabel = new Label();
        _detailTitleLabel.AddThemeFontSizeOverride("font_size", 24);
        root.AddChild(_detailTitleLabel);

        var imageCenter = new CenterContainer();
        root.AddChild(imageCenter);

        _imagePanel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(300, 300),
            MouseFilter = MouseFilterEnum.Ignore
        };
        ApplyImagePanelStyle(_imagePanel);
        imageCenter.AddChild(_imagePanel);

        _imagePlaceholderLabel = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _imagePlaceholderLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        _imagePlaceholderLabel.AddThemeFontSizeOverride("font_size", 28);
        _imagePlaceholderLabel.AddThemeColorOverride("font_color", new Color(0.54f, 0.58f, 0.66f));
        _imagePanel.AddChild(_imagePlaceholderLabel);

        _detailNameLabel = new Label();
        _detailNameLabel.AddThemeFontSizeOverride("font_size", 34);
        root.AddChild(_detailNameLabel);

        var infoGrid = new GridContainer
        {
            Columns = 2
        };
        infoGrid.AddThemeConstantOverride("h_separation", 18);
        infoGrid.AddThemeConstantOverride("v_separation", 10);
        root.AddChild(infoGrid);

        _detailTypeLabel = MakeInfoLabel();
        _detailRarityLabel = MakeInfoLabel();
        _detailCostLabel = MakeInfoLabel();
        _detailChapterLabel = MakeInfoLabel();

        infoGrid.AddChild(_detailTypeLabel);
        infoGrid.AddChild(_detailRarityLabel);
        infoGrid.AddChild(_detailCostLabel);
        infoGrid.AddChild(_detailChapterLabel);

        _detailDangerLabel = new Label
        {
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _detailDangerLabel.AddThemeFontSizeOverride("font_size", 19);
        _detailDangerLabel.AddThemeColorOverride("font_color", new Color(0.94f, 0.56f, 0.40f));
        root.AddChild(_detailDangerLabel);

        var basicPanel = CreateSectionPanel(out _basicInfoTitleLabel, out var basicContent);
        _detailBasicInfoContainer = new VBoxContainer();
        _detailBasicInfoContainer.AddThemeConstantOverride("separation", 6);
        basicContent.AddChild(_detailBasicInfoContainer);
        root.AddChild(basicPanel);

        var introPanel = CreateSectionPanel(out _introTitleLabel, out var introContent);
        _detailDescriptionLabel = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _detailDescriptionLabel.AddThemeFontSizeOverride("font_size", 20);
        _detailDescriptionLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.88f, 0.90f));
        introContent.AddChild(_detailDescriptionLabel);
        root.AddChild(introPanel);

        var detailWrapperPanel = CreatePanel(Vector2.Zero);
        var detailMargin = CreateMargin(detailWrapperPanel, 14, 14, 14, 14);
        var detailBox = new VBoxContainer();
        detailBox.AddThemeConstantOverride("separation", 10);
        detailMargin.AddChild(detailBox);
        root.AddChild(detailWrapperPanel);

        _detailToggleButton = new Button
        {
            Flat = true,
            Alignment = HorizontalAlignment.Left
        };
        _detailToggleButton.AddThemeFontSizeOverride("font_size", 22);
        _detailToggleButton.Pressed += ToggleDetailSection;
        detailBox.AddChild(_detailToggleButton);

        _detailDescriptionRichText = new RichTextLabel
        {
            FitContent = true,
            SelectionEnabled = true,
            ScrollActive = false,
            Visible = false
        };
        _detailDescriptionRichText.AddThemeFontSizeOverride("normal_font_size", 18);
        detailBox.AddChild(_detailDescriptionRichText);

        return panel;
    }

    private void RefreshCategoryButtons()
    {
        foreach (var (category, button) in _categoryButtons)
        {
            var selected = category == _currentCategory;
            button.Modulate = selected
                ? new Color(1.0f, 0.90f, 0.62f)
                : Colors.White;
        }
    }

    private void RefreshEntryList()
    {
        if (_entryListContainer == null)
        {
            return;
        }

        foreach (var child in _entryListContainer.GetChildren())
        {
            _entryListContainer.RemoveChild(child);
            child.QueueFree();
        }
        _entryButtons.Clear();

        var entries = GetFilteredEntries();
        _entryCountLabel!.Text = Localization.GetFmt("codex.count_fmt", entries.Count);

        if (entries.Count == 0)
        {
            var empty = new Label
            {
                Text = Localization.Get("codex.no_results"),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            empty.AddThemeFontSizeOverride("font_size", 22);
            empty.AddThemeColorOverride("font_color", new Color(0.72f, 0.74f, 0.78f));
            _entryListContainer.AddChild(empty);
            UpdateDetail(null);
            return;
        }

        if (entries.All(entry => entry.Id != _selectedEntryId))
        {
            _selectedEntryId = entries[0].Id;
            _detailExpanded = false;
        }

        foreach (var entry in entries)
        {
            var button = BuildEntryButton(entry);
            _entryListContainer.AddChild(button);
            _entryButtons[entry.Id] = button;
        }

        RefreshEntryButtonStates();
        UpdateDetail(entries.FirstOrDefault(entry => entry.Id == _selectedEntryId));
    }

    private Button BuildEntryButton(CodexEntryModel entry)
    {
        var button = new Button
        {
            Text = string.Empty,
            CustomMinimumSize = new Vector2(0, 72),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        button.AddThemeStyleboxOverride("normal", CreateListEntryStyle(false));
        button.AddThemeStyleboxOverride("hover", CreateListEntryStyle(true));
        button.AddThemeStyleboxOverride("pressed", CreateListEntryStyle(true));

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        button.AddChild(row);

        var iconBox = new PanelContainer
        {
            CustomMinimumSize = new Vector2(44, 44),
            MouseFilter = MouseFilterEnum.Ignore
        };
        ApplySmallIconStyle(iconBox);
        row.AddChild(iconBox);

        var iconLabel = new Label
        {
            Text = Localization.Get("codex.icon.placeholder"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        iconLabel.SetAnchorsPreset(LayoutPreset.FullRect);
        iconLabel.AddThemeFontSizeOverride("font_size", 16);
        iconLabel.AddThemeColorOverride("font_color", new Color(0.74f, 0.78f, 0.84f));
        iconBox.AddChild(iconLabel);

        var textBox = new VBoxContainer();
        textBox.AddThemeConstantOverride("separation", 2);
        textBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(textBox);

        var nameLabel = new Label { Text = entry.Name };
        nameLabel.AddThemeFontSizeOverride("font_size", 22);
        textBox.AddChild(nameLabel);

        var meta = BuildListMeta(entry);
        if (!string.IsNullOrEmpty(meta))
        {
            var metaLabel = new Label { Text = meta };
            metaLabel.AddThemeFontSizeOverride("font_size", 15);
            metaLabel.AddThemeColorOverride("font_color", new Color(0.68f, 0.72f, 0.80f));
            textBox.AddChild(metaLabel);
        }

        button.Pressed += () =>
        {
            _selectedEntryId = entry.Id;
            RefreshEntryButtonStates();
            UpdateDetail(entry);
        };

        return button;
    }

    private void RefreshEntryButtonStates()
    {
        foreach (var (id, button) in _entryButtons)
        {
            var selected = id == _selectedEntryId;
            button.AddThemeStyleboxOverride("normal", CreateListEntryStyle(selected));
            button.AddThemeStyleboxOverride("hover", CreateListEntryStyle(true));
            button.AddThemeStyleboxOverride("pressed", CreateListEntryStyle(true));
        }
    }

    private void UpdateDetail(CodexEntryModel? entry)
    {
        if (_detailNameLabel == null
            || _detailTypeLabel == null
            || _detailRarityLabel == null
            || _detailCostLabel == null
            || _detailChapterLabel == null
            || _detailDangerLabel == null
            || _detailBasicInfoContainer == null
            || _detailDescriptionLabel == null
            || _detailDescriptionRichText == null
            || _detailToggleButton == null
            || _imagePlaceholderLabel == null)
        {
            return;
        }

        if (entry == null)
        {
            if (_detailTitleLabel != null) _detailTitleLabel.Text = string.Empty;
            if (_introTitleLabel != null) _introTitleLabel.Text = Localization.Get("codex.info.intro_title");
            _detailNameLabel.Text = Localization.Get("codex.no_selection");
            _detailTypeLabel.Text = string.Empty;
            _detailRarityLabel.Text = string.Empty;
            _detailCostLabel.Text = string.Empty;
            _detailChapterLabel.Text = string.Empty;
            _detailDangerLabel.Visible = false;
            _detailDescriptionLabel.Text = Localization.Get("codex.select_prompt");
            _detailDescriptionRichText.Text = string.Empty;
            _detailDescriptionRichText.Visible = false;
            _detailToggleButton.Text = Localization.Get("codex.rules_expand");
            _imagePlaceholderLabel.Text = Localization.Get("codex.image_placeholder");

            foreach (var child in _detailBasicInfoContainer.GetChildren())
            {
                _detailBasicInfoContainer.RemoveChild(child);
                child.QueueFree();
            }
            return;
        }

        if (_detailTitleLabel != null)
        {
            _detailTitleLabel.Text = entry.CodeText;
            if (!string.IsNullOrEmpty(entry.CodeText))
            {
                _detailTitleLabel.AddThemeFontSizeOverride("font_size", 16);
                _detailTitleLabel.AddThemeColorOverride("font_color", new Color(0.62f, 0.65f, 0.70f));
            }
        }
        _detailNameLabel.Text = entry.Name;
        _detailTypeLabel.Text = string.IsNullOrEmpty(entry.TypeText) ? string.Empty : Localization.GetFmt("codex.info.type_fmt", entry.TypeText);
        _detailRarityLabel.Text = string.IsNullOrEmpty(entry.RarityText) ? string.Empty : Localization.GetFmt("codex.info.rarity_fmt", entry.RarityText);
        _detailCostLabel.Text = string.IsNullOrEmpty(entry.CostText) ? string.Empty : Localization.GetFmt("codex.info.cost_fmt", entry.CostText);
        _detailChapterLabel.Text = string.IsNullOrEmpty(entry.ChapterText) ? string.Empty : Localization.GetFmt("codex.info.chapter_fmt", entry.ChapterText);
        _detailDangerLabel.Text = string.IsNullOrEmpty(entry.DangerReason) ? string.Empty : Localization.GetFmt("codex.info.danger_fmt", entry.DangerReason);
        _detailDangerLabel.Visible = !string.IsNullOrEmpty(entry.DangerReason);
        _introTitleLabel!.Text = entry.Category == CodexCategory.Equipment
            ? Localization.Get("codex.equip.description_title")
            : Localization.Get("codex.info.intro_title");
        _detailDescriptionLabel.Text = entry.Description;
        _detailDescriptionRichText.Text = entry.DetailDescription;
        _detailDescriptionRichText.Visible = _detailExpanded;
        _detailToggleButton.Text = entry.Category == CodexCategory.Equipment
            ? Localization.Get(_detailExpanded ? "codex.equip.effects_collapse" : "codex.equip.effects_expand")
            : Localization.Get(_detailExpanded ? "codex.rules_collapse" : "codex.rules_expand");
        _imagePlaceholderLabel.Text = string.IsNullOrEmpty(entry.IconPath) ? Localization.Get("codex.image_placeholder") : Localization.Get("codex.image_pending");

        foreach (var child in _detailBasicInfoContainer.GetChildren())
        {
            _detailBasicInfoContainer.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var (label, value) in entry.BasicInfo)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var line = new Label
            {
                Text = Localization.GetFmt("ui.label_value_fmt", label, value),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            line.AddThemeFontSizeOverride("font_size", 18);
            line.AddThemeColorOverride("font_color", new Color(0.84f, 0.86f, 0.90f));
            _detailBasicInfoContainer.AddChild(line);
        }
    }

    private void ToggleDetailSection()
    {
        _detailExpanded = !_detailExpanded;
        var entry = GetFilteredEntries().FirstOrDefault(model => model.Id == _selectedEntryId);
        UpdateDetail(entry);
    }

    private List<CodexEntryModel> GetFilteredEntries()
    {
        var entries = BuildEntriesForCategory(_currentCategory);
        if (string.IsNullOrWhiteSpace(_searchText))
        {
            return entries;
        }

        return entries
            .Where(entry => entry.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private List<CodexEntryModel> BuildEntriesForCategory(CodexCategory category)
    {
        return category switch
        {
            CodexCategory.Overview => BuildOverviewEntries(),
            CodexCategory.Card => BuildCardEntries(),
            CodexCategory.Equipment => BuildEquipmentEntries(),
            CodexCategory.Enemy => BuildEnemyEntries(includeBoss: false),
            CodexCategory.Buff => BuildBuffEntries(),
            CodexCategory.Event => BuildEventEntries(),
            CodexCategory.Boss => BuildEnemyEntries(includeBoss: true),
            CodexCategory.Route => BuildRouteEntries(),
            _ => new List<CodexEntryModel>()
        };
    }

    // 总览分类只有单一条目：把 CodexService.Global 的累计统计与各分类的发现/完成度
    // 汇总成一段说明文字，不走各分类各自的"发现/未发现"门控（总览本身始终可见）。
    private static List<CodexEntryModel> BuildOverviewEntries()
    {
        var g = CodexService.Global;
        var isDev = DeveloperModeManager.IsDeveloperMode;

        var totalCards = ((CardType[])Enum.GetValues(typeof(CardType))).Length;
        var discoveredCards = ((CardType[])Enum.GetValues(typeof(CardType)))
            .Count(type => CodexService.GetCard(type)?.Discovered ?? false);

        var allEnemies = EnemyDatabase.GetAllEnemies().ToList();
        var totalEnemies = allEnemies.Count;
        var discoveredEnemies = allEnemies.Count(def => CodexService.GetEnemy(def.Id)?.Discovered ?? false);

        var allEquipment = EquipmentDatabase.GetAllEquipments().ToList();
        var totalEquipment = allEquipment.Count;
        var obtainedEquipment = allEquipment.Count(def => CodexService.GetEquipment(def.Id)?.Obtained ?? false);

        var allEvents = EventDatabase.GetAllEvents().ToList();
        var totalEvents = allEvents.Count;
        var discoveredEvents = allEvents.Count(ev => CodexService.GetEvent(ev.Id)?.Discovered ?? false);

        static int Percent(int done, int total) => total > 0 ? done * 100 / total : 0;

        var totalDiscovered = discoveredCards + discoveredEnemies + obtainedEquipment + discoveredEvents;
        var totalDefinitions = totalCards + totalEnemies + totalEquipment + totalEvents;
        var overallPercent = Percent(totalDiscovered, totalDefinitions);

        var info = new List<(string, string)>
        {
            (Localization.Get("codex.overview.runs"), g.TotalRuns.ToString()),
            (Localization.Get("codex.overview.runs_completed"), g.RunsCompleted.ToString()),
            (Localization.Get("codex.overview.battles"), g.TotalBattles.ToString()),
            (Localization.Get("codex.overview.victories"), g.TotalVictories.ToString()),
            (Localization.Get("codex.overview.deaths"), g.TotalDeaths.ToString()),
            (Localization.Get("codex.overview.enemies_defeated"), g.TotalEnemiesDefeated.ToString()),
            (Localization.Get("codex.overview.bosses_defeated"), g.TotalBossesDefeated.ToString()),
            (Localization.Get("codex.overview.cards_used"), g.TotalCardsUsed.ToString()),
            (Localization.Get("codex.overview.damage_dealt"), g.TotalDamageDealt.ToString()),
            (Localization.Get("codex.overview.damage_taken"), g.TotalDamageTaken.ToString()),
            (Localization.Get("codex.overview.healing_done"), g.TotalHealingDone.ToString()),
            (Localization.Get("codex.overview.gold_earned"), g.TotalGoldEarned.ToString()),
            (Localization.Get("codex.overview.gold_spent"), g.TotalGoldSpent.ToString()),
            (Localization.Get("codex.overview.equipment_obtained"), g.TotalEquipmentObtained.ToString())
        };

        var detail = string.Join("\n", info.Select(pair => $"{pair.Item1}：{pair.Item2}"))
            + "\n\n" + Localization.Get("codex.overview.completion_header")
            + "\n" + Localization.GetFmt("codex.overview.completion_cards_fmt", discoveredCards, totalCards, Percent(discoveredCards, totalCards))
            + "\n" + Localization.GetFmt("codex.overview.completion_enemies_fmt", discoveredEnemies, totalEnemies, Percent(discoveredEnemies, totalEnemies))
            + "\n" + Localization.GetFmt("codex.overview.completion_equipment_fmt", obtainedEquipment, totalEquipment, Percent(obtainedEquipment, totalEquipment))
            + "\n" + Localization.GetFmt("codex.overview.completion_events_fmt", discoveredEvents, totalEvents, Percent(discoveredEvents, totalEvents))
            + "\n" + Localization.GetFmt("codex.overview.completion_overall_fmt", overallPercent);

        if (isDev)
        {
            info.Insert(0, (Localization.Get("codex.info.internal_id"), "overview"));
        }

        return new List<CodexEntryModel>
        {
            new CodexEntryModel
            {
                Id = "overview:global",
                Name = Localization.Get("codex.overview.title"),
                Category = CodexCategory.Overview,
                Description = Localization.Get("codex.overview.subtitle"),
                DetailDescription = detail,
                BasicInfo = info,
                Discovered = true
            }
        };
    }

    private static List<CodexEntryModel> BuildCardEntries()
    {
        return ((CardType[])Enum.GetValues(typeof(CardType)))
            .Select(type => new Card(type))
            .OrderBy(card => card.Cost)
            .ThenBy(card => Localization.GetName(card))
            .Select(card =>
            {
                var discovered = DeveloperModeManager.IsDeveloperMode || (CodexService.GetCard(card.Type)?.Discovered ?? false);
                if (!discovered)
                {
                    return new CodexEntryModel
                    {
                        Id = $"card:{card.Type}",
                        Name = Localization.Get("codex.undiscovered"),
                        Category = CodexCategory.Card,
                        TypeText = card.TypeLabel,
                        CostText = BattleRules.FormatMana(card.Cost),
                        Description = Localization.Get("codex.undiscovered.desc"),
                        DetailDescription = Localization.Get("codex.undiscovered.desc"),
                        Discovered = false
                    };
                }

                var matchupText = string.Join("\n", CardMatchupData.GetMatchups(card.Type) ?? Array.Empty<string>());
                return new CodexEntryModel
                {
                    Id = $"card:{card.Type}",
                    Name = Localization.GetName(card),
                    Category = CodexCategory.Card,
                    TypeText = card.TypeLabel,
                    CostText = BattleRules.FormatMana(card.Cost),
                    Description = BuildCardShortDescription(card),
                    DetailDescription = BuildCardDetailDescription(card, matchupText) + BuildCardStatsText(card.Type),
                    BasicInfo = new List<(string, string)>
                    {
                        (Localization.Get("codex.info.subtype"), GetCardSubTypeText(card.SubType)),
                        (Localization.Get("codex.info.target"), GetCardTargetTypeText(card.TargetType)),
                        (Localization.Get("codex.info.category"), GetCardCategoryText(card.Categories))
                    },
                    Discovered = true
                };
            })
            .ToList();
    }

    // 按卡牌类型选择显示哪些统计槽位：攻击类看伤害/命中/格挡/击杀，费/桃/酒/顺手牵羊
    // 各自看通用的 TotalValue/SpecialCount 槽位，避免非伤害牌强行显示"总伤害=0"。
    private static string BuildCardStatsText(CardType type)
    {
        var entry = CodexService.GetCard(type);
        if (entry == null)
        {
            return string.Empty;
        }

        var lines = new List<string> { "\n" + Localization.Get("codex.card.stats_header") };
        lines.Add(Localization.GetFmt("codex.card.stats.times_used_fmt", entry.TimesUsed));

        if (BattleRules.IsAnyAttackCard(type))
        {
            lines.Add(Localization.GetFmt("codex.card.stats.times_hit_fmt", entry.TimesHit));
            lines.Add(Localization.GetFmt("codex.card.stats.times_blocked_fmt", entry.TimesBlocked));
            lines.Add(Localization.GetFmt("codex.card.stats.total_damage_fmt", entry.TotalDamage));
            lines.Add(Localization.GetFmt("codex.card.stats.max_damage_fmt", entry.MaxSingleDamage));
            lines.Add(Localization.GetFmt("codex.card.stats.kill_count_fmt", entry.KillCount));
        }
        else if (type == CardType.Fee)
        {
            lines.Add(Localization.GetFmt("codex.card.stats.total_value_fee_fmt", (int)entry.TotalValue));
        }
        else if (type == CardType.Peach)
        {
            lines.Add(Localization.GetFmt("codex.card.stats.total_value_heal_fmt", (int)entry.TotalValue));
            lines.Add(Localization.GetFmt("codex.card.stats.special_revive_fmt", entry.SpecialCount));
        }
        else if (type == CardType.Wine)
        {
            lines.Add(Localization.GetFmt("codex.card.stats.special_revive_fmt", entry.SpecialCount));
        }
        else if (type == CardType.Steal)
        {
            lines.Add(Localization.GetFmt("codex.card.stats.special_success_fmt", entry.SpecialCount));
            lines.Add(Localization.GetFmt("codex.card.stats.total_value_steal_fmt", (int)entry.TotalValue));
        }

        return string.Join("\n", lines);
    }

    private static List<CodexEntryModel> BuildEquipmentEntries()
    {
        var isDev = DeveloperModeManager.IsDeveloperMode;
        return EquipmentDatabase.GetAllEquipments()
            .OrderBy(def => GetEquipmentRarityOrder(def.Rarity))
            .ThenBy(def => Localization.GetName(def))
            .Select(def =>
            {
                var codexEntry = CodexService.GetEquipment(def.Id);
                var seen = isDev || (codexEntry?.Seen ?? false);
                // 第一版简化方案：见面板33"如果不想增加复杂度"——真正获得后才解锁完整效果与统计，
                // 光是"看见"（Seen）只用来在未来扩展"基础信息"展示，本版本 UI 仍以 Obtained 为准。
                var obtained = isDev || (codexEntry?.Obtained ?? false);
                if (!seen && !obtained)
                {
                    return new CodexEntryModel
                    {
                        Id = $"equipment:{def.Id}",
                        Name = Localization.Get("codex.undiscovered"),
                        Category = CodexCategory.Equipment,
                        RarityText = GetEquipmentRarityName(def.Rarity),
                        Description = Localization.Get("codex.undiscovered.desc"),
                        DetailDescription = Localization.Get("codex.undiscovered.desc"),
                        Discovered = false
                    };
                }

                var info = new List<(string, string)>
                {
                    (Localization.Get("codex.info.equipment_code"), string.IsNullOrEmpty(def.AssetCode) ? "—" : def.AssetCode),
                    (Localization.Get("codex.info.acquire"), GetAcquisitionName(def.AcquisitionMethod)),
                    (Localization.Get("codex.info.tags"), string.Join(" / ", def.Tags)),
                    (Localization.Get("codex.info.source"), def.UnlockConditions.Count > 0 ? Localization.GetEquipmentUnlockConditions(def, " / ") : Localization.Get("codex.equip.source_generic"))
                };
                if (isDev)
                    info.Insert(1, (Localization.Get("codex.info.internal_id"), def.Id));
                var detail = obtained
                    ? BuildEquipmentDetailDescription(def) + BuildEquipmentStatsText(def.Id)
                    : Localization.Get("codex.equip.seen_not_obtained");
                return new CodexEntryModel
                {
                    Id = $"equipment:{def.Id}",
                    Name = Localization.GetName(def),
                    Category = CodexCategory.Equipment,
                    TypeText = GetEquipmentTypesText(def.Types),
                    RarityText = GetEquipmentRarityName(def.Rarity),
                    CodeText = def.AssetCode,
                    Description = Localization.GetEquipmentFlavorDescription(def),
                    DetailDescription = detail,
                    BasicInfo = info,
                    Discovered = true
                };
            })
            .ToList();
    }

    private static string BuildEquipmentStatsText(string equipmentId)
    {
        var entry = CodexService.GetEquipment(equipmentId);
        if (entry == null)
        {
            return string.Empty;
        }

        var stats = "\n" + Localization.Get("codex.equip.stats_header")
            + "\n" + Localization.GetFmt("codex.equip.stats.obtained_fmt", entry.ObtainedCount)
            + "\n" + Localization.GetFmt("codex.equip.stats.equipped_fmt", entry.EquippedCount)
            + "\n" + Localization.GetFmt("codex.equip.stats.sold_fmt", entry.SoldCount)
            + "\n" + Localization.GetFmt("codex.equip.stats.runs_fmt", entry.RunsObtainedIn.Count);

        if (equipmentId == EquipmentIds.TreasureDonkey)
        {
            stats += "\n" + Localization.GetFmt("codex.equip.stats.triggered_fmt", entry.TriggeredCount)
                + "\n" + Localization.GetFmt("codex.equip.stats.total_energy_fmt", entry.TotalEnergyProvided)
                + "\n" + Localization.GetFmt("codex.equip.stats.max_energy_fmt", entry.MaxEnergyProvidedInBattle);
        }

        return stats;
    }

    private static List<CodexEntryModel> BuildEnemyEntries(bool includeBoss)
    {
        var isDev = DeveloperModeManager.IsDeveloperMode;
        return EnemyDatabase.GetAllEnemies()
            .Where(def => includeBoss ? def.Type == EnemyType.Boss : def.Type != EnemyType.Boss)
            .OrderBy(def => GetEnemyTypeOrder(def.Type))
            .ThenBy(def => Localization.GetName(def))
            .Select(def =>
            {
                var discovered = isDev || (CodexService.GetEnemy(def.Id)?.Discovered ?? false);
                if (!discovered)
                {
                    return new CodexEntryModel
                    {
                        Id = $"enemy:{def.Id}",
                        Name = Localization.Get("codex.undiscovered"),
                        Category = includeBoss ? CodexCategory.Boss : CodexCategory.Enemy,
                        TypeText = GetEnemyTypeName(def.Type),
                        ChapterText = GetEnemyStageText(def),
                        Description = Localization.Get("codex.undiscovered.desc"),
                        DetailDescription = Localization.Get("codex.undiscovered.desc"),
                        Discovered = false
                    };
                }

                return new CodexEntryModel
                {
                    Id = $"enemy:{def.Id}",
                    Name = Localization.GetName(def),
                    Category = includeBoss ? CodexCategory.Boss : CodexCategory.Enemy,
                    TypeText = GetEnemyTypeName(def.Type),
                    ChapterText = GetEnemyStageText(def),
                    DangerReason = EnemyInfoFormatter.GetDangerReason(def.LoreId),
                    Description = BuildEnemyShortDescription(def),
                    DetailDescription = BuildEnemyDetailDescription(def) + BuildEnemyStatsText(def.Id, includeBoss),
                    BasicInfo = new List<(string, string)>
                    {
                        (Localization.Get("codex.info.hp"), def.MaxHP.ToString()),
                        (Localization.Get("codex.info.start_mp"), BattleRules.FormatMana(def.StartingResource)),
                        (Localization.Get("codex.info.faction"), GetFactionText(def.Tags)),
                        (Localization.Get("codex.info.skills"), JoinSkillNames(def.SkillIds)),
                        (Localization.Get("codex.info.equipment"), JoinEquipmentNames(def.EquipmentIds))
                    },
                    Discovered = true
                };
            })
            .ToList();
    }

    private static string BuildEnemyStatsText(string enemyId, bool isBoss)
    {
        var entry = CodexService.GetEnemy(enemyId);
        if (entry == null)
        {
            return string.Empty;
        }

        var text = "\n" + Localization.Get("codex.enemy.stats_header")
            + "\n" + Localization.GetFmt("codex.enemy.stats.encounter_fmt", entry.EncounterCount)
            + "\n" + Localization.GetFmt("codex.enemy.stats.defeated_fmt", entry.DefeatedCount)
            + "\n" + Localization.GetFmt("codex.enemy.stats.defeated_by_fmt", entry.PlayerDefeatedByCount)
            + "\n" + Localization.GetFmt("codex.enemy.stats.damage_dealt_fmt", entry.DamageDealtToEnemy)
            + "\n" + Localization.GetFmt("codex.enemy.stats.damage_taken_fmt", entry.DamageTakenFromEnemy);
        if (entry.FastestDefeatTurn != int.MaxValue)
        {
            text += "\n" + Localization.GetFmt("codex.enemy.stats.fastest_fmt", entry.FastestDefeatTurn);
        }

        if (isBoss)
        {
            var winRate = entry.ChallengeCount > 0 ? entry.DefeatedCount * 100 / entry.ChallengeCount : 0;
            text += "\n" + Localization.GetFmt("codex.boss.stats.challenge_fmt", entry.ChallengeCount)
                + "\n" + Localization.GetFmt("codex.boss.stats.win_rate_fmt", winRate);
            if (entry.DefeatedByCharacterIds.Count > 0)
            {
                var names = entry.DefeatedByCharacterIds
                    .Select(id => CharacterDatabase.GetCharacter(id)?.Name ?? id);
                text += "\n" + Localization.Get("codex.boss.stats.defeated_by_characters_header")
                    + "\n" + string.Join("、", names);
            }
        }

        return text;
    }

    private static List<CodexEntryModel> BuildBuffEntries()
    {
        var entries = new List<CodexEntryModel>();

        foreach (var buff in RunBuffDatabase.GetAll().OrderBy(buff => Localization.GetName(buff)))
        {
            entries.Add(new CodexEntryModel
            {
                Id = $"runbuff:{buff.BuffId}",
                Name = Localization.GetName(buff),
                Category = CodexCategory.Buff,
                TypeText = Localization.GetFmt("codex.buff.type_fmt", GetRunBuffTypeName(buff.Type)),
                Description = Localization.GetDescription(buff),
                DetailDescription = BuildRunBuffDetailDescription(buff),
                BasicInfo = new List<(string, string)>
                {
                    (Localization.Get("codex.info.duration"), buff.DefaultRemainingBattles < 0 ? Localization.Get("codex.buff.permanent") : Localization.GetFmt("codex.buff.duration_fmt", buff.DefaultRemainingBattles)),
                    (Localization.Get("codex.info.stackable"), buff.Stackable ? Localization.Get("codex.buff.yes") : Localization.Get("codex.buff.no"))
                }
            });
        }

        entries.AddRange(BuildBattleBuffEntries());
        return entries.OrderBy(entry => entry.Name).ToList();
    }

    private static List<CodexEntryModel> BuildBattleBuffEntries()
    {
        return new List<CodexEntryModel>
        {
            CreateBattleBuffEntry("battlebuff:peach_shield", Localization.Get("codex.battlebuff.peach_shield.name"), Localization.Get("codex.type.battle_buff"), Localization.Get("codex.battlebuff.peach_shield.desc"), Localization.Get("codex.battlebuff.peach_shield.detail")),
            CreateBattleBuffEntry("battlebuff:wine_shield", Localization.Get("codex.battlebuff.wine_shield.name"), Localization.Get("codex.type.battle_buff"), Localization.Get("codex.battlebuff.wine_shield.desc"), Localization.Get("codex.battlebuff.wine_shield.detail")),
            CreateBattleBuffEntry("battlebuff:dodge_defense", Localization.Get("codex.battlebuff.dodge_defense.name"), Localization.Get("codex.type.battle_buff"), Localization.Get("codex.battlebuff.dodge_defense.desc"), Localization.Get("codex.battlebuff.dodge_defense.detail")),
            CreateBattleBuffEntry("battlebuff:counter_defense", Localization.Get("codex.battlebuff.counter_defense.name"), Localization.Get("codex.type.battle_buff"), Localization.Get("codex.battlebuff.counter_defense.desc"), Localization.Get("codex.battlebuff.counter_defense.detail")),
            CreateBattleBuffEntry("battlebuff:frozen", Localization.Get("codex.battlebuff.frozen.name"), Localization.Get("codex.type.battle_buff"), Localization.Get("codex.battlebuff.frozen.desc"), Localization.Get("codex.battlebuff.frozen.detail")),
            CreateBattleBuffEntry("battlebuff:darkness", Localization.Get("codex.battlebuff.darkness.name"), Localization.Get("codex.type.run_buff"), Localization.Get("codex.battlebuff.darkness.desc"), Localization.Get("codex.battlebuff.darkness.detail"))
        };
    }

    private static List<CodexEntryModel> BuildEventEntries()
    {
        var isDev = DeveloperModeManager.IsDeveloperMode;
        return EventDatabase.GetAllEvents()
            .OrderBy(ev => GetEventRarityOrder(ev.Rarity))
            .ThenBy(ev => Localization.GetName(ev))
            .Select(ev =>
            {
                var discovered = isDev || (CodexService.GetEvent(ev.Id)?.Discovered ?? false);
                if (!discovered)
                {
                    return new CodexEntryModel
                    {
                        Id = $"event:{ev.Id}",
                        Name = Localization.Get("codex.undiscovered"),
                        Category = CodexCategory.Event,
                        TypeText = GetCategoryName(ev.Category),
                        RarityText = GetEventRarityName(ev.Rarity),
                        ChapterText = GetEventChapterText(ev),
                        Description = Localization.Get("codex.undiscovered.desc"),
                        DetailDescription = Localization.Get("codex.undiscovered.desc"),
                        Discovered = false
                    };
                }

                return new CodexEntryModel
                {
                    Id = $"event:{ev.Id}",
                    Name = Localization.GetName(ev),
                    Category = CodexCategory.Event,
                    TypeText = GetCategoryName(ev.Category),
                    RarityText = GetEventRarityName(ev.Rarity),
                    ChapterText = GetEventChapterText(ev),
                    Description = FirstLine(Localization.GetDescription(ev)),
                    DetailDescription = BuildEventDetailDescription(ev, isDev) + BuildEventStatsText(ev),
                    BasicInfo = new List<(string, string)>
                    {
                        (Localization.Get("codex.info.event_code"), string.IsNullOrEmpty(ev.AssetCode) ? "—" : ev.AssetCode),
                        (Localization.Get("codex.info.duration"), GetRepeatTypeName(ev.RepeatType)),
                        (Localization.Get("codex.info.tags"), ev.EventTags.Count > 0 ? string.Join(" / ", ev.EventTags) : Localization.Get("codex.none"))
                    },
                    Discovered = true
                };
            })
            .ToList();
    }

    private static string BuildEventStatsText(EventData ev)
    {
        var entry = CodexService.GetEvent(ev.Id);
        if (entry == null)
        {
            return string.Empty;
        }

        var text = "\n" + Localization.Get("codex.event.stats_header")
            + "\n" + Localization.GetFmt("codex.event.stats.encounter_fmt", entry.EncounterCount)
            + "\n" + Localization.GetFmt("codex.event.stats.discovered_options_fmt", entry.DiscoveredOptionIndexes.Count, ev.Options.Count);

        if (entry.ChoiceCounts.Count > 0)
        {
            var lines = new List<string> { Localization.Get("codex.event.stats.choices_header") };
            for (var i = 0; i < ev.Options.Count; i++)
            {
                if (!entry.ChoiceCounts.TryGetValue(i, out var count) || count <= 0)
                {
                    continue;
                }
                lines.Add($"  {ev.Options[i].DisplayName}：{count}");
            }
            text += "\n" + string.Join("\n", lines);
        }

        return text;
    }

    private static List<CodexEntryModel> BuildRouteEntries()
    {
        return new List<CodexEntryModel>
        {
            new()
            {
                Id = "route:city",
                Name = Localization.Get("codex.route.default.name"),
                Category = CodexCategory.Route,
                TypeText = Localization.Get("codex.type.route"),
                ChapterText = Localization.Get("codex.route.default.chapter"),
                Description = Localization.Get("codex.route.default.desc"),
                DetailDescription = Localization.Get("codex.route.default.detail"),
                BasicInfo = new List<(string, string)>
                {
                    (Localization.Get("codex.route.detail.internal_id"), ChapterRoute.Default.ToString()),
                    (Localization.Get("codex.route.detail.positioning_label"), Localization.Get("codex.route.default.positioning"))
                }
            },
            new()
            {
                Id = "route:sewer",
                Name = Localization.Get("codex.route.sewer.name"),
                Category = CodexCategory.Route,
                TypeText = Localization.Get("codex.type.route"),
                ChapterText = Localization.Get("codex.route.sewer.chapter"),
                Description = Localization.Get("codex.route.sewer.desc"),
                DetailDescription = Localization.Get("codex.route.sewer.detail"),
                BasicInfo = new List<(string, string)>
                {
                    (Localization.Get("codex.route.detail.internal_id"), ChapterRoute.Sewer.ToString()),
                    (Localization.Get("codex.route.detail.positioning_label"), Localization.Get("codex.route.sewer.positioning"))
                }
            },
            new()
            {
                Id = "route:imperial",
                Name = Localization.Get("codex.route.imperial.name"),
                Category = CodexCategory.Route,
                TypeText = Localization.Get("codex.type.route"),
                ChapterText = Localization.Get("codex.route.imperial.chapter"),
                Description = Localization.Get("codex.route.imperial.desc"),
                DetailDescription = Localization.Get("codex.route.imperial.detail"),
                BasicInfo = new List<(string, string)>
                {
                    (Localization.Get("codex.route.detail.internal_id"), ChapterRoute.Imperial.ToString()),
                    (Localization.Get("codex.route.detail.positioning_label"), Localization.Get("codex.route.imperial.positioning"))
                }
            }
        };
    }

    private static CodexEntryModel CreateBattleBuffEntry(string id, string name, string typeText, string description, string detail)
    {
        return new CodexEntryModel
        {
            Id = id,
            Name = name,
            Category = CodexCategory.Buff,
            TypeText = typeText,
            Description = description,
            DetailDescription = detail,
            BasicInfo = new List<(string, string)>
            {
                (Localization.Get("codex.detail.source_label"), Localization.Get("codex.battlebuff.detail.source_value")),
                (Localization.Get("codex.detail.duration_label"), Localization.Get("codex.battlebuff.detail.duration_value"))
            }
        };
    }

    private static string BuildListMeta(CodexEntryModel entry)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(entry.RarityText))
        {
            parts.Add(entry.RarityText);
        }
        if (!string.IsNullOrEmpty(entry.CostText))
        {
            parts.Add(entry.CostText);
        }
        if (!string.IsNullOrEmpty(entry.TypeText))
        {
            parts.Add(entry.TypeText);
        }
        return string.Join("  ·  ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string BuildCardShortDescription(Card card)
    {
        return card.Type switch
        {
            CardType.FireKill => Localization.Get("codex.card.fire_kill.short"),
            CardType.ThunderKill => Localization.Get("codex.card.thunder_kill.short"),
            CardType.FireThunderKill => Localization.Get("codex.card.fire_thunder_kill.short"),
            CardType.SureKill => Localization.Get("codex.card.sure_kill.short"),
            CardType.IceKill => Localization.Get("codex.card.ice_kill.short"),
            CardType.CelestialImpact => Localization.Get("codex.card.celestial_impact.short"),
            CardType.Dodge => Localization.Get("codex.card.dodge.short"),
            CardType.Peach => Localization.Get("codex.card.peach.short"),
            CardType.Wine => Localization.Get("codex.card.wine.short"),
            CardType.Steal => Localization.Get("codex.card.steal.short"),
            CardType.Unassailable => Localization.Get("codex.card.unassailable.short"),
            CardType.Fee => Localization.Get("codex.card.fee.short"),
            CardType.ArrowBarrage => Localization.Get("codex.card.arrow_barrage.short"),
            CardType.NanmanInvasion => Localization.Get("codex.card.nanman_invasion.short"),
            CardType.Guanxing => Localization.Get("codex.card.guanxing.short"),
            _ => card.Description + "。"
        };
    }

    private static string BuildCardDetailDescription(Card card, string matchupText)
    {
        var lines = new List<string>
        {
            Localization.GetFmt("codex.card.detail.desc_label", card.Description),
            Localization.GetFmt("codex.card.detail.target_label", GetCardTargetTypeText(card.TargetType)),
            Localization.GetFmt("codex.card.detail.cat_label", GetCardCategoryText(card.Categories))
        };

        if (card.IsAttackAction)
        {
            lines.Add(card.AttributeTraitText);
        }

        if (!string.IsNullOrWhiteSpace(matchupText))
        {
            lines.Add(Localization.Get("codex.card.detail.matchup_label"));
            lines.Add(matchupText);
        }

        return string.Join("\n\n", lines);
    }

    private static string BuildEquipmentDetailDescription(EquipmentDefinition def)
    {
        if (def.Effects.Count == 0)
        {
            return Localization.Get("codex.none");
        }

        var effectLines = Enumerable.Range(0, def.Effects.Count)
            .Select(index => $"• {Localization.GetEquipmentEffectDescription(def, index)}");
        return Localization.Get("codex.equip.detail.effects_header") + "\n" + string.Join("\n", effectLines);
    }

    private static string BuildEnemyShortDescription(EnemyDefinition def)
    {
        var danger = EnemyInfoFormatter.GetDangerReason(def.LoreId);
        return string.IsNullOrEmpty(danger)
            ? Localization.GetFmt("codex.enemy.type_enemy_fmt", GetEnemyTypeName(def.Type))
            : danger;
    }

    private static string BuildEnemyDetailDescription(EnemyDefinition def)
    {
        var parts = new List<string>
        {
            Localization.GetFmt("codex.enemy.detail.stage_fmt", GetEnemyStageText(def))
        };

        if (def.SkillIds.Count > 0)
        {
            parts.Add(Localization.Get("codex.enemy.detail.skills_header") + "\n" + string.Join("\n", def.SkillIds.Select(id => { var s = SkillDatabase.GetSkill(id); return $"• {(s is null ? id : Localization.GetName(s))}"; })));
        }

        if (def.EquipmentIds.Count > 0)
        {
            parts.Add(Localization.Get("codex.enemy.detail.equip_header") + "\n" + string.Join("\n", def.EquipmentIds.Select(id => { var e = EquipmentDatabase.GetEquipment(id); return $"• {(e is null ? id : Localization.GetName(e))}"; })));
        }

        var rewardLines = new List<string>();
        if (def.Reward.GrantsGold)
        {
            if (def.Reward.GoldOverride.HasValue)
            {
                rewardLines.Add(Localization.GetFmt("codex.enemy.detail.gold_fmt", def.Reward.GoldOverride.Value));
            }
            else
            {
                var (goldMin, goldMax) = EnemyRewardConfig.GetGoldRange(def.Type);
                rewardLines.Add(Localization.GetFmt("codex.enemy.detail.gold_range_fmt", goldMin, goldMax));
            }
        }
        foreach (var equipmentId in def.Reward.EquipmentRewards)
        {
            rewardLines.Add(Localization.GetFmt("codex.enemy.detail.equip_reward_fmt", (EquipmentDatabase.GetEquipment(equipmentId) is { } eq1 ? Localization.GetName(eq1) : equipmentId)));
        }
        foreach (var drop in def.Reward.EquipmentDrops)
        {
            rewardLines.Add(Localization.GetFmt("codex.enemy.detail.drop_fmt", (EquipmentDatabase.GetEquipment(drop.EquipmentId) is { } eq2 ? Localization.GetName(eq2) : drop.EquipmentId), (int)(drop.Probability * 100)));
        }
        var randomPool = def.Reward.RandomEquipmentPool;
        if (randomPool.Count > 0)
        {
            var names = string.Join(" / ", randomPool.Select(id => EquipmentDatabase.GetEquipment(id) is { } eq3 ? Localization.GetName(eq3) : id));
            rewardLines.Add(Localization.GetFmt("codex.enemy.detail.pool_fmt", names));
        }

        if (rewardLines.Count > 0)
        {
            parts.Add(Localization.Get("codex.enemy.detail.reward_header") + "\n" + string.Join("\n", rewardLines));
        }

        return string.Join("\n\n", parts);
    }

    private static string BuildRunBuffDetailDescription(RunBuffDefinition buff)
    {
        var parts = new List<string> { Localization.GetDescription(buff) };
        if (buff.Effects.Count > 0)
        {
            parts.Add(Localization.Get("codex.buff.triggers_header") + "\n" + string.Join("\n", buff.Effects.Select(effect =>
                Localization.GetFmt("codex.buff.effect_fmt", GetRunBuffTimingName(effect.Timing), GetRunBuffEffectTypeName(effect.Type), effect.Value))));
        }
        return string.Join("\n\n", parts);
    }

    private static string BuildEventDetailDescription(EventData eventData, bool isDev)
    {
        var parts = new List<string> { Localization.GetDescription(eventData) };

        if (eventData.Options.Count > 0)
        {
            var codexEntry = CodexService.GetEvent(eventData.Id);
            var optionLines = new List<string>();
            for (var i = 0; i < eventData.Options.Count; i++)
            {
                var option = eventData.Options[i];
                // 阵营/装备/角色专属选项、隐藏结果选项：在玩家真正选到过之前不能提前泄露，
                // 只显示？？？（不区分"选项本身是否隐藏可见性条件"，统一按"是否选过"判断，
                // 简化实现——条件满足但从没选过的选项同样先保持？？？，不会因为条件恰好
                // 满足就提前剧透具体内容）。
                var optionDiscovered = isDev || (codexEntry?.DiscoveredOptionIndexes.Contains(i) ?? false);
                if (!optionDiscovered)
                {
                    optionLines.Add("• " + Localization.Get("codex.event.hidden_option"));
                    continue;
                }

                optionLines.Add($"• {option.DisplayName}");
                if (!string.IsNullOrWhiteSpace(option.DisplayDescription))
                {
                    optionLines.Add($"  {option.DisplayDescription}");
                }
            }
            parts.Add(Localization.Get("codex.event.options_header") + "\n" + string.Join("\n", optionLines));
        }

        return string.Join("\n\n", parts);
    }

    private static string FirstLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length > 0 ? lines[0] : text.Trim();
    }

    private static int GetEquipmentRarityOrder(EquipmentRarity rarity) => rarity switch
    {
        EquipmentRarity.Common => 0,
        EquipmentRarity.Rare => 1,
        EquipmentRarity.Epic => 2,
        EquipmentRarity.Legendary => 3,
        _ => 99
    };

    private static int GetEnemyTypeOrder(EnemyType type) => type switch
    {
        EnemyType.Normal => 0,
        EnemyType.Elite => 1,
        EnemyType.Boss => 2,
        _ => 99
    };

    private static int GetEventRarityOrder(EventRarity rarity) => rarity switch
    {
        EventRarity.Common => 0,
        EventRarity.Rare => 1,
        EventRarity.Epic => 2,
        EventRarity.Legendary => 3,
        _ => 99
    };

    private static string GetEquipmentTypesText(IReadOnlyList<EquipmentType> types)
    {
        return string.Join(" / ", types.Select(type => type switch
        {
            EquipmentType.Buff => Localization.Get("codex.equip.type.buff"),
            EquipmentType.Weapon => Localization.Get("codex.equip.type.weapon"),
            EquipmentType.Armor => Localization.Get("codex.equip.type.armor"),
            EquipmentType.Vehicle => Localization.Get("codex.equip.type.vehicle"),
            EquipmentType.Mount => Localization.Get("codex.equip.type.mount"),
            EquipmentType.Defense => Localization.Get("codex.equip.type.defense"),
            EquipmentType.Attack => Localization.Get("codex.equip.type.attack"),
            EquipmentType.Accessory => Localization.Get("codex.equip.type.accessory"),
            _ => type.ToString()
        }));
    }

    private static string GetEquipmentRarityName(EquipmentRarity rarity) => rarity switch
    {
        EquipmentRarity.Common => Localization.Get("equip.rarity.common"),
        EquipmentRarity.Rare => Localization.Get("equip.rarity.rare"),
        EquipmentRarity.Epic => Localization.Get("equip.rarity.epic"),
        EquipmentRarity.Legendary => Localization.Get("equip.rarity.legendary"),
        _ => rarity.ToString()
    };

    private static string GetAcquisitionName(EquipmentAcquisitionMethod method) => method switch
    {
        EquipmentAcquisitionMethod.Reward => Localization.Get("codex.acquire.reward"),
        EquipmentAcquisitionMethod.Event => Localization.Get("codex.acquire.event"),
        EquipmentAcquisitionMethod.Shop => Localization.Get("codex.acquire.shop"),
        EquipmentAcquisitionMethod.Debug => Localization.Get("codex.acquire.debug"),
        _ => Localization.Get("codex.acquire.unknown")
    };

    private static string GetEnemyTypeName(EnemyType type) => type switch
    {
        EnemyType.Normal => Localization.Get("codex.enemy.type.normal"),
        EnemyType.Elite => Localization.Get("codex.enemy.type.elite"),
        EnemyType.Boss => Localization.Get("codex.enemy.type.boss"),
        _ => type.ToString()
    };

    private static string GetEnemyStageText(EnemyDefinition def)
    {
        if (def.StageRange.MinStage <= 0 && def.StageRange.MaxStage <= 0)
        {
            return Localization.Get("codex.enemy.stage.event_only");
        }

        if (def.StageRange.MinStage == def.StageRange.MaxStage)
        {
            return Localization.GetFmt("codex.enemy.stage.single_fmt", def.StageRange.MinStage);
        }

        return Localization.GetFmt("codex.enemy.stage.range_fmt", def.StageRange.MinStage, def.StageRange.MaxStage);
    }

    private static string GetFactionText(List<EnemyTag> tags)
    {
        if (tags.Contains(EnemyTag.Shu)) return Localization.Get("faction.shu");
        if (tags.Contains(EnemyTag.Wei)) return Localization.Get("faction.wei");
        if (tags.Contains(EnemyTag.Wu)) return Localization.Get("faction.wu");
        if (tags.Contains(EnemyTag.Qun)) return Localization.Get("faction.qun");
        return Localization.Get("faction.unknown");
    }

    private static string JoinSkillNames(List<string> skillIds)
    {
        if (skillIds.Count == 0)
        {
            return Localization.Get("codex.none");
        }

        return string.Join("、", skillIds.Select(id => SkillDatabase.GetSkill(id) is { } s ? Localization.GetName(s) : id));
    }

    private static string JoinEquipmentNames(List<string> equipmentIds)
    {
        if (equipmentIds.Count == 0)
        {
            return Localization.Get("codex.none");
        }

        return string.Join("、", equipmentIds.Select(id => EquipmentDatabase.GetEquipment(id) is { } e ? Localization.GetName(e) : id));
    }

    private static string GetRunBuffTypeName(RunBuffType type) => type switch
    {
        RunBuffType.Blessing => Localization.Get("runbuff.type.blessing"),
        RunBuffType.Curse => Localization.Get("runbuff.type.curse"),
        RunBuffType.ChapterVariant => Localization.Get("runbuff.type.chapter"),
        RunBuffType.RouteVariant => Localization.Get("runbuff.type.route"),
        RunBuffType.BossEffect => Localization.Get("runbuff.type.boss"),
        RunBuffType.Event => Localization.Get("runbuff.type.event"),
        _ => type.ToString()
    };

    private static string GetRunBuffTimingName(RunBuffTriggerTiming timing) => timing switch
    {
        RunBuffTriggerTiming.OnBattleStart => Localization.Get("codex.buff.timing.battle_start"),
        RunBuffTriggerTiming.OnBattleEnd => Localization.Get("codex.buff.timing.battle_end"),
        RunBuffTriggerTiming.OnChapterStart => Localization.Get("codex.buff.timing.chapter_start"),
        RunBuffTriggerTiming.OnChapterEnd => Localization.Get("codex.buff.timing.chapter_end"),
        RunBuffTriggerTiming.OnDamageCalculate => Localization.Get("codex.buff.timing.damage_calc"),
        RunBuffTriggerTiming.OnRewardGenerate => Localization.Get("codex.buff.timing.reward_gen"),
        _ => timing.ToString()
    };

    private static string GetRunBuffEffectTypeName(RunBuffEffectType type) => type switch
    {
        RunBuffEffectType.EnemyMaxHpPercentBonus => Localization.Get("codex.buff.effect.enemy_hp_pct"),
        RunBuffEffectType.PlayerDamageTakenMultiplierBonus => Localization.Get("codex.buff.effect.player_dmg_mult"),
        _ => type.ToString()
    };

    private static string GetRepeatTypeName(EventRepeatType repeatType) => repeatType switch
    {
        EventRepeatType.Repeatable => Localization.Get("codex.event.repeat.repeatable"),
        EventRepeatType.RunOnce => Localization.Get("codex.event.repeat.run_once"),
        EventRepeatType.PermanentOnce => Localization.Get("codex.event.repeat.perm_once"),
        EventRepeatType.OncePerChapter => Localization.Get("codex.event.repeat.once_per_chap"),
        _ => repeatType.ToString()
    };

    private static string GetEventRarityName(EventRarity rarity) => rarity switch
    {
        EventRarity.Common => Localization.Get("equip.rarity.common"),
        EventRarity.Rare => Localization.Get("equip.rarity.rare"),
        EventRarity.Epic => Localization.Get("equip.rarity.epic"),
        EventRarity.Legendary => Localization.Get("equip.rarity.legendary"),
        _ => rarity.ToString()
    };

    private static string GetEventChapterText(EventData eventData)
    {
        var chapters = new List<int>();
        foreach (var condition in eventData.Conditions)
        {
            if (condition.Type != EventConditionType.CurrentChapter)
            {
                continue;
            }

            switch (condition.Comparison)
            {
                case EventComparison.Equal:
                    chapters.Add(condition.IntValue);
                    break;
                case EventComparison.GreaterOrEqual:
                    for (var chapter = condition.IntValue; chapter <= GameManager.TotalChapters; chapter++)
                    {
                        chapters.Add(chapter);
                    }
                    break;
            }
        }

        if (chapters.Count == 0)
        {
            return Localization.Get("codex.event.chapter.universal");
        }

        return string.Join(" / ", chapters.Distinct().OrderBy(chapter => chapter).Select(GameManager.GetChapterDisplayName));
    }

    private static string GetCategoryName(EventCategory category) => category switch
    {
        EventCategory.Common => Localization.Get("codex.event.cat.common"),
        EventCategory.CharacterExclusive => Localization.Get("codex.event.cat.char"),
        EventCategory.Conditional => Localization.Get("codex.event.cat.conditional"),
        EventCategory.Shop => Localization.Get("codex.event.cat.shop"),
        EventCategory.Story => Localization.Get("codex.event.cat.story"),
        EventCategory.Special => Localization.Get("codex.event.cat.special"),
        _ => category.ToString()
    };

    private static string GetCardSubTypeText(CardSubType subType) => subType switch
    {
        CardSubType.Kill => Localization.Get("codex.card.subtype.kill"),
        CardSubType.FireKill => Localization.Get("codex.card.subtype.fire_kill"),
        CardSubType.ThunderKill => Localization.Get("codex.card.subtype.thunder_kill"),
        CardSubType.FireThunderKill => Localization.Get("codex.card.subtype.fire_thunder_kill"),
        CardSubType.DirectSha => Localization.Get("codex.card.subtype.sure_kill"),
        CardSubType.IceKill => Localization.Get("codex.card.subtype.ice_kill"),
        CardSubType.CelestialImpact => Localization.Get("codex.card.subtype.celestial"),
        CardSubType.ArrowBarrage => Localization.Get("codex.card.subtype.barrage"),
        CardSubType.NanmanInvasion => Localization.Get("codex.card.subtype.invasion"),
        CardSubType.Dodge => Localization.Get("codex.card.subtype.dodge"),
        CardSubType.Peach => Localization.Get("codex.card.subtype.peach"),
        CardSubType.Wine => Localization.Get("codex.card.subtype.wine"),
        CardSubType.Steal => Localization.Get("codex.card.subtype.steal"),
        CardSubType.Unassailable => Localization.Get("codex.card.subtype.unassailable"),
        CardSubType.Fee => Localization.Get("codex.card.subtype.fee"),
        CardSubType.Guanxing => Localization.Get("codex.card.subtype.guanxing"),
        CardSubType.JiGuActivate => Localization.Get("codex.card.subtype.jigu"),
        _ => subType.ToString()
    };

    private static string GetCardTargetTypeText(CardTargetType targetType) => targetType switch
    {
        CardTargetType.NonTargeted => Localization.Get("codex.card.target.none"),
        CardTargetType.SelfTarget => Localization.Get("codex.card.target.self"),
        CardTargetType.Targeted => Localization.Get("codex.card.target.targeted"),
        _ => targetType.ToString()
    };

    private static string GetCardCategoryText(CardCategory category)
    {
        var parts = new List<string>();
        if (category.HasFlag(CardCategory.Attack)) parts.Add(Localization.Get("codex.card.cat.attack"));
        if (category.HasFlag(CardCategory.Defense)) parts.Add(Localization.Get("codex.card.cat.defense"));
        if (category.HasFlag(CardCategory.Recovery)) parts.Add(Localization.Get("codex.card.cat.recovery"));
        if (category.HasFlag(CardCategory.Trick)) parts.Add(Localization.Get("codex.card.cat.trick"));
        if (category.HasFlag(CardCategory.Resource)) parts.Add(Localization.Get("codex.card.cat.resource"));
        if (category.HasFlag(CardCategory.Enhancement)) parts.Add(Localization.Get("codex.card.cat.enhancement"));
        return parts.Count == 0 ? Localization.Get("codex.card.cat.none") : string.Join(" / ", parts);
    }

    private static PanelContainer CreatePanel(Vector2 minimumSize)
    {
        var panel = new PanelContainer();
        if (minimumSize != Vector2.Zero)
        {
            panel.CustomMinimumSize = minimumSize;
        }
        ApplyPanelStyle(panel);
        return panel;
    }

    private static MarginContainer CreateMargin(Control parent, int left, int top, int right, int bottom)
    {
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", left);
        margin.AddThemeConstantOverride("margin_top", top);
        margin.AddThemeConstantOverride("margin_right", right);
        margin.AddThemeConstantOverride("margin_bottom", bottom);
        parent.AddChild(margin);
        return margin;
    }

    private static PanelContainer CreateSectionPanel(out Label titleLabel, out VBoxContainer contentBox)
    {
        var wrapper = new PanelContainer();
        ApplySubPanelStyle(wrapper);
        var margin = CreateMargin(wrapper, 14, 14, 14, 14);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);

        titleLabel = new Label();
        titleLabel.AddThemeFontSizeOverride("font_size", 22);
        box.AddChild(titleLabel);

        contentBox = box;
        return wrapper;
    }

    private static Label MakeInfoLabel()
    {
        var label = new Label();
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", new Color(0.78f, 0.82f, 0.90f));
        return label;
    }

    private static void ApplyPanelStyle(PanelContainer panel)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.13f, 0.16f),
            BorderColor = new Color(0.26f, 0.29f, 0.36f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        };
        panel.AddThemeStyleboxOverride("panel", style);
    }

    private static void ApplySubPanelStyle(PanelContainer panel)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.11f, 0.14f),
            BorderColor = new Color(0.22f, 0.25f, 0.30f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        };
        panel.AddThemeStyleboxOverride("panel", style);
    }

    private static void ApplyImagePanelStyle(PanelContainer panel)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.10f, 0.11f, 0.13f),
            BorderColor = new Color(0.30f, 0.33f, 0.40f),
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        };
        panel.AddThemeStyleboxOverride("panel", style);
    }

    private static void ApplySmallIconStyle(PanelContainer panel)
    {
        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.16f, 0.17f, 0.21f),
            BorderColor = new Color(0.30f, 0.33f, 0.40f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        };
        panel.AddThemeStyleboxOverride("panel", style);
    }

    private static StyleBoxFlat CreateListEntryStyle(bool highlighted)
    {
        return new StyleBoxFlat
        {
            BgColor = highlighted ? new Color(0.18f, 0.20f, 0.26f) : new Color(0.13f, 0.14f, 0.18f),
            BorderColor = highlighted ? new Color(0.82f, 0.70f, 0.38f) : new Color(0.24f, 0.27f, 0.34f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12,
            ContentMarginTop = 10,
            ContentMarginRight = 12,
            ContentMarginBottom = 10
        };
    }
}
