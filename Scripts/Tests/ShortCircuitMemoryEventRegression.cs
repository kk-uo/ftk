//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/ShortCircuitMemoryEventRegression.cs
//
// 职责：验证【意外短路】的第一章投放、五项选择与顺手牵羊非默认牌池规则。
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 【意外短路】事件的 Headless 回归入口。
/// </summary>
public partial class ShortCircuitMemoryEventRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestEventMetadataAndOptions();
            TestCardReplacementRewards();
            TestStealIsEarnedRatherThanStartingCard();
            GD.Print($"SHORT_CIRCUIT_MEMORY_EVENT_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"SHORT_CIRCUIT_MEMORY_EVENT_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestEventMetadataAndOptions()
    {
        ResetRun();
        var eventData = GetEvent();
        var node = new MapNode { Id = "__short_circuit_memory_test__", Type = MapNodeType.Event, StageIndex = 1 };

        Assert(eventData.Category == EventCategory.Common && eventData.Rarity == EventRarity.Common,
            "意外短路不是普通事件");
        Assert(eventData.RepeatType == EventRepeatType.RunOnce && !eventData.IsRepeatable,
            "意外短路不是整局唯一事件");
        Assert(EventManager.CanUseEvent(eventData, node), "意外短路未进入第一章事件池");
        Assert(eventData.Options.Count == 5, "意外短路没有严格保留五个选项");
        Assert(EventManager.GetVisibleOptions(eventData).Count == 5,
            "默认初始牌组下意外短路应显示全部五个选项");

        var option1 = eventData.Options[0];
        Assert(IsReplacement(option1, CardType.ThunderKill, CardType.NanmanInvasion),
            "选项一不是雷杀替换为南蛮入侵");
        var option2 = eventData.Options[1];
        Assert(IsReplacement(option2, CardType.FireKill, CardType.ArrowBarrage),
            "选项二不是火杀替换为万箭齐发");
        var option3 = eventData.Options[2];
        Assert(IsReplacement(option3, CardType.FireKill, CardType.FireAttack),
            "选项三不是火杀替换为火攻");
        Assert(option3.Costs.Count == 1
               && option3.Costs[0].Type == EventCostType.Gold
               && option3.Costs[0].Amount == 200,
            "选项三没有消耗200金币");
        var option4 = eventData.Options[3];
        Assert(option4.Costs.Count == 1
               && option4.Costs[0].Type == EventCostType.Gold
               && option4.Costs[0].Amount == 50
               && option4.Rewards.Count == 1
               && option4.Rewards[0].Type == EventRewardType.AddPlayerCardType
               && option4.Rewards[0].StringValue == nameof(CardType.Steal),
            "选项四不是失去50金币获得顺手牵羊");
        Assert(eventData.Options[4].Rewards.Any(reward => reward.Type == EventRewardType.ChooseOneChipOnce),
            "选项五没有提供芯片三选一");
    }

    private void TestCardReplacementRewards()
    {
        ResetRun();
        var eventData = GetEvent();
        Assert(EventManager.TryResolveOption(eventData, eventData.Options[0], out _), "雷杀替换选项无法结算");
        Assert(!GameManager.HasPlayerCardType(CardType.ThunderKill) && GameManager.HasPlayerCardType(CardType.NanmanInvasion),
            "雷杀没有正确替换为南蛮入侵");

        ResetRun();
        eventData = GetEvent();
        GameManager.AddGold(200);
        var goldBefore = GameManager.Gold;
        Assert(EventManager.TryResolveOption(eventData, eventData.Options[2], out _), "火攻替换选项无法结算");
        Assert(!GameManager.HasPlayerCardType(CardType.FireKill) && GameManager.HasPlayerCardType(CardType.FireAttack),
            "火杀没有正确替换为火攻");
        Assert(GameManager.Gold == goldBefore - 200, "火攻替换没有扣除200金币");
    }

    private void TestStealIsEarnedRatherThanStartingCard()
    {
        ResetRun();
        Assert(!GameManager.HasPlayerCardType(CardType.Steal), "顺手牵羊仍然存在于默认初始牌组");

        var eventData = GetEvent();
        var goldBefore = GameManager.Gold;
        Assert(goldBefore >= 50, "测试运行没有足够金币验证顺手牵羊选项");
        Assert(EventManager.TryResolveOption(eventData, eventData.Options[3], out _), "失去金币获得顺手牵羊选项无法结算");
        Assert(GameManager.Gold == goldBefore - 50, "获得顺手牵羊没有扣除50金币");
        Assert(GameManager.HasPlayerCardType(CardType.Steal), "事件奖励后顺手牵羊没有加入出牌栏");
    }

    private static bool IsReplacement(EventOption option, CardType oldType, CardType newType)
    {
        return option.Conditions.Any(condition => condition.Type == EventConditionType.HasPlayerCardType
                                                   && condition.StringValue == oldType.ToString())
               && option.Rewards.Count == 1
               && option.Rewards[0].Type == EventRewardType.ReplaceCardType
               && option.Rewards[0].StringValue == oldType.ToString()
               && option.Rewards[0].SecondaryStringValue == newType.ToString();
    }

    private static EventData GetEvent()
    {
        return EventDatabase.GetEvent("short_circuit_memory")
            ?? throw new InvalidOperationException("找不到意外短路事件定义");
    }

    private static void ResetRun()
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.MaChao);
        GameManager.DebugGoToChapter(1);
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
