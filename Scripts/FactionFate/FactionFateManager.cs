//////////////////////////////////////////////////////////
// 文件：Scripts/FactionFate/FactionFateManager.cs
//
// 模块：Faction Fate System
//
// 职责：
// 1. 承载四个阵营命运的随机判定、进度消耗、发放逻辑与 HUD 构建。
// 2. 为其它模块（GameManager/ChoicePanel/EventController/InventoryController/
//    DeveloperDebugPanel）提供清晰、稳定的调用边界。
//
// 不负责：
// × 从哪个池子抽装备/技能/背包候选——这部分完全交给调用方闭包，本类只关心
//   "阵营命运判定 + 次数消耗 + 合法性校验" 这一层协议，避免和各 Provider 的
//   具体参数耦合。
//////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Faction Fate System 的公开类：FactionFateManager。
///
/// 静态类——和项目里 GameManager/RunBuffManager 等"整局只有一份状态"的 Manager
/// 风格一致，内部持有一个可整体替换的 FactionFateState。
/// </summary>
public static class FactionFateManager
{
    private static FactionFateState _state = new();

    public static string? CurrentFateId => _state.SelectedFateId;
    public static int RerollRemaining => _state.RerollRemaining;
    public static int ReforgeSaleCount => _state.ReforgeSaleCount;
    public static IReadOnlyList<int> DiceHistory => _state.ChapterDiceHistory;
    public static ChipChoiceType? QunForcedChipType =>
        _state.SelectedFateId == FactionFateIds.QunChipReconfiguration
            ? _state.QunForcedChipType
            : null;
    public static int QunEquipmentUpgradesRemaining =>
        _state.SelectedFateId == FactionFateIds.QunFirstTwoEquipmentUpgrade
            ? Math.Max(0, 2 - _state.QunEquipmentUpgradeCount)
            : 0;

    public static string? LastRollResultMessage { get; private set; }
    public static string? LastLegendaryMessage { get; private set; }

    /// <summary>
    /// 新开 Run 必须重新随机命运（硬约束：新 Run 重新随机，本 Run 内重进地图/读档不重随），
    /// 所以这里整体替换状态，而不是保留旧的 SelectedFateId。由 GameManager.ResetRunData() 调用。
    /// </summary>
    public static void ResetForNewRun()
    {
        _state = new FactionFateState();
        LastRollResultMessage = null;
        LastLegendaryMessage = null;
    }

    /// <summary>
    /// 按当前角色所属阵营随机获得阵营命运（Phase 1 起支持群/魏；蜀/吴候选列表暂为空，
    /// 对应阵营的角色会因为 candidates.Count == 0 而直接跳过，不会异常获得命运）。
    /// 由 GameManager.SelectCharacter() 在 RollChapterVariant() 之后调用一次。
    /// </summary>
    public static void RollFateIfEligible()
    {
        if (_state.SelectedFateId != null) return; // 防御性幂等：正常流程下 ResetForNewRun 已经清空，这里只是双保险

        var faction = GameManager.CurrentCharacter?.Faction;
        var candidates = faction switch
        {
            Faction.Qun => FactionFateDatabase.AllQunFates,
            Faction.Wei => FactionFateDatabase.AllWeiFates,
            Faction.Shu => FactionFateDatabase.AllShuFates,
            Faction.Wu => FactionFateDatabase.AllWuFates,
            _ => null
        };
        if (candidates == null || candidates.Count == 0) return;

        var index = GameManager.EventRewardRandom.Next(candidates.Count);
        _state.SelectedFateId = candidates[index].Id;
        ConfigureSelectedFateState();
        MainFlow.TryShowFirstTimeHint(FirstTimeHintManager.HintIds.FirstFactionFate);

        // 魏·黄金储备：命中即刻发放黄金雕像与100金币，只在“真正抽中这个命运”的这一次生效，
        // 不需要额外的幂等字段——因为 _state.SelectedFateId 本身只会被赋值一次（上面的早退保证）。
        if (_state.SelectedFateId == FactionFateIds.WeiGoldenReserve)
        {
            // 先给固定的100金币再加入会增益金币的雕像，保证“+100额外金币”是精确数值，
            // 不会被同一命运刚获得的黄金雕像额外放大成120。
            GameManager.AddGold(100);
            GameManager.AddEquipment(EquipmentIds.GoldenStatue, EquipmentGainSource.RunInitialization);
        }
        // 魏·备用电池：命中即刻最大电量永久+25，同步把当前电量也+25。
        else if (_state.SelectedFateId == FactionFateIds.WeiBackupBattery)
        {
            GameManager.GrantFactionFateMaxPowerBonus(25);
        }
    }

