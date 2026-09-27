//////////////////////////////////////////////////////////
// 文件：Scripts/DamagePreview/DamagePreviewService.cs
//
// 模块：Damage Preview System
//
// 职责：
// 1. 在不产生任何副作用的前提下，复用真实伤害管线（OnDamage 阶段
//    已注册的全部 IBattleEffect）预估一张手牌"如果现在打出去，
//    假设必定命中"会造成多少伤害。
// 2. 支持单目标、多目标（南蛮入侵/万箭齐发/张辽突袭）与孙策【激昂】
//    这类命中后链式追加伤害的场景。
//
// 不负责：
// × 建模是否会被闪/无懈可击/杀系克制抵消——一律假设命中，
//   由 DamagePreviewResult.IsUncertain 统一标注这份不确定性。
// × 消耗任何资源（装备使用次数、护盾层数、连营免费资格等）——
//   这些全部发生在 OnBeforeDamage/OnDamageTaken 阶段，本服务
//   只跑 OnDamage 阶段 + 手动调用一次纯计算的 ResolveModifiers。
// × 写入真实战斗日志/触发技能大字表现——本服务永远只在一个
//   全新的、随用随弃的 BattleContext 上跑，不使用真实 context。
//
// 主要依赖：
// TriggerManager（无状态，可安全共享）、真实 Player 实例（身份判断
// 如 damage.Source == context.Player 需要同一个实例)。
//////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Damage Preview System 的公开类：DamagePreviewService。
/// </summary>
public static class DamagePreviewService
{
    /// <summary>
    /// 预估一张牌打出去的伤害。非伤害牌返回 IsDamageCard=false。
    /// 单目标牌使用 lockedSingleTarget；多目标牌（南蛮入侵/万箭齐发/张辽突袭）
    /// 忽略 lockedSingleTarget，改为对 realContext.Encounter 中全部存活敌人分别预估。
    /// </summary>
    public static DamagePreviewResult PreviewCard(
        BattleContext realContext,
        Player attacker,
        CardType cardType,
        Player? lockedSingleTarget)
    {
        if (!BattleRules.IsAnyAttackCard(cardType))
        {
            return DamagePreviewResult.NotDamageCard();
        }

        var isMultiTarget = IsMultiTargetCard(cardType);
        var targets = GetTargetsForCard(realContext, attacker, cardType, lockedSingleTarget);
        if (targets.Count == 0)
        {
            return DamagePreviewResult.NotDamageCard();
        }

        // 火攻（周瑜专属）基础伤害依赖目标，不能用无目标版本的 GetCardBaseDamage；
        // 单目标牌这里始终只有1个目标，直接取它来算展示用的基础伤害。
        var baseDamage = FoldingKnifeEquipment.AppliesTo(attacker, cardType)
            ? FoldingKnifeEquipment.DamagePerHit * FoldingKnifeEquipment.HitCount
            : cardType == CardType.FireAttack
                ? BattleRules.GetFireAttackDamage(targets[0])
                : BattleRules.GetCardBaseDamage(cardType);
        var targetBreakdown = new List<DamagePreviewTargetEntry>();
        var breakdown = new List<string>();
        var warnings = new List<string>();
        DamagePreviewFollowUp? followUp = null;
        var first = true;

        foreach (var target in targets)
        {
            var single = PreviewSingleTarget(realContext, attacker, cardType, target);
            targetBreakdown.Add(new DamagePreviewTargetEntry(target.DisplayName, single.Amount, single.Note));

            if (first)
            {
                breakdown.AddRange(single.Breakdown);
                followUp = single.FollowUp;
                first = false;
            }
        }

        if (cardType == CardType.Tuxi)
        {
            warnings.Add(Localization.Get("damage_preview.warning.tuxi_steal"));
        }

        var amounts = targetBreakdown.Select(t => t.Amount).ToList();
        var differ = amounts.Distinct().Count() > 1;
        var predictedTotal = isMultiTarget ? amounts.Sum() : amounts.FirstOrDefault();
        breakdown.Add(Localization.GetFmt("damage_preview.breakdown.total", predictedTotal));

        return DamagePreviewResult.Create(
            baseDamage,
            predictedTotal,
            isMultiTarget,
            differ,
            targetBreakdown,
            breakdown,
            warnings,
            followUp);
    }

