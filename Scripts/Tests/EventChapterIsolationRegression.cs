//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/EventChapterIsolationRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证第一章限定事件不会进入后续章节事件池。
// 2. 验证仙丹术士仍可在第一章正常出现。
//
// 不负责：
// × 控制事件随机权重。
// × 模拟事件选项结算。
//
// 主要依赖：
// EventDatabase
// EventManager
// GameManager
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 事件章节隔离规则的独立Headless回归入口。
/// </summary>
public partial class EventChapterIsolationRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行章节事件池回归，并通过进程退出码返回测试状态。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            Run();
            GD.Print($"EVENT_CHAPTER_ISOLATION_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"EVENT_CHAPTER_ISOLATION_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run()
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.ZhaoYun);
        var eventData = EventDatabase.GetEvent("xiandan_shushi")
            ?? throw new InvalidOperationException("找不到仙丹术士事件定义。");
        var eventNode = new MapNode
        {
            Id = "__event_chapter_isolation__",
            Type = MapNodeType.Event,
            StageIndex = 1
        };

        Assert(GameManager.CurrentChapter == 1, "新Run没有从第一章开始");
        Assert(EventManager.CanUseEvent(eventData, eventNode), "仙丹术士在第一章被错误排除");

        GameManager.DebugGoToChapter(2);
        GameManager.SetChapterRoute(ChapterRoute.Sewer);
        Assert(GameManager.CurrentChapter == 2, "未进入第二章测试环境");
        Assert(GameManager.CurrentChapterRoute == ChapterRoute.Sewer, "未进入下水道路线测试环境");
        Assert(!EventManager.CanUseEvent(eventData, eventNode), "仙丹术士仍可进入第二章下水道事件池");

        eventNode.FixedEventId = eventData.Id;
        var replacement = EventManager.GetEventForNode(eventNode);
        Assert(replacement?.Id != eventData.Id, "旧FixedEventId仍绕过章节条件打开仙丹术士");
        Assert(eventNode.FixedEventId != eventData.Id, "失效的仙丹术士FixedEventId没有被清除");
        Assert(replacement == null || eventNode.FixedEventId == replacement.Id, "替换事件没有重新缓存到节点");

        foreach (var candidate in EventManager.GetAllEvents())
        {
            if (candidate.Id == eventData.Id)
            {
                Assert(!EventManager.CanUseEvent(candidate, eventNode), "遍历事件池时仙丹术士未被章节条件过滤");
            }
        }
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