    private static void ConfigureSelectedFateState()
    {
        _state.ExtraInitialEventChoicesRemaining =
            _state.SelectedFateId == FactionFateIds.DoubleInitialChoice ? 1 : 0;
        _state.QunEquipmentUpgradeCount = 0;
        _state.QunForcedChipType = _state.SelectedFateId == FactionFateIds.QunChipReconfiguration
            ? new[] { ChipChoiceType.Skill, ChipChoiceType.Attack, ChipChoiceType.Defense }[
                GameManager.EventRewardRandom.Next(3)]
            : null;
    }

    // ======================================================
    // 改命（Reroll）
    // ======================================================

    /// <summary>
    /// 改命·装备/技能/背包类三选一的单选项重抽。调用方传入"当前展示的全部选项id"（含被替换项自身），
    /// 传入一个"给定排除id集合、抽1个新候选"的委托（由调用方按各自 Provider 类型构造，因为
    /// EquipmentChoiceProvider/SkillChoiceProvider/InventoryChoiceProvider 各自的抽取参数不同，
    /// 这里不关心具体池，只关心"排除id进去、候选项出来"这一层协议）。
    /// </summary>
    public static bool TryRerollChoiceOption(
        ChoiceOption current,
        IReadOnlyList<string> allShownIds,
        Func<IReadOnlyList<string>, ChoiceOption?> drawExcluding,
        out ChoiceOption? replacement,
        bool allowDuplicateWithOtherOptions = false)
    {
        replacement = null;
        if (_state.SelectedFateId != FactionFateIds.Reroll) return false;
        if (_state.RerollRemaining <= 0) return false;

        var exclude = new List<string>(allShownIds);
        var candidate = drawExcluding(exclude);
        if (candidate == null
            || candidate.Id == current.Id
            || (!allowDuplicateWithOtherOptions && allShownIds.Contains(candidate.Id)))
        {
            return false; // 没有合法新选项：不消耗次数
        }

        replacement = candidate;
        _state.RerollRemaining -= 1;
        return true;
    }

    /// <summary>
    /// 改命·初始事件选项重抽。只替换玩家点击刷新对应的一个选项，并由
    /// <see cref="InitialEventManager"/> 保证新结果不与另外两个选项重复或产生Tag冲突。
    /// </summary>
    public static bool TryRerollInitialEventOption(
        InitialEventDefinition current,
        IReadOnlyList<InitialEventDefinition> allShown,
        out InitialEventDefinition? replacement)
    {
        replacement = null;
        if (_state.SelectedFateId != FactionFateIds.Reroll) return false;
        if (_state.RerollRemaining <= 0) return false;

        replacement = InitialEventManager.PickReplacement(current, allShown);
        if (replacement == null) return false;

        _state.RerollRemaining -= 1;
        return true;
    }

    /// <summary>
    /// 事件选项版本的改命——先只做最小骨架，因为当前没有任何事件数据实际把
    /// CanFactionReroll 设为 true，这里只保证"如果未来有数据设置了这个字段，
    /// 机制能正常工作"，不做过度设计。
    /// </summary>
    public static bool TryRerollEventOption(EventOption option, Func<ChoiceOption?> drawReplacement, out ChoiceOption? replacement)
    {
        replacement = null;
        if (_state.SelectedFateId != FactionFateIds.Reroll) return false;
        if (_state.RerollRemaining <= 0) return false;
        if (!option.CanFactionReroll || string.IsNullOrEmpty(option.RefreshPoolId)) return false;

        var candidate = drawReplacement();
        if (candidate == null) return false;

        replacement = candidate;
        _state.RerollRemaining -= 1;
        return true;
    }

    // ======================================================
    // 倒手重铸（Reforge）
    // ======================================================

    public static bool ShouldOfferReforgeChoice(OwnedEquipment item)
    {
        if (_state.SelectedFateId != FactionFateIds.Reforge) return false;
        if (_state.ReforgeSaleCount >= 3) return false;
        return InventoryManager.IsEligibleForReforge(item.Definition);
    }

    /// <summary>
    /// 在统一的同稀有度重铸池中随机选取一件候选。找不到候选返回 false。
    /// </summary>
    public static bool TryFindReforgeCandidate(EquipmentDefinition original, out EquipmentDefinition? candidate)
    {
        candidate = null;
        var candidates = InventoryManager.GetReforgeCandidates(original);
        if (candidates.Count == 0) return false;
        candidate = candidates[GameManager.EventRewardRandom.Next(candidates.Count)];
        return true;
    }

