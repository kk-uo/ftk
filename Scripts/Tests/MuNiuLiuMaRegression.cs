//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/MuNiuLiuMaRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证木牛流马每3回合获得0.5费。
// 2. 验证非触发回合不会提前增加费用。
// 3. 验证未装备时效果不生效。
//
// 不负责：
// × 模拟完整战斗界面。
// × 验证装备获取来源。
//
// 主要依赖：
// MuNiuLiuMaResourceEffect
// InventoryManager
// BattleContext
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 木牛流马费用周期的 Headless 回归入口。
/// </summary>
public partial class MuNiuLiuMaRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行装备周期回归并通过退出码返回结果。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestEquippedTriggerInterval();
            TestUnequippedDoesNotTrigger();
            GD.Print($"MU_NIU_LIU_MA_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"MU_NIU_LIU_MA_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestEquippedTriggerInterval()
    {
        GameManager.BeginNewRun();
        var equipment = InventoryManager.AddToInventory(
            EquipmentIds.MuNiuLiuMa,
            EquipmentGainSource.SaveRestore);
        Assert(equipment != null, "无法创建木牛流马测试装备");
        Assert(InventoryManager.EquipToSlot(equipment!.InstanceId, EquipmentSlot.Vehicle), "木牛流马未能装备到载具槽");

        var player = CreatePlayer();
        var context = new BattleContext(player, new TriggerManager());
        var effect = new MuNiuLiuMaResourceEffect();

        for (var turn = 1; turn <= 6; turn++)
        {
            context.TurnCounter = turn;
            effect.Execute(context);
            var expectedMana = 1.0 + (turn / 3) * 0.5;
            Assert(Math.Abs(player.CurrentMana - expectedMana) < 0.001, $"第{turn}回合费用错误");
        }

        Assert(context.RoundResult.Text.Contains("第3回合", StringComparison.Ordinal), "第3回合触发缺少战报");
        Assert(context.RoundResult.Text.Contains("第6回合", StringComparison.Ordinal), "第6回合触发缺少战报");
    }

    private void TestUnequippedDoesNotTrigger()
    {
        GameManager.BeginNewRun();
        InventoryManager.AddToInventory(EquipmentIds.MuNiuLiuMa, EquipmentGainSource.SaveRestore);
        var player = CreatePlayer();
        var context = new BattleContext(player, new TriggerManager())
        {
            TurnCounter = 3
        };

        new MuNiuLiuMaResourceEffect().Execute(context);
        Assert(Math.Abs(player.CurrentMana - 1.0) < 0.001, "未装备木牛流马仍然增加了费用");
    }

    private static Player CreatePlayer()
    {
        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        return player;
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
