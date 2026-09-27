using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 自由探索（ContinueExplore）系统：精英战清完后开放Boss入口、动态刷新一批可选节点。
/// 从 GameManager 抽出并泛化为对所有章节通用（不再只有第一章），通过 GameManager
/// 现有的地图/随机数状态交互，不重复保存权威数据。
/// </summary>
public static class ContinueExploreManager
{
    private static int _nodeCounter;
    private static bool _hasShownTip;

    /// <summary>当前章节是否已进入自由探索阶段（精英战已清完）。</summary>
    public static bool IsActive { get; private set; }

    /// <summary>首次进入自由探索时置真一次，由地图 UI 层消费后清空。</summary>
    public static bool ShouldShowTip { get; private set; }

    /// <summary>
    /// 精英战清完后：开放Boss入口（不消耗电量、随时可进），并刷新首批自由探索节点。
    /// </summary>
    public static void Enter(int chapter)
    {
        IsActive = true;
        MainFlow.TryShowFirstTimeHint(FirstTimeHintManager.HintIds.FirstFreeExplore);

        // 魏·双线征伐 会让 MapNodes 里出现两个 Type==Boss 的节点（主Boss + 额外Boss，
        // Id 形如 "boss_extra_ch1"，且额外Boss节点排在主Boss之前——见
        // FactionFateManager.TryAddExtraBossNodeForChapter 的 Insert 位置说明）。这里必须
        // 排除额外Boss，否则会把"精英战清完解锁Boss"这一步错误地解锁到额外Boss头上，
        // 主Boss反而永远不会被解锁。额外Boss本身在建图时就已经单独解锁过，不需要这里再处理。
        var bossNode = GameManager.MapNodes.Find(n => n.Type == MapNodeType.Boss && !n.Id.StartsWith("boss_extra_ch", System.StringComparison.Ordinal));
        if (bossNode != null)
        {
            GameManager.UnlockedNodeIds.Add(bossNode.Id);
        }

        if (!_hasShownTip)
        {
            _hasShownTip = true;
            ShouldShowTip = true;
        }

        RegenerateChoices(chapter);
    }

    /// <summary>
    /// 清空当前这一批自由探索节点（含未被选中的），刷新出新的一批；Boss节点始终保留、
    /// 始终排在自由探索节点之后（不影响 AllStagesCleared 依赖"最后一个节点"的既有判断）。
    /// </summary>
    public static void RegenerateChoices(int chapter)
    {
        var stale = GameManager.MapNodes.FindAll(n => n.IsContinueExploring);
        foreach (var staleNode in stale)
        {
            GameManager.MapNodes.Remove(staleNode);
            GameManager.UnlockedNodeIds.Remove(staleNode.Id);
            GameManager.ClearedNodeIds.Remove(staleNode.Id);
        }

        // 同上：必须排除魏·双线征伐 的额外Boss节点，否则新一批自由探索节点会被插到
        // 额外Boss前面（额外Boss在列表里排在主Boss之前），导致主Boss和新节点之间
        // 夹着一个额外Boss，还可能把 insertAt 算错位置。
        var bossIndex = GameManager.MapNodes.FindIndex(n => n.Type == MapNodeType.Boss && !n.Id.StartsWith("boss_extra_ch", System.StringComparison.Ordinal));
        var insertAt = bossIndex >= 0 ? bossIndex : GameManager.MapNodes.Count;

        var batch = PickBatchCategories(chapter);
        foreach (var category in batch)
        {
            var node = CreateNode(category, chapter);
            GameManager.MapNodes.Insert(insertAt, node);
            insertAt++;
            GameManager.UnlockedNodeIds.Add(node.Id);
        }
    }

    /// <summary>
    /// 一批的品类组成：至少1个普通战斗（守底），剩余名额按权重从可用池随机抽取。
    /// 商店是同一批次中的唯一型节点，不能同时生成两个商店选项。
    /// </summary>
    private static List<ContinueExploreCategory> PickBatchCategories(int chapter)
    {
        var available = GetAvailableCategories(chapter);
        var result = new List<ContinueExploreCategory> { ContinueExploreCategory.NormalBattle };

        var weightedPool = available.Where(c => c != ContinueExploreCategory.NormalBattle).ToList();
        if (weightedPool.Count == 0)
        {
            return result;
        }

        for (var i = 0; i < ContinueExploreConfig.WindowSize - 1; i++)
        {
            var totalWeight = weightedPool.Sum(c => ContinueExploreConfig.CategoryWeights[c]);
            var selected = PickWeighted(weightedPool, totalWeight);
            result.Add(selected);

            // 商店无需在同一轮的两个可选位中重复出现；事件仍保留原来的加权重复逻辑。
            if (selected == ContinueExploreCategory.Shop)
            {
                weightedPool.Remove(selected);
                if (weightedPool.Count == 0)
                {
                    break;
                }
            }
        }

        return result;
    }

