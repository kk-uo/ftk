//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/ChibiRuinsEventRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证赤壁残骸仅在第一章出现且每个Run只能完成一次。
// 2. 验证打捞、船夫、沼泽跳转与华容道四个分支的资源结算。
// 3. 验证吴阵营免费与魏阵营隐藏选项条件。
//
// 不负责：
// × 验证ChoicePanel的视觉布局。
// × 验证随机装备的概率分布。
//
// 主要依赖：
// EventDatabase
// EventManager
// GameManager
// RunBuffManager
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 赤壁残骸事件规则的 Headless 回归入口。
/// </summary>
public partial class ChibiRuinsEventRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行赤壁残骸事件回归，并通过进程退出码返回测试状态。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestMetadataAndChapterIsolation();
            TestSalvageRewards();
            TestBoatmanFactionCost();
            TestSwampTransition();
            TestHuarongVisibilityAndChoice();
            GD.Print($"CHIBI_RUINS_EVENT_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"CHIBI_RUINS_EVENT_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestMetadataAndChapterIsolation()
    {
        ResetRun(CharacterIds.ZhaoYun);
        var eventData = GetEvent();
        var node = CreateEventNode();

        Assert(eventData.Rarity == EventRarity.Common, "赤壁残骸不是普通事件");
        Assert(eventData.RepeatType == EventRepeatType.RunOnce && !eventData.IsRepeatable, "赤壁残骸不是RunOnce事件");
        Assert(EventManager.CanUseEvent(eventData, node), "赤壁残骸未进入第一章事件池");

        GameManager.DebugGoToChapter(2);
        Assert(!EventManager.CanUseEvent(eventData, node), "赤壁残骸错误进入第二章事件池");
    }

    private void TestSalvageRewards()
    {
        ResetRun(CharacterIds.ZhaoYun);
        var eventData = GetEvent();
        var option = eventData.Options[0];
        var originalEquipmentCount = InventoryManager.GetAllOwned().Count;
        var originalGold = GameManager.Gold;
        var originalPower = GameManager.Power;

        Assert(EventManager.CanAffordOption(option, out _), "打捞残骸不可用");
        Assert(EventManager.TryResolveOption(eventData, option, out _), "打捞残骸结算失败");
        Assert(InventoryManager.GetAllOwned().Count == originalEquipmentCount + 2, "打捞残骸没有获得两件装备");
        Assert(
            InventoryManager.GetAllOwned().Skip(originalEquipmentCount)
                .All(owned => owned.Definition.Rarity == EquipmentRarity.Common),
            "打捞残骸获得了非普通品质装备");
        Assert(GameManager.Gold == originalGold, "打捞残骸不应改变金币");
        Assert(GameManager.Power == originalPower, "打捞残骸不应改变电量");
        Assert(!EventManager.CanUseEvent(eventData, CreateEventNode()), "赤壁残骸完成后仍可在同一Run重复出现");
    }

    private void TestBoatmanFactionCost()
    {
        ResetRun(CharacterIds.LuXun);
        var wuEvent = GetEvent();
        var wuGold = GameManager.Gold;
        GameManager.AddCurrentHp(-10);
        Assert(EventManager.TryResolveOption(wuEvent, wuEvent.Options[1], out _), "吴阵营寻找船夫结算失败");
        Assert(GameManager.Gold == wuGold, "吴阵营寻找船夫仍被扣除金币");
        Assert(GameManager.CurrentHP == GameManager.MaxHP, "寻找船夫没有恢复10点生命");
        Assert(
            InventoryManager.GetAllOwned().Any(owned => owned.Definition.Id == EquipmentIds.WaterproofModule),
            "寻找船夫没有获得防水模块");

        ResetRun(CharacterIds.ZhaoYun);
        var nonWuEvent = GetEvent();
        var nonWuGold = GameManager.Gold;
        Assert(EventManager.TryResolveOption(nonWuEvent, nonWuEvent.Options[1], out _), "非吴阵营寻找船夫结算失败");
        Assert(GameManager.Gold == nonWuGold - 50, "非吴阵营寻找船夫没有扣除50金币");
    }

    private void TestSwampTransition()
    {
        ResetRun(CharacterIds.ZhaoYun);
        var eventData = GetEvent();
        var option = eventData.Options[2];

        Assert(
            RewardManager.HasJumpEvent(option.RewardSequence, out var jumpEventId)
            && jumpEventId == "ancient_phantom",
            "进入沼泽奖励序列没有跳转到旧日虚影事件");
        Assert(EventManager.TryResolveOption(eventData, option, out _), "进入沼泽选项结算失败");
        Assert(RunBuffManager.CountStacks(RunBuffIds.Darkness) == 0, "进入沼泽不应额外获得黑暗Buff");
    }

    private void TestHuarongVisibilityAndChoice()
    {
        ResetRun(CharacterIds.ZhaoYun);
        var shuEvent = GetEvent();
        Assert(!EventManager.GetVisibleOptions(shuEvent).Contains(shuEvent.Options[3]), "非魏阵营错误显示华容道选项");

        ResetRun(CharacterIds.ZhenJi);
        var weiEvent = GetEvent();
        var option = weiEvent.Options[3];
        Assert(EventManager.GetVisibleOptions(weiEvent).Contains(option), "魏阵营没有显示华容道选项");
        Assert(
            RewardManager.HasChoiceRequest(option.RewardSequence, RewardChoiceType.Chip),
            "华容道奖励序列没有请求芯片三选一");
        Assert(EventManager.TryResolveOption(weiEvent, option, out _), "魏阵营华容道选项结算失败");
    }

    private static EventData GetEvent()
    {
        return EventDatabase.GetEvent("chibi_ruins")
            ?? throw new InvalidOperationException("找不到赤壁残骸事件定义");
    }

    private static MapNode CreateEventNode()
    {
        return new MapNode
        {
            Id = "__chibi_ruins_event_test__",
            Type = MapNodeType.Event,
            StageIndex = 1
        };
    }

    private static void ResetRun(string characterId)
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(characterId);
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
