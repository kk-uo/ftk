using Godot;
using System;
using System.Linq;

/// <summary>
/// 验证长坂坡第一章事件的赵云保底、阵营专属选项与奖励结算。
/// </summary>
public partial class ChangbanpoEventRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestMetadataAndZhaoYunGuarantee();
            TestBasicRewards();
            TestExclusiveOptions();
            GD.Print($"CHANGBANPO_EVENT_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"CHANGBANPO_EVENT_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestMetadataAndZhaoYunGuarantee()
    {
        ResetRun(CharacterIds.ZhaoYun);
        var eventData = GetEvent();
        var node = CreateEventNode();

        Assert(eventData.Rarity == EventRarity.Common, "长坂坡不是普通事件");
        Assert(eventData.RepeatType == EventRepeatType.RunOnce && !eventData.IsRepeatable, "长坂坡不是每局唯一事件");
        Assert(eventData.GuaranteedForCharacterId == CharacterIds.ZhaoYun, "赵云没有获得长坂坡第一章保底");
        Assert(EventManager.CanUseEvent(eventData, node), "长坂坡没有进入第一章事件池");

        GameManager.DebugGoToChapter(2);
        Assert(!EventManager.CanUseEvent(eventData, node), "长坂坡错误进入第二章事件池");
    }

    private void TestBasicRewards()
    {
        ResetRun(CharacterIds.MaChao);
        var eventData = GetEvent();
        var originalOwnedCount = InventoryManager.GetAllOwned().Count;
        Assert(EventManager.TryResolveOption(eventData, eventData.Options[0], out _), "翻找武器结算失败");

        var rewarded = InventoryManager.GetAllOwned().Skip(originalOwnedCount).ToArray();
        Assert(rewarded.Length == 2, "翻找武器没有获得两件装备");
        Assert(rewarded.Any(item => item.Definition.Rarity == EquipmentRarity.Common
                                    && InventoryManager.GetSlotCategory(item.Definition) == EquipmentSlotCategory.Weapon),
            "翻找武器没有获得普通品质武器");
        Assert(rewarded.Any(item => item.Definition.Rarity == EquipmentRarity.Common
                                    && InventoryManager.GetSlotCategory(item.Definition) == EquipmentSlotCategory.Armor),
            "翻找武器没有获得普通品质护甲");

        ResetRun(CharacterIds.MaChao);
        var goldBefore = GameManager.Gold;
        Assert(EventManager.TryResolveOption(GetEvent(), GetEvent().Options[1], out _), "拼凑尸体结算失败");
        Assert(GameManager.Gold == goldBefore + 100, "拼凑尸体没有获得100金币");

        ResetRun(CharacterIds.MaChao);
        Assert(EventManager.TryResolveOption(GetEvent(), GetEvent().Options[2], out _), "吸收记忆结算失败");
        Assert(GameManager.DefenseChipCount == 1, "吸收记忆没有获得防御芯片");
    }

    private void TestExclusiveOptions()
    {
        ResetRun(CharacterIds.ZhaoYun);
        var zhaoYunEvent = GetEvent();
        var zhaoYunVisible = EventManager.GetVisibleOptions(zhaoYunEvent);
        Assert(zhaoYunVisible.Contains(zhaoYunEvent.Options[3]), "赵云没有显示搜寻古井选项");
        Assert(zhaoYunVisible.Contains(zhaoYunEvent.Options[4]), "蜀国角色没有显示知识芯片悼念选项");
        Assert(EventManager.TryResolveOption(zhaoYunEvent, zhaoYunEvent.Options[3], out _), "赵云搜寻古井结算失败");
        Assert(GameManager.OwnsEquipment(EquipmentIds.BlueSteelSword) && GameManager.OwnsEquipment(EquipmentIds.MysteriousChip),
            "赵云搜寻古井没有获得青钢剑和神秘芯片");

        ResetRun(CharacterIds.ZhenJi);
        var weiEvent = GetEvent();
        var weiVisible = EventManager.GetVisibleOptions(weiEvent);
        Assert(weiVisible.Contains(weiEvent.Options[5]), "魏国角色没有显示攻击芯片悼念选项");
        Assert(!weiVisible.Contains(weiEvent.Options[4]), "魏国角色错误显示蜀国悼念选项");
        Assert(EventManager.TryResolveOption(weiEvent, weiEvent.Options[5], out _), "魏国悼念结算失败");
        Assert(GameManager.AttackChipCount == 1, "魏国悼念没有获得攻击芯片");
    }

    private static EventData GetEvent()
    {
        return EventDatabase.GetEvent("changbanpo")
            ?? throw new InvalidOperationException("找不到长坂坡事件定义");
    }

    private static MapNode CreateEventNode()
    {
        return new MapNode { Id = "__changbanpo_event_test__", Type = MapNodeType.Event, StageIndex = 1 };
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
