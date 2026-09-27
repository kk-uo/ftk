//////////////////////////////////////////////////////////
// 文件：Scripts/DamagePreview/DamagePreviewResult.cs
//
// 模块：Damage Preview System
//
// 职责：
// 1. 承载"手牌预估伤害显示"功能的只读结果数据结构。
// 2. 不参与任何计算，只用于把 DamagePreviewService 的计算结果
//    传递给 UI 层（CardUI/ActionSlot）。
//
// 不负责：
// × 计算伤害（见 DamagePreviewService）。
// × 渲染 UI。
//////////////////////////////////////////////////////////

using System.Collections.Generic;

/// <summary>
/// 单个目标的预估伤害明细。多目标牌（南蛮入侵/万箭齐发/张辽突袭）每个存活敌人各一条。
/// </summary>
public sealed class DamagePreviewTargetEntry
{
    public DamagePreviewTargetEntry(string targetName, int amount, string? note = null)
    {
        TargetName = targetName;
        Amount = amount;
        Note = note;
    }

    public string TargetName { get; }
    public int Amount { get; }
    public string? Note { get; }
}

/// <summary>
/// 链式追加伤害（目前仅孙策【激昂】）：命中本体后额外触发的一次独立伤害。
/// 它会计入卡面最终伤害总数，同时在悬停明细中单独列出来源。
/// </summary>
public sealed class DamagePreviewFollowUp
{
    public DamagePreviewFollowUp(string sourceLabel, int amount)
    {
        SourceLabel = sourceLabel;
        Amount = amount;
    }

    public string SourceLabel { get; }
    public int Amount { get; }
}

/// <summary>
/// Damage Preview System 的公开类：DamagePreviewResult。
///
/// 手牌预估伤害显示功能的最终结果：卡面角标数字 + 悬停明细文案 +
/// 多目标拆分 + 链式追加，全部只读，UI 层直接消费。
/// </summary>
public sealed class DamagePreviewResult
{
    private DamagePreviewResult()
    {
    }

    /// <summary>是否是一张会造成伤害的牌；非伤害牌一律不显示角标。</summary>
    public bool IsDamageCard { get; private init; }

    /// <summary>该牌的基础伤害（未经任何修正），来自 BattleRules.GetCardBaseDamage。</summary>
    public int BaseDamage { get; private init; }

    /// <summary>
    /// 卡面主角标显示的最终伤害数字，包含命中后必然触发的追加伤害
    /// （例如孙策【激昂】）；单目标=该目标总伤害，多目标=所有目标总和。
    /// </summary>
    public int PredictedFinalDamage { get; private init; }

    /// <summary>是否为多目标牌（南蛮入侵/万箭齐发/张辽突袭）。</summary>
    public bool IsMultiTarget { get; private init; }

    /// <summary>多目标牌下，各目标是否预估伤害不同（不同则角标显示总计而非"数字×N"）。</summary>
    public bool TargetsHaveDifferentAmounts { get; private init; }

    /// <summary>每个目标的预估明细，单目标牌也会填一条。</summary>
    public IReadOnlyList<DamagePreviewTargetEntry> TargetBreakdown { get; private init; } = System.Array.Empty<DamagePreviewTargetEntry>();

    /// <summary>
    /// 不确定性标记：本预估假设"必定命中"，不建模闪/无懈可击/杀系克制等可能的抵消，
    /// 因此该标志仅供详情说明使用；卡面仍显示已计算的最终伤害数值，
    /// 不再用问号遮蔽玩家需要的数字。
    /// </summary>
    public bool IsUncertain { get; private init; }

    /// <summary>构成明细，按伤害管线阶段顺序排列的文本行，供悬停 Tooltip 展示。</summary>
    public IReadOnlyList<string> Breakdown { get; private init; } = System.Array.Empty<string>();

    /// <summary>非伤害数字类提示，例如"命中后额外触发顺手牵羊，偷取1点费用"。</summary>
    public IReadOnlyList<string> Warnings { get; private init; } = System.Array.Empty<string>();

    /// <summary>链式追加伤害（孙策激昂），已计入 <see cref="PredictedFinalDamage"/>。</summary>
    public DamagePreviewFollowUp? FollowUpDamage { get; private init; }

    public static DamagePreviewResult NotDamageCard() => new() { IsDamageCard = false };

    public static DamagePreviewResult Create(
        int baseDamage,
        int predictedFinalDamage,
        bool isMultiTarget,
        bool targetsHaveDifferentAmounts,
        IReadOnlyList<DamagePreviewTargetEntry> targetBreakdown,
        IReadOnlyList<string> breakdown,
        IReadOnlyList<string> warnings,
        DamagePreviewFollowUp? followUpDamage)
    {
        return new DamagePreviewResult
        {
            IsDamageCard = true,
            BaseDamage = baseDamage,
            PredictedFinalDamage = predictedFinalDamage,
            IsMultiTarget = isMultiTarget,
            TargetsHaveDifferentAmounts = targetsHaveDifferentAmounts,
            TargetBreakdown = targetBreakdown,
            IsUncertain = true,
            Breakdown = breakdown,
            Warnings = warnings,
            FollowUpDamage = followUpDamage,
        };
    }
}
