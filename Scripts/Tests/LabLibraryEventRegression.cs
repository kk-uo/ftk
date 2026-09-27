//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/LabLibraryEventRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证废弃实验室的强化剂、密室数据与电池数值。
// 2. 验证密室数据会确保禁书库出现，并开启传奇武器三选一。
// 3. 验证禁书库只在第三章皇宫、每局一次出现。
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 废弃实验室与禁书库事件规则的 Headless 回归入口。
/// </summary>
public partial class LabLibraryEventRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestExpiredEnhancer();
            TestSecretDataUnlocksForbiddenLibrary();
            TestLegendaryWeaponPool();
            GD.Print($"LAB_LIBRARY_EVENT_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"LAB_LIBRARY_EVENT_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestExpiredEnhancer()
    {
        ResetRun(1);
        var eventData = GetEvent("abandoned_lab");
        var option = eventData.Options[0];
        var originalMaxHp = GameManager.MaxHP;

        Assert(option.Rewards.Any(reward => reward.Type == EventRewardType.MaxHp && reward.Amount == 30),
            "注射药剂没有提供第一、二章的30点最大生命");
        Assert(option.Rewards.Any(reward => reward.Type == EventRewardType.RunBuff
                                             && reward.StringValue == RunBuffIds.ExpiredEnhancer),
            "注射药剂没有提供过期强化剂");
        Assert(EventManager.TryResolveOption(eventData, option, out _), "注射药剂无法结算");
        Assert(GameManager.MaxHP == originalMaxHp + 30, "注射药剂没有增加30点最大生命");
        Assert(RunBuffManager.GetPlayerBattleStartManaBonus() == 2,
            "过期强化剂没有在战斗开始时提供2费");

        RunBuffManager.ApplyChapterStart(3);
        Assert(GameManager.MaxHP == originalMaxHp, "进入第三章后过期强化剂的30点最大生命没有移除");
        Assert(RunBuffManager.CountStacks(RunBuffIds.ExpiredEnhancer) == 0,
            "进入第三章后过期强化剂没有结束");
        Assert(RunBuffManager.GetPlayerBattleStartManaBonus() == 0,
            "进入第三章后过期强化剂仍提供战斗开始费用");
    }

    private void TestSecretDataUnlocksForbiddenLibrary()
    {
        ResetRun(1);
        var lab = GetEvent("abandoned_lab");
        var originalGold = GameManager.Gold;
        Assert(EventManager.TryResolveOption(lab, lab.Options[2], out _), "收集数据无法结算");
        Assert(GameManager.Gold == originalGold + 50, "收集数据没有获得50金币");
        Assert(RunBuffManager.CountStacks(RunBuffIds.ExperimentData) == 1, "收集数据没有保留密室数据");

        GameManager.DebugGoToChapter(3);
        GameManager.SetChapterRoute(ChapterRoute.Imperial);
        var library = GetEvent("forbidden_library");
        var node = new MapNode { Id = "__lab_library_test__", Type = MapNodeType.Event, StageIndex = 1 };

        Assert(library.Rarity == EventRarity.Common, "禁书库不是普通事件");
        Assert(library.RepeatType == EventRepeatType.RunOnce && !library.IsRepeatable,
            "禁书库不是单局一次事件");
        Assert(EventManager.CanUseEvent(library, node), "持有密室数据时禁书库未在第三章皇宫出现");
        Assert(library.Options[4].Rewards.Any(reward =>
                reward.Type == EventRewardType.ChooseOneLegendaryWeaponEquipment),
            "密室数据暗门没有提供传奇武器三选一");

        GameManager.SetChapterRoute(ChapterRoute.Sewer);
        Assert(!EventManager.CanUseEvent(library, node), "禁书库错误地出现在第三章下水道路线上");
    }

    private void TestLegendaryWeaponPool()
    {
        var choices = new EquipmentChoiceProvider
        {
            Count = 3,
            Rarities = new[] { EquipmentRarity.Legendary },
            SlotCategories = new[] { EquipmentSlotCategory.Weapon }
        }.CreateChoices();

        Assert(choices.Count == 3, "禁书库传奇武器池无法提供三选一");
        Assert(choices.All(choice => choice.Payload is EquipmentDefinition definition
                                     && definition.Types.Contains(EquipmentType.Weapon)),
            "禁书库传奇武器选择混入了非武器装备");
    }

    private static EventData GetEvent(string id)
        => EventDatabase.GetEvent(id) ?? throw new InvalidOperationException($"找不到事件：{id}");

    private static void ResetRun(int chapter)
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.ZhaoYun);
        InventoryManager.Reset();
        GameManager.DebugGoToChapter(chapter);
        if (chapter == 3)
        {
            GameManager.SetChapterRoute(ChapterRoute.Imperial);
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
