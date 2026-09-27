using Godot;
using System;
using System.Linq;

/// <summary>第四章深渊事件、出牌栏新增与元素词条的无界面回归。</summary>
public partial class AbyssEventsRegression : Node
{
    private int _assertions;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestChapterFourNodes();
            TestAbyssCall();
            TestElementalAltarCardsAndAttributes();
            TestMoonGemAwakening();
            GD.Print($"ABYSS_EVENTS_TEST_PASS assertions={_assertions}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"ABYSS_EVENTS_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestChapterFourNodes()
    {
        ResetRun();
        GameManager.DebugGoToChapter(4);
        Assert(GameManager.GetNode("event_4_2")?.FixedEventId == "abyss_call", "第四章第一个事件节点未锁定深渊呼唤");
        Assert(GameManager.GetNode("event_4_4")?.FixedEventId == "elemental_altar", "第四章第二个事件节点未锁定元素祭坛");
    }

    private void TestAbyssCall()
    {
        ResetRun();
        GameManager.DebugGoToChapter(4);
        var call = GetEvent("abyss_call");
        Assert(call.Rarity == EventRarity.Common && call.RepeatType == EventRepeatType.RunOnce, "深渊呼唤元数据错误");
        var beforeMax = GameManager.MaxHP;
        Assert(EventManager.TryResolveOption(call, call.Options[0], out _), "回应呼唤结算失败");
        Assert(GameManager.MaxHP == beforeMax - beforeMax * 30 / 100, "回应呼唤未扣除30%最大生命");
        Assert(GameManager.AcquiredSkills.Count > 0, "回应呼唤没有发放技能芯片效果");

        ResetRun();
        GameManager.DebugGoToChapter(4);
        var fanatical = GetEvent("abyss_call").Options[1];
        GameManager.IncrementAttackChipCount();
        Assert(RewardManager.HasChoiceRequest(fanatical.RewardSequence, RewardChoiceType.Skill), "狂热加入没有请求技能二选一");
        Assert(EventManager.TryResolveOption(GetEvent("abyss_call"), fanatical, out _), "狂热加入结算失败");
        Assert(GameManager.AttackChipCount == 0, "狂热加入没有清除芯片");
    }

    private void TestElementalAltarCardsAndAttributes()
    {
        ResetRun();
        GameManager.DebugGoToChapter(4);
        var altar = GetEvent("elemental_altar");
        Assert(EventManager.TryResolveOption(altar, altar.Options[0], out _), "召唤冰元素结算失败");
        Assert(GameManager.HasPlayerCardType(CardType.IceKill), "冰元素没有把冰杀加入出牌栏");

        ResetRun();
        GameManager.DebugGoToChapter(4);
        altar = GetEvent("elemental_altar");
        Assert(EventManager.TryResolveOption(altar, altar.Options[2], out _), "召唤火焰结算失败");
        Assert(AttackAttributeRules.GetAttributes(CardType.Kill).HasFlag(AttackAttribute.Fire), "火焰祭坛没有给普通杀添加火属性");
        var player = new Player("玩家", "abyss_attribute_player", BattleTeam.Player);
        var enemy = new Player("敌人", "abyss_attribute_enemy", BattleTeam.Enemy);
        var damage = new DamageEvent(player, enemy, CardType.Kill, 10, isDirectAttackDamage: true);
        Assert(damage.DamageType.HasFlag(DamageType.Fire) && !damage.DamageType.HasFlag(DamageType.Physical), "火焰祭坛攻击未按火属性进入伤害管线");
    }

    private void TestMoonGemAwakening()
    {
        ResetRun();
        GameManager.DebugGoToChapter(4);
        GameManager.AddEquipment(EquipmentIds.MoonGem, EquipmentGainSource.GameplayReward);
        var altar = GetEvent("elemental_altar");
        var option = altar.Options[4];
        Assert(EventManager.GetVisibleOptions(altar).Contains(option), "拥有月亮宝石时未显示进化选项");
        Assert(EventManager.TryResolveOption(altar, option, out _), "月亮宝石进化结算失败");
        var attributes = AttackAttributeRules.GetAttributes(CardType.CelestialImpact);
        Assert(attributes.HasFlag(AttackAttribute.Fire) && attributes.HasFlag(AttackAttribute.Thunder)
            && attributes.HasFlag(AttackAttribute.Ice) && attributes.HasFlag(AttackAttribute.Poison), "天体撞击未获得四元素属性");
    }

    private static EventData GetEvent(string id)
        => EventDatabase.GetEvent(id) ?? throw new InvalidOperationException($"找不到事件：{id}");

    private static void ResetRun()
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.ZhaoYun);
        InventoryManager.Reset();
    }

    private void Assert(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
