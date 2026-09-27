//////////////////////////////////////////////////////////
// 文件：Scripts/MapRunState.cs
//
// 模块：Map System
//
// 为什么存在：
// 地图当前所在节点、走过/完成的节点，属于"这一局地图"的状态，退出地图去打
// 战斗/事件/商店/Boss 后，回到地图应该恢复这些状态，而不是重新开始。这份状态
// 不应该放在场景节点或 UI 控件的临时字段里（场景每次重新加载都会丢失），
// 也不应该另起一套地图数据——节点本身（MapNode/MapNodeType/解锁判断）仍然
// 完全由 GameManager 负责，MapRunState 只负责记录"进展到哪了"。
//
// 职责：
// 1. 记录玩家当前所在的节点 Id（CurrentNodeId）。
// 2. 记录玩家曾经进入过的节点集合（VisitedNodeIds）——这是全新的、
//    只属于地图表现层的状态，GameManager 里没有对应概念。
// 3. 提供"节点是否已完成"（CompletedNodeIds/IsCompleted）的只读查询——
//    这不是独立维护的第二份数据，而是直接读取 GameManager.IsNodeCleared
//    的一个只读视图，避免出现两份可能互相不一致的"节点是否通关"状态。
// 4. 在新章节、新 Run 时重置自己（章节/节点解锁状态本身仍由 GameManager 重置，
//    这里只重置地图相关的这几项）。
//
// 不负责：
// × 地图节点生成、解锁判断（完全交给 GameManager/EventSystem，只读取结果）。
// × 进入/退出关卡的流程（仍由 MainFlow 负责，本类不触发任何场景切换）。
// × 战斗、事件、商店、Boss、Tutorial、Trigger、Reward 的任何逻辑。
// × 地图角色的世界坐标——地图已经改回点击节点即可进入，不存在可移动的
//   地图角色，因此这里不再记录任何坐标。
//
// 主要依赖：
// GameManager（只读查询 IsNodeCleared/Nodes，不反向修改 GameManager 的字段）
//////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 地图运行状态：当前节点 + 节点访问/完成状态。
///
/// 以后这里就是地图存档的落点——所有字段都是简单值类型/字符串集合，
/// 方便直接序列化。当前不涉及真正的存档读档，只是把这些状态从"进地图就丢失"
/// 的场景临时变量，提升成一份跨场景存在的静态运行状态。
/// </summary>
public static class MapRunState
{
    /// <summary>当前地图所属章节（和 GameManager.CurrentChapter 保持一致）。</summary>
    public static int ChapterId { get; private set; } = 1;

    /// <summary>
    /// 玩家当前所在（最近一次进入）的节点 Id。
    ///
    /// 这里不独立维护一份数据——MainFlow 早就通过 GameManager.SetCurrentNode
    /// 维护了 GameManager.CurrentNodeId 这个权威状态，重新记一份会变成
    /// "维护多个地图状态"。这里只是把它按"节点状态"的分类再暴露一次，
    /// 方便调用方统一从 MapRunState 读取位置+节点信息。
    /// </summary>
    public static string CurrentNodeId => GameManager.CurrentNodeId;

    private static readonly HashSet<string> VisitedNodeIdSet = new();

    /// <summary>玩家曾经进入过的节点 Id 集合，只增不减（同一章节内）。</summary>
    public static IReadOnlyCollection<string> VisitedNodeIds => VisitedNodeIdSet;

    /// <summary>
    /// 已完成（通关）的节点 Id 集合。
    ///
    /// 这不是 MapRunState 自己维护的第二份数据，而是直接从
    /// <see cref="GameManager.Nodes"/> + <see cref="GameManager.IsNodeCleared"/>
    /// 实时计算出来的只读视图——"节点是否完成"永远只有 GameManager 这一个真相来源。
    /// </summary>
    public static IReadOnlyCollection<string> CompletedNodeIds =>
        GameManager.Nodes.Where(node => GameManager.IsNodeCleared(node.Id)).Select(node => node.Id).ToList();

    /// <summary>
    /// 记录玩家进入了某个节点：把该节点加入 VisitedNodeIds（CurrentNodeId 本身
    /// 由 GameManager.SetCurrentNode 负责，见 <see cref="CurrentNodeId"/> 说明）。
    ///
    /// 调用方应该在点击节点真正触发进入关卡时调用，不涉及任何场景切换——
    /// 切换场景仍然由 MainFlow 负责。
    /// </summary>
    public static void EnterNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            return;
        }

        VisitedNodeIdSet.Add(nodeId);
    }

    /// <summary>节点是否曾经被玩家进入过。</summary>
    public static bool IsVisited(string nodeId) => VisitedNodeIdSet.Contains(nodeId);

    /// <summary>节点是否已完成（直接读取 GameManager.IsNodeCleared，见 <see cref="CompletedNodeIds"/> 说明）。</summary>
    public static bool IsCompleted(string nodeId) => GameManager.IsNodeCleared(nodeId);

    /// <summary>
    /// 切换到新章节时重置地图状态：VisitedNodeIds 清空，不继承上一章节的地图进度。
    /// </summary>
    public static void ResetForNewChapter(int chapterId)
    {
        ChapterId = chapterId;
        VisitedNodeIdSet.Clear();
        // CurrentNodeId 不需要在这里清——GameManager.AdvanceToNextChapter/
        // DebugGoToChapter 自己已经把 GameManager.CurrentNodeId 清空了。
    }

    /// <summary>开始新 Run 时重置整个地图状态。</summary>
    public static void ResetForNewRun()
    {
        ResetForNewChapter(1);
    }
}
