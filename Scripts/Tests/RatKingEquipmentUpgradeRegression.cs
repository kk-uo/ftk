//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/RatKingEquipmentUpgradeRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证鼠王献礼可将藤甲升级为传奇装备。
// 2. 验证升级候选为空时不会摧毁原装备。
// 3. 验证传奇随机池仍遵守统一合法性门禁。
//
// 不负责：
// × 模拟完整事件界面点击。
// × 验证事件美术表现。
//
// 主要依赖：
// EventManager
// RewardManager
// InventoryManager
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 鼠王装备献礼的 Headless 回归入口。
/// </summary>
public partial class RatKingEquipmentUpgradeRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行鼠王装备升级回归并通过退出码返回结果。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestTengjiaUpgradesToLegendary();
            TestNoCandidatePreservesTengjia();
            GD.Print($"RAT_KING_UPGRADE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"RAT_KING_UPGRADE_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestTengjiaUpgradesToLegendary()
    {
        InventoryManager.Reset();
        var tengjia = InventoryManager.AddToInventory(EquipmentIds.Tengjia, EquipmentGainSource.SaveRestore);
        Assert(tengjia != null, "藤甲测试前提失败");

        var message = EventManager.ResolveRatKingOfferGift();
        var owned = InventoryManager.GetAllOwned();
        Assert(owned.All(item => item.Definition.Id != EquipmentIds.Tengjia), "藤甲没有被摧毁");
        Assert(owned.Any(item => item.Definition.Rarity == EquipmentRarity.Legendary), "没有获得传奇装备");
        Assert(!message.Contains("无可获得", StringComparison.Ordinal), "错误返回无升级候选");
    }

    private void TestNoCandidatePreservesTengjia()
    {
        InventoryManager.Reset();
        var legalLegendaryIds = new List<string>();
        foreach (var definition in EquipmentDatabase.GetAllEquipments())
        {
            if (definition.Rarity == EquipmentRarity.Legendary
                && RewardManager.CanAppearInRandomEquipmentUpgradeReward(definition, excludeOwned: false))
            {
                legalLegendaryIds.Add(definition.Id);
                InventoryManager.AddToInventory(definition.Id, EquipmentGainSource.SaveRestore);
            }
        }

        Assert(legalLegendaryIds.Count > 0, "传奇升级池仍然为空");
        var tengjia = InventoryManager.AddToInventory(EquipmentIds.Tengjia, EquipmentGainSource.SaveRestore);
        Assert(tengjia != null, "无候选测试无法加入藤甲");

        var message = EventManager.ResolveRatKingOfferGift();
        Assert(InventoryManager.FindOwned(tengjia!.InstanceId) != null, "候选为空时藤甲仍被摧毁");
        Assert(message.Contains("原装备未被摧毁", StringComparison.Ordinal), "候选为空提示不正确");
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
