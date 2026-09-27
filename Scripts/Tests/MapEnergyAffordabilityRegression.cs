//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/MapEnergyAffordabilityRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证自由探索节点的生成价格在显示与点击期间保持不变。
// 2. 验证战后自由探索普通战斗的固定费用为10电量。
// 3. 验证电量15时无法进入电量20的节点。
// 3. 验证普通事件生成时锁定普通品质事件，不会在查询费用时重抽。
// 4. 验证同一批自由探索选项不会出现两个商店。
//
// 不负责：
// × 验证地图美术。
// × 验证事件奖励内容。
//
// 主要依赖：
// ContinueExploreManager
// GameManager
// MapExplorationView
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Reflection;

/// <summary>
/// 地图节点电量门槛的无头回归入口。
/// </summary>
public partial class MapEnergyAffordabilityRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行自由探索电量回归并通过进程退出码报告结果。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestContinueExplorePriceIsLocked();
            TestContinueExploreNormalBattleCostsTen();
            TestFifteenPowerCannotEnterTwentyPowerNode();
            TestContinueExploreBatchHasAtMostOneShop();
            GD.Print($"MAP_ENERGY_AFFORDABILITY_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"MAP_ENERGY_AFFORDABILITY_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestContinueExplorePriceIsLocked()
    {
        GameManager.SelectCharacter(CharacterIds.ZhangLiao);

        var createNode = typeof(ContinueExploreManager).GetMethod(
            "CreateNode",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert(createNode != null, "无法访问自由探索节点生成入口");

        var node = createNode!.Invoke(
            null,
            new object[] { ContinueExploreCategory.NormalEvent, 1 }) as MapNode;
        Assert(node != null, "普通自由探索事件节点生成失败");
        Assert(!string.IsNullOrWhiteSpace(node!.FixedEventId), "普通自由探索事件没有锁定具体事件");

        var eventData = EventDatabase.GetEvent(node.FixedEventId!);
        Assert(eventData?.Rarity == EventRarity.Common, "普通自由探索事件错误锁定为其它品质");
        Assert(GameManager.GetNodeEnergyCost(node) == node.PowerCost, "自由探索节点费用没有使用生成时锁定的报价");

        node.FixedEventId = "invalid_event_for_price_regression";
        Assert(GameManager.GetNodeEnergyCost(node) == node.PowerCost, "事件缓存变化后自由探索节点报价发生变化");
    }

    private void TestFifteenPowerCannotEnterTwentyPowerNode()
    {
        GameManager.BeginNewRun();
        Assert(GameManager.TrySpendPower(GameManager.MaxPower - 15), "测试准备阶段无法把电量设置为15");

        var node = new MapNode
        {
            Id = "regression_event_cost_20",
            Name = "电量门槛回归事件",
            Type = MapNodeType.Event,
            StageIndex = ContinueExploreConfig.StageIndex,
            PowerCost = 20,
            IsContinueExploring = true
        };
        GameManager.MapNodes.Add(node);
        GameManager.UnlockedNodeIds.Add(node.Id);

        var view = new MapExplorationView();
        AddChild(view);
        var visual = FindNodeVisual(view, node.Id);

        Assert(GameManager.GetNodeEnergyCost(node) == 20, "节点报价不是20电量");
        Assert(!GameManager.CanAffordPower(GameManager.GetNodeEnergyCost(node)), "15电量被错误判定为可负担20电量");
        Assert(visual != null && !visual.Interactable, "15电量时20电量节点仍可点击");
        Assert(!GameManager.TrySpendPower(GameManager.GetNodeEnergyCost(node)), "最终扣费防线允许15电量支付20电量");
        Assert(GameManager.Power == 15, "扣费失败后电量发生变化");

        view.QueueFree();
    }

    private void TestContinueExploreNormalBattleCostsTen()
    {
        var createNode = typeof(ContinueExploreManager).GetMethod(
            "CreateNode",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert(createNode != null, "无法访问自由探索普通战斗节点生成入口");

        var node = createNode!.Invoke(
            null,
            new object[] { ContinueExploreCategory.NormalBattle, 2 }) as MapNode;

        Assert(node != null, "自由探索普通战斗节点生成失败");
        Assert(node!.IsContinueExploring && node.Type == MapNodeType.Battle, "生成的自由探索普通战斗节点类型错误");
        Assert(node.PowerCost == ContinueExploreConfig.NormalBattlePowerCost, "自由探索普通战斗没有锁定为10电量");
        Assert(GameManager.GetNodeEnergyCost(node) == ContinueExploreConfig.NormalBattlePowerCost, "自由探索普通战斗的显示/实际扣费不是10电量");
    }

    private void TestContinueExploreBatchHasAtMostOneShop()
    {
        var pickBatch = typeof(ContinueExploreManager).GetMethod(
            "PickBatchCategories",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert(pickBatch != null, "无法访问自由探索批次生成入口");

        // 重复抽取以覆盖“第一个额外位抽中商店”这一过去会导致第二个额外位再次抽中商店的路径。
        for (var i = 0; i < 500; i++)
        {
            var batch = pickBatch!.Invoke(null, new object[] { 1 }) as System.Collections.Generic.List<ContinueExploreCategory>;
            Assert(batch != null, "自由探索批次生成失败");

            var shopCount = 0;
            foreach (var category in batch!)
            {
                if (category == ContinueExploreCategory.Shop)
                {
                    shopCount++;
                }
            }

            Assert(shopCount <= 1, "同一批自由探索选项出现了多个商店");
        }
    }

    private static MapNodeVisual? FindNodeVisual(Node root, string nodeId)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is MapNodeVisual visual && visual.NodeData?.Id == nodeId)
            {
                return visual;
            }

            var nested = FindNodeVisual(child, nodeId);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
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
