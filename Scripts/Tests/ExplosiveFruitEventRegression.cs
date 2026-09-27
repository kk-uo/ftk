//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/ExplosiveFruitEventRegression.cs
//
// 职责：验证【爆炸果实】的章节范围、五项选择、奖励结算与无结果页配置。
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 爆炸果实事件的 Headless 回归入口。
/// </summary>
public partial class ExplosiveFruitEventRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestMetadataAndRouteAvailability();
            TestOptionVisibilityAndDefinitions();
            TestTouchLethalRecoveryAndAccessorySlot();
            TestPowerKnowledgeAndFireAttackRewards();
            GD.Print($"EXPLOSIVE_FRUIT_EVENT_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"EXPLOSIVE_FRUIT_EVENT_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestMetadataAndRouteAvailability()
    {
        ResetRun();
        var eventData = GetEvent();
        var node = CreateEventNode();

        Assert(eventData.Rarity == EventRarity.Rare, "爆炸果实不是稀有事件");
        Assert(eventData.RepeatType == EventRepeatType.RunOnce && !eventData.IsRepeatable, "爆炸果实不是整局唯一事件");
        Assert(eventData.SuppressResultPresentation, "爆炸果实仍会显示选项结果页");
        Assert(EventManager.CanUseEvent(eventData, node), "爆炸果实未进入第二章城市事件池");

        GameManager.SetChapterRoute(ChapterRoute.Sewer);
        Assert(EventManager.CanUseEvent(eventData, node), "爆炸果实未进入第二章下水道事件池");

        GameManager.DebugGoToChapter(1);
        Assert(!EventManager.CanUseEvent(eventData, node), "爆炸果实错误进入第一章事件池");
    }

    private void TestOptionVisibilityAndDefinitions()
    {
        ResetRun();
        var eventData = GetEvent();
        Assert(eventData.Options.Count == 5, "爆炸果实没有严格保留五个选项");
        Assert(EventManager.GetVisibleOptions(eventData).Count == 4, "未拥有万箭齐发时远射选项不应显示");

        var arrowOption = eventData.Options[1];
        Assert(arrowOption.Conditions.Count == 1
               && arrowOption.Conditions[0].Type == EventConditionType.HasPlayerCardType
               && arrowOption.Conditions[0].StringValue == nameof(CardType.ArrowBarrage), "远射选项没有正确要求万箭齐发");

        GameManager.AddPlayerCardType(CardType.ArrowBarrage);
        Assert(EventManager.GetVisibleOptions(eventData).Count == 5, "拥有万箭齐发后远射选项没有显示");
        Assert(eventData.Options.All(option => string.IsNullOrEmpty(option.ResultText) && string.IsNullOrEmpty(option.ResultTextKey)),
            "爆炸果实选项仍配置了结果文本");
    }

    private void TestTouchLethalRecoveryAndAccessorySlot()
    {
        ResetRun();
        var option = GetEvent().Options[0];
        var forageBefore = GameManager.Forage;

        Assert(EventManager.TryResolveOption(GetEvent(), option, out _), "碰一下选项无法结算");
        Assert(GameManager.Forage == forageBefore - 1, "碰一下致死时没有消耗粮草");
        Assert(GameManager.CurrentHP == GameManager.MaxHP, "碰一下致死后没有回满生命值");
        Assert(GameManager.HasExplosiveFruitAccessorySlot, "碰一下后没有获得普通饰品槽");
    }

    private void TestPowerKnowledgeAndFireAttackRewards()
    {
        ResetRun();
        GameManager.AddPlayerCardType(CardType.ArrowBarrage);
        Assert(EventManager.TryResolveOption(GetEvent(), GetEvent().Options[1], out _), "远处射箭激发果实选项无法结算");
        Assert(GameManager.IsArrowBarrageFireUpgraded, "远处射箭没有赋予万箭齐发火属性强化");

        ResetRun();
        var powerOption = GetEvent().Options[2];
        Assert(EventManager.TryResolveOption(GetEvent(), powerOption, out _), "小心绕行选项无法结算");
        Assert(GameManager.MaxPower == GameManager.InitialMaxPower + 15, "小心绕行没有增加15点电量上限");
        Assert(GameManager.Power == GameManager.InitialMaxPower + 5, "小心绕行没有在加上限后扣除10点当前电量");

        ResetRun();
        Assert(EventManager.TryResolveOption(GetEvent(), GetEvent().Options[3], out _), "进行分析选项无法结算");
        Assert(GameManager.KnowledgeChipCount == 1, "进行分析没有获得知识芯片");

        ResetRun();
        Assert(EventManager.TryResolveOption(GetEvent(), GetEvent().Options[4], out _), "掌握反应选项无法结算");
        Assert(GameManager.HasPlayerCardType(CardType.FireAttack), "掌握反应没有获得火攻");
    }

    private static EventData GetEvent()
    {
        return EventDatabase.GetEvent("explosive_fruit")
            ?? throw new InvalidOperationException("找不到爆炸果实事件定义");
    }

    private static MapNode CreateEventNode()
    {
        return new MapNode { Id = "__explosive_fruit_event_test__", Type = MapNodeType.Event, StageIndex = 1 };
    }

    private static void ResetRun()
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.MaChao);
        GameManager.DebugGoToChapter(2);
        GameManager.SetChapterRoute(ChapterRoute.Default);
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