    private static bool IsMultiTargetCard(CardType cardType) =>
        cardType is CardType.NanmanInvasion or CardType.Tuxi or CardType.ArrowBarrage;

    private static List<Player> GetTargetsForCard(
        BattleContext realContext,
        Player attacker,
        CardType cardType,
        Player? lockedSingleTarget)
    {
        if (IsMultiTargetCard(cardType))
        {
            if (realContext.Encounter == null)
            {
                return new List<Player>();
            }

            return realContext.Encounter.Enemies
                .Where(enemy => !enemy.IsDead && enemy != attacker)
                .Cast<Player>()
                .ToList();
        }

        if (lockedSingleTarget == null || lockedSingleTarget.IsDead)
        {
            return new List<Player>();
        }

        return new List<Player> { lockedSingleTarget };
    }

    private readonly struct SingleTargetPreview
    {
        public SingleTargetPreview(int amount, string? note, IReadOnlyList<string> breakdown, DamagePreviewFollowUp? followUp)
        {
            Amount = amount;
            Note = note;
            Breakdown = breakdown;
            FollowUp = followUp;
        }

        public int Amount { get; }
        public string? Note { get; }
        public IReadOnlyList<string> Breakdown { get; }
        public DamagePreviewFollowUp? FollowUp { get; }
    }

    /// <summary>
    /// 对单个目标跑一次"只读预览"：构造一个全新的、随用随弃的 BattleContext
    /// （共享真实 Player 与 TriggerManager，复制 TurnNumber/Encounter），
    /// 只触发 OnDamage 阶段，然后手动调用一次纯计算的 ResolveModifiers——
    /// 全程不触发 OnBeforeDamage（资源消耗/眩晕随机取消所在阶段）与
    /// OnDamageTaken（真正扣血/濒死判定所在阶段）。
    /// </summary>
    private static SingleTargetPreview PreviewSingleTarget(
        BattleContext realContext,
        Player attacker,
        CardType cardType,
        Player target)
    {
        if (FoldingKnifeEquipment.AppliesTo(attacker, cardType))
        {
            return PreviewFoldedKnife(realContext, attacker, target);
        }

        var baseDamage = cardType == CardType.FireAttack
            ? BattleRules.GetFireAttackDamage(target)
            : BattleRules.GetCardBaseDamage(cardType);
        var damage = RunPreviewDamageEvent(realContext, attacker, target, cardType, baseDamage);

        var breakdown = BuildBreakdown(cardType, damage);
        // TakeDamage cannot reduce HP below zero. Preview the actual amount dealt,
        // not the uncapped modifier result, so a nearly defeated target never shows
        // impossible excess damage on the card.
        var primaryDamage = Math.Min(damage.Amount, Math.Max(0, target.CurrentHP));
        var followUp = TryComputeJiAngFollowUp(realContext, attacker, target, damage, primaryDamage, target.CurrentHP, breakdown);
        var totalDamage = primaryDamage + (followUp?.Amount ?? 0);

        return new SingleTargetPreview(totalDamage, null, breakdown, followUp);
    }

    /// <summary>
    /// 折叠刀的普通杀不是一笔10点伤害，而是两笔独立的5点基础伤害；预览必须同样
    /// 分段运行 OnDamage，才能正确展示每段都会重复获得的普通杀加伤。这里不触发
    /// OnDamageTaken 的真实副作用，但会按每段剩余生命值限制显示量，并复用激昂的
    /// 只读追加估算。
    /// </summary>
    private static SingleTargetPreview PreviewFoldedKnife(BattleContext realContext, Player attacker, Player target)
    {
        var breakdown = new List<string>
        {
            $"折叠刀：{BattleRules.GetCardName(CardType.Kill)}分为{FoldingKnifeEquipment.HitCount}段，每段基础伤害{FoldingKnifeEquipment.DamagePerHit}。"
        };

        var remainingHealth = Math.Max(0, target.CurrentHP);
        var totalDamage = 0;
        var jiangTotal = 0;
        for (var hitIndex = 0;
             hitIndex < FoldingKnifeEquipment.HitCount && remainingHealth > 0;
             hitIndex++)
        {
            var damage = RunPreviewDamageEvent(
                realContext,
                attacker,
                target,
                CardType.Kill,
                FoldingKnifeEquipment.DamagePerHit);
            var primaryDamage = Math.Min(damage.Amount, remainingHealth);
            var hitBreakdown = BuildBreakdown(CardType.Kill, damage);
            if (hitIndex == 0)
            {
                // 第一段保留完整修正明细，避免多段显示同一批修正导致 Tooltip 过长。
                breakdown.AddRange(hitBreakdown);
            }

            var followUp = TryComputeJiAngFollowUp(
                realContext,
                attacker,
                target,
                damage,
                primaryDamage,
                remainingHealth,
                breakdown);
            var hitTotal = primaryDamage + (followUp?.Amount ?? 0);
            jiangTotal += followUp?.Amount ?? 0;
            totalDamage += hitTotal;
            remainingHealth = Math.Max(0, remainingHealth - hitTotal);
            breakdown.Add($"折叠刀第{hitIndex + 1}段最终伤害：{hitTotal}。");
        }

        DamagePreviewFollowUp? combinedFollowUp = jiangTotal > 0
            ? new DamagePreviewFollowUp(Localization.Get("damage_preview.followup.jiang"), jiangTotal)
            : null;
        return new SingleTargetPreview(totalDamage, null, breakdown, combinedFollowUp);
    }

