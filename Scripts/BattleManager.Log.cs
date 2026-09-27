//////////////////////////////////////////////////////////
// 文件：Scripts/BattleManager.Log.cs
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
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Core System 的公开类：BattleManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class BattleManager
{
    private void ShowBattleLogWindow()
    {
        if (_battleLogWindow != null && !_battleLogWindow.IsQueuedForDeletion())
        {
            RebuildBattleLogView();
            _battleLogWindow.PopupCentered();
            return;
        }

        var window = new Window
        {
            Title = Localization.Get("battlelog.window.title"),
            Size = new Vector2I(1040, 760),
            Exclusive = false
        };
        _battleLogWindow = window;
        AddChild(window);

        var windowPanel = new PanelContainer();
        windowPanel.SetAnchorsPreset(LayoutPreset.FullRect);
        windowPanel.AddThemeStyleboxOverride("panel", UIResourceDatabase.CreateBattleLogFrameStyle());
        window.AddChild(windowPanel);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_top", 18);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_bottom", 18);
        windowPanel.AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var tools = new HBoxContainer();
        tools.AddThemeConstantOverride("separation", 10);
        root.AddChild(tools);

        _battleLogSearchInput = new LineEdit
        {
            PlaceholderText = Localization.Get("battlelog.search.placeholder"),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 42)
        };
        _battleLogSearchInput.TextChanged += _ => RebuildBattleLogView();
        _battleLogSearchInput.AddThemeStyleboxOverride("normal", UIResourceDatabase.CreateCommonPanelStyle(8));
        _battleLogSearchInput.AddThemeStyleboxOverride("focus", UIResourceDatabase.CreateCommonPanelStyle(8));
        tools.AddChild(_battleLogSearchInput);

        var exportButton = new Button
        {
            Text = Localization.Get("battlelog.export"),
            CustomMinimumSize = new Vector2(130, 42)
        };
        UIResourceDatabase.ApplyCommonButton(exportButton);
        exportButton.Pressed += ExportBattleLog;
        tools.AddChild(exportButton);

        var clearButton = new Button
        {
            Text = Localization.Get("battlelog.clear"),
            CustomMinimumSize = new Vector2(130, 42)
        };
        UIResourceDatabase.ApplyCommonButton(clearButton);
        clearButton.Pressed += () =>
        {
            _battleLogManager.Clear();
            RebuildBattleLogView();
            UpdateRecentReport();
        };
        tools.AddChild(clearButton);

        var expandButton = new Button
        {
            Text = Localization.Get("battlelog.tree.expand_all"),
            CustomMinimumSize = new Vector2(105, 42)
        };
        UIResourceDatabase.ApplyCommonButton(expandButton);
        expandButton.Pressed += () => SetAllBattleLogTreeCollapsed(false);
        tools.AddChild(expandButton);

        var collapseButton = new Button
        {
            Text = Localization.Get("battlelog.tree.collapse_all"),
            CustomMinimumSize = new Vector2(105, 42)
        };
        UIResourceDatabase.ApplyCommonButton(collapseButton);
        collapseButton.Pressed += () => SetAllBattleLogTreeCollapsed(true);
        tools.AddChild(collapseButton);

        var modeRow = new HBoxContainer();
        modeRow.AddThemeConstantOverride("separation", 8);
        root.AddChild(modeRow);
        AddBattleLogModeButton(modeRow, false, "battlelog.view.history");
        if (DeveloperModeManager.IsDeveloperMode)
        {
            AddBattleLogModeButton(modeRow, true, "battlelog.view.developer");
        }

        var contentSplit = new HSplitContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SplitOffsets = new[] { 250 }
        };
        root.AddChild(contentSplit);

        _battleLogTree = new Tree
        {
            CustomMinimumSize = new Vector2(250, 640),
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HideRoot = false
        };
        _battleLogTree.ItemSelected += OnBattleLogTreeSelected;
        contentSplit.AddChild(_battleLogTree);

        var reportArea = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        reportArea.AddThemeConstantOverride("separation", 8);
        contentSplit.AddChild(reportArea);

        _battleLogFilterRow = new HFlowContainer();
        _battleLogFilterRow.AddThemeConstantOverride("separation", 8);
        reportArea.AddChild(_battleLogFilterRow);
        reportArea.AddChild(UIResourceDatabase.CreateDivider());

        _battleLogScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(700, 600)
        };
        reportArea.AddChild(_battleLogScroll);

        _battleLogList = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _battleLogList.AddThemeConstantOverride("separation", 8);
        _battleLogScroll.AddChild(_battleLogList);
        UIResourceDatabase.ApplyScrollBar(_battleLogScroll);

        window.CloseRequested += window.Hide;
        RebuildBattleLogView();
        window.PopupCentered();
    }

    private void RebuildBattleLogView()
    {
        NormalizeBattleLogSelection();
        RebuildBattleLogTree();
        RebuildBattleLogFilters();
        RebuildBattleLogList();
    }

    private void AddBattleLogModeButton(HBoxContainer row, bool developerView, string textKey)
    {
        var button = new Button
        {
            Text = Localization.Get(textKey),
            ToggleMode = true,
            ButtonPressed = _battleLogDeveloperView == developerView,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(190, 38)
        };
        UIResourceDatabase.ApplyBattleLogCategoryButton(button);
        button.Pressed += () =>
        {
            _battleLogDeveloperView = developerView && DeveloperModeManager.IsDeveloperMode;
            foreach (var child in row.GetChildren())
            {
                if (child is Button modeButton)
                {
                    modeButton.ButtonPressed = ReferenceEquals(modeButton, button);
                }
            }
            RebuildBattleLogView();
        };
        row.AddChild(button);
    }

    private void RebuildBattleLogTree()
    {
        if (_battleLogTree == null || _battleLogTree.IsQueuedForDeletion())
        {
            return;
        }

        _battleLogTree.Clear();
        var service = _battleLogManager.Service;
        var root = _battleLogTree.CreateItem();
        root.SetText(0, Localization.Get("battlelog.window.title"));
        root.SetMetadata(0, "all");
        root.Collapsed = false;

        foreach (var run in service.Runs.Reverse())
        {
            var runItem = _battleLogTree.CreateItem(root);
            runItem.SetText(0, Localization.GetFmt("battlelog.tree.run", run.RunId));
            runItem.SetMetadata(0, $"run:{run.RunId}");
            runItem.Collapsed = run.RunId != (_battleLogSelectedRunId ?? service.CurrentRun.RunId);

            foreach (var battle in run.Battles)
            {
                var battleItem = _battleLogTree.CreateItem(runItem);
                battleItem.SetText(
                    0,
                    Localization.GetFmt(
                        "battlelog.tree.battle",
                        battle.BattleId,
                        string.IsNullOrWhiteSpace(battle.EncounterId) ? "-" : battle.EncounterId));
                battleItem.SetMetadata(0, $"battle:{run.RunId}:{battle.BattleId}");
                battleItem.Collapsed = battle.BattleId != (_battleLogSelectedBattleId
                    ?? service.CurrentBattle?.BattleId);

                foreach (var round in battle.Rounds)
                {
                    var roundItem = _battleLogTree.CreateItem(battleItem);
                    roundItem.SetText(0, Localization.GetFmt("battlelog.tree.round", round.Round));
                    roundItem.SetMetadata(0, $"round:{run.RunId}:{battle.BattleId}:{round.Round}");
                }
            }
        }
    }

    private void OnBattleLogTreeSelected()
    {
        var metadata = _battleLogTree?.GetSelected()?.GetMetadata(0).AsString() ?? string.Empty;
        var parts = metadata.Split(':');
        if (metadata == "all")
        {
            _battleLogScope = BattleLogScope.AllRun;
            _battleLogSelectedRunId = null;
            _battleLogSelectedBattleId = null;
        }
        else if (parts.Length == 2
            && parts[0] == "run"
            && int.TryParse(parts[1], out var runId))
        {
            _battleLogScope = BattleLogScope.AllRun;
            _battleLogSelectedRunId = runId;
            _battleLogSelectedBattleId = null;
        }
        else if (parts.Length == 3
            && parts[0] == "battle"
            && int.TryParse(parts[1], out runId)
            && int.TryParse(parts[2], out var battleId))
        {
            _battleLogScope = BattleLogScope.CurrentBattle;
            _battleLogSelectedRunId = runId;
            _battleLogSelectedBattleId = battleId;
        }
        else if (parts.Length == 4
            && parts[0] == "round"
            && int.TryParse(parts[1], out runId)
            && int.TryParse(parts[2], out battleId)
            && int.TryParse(parts[3], out var round))
        {
            _battleLogScope = BattleLogScope.CurrentRound;
            _battleLogSelectedRunId = runId;
            _battleLogSelectedBattleId = battleId;
            _battleLogSelectedRound = round;
        }

        RebuildBattleLogFilters();
        RebuildBattleLogList();
    }

    private void SetAllBattleLogTreeCollapsed(bool collapsed)
    {
        var root = _battleLogTree?.GetRoot();
        if (root == null)
        {
            return;
        }

        SetBattleLogTreeBranchCollapsed(root, collapsed, isRoot: true);
    }

    private static void SetBattleLogTreeBranchCollapsed(TreeItem item, bool collapsed, bool isRoot = false)
    {
        item.Collapsed = !isRoot && collapsed;
        var child = item.GetFirstChild();
        while (child != null)
        {
            SetBattleLogTreeBranchCollapsed(child, collapsed);
            child = child.GetNext();
        }
    }

    private void RebuildBattleLogFilters()
    {
        if (_battleLogFilterRow == null || _battleLogFilterRow.IsQueuedForDeletion())
        {
            return;
        }

        foreach (var child in _battleLogFilterRow.GetChildren())
        {
            _battleLogFilterRow.RemoveChild(child);
            child.QueueFree();
        }

        AddBattleLogScopeButton(BattleLogScope.CurrentRound, "battlelog.scope.round");
        AddBattleLogScopeButton(BattleLogScope.CurrentBattle, "battlelog.scope.battle");
        AddBattleLogScopeButton(BattleLogScope.AllRun, "battlelog.scope.run");

        if (_battleLogDeveloperView && DeveloperModeManager.IsDeveloperMode)
        {
            AddDeveloperLogFilterButton(DeveloperLogFilter.All, "battlelog.filter.all");
            AddDeveloperLogFilterButton(DeveloperLogFilter.Trigger, "battlelog.filter.trigger");
            AddDeveloperLogFilterButton(DeveloperLogFilter.Damage, "battlelog.filter.damage");
            AddDeveloperLogFilterButton(DeveloperLogFilter.Skill, "battlelog.filter.skill");
            AddDeveloperLogFilterButton(DeveloperLogFilter.Equipment, "battlelog.filter.equipment");
            AddDeveloperLogFilterButton(DeveloperLogFilter.Buff, "battlelog.filter.buff");
            AddDeveloperLogFilterButton(DeveloperLogFilter.Ai, "battlelog.filter.ai");
            AddDeveloperLogFilterButton(DeveloperLogFilter.Warning, "battlelog.filter.warning");
            AddDeveloperLogFilterButton(DeveloperLogFilter.Error, "battlelog.filter.error");
        }
        else
        {
            AddBattleLogFilterButton(BattleLogFilter.All, "battlelog.filter.all");
            AddBattleLogFilterButton(BattleLogFilter.Action, "battlelog.filter.action");
            AddBattleLogFilterButton(BattleLogFilter.Damage, "battlelog.filter.damage");
            AddBattleLogFilterButton(BattleLogFilter.System, "battlelog.filter.system");
        }
    }

    private void AddBattleLogScopeButton(BattleLogScope scope, string textKey)
    {
        if (_battleLogFilterRow == null)
        {
            return;
        }

        var button = new Button
        {
            Text = Localization.Get(textKey),
            ToggleMode = true,
            ButtonPressed = _battleLogScope == scope,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(112, 38)
        };
        UIResourceDatabase.ApplyBattleLogCategoryButton(button);
        button.Pressed += () =>
        {
            _battleLogScope = scope;
            if (scope == BattleLogScope.AllRun)
            {
                _battleLogSelectedRunId = null;
                _battleLogSelectedBattleId = null;
            }
            else
            {
                _battleLogSelectedRunId = _battleLogManager.Service.CurrentRun.RunId;
                _battleLogSelectedBattleId = _battleLogManager.Service.CurrentBattle?.BattleId;
            }
            if (scope == BattleLogScope.CurrentRound)
            {
                _battleLogSelectedRound = _turnNumber;
            }
            RebuildBattleLogFilters();
            RebuildBattleLogList();
        };
        _battleLogFilterRow.AddChild(button);
    }

    private void AddDeveloperLogFilterButton(DeveloperLogFilter filter, string textKey)
    {
        if (_battleLogFilterRow == null)
        {
            return;
        }

        var button = new Button
        {
            Text = Localization.Get(textKey),
            ToggleMode = true,
            ButtonPressed = _developerLogFilter == filter,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(90, 38)
        };
        UIResourceDatabase.ApplyBattleLogCategoryButton(button);
        button.Pressed += () =>
        {
            _developerLogFilter = filter;
            RebuildBattleLogFilters();
            RebuildBattleLogList();
        };
        _battleLogFilterRow.AddChild(button);
    }

    private void AddBattleLogFilterButton(BattleLogFilter filter, string textKey)
    {
        if (_battleLogFilterRow == null)
        {
            return;
        }

        var button = new Button
        {
            Text = Localization.Get(textKey),
            ToggleMode = true,
            ButtonPressed = _battleLogFilter == filter,
            FocusMode = FocusModeEnum.None,
            CustomMinimumSize = new Vector2(104, 38)
        };
        UIResourceDatabase.ApplyBattleLogCategoryButton(button);
        button.Pressed += () =>
        {
            _battleLogFilter = filter;
            RebuildBattleLogView();
        };
        _battleLogFilterRow.AddChild(button);
    }

    private void UpdateRecentReport()
    {
        if (_recentReportLabel == null)
        {
            return;
        }

        var currentBattle = _battleLogManager.Service.CurrentBattle;
        var entries = currentBattle?.Entries
            .Where(entry => entry.Round == _turnNumber && !entry.DebugOnly)
            .ToList()
            ?? new List<BattleLogEntry>();
        if (entries.Count == 0)
        {
            _recentReportLabel.Text = $"[center][font_size=18][color=#b9c0cc]{EscapeBbCode(Localization.Get("battlelog.recent.title"))}[/color][/font_size][/center]";
            return;
        }

        var playerName = GameManager.CurrentCharacter?.Name ?? Localization.Get("battlelog.actor.player");
        var sections = CombatFeedFormatter.BuildRound(entries, _turnNumber, playerName);
        var parts = new List<string>
        {
            $"[center][color=#607080]━━━━━━━━━━━━[/color][/center]",
            $"[center][font_size=20][b][color=#f0d58b]{EscapeBbCode(Localization.GetFmt("combat_feed.round", _turnNumber))}[/color][/b][/font_size][/center]",
            $"[center][color=#607080]━━━━━━━━━━━━[/color][/center]"
        };

        foreach (var section in sections)
        {
            if (section.Items.Count > 0)
            {
                var details = string.Join(Localization.Get("combat_feed.item_separator"), section.Items);
                parts.Add(
                    $"[font_size=16][b][color={section.ColorHex}]"
                    + $"【{EscapeBbCode(Localization.Get(section.TitleKey))}】[/color][/b]"
                    + $"[color=#e6e9ef]{EscapeBbCode(details)}[/color][/font_size]");
            }
        }

        _recentReportLabel.Text = string.Join("\n", parts);
        ScrollRecentReportToBottom();
    }

    private void ScrollRecentReportToBottom()
    {
        if (_recentReportLabel == null)
        {
            return;
        }

        var label = _recentReportLabel;
        Callable.From(() =>
        {
            if (IsInstanceValid(label))
            {
                label.ScrollToLine(System.Math.Max(0, label.GetLineCount() - 1));
            }
        }).CallDeferred();
    }

    private void RebuildBattleLogList()
    {
        if (_battleLogList == null || _battleLogList.IsQueuedForDeletion())
        {
            return;
        }

        foreach (var child in _battleLogList.GetChildren())
        {
            _battleLogList.RemoveChild(child);
            child.QueueFree();
        }

        var entries = GetFilteredBattleLogEntries();
        if (entries.Count == 0)
        {
            _battleLogList.AddChild(CreateLogEmptyLabel());
            return;
        }

        var previousPhase = string.Empty;
        foreach (var entry in entries)
        {
            if (!string.Equals(previousPhase, entry.Phase, System.StringComparison.Ordinal))
            {
                _battleLogList.AddChild(CreatePhaseHeader(entry));
                previousPhase = entry.Phase;
            }
            _battleLogList.AddChild(CreateBattleLogEntryRow(entry));
        }

        ScrollBattleLogToBottom();
    }

    /// <summary>
    /// 自动滚动到底部：新日志追加在 VBoxContainer 末尾（最新日志始终在最下面），
    /// 每次重建列表后都滚到底，玩家仍然可以手动向上滚查看历史。延后一帧执行，
    /// 保证 ScrollContainer 已经根据新增的子节点重新计算过内容高度。
    /// </summary>
    private void ScrollBattleLogToBottom()
    {
        if (_battleLogScroll == null)
        {
            return;
        }

        var scroll = _battleLogScroll;
        Callable.From(() =>
        {
            if (IsInstanceValid(scroll))
            {
                scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue;
            }
        }).CallDeferred();
    }

    private List<BattleLogEntry> GetFilteredBattleLogEntries()
    {
        // 按时间正序（旧→新）返回，配合 RebuildBattleLogList 按顺序 AddChild，
        // 保证"最新日志始终在最下面"；滚动到底部由 RebuildBattleLogList 调用后
        // 统一处理，这里只负责筛选顺序。
        var query = _battleLogSearchInput?.Text.Trim() ?? string.Empty;
        var selectedRound = _battleLogSelectedRound > 0 ? _battleLogSelectedRound : _turnNumber;
        var source = _battleLogManager.Service.Query(
            _battleLogScope,
            selectedRound,
            _battleLogDeveloperView && DeveloperModeManager.IsDeveloperMode,
            query,
            _developerLogFilter,
            _battleLogSelectedBattleId,
            _battleLogSelectedRunId);
        var entries = new List<BattleLogEntry>();
        for (var i = 0; i < source.Count; i++)
        {
            var entry = source[i];
            if (!_battleLogDeveloperView && entry.DebugOnly)
            {
                continue;
            }

            if (!_battleLogDeveloperView && !MatchesFilter(entry))
            {
                continue;
            }

            if (!MatchesSearch(entry, query))
            {
                continue;
            }

            entries.Add(entry);
        }

        return entries;
    }

    private void NormalizeBattleLogSelection()
    {
        var service = _battleLogManager.Service;
        if (_battleLogScope == BattleLogScope.AllRun && !_battleLogSelectedRunId.HasValue)
        {
            return;
        }

        var selectedRun = _battleLogSelectedRunId.HasValue
            ? service.Runs.FirstOrDefault(run => run.RunId == _battleLogSelectedRunId.Value)
            : null;
        var selectedBattle = _battleLogSelectedBattleId.HasValue
            ? service.Runs.SelectMany(run => run.Battles)
                .FirstOrDefault(battle => battle.BattleId == _battleLogSelectedBattleId.Value)
            : null;

        if (selectedBattle != null)
        {
            selectedRun = service.Runs.FirstOrDefault(run => run.Battles.Contains(selectedBattle));
        }

        if (selectedRun == null)
        {
            selectedRun = service.CurrentRun;
            _battleLogSelectedRunId = selectedRun.RunId;
        }

        if (_battleLogScope == BattleLogScope.AllRun)
        {
            return;
        }

        if (selectedBattle == null || !selectedRun.Battles.Contains(selectedBattle))
        {
            selectedBattle = selectedRun == service.CurrentRun
                ? service.CurrentBattle
                : selectedRun.Battles.LastOrDefault();
            _battleLogSelectedBattleId = selectedBattle?.BattleId;
        }

        if (_battleLogScope != BattleLogScope.CurrentRound || selectedBattle == null)
        {
            return;
        }

        if (_battleLogSelectedRound <= 0
            || selectedBattle.Rounds.All(round => round.Round != _battleLogSelectedRound))
        {
            _battleLogSelectedRound = selectedBattle.Rounds.LastOrDefault()?.Round ?? _turnNumber;
        }
    }

    private static Label CreatePhaseHeader(BattleLogEntry entry)
    {
        var label = new Label
        {
            Text = $"{Localization.Get("battlelog.phase_header")} {entry.Phase}",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        label.AddThemeFontSizeOverride("font_size", 17);
        label.AddThemeColorOverride("font_color", new Color(0.36f, 0.86f, 0.92f));
        return label;
    }

    private bool MatchesFilter(BattleLogEntry entry)
        => _battleLogFilter switch
        {
            BattleLogFilter.All => !entry.DebugOnly || DeveloperModeManager.IsDeveloperMode,
            BattleLogFilter.Action => entry.Category is BattleLogCategory.Action or BattleLogCategory.Buff,
            BattleLogFilter.Damage => entry.Category is BattleLogCategory.Damage or BattleLogCategory.Recover,
            BattleLogFilter.System => entry.Category == BattleLogCategory.System,
            BattleLogFilter.Debug => DeveloperModeManager.IsDeveloperMode && entry.DebugOnly,
            _ => true
        };

    private static bool MatchesSearch(BattleLogEntry entry, string query)
    {
        return string.IsNullOrWhiteSpace(query)
            || entry.GetSearchText().Contains(query, System.StringComparison.OrdinalIgnoreCase);
    }

    private static Label CreateLogEmptyLabel()
    {
        var label = new Label
        {
            Text = Localization.Get("battlelog.empty")
        };
        label.AddThemeFontSizeOverride("font_size", 22);
        label.AddThemeColorOverride("font_color", new Color(0.74f, 0.78f, 0.84f));
        return label;
    }

    private Control CreateBattleLogEntryRow(BattleLogEntry entry)
    {
        var card = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        card.AddThemeStyleboxOverride("panel", CreateLogCardStyle(entry.Category));

        var label = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Text = _battleLogDeveloperView
                ? FormatDeveloperEntryText(entry)
                : FormatEntryText(entry)
        };
        label.AddThemeFontSizeOverride("normal_font_size", entry.DebugOnly ? 16 : 19);
        card.AddChild(label);
        return card;
    }

    private static string FormatDeveloperEntryText(BattleLogEntry entry)
    {
        var parts = new List<string>
        {
            $"#{entry.EventId}",
            entry.Kind.ToString()
        };
        if (entry.TriggerTiming.HasValue)
        {
            parts.Add($"Timing={entry.TriggerTiming}");
        }
        if (entry.Priority.HasValue)
        {
            parts.Add($"Priority={entry.Priority}");
        }
        if (!string.IsNullOrWhiteSpace(entry.Source))
        {
            parts.Add($"Source={entry.Source}");
        }
        if (!string.IsNullOrWhiteSpace(entry.Actor))
        {
            parts.Add($"Actor={entry.Actor}");
        }
        if (!string.IsNullOrWhiteSpace(entry.Target))
        {
            parts.Add($"Target={entry.Target}");
        }
        if (entry.DamageBefore.HasValue || entry.DamageAfter.HasValue)
        {
            parts.Add($"Damage={entry.DamageBefore?.ToString("0.##") ?? "-"}→{entry.DamageAfter?.ToString("0.##") ?? "-"}");
        }
        if (entry.HpBefore.HasValue || entry.HpAfter.HasValue)
        {
            parts.Add($"HP={entry.HpBefore?.ToString() ?? "-"}→{entry.HpAfter?.ToString() ?? "-"}");
        }
        if (!string.IsNullOrWhiteSpace(entry.Result))
        {
            parts.Add($"Result={entry.Result}");
        }

        var header = EscapeBbCode(string.Join(" | ", parts));
        var text = EscapeBbCode(entry.GetText());
        return $"[color={ToHtmlColor(entry.Color)}]{header}[/color]\n[color=#B9C0CC]{text}[/color]";
    }

    private static string FormatEntryText(BattleLogEntry entry)
    {
        var text = EscapeBbCode(entry.GetText());
        if (entry.StackCount > 1)
        {
            text = Localization.GetFmt("battlelog.stack_fmt", text, entry.StackCount);
        }

        return $"[color={ToHtmlColor(entry.Color)}]{text}[/color]";
    }

    private static StyleBox CreateLogCardStyle(BattleLogCategory category)
    {
        var border = category switch
        {
            BattleLogCategory.Debug => new Color(0.76f, 0.61f, 0.18f),
            BattleLogCategory.Error => new Color(0.85f, 0.34f, 0.12f),
            _ => new Color(0.32f, 0.36f, 0.42f)
        };

        return UIResourceDatabase.CreateLogEntryStyle(Colors.White.Lerp(border, 0.12f));
    }

    private void AppendBattleLogEntry()
    {
        if (_context?.PlayerAction == null || _context.EnemyActions.Count == 0)
        {
            return;
        }

        var roundEntry = _battleLogManager.CreateEntry(
            _turnNumber,
            BattleLogCategory.System,
            BattleLogEventKind.RoundStart,
            BattlePhase.BattlePhase.ToString(),
            "battlelog.round",
            _turnNumber.ToString());
        _battleLogManager.Add(roundEntry);

        var phaseEntry = _battleLogManager.CreateEntry(
            _turnNumber,
            BattleLogCategory.System,
            BattleLogEventKind.Reveal,
            BattlePhase.BattlePhase.ToString(),
            "battlelog.phase",
            Localization.Get("battle.phase.resolve"));
        _battleLogManager.Add(phaseEntry);

        AddActionLogForPlayer(_context.PlayerAction);
        foreach (var entry in _context.EnemyActions)
        {
            // EnemyActions 是本回合亮牌时已经确定的行动。敌人在结算后死亡不能
            // 反向抹除其行动，否则同回合的伤害、费用和位置结果将失去单位映射。
            var enemyActionEntry = _battleLogManager.CreateEntry(
                _turnNumber,
                BattleLogCategory.Action,
                BattleLogEventKind.EnemyAction,
                BattlePhase.BattlePrePhase.ToString(),
                "battlelog.enemy_use_card",
                entry.Enemy.Name,
                entry.Action.DisplayName);
            enemyActionEntry.Actor = entry.Enemy.DisplayName;
            enemyActionEntry.Target = entry.Action.Target?.Name ?? _player.DisplayName;
            enemyActionEntry.Card = entry.Action.DisplayName;
            enemyActionEntry.Source = BattleContext.GetUnitStateKey(entry.Enemy);
            _battleLogManager.Add(enemyActionEntry);
        }

        AddResultLogs(_context.RoundResult);
        AddSkillLogs();

        // 回合结束标记：这一回合的结算已经全部写完，方便阅读日志时看清回合分界。
        var roundEndEntry = _battleLogManager.CreateEntry(
            _turnNumber,
            BattleLogCategory.System,
            BattleLogEventKind.RoundEnd,
            BattlePhase.EndPhase.ToString(),
            "battlelog.round_end",
            _turnNumber.ToString());
        _battleLogManager.Add(roundEndEntry);

        RebuildBattleLogView();
        UpdateRecentReport();
    }

    private void AddActionLogForPlayer(BattleAction action)
    {
        var entry = _battleLogManager.CreateEntry(
            _turnNumber,
            BattleLogCategory.Action,
            BattleLogEventKind.PlayerAction,
            BattlePhase.BattlePrePhase.ToString(),
            "battlelog.player_use_card",
            action.DisplayName);
        entry.Actor = _player.DisplayName;
        entry.Target = action.Target?.Name ?? string.Empty;
        entry.Card = action.DisplayName;
        _battleLogManager.Add(entry);
    }

    private void AddResultLogs(RoundResult result)
    {
        var playerName = Localization.Get("battlelog.actor.player");
        var enemyName = Localization.Get("battlelog.actor.enemy");

        // 亮牌克制关系（例如"火杀克制杀"）：直接读取 BattleResolver 已经写入
        // RoundResult 的 Relations，不重新判断谁克制谁。
        foreach (var relation in result.Relations)
        {
            var entry = _battleLogManager.CreateEntry(
                _turnNumber,
                BattleLogCategory.Action,
                BattleLogEventKind.Counter,
                BattlePhase.BattlePhase.ToString(),
                "battlelog.raw",
                relation);
            entry.Result = relation;
            _battleLogManager.Add(entry);
        }

        foreach (var steal in result.StealResolutions)
        {
            var entry = _battleLogManager.CreateEntry(
                _turnNumber,
                BattleLogCategory.Action,
                BattleLogEventKind.CardResolution,
                BattlePhase.BattlePhase.ToString(),
                steal.Resolved
                    ? "combat_feed.steal_resolved"
                    : "combat_feed.steal_cancelled",
                BattleRules.GetCardName(CardType.Steal),
                BattleRules.FormatMana(steal.Amount));
            entry.Actor = steal.Actor;
            entry.Target = steal.Target;
            entry.Card = BattleRules.GetCardName(CardType.Steal);
            entry.EnergyAfter = steal.Amount;
            entry.Result = steal.Resolved ? "Resolved" : "Cancelled";
            _battleLogManager.Add(entry);
        }

        // 结算细节叙述（龙胆免伤、酒/伤害计算明细等）：同样直接读取既有的
        // RoundResult.Lines，由战斗流程自己写入，这里只负责展示。
        foreach (var line in result.Lines)
        {
            _battleLogManager.AddAction(_turnNumber, "battlelog.raw", line);
        }

        if (result.EnemyDamage > 0
            && !_battleLogManager.Service.HasCurrentBattleEvent(
                BattleLogEventKind.Damage,
                _turnNumber))
        {
            _battleLogManager.AddDamage(_turnNumber, "battlelog.damage", enemyName, result.EnemyDamage.ToString());
        }
        if (result.PlayerDamage > 0
            && !_battleLogManager.Service.HasCurrentBattleEvent(
                BattleLogEventKind.Damage,
                _turnNumber))
        {
            _battleLogManager.AddDamage(_turnNumber, "battlelog.damage", playerName, result.PlayerDamage.ToString());
        }
        if (result.HealByUnit.Count > 0)
        {
            foreach (var heal in result.HealByUnit)
            {
                var targetName = ReferenceEquals(heal.Key, _player) ? playerName : heal.Key.DisplayName;
                AddHealLog(targetName, BattleContext.GetUnitStateKey(heal.Key), heal.Value);
            }
        }
        else
        {
            if (result.PlayerHeal > 0)
            {
                AddHealLog(playerName, BattleContext.GetUnitStateKey(_player), result.PlayerHeal);
            }
            if (result.EnemyHeal > 0)
            {
                AddHealLog(enemyName, string.Empty, result.EnemyHeal);
            }
        }
        if (TotalEnemyBlocks(result) > 0)
        {
            _battleLogManager.AddDamage(_turnNumber, "battlelog.block", enemyName);
        }
        if (TotalPlayerBlocks(result) > 0)
        {
            _battleLogManager.AddDamage(_turnNumber, "battlelog.block", playerName);
        }
        if (result.WineGainByUnit.Count > 0)
        {
            foreach (var wine in result.WineGainByUnit)
            {
                var actorName = ReferenceEquals(wine.Key, _player) ? playerName : wine.Key.DisplayName;
                AddWineGainLog(actorName, BattleContext.GetUnitStateKey(wine.Key), wine.Value);
            }
        }
        else
        {
            if (result.PlayerWineGain > 0)
            {
                AddWineGainLog(playerName, BattleContext.GetUnitStateKey(_player), result.PlayerWineGain);
            }
            if (result.EnemyWineGain > 0)
            {
                AddWineGainLog(enemyName, string.Empty, result.EnemyWineGain);
            }
        }
        if (result.ManaGainByUnit.Count > 0)
        {
            foreach (var gain in result.ManaGainByUnit)
            {
                var actorName = ReferenceEquals(gain.Key, _player)
                    ? playerName
                    : gain.Key.DisplayName;
                AddManaGainLog(actorName, BattleContext.GetUnitStateKey(gain.Key), gain.Value);
            }
        }
        else
        {
            // 旧存档或尚未迁移的调用仍可能只写双方合计；保留降级显示，避免日志缺失。
            if (result.PlayerManaGain > 0)
            {
                AddManaGainLog(playerName, BattleContext.GetUnitStateKey(_player), result.PlayerManaGain);
            }
            if (result.EnemyManaGain > 0)
            {
                AddManaGainLog(enemyName, string.Empty, result.EnemyManaGain);
            }
        }
        if (result.PlayerStealGain > 0)
        {
            _battleLogManager.AddAction(_turnNumber, "battlelog.steal_gain", playerName, BattleRules.FormatMana(result.PlayerStealGain));
        }
        if (result.EnemyStealGain > 0)
        {
            _battleLogManager.AddAction(_turnNumber, "battlelog.steal_gain", enemyName, BattleRules.FormatMana(result.EnemyStealGain));
        }
        if (!result.HasLines)
        {
            _battleLogManager.AddSystem(_turnNumber, "battlelog.no_damage");
        }
    }

    private void AddManaGainLog(string actorName, string unitKey, double amount)
    {
        var entry = _battleLogManager.CreateEntry(
            _turnNumber,
            BattleLogCategory.Action,
            BattleLogEventKind.Energy,
            BattlePhase.BattlePhase.ToString(),
            "battlelog.mana_gain",
            actorName,
            BattleRules.FormatMana(amount));
        entry.Actor = actorName;
        entry.Source = unitKey;
        entry.EnergyAfter = amount;
        _battleLogManager.Add(entry);
    }

    private void AddHealLog(string targetName, string unitKey, int amount)
    {
        var entry = _battleLogManager.CreateEntry(
            _turnNumber,
            BattleLogCategory.Recover,
            BattleLogEventKind.Heal,
            BattlePhase.BattlePhase.ToString(),
            "battlelog.heal",
            targetName,
            amount.ToString());
        entry.Target = targetName;
        entry.Source = unitKey;
        entry.DamageAfter = amount;
        _battleLogManager.Add(entry);
    }

    private void AddWineGainLog(string actorName, string unitKey, int amount)
    {
        var entry = _battleLogManager.CreateEntry(
            _turnNumber,
            BattleLogCategory.Buff,
            BattleLogEventKind.Buff,
            BattlePhase.BattlePhase.ToString(),
            "battlelog.wine_gain",
            actorName,
            amount.ToString());
        entry.Actor = actorName;
        entry.Source = unitKey;
        entry.Buff = "Wine";
        _battleLogManager.Add(entry);
    }

    private static int TotalPlayerBlocks(RoundResult result)
        => result.PlayerDodgeBlocks
            + result.PlayerKillDodgeBlocks
            + result.PlayerFireKillDodgeBlocks
            + result.PlayerArrowDodgeBlocks
            + result.PlayerThunderCounterBlocks
            + result.PlayerSureKillCounterBlocks
            + result.PlayerArrowCounterBlocks
            + result.PlayerStealCounterBlocks
            + result.PlayerQingnangDodgeBlocks
            + result.PlayerUniversalBlocks
            + result.PlayerWineBlocks;

    private static int TotalEnemyBlocks(RoundResult result)
        => result.EnemyDodgeBlocks
            + result.EnemyKillDodgeBlocks
            + result.EnemyFireKillDodgeBlocks
            + result.EnemyArrowDodgeBlocks
            + result.EnemyThunderCounterBlocks
            + result.EnemySureKillCounterBlocks
            + result.EnemyArrowCounterBlocks
            + result.EnemyStealCounterBlocks
            + result.EnemyQingnangDodgeBlocks
            + result.EnemyUniversalBlocks
            + result.EnemyWineBlocks;

    private void AddSkillLogs()
    {
        if (_context == null)
        {
            return;
        }

        foreach (var line in _context.TriggerLogs)
        {
            var skillName = ExtractBracketName(line);
            if (!string.IsNullOrWhiteSpace(skillName))
            {
                if (_battleLogManager.Service.CurrentBattle?.Entries.Any(
                    entry => entry.Round == _turnNumber
                        && entry.Kind == BattleLogEventKind.Skill
                        && string.Equals(entry.Skill, skillName, System.StringComparison.Ordinal)) == true)
                {
                    continue;
                }

                var entry = _battleLogManager.CreateEntry(
                    _turnNumber,
                    BattleLogCategory.Buff,
                    BattleLogEventKind.Skill,
                    _phase.ToString(),
                    "battlelog.skill_activated",
                    skillName);
                entry.Skill = skillName;
                _battleLogManager.Add(entry);
            }
        }
    }

    private void AddDebugLogs()
    {
        if (_context == null)
        {
            return;
        }

        _battleLogManager.AddDebug(_turnNumber, "battlelog.debug_line", "Card Resolve: counter check");
        _battleLogManager.AddDebug(_turnNumber, "battlelog.debug_line", "Card Resolve: defense layer");
        _battleLogManager.AddDebug(_turnNumber, "battlelog.debug_line", "Card Resolve: damage/resource/heal");
        foreach (var line in _context.TriggerLogs)
        {
            _battleLogManager.AddDebug(_turnNumber, "battlelog.debug_line", line);
        }
    }

    // 除了 TriggerTiming 枚举名（"[OnTurnStart]"等，来自 context.AddEffectDebugLog
    // 的引擎调试追踪），还有少数几个纯内部流程/管线标记也写进了同一份
    // TriggerLogs，同样不是真正的技能/效果触发名："[DamageModifierPipeline]"
    // （Scripts/Damage/DamageEffects.cs 伤害修正管线阶段标记）、"[Debug]"
    // （调试用途）、"[Flow]"（BattleManager 内部流程追踪，如"返回地图按钮点击"）。
    private static readonly HashSet<string> NonSkillBracketTags = new()
    {
        "DamageModifierPipeline",
        "Debug",
        "Flow",
        "Equipment",
        "Buff",
        "TargetBinding"
    };

    private static string ExtractBracketName(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("[") || !line.EndsWith("]"))
        {
            return string.Empty;
        }

        var text = line.Trim('[', ']');
        if (text.Contains('/'))
        {
            return string.Empty;
        }

        // context.AddEffectDebugLog() 会把"[OnTurnStart]"这类引擎内部的 Trigger
        // 时机标记也写进同一份 TriggerLogs（只在 EnableEffectDebugLog 时用于
        // Developer 调试面板追踪，见 BattleContext.cs）。这些不是真正的技能/效果
        // 触发名，不能被当成"[龙胆]"/"[武库]"这类真实技能标记显示给玩家——
        // 否则每回合都会刷出"OnTurnStart发动。OnBattlePrePhase发动。"之类的噪声，
        // 把真正有意义的技能触发和结算内容淹没掉，这正是"日志只能看到 Round /
        // Battle Start"这个 Bug 的根因。按 TriggerTiming 枚举名单 + 上面这几个
        // 已知的内部管线标记排除即可，不需要新增任何标记或修改
        // AddEffectDebugLog/DamageEffects.cs 本身的写入逻辑。
        if (System.Enum.TryParse<TriggerTiming>(text, out _) || NonSkillBracketTags.Contains(text))
        {
            return string.Empty;
        }

        return text;
    }

    private void ExportBattleLog()
    {
        var textPath = "user://battle_log.txt";
        var jsonPath = "user://battle_log.json";
        using var textFile = FileAccess.Open(textPath, FileAccess.ModeFlags.Write);
        using var jsonFile = FileAccess.Open(jsonPath, FileAccess.ModeFlags.Write);
        if (textFile == null || jsonFile == null)
        {
            ShowBattleMessage(Localization.Get("battlelog.export_failed"));
            return;
        }

        textFile.StoreString(_battleLogManager.Service.ExportText());
        jsonFile.StoreString(_battleLogManager.Service.ExportJson());
        ShowBattleMessage(Localization.GetFmt(
            "battlelog.export_success",
            ProjectSettings.GlobalizePath(textPath)));
    }

    private string BuildExportBattleLogText()
    {
        return _battleLogManager.Service.ExportText();
    }

    private static string ToHtmlColor(Color color)
    {
        var r = (int)System.Math.Clamp(color.R * 255f, 0f, 255f);
        var g = (int)System.Math.Clamp(color.G * 255f, 0f, 255f);
        var b = (int)System.Math.Clamp(color.B * 255f, 0f, 255f);
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
