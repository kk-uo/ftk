//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/QiXingTanBattleRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证七星坛只从第一章主 Boss 池召魂。
// 2. 验证本 Run 实际 Boss 记录优先于随机回退。
// 3. 验证生命与最终伤害倍率只作用于特殊战斗实例。
//
// 不负责：
// × 模拟事件按钮点击。
// × 模拟完整 Boss AI 回合。
//
// 主要依赖：
// GameManager
// EnemyFactory
// DamageModifierPipeline
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 七星坛招魂战斗的 Headless 回归入口。
/// </summary>
public partial class QiXingTanBattleRegression : Node
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
            Run();
            GD.Print($"QIXINGTAN_BATTLE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"QIXINGTAN_BATTLE_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run()
    {
        var candidates = GameManager.GetQiXingTanBossCandidateIds();
        Assert(candidates.Count == 3, "七星坛候选不是第一章三个主 Boss");

        var candidateNames = new HashSet<string>();
        foreach (var candidateId in candidates)
        {
            var definition = EnemyDatabase.GetEnemy(candidateId);
            Assert(definition != null, $"候选 Boss 不存在：{candidateId}");
            Assert(definition!.Type == EnemyType.Boss, $"候选包含非 Boss：{candidateId}");
            candidateNames.Add(definition.Name);
        }

        Assert(candidateNames.SetEquals(new[] { "医者", "背叛者", "失疯卖艺人" }), "第一章 Boss 候选内容错误");

        foreach (var candidateId in candidates)
        {
            TestRecordedBossAndInstanceModifiers(candidateId);
        }

        TestRandomFallbackUsesOnlyChapterOneBossPool(candidates);
        TestVictoryReward();
    }

    private void TestRecordedBossAndInstanceModifiers(string bossId)
    {
        GameManager.BeginNewRun();
        var definition = EnemyDatabase.GetEnemy(bossId)
            ?? throw new InvalidOperationException($"找不到测试 Boss：{bossId}");
        var originalDefinitionMaxHp = definition.MaxHP;
        var baseline = new EnemyInstance(definition);
        var baselineMaxHp = baseline.MaxHealth;
        var originalSkills = definition.SkillIds.ToArray();
        var originalEquipment = definition.EquipmentIds.ToArray();
        var originalDeck = definition.StartingDeck.Cards.ToArray();

        GameManager.SetDebugBossEncounterPlan(1, new[] { bossId });
        GameManager.MarkChapterBossDefeated(1);
        Assert(GameManager.GetDefeatedChapterBossEnemyId(1) == bossId, $"未记录实际击败 Boss：{bossId}");

        GameManager.BeginQiXingTanBattle();
        var activeIds = GameManager.GetActiveSpecialBattleEnemyIds();
        Assert(activeIds.Count == 1 && activeIds[0] == bossId, $"七星坛没有优先复用实际 Boss：{bossId}");
        Assert(activeIds[0] != "boss_tyrant", "七星坛仍然生成了董卓");

        var strengthened = EnemyFactory.CreateEnemy(activeIds[0])
            ?? throw new InvalidOperationException($"无法创建七星坛 Boss：{activeIds[0]}");
        Assert(strengthened.MaxHealth == baselineMaxHp * 3, $"{bossId} 最大生命没有正确 ×3");
        Assert(strengthened.Health == strengthened.MaxHealth, $"{bossId} 当前生命没有同步到强化后上限");
        Assert(strengthened.RuntimeStates.TryGetValue("qixingtan_empowered", out var empowered) && empowered is true,
            $"{bossId} 没有被标记为七星坛强化实例");
        Assert(strengthened.RuntimeSkillIds.SequenceEqual(originalSkills), $"{bossId} 技能被强化流程修改");
        Assert(strengthened.Equipments.Select(item => item.Id).SequenceEqual(originalEquipment), $"{bossId} 装备被强化流程修改");
        Assert(strengthened.RuntimeDeck.SequenceEqual(originalDeck), $"{bossId} 出牌池被强化流程修改");

        var player = new Player("玩家", "qixingtan_test_player", BattleTeam.Player);
        player.ResetForNewBattle(100, 100, 1);
        var context = new BattleContext(player, new TriggerManager());
        context.BeginRoundResult();
        var damage = new DamageEvent(strengthened, player, CardType.Kill, 10, isDirectAttackDamage: true);
        context.DamageEvent = damage;
        new RunBuffEnemyDamageBonusEffect().Execute(context);
        Assert(damage.Modifiers.Resolve(damage.BaseAmount) == 20, $"{bossId} 最终伤害没有正确 ×2");

        GameManager.CompleteSpecialBattle();
        var normalAfterBattle = new EnemyInstance(definition);
        Assert(definition.MaxHP == originalDefinitionMaxHp, $"{bossId} 数据库最大生命被污染");
        Assert(normalAfterBattle.MaxHealth == baselineMaxHp, $"{bossId} 后续普通实例仍带有七星坛强化");
        Assert(GameManager.ActiveSpecialBattleEnemyFinalDamageMultiplier == 1.0, "特殊战斗结束后伤害倍率未清理");
    }

    private void TestRandomFallbackUsesOnlyChapterOneBossPool(IReadOnlyList<string> candidates)
    {
        for (var i = 0; i < 30; i++)
        {
            GameManager.BeginNewRun();
            GameManager.InvalidateChapterBossEncounterPlan(1);
            GameManager.BeginQiXingTanBattle();
            var activeIds = GameManager.GetActiveSpecialBattleEnemyIds();
            Assert(activeIds.Count == 1, "无记录回退没有生成且仅生成一个 Boss");
            Assert(candidates.Contains(activeIds[0]), $"无记录回退生成了非法敌人：{activeIds[0]}");
            Assert(EnemyDatabase.GetEnemy(activeIds[0])?.Type == EnemyType.Boss, "无记录回退生成了普通敌人");
            GameManager.CompleteSpecialBattle();
        }
    }

    private void TestVictoryReward()
    {
        GameManager.BeginNewRun();
        Assert(!GameManager.OwnsEquipment(EquipmentIds.SoulStone), "测试开始前已经持有灵魂石");
        var soulStoneCountBefore = InventoryManager.GetAllOwned().Count(item => item.Definition.Id == EquipmentIds.SoulStone);
        GameManager.GrantQiXingTanVictoryReward();
        var soulStoneCountAfter = InventoryManager.GetAllOwned().Count(item => item.Definition.Id == EquipmentIds.SoulStone);
        Assert(soulStoneCountAfter == soulStoneCountBefore + 2, "七星坛胜利后没有获得两枚灵魂石");
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