    private static DamageEvent RunPreviewDamageEvent(
        BattleContext realContext,
        Player attacker,
        Player target,
        CardType cardType,
        int baseDamage)
    {
        var previewContext = new BattleContext(realContext.Player, realContext.TriggerManager)
        {
            TurnNumber = realContext.TurnNumber,
            Encounter = realContext.Encounter,
            IsDamagePreview = true,
        };

        var damage = new DamageEvent(attacker, target, cardType, baseDamage, isDirectAttackDamage: true);
        previewContext.DamageEvent = damage;
        previewContext.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, previewContext);
        damage.ResolveModifiers(attacker.WinePower);
        previewContext.DamageEvent = null;
        return damage;
    }

    private static List<string> BuildBreakdown(CardType cardType, DamageEvent damage)
    {
        var lines = new List<string>
        {
            Localization.GetFmt("damage_preview.breakdown.base", BattleRules.GetCardName(cardType), damage.BaseAmount)
        };

        foreach (var modifier in damage.Modifiers.Modifiers)
        {
            lines.Add($"{modifier.Name}：{modifier.Operation} {modifier.FloatValue}");
        }

        lines.Add(Localization.GetFmt("damage_preview.breakdown.card_damage", damage.Amount));
        return lines;
    }

    /// <summary>
    /// 孙策【激昂】命中后链式追加：真实效果（JiAngBattleEffect）挂在 OnDamageTaken，
    /// 会被本服务刻意跳过的阶段，因此需要在预览层复刻其触发门槛
    /// （HasSkill(JiAng) + 杀系攻击 + 本体预估伤害>0），额外单独跑一次
    /// 同样"只读 OnDamage"的预览。追加量本身固定为 floor(MaxHealth*0.20)，
    /// 与真实效果完全一致，并计入卡面最终伤害总数。
    /// </summary>
    private static DamagePreviewFollowUp? TryComputeJiAngFollowUp(
        BattleContext realContext,
        Player attacker,
        Player target,
        DamageEvent mainDamage,
        int primaryDamage,
        int targetHealthBeforeHit,
        List<string> breakdown)
    {
        // 真实【激昂】只会在本体实际造成伤害、且目标仍存活时追加攻击。
        if (primaryDamage <= 0 || targetHealthBeforeHit <= primaryDamage)
        {
            return null;
        }

        if (!attacker.HasSkill(SkillIds.JiAng) || !BattleRules.IsShaAttack(mainDamage.AttackType))
        {
            return null;
        }

        var extraDamage = (int)Math.Floor(attacker.MaxHealth * JiAngBattleEffect.ExtraDamageMaxHealthRatio);
        if (extraDamage <= 0)
        {
            return null;
        }

        var followUpDamage = RunPreviewDamageEvent(realContext, attacker, target, CardType.Kill, extraDamage);
        var remainingHp = targetHealthBeforeHit - primaryDamage;
        var actualFollowUpDamage = Math.Min(followUpDamage.Amount, Math.Max(0, remainingHp));
        if (actualFollowUpDamage <= 0)
        {
            return null;
        }

        breakdown.Add(Localization.GetFmt("damage_preview.breakdown.jiang_followup", actualFollowUpDamage));
        return new DamagePreviewFollowUp(Localization.Get("damage_preview.followup.jiang"), actualFollowUpDamage);
    }
}
