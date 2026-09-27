//////////////////////////////////////////////////////////
// 文件：Scripts/Reactions/LongdanReaction.cs
//
// 模块：Reaction System
//
// 职责：
// 1. 承载实时反应窗口与响应式出牌相关代码。
// 2. 为其它模块提供清晰、稳定的调用边界。
// 3. 保持本文件内的状态变化可追踪、可调试。
//
// 不负责：
// × 处理无关模块的业务规则。
// × 绕过既有 Manager 或 Trigger 流程直接改写跨系统状态。
// × 在数据定义层混入表现层细节。
//
// 主要依赖：
// Godot / C# Runtime
// 项目内对应 Manager、Database 与 Trigger 系统
//////////////////////////////////////////////////////////

using System.Collections.Generic;

/// <summary>
/// Reaction System 的公开类：LongdanReaction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class LongdanReaction : IReaction
{
    /// <summary>
    /// Reaction System 的公开入口：LongdanReaction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public LongdanReaction(double currentMana)
    {
        Options = new List<ReactionOption>
        {
            CreateAttackOption(CardType.Kill, currentMana),
            CreateAttackOption(CardType.FireKill, currentMana),
            CreateAttackOption(CardType.ThunderKill, currentMana),
            new("pass", Localization.Get("reaction.option.pass"), true, Pass)
        };
    }

    public string Id => ReactionIds.Longdan;
    public TriggerTiming Timing => TriggerTiming.OnBattlePostPhase;
    public EffectPriority Priority => EffectPriority.High;
    public string Title => Localization.Get("reaction.longdan.title");
    public List<ReactionOption> Options { get; }

    /// <summary>
    /// Reaction System 的公开入口：GetAvailableCardTypes。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static List<CardType> GetAvailableCardTypes(double currentMana)
    {
        var available = new List<CardType>();
        foreach (var cardType in new[] { CardType.Kill, CardType.FireKill, CardType.ThunderKill })
        {
            if (BattleRules.CanPlayReactionCard(ReactionIds.Longdan, cardType, currentMana))
            {
                available.Add(cardType);
            }
        }

        return available;
    }

    private static ReactionOption CreateAttackOption(CardType type, double currentMana)
    {
        var enabled = BattleRules.CanPlayReactionCard(ReactionIds.Longdan, type, currentMana);
        var cost = BattleRules.GetReactionCardCost(ReactionIds.Longdan, type);
        var name = BattleRules.GetCardName(type);
        var text = enabled ? name : Localization.GetFmt("reaction.option.insufficient_cost_fmt", name);
        return new ReactionOption(type.ToString(), text, enabled, context => ResolveLongdanAttack(context, type, cost), type, cost);
    }

    /// <summary>
    /// Reaction System 的公开入口：HasTriggeringEnemyAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool HasTriggeringEnemyAction(BattleContext context)
    {
        foreach (var enemyEntry in context.EnemyActions)
        {
            if (!enemyEntry.Enemy.IsDead && enemyEntry.Action.CanTriggerDragonCourage)
            {
                return true;
            }
        }

        return false;
    }

    private static void ResolveLongdanAttack(BattleContext context, CardType attackType, double cost)
    {
        // 【闪】不是指定目标的攻击牌，因此 PlayerAction.Target 通常为空。
        // 旧代码只调用 GetTargetEnemyActionEntry，导致龙胆虽然能打开并接受点击，
        // 却在这里拿不到敌方行动而直接 return，反击完全没有进入伤害结算。
        // 优先使用玩家当前锁定且正在发动攻击的敌人；未锁定时（例如多目标战斗中
        // 直接打出闪）稳定地选择第一个触发龙胆的存活攻击者。
        var enemyEntry = GetReactionTargetEnemyActionEntry(context);
        if (enemyEntry == null)
        {
            context.AddTriggerLog("龙胆反击未找到有效敌方攻击目标。");
            return;
        }

        var cardName = BattleRules.GetCardName(attackType);
        context.RoundResult.AddLine("龙胆触发。");
        context.RoundResult.AddLine($"玩家选择：{cardName}。");
        context.AddTriggerLog("[龙胆]");
        context.AddTriggerLog($"玩家选择：{cardName}");

        if (enemyEntry.Action.IsArrowBarrage)
        {
            context.PlayerDodgeDefenseActive = false;
            context.RoundResult.RemovePersistentDodgeBlock(true, CardType.ArrowBarrage);
            context.RoundResult.AddLine("闪 → " + cardName + "。");
            if (cost > 0)
            {
                BattleRules.PayManaAndRaiseResourceChanged(context, context.Player, cost, true);
            }

            BattlePhaseResolutionEffect.DealAttackDamage(context, context.Player, enemyEntry.Enemy, attackType);
            BattlePhaseResolutionEffect.ApplyArrowBarrageHit(context, enemyEntry.Enemy, context.Player);
            return;
        }

        var restoredDamage = context.RoundResult.NullifyPlayerDamageByLongdan();
        if (restoredDamage > 0)
        {
            BattleHealing.Apply(
                context,
                context.Player,
                restoredDamage,
                false,
                new HealthChangeSource(HealthChangeSourceKind.Skill, "龙胆", SkillIds.Longdan, context.Player));
        }

        if (cost > 0)
        {
            BattleRules.PayManaAndRaiseResourceChanged(context, context.Player, cost, true);
        }

        BattlePhaseResolutionEffect.ResolveReactionAttack(
            context,
            enemyEntry.Enemy,
            BattleAction.FromCard(new Card(attackType)),
            enemyEntry.Action);
    }

    private static EnemyActionEntry? GetReactionTargetEnemyActionEntry(BattleContext context)
    {
        var lockedEntry = context.GetPlayerLockedEnemyActionEntry();
        if (lockedEntry != null
            && !lockedEntry.Enemy.IsDead
            && lockedEntry.Action.CanTriggerDragonCourage)
        {
            return lockedEntry;
        }

        foreach (var entry in context.EnemyActions)
        {
            if (!entry.Enemy.IsDead && entry.Action.CanTriggerDragonCourage)
            {
                return entry;
            }
        }

        return null;
    }

    private static void Pass(BattleContext context)
    {
        context.RoundResult.AddLine("玩家放弃龙胆。");
        context.AddTriggerLog("[龙胆]");
        context.AddTriggerLog("玩家选择：放弃");
    }
}
