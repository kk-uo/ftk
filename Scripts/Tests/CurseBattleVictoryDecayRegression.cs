//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/CurseBattleVictoryDecayRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证【诅咒】每次战斗结束都会永久减少1层。
// 2. 验证【诅咒之刃】会关闭该自动衰减。
// 3. 验证 ConsumeBattleVictory 仍然保留原有 ConsumeBattle 的行为
//    （RemainingBattles 计数的普通Buff照常衰减/到期移除）。
//
// 不负责：
// × 验证诅咒叠满44层额外获得【煞气缠身】的逻辑（见 RunBuffManager.AddStacks）。
// × 模拟完整战斗界面。
//
// 主要依赖：
// RunBuffManager
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 【诅咒】战斗胜利衰减规则的 Headless 回归入口。
/// </summary>
public partial class CurseBattleVictoryDecayRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Run();
            GD.Print($"CURSE_BATTLE_VICTORY_DECAY_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"CURSE_BATTLE_VICTORY_DECAY_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run()
    {
        Localization.Initialize();
        TestVictoryReducesCurseByOneStack();
        TestPlainConsumeBattleReducesCurse();
        TestCurseBladePreventsBattleEndDecay();
        TestCurseThresholdAddsShaQiWithoutRemovingCurses();
        TestShaQiLocksMapAndBattleHealthToOne();
        TestVictoryStillDecaysOtherRemainingBattlesBuffs();
    }

    private void TestVictoryReducesCurseByOneStack()
    {
        GameManager.BeginNewRun();
        RunBuffManager.AddStacks(RunBuffIds.Curse, 5, null, false);
        Assert(RunBuffManager.CountStacks(RunBuffIds.Curse) == 5, "诅咒测试前提失败：未能叠到5层");

        RunBuffManager.ConsumeBattleVictory();
        Assert(RunBuffManager.CountStacks(RunBuffIds.Curse) == 4, "战斗胜利后诅咒应该减少1层");

        RunBuffManager.ConsumeBattleVictory();
        Assert(RunBuffManager.CountStacks(RunBuffIds.Curse) == 3, "第二次战斗胜利后诅咒应该继续减少1层");
    }

    private void TestPlainConsumeBattleReducesCurse()
    {
        GameManager.BeginNewRun();
        RunBuffManager.AddStacks(RunBuffIds.Curse, 3, null, false);
        Assert(RunBuffManager.CountStacks(RunBuffIds.Curse) == 3, "诅咒测试前提失败：未能叠到3层");

        // 战败时仍然只调用 ConsumeBattle（GameManager.HandleBattleLoss 的既有行为），
        // 也属于“每次战斗结束后”的结算范围。
        RunBuffManager.ConsumeBattle();
        Assert(RunBuffManager.CountStacks(RunBuffIds.Curse) == 2, "战斗结束（普通ConsumeBattle）应该减少1层诅咒");
    }

    private void TestCurseBladePreventsBattleEndDecay()
    {
        GameManager.BeginNewRun();
        var blade = GameManager.AddEquipment(EquipmentIds.CurseBlade);
        Assert(blade != null, "诅咒之刃测试前提失败：未能加入背包");
        Assert(InventoryManager.EquipToSlot(blade!.InstanceId, EquipmentSlot.Weapon), "诅咒之刃测试前提失败：未能装备至武器槽");

        RunBuffManager.AddStacks(RunBuffIds.Curse, 1, null, false);
        Assert(RunBuffManager.CountStacks(RunBuffIds.Curse) == 1, "诅咒之刃测试前提失败：未能添加诅咒");
        RunBuffManager.ConsumeBattle();
        Assert(RunBuffManager.CountStacks(RunBuffIds.Curse) == 1, "装备诅咒之刃后，战斗结束不应自动衰减诅咒");
    }

    private void TestCurseThresholdAddsShaQiWithoutRemovingCurses()
    {
        GameManager.BeginNewRun();
        GameManager.SetCurrentHp(GameManager.MaxHP);
        RunBuffManager.AddStacks(RunBuffIds.Curse, 44, null, false);

        Assert(RunBuffManager.CountStacks(RunBuffIds.Curse) == 44, "达到44层诅咒后不应移除原有诅咒层数");
        Assert(RunBuffManager.CountStacks(RunBuffIds.ShaQiChenShen) == 1, "达到44层诅咒后应额外获得1层煞气缠身");
        Assert(GameManager.CurrentHP == 1, "自动获得煞气缠身时，地图生命值应立刻变为1");
        Assert(System.Math.Abs(RunBuffManager.GetPlayerDamageTakenMultiplier() - 5.4d) < 0.0001d,
            "44层诅咒应保留并使受伤倍率达到×5.4");
        Assert(System.Math.Abs(RunBuffManager.GetPlayerDamageDealtMultiplier() - 1.5d) < 0.0001d,
            "煞气缠身应使造成伤害倍率达到×1.5");
    }

    private void TestShaQiLocksMapAndBattleHealthToOne()
    {
        GameManager.BeginNewRun();
        RunBuffManager.Add(RunBuffIds.ShaQiChenShen);

        GameManager.SetCurrentHp(30);
        Assert(GameManager.CurrentHP == 1, "煞气缠身期间地图生命值不能被设为1以外的存活值");
        GameManager.SaveBattleState(30, 1);
        Assert(GameManager.CurrentHP == 1, "煞气缠身期间战后存档生命值错误覆盖了恒定1点");

        var player = new Player("玩家", "shaqi_player", BattleTeam.Player);
        player.ResetForNewBattle(40, 1, 1);
        player.SetCurrentHealth(25);
        Assert(player.Health == 1, "煞气缠身期间战斗内生命值没有被锁定为1");

        RunBuffManager.RemoveAllStacks(RunBuffIds.ShaQiChenShen);
    }

    private void TestVictoryStillDecaysOtherRemainingBattlesBuffs()
    {
        GameManager.BeginNewRun();
        // Darkness 的 DefaultRemainingBattles=1，本场战斗结束后应该正好到期移除，
        // 确认 ConsumeBattleVictory 没有破坏原有 ConsumeBattle 的衰减行为。
        RunBuffManager.Add(RunBuffIds.Darkness);
        Assert(RunBuffManager.CountStacks(RunBuffIds.Darkness) == 1, "黑暗测试前提失败：未能添加");

        RunBuffManager.ConsumeBattleVictory();
        Assert(RunBuffManager.CountStacks(RunBuffIds.Darkness) == 0, "ConsumeBattleVictory未能像原ConsumeBattle一样让黑暗到期移除");
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
