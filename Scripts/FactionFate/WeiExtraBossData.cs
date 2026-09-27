//////////////////////////////////////////////////////////
// 文件：Scripts/FactionFate/WeiExtraBossData.cs
//
// 模块：Faction Fate System（魏·双线征伐）
//
// 职责：
// 1. 承载"每章合法额外Boss候选"的静态数据——从各章主Boss加权候选表里摘录
//    只含单一敌人的条目（多敌人组合Boss，例如共享血池的蜀汉共生体，不适合
//    被单独抽出来当"另一个独立Boss"，故排除）。
// 2. 提供每章对应的额外Boss专用单敌人 stage id（在 StageDatabase.cs 里注册）。
//
// 不负责：
// × 建图/发放奖励——这些交给 FactionFateManager.TryAddExtraBossNodeForChapter
//   和既有的 MainFlow 奖励结算流程。
//////////////////////////////////////////////////////////

using System.Collections.Generic;

/// <summary>
/// Faction Fate System 的公开类：WeiExtraBossData。
///
/// 项目里没有现成的"按章节查Boss池"API，主Boss池本身是加权候选表，这里手工摘录一份用于
/// 挑选"另一个不同的Boss"，不影响主Boss池自身的抽取逻辑。
///
/// 数据来源（Scripts/StageDatabase.cs 对应章节Boss关的 EncounterPool）：
/// - 第一章 "1-7"：{feather_guard,boss_healer,feather_guard} / {boss_traitor} / {crazy_performer}
///   —— 只有 crazy_performer 是单敌人条目，候选列表只有它一个。
/// - 第二章 "2-8"：{wulong_collective_intelligence} / {zuoci} / {gang_boss} / {huang_yi_zhi_zhu}
///   —— 全部是单敌人条目，候选列表有四个。
/// - 第三章 "3-8"：{liu_bei,guan_yu,zhang_fei}（蜀汉共生体，共享血池）/ {boss_tyrant,feather_guard,feather_guard}
///   —— 两个条目都是多敌人组合，没有可摘录的单敌人候选，候选列表为空（预期行为，见
///   FactionFateManager.TryAddExtraBossNodeForChapter 里的空列表跳过分支）。
/// </summary>
public static class WeiExtraBossData
{
    public static readonly IReadOnlyDictionary<int, IReadOnlyList<string>> CandidatesByChapter = new Dictionary<int, IReadOnlyList<string>>
    {
        [1] = new List<string> { "crazy_performer" },
        [2] = new List<string> { "wulong_collective_intelligence", "zuoci", "gang_boss", "huang_yi_zhi_zhu" },
        [3] = new List<string>(), // 第三章主Boss池没有单敌人候选，故意留空——不是遗漏。
    };

    /// <summary>
    /// 每章"额外Boss"专用单敌人 stage id，注册在 Scripts/StageDatabase.cs
    /// （CreateStageOneSevenBossExtra/CreateStageTwoEightBossExtra），EncounterPool
    /// 就是上面 CandidatesByChapter 这份数据，按等权重现场抽取——和主Boss节点
    /// 的抽取时机、机制完全一致。第三章没有合法候选，不注册。
    /// </summary>
    public static readonly IReadOnlyDictionary<int, string> ExtraBossStageIdByChapter = new Dictionary<int, string>
    {
        [1] = "1-7-extra",
        [2] = "2-8-extra",
    };
}