    /// <summary>
    /// 出售流程结束时调用一次——无论玩家选了正常出售还是重铸，也无论当时有没有合法重铸候选，
    /// 只要走到了"前3次出售判定"这一步就算消耗了一次机会（需求原文："仍按照一次出售机会处理"）。
    /// </summary>
    public static void ResolveSaleOpportunity()
    {
        if (_state.SelectedFateId != FactionFateIds.Reforge) return;
        if (_state.ReforgeSaleCount >= 3) return;
        _state.ReforgeSaleCount += 1;
    }

    // ======================================================
    // 天命骰（Dice）
    // ======================================================

    public static void RollChapterDiceIfNeeded(int chapter)
    {
        if (_state.SelectedFateId != FactionFateIds.Dice) return;
        if (_state.LastDiceRolledChapter == chapter) return; // 幂等：同一章节不重复投

        var rolls = ConsumeChapterDiceRolls();
        var total = rolls.Sum();
        _state.LastDiceRolledChapter = chapter;
        _state.ChapterDiceHistory.Add(total);

        LastLegendaryMessage = null;
        var rewards = new List<string>();
        var gold = total * 50;
        GameManager.AddGold(gold); // 黄金雕像倍率由 AddGold 统一处理。
        rewards.Add($"获得{gold}金币。");

        if (rolls.Contains(6))
        {
            var rare = TryGrantRandomEquipmentByRarity(EquipmentRarity.Rare);
            rewards.Add(rare != null
                ? $"骰出6点，获得稀有装备【{Localization.GetName(rare)}】。"
                : "骰出6点，但当前没有可获得的稀有装备。");
        }

        // 任意两颗（含三颗）骰子出现相同数字即为传奇奖励；不再要求旧规则的“三同”。
        var hasMatchingNumbers = rolls.Distinct().Count() < rolls.Count;
        var sorted = rolls.OrderBy(value => value).ToArray();
        var isStraight = sorted[1] == sorted[0] + 1 && sorted[2] == sorted[1] + 1;
        if (hasMatchingNumbers)
        {
            var legendary = TryGrantRandomEquipmentByRarity(EquipmentRarity.Legendary);
            LastLegendaryMessage = legendary != null
                ? $"【天命已成】骰出相同数字，获得随机传奇装备【{Localization.GetName(legendary)}】。"
                : "【天命已成】骰出相同数字，但当前没有可获得的传奇装备。";
        }
        else if (isStraight)
        {
            var epic = TryGrantRandomEquipmentByRarity(EquipmentRarity.Epic);
            LastLegendaryMessage = epic != null
                ? $"【天命有兆】三骰构成顺子，获得随机史诗装备【{Localization.GetName(epic)}】。"
                : "【天命有兆】三骰构成顺子，但当前没有可获得的史诗装备。";
        }

        if (total <= 6)
        {
            var common = TryGrantRandomEquipmentByRarity(EquipmentRarity.Common);
            rewards.Add(common != null
                ? $"总点数不高于6，获得普通装备【{Localization.GetName(common)}】。"
                : "总点数不高于6，但当前没有可获得的普通装备。");
        }

        var resultLine = $"【天命骰】本章三骰：{string.Join(" / ", rolls)}（总计{total}）\n{string.Join("\n", rewards)}";
        LastRollResultMessage = resultLine;
        _state.PendingDicePresentation = new FactionFateDicePresentationRequest(
            chapter,
            rolls,
            total,
            resultLine,
            LastLegendaryMessage ?? string.Empty);
    }

    private static List<int> ConsumeChapterDiceRolls()
    {
        var rolls = _state.DebugForcedNextDiceRolls is { Count: 3 }
            ? new List<int>(_state.DebugForcedNextDiceRolls)
            : new List<int>
            {
                GameManager.EventRewardRandom.Next(1, 7),
                GameManager.EventRewardRandom.Next(1, 7),
                GameManager.EventRewardRandom.Next(1, 7)
            };
        _state.DebugForcedNextDiceRolls = null;
        return rolls;
    }

    /// <summary>
    /// 取出一次待播放的章节天命骰结果。奖励在投骰时已经结算，本方法只把一次性表现请求
    /// 交给MainFlow；成功取出后立即清空，避免反复进入地图重复播放。
    /// </summary>
    public static bool TryConsumePendingDicePresentation(out FactionFateDicePresentationRequest? request)
    {
        request = _state.PendingDicePresentation;
        _state.PendingDicePresentation = null;
        return request != null;
    }

