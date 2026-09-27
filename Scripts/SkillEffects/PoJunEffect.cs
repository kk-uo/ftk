//////////////////////////////////////////////////////////
// 文件：Scripts/SkillEffects/PoJunEffect.cs
//
// 模块：Skill Effect System
//
// 职责：
// 1. 承载技能效果实现与 Trigger 接入相关代码。
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

// 破军（徐盛专属·稀有）：
// 玩家版本：杀系攻击命中后，逐次消耗0.5费使本次伤害+1倍（即×(1+N)），可多次触发。
// 敌方AI版本：按0.5费/次自动计算足以击杀目标的次数。

/// <summary>
/// Skill Effect System 的公开类：PoJunEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class PoJunEffect : ISkillEffect
{
    public string SkillId => SkillIds.PoJun;
    /// <summary>
    /// Skill Effect System 的公开入口：Register。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Register(TriggerManager triggerManager, Player owner) { }
}

// 敌方AI版本：OnDamage, Mid — 对杀系攻击自动追加倍率，公式 (1+N)×基础伤害。
/// <summary>
/// Skill Effect System 的公开类：PoJunEnemyDamageEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class PoJunEnemyDamageEffect : IBattleEffect
{
    private const double ManaCostPerTrigger = 0.5;
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled) return;
        if (damage.Source == context.Player) return;          // 玩家版本由窗口处理
        if (!damage.Source.HasSkill(SkillIds.PoJun)) return;
        if (!BattleRules.IsShaAttack(damage.AttackType)) return;
        if (context.IgnorePoJunCapture) return;              // 防止补刀循环

        var source = damage.Source;
        var target = damage.Target;

        var maxClicks = (int)System.Math.Floor(source.CurrentMana / ManaCostPerTrigger);
        if (maxClicks <= 0) return;

        // AI决策：找出最少触发次数使总伤害 >= 目标剩余血量；若无法击杀则全力触发。
        var estBase = damage.BaseAmount;
        var targetHp = target.Health;
        var clicks = maxClicks;
        for (var n = 0; n <= maxClicks; n++)
        {
            if (estBase * (1 + n) >= targetHp)
            {
                clicks = n;
                break;
            }
        }

        if (clicks <= 0) return;

        BattleRules.PayManaAndRaiseResourceChanged(context, source, clicks * ManaCostPerTrigger, true);
        damage.AddModifier(new DamageModifier(
            "破军",
            DamageModifierPriority.SpecialMultiplier,
            DamageModifierOperation.MultiplyFloat,
            1.0 + clicks));

        context.RoundResult.AddLine($"{source.DisplayName}发动【破军】×{clicks}：消耗{BattleRules.FormatMana(clicks * ManaCostPerTrigger)}费，本次攻击伤害×{1 + clicks}。");
        context.AddTriggerLog($"[破军/AI] 触发{clicks}次，消耗{BattleRules.FormatMana(clicks * ManaCostPerTrigger)}费，伤害×{1 + clicks}。");
    }
}

// 玩家版本捕获效果：OnDamageTaken, Lowest — 在 ApplyDamageEffect（Mid）完成修正器结算后，
// 立即进入破军 ReactionQueue；BattleManager 会在 OnBattlePhase 返回后、OnBattleEnd 前显示它。
/// <summary>
/// Skill Effect System 的公开类：PoJunPlayerCaptureEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class PoJunPlayerCaptureEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.IgnorePoJunCapture) return;
        if (context.GameOver) return;
        if (context.PoJunPendingAmount > 0) return;          // 同回合只捕获第一次命中（AOE场景）

        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || !damage.IsResolved) return;
        if (damage.Source != context.Player) return;
        if (!damage.Source.HasSkill(SkillIds.PoJun)) return;
        if (!BattleRules.IsShaAttack(damage.AttackType)) return;
        // “命中”必须是已经对目标造成了实际生命损失；被闪避、护盾完全吸收或
        // 无敌抵消的杀不会打开破军反应条。
        if (damage.ActualDamageDealt <= 0)
        {
            context.AddTriggerLog("[破军] 未捕获：本次杀未造成实际生命损失（被闪避、护盾或无敌抵消）。");
            return;
        }

        context.PoJunPendingAmount = damage.ActualDamageDealt;
        context.PoJunPendingAttackType = damage.AttackType;
        context.PoJunPendingTarget = damage.Target;

        if (damage.Target is EnemyInstance enemy && enemy.IsDead)
        {
            context.AddTriggerLog("[破军] 未打开反应条：本次命中已击败目标。");
            return;
        }

        // 费用检查在命中当下完成：普通【杀】的 1 费已经扣除，只要还有 0.5 费，
        // 就直接把追击塞进反应队列，不再等 BattlePostPhase。
        if (context.Player.CurrentMana < 0.5)
        {
            context.RoundResult.AddLine("破军未触发：命中后剩余费用不足0.5费。");
            context.AddTriggerLog($"[破军] 未打开反应条：命中时剩余{BattleRules.FormatMana(context.Player.CurrentMana)}费，不足0.5费。");
            return;
        }

        context.Reactions.Enqueue(new BreakArmyReaction(
            context.Player.CurrentMana,
            damage.ActualDamageDealt,
            damage.AttackType,
            damage.Target));
        context.AddTriggerLog($"[破军] 命中捕获：已结算伤害={damage.ActualDamageDealt}，招式={BattleRules.GetCardName(damage.AttackType)}，目标={damage.Target.DisplayName}；立即进入反应条。");
        context.AddTriggerLog("ReactionWindow触发：破军（即时）");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.PoJun, Timing, Priority, "reaction_window");
    }
}
