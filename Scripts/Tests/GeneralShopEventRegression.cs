//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/GeneralShopEventRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证普通商店事件只进入第二、三章事件池。
// 2. 验证进入商店与打劫选项的金币、电量结算。
// 3. 验证事件单局一次且电量不足时不可打劫。
//
// 不负责：
// × 模拟商店商品购买界面。
// × 验证随机事件权重分布。
//
// 主要依赖：
// EventDatabase
// EventManager
// GameManager
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 普通商店事件规则的 Headless 回归入口。
/// </summary>
public partial class GeneralShopEventRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行商店事件回归并通过进程退出码返回测试状态。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestEventMetadataAndChapterRange();
            TestEnterShopReward();
            TestRobberyCostAndReward();
            TestInsufficientPowerBlocksRobbery();
            GD.Print($"GENERAL_SHOP_EVENT_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"GENERAL_SHOP_EVENT_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestEventMetadataAndChapterRange()
    {
        ResetRun();
        var eventData = GetEvent();
        var node = CreateEventNode();

        Assert(eventData.Rarity == EventRarity.Common, "商店事件不是普通品质");
        Assert(eventData.RepeatType == EventRepeatType.RunOnce && !eventData.IsRepeatable, "商店事件不是单局一次事件");
        Assert(!EventManager.CanUseEvent(eventData, node), "商店事件错误进入第一章事件池");

        GameManager.DebugGoToChapter(2);
        Assert(EventManager.CanUseEvent(eventData, node), "商店事件未进入第二章事件池");

        GameManager.DebugGoToChapter(3);
        Assert(EventManager.CanUseEvent(eventData, node), "商店事件未进入第三章事件池");
    }

    private void TestEnterShopReward()
    {
        ResetRun();
        GameManager.DebugGoToChapter(2);
        var eventData = GetEvent();
        var option = eventData.Options[0];
        var originalGold = GameManager.Gold;

        Assert(
            option.Rewards.Exists(reward => reward.Type == EventRewardType.OpenShopUI),
            "进入商店选项缺少打开商店奖励");
        Assert(EventManager.TryResolveOption(eventData, option, out _), "进入商店选项结算失败");
        Assert(GameManager.Gold == originalGold + 75, "进入商店前未获得75金币");
        Assert(!EventManager.CanUseEvent(eventData, CreateEventNode()), "结算后商店事件仍可再次出现");
    }

    private void TestRobberyCostAndReward()
    {
        ResetRun();
        GameManager.DebugGoToChapter(3);
        var eventData = GetEvent();
        var option = eventData.Options[1];
        var originalPower = GameManager.Power;
        var originalGold = GameManager.Gold;

        Assert(EventManager.CanAffordOption(option, out _), "满电量时打劫选项不可用");
        Assert(EventManager.TryResolveOption(eventData, option, out _), "打劫选项结算失败");
        Assert(GameManager.Power == originalPower - 10, "打劫没有扣除10点电量");
        Assert(GameManager.Gold == originalGold + 225, "打劫没有获得225金币");
    }

    private void TestInsufficientPowerBlocksRobbery()
    {
        ResetRun();
        GameManager.DebugGoToChapter(2);
        Assert(GameManager.TrySpendPower(GameManager.Power - 9), "无法构造低电量测试状态");
        var eventData = GetEvent();
        var option = eventData.Options[1];
        var originalPower = GameManager.Power;
        var originalGold = GameManager.Gold;

        Assert(!EventManager.CanAffordOption(option, out var reason), "9点电量仍可选择打劫");
        Assert(reason == "电量不足", "打劫禁用原因不是电量不足");
        Assert(!EventManager.TryResolveOption(eventData, option, out _), "电量不足时仍成功结算打劫");
        Assert(GameManager.Power == originalPower && GameManager.Gold == originalGold, "失败结算修改了玩家资源");
    }

    private static EventData GetEvent()
    {
        return EventDatabase.GetEvent("shop_general")
            ?? throw new InvalidOperationException("找不到普通商店事件定义");
    }

    private static MapNode CreateEventNode()
    {
        return new MapNode
        {
            Id = "__general_shop_event_test__",
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