    /// <summary>
    /// 从"当前允许随机获得"的指定稀有度装备池里随机选1个。
    /// 找不到候选返回 null。复用 RewardManager.CanAppearInRandomEquipmentReward 网关，
    /// 与项目里其它随机装备奖励（例如 RandomEquipmentByRarityRewardAction）遵守同一套排除规则，
    /// 不新建第二套过滤逻辑。
    /// </summary>
    private static EquipmentDefinition? GetRandomEquipmentByRarity(EquipmentRarity rarity)
    {
        var candidates = new List<EquipmentDefinition>();
        foreach (var def in EquipmentDatabase.GetAllEquipments())
        {
            if (def.Rarity == rarity && RewardManager.CanAppearInRandomEquipmentReward(def))
            {
                candidates.Add(def);
            }
        }

        if (candidates.Count == 0) return null;
        return candidates[GameManager.EventRewardRandom.Next(candidates.Count)];
    }

    /// <summary>
    /// 从指定稀有度装备池随机选取并立即发放。用于奖励在结果确定时就必须入包的场景（如天命骰）。
    /// </summary>
    private static EquipmentDefinition? TryGrantRandomEquipmentByRarity(
        EquipmentRarity rarity,
        EquipmentGainSource source = EquipmentGainSource.GameplayReward)
    {
        var chosen = GetRandomEquipmentByRarity(rarity);
        if (chosen == null) return null;
        var added = GameManager.AddEquipment(chosen.Id, source);
        return added?.Definition;
    }

    // ======================================================
    // 调试接口——全部只操作正式状态/调用正式方法，不允许伪造UI文本。
    // ======================================================

    public static void DebugForceFate(string fateId)
    {
        _state.SelectedFateId = fateId;
        ConfigureSelectedFateState();
    }

    public static void DebugReset()
    {
        ResetForNewRun();
    }

    public static void DebugSetRerollRemaining(int value)
    {
        _state.RerollRemaining = Math.Max(0, value);
    }

    public static void DebugSetReforgeSaleCount(int value)
    {
        _state.ReforgeSaleCount = Math.Clamp(value, 0, 3);
    }

    public static void DebugSetNextDiceResult(int value)
    {
        var roll = Math.Clamp(value, 1, 6);
        _state.DebugForcedNextDiceRolls = new List<int> { roll, roll, roll };
    }

    /// <summary>为一整个章节的三次天命骰注入固定结果，供回归和开发调试使用。</summary>
    public static void DebugSetNextDiceRolls(int first, int second, int third)
    {
        _state.DebugForcedNextDiceRolls = new List<int>
        {
            Math.Clamp(first, 1, 6),
            Math.Clamp(second, 1, 6),
            Math.Clamp(third, 1, 6)
        };
    }

    /// <summary>仅供回归测试固定芯片重构本局选中的目标芯片。</summary>
    public static void DebugSetQunForcedChipType(ChipChoiceType type)
    {
        if (_state.SelectedFateId == FactionFateIds.QunChipReconfiguration
            && type is ChipChoiceType.Skill or ChipChoiceType.Attack or ChipChoiceType.Defense)
        {
            _state.QunForcedChipType = type;
        }
    }

    /// <summary>
    /// 调试面板用：强制把"上次投掷章节"设为一个不可能等于当前章节的值，
    /// 这样可以在调试面板里反复触发投掷用于测试，不受"同一章节只投一次"的幂等限制干扰。
    /// </summary>
    public static void DebugRollDiceNow()
    {
        _state.LastDiceRolledChapter = -1;
        RollChapterDiceIfNeeded(GameManager.CurrentChapter);
    }

    // ======================================================
    // HUD 构建
    // ======================================================

    public static Control? BuildHudPanel()
    {
        if (_state.SelectedFateId == null) return null;
        var def = FactionFateDatabase.Get(_state.SelectedFateId);
        if (def == null) return null;

        var box = new VBoxContainer();
        var title = new Label
        {
            Text = Localization.GetFmt(
                "factionfate.hud.title_fmt",
                BuildFactionShortName(def.Faction),
                Localization.Get(def.NameKey))
        };
        title.AddThemeFontSizeOverride("font_size", 18);
        box.AddChild(title);
        var progress = new Label { Text = BuildProgressSummary() };
        progress.AddThemeFontSizeOverride("font_size", 16);
        progress.AddThemeColorOverride("font_color", new Color(0.86f, 0.82f, 0.64f));
        box.AddChild(progress);

        var wrapper = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        wrapper.AddChild(box);
        wrapper.MouseEntered += () => TooltipManager.Show(BuildTooltipText(), wrapper);
        wrapper.MouseExited += TooltipManager.Hide;
        return wrapper;
    }

