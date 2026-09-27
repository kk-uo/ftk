//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/ModificationShopEventRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证改造铺的新三选项配置与资源结算。
// 2. 验证事件仅在第一至第三章出现且每章一次。
// 3. 验证武器重铸候选包含背包和已装备武器。
//
// 不负责：
// × 模拟 ChoicePanel 的鼠标点击。
// × 验证随机抽样的统计分布。
//
// 主要依赖：
// EventDatabase
// EventManager
// InventoryChoiceProvider
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 改造铺事件规则的 Headless 回归入口。
/// </summary>
public partial class ModificationShopEventRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行改造铺回归并通过进程退出码返回测试状态。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestMetadataAndChapterRepeatRule();
            TestChipChoiceCost();
            TestRandomPartsGoldRange();
            TestWeaponChoicePool();
            GD.Print($"MODIFICATION_SHOP_EVENT_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"MODIFICATION_SHOP_EVENT_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestMetadataAndChapterRepeatRule()
    {
        ResetRun();
        var eventData = GetEvent();
        var node = CreateEventNode();

        Assert(eventData.Rarity == EventRarity.Common, "改造铺不是普通事件");
        Assert(eventData.RepeatType == EventRepeatType.OncePerChapter, "改造铺不是每章一次");
        Assert(eventData.Options.Count == 3, "改造铺没有严格保留三个新选项");
        Assert(EventManager.CanUseEvent(eventData, node), "改造铺未进入第一章事件池");

        Assert(EventManager.TryResolveOption(eventData, eventData.Options[1], out _), "改造铺选项无法结算");
        Assert(!EventManager.CanUseEvent(eventData, node), "改造铺同一章可以重复出现");

        GameManager.DebugGoToChapter(2);
        Assert(EventManager.CanUseEvent(eventData, node), "改造铺未在第二章重新开放");
        GameManager.DebugGoToChapter(3);
        Assert(EventManager.CanUseEvent(eventData, node), "改造铺未进入第三章事件池");
        GameManager.DebugGoToChapter(4);
        Assert(!EventManager.CanUseEvent(eventData, node), "改造铺错误进入第四章事件池");
    }

    private void TestChipChoiceCost()
    {
        ResetRun();
        GameManager.AddGold(75);
        var eventData = GetEvent();
        var option = eventData.Options[0];
        var originalGold = GameManager.Gold;

        Assert(option.Costs.Count == 1
            && option.Costs[0].Type == EventCostType.Gold
            && option.Costs[0].Amount == 75, "芯片三选一成本不是75金币");
        Assert(option.Rewards.Any(reward => reward.Type == EventRewardType.ChooseOneChipOnce), "选项未请求芯片三选一");
        Assert(EventManager.TryResolveOption(eventData, option, out _), "芯片三选一选项结算失败");
        Assert(GameManager.Gold == originalGold - 75, "芯片三选一没有正确扣除75金币");
    }

    private void TestRandomPartsGoldRange()
    {
        for (var i = 0; i < 20; i++)
        {
            ResetRun();
            var eventData = GetEvent();
            var option = eventData.Options[1];
            var originalGold = GameManager.Gold;

            Assert(EventManager.TryResolveOption(eventData, option, out var result), "零件金币选项结算失败");
            var gained = GameManager.Gold - originalGold;
            Assert(gained is >= 100 and <= 125, $"零件金币超出100至125范围：{gained}");
            Assert(result.Contains(gained.ToString(), StringComparison.Ordinal), "随机金币结果未显示实际获得数值");
        }
    }

    private void TestWeaponChoicePool()
    {
        ResetRun();
        var equipped = GameManager.AddEquipment(EquipmentIds.RustSword)
            ?? throw new InvalidOperationException("无法添加已装备测试武器");
        _ = GameManager.AddEquipment(EquipmentIds.RustSpear);
        _ = GameManager.AddEquipment(EquipmentIds.ShortBow);
        _ = GameManager.AddEquipment(EquipmentIds.LongBow);
        _ = GameManager.AddEquipment(EquipmentIds.RustShield);
        Assert(InventoryManager.EquipToSlot(equipped.InstanceId, EquipmentSlot.Weapon), "测试武器无法装备");

        var completePool = new InventoryChoiceProvider
        {
            Scope = InventoryChoiceScope.All,
            SlotCategories = new[] { EquipmentSlotCategory.Weapon },
            Count = 10,
            Randomize = false,
            RequireReforgeCandidate = true
        }.CreateChoices();
        Assert(completePool.Any(option => option.Payload is OwnedEquipment item
            && item.InstanceId == equipped.InstanceId
            && item.EquippedSlot == EquipmentSlot.Weapon), "武器候选池遗漏已装备武器");
        Assert(completePool.All(option => option.Payload is OwnedEquipment item
            && InventoryManager.GetSlotCategory(item.Definition) == EquipmentSlotCategory.Weapon), "武器候选池混入非武器");

        var randomThree = new InventoryChoiceProvider
        {
            Scope = InventoryChoiceScope.All,
            SlotCategories = new[] { EquipmentSlotCategory.Weapon },
            Count = 3,
            Randomize = true,
            RequireReforgeCandidate = true
        }.CreateChoices();
        Assert(randomThree.Count == 3, "拥有至少三件武器时没有随机提供三件");
        Assert(randomThree.Select(option => option.Id).Distinct().Count() == randomThree.Count, "随机三武器候选出现重复实例");

        var chosen = randomThree[0].Payload as OwnedEquipment
            ?? throw new InvalidOperationException("随机武器候选没有携带装备实例");
        var reforge = InventoryManager.ReforgeEquipment(chosen.InstanceId);
        Assert(reforge.Original != null && reforge.Replacement != null, "选择武器后未成功复用正式重铸流程");
    }

    private static EventData GetEvent()
    {
        return EventDatabase.GetEvent("modification_shop")
            ?? throw new InvalidOperationException("找不到改造铺事件定义");
    }

    private static MapNode CreateEventNode()
    {
        return new MapNode
        {
            Id = "__modification_shop_event_test__",
            Type = MapNodeType.Event,
            StageIndex = 1
        };
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
