//////////////////////////////////////////////////////////
// 文件：Scripts/DeveloperDebugPanel.cs
//
// 模块：Developer Tools
//
// 为什么存在：
// 之前每新增一个系统（英雄解锁、地图调试、商店调试）都会在各自的界面里加一个
// 专属的调试窗口/按钮。这个面板把"英雄/Boss/事件/成就/地图/装备/金币"这些
// 调试操作全部收拢到一个统一的、按 F8 开关的全局面板里，以后任何系统的调试
// 需求都应该加到这里的某一页，而不是再造一个专属 Debug UI。
//
// 职责：
// 1. 提供一个 TabContainer：Hero / Enemy / Event / Achievement / Inventory /
//    Battle / Save 七个标签页，左侧 Tab、右侧内容，支持滚动。
// 2. 所有按钮只调用既有的正式接口（HeroUnlockProgress / GameManager /
//    InventoryManager / EquipmentDatabase / RunBuffDatabase / RunBuffManager /
//    MainFlow 提供的调试专用只读转发方法），不自己维护第二份状态。
// 3. 只在开发者模式下可见、可用；正式游戏不受任何影响。
//
// 不负责：
// × 判断战斗结算、伤害计算（Battle 页只调用既有的 Tutorial 调试专用公开方法
//   读写当前battle Player 的 HP/费用，不修改 BattleManager 本身）。
// × 维护 Hero/Enemy/Event/Achievement 的"数据库"（哪些英雄、哪些敌人、哪些
//   事件——全部直接读取既有的 CharacterDatabase/EnemyDatabase/EventDatabase，
//   这个类不缓存、不复制一份）。
// × 修改任何 HeroUnlockDatabase/EnemyDatabase/EventDatabase 里的只读定义——
//   所有修改都只作用于 HeroUnlockProgress（见该类新增的一批 XxxForDebug 方法）。
//
// 主要依赖：
// HeroUnlockProgress / HeroUnlockDatabase / GameManager / InventoryManager /
// EquipmentDatabase / RunBuffDatabase / RunBuffManager / DeveloperModeManager
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 开发者进度调试面板：按 F8 开关（由 MainFlow 统一处理按键并挂载/隐藏这个面板），
/// 只在 <see cref="DeveloperModeManager.IsDeveloperMode"/> 为 true 时可以打开。
/// </summary>
public partial class DeveloperDebugPanel : Control
{
    /// <summary>
    /// 由 MainFlow 注入：返回当前正在进行的 BattleManager 实例（不在战斗中则为 null）。
    /// Battle 页的按钮通过这个委托拿到实例，再调用它已经公开的 Tutorial 调试方法——
    /// 不新增 BattleManager 的任何方法，也不在这里直接 new 一个 BattleManager。
    /// </summary>
    public Func<BattleManager?>? GetActiveBattleManager { get; set; }

    /// <summary>
    /// 由 MainFlow 注入：等价于调用 MainFlow 原有的（私有）OnNodeSelected(nodeId)。
    /// 只是把已有的节点进入流程暴露出来给调试面板用，不新增任何进入关卡的逻辑。
    /// </summary>
    public Action<string>? DebugSelectNode { get; set; }

    /// <summary>由 MainFlow 注入：等价于调用其私有 ShowTutorialSelect()。</summary>
    public Action? OpenTutorialSelectRequested { get; set; }

    /// <summary>由 MainFlow 注入：等价于调用其私有 ShowCombatTutorial()。</summary>
    public Action? OpenCombatTutorialRequested { get; set; }

    /// <summary>由 MainFlow 注入：等价于调用其私有 ShowMetaTutorial()。</summary>
    public Action? OpenMetaTutorialRequested { get; set; }

    /// <summary>
    /// 快速模式开关：本项目第一个此类"跳过/加速演出动画"的全局标志位。当前只被
    /// <see cref="DiceRollOverlay"/> 消费（周泰【不屈】骰子动画），未来任何新增的
    /// 演出动画都可以复用同一个开关，而不必各自新增一份状态。
    /// </summary>
    public static bool FastMode;

    /// <summary>
    /// 开发者强制指定周泰【不屈】下一次1D6判定结果（1~6）。由 ZhouTaiBuQuTriggerEffect
    /// 消费：读取后立即清空为 null，不影响后续正常随机——与
    /// FactionFateState.DebugForcedNextDiceRoll 的既有约定完全一致。
    /// </summary>
    public static int? ForcedZhouTaiDiceResult;

    private Label? _zhouTaiStatusLabel;
    private DiceRollOverlay? _zhouTaiTestDiceOverlay;
    private Label? _stinkyMushroomStatusLabel;

    private VBoxContainer? _heroContent;
    private VBoxContainer? _enemyContent;
    private VBoxContainer? _eventContent;
    private VBoxContainer? _achievementContent;