    private static string BuildProgressSummary()
    {
        return _state.SelectedFateId switch
        {
            FactionFateIds.Reroll => Localization.GetFmt("factionfate.remaining_fmt", _state.RerollRemaining, 3),
            FactionFateIds.Reforge => Localization.GetFmt("factionfate.remaining_fmt", 3 - _state.ReforgeSaleCount, 3),
            FactionFateIds.Dice => BuildRecentDiceSummary(),
            FactionFateIds.WeiBackupBattery => $"{GameManager.Power}/{GameManager.MaxPower}",
            FactionFateIds.WuFirstPurchaseFree => Localization.Get(IsFirstPurchaseFreeAvailable() ? "factionfate.first_purchase_available" : "factionfate.first_purchase_used"),
            // 蜀·连策：两个计数器（普通锦囊/顺手牵羊）实际存在 context.Player.RuntimeStates 里，
            // 属于"单场战斗内"的数据。BuildHudPanel 只被 InventoryController/ShopController/
            // MapController/EventController 这几个"战斗外场景"调用（已确认，战斗内 UI 不走这个
            // 入口），FactionFateManager 也没有、也不应该新增一个指向"当前战斗 Player 实例"的静态
            // 引用（会破坏战斗系统"状态只在 BattleContext/Player 内传递"的既有边界）。因此这里始终
            // 展示通用兜底文案，不尝试读取战斗内数据——和 Qun 天命骰在"无历史记录"时的空文案兜底是
            // 同一思路（没有可展示数据时不强行拼凑）。
            FactionFateIds.DoubleInitialChoice => Localization.Get(
                _state.ExtraInitialEventChoicesRemaining > 0
                    ? "factionfate.extra_initial_choice_available"
                    : "factionfate.extra_initial_choice_complete"),
            // 其余魏/蜀命运（黄金储备/战后清算/官方特权/双线征伐/兵谋同源/先机/木牛流马）没有需要
            // 持续追踪的"剩余次数"类进度，用通用的阵营+简述兜底，避免 HUD 出现空白。
            _ when FactionFateDatabase.Get(_state.SelectedFateId) is { } def => BuildGenericProgressSummary(def),
            _ => string.Empty
        };
    }

    /// <summary>
    /// 没有专属进度摘要的命运（多数魏命运 + 尚未实现的蜀/吴命运）统一兜底文案：
    /// “阵营：{阵营}”+简述，保证 HUD 不出现空白进度行。
    /// </summary>
    private static string BuildGenericProgressSummary(FactionFateDefinition def)
    {
        return Localization.GetFmt("factionfate.faction_fmt", BuildFactionShortName(def.Faction));
    }

    private static string BuildFactionShortName(Faction faction)
    {
        return faction switch
        {
            Faction.Qun => Localization.Get("faction.qun"),
            Faction.Wei => Localization.Get("faction.wei"),
            Faction.Shu => Localization.Get("faction.shu"),
            Faction.Wu => Localization.Get("faction.wu"),
            _ => faction.ToString()
        };
    }

    private static string BuildRecentDiceSummary()
    {
        var h = _state.ChapterDiceHistory;
        if (h.Count == 0) return string.Empty;
        var take = h.Count > 3 ? h.GetRange(h.Count - 3, 3) : h;
        return Localization.GetFmt("factionfate.recent_rolls_fmt", string.Join(" / ", take));
    }

    public static string BuildTooltipText()
    {
        var def = FactionFateDatabase.Get(_state.SelectedFateId);
        if (def == null) return string.Empty;
        return $"{Localization.Get(def.NameKey)}\n{Localization.GetFmt("factionfate.faction_fmt", BuildFactionShortName(def.Faction))}\n{Localization.Get(def.DescriptionKey)}\n\n{BuildProgressSummary()}\n{Localization.Get("factionfate.reset_scope_run")}";
    }

    // ======================================================
    // 魏·战后清算 / 魏·官方特权 / 魏·双线征伐
    // ======================================================

    /// <summary>
    /// 魏·战后清算按本场由玩家实际造成的对敌伤害发放金币（最多200）；
    /// 吴·战后分红按战斗结束瞬间当前生命值的绝对值等额发放金币，不设上限。
    /// </summary>
    public static void SettleBattleVictoryGold(int currentHp, int playerDamageDealt)
    {
        var gold = _state.SelectedFateId switch
        {
            FactionFateIds.WeiBattleSettlement => Math.Min(200, Math.Max(0, playerDamageDealt)),
            FactionFateIds.WuBattleDividend => Math.Abs(currentHp),
            _ => 0
        };
        if (gold <= 0) return;
        GameManager.AddGold(gold);
    }