    private static ContinueExploreCategory PickWeighted(List<ContinueExploreCategory> pool, float totalWeight)
    {
        var roll = (float)GameManager.EventRewardRandom.NextDouble() * totalWeight;
        var cumulative = 0f;
        foreach (var category in pool)
        {
            cumulative += ContinueExploreConfig.CategoryWeights[category];
            if (roll < cumulative)
            {
                return category;
            }
        }

        return pool[^1];
    }

    /// <summary>
    /// 当前章节实际可以刷新的自由探索节点品类：稀有/史诗事件必须先确认本章确实存在
    /// 对应品质的可用事件，否则不加入候选（避免刷出打开就是空的节点）。
    /// </summary>
    private static List<ContinueExploreCategory> GetAvailableCategories(int chapter)
    {
        var probeNode = new MapNode { Id = "__continue_explore_probe__", Type = MapNodeType.Event, StageIndex = ContinueExploreConfig.StageIndex };
        var categories = new List<ContinueExploreCategory>
        {
            ContinueExploreCategory.NormalBattle,
            ContinueExploreCategory.NormalEvent,
            ContinueExploreCategory.Shop
        };

        if (EventManager.HasAnyEventForRarity(EventRarity.Rare, probeNode))
        {
            categories.Add(ContinueExploreCategory.RareEvent);
        }

        if (EventManager.HasAnyEventForRarity(EventRarity.Epic, probeNode))
        {
            categories.Add(ContinueExploreCategory.EpicEvent);
        }

        return categories;
    }

    private static MapNode CreateNode(ContinueExploreCategory category, int chapter)
    {
        _nodeCounter++;
        var id = $"continue_explore_{_nodeCounter}";
        var powerCost = ContinueExploreConfig.GetPowerCost(category);

        switch (category)
        {
            case ContinueExploreCategory.NormalBattle:
                return new MapNode
                {
                    Id = id,
                    Name = "自由探索·遭遇战",
                    Type = MapNodeType.Battle,
                    StageIndex = ContinueExploreConfig.StageIndex,
                    PowerCost = powerCost,
                    IsContinueExploring = true,
                    StageIdOverride = ContinueExploreConfig.GetBattleStageId(chapter)
                };
            case ContinueExploreCategory.Shop:
                return new MapNode
                {
                    Id = id,
                    Name = "自由探索·商店",
                    Type = MapNodeType.Shop,
                    StageIndex = ContinueExploreConfig.StageIndex,
                    PowerCost = powerCost,
                    IsContinueExploring = true
                };
            case ContinueExploreCategory.NormalEvent:
                return CreateRarityEventNode(id, EventRarity.Common, "自由探索·事件", powerCost);
            case ContinueExploreCategory.RareEvent:
                return CreateRarityEventNode(id, EventRarity.Rare, "自由探索·稀有事件", powerCost);
            case ContinueExploreCategory.EpicEvent:
                return CreateRarityEventNode(id, EventRarity.Epic, "自由探索·史诗事件", powerCost);
            default:
                return CreateRarityEventNode(id, EventRarity.Common, "自由探索·事件", powerCost);
        }
    }

    private static MapNode CreateRarityEventNode(string id, EventRarity rarity, string name, int powerCost)
    {
        var node = new MapNode
        {
            Id = id,
            Name = name,
            Type = MapNodeType.Event,
            StageIndex = ContinueExploreConfig.StageIndex,
            PowerCost = powerCost,
            IsContinueExploring = true
        };

        // 生成时就把具体事件定下来（FixedEventId），进入节点时复用既有的
        // EventSystem.GetEventForNode“FixedEventId优先”分支，不新增额外选取路径。
        var chosen = EventManager.GetEventForNodeWithRarity(node, rarity);
        node.FixedEventId = chosen?.Id;
        return node;
    }

    /// <summary>地图 UI 层展示完提示后调用，清空一次性标记。</summary>
    public static void ConsumeTip()
    {
        ShouldShowTip = false;
    }

    /// <summary>整局重置：仅在开始新的一局时调用。</summary>
    public static void ResetForNewRun()
    {
        _nodeCounter = 0;
        _hasShownTip = false;
        ShouldShowTip = false;
        IsActive = false;
    }

    /// <summary>章节切换重置：每次进入新章节都要重新触发自由探索，不是整局只触发一次。</summary>
    public static void ResetForNewChapter()
    {
        IsActive = false;
    }
}
