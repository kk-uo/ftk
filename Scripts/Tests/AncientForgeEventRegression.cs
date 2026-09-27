//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/AncientForgeEventRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证泉水可将锈古锭刀升级为古锭刀。
// 2. 验证古蜀铸炉的章节、路线、奖励与隐藏选项配置。
// 3. 验证专属武器升级会消耗芯片并保留装备槽位。
//
// 不负责：
// × 模拟 ChoicePanel 鼠标交互。
// × 验证事件界面美术表现。
//
// 主要依赖：
// EventManager
// RewardManager
// InventoryManager
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 古蜀铸炉与泉水武器升级的 Headless 回归入口。
/// </summary>
public partial class AncientForgeEventRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行事件回归并通过进程退出码返回测试状态。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestLifeSpringGuDingDaoUpgrade();
            TestAncientForgeMetadata();
            TestSoulSacrificeSettlement();
            TestZhaoYunHiddenUpgrade();
            TestXuShengHiddenUpgrade();
            TestHigherRarityCandidateFilter();
            GD.Print($"ANCIENT_FORGE_EVENT_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"ANCIENT_FORGE_EVENT_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestLifeSpringGuDingDaoUpgrade()
    {
        ResetRun(CharacterIds.XuSheng);
        var rustBlade = AddEquipment(EquipmentIds.RustGuDingDao);
        Assert(InventoryManager.EquipToSlot(rustBlade.InstanceId, EquipmentSlot.Weapon),
            "泉水测试无法装备锈古锭刀");

        var eventData = GetEvent("life_spring");
        var option = eventData.Options[1];
        var previousBonus = GameManager.RunKillDamageBonus;

        Assert(EventManager.GetVisibleOptions(eventData).Contains(option), "持有锈古锭刀时淬炼刀刃没有显示");
        Assert(EventManager.TryResolveOption(eventData, option, out _), "淬炼刀刃无法结算");
        Assert(!GameManager.OwnsEquipment(EquipmentIds.RustGuDingDao), "锈古锭刀没有被消耗");
        Assert(GameManager.OwnsEquipment(EquipmentIds.GuDingDao), "没有获得古锭刀");
        Assert(GameManager.RunKillDamageBonus == previousBonus + 5, "泉水杀系伤害加成不是+5");

        var upgraded = InventoryManager.GetAllOwned()
            .Single(item => item.Definition.Id == EquipmentIds.GuDingDao);
        Assert(upgraded.EquippedSlot == EquipmentSlot.Weapon, "泉水升级后没有保留武器槽位");
    }

    private void TestAncientForgeMetadata()
    {
        var eventData = GetEvent("ancient_forge");
        Assert(eventData.Rarity == EventRarity.Epic, "古蜀铸炉不是史诗事件");
        Assert(eventData.RepeatType == EventRepeatType.RunOnce && !eventData.IsRepeatable,
            "古蜀铸炉不是本局一次性事件");
        Assert(eventData.Options.Count == 6, "古蜀铸炉没有六个选项");
        Assert(eventData.Conditions.Any(condition =>
                condition.Type == EventConditionType.CurrentChapter
                && condition.IntValue == 3),
            "古蜀铸炉没有限制第三章");
        Assert(eventData.Conditions.Any(condition =>
                condition.Type == EventConditionType.CurrentChapterRoute
                && condition.StringValue == nameof(ChapterRoute.Imperial)),
            "古蜀铸炉没有限制皇宫路线");

        Assert(eventData.Options[0].Rewards.Count(reward =>
                reward.Type == EventRewardType.RunBuff
                && reward.StringValue == RunBuffIds.Curse) == 2,
            "献祭装备没有给予2层诅咒");
        Assert(eventData.Options[0].Rewards.Any(reward =>
                reward.Type == EventRewardType.DestroyAllEquipmentAndGold),
            "献祭装备没有摧毁全部装备与金币");
        Assert(eventData.Options[0].Rewards.Any(reward =>
                reward.Type == EventRewardType.ChooseOneLegendaryEquipment),
            "献祭装备没有传奇装备三选一");

        Assert(eventData.Options[1].Rewards.Any(reward =>
                reward.Type == EventRewardType.SetCurrentHpToOneAndLoseMaxHpPercent
                && reward.Amount == 30),
            "献祭灵魂没有设为1点生命并扣除30%最大生命");
        Assert(!eventData.Options[1].Rewards.Any(reward =>
                reward.Type == EventRewardType.RunBuff
                && reward.StringValue == RunBuffIds.Curse),
            "献祭灵魂不应再给予诅咒");
        Assert(eventData.Options[1].Rewards.Any(reward =>
                reward.Type == EventRewardType.ChooseOneLegendaryEquipment),
            "献祭灵魂没有传奇装备三选一");
        Assert(eventData.Options[2].Rewards.Any(reward =>
                reward.Type == EventRewardType.ChooseOneRareOrEpicEquipment),
            "寻找装备没有稀有/史诗三选一");
        Assert(eventData.Options[3].Rewards.Count(reward =>
                reward.Type == EventRewardType.RunBuff
                && reward.StringValue == RunBuffIds.Curse) == 5,
            "回炉重铸没有给予5层诅咒");
        Assert(eventData.Options[3].Rewards.Any(reward =>
                reward.Type == EventRewardType.SacrificeOneEquipmentForUpgrade),
            "回炉重铸没有请求装备升级选择");
    }

    private void TestSoulSacrificeSettlement()
    {
        ResetRun(CharacterIds.ZhaoYun);
        var previousMaxHp = GameManager.MaxHP;
        var previousCurseStacks = RunBuffManager.CountStacks(RunBuffIds.Curse);
        var eventData = GetEvent("ancient_forge");

        Assert(EventManager.TryResolveOption(eventData, eventData.Options[1], out _),
            "献祭灵魂无法结算");
        Assert(GameManager.CurrentHP == 1, "献祭灵魂结算后当前生命不是1点");
        Assert(GameManager.MaxHP == previousMaxHp - previousMaxHp * 30 / 100,
            "献祭灵魂结算后最大生命没有降低30%");
        Assert(RunBuffManager.CountStacks(RunBuffIds.Curse) == previousCurseStacks,
            "献祭灵魂不应额外获得诅咒");
    }

    private void TestZhaoYunHiddenUpgrade()
    {
        ResetRun(CharacterIds.ZhaoYun);
        var sword = AddEquipment(EquipmentIds.BlueSteelSword);
        Assert(InventoryManager.EquipToSlot(sword.InstanceId, EquipmentSlot.Weapon),
            "赵云测试无法装备青钢剑");
        AddEquipment(EquipmentIds.MysteriousChip);

        var eventData = GetEvent("ancient_forge");
        var option = eventData.Options[4];
        Assert(EventManager.GetVisibleOptions(eventData).Contains(option), "赵云隐藏选项未显示");
        Assert(EventManager.TryResolveOption(eventData, option, out _), "赵云隐藏选项无法结算");
        Assert(!GameManager.OwnsEquipment(EquipmentIds.BlueSteelSword), "青钢剑没有被消耗");
        Assert(!GameManager.OwnsEquipment(EquipmentIds.MysteriousChip), "赵云升级没有消耗神秘芯片");
        Assert(GameManager.OwnsEquipment(EquipmentIds.TrueBlueSteelSword), "赵云没有获得真·青钢剑");

        var upgraded = InventoryManager.GetAllOwned()
            .Single(item => item.Definition.Id == EquipmentIds.TrueBlueSteelSword);
        Assert(upgraded.EquippedSlot == EquipmentSlot.Weapon, "真·青钢剑没有保留武器槽位");

        ResetRun(CharacterIds.ZhaoYun);
        AddEquipment(EquipmentIds.BlueSteelSword);
        Assert(!EventManager.GetVisibleOptions(GetEvent("ancient_forge")).Contains(option),
            "缺少神秘芯片时赵云隐藏选项仍显示");

        ResetRun(CharacterIds.ZhaoYun);
        AddEquipment(EquipmentIds.BlueSteelSword);
        AddEquipment(EquipmentIds.MysteriousChip);
        Assert(EventManager.GetVisibleOptions(GetEvent("ancient_forge")).Contains(option),
            "背包中未装备的青钢剑未触发赵云隐藏选项");
    }

    private void TestXuShengHiddenUpgrade()
    {
        ResetRun(CharacterIds.XuSheng);
        var blade = AddEquipment(EquipmentIds.GuDingDao);
        Assert(InventoryManager.EquipToSlot(blade.InstanceId, EquipmentSlot.Weapon),
            "徐盛测试无法装备古锭刀");
        AddEquipment(EquipmentIds.MysteriousChip);

        var eventData = GetEvent("ancient_forge");
        var option = eventData.Options[5];
        Assert(EventManager.GetVisibleOptions(eventData).Contains(option), "徐盛隐藏选项未显示");
        Assert(EventManager.TryResolveOption(eventData, option, out _), "徐盛隐藏选项无法结算");
        Assert(!GameManager.OwnsEquipment(EquipmentIds.GuDingDao), "古锭刀没有被消耗");
        Assert(!GameManager.OwnsEquipment(EquipmentIds.MysteriousChip), "徐盛升级没有消耗神秘芯片");
        Assert(GameManager.OwnsEquipment(EquipmentIds.TrueGuDingDao), "徐盛没有获得真·古锭刀");

        var upgraded = InventoryManager.GetAllOwned()
            .Single(item => item.Definition.Id == EquipmentIds.TrueGuDingDao);
        Assert(upgraded.EquippedSlot == EquipmentSlot.Weapon, "真·古锭刀没有保留武器槽位");

        ResetRun(CharacterIds.XuSheng);
        AddEquipment(EquipmentIds.GuDingDao);
        Assert(!EventManager.GetVisibleOptions(GetEvent("ancient_forge")).Contains(option),
            "缺少神秘芯片时徐盛隐藏选项仍显示");

        ResetRun(CharacterIds.XuSheng);
        AddEquipment(EquipmentIds.GuDingDao);
        AddEquipment(EquipmentIds.MysteriousChip);
        Assert(EventManager.GetVisibleOptions(GetEvent("ancient_forge")).Contains(option),
            "背包中未装备的古锭刀未触发徐盛隐藏选项");
    }

    private void TestHigherRarityCandidateFilter()
    {
        ResetRun(CharacterIds.XuSheng);
        var original = EquipmentDatabase.GetEquipment(EquipmentIds.RustGuDingDao)
            ?? throw new InvalidOperationException("缺少锈古锭刀定义");
        var candidates = RewardManager.GetEquipmentUpgradeCandidates(original);

        Assert(candidates.Count > 0, "稀有装备没有史诗升级候选");
        Assert(candidates.All(candidate => candidate.Rarity == EquipmentRarity.Epic),
            "升级候选没有严格提升一级品质");
        Assert(candidates.All(InventoryManager.IsEligibleForReforge),
            "升级候选包含剧情或角色专属装备");
        Assert(candidates.All(candidate =>
                RewardManager.CanAppearInRandomEquipmentUpgradeReward(candidate)),
            "升级候选绕过统一合法性过滤");
    }

    private static EventData GetEvent(string id)
    {
        return EventDatabase.GetEvent(id)
            ?? throw new InvalidOperationException($"找不到事件定义：{id}");
    }

    private static OwnedEquipment AddEquipment(string id)
    {
        return InventoryManager.AddToInventory(id, EquipmentGainSource.SaveRestore)
            ?? throw new InvalidOperationException($"无法加入测试装备：{id}");
    }

    private static void ResetRun(string characterId)
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(characterId);
        InventoryManager.Reset();
        GameManager.DebugGoToChapter(3);
        GameManager.SetChapterRoute(ChapterRoute.Imperial);
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