    /// <summary>
    /// 魏·官方特权：事件选项的金币成本 ×25%（向上取整，最低1）。只影响 EventCostType.Gold
    /// 这一条路径的显示与实际扣费——两处必须调用同一个函数，否则会出现"UI显示折扣价、实际扣原价"的bug。
    /// </summary>
    public static int ApplyEventGoldCostDiscount(int originalAmount)
    {
        if (_state.SelectedFateId != FactionFateIds.WeiOfficialPrivilege) return originalAmount;
        if (originalAmount <= 0) return originalAmount;
        return Math.Max(1, (int)Math.Ceiling(originalAmount * 0.25));
    }

    /// <summary>
    /// 魏·双线征伐：每章地图构建后追加一个独立的额外Boss节点（若该章有合法候选）。
    /// 必须在主Boss节点已经加入 GameManager.MapNodes 之后调用，只是"多加一条"，不替代/不修改主Boss节点。
    /// </summary>
    public static void TryAddExtraBossNodeForChapter(int chapter)
    {
        if (_state.SelectedFateId != FactionFateIds.WeiDoubleCampaign) return;
        if (!WeiExtraBossData.CandidatesByChapter.TryGetValue(chapter, out var candidates) || candidates.Count == 0
            || !WeiExtraBossData.ExtraBossStageIdByChapter.TryGetValue(chapter, out var stageId))
        {
            GD.Print($"[WeiDoubleCampaign] 第{chapter}章找不到第二个合法Boss候选，跳过额外Boss生成。");
            return;
        }

        var nodeId = $"boss_extra_ch{chapter}";
        if (GameManager.MapNodes.Any(n => n.Id == nodeId)) return; // 防御性幂等，避免重复建图时重复添加

        // 具体是候选池里的哪个敌人，交给 StageDatabase.RollEncounter 在真正进入这个节点时
        // 按等权重现场抽取（和主Boss节点的抽取时机、机制完全一致），这里只负责挂上 StageIdOverride，
        // 不在建图阶段就把敌人id写死。
        var extraNode = new MapNode
        {
            Id = nodeId,
            Name = "额外挑战",
            Type = MapNodeType.Boss,
            StageIndex = 7,
            StageIdOverride = stageId,
            PowerCost = ContinueExploreConfig.ExtraBossEnergyCost,
            // 额外Boss固定消耗15电量，不受"Boss默认0电量"或其它新电量规则影响——
            // 用 UseFixedEnergyCost 接入 GameManager.GetNodeEnergyCost 的统一计算入口，
            // 数值来自 ContinueExploreConfig.ExtraBossEnergyCost，不再散落字面量。
            UseFixedEnergyCost = true,
            FixedEnergyCost = ContinueExploreConfig.ExtraBossEnergyCost
            // 不设置 NextNodeIds：这是一个独立可选分支节点，不作为任何其它节点的前置条件。
        };

        // 关键：GameManager.AllStagesCleared()（决定本章是否完成/触发通关）读取的是
        // MapNodes[^1]（列表最后一个节点）是否已清空，而不是按 Id/Type 查找主Boss节点。
        // 如果直接 Add 到末尾，额外Boss节点会变成新的"最后一个节点"，导致清完额外Boss
        // 就误判整章通关、清完主Boss反而不算数。这里改用 Insert 插在主Boss节点之前，
        // 保证主Boss节点始终是列表最后一个元素，不改动 AllStagesCleared 本身。
        var mapNodes = GameManager.MapNodes;
        var insertIndex = mapNodes.Count > 0 ? mapNodes.Count - 1 : 0;
        mapNodes.Insert(insertIndex, extraNode);
        GameManager.UnlockedNodeIds.Add(nodeId);
    }

    // ======================================================
    // 蜀·兵谋同源 / 蜀·奇策增幅——供 BattleRules/Player/技能装备效果做简单布尔查询。
    // ======================================================

    /// <summary>
    /// 蜀·兵谋同源是否生效。供 BattleRules.IsShaAttackWithFactionCompat /
    /// IsAttackingTrickWithFactionCompat 内部调用，不在别处直接判断字符串。
    /// </summary>
    internal static bool IsShuUnifiedTacticsActive() => CurrentFateId == FactionFateIds.ShuUnifiedTactics;

    /// <summary>
    /// 群·命运抉择：消费一次额外初始事件选择。只有第一次初始事件完成时返回 true。
    /// </summary>
    public static bool TryConsumeExtraInitialEventChoice()
    {
        if (_state.SelectedFateId != FactionFateIds.DoubleInitialChoice
            || _state.ExtraInitialEventChoicesRemaining <= 0)
        {
            return false;
        }

        _state.ExtraInitialEventChoicesRemaining--;
        return true;
    }