    /// <summary>
    /// Developer Tools 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        Visible = false;
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsPreset(LayoutPreset.FullRect);
        BuildLayout();
        Localization.LanguageChanged += RebuildLayout;
    }

    /// <summary>
    /// Developer Tools 的公开入口：_ExitTree。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _ExitTree()
    {
        Localization.LanguageChanged -= RebuildLayout;
    }

    /// <summary>打开面板前调用一次，保证每页显示的都是最新数据。</summary>
    public void RefreshAndShow()
    {
        RefreshHeroTab();
        RefreshEnemyTab();
        RefreshEventTab();
        RefreshAchievementTab();
        RefreshTutorialTab();
        Visible = true;
    }

    // 面板从 MainFlow._Ready() 就常驻创建、隐藏在后台，不像其它屏幕那样每次
    // 重新打开都会重建；语言切换时如果不重建，标题/按钮文字会停留在创建时的
    // 语言，和这次要修的"没有本地化"是同一类问题，这里补上和其它屏幕一致的
    // 重建约定（例如 MapController.RefreshMapView）。
    private void RebuildLayout()
    {
        var wasVisible = Visible;
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        BuildLayout();
        Visible = wasVisible;
    }

    private void BuildLayout()
    {
        var background = new ColorRect
        {
            Color = new Color(0.05f, 0.05f, 0.06f, 0.94f)
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 40);
        margin.AddThemeConstantOverride("margin_top", 30);
        margin.AddThemeConstantOverride("margin_right", 40);
        margin.AddThemeConstantOverride("margin_bottom", 30);
        AddChild(margin);

        var root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 12);
        margin.AddChild(root);

        var header = new HBoxContainer();
        root.AddChild(header);

        var title = new Label
        {
            Text = Localization.Get("devpanel.title"),
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        title.AddThemeFontSizeOverride("font_size", 26);
        header.AddChild(title);

        var closeButton = new Button { Text = Localization.Get("devpanel.close"), CustomMinimumSize = new Vector2(120, 40) };
        closeButton.Pressed += () => Visible = false;
        header.AddChild(closeButton);

        var body = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        body.AddThemeConstantOverride("separation", 16);
        root.AddChild(body);

        var categoryList = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(160, 0)
        };
        categoryList.AddThemeConstantOverride("separation", 6);
        body.AddChild(categoryList);

        var contentArea = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        body.AddChild(contentArea);

        var pages = new (string Title, ScrollContainer Page)[]
        {
            (Localization.Get("devpanel.tab.hero"), BuildHeroTab()),
            (Localization.Get("devpanel.tab.enemy"), BuildEnemyTab()),
            (Localization.Get("devpanel.tab.event"), BuildEventTab()),
            (Localization.Get("devpanel.tab.achievement"), BuildAchievementTab()),
            (Localization.Get("devpanel.tab.inventory"), BuildInventoryTab()),
            (Localization.Get("devpanel.tab.factionfate"), BuildFactionFateTab()),
            (Localization.Get("devpanel.tab.battle"), BuildBattleTab()),
            (Localization.Get("devpanel.tab.tutorial"), BuildTutorialTab()),
            (Localization.Get("devpanel.tab.save"), BuildSaveTab()),
            (Localization.Get("devpanel.tab.codex"), BuildCodexTab())
        };

        var categoryButtons = new List<Button>();
        for (var i = 0; i < pages.Length; i++)
        {
            var page = pages[i].Page;
            page.SetAnchorsPreset(LayoutPreset.FullRect);
            page.Visible = i == 0;
            contentArea.AddChild(page);

            var index = i;
            var button = new Button
            {
                Text = pages[i].Title,
                ToggleMode = true,
                ButtonPressed = i == 0,
                CustomMinimumSize = new Vector2(0, 40)
            };
            button.Pressed += () =>
            {
                for (var j = 0; j < pages.Length; j++)
                {
                    pages[j].Page.Visible = j == index;
                    categoryButtons[j].ButtonPressed = j == index;
                }
            };
            categoryButtons.Add(button);
            categoryList.AddChild(button);
        }

        UIResourceDatabase.ApplyScrollBars(this);
    }

    // ════════════════════════════════════════
    // 通用小工具：一行 = 标题 Label + 一串按钮
    // ════════════════════════════════════════

    private static ScrollContainer WrapInScroll(VBoxContainer content)
    {
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(content);
        return scroll;
    }

    private static HBoxContainer MakeRow(string labelText)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var label = new Label
        {
            Text = labelText,
            CustomMinimumSize = new Vector2(320, 0)
        };
        label.AddThemeFontSizeOverride("font_size", 18);
        row.AddChild(label);
        return row;
    }

    private static Button MakeButton(string text, Action onPressed)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(64, 36) };
        button.Pressed += onPressed;
        return button;
    }

    private static void ClearChildren(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }

    // ════════════════════════════════════════
    // 第一页：Hero
    // ════════════════════════════════════════

    private ScrollContainer BuildHeroTab()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        _heroContent = content;

        var topRow = new HBoxContainer();
        topRow.AddThemeConstantOverride("separation", 12);
        topRow.AddChild(MakeButton(Localization.Get("devpanel.hero.unlock_all"), () =>
        {
            HeroUnlockProgress.UnlockAllForDebug();
            RefreshHeroTab();
        }));
        topRow.AddChild(MakeButton(Localization.Get("devpanel.hero.reset_all"), () =>
        {
            HeroUnlockProgress.ResetHeroesForDebug();
            RefreshHeroTab();
        }));
        content.AddChild(topRow);
        content.AddChild(new HSeparator());

        RefreshHeroTab();
        return WrapInScroll(content);
    }

    private void RefreshHeroTab()
    {
        if (_heroContent == null)
        {
            return;
        }

        // 保留顶部的【全部解锁】/【全部重置】行和分隔线（前两个子节点），只重建下面的列表。
        for (var i = _heroContent.GetChildCount() - 1; i >= 2; i--)
        {
            var child = _heroContent.GetChild(i);
            _heroContent.RemoveChild(child);
            child.QueueFree();
        }

        foreach (var character in CharacterDatabase.GetAllCharacters())
        {
            var unlocked = HeroUnlockProgress.IsHeroUnlocked(character.Id);
            var statusText = Localization.Get(unlocked ? "devpanel.hero.status_unlocked" : "devpanel.hero.status_locked");
            var row = MakeRow(Localization.GetFmt("devpanel.hero.row_fmt", character.Name, character.Id, statusText));

            var characterId = character.Id;
            row.AddChild(MakeButton(Localization.Get("devpanel.hero.unlock"), () =>
            {
                HeroUnlockProgress.SetHeroUnlockedForDebug(characterId, true);
                RefreshHeroTab();
            }));
            row.AddChild(MakeButton(Localization.Get("devpanel.hero.lock"), () =>
            {
                HeroUnlockProgress.SetHeroUnlockedForDebug(characterId, false);
                RefreshHeroTab();
            }));

            var condition = HeroUnlockDatabase.Get(character.Id);
            if (condition != null)
            {
                var (_, current, required) = HeroUnlockProgress.EvaluateCondition(condition);
                var progressLabel = new Label { Text = Localization.GetFmt("devpanel.hero.condition_progress_fmt", condition.DisplayDescription, current, required) };
                progressLabel.AddThemeFontSizeOverride("font_size", 14);
                progressLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.72f, 0.78f));
                row.AddChild(progressLabel);

                // 直接推进这条解锁条件底层的 Progress 计数器——KillEnemy 按 Boss 击败次数 +1，
                // Achievement 按成就进度 +1；不直接把英雄标记为已解锁，让正式的
                // HeroUnlockProgress.CheckForNewUnlocks 自己判断是否达成，和真实流程一致。
                row.AddChild(MakeButton(Localization.Get("devpanel.hero.condition_add1"), () =>
                {
                    AdvanceConditionForDebug(condition, 1);
                    RefreshHeroTab();
                }));
                row.AddChild(MakeButton(Localization.Get("devpanel.hero.condition_complete"), () =>
                {
                    AdvanceConditionForDebug(condition, condition.RequiredAmount);
                    RefreshHeroTab();
                }));
            }

            _heroContent.AddChild(row);
        }
    }

    /// <summary>
    /// 把一条 <see cref="HeroUnlockCondition"/> 底层依赖的 Progress 计数器直接设为
    /// <paramref name="value"/>（KillEnemy 时对 TargetId 用 '|' 分隔出的每个敌人 Id
    /// 都设置一遍，Achievement 时设置对应成就）。只修改 Progress，不直接把英雄标记为
    /// 已解锁，也不改 HeroUnlockDatabase/EnemyDatabase 这些只读定义。
    /// </summary>
    private static void AdvanceConditionForDebug(HeroUnlockCondition condition, int value)
    {
        switch (condition.ConditionType)
        {
            case HeroUnlockConditionType.KillEnemy:
                foreach (var enemyId in condition.TargetId.Split('|', StringSplitOptions.RemoveEmptyEntries))
                {
                    HeroUnlockProgress.SetEnemyKillCountForDebug(enemyId, value);
                }

                break;

            case HeroUnlockConditionType.Achievement:
                if (Enum.TryParse<AchievementType>(condition.TargetId, out var achievementType))
                {
                    HeroUnlockProgress.SetAchievementProgressForDebug(achievementType, value);
                }

                break;

            case HeroUnlockConditionType.CompleteEvent:
                HeroUnlockProgress.SetEventCompletedForDebug(condition.TargetId, value > 0);
                break;
        }
    }

    // ════════════════════════════════════════
    // 第二页：Enemy（含 Boss——Boss 本质上也是 EnemyDatabase 里的一个敌人）
    // ════════════════════════════════════════

    private ScrollContainer BuildEnemyTab()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        _enemyContent = content;
        RefreshEnemyTab();
        return WrapInScroll(content);
    }

    private void RefreshEnemyTab()
    {
        if (_enemyContent == null)
        {
            return;
        }

        ClearChildren(_enemyContent);

        foreach (var enemy in EnemyDatabase.GetAllEnemies())
        {
            var enemyId = enemy.Id;
            var count = HeroUnlockProgress.GetEnemyKillCount(enemyId);
            var row = MakeRow(Localization.GetFmt("devpanel.enemy.row_fmt", enemy.Name, enemyId, count));

            row.AddChild(MakeButton(Localization.Get("devpanel.common.add1"), () =>
            {
                HeroUnlockProgress.AddEnemyKillCountForDebug(enemyId, 1);
                RefreshEnemyTab();
            }));
            row.AddChild(MakeButton(Localization.Get("devpanel.common.add10"), () =>
            {
                HeroUnlockProgress.AddEnemyKillCountForDebug(enemyId, 10);
                RefreshEnemyTab();
            }));
            row.AddChild(MakeButton(Localization.Get("devpanel.common.complete"), () =>
            {
                HeroUnlockProgress.SetEnemyKillCountForDebug(enemyId, RequiredAmountFor(HeroUnlockConditionType.KillEnemy, enemyId));
                RefreshEnemyTab();
            }));
            row.AddChild(MakeButton(Localization.Get("devpanel.common.reset"), () =>
            {
                HeroUnlockProgress.SetEnemyKillCountForDebug(enemyId, 0);
                RefreshEnemyTab();
            }));

            _enemyContent.AddChild(row);
        }
    }

    // ════════════════════════════════════════
    // 第三页：Event
    // ════════════════════════════════════════

    private ScrollContainer BuildEventTab()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        _eventContent = content;
        RefreshEventTab();
        return WrapInScroll(content);
    }

    private void RefreshEventTab()
    {
        if (_eventContent == null)
        {
            return;
        }

        ClearChildren(_eventContent);

        foreach (var eventData in EventDatabase.GetAllEvents())
        {
            var eventId = eventData.Id;
            var completed = HeroUnlockProgress.IsEventCompleted(eventId);
            var statusText = Localization.Get(completed ? "devpanel.event.status_completed" : "devpanel.event.status_incomplete");
            var row = MakeRow(Localization.GetFmt("devpanel.event.row_fmt", eventData.DisplayName, eventId, statusText));

            // "触发"和"完成"在当前 Progress 模型里是同一件事——事件只有"完成过/没完成过"
            // 这一个布尔状态，没有单独的"已触发但未完成"中间态；两个按钮都调用
            // SetEventCompletedForDebug(eventId, true)，保留两个按钮只是对应任务里
            // 明确要求的按钮布局。
            row.AddChild(MakeButton(Localization.Get("devpanel.event.trigger"), () =>
            {
                HeroUnlockProgress.SetEventCompletedForDebug(eventId, true);
                RefreshEventTab();
            }));
            row.AddChild(MakeButton(Localization.Get("devpanel.common.complete"), () =>
            {
                HeroUnlockProgress.SetEventCompletedForDebug(eventId, true);
                RefreshEventTab();
            }));
            row.AddChild(MakeButton(Localization.Get("devpanel.common.reset"), () =>
            {
                HeroUnlockProgress.SetEventCompletedForDebug(eventId, false);
                RefreshEventTab();
            }));

            _eventContent.AddChild(row);
        }
    }

    // ════════════════════════════════════════
    // 第四页：Achievement
    // ════════════════════════════════════════

    private ScrollContainer BuildAchievementTab()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 8);
        _achievementContent = content;
        RefreshAchievementTab();
        return WrapInScroll(content);
    }

    private void RefreshAchievementTab()
    {
        if (_achievementContent == null)
        {
            return;
        }

        ClearChildren(_achievementContent);

        foreach (AchievementType type in Enum.GetValues(typeof(AchievementType)))
        {
            var current = HeroUnlockProgress.GetAchievementProgress(type);
            var required = RequiredAmountFor(HeroUnlockConditionType.Achievement, type.ToString());
            var row = MakeRow(required > 0
                ? Localization.GetFmt("devpanel.achievement.row_with_required_fmt", type, current, required)
                : Localization.GetFmt("devpanel.achievement.row_fmt", type, current));

            row.AddChild(MakeButton(Localization.Get("devpanel.common.add1"), () =>
            {
                HeroUnlockProgress.AddAchievementProgressForDebug(type, 1);
                RefreshAchievementTab();
            }));
            row.AddChild(MakeButton(Localization.Get("devpanel.common.add5"), () =>
            {
                HeroUnlockProgress.AddAchievementProgressForDebug(type, 5);
                RefreshAchievementTab();
            }));
            row.AddChild(MakeButton(Localization.Get("devpanel.common.complete"), () =>
            {
                // 没有任何英雄的解锁条件引用这项成就时，用一个足够大的数保证"完成"依然有意义。
                HeroUnlockProgress.SetAchievementProgressForDebug(type, required > 0 ? required : 999999);
                RefreshAchievementTab();
            }));
            row.AddChild(MakeButton(Localization.Get("devpanel.common.reset"), () =>
            {
                HeroUnlockProgress.SetAchievementProgressForDebug(type, 0);
                RefreshAchievementTab();
            }));

            _achievementContent.AddChild(row);
        }
    }

    /// <summary>
    /// 在 HeroUnlockDatabase 里找到第一条"类型 = conditionType 且 TargetId = targetId"的条件，
    /// 返回它的 RequiredAmount；找不到则返回 0（调用方应自行决定兜底值，见"完成"按钮）。
    /// </summary>
    private static int RequiredAmountFor(HeroUnlockConditionType conditionType, string targetId)
    {
        foreach (var characterId in HeroUnlockDatabase.GetAllConditionCharacterIds())
        {
            var condition = HeroUnlockDatabase.Get(characterId);
            if (condition != null && condition.ConditionType == conditionType && condition.TargetId == targetId)
            {
                return condition.RequiredAmount;
            }
        }

        return conditionType == HeroUnlockConditionType.KillEnemy ? 1 : 0;
    }

    // ════════════════════════════════════════
    // 第五页：Inventory
    // ════════════════════════════════════════

    private ScrollContainer BuildInventoryTab()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);

        content.AddChild(MakeActionRow(Localization.Get("devpanel.inventory.add_gold"), () => GameManager.AddGold(100)));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.inventory.remove_gold"), () => GameManager.AddGold(-100)));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.inventory.add_forage"), () => GameManager.AddForage(5)));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.inventory.add_random_equipment"), AddRandomEquipmentForDebug));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.inventory.add_random_runbuff"), AddRandomRunBuffForDebug));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.inventory.add_random_chip"), AddRandomChipForDebug));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.inventory.clear_backpack"), InventoryManager.ClearBackpack));

        return WrapInScroll(content);
    }

    private static void AddRandomEquipmentForDebug()
    {
        var pool = EquipmentDatabase.GetAllEquipments().Where(e => e.CanAppearInRandomPool).ToList();
        if (pool.Count == 0)
        {
            return;
        }

        var chosen = pool[new Random().Next(pool.Count)];
        GameManager.AddEquipment(chosen.Id, EquipmentGainSource.Developer);
    }

    private static void AddRandomRunBuffForDebug()
    {
        var pool = RunBuffDatabase.GetAll().ToList();
        if (pool.Count == 0)
        {
            return;
        }

        var chosen = pool[new Random().Next(pool.Count)];
        RunBuffManager.AddStacks(chosen.Id, 1);
    }

    private static void AddRandomChipForDebug()
    {
        var roll = new Random().Next(4);
        switch (roll)
        {
            case 0: GameManager.IncrementDefenseChipCount(); break;
            case 1: GameManager.IncrementAttackChipCount(); break;
            case 2: GameManager.IncrementKnowledgeChipCount(); break;
            default: GameManager.IncrementExpansionChipCount(); break;
        }
    }

    // ════════════════════════════════════════
    // 阵营命运（Faction Fate）
    // ════════════════════════════════════════

    private Label? _factionFateStatusLabel;

    private ScrollContainer BuildFactionFateTab()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);

        var hint = new Label
        {
            Text = "群/魏阵营命运；以下按钮只操作正式状态，不伪造UI文本。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        hint.AddThemeFontSizeOverride("font_size", 14);
        hint.AddThemeColorOverride("font_color", new Color(0.7f, 0.72f, 0.78f));
        content.AddChild(hint);

        content.AddChild(MakeActionRow("强制选择改命", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.Reroll))));
        content.AddChild(MakeActionRow("强制选择倒手重铸", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.Reforge))));
        content.AddChild(MakeActionRow("强制选择天命骰", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.Dice))));
        content.AddChild(MakeActionRow("强制选择命运抉择", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.DoubleInitialChoice))));
        content.AddChild(MakeActionRow("重置阵营命运", () => RunFactionFateDebugAction(FactionFateManager.DebugReset)));

        var rerollRow = new HBoxContainer();
        rerollRow.AddThemeConstantOverride("separation", 10);
        rerollRow.AddChild(new Label { Text = "设置改命剩余次数", CustomMinimumSize = new Vector2(320, 0) });
        foreach (var n in new[] { 3, 1, 0 })
        {
            var value = n;
            rerollRow.AddChild(MakeButton(value.ToString(), () => RunFactionFateDebugAction(() => FactionFateManager.DebugSetRerollRemaining(value))));
        }
        content.AddChild(rerollRow);

        var reforgeRow = new HBoxContainer();
        reforgeRow.AddThemeConstantOverride("separation", 10);
        reforgeRow.AddChild(new Label { Text = "设置倒手重铸已出售次数", CustomMinimumSize = new Vector2(320, 0) });
        foreach (var n in new[] { 0, 3 })
        {
            var value = n;
            reforgeRow.AddChild(MakeButton(value.ToString(), () => RunFactionFateDebugAction(() => FactionFateManager.DebugSetReforgeSaleCount(value))));
        }
        content.AddChild(reforgeRow);

        content.AddChild(MakeActionRow("手动投掷骰子", () => RunFactionFateDebugAction(FactionFateManager.DebugRollDiceNow)));

        var diceRow = new HBoxContainer();
        diceRow.AddThemeConstantOverride("separation", 10);
        diceRow.AddChild(new Label { Text = "指定下一次骰子结果", CustomMinimumSize = new Vector2(320, 0) });
        for (var n = 1; n <= 6; n++)
        {
            var value = n;
            diceRow.AddChild(MakeButton(value.ToString(), () => RunFactionFateDebugAction(() => FactionFateManager.DebugSetNextDiceResult(value))));
        }
        content.AddChild(diceRow);

        var weiHint = new Label
        {
            Text = "魏阵营命运（Phase 1）：",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        weiHint.AddThemeFontSizeOverride("font_size", 14);
        weiHint.AddThemeColorOverride("font_color", new Color(0.7f, 0.72f, 0.78f));
        content.AddChild(weiHint);

        content.AddChild(MakeActionRow("强制【黄金储备】", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.WeiGoldenReserve))));
        content.AddChild(MakeActionRow("强制【备用电池】", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.WeiBackupBattery))));
        content.AddChild(MakeActionRow("强制【战后清算】", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.WeiBattleSettlement))));
        content.AddChild(MakeActionRow("强制【官方特权】", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.WeiOfficialPrivilege))));
        content.AddChild(MakeActionRow("强制【双线征伐】", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.WeiDoubleCampaign))));
        content.AddChild(MakeActionRow("强制【芯片超频】", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.WeiDoubleChipEffect))));
        content.AddChild(MakeActionRow("手动生成本章额外Boss节点", () => RunFactionFateDebugAction(() => FactionFateManager.TryAddExtraBossNodeForChapter(GameManager.CurrentChapter))));

        var shuHint = new Label
        {
            Text = "蜀阵营命运（Phase 2）：",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        shuHint.AddThemeFontSizeOverride("font_size", 14);
        shuHint.AddThemeColorOverride("font_color", new Color(0.7f, 0.72f, 0.78f));
        content.AddChild(shuHint);

        content.AddChild(MakeActionRow("强制【兵谋同源】", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.ShuUnifiedTactics))));
        content.AddChild(MakeActionRow("强制【先机】", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.ShuInitiative))));
        content.AddChild(MakeActionRow("强制【锦囊蓄势】", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.ShuTrickResource))));
        content.AddChild(MakeActionRow("强制【桃酒同源】", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.ShuPeachWineUnity))));
        content.AddChild(MakeActionRow("强制【奇策先发】", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.ShuFirstTrickFree))));

        var wuHint = new Label
        {
            Text = "吴阵营命运（Phase 3）：",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        wuHint.AddThemeFontSizeOverride("font_size", 14);
        wuHint.AddThemeColorOverride("font_color", new Color(0.7f, 0.72f, 0.78f));
        content.AddChild(wuHint);

        content.AddChild(MakeActionRow("强制【百工夺赏】", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.WuBossRewardReplace))));
        content.AddChild(MakeActionRow("强制【多宝架】", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.WuAccessorySlot))));
        content.AddChild(MakeActionRow("强制【先行采购】", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.WuFirstPurchaseFree))));
        content.AddChild(MakeActionRow("强制【工坊重选】", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.WuEquipmentReselect))));
        content.AddChild(MakeActionRow("强制【战后分红】", () => RunFactionFateDebugAction(() => FactionFateManager.DebugForceFate(FactionFateIds.WuBattleDividend))));

        var status = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        status.AddThemeFontSizeOverride("font_size", 16);
        status.AddThemeColorOverride("font_color", new Color(0.86f, 0.82f, 0.64f));
        _factionFateStatusLabel = status;
        content.AddChild(status);

        RefreshFactionFateTab();
        return WrapInScroll(content);
    }

    private void RunFactionFateDebugAction(Action action)
    {
        action();
        RefreshFactionFateTab();
    }

    private void RefreshFactionFateTab()
    {
        if (_factionFateStatusLabel == null)
        {
            return;
        }

        var diceHistory = string.Join(" / ", FactionFateManager.DiceHistory);
        _factionFateStatusLabel.Text =
            $"当前命运：{FactionFateManager.CurrentFateId ?? "（无）"}\n" +
            $"改命剩余次数：{FactionFateManager.RerollRemaining}\n" +
            $"倒手重铸已出售次数：{FactionFateManager.ReforgeSaleCount}\n" +
            $"天命骰历史：{(diceHistory.Length == 0 ? "（无）" : diceHistory)}";
    }

    // ════════════════════════════════════════
    // 第六页：Battle
    // ════════════════════════════════════════

    private ScrollContainer BuildBattleTab()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);

        var hint = new Label
        {
            Text = Localization.Get("devpanel.battle.hint"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        hint.AddThemeFontSizeOverride("font_size", 14);
        hint.AddThemeColorOverride("font_color", new Color(0.7f, 0.72f, 0.78f));
        content.AddChild(hint);

        content.AddChild(MakeActionRow(Localization.Get("devpanel.battle.next_node"), () => DebugSelectNode?.Invoke(FindNextSelectableNodeId() ?? string.Empty)));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.battle.goto_boss"), () => DebugSelectNode?.Invoke(FindBossNodeId() ?? string.Empty)));
        content.AddChild(MakeActionRow("直接进入第四章 4-1", () => GoToChapterFourNode("battle_4_1")));
        content.AddChild(MakeActionRow("直接进入第四章 4-3", () => GoToChapterFourNode("battle_4_3")));
        content.AddChild(MakeActionRow("直接进入第四章Boss", () => GoToChapterFourNode("boss_4")));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.battle.win_now"), () => WithBattle(battle =>
        {
            for (var slot = 0; slot < 4; slot++)
            {
                battle.SetTutorialEnemyHealth(0, battle.GetTutorialEnemyHealth(slot), slot);
            }
        })));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.battle.lose_now"), () => WithBattle(battle => battle.SetTutorialPlayerHealth(0))));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.battle.full_heal"), () => WithBattle(battle => battle.SetTutorialPlayerHealth(battle.GetTutorialPlayerMaxHealth()))));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.battle.add_mana"), () => WithBattle(battle => battle.SetTutorialPlayerMana(battle.GetTutorialPlayerMana() + 5))));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.battle.clear_mana"), () => WithBattle(battle => battle.SetTutorialPlayerMana(0))));

        content.AddChild(BuildZhouTaiSection());
        content.AddChild(BuildStinkyMushroomSection());

        return WrapInScroll(content);
    }

    // ════════════════════════════════════════
    // 周泰【不屈】【奋激】调试
    // ════════════════════════════════════════

    private Control BuildZhouTaiSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);

        var hint = new Label
        {
            Text = "周泰【不屈】【奋激】；HP/费用/RuntimeStates 均只操作当前正在进行的真实战斗（不在战斗中则按钮无效）。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        hint.AddThemeFontSizeOverride("font_size", 14);
        hint.AddThemeColorOverride("font_color", new Color(0.7f, 0.72f, 0.78f));
        content.AddChild(hint);

        content.AddChild(MakeActionRow("强制触发周泰濒死", () => RunZhouTaiDebugAction(() => WithBattle(battle => battle.DebugForceZhouTaiDying()))));
        content.AddChild(MakeActionRow("重置本场不屈状态", () => RunZhouTaiDebugAction(() => WithBattle(battle => battle.DebugResetZhouTaiBuQuState()))));

        var diceRow = new HBoxContainer();
        diceRow.AddThemeConstantOverride("separation", 10);
        diceRow.AddChild(new Label { Text = "指定下一次骰子结果1~6", CustomMinimumSize = new Vector2(320, 0) });
        for (var n = 1; n <= 6; n++)
        {
            var value = n;
            diceRow.AddChild(MakeButton(value.ToString(), () => RunZhouTaiDebugAction(() => ForcedZhouTaiDiceResult = value)));
        }
        content.AddChild(diceRow);

        var fenjiRow = new HBoxContainer();
        fenjiRow.AddThemeConstantOverride("separation", 10);
        fenjiRow.AddChild(new Label { Text = "设置奋激层数", CustomMinimumSize = new Vector2(320, 0) });
        fenjiRow.AddChild(MakeButton("+2", () => RunZhouTaiDebugAction(() => GameManager.AddZhouTaiFenjiBonus(2))));
        fenjiRow.AddChild(MakeButton("+6", () => RunZhouTaiDebugAction(() => GameManager.AddZhouTaiFenjiBonus(6))));
        fenjiRow.AddChild(MakeButton("归零", () => RunZhouTaiDebugAction(() => GameManager.AddZhouTaiFenjiBonus(-GameManager.ZhouTaiFenjiBonus))));
        content.AddChild(fenjiRow);

        content.AddChild(MakeActionRow("快速模式：切换", () => RunZhouTaiDebugAction(() => FastMode = !FastMode)));

        var testRow = new HBoxContainer();
        testRow.AddThemeConstantOverride("separation", 10);
        testRow.AddChild(MakeButton("测试骰子动画（成功）", () => PlayZhouTaiTestDiceOverlay(true)));
        testRow.AddChild(MakeButton("测试骰子动画（失败）", () => PlayZhouTaiTestDiceOverlay(false)));
        testRow.AddChild(MakeButton("跳过动画", () => _zhouTaiTestDiceOverlay?.SkipNow()));
        content.AddChild(testRow);

        var status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        status.AddThemeFontSizeOverride("font_size", 16);
        status.AddThemeColorOverride("font_color", new Color(0.86f, 0.82f, 0.64f));
        _zhouTaiStatusLabel = status;
        content.AddChild(status);

        RefreshZhouTaiStatus();
        return content;
    }

    private void RunZhouTaiDebugAction(Action action)
    {
        action();
        RefreshZhouTaiStatus();
    }

    private void RefreshZhouTaiStatus()
    {
        if (_zhouTaiStatusLabel == null)
        {
            return;
        }

        _zhouTaiStatusLabel.Text =
            $"奋激累计伤害加值：+{GameManager.ZhouTaiFenjiBonus}\n" +
            $"下一次强制骰子结果：{(ForcedZhouTaiDiceResult?.ToString() ?? "（无，正常随机）")}\n" +
            $"快速模式：{(FastMode ? "开启" : "关闭")}";
    }

    // ════════════════════════════════════════
    // 恶臭蘑菇（第二章下水道稀有事件）调试
    // ════════════════════════════════════════

    private Control BuildStinkyMushroomSection()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);

        var hint = new Label
        {
            Text = "恶臭蘑菇：三色随机分配用 GameManager.EventRewardRandom 洗牌，本局固定；下方按钮直接指定排列（选项①②③依次对应）或强制资源状态用于测试三个消耗选项。",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        hint.AddThemeFontSizeOverride("font_size", 14);
        hint.AddThemeColorOverride("font_color", new Color(0.7f, 0.72f, 0.78f));
        content.AddChild(hint);

        var presetRow = new HBoxContainer();
        presetRow.AddThemeConstantOverride("separation", 10);
        presetRow.AddChild(new Label { Text = "指定三色排列（①/②/③）", CustomMinimumSize = new Vector2(320, 0) });
        AddStinkyPresetButton(presetRow, "红/黄/绿", EquipmentIds.StinkyMushroomRed, EquipmentIds.StinkyMushroomYellow, EquipmentIds.StinkyMushroomGreen);
        AddStinkyPresetButton(presetRow, "黄/绿/红", EquipmentIds.StinkyMushroomYellow, EquipmentIds.StinkyMushroomGreen, EquipmentIds.StinkyMushroomRed);
        AddStinkyPresetButton(presetRow, "绿/红/黄", EquipmentIds.StinkyMushroomGreen, EquipmentIds.StinkyMushroomRed, EquipmentIds.StinkyMushroomYellow);
        AddStinkyPresetButton(presetRow, "红/绿/黄", EquipmentIds.StinkyMushroomRed, EquipmentIds.StinkyMushroomGreen, EquipmentIds.StinkyMushroomYellow);
        AddStinkyPresetButton(presetRow, "黄/红/绿", EquipmentIds.StinkyMushroomYellow, EquipmentIds.StinkyMushroomRed, EquipmentIds.StinkyMushroomGreen);
        AddStinkyPresetButton(presetRow, "绿/黄/红", EquipmentIds.StinkyMushroomGreen, EquipmentIds.StinkyMushroomYellow, EquipmentIds.StinkyMushroomRed);
        content.AddChild(presetRow);

        content.AddChild(MakeActionRow("强制电量<20（设为10）", () =>
        {
            GameManager.AddPower(10 - GameManager.Power);
            RefreshStinkyMushroomStatus();
        }));

        content.AddChild(MakeActionRow("强制无合法芯片（清空攻/防/知芯片）", () =>
        {
            GameManager.ClearAllChips();
            RefreshStinkyMushroomStatus();
        }));

        content.AddChild(MakeActionRow("强制最大生命值不足（设为5）", () =>
        {
            GameManager.AddMaxHp(5 - GameManager.MaxHP);
            RefreshStinkyMushroomStatus();
        }));

        var status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        status.AddThemeFontSizeOverride("font_size", 16);
        status.AddThemeColorOverride("font_color", new Color(0.86f, 0.82f, 0.64f));
        _stinkyMushroomStatusLabel = status;
        content.AddChild(status);

        RefreshStinkyMushroomStatus();
        return content;
    }

    private void AddStinkyPresetButton(HBoxContainer row, string label, string idOpt1, string idOpt2, string idOpt3)
    {
        row.AddChild(MakeButton(label, () =>
        {
            GameManager.DebugSetStinkyMushroomColorAssignment(new List<string> { idOpt1, idOpt2, idOpt3 });
            RefreshStinkyMushroomStatus();
        }));
    }

    private void RefreshStinkyMushroomStatus()
    {
        if (_stinkyMushroomStatusLabel == null)
        {
            return;
        }

        _stinkyMushroomStatusLabel.Text =
            $"事件是否已出现：{(GameManager.HasSeenEventInRun("event_stinky_mushroom") ? "是" : "否")}\n" +
            $"当前电量：{GameManager.Power} / 最大电量：{GameManager.MaxPower}\n" +
            $"芯片：攻{GameManager.AttackChipCount} 防{GameManager.DefenseChipCount} 知{GameManager.KnowledgeChipCount}\n" +
            $"最大生命值：{GameManager.MaxHP}";
    }

    /// <summary>
    /// 测试专用：脱离真实战斗，直接把 DiceRollOverlay 挂在调试面板自身节点下播放一次骰子
    /// 动画，仅用于视觉验收，不产生任何真实的技能后果（不改 HP/费用/奋激加值）。
    /// </summary>
    private void PlayZhouTaiTestDiceOverlay(bool success)
    {
        var overlay = new DiceRollOverlay();
        AddChild(overlay);
        _zhouTaiTestDiceOverlay = overlay;
        RunZhouTaiTestDiceOverlay(overlay, success ? 6 : 2, success);
    }

    private async void RunZhouTaiTestDiceOverlay(DiceRollOverlay overlay, int result, bool success)
    {
        await overlay.PlayAsync(
            result,
            success,
            "测试：判定成功",
            "这是成功文案预览，仅供视觉验收。",
            "测试：判定失败",
            "这是失败文案预览，仅供视觉验收。",
            FastMode);
        overlay.QueueFree();
        if (_zhouTaiTestDiceOverlay == overlay)
        {
            _zhouTaiTestDiceOverlay = null;
        }
    }

    private void WithBattle(Action<BattleManager> action)
    {
        var battle = GetActiveBattleManager?.Invoke();
        if (battle != null)
        {
            action(battle);
        }
    }

    // 调试专用：直接跳到第四章的指定节点（4-1/4-3/Boss），不要求先打完前三章。
    // 若当前不在第四章，先用 DebugGoToChapter(4) 建好第四章地图，再复用既有的
    // DebugSelectNode（= MainFlow.OnNodeSelected）直接进入该节点的战斗。
    private void GoToChapterFourNode(string nodeId)
    {
        if (GameManager.CurrentChapter != 4)
        {
            GameManager.DebugGoToChapter(4);
        }

        DebugSelectNode?.Invoke(nodeId);
    }

    private static string? FindNextSelectableNodeId()
    {
        foreach (var node in GameManager.Nodes)
        {
            if (GameManager.IsNodeUnlocked(node.Id) && !GameManager.IsNodeCleared(node.Id))
            {
                return node.Id;
            }
        }

        return null;
    }

    private static string? FindBossNodeId()
    {
        // 排除魏·双线征伐 的额外Boss节点（Id 形如 "boss_extra_ch1"），否则"跳转到Boss"
        // 调试按钮在该命运下会先跳到额外Boss而不是本章真正的主Boss。
        foreach (var node in GameManager.Nodes)
        {
            if (node.Type == MapNodeType.Boss && !node.Id.StartsWith("boss_extra_ch", StringComparison.Ordinal))
            {
                return node.Id;
            }
        }

        return null;
    }

    // ════════════════════════════════════════
    // 第七页：Save
    // ════════════════════════════════════════

    private ScrollContainer BuildSaveTab()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);

        content.AddChild(MakeActionRow(Localization.Get("devpanel.save.reset_all_progress"), () =>
        {
            HeroUnlockProgress.ResetAll();
            RefreshHeroTab();
            RefreshEnemyTab();
            RefreshEventTab();
            RefreshAchievementTab();
        }));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.save.reset_heroes"), () =>
        {
            HeroUnlockProgress.ResetHeroesForDebug();
            RefreshHeroTab();
        }));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.save.reset_events"), () =>
        {
            HeroUnlockProgress.ResetEventsForDebug();
            RefreshEventTab();
        }));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.save.reset_achievements"), () =>
        {
            HeroUnlockProgress.ResetAchievementsForDebug();
            RefreshAchievementTab();
        }));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.save.clear_save"), () =>
        {
            // 项目目前没有落地到磁盘的存档系统，"清空存档"和"重置所有 Progress"
            // 是同一件事——两个入口都只清空 HeroUnlockProgress，不触碰
            // GameManager 的当前 Run 状态（那部分由 GameManager.ResetRunData 负责，
            // 不属于这次任务范围）。
            HeroUnlockProgress.ResetAll();
            RefreshHeroTab();
            RefreshEnemyTab();
            RefreshEventTab();
            RefreshAchievementTab();
        }));

        return WrapInScroll(content);
    }

    private static HBoxContainer MakeActionRow(string label, Action onPressed)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(MakeButton(label, onPressed));
        return row;
    }

    // ════════════════════════════════════════
    // 永久图鉴（Codex）调试
    // ════════════════════════════════════════

    private Label? _codexStatusLabel;
    private LineEdit? _codexCardTypeInput;
    private LineEdit? _codexCardUsesInput;
    private LineEdit? _codexEnemyIdInput;
    private LineEdit? _codexEnemyDefeatsInput;

    private ScrollContainer BuildCodexTab()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);

        var hint = new Label
        {
            Text = Localization.Get("debug.codex.hint"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        hint.AddThemeFontSizeOverride("font_size", 14);
        hint.AddThemeColorOverride("font_color", new Color(0.7f, 0.72f, 0.78f));
        content.AddChild(hint);

        content.AddChild(MakeActionRow(Localization.Get("debug.codex.unlock_all"), () => RunCodexDebugAction(CodexService.DebugUnlockAll)));
        content.AddChild(MakeActionRow(Localization.Get("debug.codex.reset"), () => RunCodexDebugAction(CodexService.DebugReset)));
        content.AddChild(MakeActionRow(Localization.Get("debug.codex.save"), () => RunCodexDebugAction(CodexService.SaveIfDirty)));

        content.AddChild(MakeActionRow(Localization.Get("debug.codex.find_orphans"), () =>
        {
            var orphans = CodexService.FindOrphanEntries();
            SetCodexStatus(orphans.Count == 0
                ? Localization.Get("debug.codex.no_orphans")
                : Localization.GetFmt("debug.codex.orphans_found_fmt", orphans.Count) + "\n" + string.Join("\n", orphans));
        }));

        content.AddChild(MakeActionRow(Localization.Get("debug.codex.export_json"), () =>
        {
            SetCodexStatus(CodexService.ExportJson());
        }));

        var cardRow = MakeRow(Localization.Get("debug.codex.card_uses"));
        _codexCardTypeInput = new LineEdit { PlaceholderText = Localization.Get("debug.codex.card_type_placeholder"), CustomMinimumSize = new Vector2(160, 0) };
        _codexCardUsesInput = new LineEdit { PlaceholderText = Localization.Get("debug.codex.uses_placeholder"), CustomMinimumSize = new Vector2(80, 0), Text = "1" };
        cardRow.AddChild(_codexCardTypeInput);
        cardRow.AddChild(_codexCardUsesInput);
        cardRow.AddChild(MakeButton(Localization.Get("debug.codex.add"), () => RunCodexDebugAction(() =>
        {
            if (Enum.TryParse<CardType>(_codexCardTypeInput?.Text.Trim(), true, out var cardType)
                && int.TryParse(_codexCardUsesInput?.Text.Trim(), out var uses))
            {
                CodexService.RecordCardUsed(cardType, uses);
                SetCodexStatus(Localization.GetFmt("debug.codex.card_uses_added_fmt", cardType, uses));
            }
            else
            {
                SetCodexStatus(Localization.Get("debug.codex.invalid_card_input"));
            }
        })));
        content.AddChild(cardRow);

        var enemyRow = MakeRow(Localization.Get("debug.codex.enemy_defeats"));
        _codexEnemyIdInput = new LineEdit { PlaceholderText = Localization.Get("debug.codex.enemy_id_placeholder"), CustomMinimumSize = new Vector2(160, 0) };
        _codexEnemyDefeatsInput = new LineEdit { PlaceholderText = Localization.Get("debug.codex.turn_placeholder"), CustomMinimumSize = new Vector2(100, 0), Text = "1" };
        enemyRow.AddChild(_codexEnemyIdInput);
        enemyRow.AddChild(_codexEnemyDefeatsInput);
        enemyRow.AddChild(MakeButton(Localization.Get("debug.codex.add"), () => RunCodexDebugAction(() =>
        {
            var enemyId = _codexEnemyIdInput?.Text.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(enemyId) || EnemyDatabase.GetEnemy(enemyId) == null)
            {
                SetCodexStatus(Localization.Get("debug.codex.invalid_enemy_input"));
                return;
            }

            var turn = int.TryParse(_codexEnemyDefeatsInput?.Text.Trim(), out var parsedTurn) ? parsedTurn : 1;
            CodexService.RecordEnemyEncounter(enemyId, 0);
            CodexService.RecordEnemyDefeated(enemyId, turn);
            SetCodexStatus(Localization.GetFmt("debug.codex.enemy_defeat_added_fmt", enemyId, turn));
        })));
        content.AddChild(enemyRow);

        var status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        status.AddThemeFontSizeOverride("font_size", 14);
        status.AddThemeColorOverride("font_color", new Color(0.86f, 0.82f, 0.64f));
        _codexStatusLabel = status;
        content.AddChild(status);

        return WrapInScroll(content);
    }

    private void RunCodexDebugAction(Action action)
    {
        action();
        SetCodexStatus("操作完成。");
    }

    private void SetCodexStatus(string text)
    {
        if (_codexStatusLabel != null)
        {
            _codexStatusLabel.Text = text;
        }
    }

    // ════════════════════════════════════════
    // 教程（战斗教程 / 战斗外教程）
    // ════════════════════════════════════════

    private Label? _tutorialStatusLabel;

    private ScrollContainer BuildTutorialTab()
    {
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);

        content.AddChild(MakeActionRow(Localization.Get("devpanel.tutorial.open_select"), () => OpenTutorialSelectRequested?.Invoke()));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.tutorial.open_combat"), () => OpenCombatTutorialRequested?.Invoke()));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.tutorial.open_meta"), () => OpenMetaTutorialRequested?.Invoke()));

        var jumpLabel = new Label { Text = Localization.Get("devpanel.tutorial.jump_hint") };
        jumpLabel.AddThemeFontSizeOverride("font_size", 14);
        jumpLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.72f, 0.78f));
        content.AddChild(jumpLabel);

        // 只在战斗外教程场景实际挂载期间有意义（TutorialManager.CurrentModuleId == Meta）；
        // 战斗教程场景内点击这些按钮不会有任何效果（不影响正式流程）。
        var jumpFlow = new HBoxContainer();
        jumpFlow.AddThemeConstantOverride("separation", 8);
        content.AddChild(jumpFlow);
        foreach (var stepId in MetaTutorialDatabase.OrderedStepIds)
        {
            var id = stepId;
            jumpFlow.AddChild(MakeButton(id, () =>
            {
                TutorialManager.JumpToStep(id);
                RefreshTutorialTab();
            }));
        }

        content.AddChild(MakeActionRow(Localization.Get("devpanel.tutorial.reset_combat"), () =>
        {
            TutorialProgress.ResetCombat();
            RefreshTutorialTab();
        }));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.tutorial.reset_meta"), () =>
        {
            TutorialProgress.ResetMeta();
            RefreshTutorialTab();
        }));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.tutorial.reset_all"), () =>
        {
            TutorialProgress.ResetAll();
            RefreshTutorialTab();
        }));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.tutorial.reset_integrated"), () =>
        {
            TutorialIntegratedProgress.ResetForReplay();
            RefreshTutorialTab();
        }));
        content.AddChild(MakeActionRow(Localization.Get("devpanel.tutorial.reset_hints"), () =>
        {
            FirstTimeHintManager.ResetAll();
            RefreshTutorialTab();
        }));

        var status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        status.AddThemeFontSizeOverride("font_size", 16);
        status.AddThemeColorOverride("font_color", new Color(0.86f, 0.82f, 0.64f));
        _tutorialStatusLabel = status;
        content.AddChild(status);

        RefreshTutorialTab();
        return WrapInScroll(content);
    }

    private void RefreshTutorialTab()
    {
        if (_tutorialStatusLabel == null)
        {
            return;
        }

        _tutorialStatusLabel.Text =
            $"当前教程模块：{TutorialManager.CurrentModuleId ?? "（无）"}\n" +
            $"当前步骤：{TutorialManager.CurrentStep?.Id ?? "（无）"}\n" +
            $"战斗教程完成：{(TutorialProgress.IsCombatCompleted ? "是" : "否")}\n" +
            $"战斗外教程完成：{(TutorialProgress.IsMetaCompleted ? "是" : "否")}";
    }
}
