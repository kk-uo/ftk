//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/SunCeSkills.cs
//
// 模块：Skill System
//
// 职责：
// 1. 承载角色技能、Boss 技能与技能触发效果相关代码。
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

using System;

/// <summary>
/// Skill System 的公开类：JiAngBattleEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class JiAngBattleEffect : IBattleEffect
{
    private const string JiAngResolvingExtraKey = "jiang_resolving_extra";
    internal const double ExtraDamageMaxHealthRatio = 0.20;

    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || context.GameOver)
        {
            return;
        }

        // 与单位无关：谁出手（damage.Source）带着激昂技能就对谁生效，不再局限于 context.Player，
        // 这样敌人（例如深渊共生体）持有激昂时也能正确触发，孙策自己出牌时 damage.Source 本来
        // 就是 context.Player，行为完全不变。
        var source = damage.Source;
        if (!source.HasSkill(SkillIds.JiAng))
        {
            return;
        }

        if (!BattleRules.IsShaAttack(damage.AttackType))
        {
            return;
        }

        if (source.RuntimeStates.TryGetValue(JiAngResolvingExtraKey, out var resolvingExtra)
            && resolvingExtra is true)
        {
            return;
        }

        if (damage.ActualDamageDealt > 0)
        {
            ResolveJiAngHit(context, source, damage);
            return;
        }

        ResolveJiAngMiss(context, source);
    }

    private static void ResolveJiAngHit(BattleContext context, Player source, DamageEvent damage)
    {
        var extraDamage = (int)Math.Floor(source.MaxHealth * ExtraDamageMaxHealthRatio);
        source.RuntimeStates[JiAngResolvingExtraKey] = true;
        try
        {
            // 追加杀需要存活目标；但原杀已经命中这一事实不会因目标随后死亡而消失。
            // 因此击杀持有【完杀】的敌人时，固定的命中回血仍必须结算。
            if (!damage.Target.IsDead && extraDamage > 0)
            {
                context.RoundResult.AddLine($"激昂：{source.DisplayName}追加一次普通杀伤害（{extraDamage}）。");
                context.AddTriggerLog("[激昂]");
                context.AddTriggerLog($"命中{damage.Target.DisplayName}：追加普通杀伤害 {extraDamage}。");

                context.DamageEvent = new DamageEvent(source, damage.Target, CardType.Kill, extraDamage, isDirectAttackDamage: true);
                context.TriggerManager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
                context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
                context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
                context.DamageEvent = null;
            }

            // 【激昂】的固定自我回复是命中技能结算，不属于会触发【完杀】的普通 OnHeal 事件。
            // 同时记录为技能来源的回血，保证战斗日志仍可完整追溯。
            var healed = BattleHealing.Apply(
                context,
                source,
                5,
                false,
                new HealthChangeSource(HealthChangeSourceKind.Skill, "激昂", SkillIds.JiAng, source),
                raiseOnHeal: false).HealedAmount;
            if (healed > 0)
            {
                context.RoundResult.AddLine($"激昂：{source.DisplayName}回复{healed}点生命。");
                context.AddTriggerLog($"激昂回复：+{healed}生命。");
            }

            context.ReportPlayerCharacterSkillTriggered(
                source,
                SkillIds.JiAng,
                TriggerTiming.OnDamageTaken,
                EffectPriority.Low,
                "hit");
        }
        finally
        {
            source.RuntimeStates.Remove(JiAngResolvingExtraKey);
        }
    }

    private static void ResolveJiAngMiss(BattleContext context, Player source)
    {
        var currentHp = source.Health;
        var hpLoss = (int)Math.Floor(currentHp * 0.33);
        if (hpLoss > 0)
        {
            // 激昂失手写作“失去生命”，不是一次可被减伤、护盾或受伤统计影响的伤害。
            // 仍在下方通过 OnDying 接入统一的濒死/死亡结算。
            source.LoseHealth(hpLoss);
            context.RoundResult.AddLine($"激昂失手：{source.DisplayName}失去{hpLoss}点生命。");
            context.AddTriggerLog("[激昂]");
            context.AddTriggerLog($"未造成伤害：失去{hpLoss}点生命。");

            if (source.Health <= 0)
            {
                context.DamageEvent = new DamageEvent(source, source, CardType.Kill, hpLoss, isDirectAttackDamage: true);
                context.RaiseOnDying();
                context.DamageEvent = null;
            }
        }

        source.GainMana(1);
        context.ReportPlayerCharacterSkillTriggered(
            source,
            SkillIds.JiAng,
            TriggerTiming.OnDamageTaken,
            EffectPriority.Low,
            "miss");
        context.RoundResult.AddLine($"激昂失手：{source.DisplayName}获得1点费用。");
        context.AddTriggerLog("激昂失手：+1费。");
    }
}

