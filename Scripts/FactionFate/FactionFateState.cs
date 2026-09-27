//////////////////////////////////////////////////////////
// 文件：Scripts/FactionFate/FactionFateState.cs
//
// 模块：Faction Fate System
//
// 职责：
// 1. 承载阵营命运在当前 Run 内的可变进度数据。
// 2. 为其它模块提供清晰、稳定的调用边界。
//
// 不负责：
// × 任何业务规则判定——判定逻辑放在 FactionFateManager，这里只是一份纯数据。
//////////////////////////////////////////////////////////

using System.Collections.Generic;

/// <summary>
/// 天命骰完成业务结算后提交给表现层的一次性请求。
/// </summary>
public sealed record FactionFateDicePresentationRequest(
    int Chapter,
    IReadOnlyList<int> Rolls,
    int Total,
    string ResultMessage,
    string LegendaryMessage);

/// <summary>
/// Faction Fate System 的公开类：FactionFateState。
///
/// 纯数据类（非 static）——整个 FactionFateManager 只持有它的一个实例，
/// 新开 Run 时直接 `_state = new FactionFateState()` 整体替换，不需要逐字段清零，
/// 也不容易漏清某个字段（这是相对于把所有字段直接摊平到 Manager 静态字段的好处）。
/// </summary>
public sealed class FactionFateState
{
    public string? SelectedFateId;
    public int RerollRemaining = 3;
    public int ReforgeSaleCount;
    public List<int> ChapterDiceHistory = new();
    public int LastDiceRolledChapter;
    public List<int>? DebugForcedNextDiceRolls;
    public FactionFateDicePresentationRequest? PendingDicePresentation;
    // 群·命运抉择只增加一次额外选择；第一次选择完成后消费，第二次完成后正常返回地图。
    public int ExtraInitialEventChoicesRemaining;

    // 吴·先行采购：以"商店访问"为单位消费。进入商店时递增 CurrentShopVisitSerial；
    // 购买后记录 UsedShopVisitSerial。刷新同一家商店不会递增，因此不会恢复免费机会。
    public int CurrentShopVisitSerial;
    public int FirstPurchaseFreeUsedShopVisitSerial;

    // 群·芯片重构：本局固定把基础芯片奖励转换为三种目标中的一种。
    public ChipChoiceType? QunForcedChipType;
    // 群·淬炼开局：已经成功提升品质的装备次数（最多两次）。
    public int QunEquipmentUpgradeCount;

    // 蜀·木牛流马：仅第一章主Boss首次奖励后发放一次。
    public bool ShuMuNiuLiuMaBossRewardGranted;
}
