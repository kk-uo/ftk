//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/FactionFateChoiceAndDiceRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证【改命】覆盖初始事件和技能候选刷新。
// 2. 验证刷新失败不消耗次数，刷新成功只消耗一次。
// 3. 验证标准芯片三选一固定提供攻击、知识、防御。
// 4. 验证【天命骰】每章只生成一次可消费的表现请求。
//
// 不负责：
// × 模拟玩家完整点击流程。
// × 验证骰子动画的视觉构图。
//
// 主要依赖：
// FactionFateManager
// InitialEventManager
// ChoiceProvider
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 群阵营命运选择刷新与章节骰子表现请求的 Headless 回归入口。
/// </summary>
public partial class FactionFateChoiceAndDiceRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行回归用例，并通过进程退出码返回测试状态。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestInitialEventPoolConfiguration();
            TestInitialEventReroll();
            TestSkillReroll();
            TestChangYinExcludedFromSkillChoices();
            TestCharacterExclusiveSkillsEnterChoicePool();
            TestFixedChipChoices();
            TestExpansionChipReplacement();
            TestSkillChipReplacementAndGrant();
            TestInitialEventChipEnhancedChoices();
            TestUpdatedFactionFatePools();
            TestQunChipReconfiguration();
            TestQunDirectChipRewardsRemainUnreplaced();
            TestDoubleInitialEventChoice();
            TestShuBattleRules();
            TestWeiChipMultiplier();
            TestWeiBattleSettlementAndShuBossReward();
            TestFailedRerollDoesNotConsume();
            TestChapterDicePresentationRequest();
            GD.Print($"FACTION_FATE_CHOICE_DICE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"FACTION_FATE_CHOICE_DICE_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestInitialEventReroll()
    {
        ResetRerollFate();
        var shown = InitialEventManager.Pick3();
        Assert(shown.Count == 3, "初始事件测试未生成三个候选");

        var success = FactionFateManager.TryRerollInitialEventOption(
            shown[0],
            shown,
            out var replacement);
        Assert(success && replacement != null, "初始事件选项刷新失败");
        Assert(shown.All(option => option.Id != replacement!.Id), "初始事件刷新出了当前已显示的选项");

        var remainingTags = shown.Skip(1)
            .SelectMany(option => option.Tags)
            .ToHashSet(StringComparer.Ordinal);
        Assert(!replacement!.Tags.Any(remainingTags.Contains), "初始事件刷新破坏了Tag互斥规则");
        Assert(FactionFateManager.RerollRemaining == 2, "初始事件刷新没有准确消耗一次改命");
    }

    private void TestInitialEventPoolConfiguration()
    {
        var expectedIds = Enumerable.Range(1, 24).Select(index => $"ie_{index:00}").ToArray();
        Assert(InitialEventPool.AllEntries.Select(entry => entry.Id).SequenceEqual(expectedIds),
            "初始事件池没有严格重建为指定的24项配置");

        var initialEventTags = InitialEventPool.AllEntries.ToDictionary(entry => entry.Id, entry => entry.Tags.ToHashSet());
        Assert(initialEventTags["ie_03"].SetEquals(new[] { "A", "E" })
               && initialEventTags["ie_11"].SetEquals(new[] { "D", "E" })
               && initialEventTags["ie_15"].SetEquals(new[] { "F" })
               && initialEventTags["ie_18"].SetEquals(new[] { "F" })
               && initialEventTags["ie_23"].SetEquals(new[] { "F" }),
            "初始事件的多重互斥标签没有按最新规则配置");

        for (var i = 0; i < 500; i++)
        {
            var selectedTags = InitialEventManager.Pick3().SelectMany(entry => entry.Tags).ToArray();
            Assert(selectedTags.Distinct(StringComparer.Ordinal).Count() == selectedTags.Length,
                "初始事件三选一出现了相同字母标签");
        }

        GameManager.BeginNewRun();
        InitialEventPool.AllEntries.Single(entry => entry.Id == "ie_04").Apply();
        Assert(GameManager.InitialEventChipChoiceEnhanced, "初始事件④没有启用芯片三选一强化");
        Assert(GameManager.InitialEventMysteriousChipChance == 0.05,
            "初始事件④的神秘芯片概率不是5%");
        Assert(GameManager.InitialEventEnhancedChipExpansionChance == 0.10
            && GameManager.InitialEventEnhancedSkillChipChance == 0.10,
            "初始事件④没有将扩容芯片和技能芯片概率提升至10%");

        GameManager.BeginNewRun();
        var initialGold = GameManager.Gold;
        InitialEventPool.AllEntries.Single(entry => entry.Id == "ie_05").Apply();
        Assert(GameManager.Gold == initialGold + 100, "初始事件⑤没有发放100金币");

        GameManager.BeginNewRun();
        InitialEventPool.AllEntries.Single(entry => entry.Id == "ie_07").Apply();
        Assert(GameManager.InitialEventAllShopsBlackMarket, "初始事件⑦没有将商店切换为黑市");

        GameManager.BeginNewRun();
        InitialEventPool.AllEntries.Single(entry => entry.Id == "ie_08").Apply();
        Assert(GameManager.InitialEventShopRefreshFree && GameManager.InitialEventExtraShopRefreshCount == 1,
            "初始事件⑧没有同时提供免费刷新和额外刷新次数");

        GameManager.BeginNewRun();
        InitialEventPool.AllEntries.Single(entry => entry.Id == "ie_13").Apply();
        Assert(GameManager.KnowledgeChipCount == 4, "初始事件⑬没有发放4个知识芯片");

        GameManager.BeginNewRun();
        InitialEventPool.AllEntries.Single(entry => entry.Id == "ie_22").Apply();
        Assert(GameManager.InitialEventAttackTrickChoicePending, "初始事件㉒没有建立攻击性锦囊三选一请求");

        foreach (var cardType in new[] { CardType.ArrowBarrage, CardType.NanmanInvasion, CardType.IronChain })
        {
            GameManager.BeginNewRun();
            Assert(GameManager.TryAddInitialEventAttackTrick(cardType), $"初始事件㉒未接受候选牌{cardType}");
            Assert(GameManager.HasPlayerCardType(cardType), $"初始事件㉒选择{cardType}后没有写入本局出牌栏状态");
        }

        Assert(!GameManager.TryAddInitialEventAttackTrick(CardType.Kill), "初始事件㉒错误接受了候选池外的普通杀");

        GameManager.BeginNewRun();
        InitialEventPool.AllEntries.Single(entry => entry.Id == "ie_23").Apply();
        Assert(InventoryManager.GetAllOwned().Any(item => item.Definition.Id == EquipmentIds.Reforger),
            "初始事件㉓没有获得【重铸器】");

        GameManager.BeginNewRun();
        InitialEventPool.AllEntries.Single(entry => entry.Id == "ie_24").Apply();
        Assert(GameManager.InitialEventStartingManaBonus == 1 && GameManager.GetPlayerInitialMana() == GameManager.InitialMana + 1,
            "初始事件㉔没有使每场战斗初始费用+1");
    }

    private void TestSkillReroll()
    {
        ResetRerollFate();
        var shown = new SkillChoiceProvider
        {
            // 【畅饮】退出随机选择池后，当前普通/稀有通用池共有三个合法技能。
            // 此处使用二选一，确保仍有一个真实候选可用于验证改命刷新成功路径；
            // 三项全部展示后的“无候选且不扣次数”由 TestFailedRerollDoesNotConsume 覆盖。
            Count = 2,
            Rarities = new[] { SkillRarity.Common, SkillRarity.Rare },
            IncludeBossSkills = false,
            IncludeOtherCharacterExclusiveSkills = false
        }.CreateChoices();
        Assert(shown.Count == 2, "技能刷新测试未生成两个候选");

        var allIds = shown.Select(option => option.Id).ToArray();
        var success = FactionFateManager.TryRerollChoiceOption(
            shown[0],
            allIds,
            exclude => new SkillChoiceProvider
            {
                Count = 1,
                Rarities = new[] { SkillRarity.Common, SkillRarity.Rare },
                IncludeBossSkills = false,
                IncludeOtherCharacterExclusiveSkills = false,
                ExcludedSkillIds = exclude
            }.CreateChoices().FirstOrDefault(),
            out var replacement);

        Assert(success && replacement != null, "技能选项刷新失败");
        Assert(!allIds.Contains(replacement!.Id), "技能刷新出了当前已显示的技能");
        Assert(FactionFateManager.RerollRemaining == 2, "技能刷新没有准确消耗一次改命");
    }

    private void TestChangYinExcludedFromSkillChoices()
    {
        GameManager.BeginNewRun();
        var controlledPool = new[]
        {
            SkillIds.ChangYin,
            SkillIds.QianXin,
            SkillIds.Plague,
            SkillIds.ChangZui
        };

        var twoChoices = new SkillChoiceProvider
        {
            Count = 2,
            Randomize = false,
            SkillPool = controlledPool,
            IncludeBossSkills = false,
            IncludeOtherCharacterExclusiveSkills = false
        }.CreateChoices();
        Assert(twoChoices.Count == 2, "排除畅饮后技能二选一候选数量不足");
        Assert(twoChoices.All(option => option.Id != SkillIds.ChangYin), "技能二选一错误出现畅饮");

        var threeChoices = new SkillChoiceProvider
        {
            Count = 3,
            Randomize = false,
            SkillPool = controlledPool,
            IncludeBossSkills = false,
            IncludeOtherCharacterExclusiveSkills = false
        }.CreateChoices();
        Assert(threeChoices.Count == 3, "排除畅饮后技能三选一候选数量不足");
        Assert(threeChoices.All(option => option.Id != SkillIds.ChangYin), "技能三选一错误出现畅饮");
        Assert(!SkillDatabase.ChangYinSkill().CanAppearInRandomChoicePool, "畅饮定义未关闭随机技能选择池开关");
    }

    private void TestCharacterExclusiveSkillsEnterChoicePool()
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.SunCe);
        var characterSkillIds = new[]
        {
            SkillIds.Longdan,
            SkillIds.Biyue,
            SkillIds.PoJun
        };

        var choices = new SkillChoiceProvider
        {
            Count = 3,
            Randomize = false,
            SkillPool = characterSkillIds,
            IncludeBossSkills = false,
            IncludeOtherCharacterExclusiveSkills = true
        }.CreateChoices();

        Assert(choices.Count == 3, "角色专属技能没有进入普通技能三选一候选池");
        Assert(choices.Select(option => option.Id).ToHashSet().SetEquals(characterSkillIds),
            "技能三选一没有保留其它角色的专属技能候选");
    }

    private void TestFixedChipChoices()
    {
        var shown = new ChipChoiceProvider
        {
            Count = 3,
            ChipTypes = new[]
            {
                ChipChoiceType.Attack,
                ChipChoiceType.Knowledge,
                ChipChoiceType.Defense
            },
            // 这个用例验证固定的基础候选顺序；概率替换由下方专用用例覆盖。
            ExpansionReplacementChance = 0,
            SkillChipReplacementChance = 0
        }.CreateChoices();

        Assert(shown.Count == 3, "标准芯片选择没有生成三个候选");
        Assert(shown[0].Payload is ChipChoiceType.Attack, "第一个芯片候选不是攻击芯片");
        Assert(shown[1].Payload is ChipChoiceType.Knowledge, "第二个芯片候选不是知识芯片");
        Assert(shown[2].Payload is ChipChoiceType.Defense, "第三个芯片候选不是防御芯片");
        Assert(shown.Select(option => option.Id).Distinct().Count() == 3, "标准芯片选择出现重复候选");
    }

    private void TestExpansionChipReplacement()
    {
        var shown = new ChipChoiceProvider
        {
            Count = 3,
            ChipTypes = new[]
            {
                ChipChoiceType.Attack,
                ChipChoiceType.Knowledge,
                ChipChoiceType.Defense
            },
            ExpansionReplacementChance = ChipChoiceProvider.ExpansionReplacementChanceDefault,
            SkillChipReplacementChance = 0,
            RandomDoubleProvider = () => 0
        }.CreateChoices();

        Assert(shown.Count == 3, "扩容芯片替换后不再是三选一");
        Assert(shown.Count(option => option.Payload is ChipChoiceType.Expansion) == 1,
            "1%概率命中时没有恰好替换一个选项为扩容芯片");
        Assert(shown.Count(option => option.Payload is ChipChoiceType.Attack or ChipChoiceType.Knowledge or ChipChoiceType.Defense) == 2,
            "扩容芯片替换后基础芯片候选数量错误");
        Assert(shown.Any(option => option.Id == "expansion" && option.Title == Localization.Get("chip.expansion.name")),
            "扩容芯片候选缺少本地化显示信息");
    }

    private void TestSkillChipReplacementAndGrant()
    {
        var shown = new ChipChoiceProvider
        {
            Count = 3,
            ChipTypes = new[]
            {
                ChipChoiceType.Attack,
                ChipChoiceType.Knowledge,
                ChipChoiceType.Defense
            },
            ExpansionReplacementChance = 0,
            SkillChipReplacementChance = ChipChoiceProvider.SkillChipReplacementChanceDefault,
            RandomDoubleProvider = () => 0
        }.CreateChoices();

        Assert(shown.Count(option => option.Payload is ChipChoiceType.Skill) == 1,
            "1%概率命中时没有恰好替换一个基础芯片为技能芯片");
        Assert(shown.Count(option => option.Payload is ChipChoiceType.Attack or ChipChoiceType.Knowledge or ChipChoiceType.Defense) == 2,
            "技能芯片替换后基础芯片候选数量错误");
        Assert(shown.Any(option => option.Id == "skill" && option.Title == Localization.Get("chip.skill.name")),
            "技能芯片候选缺少本地化显示信息");

        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.SunCe);
        var acquiredBefore = GameManager.AcquiredSkills.Count;
        var grantedSkill = GameManager.GrantRandomSkillFromSkillChip();
        Assert(grantedSkill != null, "技能芯片未能从任意品质技能池中获得技能");
        Assert(GameManager.AcquiredSkills.Count == acquiredBefore + 1 && GameManager.HasAcquiredSkill(grantedSkill!.Id),
            "技能芯片获得的技能没有写入本局技能列表");
    }

    private void TestInitialEventChipEnhancedChoices()
    {
        var shown = new ChipChoiceProvider
        {
            Count = 3,
            ChipTypes = new[]
            {
                ChipChoiceType.Attack,
                ChipChoiceType.Knowledge,
                ChipChoiceType.Defense
            },
            IncludeMysteriousChip = true,
            MysteriousChipReplacementChance = GameManager.InitialEventMysteriousChipChance,
            ExpansionReplacementChance = GameManager.InitialEventEnhancedChipExpansionChance,
            SkillChipReplacementChance = GameManager.InitialEventEnhancedSkillChipChance,
            RandomDoubleProvider = () => 0
        }.CreateChoices();

        Assert(shown.Count == 3, "初始事件④强化后的芯片选择不再是三选一");
        Assert(shown.Count(option => option.Payload is ChipChoiceType.Mysterious) == 1,
            "初始事件④在5%概率命中时没有提供神秘芯片候选");
        Assert(shown.Count(option => option.Payload is ChipChoiceType.Expansion) == 1,
            "初始事件④在10%概率命中时没有提供扩容芯片候选");
        Assert(shown.Count(option => option.Payload is ChipChoiceType.Skill) == 1,
            "初始事件④在10%概率命中时没有提供技能芯片候选");
        Assert(shown.Any(option => option.Title == Localization.Get("chip.mysterious.name")),
            "神秘芯片候选缺少本地化显示信息");

        var boundary = new ChipChoiceProvider
        {
            Count = 3,
            ChipTypes = new[]
            {
                ChipChoiceType.Attack,
                ChipChoiceType.Knowledge,
                ChipChoiceType.Defense
            },
            IncludeMysteriousChip = true,
            MysteriousChipReplacementChance = GameManager.InitialEventMysteriousChipChance,
            ExpansionReplacementChance = 0,
            SkillChipReplacementChance = 0,
            RandomDoubleProvider = () => 0.05
        }.CreateChoices();
        Assert(boundary.All(option => option.Payload is not ChipChoiceType.Mysterious),
            "神秘芯片概率边界错误：掷出5%不应判定为命中");
    }

    private void TestUpdatedFactionFatePools()
    {
        Assert(FactionFateDatabase.AllQunFates.Count == 6, "群命运池不是六项");
        Assert(FactionFateDatabase.AllQunFates.Any(fate => fate.Id == FactionFateIds.DoubleInitialChoice), "群命运池缺少命运抉择");
        Assert(FactionFateDatabase.AllQunFates.Any(fate => fate.Id == FactionFateIds.QunChipReconfiguration), "群命运池缺少芯片重构");
        Assert(FactionFateDatabase.AllQunFates.Any(fate => fate.Id == FactionFateIds.QunFirstTwoEquipmentUpgrade), "群命运池缺少淬炼开局");
        Assert(FactionFateDatabase.AllWeiFates.Count == 6, "魏命运池不是六项");
        Assert(FactionFateDatabase.AllWeiFates.Any(fate => fate.Id == FactionFateIds.WeiDoubleChipEffect), "魏命运池缺少芯片超频");

        var expectedShu = new[]
        {
            FactionFateIds.ShuUnifiedTactics,
            FactionFateIds.ShuInitiative,
            FactionFateIds.ShuTrickResource,
            FactionFateIds.ShuPeachWineUnity,
            FactionFateIds.ShuFirstTrickFree,
            FactionFateIds.ShuMuNiuLiuMaBossReward
        };
        Assert(FactionFateDatabase.AllShuFates.Select(fate => fate.Id).SequenceEqual(expectedShu), "蜀命运池没有严格替换为指定六项");
        Assert(FactionFateDatabase.AllShuFates.All(fate => fate.Id != FactionFateIds.ShuChainStrategy), "旧连策仍在蜀命运池");
        Assert(FactionFateDatabase.AllShuFates.All(fate => fate.Id != FactionFateIds.ShuStrategyAmplification), "旧奇策增幅仍在蜀命运池");
    }

    private void TestDoubleInitialEventChoice()
    {
        GameManager.BeginNewRun();
        FactionFateManager.DebugForceFate(FactionFateIds.DoubleInitialChoice);
        Assert(FactionFateManager.TryConsumeExtraInitialEventChoice(), "命运抉择未提供第二次初始事件选择");
        Assert(!FactionFateManager.TryConsumeExtraInitialEventChoice(), "命运抉择错误提供了第三次初始事件选择");
    }

    private void TestQunChipReconfiguration()
    {
        GameManager.BeginNewRun();
        FactionFateManager.DebugForceFate(FactionFateIds.QunChipReconfiguration);
        FactionFateManager.DebugSetQunForcedChipType(ChipChoiceType.Attack);

        var forcedChoices = new ChipChoiceProvider
        {
            Count = 3,
            ForcedBaseChipType = FactionFateManager.QunForcedChipType,
            ExpansionReplacementChance = 0,
            SkillChipReplacementChance = 0
        }.CreateChoices();
        Assert(forcedChoices.Count == 3 && forcedChoices.All(option => option.Payload is ChipChoiceType.Attack),
            "芯片重构没有把三选一基础芯片统一替换为随机确定的攻击芯片");

        var specialChoices = new ChipChoiceProvider
        {
            Count = 3,
            ForcedBaseChipType = FactionFateManager.QunForcedChipType,
            ExpansionReplacementChance = FactionFateManager.GetChipSpecialReplacementChance(0.01),
            SkillChipReplacementChance = FactionFateManager.GetChipSpecialReplacementChance(0.01),
            RandomDoubleProvider = () => 0
        }.CreateChoices();
        Assert(specialChoices.Count(option => option.Payload is ChipChoiceType.Expansion) == 1,
            "芯片重构下扩容芯片没有替换基础候选");
        Assert(specialChoices.Count(option => option.Payload is ChipChoiceType.Skill) == 1,
            "芯片重构下技能芯片没有替换基础候选");
        Assert(specialChoices.Count(option => option.Payload is ChipChoiceType.Attack) == 1,
            "芯片重构下两个特殊芯片替换后没有保留一个固定基础芯片候选");

        GameManager.IncrementAttackChipCount();
        Assert(GameManager.AttackChipCount == 1, "芯片重构下攻击芯片没有获得");
        Assert(GameManager.RunKillDamageBonus == 6, "芯片重构没有将攻击芯片效果提升至双倍");
        Assert(Math.Abs(FactionFateManager.GetChipSpecialReplacementChance(0.01) - 0.05) < 0.000001,
            "芯片重构没有将1%特殊芯片概率提高至5%");
        Assert(Math.Abs(FactionFateManager.GetChipSpecialReplacementChance(0.10) - 0.50) < 0.000001,
            "芯片重构没有将10%特殊芯片概率提高至50%");

        FactionFateManager.DebugSetQunForcedChipType(ChipChoiceType.Defense);
        var hpBefore = GameManager.MaxHP;
        GameManager.IncrementDefenseChipCount();
        Assert(GameManager.DefenseChipCount == 1 && GameManager.MaxHP == hpBefore + 20,
            "芯片重构没有将生命芯片效果提升至双倍");

        var acquiredBefore = GameManager.AcquiredSkills.Count;
        GameManager.AddEquipment(EquipmentIds.SkillChip, EquipmentGainSource.ChoiceReward);
        Assert(GameManager.AcquiredSkills.Count == acquiredBefore + 2,
            "芯片重构没有让技能芯片一次获得两项随机技能");

        GameManager.IncrementExpansionChipCount();
        Assert(GameManager.ExpansionChipCount == 2,
            "芯片重构没有让扩容芯片的效果翻倍");
    }

    private void TestQunDirectChipRewardsRemainUnreplaced()
    {
        GameManager.BeginNewRun();
        FactionFateManager.DebugForceFate(FactionFateIds.QunChipReconfiguration);
        FactionFateManager.DebugSetQunForcedChipType(ChipChoiceType.Skill);

        var reward = new RewardData();
        reward.EquipmentIds.Add(EquipmentIds.AttackChip);
        var result = RewardManager.ApplyReward(reward, EquipmentGainSource.BattleReward);

        Assert(result.Succeeded, "群命运下直接攻击芯片奖励被错误视为领取失败");
        Assert(GameManager.AttackChipCount == 1 && GameManager.AcquiredSkills.Count == 0,
            "群命运错误替换了非三选一来源的攻击芯片");
        Assert(GameManager.RunKillDamageBonus == 6,
            "群命运下直接获得的攻击芯片没有保留双倍数值效果");
    }

    private void TestShuBattleRules()
    {
        var player = new Player("测试玩家");
        player.ResetForNewBattle(40, 40, 3);

        FactionFateManager.DebugForceFate(FactionFateIds.ShuUnifiedTactics);
        Assert(BattleRules.IsAttackingTrickWithFactionCompat(CardType.Kill), "兵谋同源未把杀视为攻击性锦囊");
        Assert(BattleRules.IsShaAttackWithFactionCompat(CardType.ArrowBarrage), "兵谋同源未把攻击性锦囊视为杀");
        Assert(BattleRules.IsAttackingTrick(CardType.CelestialImpact), "天体撞击没有按南蛮入侵归类为攻击性锦囊");

        FactionFateManager.DebugForceFate(FactionFateIds.ShuPeachWineUnity);
        Assert(BattleRules.ShouldApplyPeachEffect(player, CardType.Wine), "桃酒同源未让酒获得桃效果");
        Assert(BattleRules.ShouldApplyWineEffect(player, CardType.Peach), "桃酒同源未让桃获得酒效果");

        FactionFateManager.DebugForceFate(FactionFateIds.ShuFirstTrickFree);
        var stealAction = BattleAction.FromCard(new Card(CardType.Steal));
        Assert(BattleRules.GetActionCost(player, stealAction) == 0, "奇策先发未免除第一张锦囊费用");
        FactionFateManager.ConsumeShuFirstTrickFree(player);
        Assert(BattleRules.GetActionCost(player, stealAction) == 1, "奇策先发被消费后仍错误免除锦囊费用");

        player.ResetForNewBattle(40, 40, 1);
        FactionFateManager.DebugForceFate(FactionFateIds.ShuTrickResource);
        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "shu_zero_mana_test_enemy",
            Name = "测试敌人",
            MaxHP = 40,
            StartingResource = 3,
            StartingDeck = new EnemyDeck()
        });
        var context = new BattleContext(player, new TriggerManager());
        context.Encounter = new BattleEncounter();
        context.Encounter.Enemies.Add(enemy);
        var effect = new ShuEnemyInitialManaEffect();
        effect.Execute(context);
        Assert(enemy.CurrentMana == 0, "蜀命运三未将敌人初始费用归零");
    }

    private void TestWeiChipMultiplier()
    {
        GameManager.BeginNewRun();
        FactionFateManager.DebugForceFate(FactionFateIds.WeiDoubleChipEffect);
        Assert(FactionFateManager.GetChipEffectMultiplier() == 2, "魏芯片超频倍率不是2");
        GameManager.IncrementAttackChipCount();
        GameManager.IncrementKnowledgeChipCount();
        Assert(GameManager.RunKillDamageBonus == 6, "芯片超频未把攻击芯片效果从+3提升为+6");
        Assert(GameManager.AttackTrickDamageBonus == 10, "芯片超频未把知识芯片效果从+5提升为+10");

        var maxHpBefore = GameManager.MaxHP;
        new AddChipRewardAction(RewardChipType.Defense).Execute();
        Assert(GameManager.MaxHP == maxHpBefore + 20, "芯片超频未把防御芯片生命效果从+10提升为+20");

        GameManager.IncrementExpansionChipCount();
        Assert(GameManager.ExpansionChipCount == 2, "芯片超频未把扩容芯片效果翻倍");

        FactionFateManager.DebugForceFate(FactionFateIds.WeiGoldenReserve);
        Assert(FactionFateManager.GetChipEffectMultiplier() == 1, "非芯片超频命运错误获得双倍芯片效果");
    }

    private void TestWeiBattleSettlementAndShuBossReward()
    {
        GameManager.BeginNewRun();
        FactionFateManager.DebugForceFate(FactionFateIds.WeiBattleSettlement);
        var player = new Player("伤害统计玩家", "fate_damage_player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        var enemy = new Player("伤害统计敌人", "fate_damage_enemy", BattleTeam.Enemy);
        enemy.ResetForNewBattle(300, 300, 1);
        var context = new BattleContext(player, new TriggerManager());
        var damage = new DamageEvent(player, enemy, CardType.Kill, 237)
        {
            ActualDamageDealt = 237
        };
        context.RecordDamageResolved(damage, hpBefore: 300, hpAfter: 63);
        Assert(context.PlayerDamageDealtThisBattle == 237, "战斗上下文没有累计玩家对敌人的最终实际伤害");
        var goldBefore = GameManager.Gold;
        FactionFateManager.SettleBattleVictoryGold(currentHp: 1, playerDamageDealt: context.PlayerDamageDealtThisBattle);
        Assert(GameManager.Gold == goldBefore + 200, "魏·战后清算没有按本场实际伤害发放并封顶200金币");

        GameManager.BeginNewRun();
        FactionFateManager.DebugForceFate(FactionFateIds.WuBattleDividend);
        goldBefore = GameManager.Gold;
        FactionFateManager.SettleBattleVictoryGold(currentHp: -137, playerDamageDealt: 0);
        Assert(GameManager.Gold == goldBefore + 137,
            "吴·战后分红应按当前生命值绝对值发放金币，且不能套用旧的30金币上限");

        GameManager.BeginNewRun();
        FactionFateManager.DebugForceFate(FactionFateIds.ShuMuNiuLiuMaBossReward);
        Assert(FactionFateManager.TryGrantShuMuNiuLiuMaAfterFirstBoss(), "蜀·后勤先行没有发放木牛流马");
        Assert(InventoryManager.GetAllOwned().Any(item => item.Definition.Id == EquipmentIds.MuNiuLiuMa),
            "蜀·后勤先行发放后背包中没有木牛流马");
        Assert(!FactionFateManager.TryGrantShuMuNiuLiuMaAfterFirstBoss(), "蜀·后勤先行重复发放木牛流马");
    }

    private void TestFailedRerollDoesNotConsume()
    {
        ResetRerollFate();
        var current = new ChoiceOption(
            ChoiceKind.Skill,
            "unchanged",
            "测试",
            "测试",
            string.Empty,
            string.Empty);

        var success = FactionFateManager.TryRerollChoiceOption(
            current,
            new[] { current.Id },
            _ => null,
            out _);
        Assert(!success, "无合法候选时错误报告刷新成功");
        Assert(FactionFateManager.RerollRemaining == 3, "刷新失败仍错误消耗了改命次数");
    }

    private void TestChapterDicePresentationRequest()
    {
        GameManager.BeginNewRun();
        FactionFateManager.DebugForceFate(FactionFateIds.Dice);
        FactionFateManager.DebugSetNextDiceRolls(1, 2, 3);
        var goldBefore = GameManager.Gold;

        FactionFateManager.RollChapterDiceIfNeeded(1);
        Assert(GameManager.Gold == goldBefore + 300, "章节三骰总和6未发放300金币");
        Assert(
            FactionFateManager.TryConsumePendingDicePresentation(out var first) && first != null,
            "第一章骰子没有生成表现请求");
        Assert(first!.Chapter == 1 && first.Rolls.SequenceEqual(new[] { 1, 2, 3 }) && first.Total == 6,
            "第一章骰子表现请求没有携带完整三骰结果与总点数");
        Assert(first.LegendaryMessage.Contains("顺子", StringComparison.Ordinal)
            && first.LegendaryMessage.Contains("史诗", StringComparison.Ordinal),
            "三骰顺子没有发放史诗装备奖励");
        Assert(InventoryManager.GetAllOwned().Any(item => item.Definition.Rarity == EquipmentRarity.Epic),
            "三骰顺子没有实际发放史诗装备");
        Assert(InventoryManager.GetAllOwned().Any(item => item.Definition.Rarity == EquipmentRarity.Common),
            "三骰总和不高于6没有发放普通装备奖励");
        Assert(!FactionFateManager.TryConsumePendingDicePresentation(out _), "同一骰子表现请求可被重复消费");

        FactionFateManager.RollChapterDiceIfNeeded(1);
        Assert(!FactionFateManager.TryConsumePendingDicePresentation(out _), "同一章节重复生成骰子表现请求");

        FactionFateManager.DebugSetNextDiceRolls(6, 2, 1);
        goldBefore = GameManager.Gold;
        FactionFateManager.RollChapterDiceIfNeeded(2);
        Assert(GameManager.Gold == goldBefore + 450, "章节三骰总和9未发放450金币");
        Assert(
            FactionFateManager.TryConsumePendingDicePresentation(out var second) && second != null,
            "第二章骰子没有生成表现请求");
        Assert(second!.Chapter == 2 && second.Rolls.SequenceEqual(new[] { 6, 2, 1 }) && second.Total == 9,
            "第二章骰子表现请求携带了错误的三骰结果");
        Assert(InventoryManager.GetAllOwned().Any(item => item.Definition.Rarity == EquipmentRarity.Rare),
            "任一骰子掷出6没有发放稀有装备奖励");

        FactionFateManager.DebugSetNextDiceRolls(2, 2, 5);
        FactionFateManager.RollChapterDiceIfNeeded(3);
        Assert(FactionFateManager.TryConsumePendingDicePresentation(out var third) && third != null,
            "第三章重复数字没有生成骰子表现请求");
        Assert(third!.LegendaryMessage.Contains("相同数字", StringComparison.Ordinal)
            && third.LegendaryMessage.Contains("传奇", StringComparison.Ordinal),
            "任意两颗骰子相同没有发放传奇装备奖励");
        Assert(InventoryManager.GetAllOwned().Any(item => item.Definition.Rarity == EquipmentRarity.Legendary),
            "任意两颗骰子相同没有实际发放传奇装备");
    }

    private static void ResetRerollFate()
    {
        GameManager.BeginNewRun();
        FactionFateManager.DebugForceFate(FactionFateIds.Reroll);
        FactionFateManager.DebugSetRerollRemaining(3);
    }

    private void Assert(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
