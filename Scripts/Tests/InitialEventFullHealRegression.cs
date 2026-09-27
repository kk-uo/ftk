//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/InitialEventFullHealRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证初始事件⑰立即发放金币并写入Run状态。
// 2. 验证胜利与可继续战败后均恢复至满血。
// 3. 验证永久禁疗与战场崩坏仍保持更高优先级。
//
// 不负责：
// × 模拟初始事件界面点击。
// × 验证战斗结束界面动画。
//
// 主要依赖：
// InitialEventPool
// GameManager
// RunBuffManager
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 初始事件⑰战后回满血的 Headless 回归入口。
/// </summary>
public partial class InitialEventFullHealRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行初始事件⑰回归并通过退出码返回测试状态。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestSelectionAndRepeatedVictoryRecovery();
            TestRecoverableBattleLoss();
            TestExplicitPriorityRules();
            TestNewRunReset();
            GD.Print($"INITIAL_EVENT_FULL_HEAL_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"INITIAL_EVENT_FULL_HEAL_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestSelectionAndRepeatedVictoryRecovery()
    {
        ResetRun();
        var initialGold = GameManager.Gold;
        ApplyInitialEvent17();

        Assert(GameManager.Gold == initialGold + 75, "初始事件⑰没有立即获得75金币");
        Assert(GameManager.InitialEventBattleEndFullHeal, "初始事件⑰没有写入Run永久状态");

        GameManager.SaveBattleState(7, GameManager.CurrentMana);
        GameManager.RecoverHealthAfterBattle();
        Assert(GameManager.CurrentHP == GameManager.MaxHP, "第一次战斗胜利后没有回满血");

        GameManager.SaveBattleState(3, GameManager.CurrentMana);
        GameManager.RecoverHealthAfterBattle();
        Assert(GameManager.CurrentHP == GameManager.MaxHP, "后续战斗胜利后没有继续回满血");
    }

    private void TestRecoverableBattleLoss()
    {
        ResetRun();
        ApplyInitialEvent17();
        GameManager.SaveBattleState(8, GameManager.CurrentMana);
        GameManager.RecordPreBattleState();
        GameManager.SaveBattleState(1, GameManager.CurrentMana);

        Assert(GameManager.HandleBattleLoss(), "仍有粮草时战败流程错误结束Run");
        Assert(GameManager.CurrentHP == GameManager.MaxHP, "可继续战败后没有按事件描述回满血");
    }

    private void TestExplicitPriorityRules()
    {
        ResetRun();
        ApplyInitialEvent17();
        GameManager.SaveBattleState(1, GameManager.CurrentMana);
        RunBuffManager.Add(RunBuffIds.ShaQiChenShen);
        GameManager.RecoverHealthAfterBattle();
        Assert(GameManager.CurrentHP == 1, "初始事件⑰错误绕过煞气缠身的永久禁疗");

        RunBuffManager.Reset();
        GameManager.SaveBattleState(6, GameManager.CurrentMana);
        GameManager.ArmCollapseVictoryHealthOverride(6);
        GameManager.RecoverHealthAfterBattle();
        Assert(GameManager.CurrentHP == 6, "初始事件⑰错误覆盖战场崩坏的战后生命结算");
    }

    private void TestNewRunReset()
    {
        ResetRun();
        ApplyInitialEvent17();
        GameManager.BeginNewRun();
        Assert(!GameManager.InitialEventBattleEndFullHeal, "新Run没有清除初始事件⑰状态");
    }

    private static void ApplyInitialEvent17()
    {
        var entry = InitialEventPool.AllEntries.Single(option => option.Id == "ie_17");
        entry.Apply();
    }

    private static void ResetRun()
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.ZhaoYun);
        InventoryManager.Reset();
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
