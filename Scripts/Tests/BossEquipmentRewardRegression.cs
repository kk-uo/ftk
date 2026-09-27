//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/BossEquipmentRewardRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证Boss待领取装备能够进入正式奖励并加入背包。
// 2. 验证Boss装备保留BossReward来源。
// 3. 验证无效装备不会被伪报为领取成功。
//
// 不负责：
// × 模拟奖励界面的鼠标点击。
// × 验证Boss掉落概率。
//
// 主要依赖：
// GameManager
// RewardManager
// InventoryManager
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// Boss装备掉落与领取链路的 Headless 回归入口。
/// </summary>
public partial class BossEquipmentRewardRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行回归并通过进程退出码返回测试状态。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestPendingBossDropIsDelivered();
            TestInvalidEquipmentDoesNotClaimOtherRewards();
            GD.Print($"BOSS_EQUIPMENT_REWARD_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"BOSS_EQUIPMENT_REWARD_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestPendingBossDropIsDelivered()
    {
        GameManager.BeginNewRun();
        GameManager.AddPendingBattleDrop(EquipmentIds.WarDrum);

        var reward = new RewardData();
        foreach (var equipmentId in GameManager.ConsumePendingBattleDrops())
        {
            reward.EquipmentIds.Add(equipmentId);
        }

        var result = RewardManager.ApplyReward(reward, EquipmentGainSource.BossReward);
        var delivered = InventoryManager.GetAllOwned()
            .SingleOrDefault(item => item.Definition.Id == EquipmentIds.WarDrum);

        Assert(result.Succeeded, "Boss固定装备奖励执行结果失败");
        Assert(delivered != null, "Boss固定掉落没有进入背包");
        Assert(
            delivered!.AcquiredFrom == EquipmentGainSource.BossReward.ToString(),
            "Boss掉落没有保留BossReward来源");
        Assert(GameManager.ConsumePendingBattleDrops().Count == 0, "Boss待领取掉落没有被正确消费");
    }

    private void TestInvalidEquipmentDoesNotClaimOtherRewards()
    {
        GameManager.BeginNewRun();
        var goldBefore = GameManager.Gold;
        var reward = new RewardData { Gold = 50 };
        reward.EquipmentIds.Add("missing_boss_equipment");

        var result = RewardManager.ApplyReward(reward, EquipmentGainSource.BossReward);

        Assert(!result.Succeeded, "无效Boss装备被错误报告为领取成功");
        Assert(
            result.FailedEquipmentIds.Contains("missing_boss_equipment"),
            "执行结果没有记录失败的Boss装备ID");
        Assert(GameManager.Gold == goldBefore, "装备校验失败后仍然提前发放了金币");
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
