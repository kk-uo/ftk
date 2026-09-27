//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/EquipmentReforgeResultRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证装备重铸返回完整的原装备与替换装备。
// 2. 验证成功和失败结果均可直接交给UI展示。
//
// 不负责：
// × 模拟玩家点击。
// × 修改正式装备数据。
//
// 主要依赖：
// InventoryManager
// EquipmentDatabase
// Localization
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 装备重铸结果反馈的独立Headless回归入口。
/// </summary>
public partial class EquipmentReforgeResultRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行重铸结果回归，并通过进程退出码返回测试状态。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Run();
            GD.Print($"EQUIPMENT_REFORGE_RESULT_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"EQUIPMENT_REFORGE_RESULT_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run()
    {
        Localization.Initialize();
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.ZhaoYun);
        InventoryManager.Reset();

        var original = FindReforgeableEquipment();
        var owned = GameManager.AddEquipment(original.Id, EquipmentGainSource.Developer);
        Assert(owned != null, "测试装备未加入背包");

        var result = InventoryManager.ReforgeEquipment(owned!.InstanceId);
        Assert(result.Original?.Id == original.Id, "结果未返回原装备");
        Assert(result.Replacement != null, "结果未返回替换装备");
        Assert(result.Replacement!.Id != original.Id, "重铸结果仍为原装备");
        Assert(result.Message.Contains(Localization.GetName(original), StringComparison.Ordinal), "结果未显示原装备名称");
        Assert(result.Message.Contains(Localization.GetName(result.Replacement), StringComparison.Ordinal), "结果未显示替换装备名称");
        Assert(GameManager.OwnsEquipment(result.Replacement.Id), "替换装备未进入背包");

        var missing = InventoryManager.ReforgeEquipment(Guid.NewGuid());
        Assert(missing.Replacement == null, "无效实例错误产生了替换装备");
        Assert(!string.IsNullOrWhiteSpace(missing.Message), "无效实例没有返回失败提示");

        TestMysteriousChipCannotBeSoldOrReforged();
        TestLegendaryCandidatesUseUnifiedPool();
        TestReforgePoolRetainsOwnedEquipment();
        TestTyrantCrownIsBossOnly();
    }

    // 神秘芯片是纯剧情道具（EquipmentIds.MysteriousChip），不应该能被卖出或重铸掉——
    // 否则玩家会永久丢失这个只能通过特定事件获得、无法再刷出来的收藏品。
    private void TestMysteriousChipCannotBeSoldOrReforged()
    {
        InventoryManager.Reset();
        var owned = GameManager.AddEquipment(EquipmentIds.MysteriousChip, EquipmentGainSource.Developer);
        Assert(owned != null, "神秘芯片测试装备未加入背包");

        var reforgeResult = InventoryManager.ReforgeEquipment(owned!.InstanceId);
        Assert(reforgeResult.Replacement == null, "神秘芯片不应该能被重铸掉");
        Assert(GameManager.OwnsEquipment(EquipmentIds.MysteriousChip), "重铸尝试后神秘芯片不应该从背包消失");

        var sellPrice = InventoryManager.SellItem(owned.InstanceId);
        Assert(sellPrice < 0, "神秘芯片不应该能被出售");
        Assert(GameManager.OwnsEquipment(EquipmentIds.MysteriousChip), "出售尝试后神秘芯片不应该从背包消失");
    }

    private void TestLegendaryCandidatesUseUnifiedPool()
    {
        InventoryManager.Reset();
        var original = EquipmentDatabase.GetEquipment(EquipmentIds.TimeHourglass)
            ?? throw new InvalidOperationException("缺少时间沙漏定义");
        var candidates = InventoryManager.GetReforgeCandidates(original);

        Assert(candidates.Count >= 3, "传奇重铸池没有提供至少3个候选");
        Assert(candidates.Any(candidate => candidate.Id == EquipmentIds.ZhugeCrossbow),
            "未知来源的非专属传奇装备没有进入统一重铸池");
        Assert(candidates.All(InventoryManager.IsEligibleForReforge),
            "重铸池包含剧情或角色专属装备");
    }

    private void TestTyrantCrownIsBossOnly()
    {
        var crown = EquipmentDatabase.GetEquipment(EquipmentIds.TyrantCrown)
            ?? throw new InvalidOperationException("缺少暴虐皇冠定义");
        var hourglass = EquipmentDatabase.GetEquipment(EquipmentIds.TimeHourglass)
            ?? throw new InvalidOperationException("缺少时间沙漏定义");

        Assert(!InventoryManager.IsEligibleForReforge(crown),
            "暴虐皇冠不应进入统一重铸池");
        Assert(!RewardManager.CanAppearInRandomEquipmentReward(crown, excludeOwned: false),
            "暴虐皇冠不应进入随机装备获得池");
        Assert(!InventoryManager.GetReforgeCandidates(hourglass).Any(candidate => candidate.Id == EquipmentIds.TyrantCrown),
            "传奇装备重铸候选中仍出现暴虐皇冠");
    }

    private void TestReforgePoolRetainsOwnedEquipment()
    {
        InventoryManager.Reset();
        var original = EquipmentDatabase.GetEquipment(EquipmentIds.YellowTalisman)
            ?? throw new InvalidOperationException("缺少黄道符定义");
        var ownedCandidate = EquipmentDatabase.GetAllEquipments()
            .First(definition => definition.Id != original.Id
                && definition.Rarity == EquipmentRarity.Epic
                && InventoryManager.IsEligibleForReforge(definition));
        GameManager.AddEquipment(ownedCandidate.Id, EquipmentGainSource.Developer);

        Assert(InventoryManager.GetReforgeCandidates(original).Any(candidate => candidate.Id == ownedCandidate.Id),
            "重铸池错误排除了已拥有装备，导致候选池可能只剩单一装备");
    }

    private static EquipmentDefinition FindReforgeableEquipment()
    {
        foreach (var definition in EquipmentDatabase.GetAllEquipments())
        {
            if (InventoryManager.GetReforgeCandidates(definition).Count > 0)
            {
                return definition;
            }
        }

        throw new InvalidOperationException("装备库中没有可用于重铸回归的装备。");
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
