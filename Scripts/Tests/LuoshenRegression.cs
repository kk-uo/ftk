//////////////////////////////////////////////////////////
// 【洛神】连续出费与断费惩罚的回归测试。
//////////////////////////////////////////////////////////

using Godot;
using System;

public partial class LuoshenRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestConsecutiveFeeGainIncreases();
            TestNoFeeTurnQueuesNextTurnDecayEvenWithoutAnyAction();
            TestAttackWithoutFeeHalvesCurrentManaThroughRealTurnStart();
            TestManaAtOrBelowTwoDoesNotDecay();
            GD.Print($"LUOSHEN_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"LUOSHEN_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestConsecutiveFeeGainIncreases()
    {
        var player = CreatePlayer(0);
        PlayFeeTurn(player);
        Assert(player.CurrentMana == 1 && player.LuoshenFeeStreak == 1,
            "洛神首个出费回合应获得1费并建立连续计数");

        PlayFeeTurn(player);
        Assert(player.CurrentMana == 3 && player.LuoshenFeeStreak == 2,
            "洛神连续第二个出费回合应额外获得2费");
    }

    private void TestNoFeeTurnQueuesNextTurnDecayEvenWithoutAnyAction()
    {
        var player = CreatePlayer(7);
        // 不调用 LuoshenOnActionStarted：复现玩家本回合没有选择任何牌的情况。
        player.LuoshenUpdateEndOfTurn();
        Assert(player.LuoshenDecayPending, "本回合未使用费时，洛神没有安排下回合减半");

        var (before, after, _) = player.LuoshenApplyDecay();
        Assert(before == 7 && after == 3 && player.CurrentMana == 3 && !player.LuoshenDecayPending,
            "洛神应在下回合将7费向下取整减半为3费，且只结算一次");
    }

    private void TestManaAtOrBelowTwoDoesNotDecay()
    {
        var player = CreatePlayer(2);
        player.LuoshenUpdateEndOfTurn();
        var (before, after, _) = player.LuoshenApplyDecay();
        Assert(before == 2 && after == 2 && player.CurrentMana == 2 && !player.LuoshenDecayPending,
            "洛神在费用为2或以下时不应触发减半");
    }

    private void TestAttackWithoutFeeHalvesCurrentManaThroughRealTurnStart()
    {
        var player = CreatePlayer(9);
        // 本回合打出攻击牌：它不是【费】，因此结束时应安排下回合减半。
        player.LuoshenOnActionStarted(player.CurrentMana);
        player.LuoshenUpdateEndOfTurn();

        var manager = BattleTriggerEffects.CreateDefaultManager();
        var context = new BattleContext(player, manager);
        context.BeginRoundResult();
        manager.RaiseTrigger(TriggerTiming.OnTurnStart, context);

        Assert(player.CurrentMana == 4,
            "甄姬本回合使用攻击牌但未使用费时，下回合应将实际持有的9费减半为4费");
        Assert(context.RoundResult.Text.Contains("洛神", StringComparison.Ordinal),
            "洛神实际减半后应写入本回合战报，便于核对费用变化");
    }

    private static Player CreatePlayer(double mana)
    {
        var player = new Player("甄姬", CharacterIds.ZhenJi, BattleTeam.Player);
        player.ResetForNewBattle(30, 30, mana);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.Luoshen)!);
        return player;
    }

    private static void PlayFeeTurn(Player player)
    {
        player.LuoshenOnActionStarted(player.CurrentMana);
        player.LuoshenOnFeeUsed();
        player.GainMana(player.LuoshenFeeStreak + 1);
        player.LuoshenUpdateEndOfTurn();
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
