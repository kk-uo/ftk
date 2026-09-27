using Godot;
using System;

/// <summary>
/// 验证粮草是死亡重试次数：最后1点粮草可复活，粮草为0后的下一次死亡才结束本局。
/// </summary>
public partial class ForageRetryRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            TestNormalBattleLoss();
            TestMapEventLethalDamage();
            TestTreasurePavilionLoss();
            GD.Print($"FORAGE_RETRY_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"FORAGE_RETRY_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestNormalBattleLoss()
    {
        ResetWithForage(1);
        GameManager.SaveBattleState(20, 3);
        GameManager.RecordPreBattleState();
        GameManager.SaveBattleState(0, 0);

        Assert(GameManager.HandleBattleLoss(), "最后1点粮草的普通战败没有允许重试");
        Assert(GameManager.Forage == 0 && GameManager.CurrentHP == 20 && GameManager.CurrentRunState == RunState.Map,
            "最后1点粮草的普通战败没有正确消耗并恢复战前状态");

        GameManager.SaveBattleState(0, 0);
        Assert(!GameManager.HandleBattleLoss(), "粮草为0时普通战败没有结束本局");
        Assert(GameManager.CurrentRunState == RunState.GameOver, "粮草为0时普通战败没有进入失败状态");
    }

    private void TestMapEventLethalDamage()
    {
        ResetWithForage(1);
        GameManager.SaveBattleState(GameManager.MaxHP, 0);
        GameManager.ApplyExplosiveFruitTouchDamage(GameManager.MaxHP + 1);

        Assert(GameManager.Forage == 0 && GameManager.CurrentHP == GameManager.MaxHP && GameManager.CurrentRunState != RunState.GameOver,
            "最后1点粮草的地图致死事件没有复活玩家");

        GameManager.ApplyExplosiveFruitTouchDamage(GameManager.MaxHP + 1);
        Assert(GameManager.CurrentRunState == RunState.GameOver, "粮草为0时地图致死事件没有结束本局");
    }

    private void TestTreasurePavilionLoss()
    {
        ResetWithForage(1);
        Assert(GameManager.HandleTreasurePavilionBattleLoss(), "最后1点粮草的藏宝阁战败没有允许重试");
        Assert(GameManager.Forage == 0 && GameManager.CurrentRunState == RunState.Map,
            "最后1点粮草的藏宝阁战败没有正确回到地图");

        Assert(!GameManager.HandleTreasurePavilionBattleLoss(), "粮草为0时藏宝阁战败没有结束本局");
        Assert(GameManager.CurrentRunState == RunState.GameOver, "粮草为0时藏宝阁战败没有进入失败状态");
    }

    private static void ResetWithForage(int forage)
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.MaChao);
        GameManager.AddForage(forage - GameManager.Forage);
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