    internal static bool IsShuEnemyZeroStartingManaActive() => CurrentFateId == FactionFateIds.ShuTrickResource;
    internal static bool IsShuPeachWineUnityActive() => CurrentFateId == FactionFateIds.ShuPeachWineUnity;
    internal static bool IsShuFirstTrickFreeActive() => CurrentFateId == FactionFateIds.ShuFirstTrickFree;

    // 旧【奇策增幅】已从命运池移除。保留这个兼容查询可防止旧存档/调试字符串使已删除的
    // 数值加成重新生效；它始终返回 false。
    internal static bool IsShuAmplificationActive() => false;

    /// <summary>
    /// 所有芯片的数值效果统一从这里读取：魏·芯片超频与群·芯片重构均为双倍，
    /// 防御芯片的生命值、攻击/知识芯片的伤害、技能芯片的技能数量都不各自硬编码倍率。
    /// </summary>
    public static int GetChipEffectMultiplier() => CurrentFateId is FactionFateIds.WeiDoubleChipEffect
        or FactionFateIds.QunChipReconfiguration ? 2 : 1;

    /// <summary>群·芯片重构使技能/扩容芯片替换概率统一变为原来的五倍。</summary>
    public static double GetChipSpecialReplacementChance(double baseChance)
    {
        return CurrentFateId == FactionFateIds.QunChipReconfiguration
            ? Math.Min(1.0, baseChance * 5.0)
            : baseChance;
    }

    /// <summary>
    /// 群·淬炼开局：前两件可正常获得、且能在统一升级重铸池找到高一品质候选的装备，
    /// 在进入背包前直接替换。事件/剧情专属装备也可作为本命运的原装备；
    /// 它们仍不能成为随机替换结果。无候选/传奇装备不消耗次数，避免吞掉奖励。
    /// </summary>
    public static bool TryFindQunEquipmentUpgradeCandidate(
        EquipmentDefinition original,
        EquipmentGainSource source,
        out EquipmentDefinition? candidate)
    {
        candidate = null;
        if (_state.SelectedFateId != FactionFateIds.QunFirstTwoEquipmentUpgrade
            || _state.QunEquipmentUpgradeCount >= 2
            || !CanTriggerQunEquipmentUpgrade(source))
        {
            return false;
        }

        var candidates = RewardManager.GetEquipmentUpgradeCandidates(
            original,
            allowExclusiveOriginal: true);
        if (candidates.Count == 0)
        {
            return false;
        }

        candidate = candidates[GameManager.EventRewardRandom.Next(candidates.Count)];
        return true;
    }

    /// <summary>只在替换装备真正成功进入背包后消费一次群·淬炼开局次数。</summary>
    public static void CommitQunEquipmentUpgrade()
    {
        if (_state.SelectedFateId == FactionFateIds.QunFirstTwoEquipmentUpgrade
            && _state.QunEquipmentUpgradeCount < 2)
        {
            _state.QunEquipmentUpgradeCount++;
        }
    }

    private static bool CanTriggerQunEquipmentUpgrade(EquipmentGainSource source)
    {
        return source is EquipmentGainSource.GameplayReward
            or EquipmentGainSource.ShopPurchase
            or EquipmentGainSource.EventReward
            or EquipmentGainSource.BattleReward
            or EquipmentGainSource.BossReward
            or EquipmentGainSource.ChoiceReward
            or EquipmentGainSource.EnemyDrop;
    }

    private const string ShuFirstTrickUsedKey = "shu_first_trick_free_used";

    internal static bool CanUseShuFirstTrickFree(Player player, CardType type)
    {
        return IsShuFirstTrickFreeActive()
            && player.Team == BattleTeam.Player
            && new Card(type).IsTrickCard
            && !player.RuntimeStates.ContainsKey(ShuFirstTrickUsedKey);
    }

    internal static void ConsumeShuFirstTrickFree(Player player)
    {
        player.RuntimeStates[ShuFirstTrickUsedKey] = true;
    }

    /// <summary>
    /// 蜀·木牛流马：第一章主Boss的首次奖励结算后发放一次。
    /// 奖励状态归命运管理器所有，避免重开奖励界面或重复回调重复加入装备。
    /// </summary>
    public static bool TryGrantShuMuNiuLiuMaAfterFirstBoss()
    {
        if (_state.SelectedFateId != FactionFateIds.ShuMuNiuLiuMaBossReward
            || _state.ShuMuNiuLiuMaBossRewardGranted)
        {
            return false;
        }

        var granted = GameManager.AddEquipment(EquipmentIds.MuNiuLiuMa, EquipmentGainSource.BossReward);
        if (granted == null)
        {
            return false;
        }

        _state.ShuMuNiuLiuMaBossRewardGranted = true;
        return true;
    }