/// <summary>
/// Skill System 的公开类：HunZiTriggerEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class HunZiTriggerEffect : IBattleEffect
{
    internal const string HunZiTriggeredKey = "hunzi_triggered";
    internal const string HunZiOriginalMaxKey = "hunzi_original_max_hp";
    internal const double MaxHealthReductionRatio = 0.20;
    internal const int FlatMaxHealthReduction = 10;
    internal const int MinimumTriggerMaxHealth = 20;

    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null
            || damage.Cancelled
            || damage.ActualDamageDealt <= 0
            || damage.Target != context.Player
            || context.GameOver)
        {
            return;
        }

        var player = context.Player;
        if (!player.HasSkill(SkillIds.HunZi))
        {
            return;
        }

        var currentHp = player.Health;
        var currentMax = player.MaxHealth;
        // 魂姿只处理受伤后仍然存活的低血量状态；致命伤继续进入正常濒死流程，
        // 不能通过缩减最大生命值并回满来取消本次死亡。
        if (currentMax <= MinimumTriggerMaxHealth || currentHp <= 0 || currentHp * 2 >= currentMax)
        {
            return;
        }

        player.RuntimeStates[HunZiTriggeredKey] = true;
        if (!player.RuntimeStates.ContainsKey(HunZiOriginalMaxKey))
        {
            player.RuntimeStates[HunZiOriginalMaxKey] = currentMax;
        }

        var reducedMax = Math.Max(1, currentMax
            - (int)Math.Floor(currentMax * MaxHealthReductionRatio)
            - FlatMaxHealthReduction);
        player.DebugSetMaxHealth(reducedMax);
        var healed = BattleHealing.Apply(
            context,
            player,
            reducedMax - player.Health,
            false,
            new HealthChangeSource(HealthChangeSourceKind.Skill, "魂姿", SkillIds.HunZi, player)).HealedAmount;

        context.RoundResult.AddLine($"魂姿触发：最大生命值 {currentMax} → {reducedMax}，并回复至 {player.Health}/{player.MaxHealth}。");
        if (healed > 0)
        {
            context.RoundResult.AddHeal(player, healed);
        }
        context.ReportPlayerCharacterSkillTriggered(player, SkillIds.HunZi, Timing, Priority);
        context.AddTriggerLog("[魂姿]");
        context.AddTriggerLog($"受到伤害后低于50%生命：MaxHP {currentMax} -> {reducedMax}，回复 {healed}。");
    }

    /// <summary>
    /// 在战斗结束时恢复【魂姿】首次触发前记录的最大生命值。
    ///
    /// 记录只在本场战斗第一次触发时写入，因此无论本场触发多少次，
    /// 都会恢复到战斗开始时的同一个基准值。
    /// </summary>
    internal static void RestoreBattleMaxHealth(Player player)
    {
        if (player.RuntimeStates.TryGetValue(HunZiOriginalMaxKey, out var originalMaxObj)
            && originalMaxObj is int originalMax
            && originalMax > 0)
        {
            player.DebugSetMaxHealth(originalMax);
        }

        player.RuntimeStates.Remove(HunZiTriggeredKey);
        player.RuntimeStates.Remove(HunZiOriginalMaxKey);
    }
}
