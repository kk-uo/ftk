using Godot;
using System;
using System.Linq;

/// <summary>覆盖难度累加、终章、Boss生命、血月与经济规则。</summary>
public partial class DifficultySystemRegression : Node
{
    private int _assertions;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            DifficultyManager.ResetForTest();

            TestNoDifficultyIsolation();
            TestNormalDifficultyChapterOneBossHealth();
            TestFinalChapterRules();
            TestBossHealthBonuses();
            TestBloodMoonFlatHealthAndChapterLifetime();
            TestDifficultyThreeCollapseAndBlackMarketOption();
            TestDifficultyFourFactionFateAndPower();
            TestDifficultyFiveEconomy();
            TestDifficultySixBossRulesAndSkillChoice();

            GD.Print($"DIFFICULTY_SYSTEM_TEST_PASS assertions={_assertions}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"DIFFICULTY_SYSTEM_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestNoDifficultyIsolation()
    {
        StartRun(DifficultyLevel.None);
        Assert(!DifficultyManager.ChapterVariantsEnabled, "无难度错误启用了章节变种");
        Assert(GameManager.CurrentChapterVariant == ChapterVariant.None, "无难度生成了第一章变种");
        Assert(!GameManager.GlobalRainActive, "无难度生成了全局降雨");
        Assert(DifficultyManager.FinalChapter == 3, "无难度终章不是第三章");
        var shopEvent = EventDatabase.GetEvent("shop_general")
            ?? throw new InvalidOperationException("缺少普通商店事件");
        Assert(!EventManager.GetVisibleOptions(shopEvent)
            .Any(option => option.Rewards.Any(reward => reward.Type == EventRewardType.OpenBlackMarketShop)),
            "无难度错误出现了难度黑市选项");
        var tavernEvent = EventDatabase.GetEvent("tavern")
            ?? throw new InvalidOperationException("缺少酒馆事件");
        Assert(!EventManager.GetVisibleOptions(tavernEvent)
            .Any(option => option.Rewards.Any(reward => reward.Type == EventRewardType.OpenBlackMarketShop)),
            "无难度错误保留了酒馆的旧黑市入口");
    }

    private void TestFinalChapterRules()
    {
        StartRun(DifficultyLevel.Difficulty1);
        Assert(DifficultyManager.FinalChapter == 3, "难度1终章不是第三章");

        StartRun(DifficultyLevel.Difficulty2);
        Assert(DifficultyManager.FinalChapter == 4, "难度2没有开放第四章");

        StartRun(DifficultyLevel.Difficulty3);
        Assert(DifficultyManager.FinalChapter == 4, "难度3终章不是第四章");
    }

    private void TestNormalDifficultyChapterOneBossHealth()
    {
        StartRun(DifficultyLevel.None);
        var healer = EnemyFactory.CreateEnemy("boss_healer")
            ?? throw new InvalidOperationException("缺少医者定义");
        var traitor = EnemyFactory.CreateEnemy("boss_traitor")
            ?? throw new InvalidOperationException("缺少背叛者定义");
        var performer = EnemyFactory.CreateEnemy("crazy_performer")
            ?? throw new InvalidOperationException("缺少失疯卖艺人定义");

        Assert(healer.MaxHealth == 50, $"普通难度医者最终生命应为50（基础40+藤甲10），实际{healer.MaxHealth}");
        Assert(traitor.MaxHealth == 60, $"普通难度背叛者最终生命应为60，实际{traitor.MaxHealth}");
        Assert(performer.MaxHealth == 90, $"普通难度失疯卖艺人最终生命应为90，实际{performer.MaxHealth}");
    }

    private void TestBossHealthBonuses()
    {
        StartRun(DifficultyLevel.Difficulty2);
        GameManager.SetDebugChapterVariant(ChapterVariant.None);
        for (var chapter = 1; chapter <= 4; chapter++)
        {
            GameManager.SetRunState(RunState.Map);
            GameManager.DebugGoToChapter(chapter);
            GameManager.SetRunState(RunState.Battle);
            var enemy = EnemyFactory.CreateEnemy(CreateEnemyDefinition($"difficulty_boss_{chapter}", EnemyType.Boss));
            var expected = 100 + DifficultyManager.GetBossHealthBonus(chapter);
            Assert(enemy.MaxHealth == expected, $"第{chapter}章Boss生命应为{expected}，实际{enemy.MaxHealth}");
        }
    }

    private void TestBloodMoonFlatHealthAndChapterLifetime()
    {
        StartRun(DifficultyLevel.Difficulty1);
        GameManager.SetDebugChapterVariant(ChapterVariant.BloodMoon);
        GameManager.SetRunState(RunState.Battle);
        var enemy = EnemyFactory.CreateEnemy(CreateEnemyDefinition("blood_moon_normal", EnemyType.Normal));
        Assert(enemy.MaxHealth == 130, $"血红之月应固定增加30最大生命，实际{enemy.MaxHealth}");

        GameManager.SetRunState(RunState.Map);
        GameManager.AdvanceToNextChapter();
        Assert(!GameManager.HasChapterVariant(ChapterVariant.BloodMoon), "血红之月离开第一章后仍然存在");
        Assert(RunBuffManager.CountStacks(RunBuffIds.BloodMoonDarkness) == 0, "血红之月黑暗没有在章节结束时移除");
    }

    private void TestDifficultyThreeCollapseAndBlackMarketOption()
    {
        StartRun(DifficultyLevel.Difficulty2);
        var lowerDifficultyContext = CreateCollapseContext();
        new BattlefieldCollapseEffect().Execute(lowerDifficultyContext);
        Assert(lowerDifficultyContext.Player.Health == 40, "难度2错误启用了第55回合时间崩坏");

        StartRun(DifficultyLevel.Difficulty3);
        Assert(DifficultyManager.TimeCollapseEnabled, "难度3没有开启时间崩坏");
        Assert(ShopManager.GetBuyPrice(EquipmentRarity.Common) == 75, "难度3错误提前启用难度5价格");
        var collapseContext = CreateCollapseContext();
        new BattlefieldCollapseEffect().Execute(collapseContext);
        Assert(collapseContext.Player.Health == 39, "难度3第55回合没有触发时间崩坏");

        var shopEvent = EventDatabase.GetEvent("shop_general")
            ?? throw new InvalidOperationException("缺少普通商店事件");
        var hasBlackMarketOption = EventManager.GetVisibleOptions(shopEvent)
            .Any(option => option.Rewards.Any(reward => reward.Type == EventRewardType.OpenBlackMarketShop));
        Assert(hasBlackMarketOption, "难度3商店事件没有追加黑市选项");
        Assert(!shopEvent.Options
            .Any(option => option.Rewards.Any(reward => reward.Type == EventRewardType.OpenBlackMarketShop)),
            "难度黑市选项污染了静态事件数据");
        var tavernEvent = EventDatabase.GetEvent("tavern")
            ?? throw new InvalidOperationException("缺少酒馆事件");
        Assert(EventManager.GetVisibleOptions(tavernEvent)
            .Count(option => option.Rewards.Any(reward => reward.Type == EventRewardType.OpenBlackMarketShop)) == 1,
            "难度3酒馆黑市入口缺失或重复");
    }

    private void TestDifficultyFourFactionFateAndPower()
    {
        StartRun(DifficultyLevel.Difficulty3);
        Assert(FactionFateManager.CurrentFateId == null, "难度3错误提前启用阵营命运");
        Assert(GameManager.MaxPower == 100, "难度3初始电量不应降低");

        StartRun(DifficultyLevel.Difficulty4);
        Assert(FactionFateManager.CurrentFateId != null, "难度4没有抽取阵营命运");
        Assert(DifficultyManager.InitialPower == 80, "难度4基础电量没有从100降为80");
        Assert(GameManager.Power == GameManager.MaxPower, "难度4开局电量没有填满到当前上限");
    }

    private void TestDifficultyFiveEconomy()
    {
        StartRun(DifficultyLevel.Difficulty5);
        Assert(ShopManager.GetBuyPrice(EquipmentRarity.Common) == 100, "难度5普通装备价格错误");
        Assert(ShopManager.GetBuyPrice(EquipmentRarity.Rare) == 200, "难度5稀有装备价格错误");
        Assert(ShopManager.GetBuyPrice(EquipmentRarity.Epic) == 400, "难度5史诗装备价格错误");
        Assert(ShopManager.GetBuyPrice(EquipmentRarity.Legendary) == 800, "难度5传奇装备价格错误");
        Assert(Math.Abs(DifficultyManager.GetDifficultyLegendaryShopChance(1)) < 0.0001, "难度5第一章不应出现额外传奇概率");
        Assert(Math.Abs(DifficultyManager.GetDifficultyLegendaryShopChance(2) - 0.02) < 0.0001, "难度5第二章传奇概率错误");
        Assert(Math.Abs(DifficultyManager.GetDifficultyLegendaryShopChance(3) - 0.04) < 0.0001, "难度5第三章传奇概率错误");
        Assert(Math.Abs(DifficultyManager.GetDifficultyLegendaryShopChance(4) - 0.10) < 0.0001, "难度5第四章传奇概率错误");
    }

    private void TestDifficultySixBossRulesAndSkillChoice()
    {
        StartRun(DifficultyLevel.Difficulty6);
        Assert(DifficultyManager.GetBossCompanionEnemyId(1) == "gate_guard", "难度6第一章护卫错误");
        Assert(DifficultyManager.GetBossCompanionEnemyId(2) == "steel_guard", "难度6第二章护卫错误");
        Assert(DifficultyManager.GetBossBonusEquipmentId(3) == EquipmentIds.ClampExoskeleton, "难度6第三章Boss装备错误");
        Assert(DifficultyManager.GetBossBonusEquipmentId(4) == EquipmentIds.TimeHourglass, "难度6第四章Boss装备错误");

        GameManager.SetRunState(RunState.Map);
        GameManager.DebugGoToChapter(3);
        Assert(GameManager.TryConsumeDifficultySixChapterThreeSkillChoice(), "难度6第三章Boss前技能选择未触发");
        Assert(!GameManager.TryConsumeDifficultySixChapterThreeSkillChoice(), "难度6第三章Boss前技能选择重复触发");

        var boss = new EnemyInstance(CreateEnemyDefinition("difficulty_six_boss", EnemyType.Boss));
        Assert(boss.TryAddBattleEquipment(EquipmentIds.ClampExoskeleton), "无法给难度6Boss添加钳制机械外骨骼");
        Assert(boss.MaxHealth == 180, "钳制机械外骨骼没有为难度6Boss增加80生命");
    }

    private static void StartRun(DifficultyLevel difficulty)
    {
        GameManager.BeginNewRun();
        if (!DifficultyManager.TrySelect(difficulty))
        {
            throw new InvalidOperationException($"无法选择测试难度：{difficulty}");
        }
        GameManager.SelectCharacter(CharacterIds.ZhaoYun);
    }

    private static EnemyDefinition CreateEnemyDefinition(string id, EnemyType type)
    {
        return new EnemyDefinition
        {
            Id = id,
            Name = id,
            MaxHP = 100,
            Type = type,
            StartingResource = 0,
            StartingDeck = new EnemyDeck()
        };
    }

    private static BattleContext CreateCollapseContext()
    {
        var player = new Player("玩家", "difficulty_player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 0);
        var enemy = new EnemyInstance(CreateEnemyDefinition("difficulty_collapse_enemy", EnemyType.Normal));
        var context = new BattleContext(player, new TriggerManager())
        {
            TurnCounter = BattlefieldCollapseEffect.TriggerTurn,
            Encounter = new BattleEncounter()
        };
        context.Encounter.Enemies.Add(enemy);
        return context;
    }

    private void Assert(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