    // ======================================================
    // 吴·百工夺赏 / 吴·先行采购 / 吴·工坊重选
    // ======================================================

    /// <summary>
    /// 吴·百工夺赏：按章节号抽取一件额外 Boss 掉落。
    /// 只选取定义，不在这里入包，使其能与普通 Boss 奖励一起显示、并在玩家点击领取时
    /// 统一走 RewardManager.ApplyReward。第4章及以后没有配置规则时返回 null。
    /// </summary>
    public static EquipmentDefinition? GetBossRewardBonus(int chapter)
    {
        var rarity = chapter switch
        {
            1 => EquipmentRarity.Rare,
            2 => EquipmentRarity.Epic,
            3 => EquipmentRarity.Legendary,
            _ => (EquipmentRarity?)null
        };
        if (rarity == null)
        {
            GD.Print($"[WuBossRewardBonus] 第{chapter}章没有配置百工夺赏额外掉落规则。");
            return null;
        }

        return GetRandomEquipmentByRarity(rarity.Value);
    }

    /// <summary>
    /// 吴·先行采购：进入每个商店时开启一次"第一件商品免费"机会。
    /// 刷新商店不会调用本入口，因此同一家商店刷新后不会恢复免费机会。
    /// </summary>
    public static void BeginShopVisit()
    {
        if (_state.SelectedFateId != FactionFateIds.WuFirstPurchaseFree) return;
        _state.CurrentShopVisitSerial += 1;
    }

    /// <summary>
    /// 吴·先行采购：当前商店访问中，第一件装备是否仍可免费。
    /// </summary>
    public static bool IsFirstPurchaseFreeAvailable()
    {
        return _state.SelectedFateId == FactionFateIds.WuFirstPurchaseFree
            && _state.CurrentShopVisitSerial > 0
            && _state.FirstPurchaseFreeUsedShopVisitSerial != _state.CurrentShopVisitSerial;
    }

    /// <summary>
    /// 吴·先行采购：消费当前商店访问的免费购买机会。
    /// </summary>
    public static void ConsumeFirstPurchaseFree()
    {
        _state.FirstPurchaseFreeUsedShopVisitSerial = _state.CurrentShopVisitSerial;
    }

    /// <summary>
    /// 吴·工坊重选：新获得的可重铸装备会从统一的同稀有度候选池中给出最多3项选择。
    /// 所有品质遵守同一规则：仅剧情/角色专属装备被排除；已拥有装备也保留为候选。
    /// </summary>
    public static void TryTriggerEquipmentReselect(OwnedEquipment original)
    {
        var originalDef = original.Definition;
        if (!InventoryManager.IsEligibleForReforge(originalDef))
        {
            return;
        }

        var candidates = InventoryManager.GetReforgeCandidates(originalDef);

        if (candidates.Count == 0) return; // 没有合法候选：保留原装备，不弹窗，不销毁

        var pickCount = Math.Min(3, candidates.Count);
        var pool = new List<EquipmentDefinition>(candidates);
        var chosenCandidates = new List<EquipmentDefinition>();
        for (var i = 0; i < pickCount; i++)
        {
            var index = GameManager.EventRewardRandom.Next(pool.Count);
            chosenCandidates.Add(pool[index]);
            pool.RemoveAt(index);
        }

        var options = new List<ChoiceOption>();
        foreach (var candidate in chosenCandidates)
        {
            options.Add(EquipmentChoiceProvider.ToChoiceOption(candidate));
        }

        var request = new ChoiceRequest
        {
            Title = Localization.Get("factionfate.workshop_reselect_title"),
            Description = Localization.Get("factionfate.wu_fate_equipment_reselect.desc"),
            AllowCancel = false,
            Options = options
        };

        // 工坊重选发生在商店、事件、战斗奖励等任意现有页面中，必须使用覆盖式模态选择，
        // 不能通过 MainFlow.SwitchTo 销毁当前页面。只有常驻 MainFlow 确认能够承载弹层后
        // 才销毁原装备，避免异常场景下出现“装备已删但没有选择界面”。
        var shown = MainFlow.TryShowFactionFateChoicePanel(request, result =>
        {
            if (result.Cancelled || result.Id == null) return;
            var added = GameManager.AddEquipment(result.Id, EquipmentGainSource.FactionFateReplacement);
            if (added != null)
            {
                added.AcquiredFrom = "FactionFateReselect";
            }
        });

        if (shown)
        {
            InventoryManager.DestroyEquipment(original.InstanceId);
        }
    }

}
