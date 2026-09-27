//////////////////////////////////////////////////////////
// 贪婪密窟与巨大石碑奖励、锈类装备进阶回归测试。
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

public partial class GreedyVaultAndSteleRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestGreedyVaultRewards();
            TestGiantStoneSteleRewards();
            TestSteleUpgradesEveryRustItem();
            GD.Print($"GREEDY_VAULT_STELE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"GREEDY_VAULT_STELE_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestGreedyVaultRewards()
    {
        var eventData = GetEvent("greedy_vault");
        Assert(eventData.Rarity == EventRarity.Common && eventData.RepeatType == EventRepeatType.RunOnce,
            "贪婪密窟应为第一章普通、单次事件");
        Assert(eventData.Options[0].DisplayName == "选择金币", "贪婪密窟首项名称未更新");
        Assert(eventData.Options[0].RewardSequence.Actions.Single() is AddGoldRewardAction gold && gold.Amount == 200,
            "贪婪密窟的选择金币应获得200金币");
        Assert(eventData.Options[1].RewardSequence.Actions.Single() is RandomEquipmentByRarityRewardAction,
            "贪婪密窟的寻找财宝应保持随机稀有装备");
        Assert(eventData.Options[2].RewardSequence.Actions[0] is LoseMaxHpRewardAction hpCost && hpCost.Amount == 5,
            "奉上血液应失去5点最大生命");
        Assert(eventData.Options[2].RewardSequence.Actions[1] is AddEquipmentRewardAction statue
            && statue.EquipmentId == EquipmentIds.GoldenStatue,
            "奉上血液应获得黄金雕像");
    }

    private void TestGiantStoneSteleRewards()
    {
        var eventData = GetEvent("giant_stone_stele");
        Assert(eventData.Rarity == EventRarity.Epic && eventData.RepeatType == EventRepeatType.RunOnce,
            "巨大石碑应为第二章史诗、单次事件");
        Assert(eventData.Options[0].Rewards.Single(reward => reward.Type == EventRewardType.AttackTrickDamageBonus).Amount == 10,
            "石碑供奉金钱获得力量应为攻击性锦囊伤害+10");
        Assert(eventData.Options[1].Rewards.Single(reward => reward.Type == EventRewardType.MaxHp).Amount == 25,
            "石碑供奉金钱获得生命应为最大生命+25，不能额外回血");
        Assert(eventData.Options[1].Rewards.All(reward => reward.Type != EventRewardType.Heal),
            "石碑生命选项不应再额外回复当前生命");
        Assert(eventData.Options[2].RewardSequence.Actions[0] is LoseMaxHpRewardAction hpCost && hpCost.Amount == 50,
            "石碑献上鲜血应失去50点最大生命");
        Assert(eventData.Options[2].RewardSequence.Actions[1] is AddEquipmentRewardAction skillChip
            && skillChip.EquipmentId == EquipmentIds.SkillChip,
            "石碑献上鲜血应获得技能芯片");
    }

    private void TestSteleUpgradesEveryRustItem()
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.ZhaoYun);
        foreach (var equipmentId in new[]
                 {
                     EquipmentIds.RustShield,
                     EquipmentIds.RustSpear,
                     EquipmentIds.RustSword,
                     EquipmentIds.RustBlueSteelSword,
                     EquipmentIds.RustGuDingDao
                 })
        {
            Assert(GameManager.AddEquipment(equipmentId) != null, $"测试无法加入{equipmentId}");
        }

        var eventData = GetEvent("giant_stone_stele");
        var option = eventData.Options[3];
        Assert(EventManager.GetVisibleOptions(eventData).Contains(option), "持有锈类装备时石碑磨刀选项应显示");
        Assert(EventManager.TryResolveOption(eventData, option, out _), "石碑磨刀无法结算");

        AssertUpgraded(EquipmentIds.RustShield, EquipmentIds.GangDun);
        AssertUpgraded(EquipmentIds.RustSpear, EquipmentIds.HejinJian);
        AssertUpgraded(EquipmentIds.RustSword, EquipmentIds.HejinMao);
        AssertUpgraded(EquipmentIds.RustBlueSteelSword, EquipmentIds.BlueSteelSword);
        AssertUpgraded(EquipmentIds.RustGuDingDao, EquipmentIds.GuDingDao);
    }

    private void AssertUpgraded(string rustId, string advancedId)
    {
        Assert(!GameManager.OwnsEquipment(rustId) && GameManager.OwnsEquipment(advancedId),
            $"石碑未将{rustId}升级为{advancedId}");
    }

    private static EventData GetEvent(string id)
    {
        return EventManager.GetAllEvents().Single(eventData => eventData.Id == id);
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
