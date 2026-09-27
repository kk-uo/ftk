//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/WulongGuanxingRegression.cs
//
// 职责：验证卧龙集智体【观星】的费用保护与高费用录制行为。
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 卧龙集智体观星行为的 Headless 回归入口。
/// </summary>
public partial class WulongGuanxingRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestGuanxingBlocksUnifiedSteal();
            TestGuanxingRecordsHighestAffordableAttack();
            TestGuanxingOnlyRecordsFeeAtZeroMana();
            TestChapterTwoBossRewardsLifeChipInsteadOfAttackChip();
            GD.Print($"WULONG_GUANXING_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"WULONG_GUANXING_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestGuanxingBlocksUnifiedSteal()
    {
        var definition = GetWulongDefinition();
        var player = CreatePlayer(1);
        var wulong = new EnemyInstance(definition);
        wulong.GainMana(1); // 观星前已蓄至 3 费。
        wulong.StartGuanxingRecording();

        var context = CreateContext(player, wulong);
        context.BeginRoundResult();
        var stolen = BattlePhaseResolutionEffect.ResolveStealEffect(context, player, wulong);

        Assert(stolen == 0, "观星中的卧龙集智体仍被顺手牵羊偷取费用");
        Assert(wulong.CurrentMana == 3, "观星免疫后卧龙集智体的费用被错误扣除");
        Assert(player.CurrentMana == 1, "观星免疫后使用者仍获得了费用");
        Assert(context.RoundResult.Text.Contains("观星状态", StringComparison.Ordinal), "观星免偷取缺少战报说明");
    }

    private void TestGuanxingRecordsHighestAffordableAttack()
    {
        var definition = GetWulongDefinition();
        var wulong = new EnemyInstance(definition);
        wulong.GainMana(1); // 3 费：火雷杀可用。
        wulong.StartGuanxingRecording();

        var selected = new EnemyAI().SelectGuanxingRecordCard(wulong, definition);
        Assert(selected.Type == CardType.FireThunderKill, "卧龙集智体观星后没有优先记录最高费用的火雷杀");
        Assert(selected.Type != CardType.Fee, "卧龙集智体有费用时在观星期间错误记录了费");
    }

    private void TestGuanxingOnlyRecordsFeeAtZeroMana()
    {
        var definition = GetWulongDefinition();
        var wulong = new EnemyInstance(definition);
        wulong.PayMana(wulong.CurrentMana);
        wulong.StartGuanxingRecording();

        var selected = new EnemyAI().SelectGuanxingRecordCard(wulong, definition);
        Assert(selected.Type == CardType.Fee, "卧龙集智体在0费时没有以费作为观星录制兜底");
    }

    private void TestChapterTwoBossRewardsLifeChipInsteadOfAttackChip()
    {
        var chapterTwoBossIds = new[]
        {
            "zuoci",
            "gang_boss",
            "wulong_collective_intelligence",
            "huang_yi_zhi_zhu"
        };

        foreach (var bossId in chapterTwoBossIds)
        {
            var definition = EnemyDatabase.GetEnemy(bossId)
                ?? throw new InvalidOperationException($"缺少第二章Boss：{bossId}");
            Assert(definition.Reward.DefenseChipDropCount == 1,
                $"第二章Boss【{definition.Name}】应奖励1个生命芯片（防御芯片）");
            Assert(!definition.Reward.EquipmentRewards.Contains(EquipmentIds.AttackChip),
                $"第二章Boss【{definition.Name}】不应再奖励攻击芯片");
        }
    }

    private static EnemyDefinition GetWulongDefinition()
    {
        return EnemyDatabase.GetEnemy("wulong_collective_intelligence")
            ?? throw new InvalidOperationException("缺少卧龙集智体敌人定义");
    }

    private static Player CreatePlayer(double mana)
    {
        var player = new Player("测试玩家", "wulong_test_player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, mana);
        return player;
    }

    private static BattleContext CreateContext(Player player, EnemyInstance enemy)
    {
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        return new BattleContext(player, new TriggerManager())
        {
            Encounter = encounter,
            TurnNumber = 1
        };
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
