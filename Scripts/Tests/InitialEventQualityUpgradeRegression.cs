//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/InitialEventQualityUpgradeRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 在独立Godot进程中验证品质跃迁的Run状态生命周期。
// 2. 验证正式装备来源统一经过一次性升级入口。
// 3. 验证品质、统一重铸池、无候选和递归保护规则。
//
// 不负责：
// × 修改正式游戏数据定义。
// × 模拟玩家点击或替代完整UI验收。
// × 在正常游戏启动流程中自动运行。
//
// 主要依赖：
// GameManager
// InventoryManager
// RewardManager
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 品质跃迁的独立 Headless 回归入口，仅由测试场景显式启动。
/// </summary>
public partial class InitialEventQualityUpgradeRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行全部回归用例，并通过进程退出码返回结果。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            RunAll();
            GD.Print($"QUALITY_UPGRADE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"QUALITY_UPGRADE_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void RunAll()
    {
        TestSelectionAndArmingLifecycle();
        TestExcludedGainSources();
        TestAllPrimarySlotCategories();
        TestAllFormalGainSources();
        TestLegendaryDoesNotConsume();
        TestNoCandidateDoesNotConsume();
        TestReplacementDoesNotRecurseAndOnlyTriggersOnce();
        TestQunFateUpgradesFirstTwoEquipment();
        TestQunFateUpgradesEventExclusiveSecondEquipment();
        TestNewRunResetsState();
    }

    private void TestSelectionAndArmingLifecycle()
    {
        ResetRun();
        var pair = FindUpgradeablePair(EquipmentSlotCategory.Weapon);
        GameManager.AddEquipment(pair.Original.Id, EquipmentGainSource.RunInitialization);
        var existingCount = InventoryManager.GetAllOwned().Count;

        GameManager.SetInitialEventUpgradeFirstEquip();
        Assert(GameManager.InitialEventUpgradeFirstEquipIsSelected, "选择后未写入Run状态");
        Assert(!GameManager.InitialEventUpgradeFirstEquipIsArmed, "选择后不应立即激活");
        Assert(!GameManager.InitialEventUpgradeFirstEquipHasTriggered, "选择时不应标记已触发");
        Assert(InventoryManager.GetAllOwned().Count == existingCount, "选择效果不应改写已有装备");

        var beforeStage = GameManager.AddEquipment(pair.Original.Id, EquipmentGainSource.BattleReward);
        Assert(beforeStage?.Definition.Id == pair.Original.Id, "进入1-1前正式奖励不应触发");
        Assert(!GameManager.InitialEventUpgradeFirstEquipHasTriggered, "进入1-1前效果被消费");

        GameManager.ArmInitialEventUpgradeFirstEquip("2-1");
        Assert(!GameManager.InitialEventUpgradeFirstEquipIsArmed, "非1-1错误激活");
        GameManager.ArmInitialEventUpgradeFirstEquip("1-1");
        Assert(GameManager.InitialEventUpgradeFirstEquipIsArmed, "进入1-1后未激活");

        GameManager.SetCurrentNode("battle_1");
        Assert(GameManager.InitialEventUpgradeFirstEquipIsArmed, "场景/节点状态切换导致Run状态丢失");
    }

    private void TestExcludedGainSources()
    {
        var excludedSources = new[]
        {
            EquipmentGainSource.CharacterStartingEquipment,
            EquipmentGainSource.RunInitialization,
            EquipmentGainSource.InitialEvent,
            EquipmentGainSource.Developer,
            EquipmentGainSource.SaveRestore,
            EquipmentGainSource.Transformation,
            EquipmentGainSource.ReforgeReplacement,
            EquipmentGainSource.FactionFateReplacement,
            EquipmentGainSource.QualityUpgradeReplacement
        };

        foreach (var source in excludedSources)
        {
            ResetRun();
            var pair = FindUpgradeablePair(EquipmentSlotCategory.Weapon);
            SelectAndArm();
            var added = GameManager.AddEquipment(pair.Original.Id, source);
            Assert(added?.Definition.Id == pair.Original.Id, $"来源{source}不应触发品质跃迁");
            Assert(GameManager.InitialEventUpgradeFirstEquipIsArmed, $"来源{source}错误消费效果");
            Assert(!GameManager.InitialEventUpgradeFirstEquipHasTriggered, $"来源{source}错误标记已触发");
        }
    }

    private void TestAllPrimarySlotCategories()
    {
        var categories = new[]
        {
            EquipmentSlotCategory.Weapon,
            EquipmentSlotCategory.Armor,
            EquipmentSlotCategory.Accessory,
            EquipmentSlotCategory.Vehicle
        };

        foreach (var category in categories)
        {
            ResetRun();
            var pair = FindUpgradeablePair(category);
            SelectAndArm();
            var added = GameManager.AddEquipment(pair.Original.Id, EquipmentGainSource.BattleReward);
            AssertUpgradeResult(pair.Original, added);
        }
    }

    private void TestAllFormalGainSources()
    {
        var formalSources = new[]
        {
            EquipmentGainSource.GameplayReward,
            EquipmentGainSource.ShopPurchase,
            EquipmentGainSource.EventReward,
            EquipmentGainSource.BattleReward,
            EquipmentGainSource.BossReward,
            EquipmentGainSource.ChoiceReward,
            EquipmentGainSource.EnemyDrop
        };

        foreach (var source in formalSources)
        {
            ResetRun();
            var pair = FindUpgradeablePair(EquipmentSlotCategory.Weapon);
            SelectAndArm();
            var added = GameManager.AddEquipment(pair.Original.Id, source);
            AssertUpgradeResult(pair.Original, added);
        }
    }

    private void TestLegendaryDoesNotConsume()
    {
        ResetRun();
        var legendary = FindPoolEquipment(EquipmentRarity.Legendary);
        var pair = FindUpgradeablePair(EquipmentSlotCategory.Weapon);
        SelectAndArm();

        var legendaryAdded = GameManager.AddEquipment(legendary.Id, EquipmentGainSource.BossReward);
        Assert(legendaryAdded?.Definition.Id == legendary.Id, "传奇装备不应被替换");
        Assert(GameManager.InitialEventUpgradeFirstEquipIsArmed, "传奇装备错误消费效果");
        Assert(!GameManager.InitialEventUpgradeFirstEquipHasTriggered, "传奇装备错误标记已触发");

        var upgraded = GameManager.AddEquipment(pair.Original.Id, EquipmentGainSource.EventReward);
        AssertUpgradeResult(pair.Original, upgraded);
    }

    private void TestNoCandidateDoesNotConsume()
    {
        ResetRun();
        // 重铸池允许重复装备后，“先拥有所有候选”不再等价于无候选。
        // 传奇不存在更高品质，才是品质跃迁的真实无候选状态。
        var legendary = FindPoolEquipment(EquipmentRarity.Legendary);

        SelectAndArm();
        var added = GameManager.AddEquipment(legendary.Id, EquipmentGainSource.BattleReward);
        Assert(added?.Definition.Id == legendary.Id, "无候选时原装备未被保留");
        Assert(GameManager.InitialEventUpgradeFirstEquipIsArmed, "无候选时效果被消费");
        Assert(!GameManager.InitialEventUpgradeFirstEquipHasTriggered, "无候选时错误标记已触发");
    }

    private void TestReplacementDoesNotRecurseAndOnlyTriggersOnce()
    {
        ResetRun();
        var pair = FindUpgradeablePair(EquipmentSlotCategory.Weapon);
        var triggerCount = 0;
        Action<EquipmentDefinition, EquipmentDefinition> handler = (_, _) => triggerCount++;
        GameManager.InitialEventQualityUpgradeTriggered += handler;
        try
        {
            SelectAndArm();
            var first = GameManager.AddEquipment(pair.Original.Id, EquipmentGainSource.BattleReward);
            AssertUpgradeResult(pair.Original, first);
            Assert(first?.Definition.Rarity == EquipmentRarity.Rare, "替换结果发生递归升级");
            Assert(triggerCount == 1, "一次装备获得触发了多次品质跃迁");

            var second = GameManager.AddEquipment(pair.Original.Id, EquipmentGainSource.EventReward);
            Assert(second?.Definition.Id == pair.Original.Id, "成功触发后仍替换后续装备");
            Assert(triggerCount == 1, "成功触发后事件再次发布");
        }
        finally
        {
            GameManager.InitialEventQualityUpgradeTriggered -= handler;
        }
    }

    private void TestQunFateUpgradesFirstTwoEquipment()
    {
        ResetRun();
        FactionFateManager.DebugForceFate(FactionFateIds.QunFirstTwoEquipmentUpgrade);
        var pair = FindUpgradeablePair(EquipmentSlotCategory.Weapon);

        var first = GameManager.AddEquipment(pair.Original.Id, EquipmentGainSource.BattleReward);
        Assert(first?.Definition.Rarity == GetNextRarity(pair.Original.Rarity),
            "群·淬炼开局没有提升第一件装备品质");
        Assert(FactionFateManager.QunEquipmentUpgradesRemaining == 1,
            "群·淬炼开局第一件成功替换后剩余次数错误");

        var second = GameManager.AddEquipment(pair.Original.Id, EquipmentGainSource.EventReward);
        Assert(second?.Definition.Rarity == GetNextRarity(pair.Original.Rarity),
            "群·淬炼开局没有提升第二件装备品质");
        Assert(FactionFateManager.QunEquipmentUpgradesRemaining == 0,
            "群·淬炼开局第二件成功替换后没有耗尽次数");

        var third = GameManager.AddEquipment(pair.Original.Id, EquipmentGainSource.ShopPurchase);
        Assert(third?.Definition.Id == pair.Original.Id,
            "群·淬炼开局在两次成功替换后仍升级后续装备");
    }

    private void TestQunFateUpgradesEventExclusiveSecondEquipment()
    {
        ResetRun();
        FactionFateManager.DebugForceFate(FactionFateIds.QunFirstTwoEquipmentUpgrade);
        var pair = FindUpgradeablePair(EquipmentSlotCategory.Weapon);
        var yellowMushroom = EquipmentDatabase.GetEquipment(EquipmentIds.StinkyMushroomYellow)
            ?? throw new InvalidOperationException("缺少恶臭蘑菇·黄定义");

        Assert(!InventoryManager.IsEligibleForReforge(yellowMushroom),
            "恶臭蘑菇·黄不应进入普通随机重铸池");

        var first = GameManager.AddEquipment(pair.Original.Id, EquipmentGainSource.BattleReward);
        Assert(first?.Definition.Rarity == GetNextRarity(pair.Original.Rarity),
            "群·淬炼开局没有升级第一件普通装备");

        var second = GameManager.AddEquipment(yellowMushroom.Id, EquipmentGainSource.EventReward);
        Assert(second != null, "恶臭蘑菇·黄替换结果未进入背包");
        Assert(second!.Definition.Id != yellowMushroom.Id,
            "群·淬炼开局没有替换第二件恶臭蘑菇·黄");
        Assert(second.Definition.Rarity == EquipmentRarity.Epic,
            "恶臭蘑菇·黄没有提升到史诗品质");
        Assert(InventoryManager.IsEligibleForReforge(second.Definition),
            "恶臭蘑菇·黄的替换结果没有通过随机池准入");
        Assert(FactionFateManager.QunEquipmentUpgradesRemaining == 0,
            "恶臭蘑菇·黄作为第二件装备后未消耗淬炼次数");
    }

    private void TestNewRunResetsState()
    {
        ResetRun();
        SelectAndArm();
        GameManager.BeginNewRun();
        Assert(!GameManager.InitialEventUpgradeFirstEquipIsSelected, "新Run未重置IsSelected");
        Assert(!GameManager.InitialEventUpgradeFirstEquipIsArmed, "新Run未重置IsArmed");
        Assert(!GameManager.InitialEventUpgradeFirstEquipHasTriggered, "新Run未重置HasTriggered");
    }

    private void AssertUpgradeResult(
        EquipmentDefinition original,
        OwnedEquipment? added)
    {
        Assert(added != null, "替换装备未进入背包");
        var finalDefinition = added!.Definition;
        Assert(finalDefinition.Id != original.Id, "原装备未被替换");
        Assert(finalDefinition.Rarity == GetNextRarity(original.Rarity), "替换装备不是高一级品质");
        Assert(InventoryManager.IsEligibleForReforge(finalDefinition), "替换装备未通过统一重铸池门禁");
        Assert(!GameManager.OwnsEquipment(original.Id), "原装备仍残留在背包");
        Assert(GameManager.InitialEventUpgradeFirstEquipHasTriggered, "成功替换后未标记已触发");
        Assert(!GameManager.InitialEventUpgradeFirstEquipIsArmed, "成功替换后仍保持激活");
    }

    private static (EquipmentDefinition Original, EquipmentDefinition Candidate) FindUpgradeablePair(
        EquipmentSlotCategory category)
    {
        var rarities = new[]
        {
            EquipmentRarity.Common,
            EquipmentRarity.Rare,
            EquipmentRarity.Epic
        };
        foreach (var rarity in rarities)
        {
            foreach (var original in EquipmentDatabase.GetAllEquipments())
            {
                if (original.Rarity != rarity) continue;
                if (InventoryManager.GetSlotCategory(original) != category) continue;
                if (!InventoryManager.IsEligibleForReforge(original)) continue;

                var candidates = FindUpgradeCandidates(original);
                if (candidates.Count > 0)
                {
                    return (original, candidates[0]);
                }
            }
        }

        throw new InvalidOperationException($"没有可用于测试的可升级{category}装备对。");
    }

    private static List<EquipmentDefinition> FindUpgradeCandidates(EquipmentDefinition original)
    {
        return new List<EquipmentDefinition>(RewardManager.GetEquipmentUpgradeCandidates(original));
    }

    private static EquipmentDefinition FindPoolEquipment(EquipmentRarity rarity)
    {
        foreach (var definition in EquipmentDatabase.GetAllEquipments())
        {
            if (definition.Rarity == rarity
                && InventoryManager.IsEligibleForReforge(definition))
            {
                return definition;
            }
        }

        throw new InvalidOperationException($"没有可用于测试的{rarity}装备。");
    }

    private static EquipmentRarity GetNextRarity(EquipmentRarity rarity)
    {
        return rarity switch
        {
            EquipmentRarity.Common => EquipmentRarity.Rare,
            EquipmentRarity.Rare => EquipmentRarity.Epic,
            EquipmentRarity.Epic => EquipmentRarity.Legendary,
            _ => throw new InvalidOperationException("传奇装备没有更高品质。")
        };
    }

    private static void SelectAndArm()
    {
        GameManager.SetInitialEventUpgradeFirstEquip();
        GameManager.ArmInitialEventUpgradeFirstEquip("1-1");
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
