using System.Collections.Generic;

/// <summary>
/// 自由探索（ContinueExplore）阶段可选节点品类。
/// </summary>
public enum ContinueExploreCategory
{
    NormalBattle,
    NormalEvent,
    Shop,
    RareEvent,
    EpicEvent
}

/// <summary>
/// 自由探索系统的可调参数集中配置：批次大小、各品类电量消耗、权重、
/// 章节相关的战斗遭遇池映射。新增章节/调整数值只需改这里，不需要碰控制流。
/// </summary>
public static class ContinueExploreConfig
{
    // 每批自由探索刷新的可选节点数量。
    public const int WindowSize = 3;

    // 自由探索节点统一使用的“晚期”阶段索引：既满足大多数事件 MinStage/MaxStage 的
    // 合法范围，又不会和固定流程节点（0-4）的 StageIndex 冲突。
    public const int StageIndex = 6;

    // 固定流程普通战斗及战后自由探索中的普通战斗均为10电量；精英战斗维持20电量。
    // 这些值同时作为自由探索节点的锁定报价，避免地图显示和最终扣费不一致。
    public const int NormalBattlePowerCost = 10;
    public const int ElitePowerCost = 20;
    public const int NormalEventPowerCost = 10;
    public const int RareEventPowerCost = 15;
    public const int EpicEventPowerCost = 20;
    public const int ShopPowerCost = 10;
    public const int BossPowerCost = 0;

    // ── 新电量规则（第一章第6关起 + 第二/三章全程）───────────────────────────
    // 按节点类型/事件品质统一计算，供 GameManager.GetNodeEnergyCost 与自由探索
    // 共用同一套数值，避免散落魔法数字。
    public const int EventCommonEnergyCost = 15;
    public const int EventRareEnergyCost = 20;
    public const int EventEpicEnergyCost = 25;
    public const int CombatEnergyCost = 20;    // 固定流程/精英战斗的通用20电量费用。
    public const int ExtraBossEnergyCost = 15; // 魏·双线征伐 额外Boss专用

    // “至少1个普通战斗”之外，剩余名额按权重从可用品类池中随机抽取；
    // 若某品质本章不存在（如无史诗事件），会被排除在可用池之外，
    // 分母永远是“当前实际可用池”的权重和，天然完成重新归一化。
    public static readonly IReadOnlyDictionary<ContinueExploreCategory, float> CategoryWeights =
        new Dictionary<ContinueExploreCategory, float>
        {
            [ContinueExploreCategory.NormalEvent] = 0.45f,
            [ContinueExploreCategory.RareEvent] = 0.25f,
            [ContinueExploreCategory.Shop] = 0.20f,
            [ContinueExploreCategory.EpicEvent] = 0.10f,
        };

    // 自由探索动态生成的普通战斗节点复用哪个章节的中期遭遇池（沿用
    // StageDatabase 既有的 "<chapter>-3" 命名习惯）。
    public static string GetBattleStageId(int chapter) => $"{chapter}-3";

    /// <summary>
    /// 返回自由探索品类对应的锁定电量费用。战后自由探索的普通战斗为10电量，
    /// 其余事件和商店维持各自品类的既有费用。
    /// </summary>
    public static int GetPowerCost(ContinueExploreCategory category)
    {
        return category switch
        {
            ContinueExploreCategory.NormalBattle => NormalBattlePowerCost,
            ContinueExploreCategory.NormalEvent => EventCommonEnergyCost,
            ContinueExploreCategory.RareEvent => EventRareEnergyCost,
            ContinueExploreCategory.EpicEvent => EventEpicEnergyCost,
            ContinueExploreCategory.Shop => ShopPowerCost,
            _ => EventCommonEnergyCost
        };
    }
}
