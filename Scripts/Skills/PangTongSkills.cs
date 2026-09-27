//////////////////////////////////////////////////////////
// 庞统专属技能：【涅槃】与【铁索连环】。
//
// 濒死救援统一由 OnDying 队列决定：涅槃优先于 DyingEffect，故只有涅槃
// 已触发或不具备条件时，桃/酒等通用救援才会被尝试。
//////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

/// <summary>
/// 【涅槃】：每场战斗第一次濒死时满血复活、全场费用设为1，并在本回合与下回合无敌。
/// </summary>
public sealed class PangTongNirvanaTriggerEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDying;
    public EffectPriority Priority => EffectPriority.Immediate;

    private const string TriggeredKey = "pangtong_nirvana_triggered";
    internal const string InvincibleTurnsRemainingKey = "pangtong_nirvana_invincible_turns_remaining";

    public void Execute(BattleContext context)
    {
        var target = context.DamageEvent?.Target;
        if (target == null || target.Health > 0 || context.GameOver
            || !target.HasSkill(SkillIds.PangTongNirvana)
            || target.RuntimeStates.ContainsKey(TriggeredKey))
        {
            return;
        }

        target.RuntimeStates[TriggeredKey] = true;
        // 触发所在回合和紧随其后的一个完整回合都应免疫伤害；回合结束时统一递减。
        target.RuntimeStates[InvincibleTurnsRemainingKey] = 2;
        target.SetCurrentHealth(target.MaxHealth);
        target.SetManaToOne();

        var enemies = context.Encounter?.Enemies;
        if (enemies != null)
        {
            foreach (var enemy in enemies)
            {
                if (!enemy.IsDead)
                {
                    enemy.SetManaToOne();
                }
            }
        }

        context.RoundResult.AddLine($"{target.DisplayName}【涅槃】触发：满血复活，全场费用重置为1；本回合及下回合无敌。");
        context.AddTriggerLog("[涅槃]");
        context.AddTriggerLog("Trigger: OnDying / Immediate");
        context.AddTriggerLog($"{target.DisplayName}：生命恢复至上限；所有存活单位费用→1；无敌剩余2回合。");
        context.ReportPlayerCharacterSkillTriggered(
            target, SkillIds.PangTongNirvana, Timing, Priority, "revive_full_and_reset_mana");
    }
}

/// <summary>
/// 【涅槃】的两回合无敌层。只在濒死后创建，不影响触发它的首次致死伤害本身。
/// </summary>
public sealed class PangTongNirvanaInvincibilityEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.Immediate;

    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Amount <= 0
            || !damage.Target.RuntimeStates.TryGetValue(PangTongNirvanaTriggerEffect.InvincibleTurnsRemainingKey, out var value)
            || value is not int turnsRemaining || turnsRemaining <= 0)
        {
            return;
        }

        // 无敌只免除庞统本人承受的伤害；若铁索连环已生效，原始伤害仍需同步给
        // 其它连锁单位。该标记由铁索连环的 OnDamageTaken 结算器单独消费。
        damage.PropagateThroughIronChainWhenCancelled = true;
        damage.CancelAsFullyBlocked();
        context.RoundResult.AddLine($"{damage.Target.DisplayName}【涅槃】：无敌抵挡本次伤害（剩余{turnsRemaining}回合）。");
        context.AddTriggerLog($"[涅槃] OnBeforeDamage：无敌生效，剩余{turnsRemaining}回合。");
        context.ReportPlayerCharacterSkillTriggered(
            damage.Target, SkillIds.PangTongNirvana, Timing, Priority, "invincible");
    }
}

/// <summary>
/// 每回合结束后消耗一次涅槃无敌计数：触发回合结束后覆盖下一回合，下一回合结束后失效。
/// </summary>
public sealed class PangTongNirvanaInvincibilityTurnEndEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnEnd;
    public EffectPriority Priority => EffectPriority.Highest;

    public void Execute(BattleContext context)
    {
        ConsumeForUnit(context, context.Player);
        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            ConsumeForUnit(context, enemy);
        }
    }

    private static void ConsumeForUnit(BattleContext context, Player target)
    {
        if (!target.RuntimeStates.TryGetValue(PangTongNirvanaTriggerEffect.InvincibleTurnsRemainingKey, out var value)
            || value is not int turnsRemaining || turnsRemaining <= 0)
        {
            return;
        }

        var next = turnsRemaining - 1;
        if (next <= 0)
        {
            target.RuntimeStates.Remove(PangTongNirvanaTriggerEffect.InvincibleTurnsRemainingKey);
            context.RoundResult.AddLine($"{target.DisplayName}【涅槃】无敌结束。");
            return;
        }

        target.RuntimeStates[PangTongNirvanaTriggerEffect.InvincibleTurnsRemainingKey] = next;
        context.AddTriggerLog($"[涅槃] 回合结束：{target.DisplayName}无敌剩余{next}回合。");
    }
}

/// <summary>
/// 【铁索连环】使用时先获得一层护盾，连锁从下一回合开始生效。
/// </summary>
public sealed class PangTongIronChainActivateEffect : IBattleEffect
{
    internal const string ShieldStateKey = "pangtong_iron_chain_shield";
    internal const string PendingActivationTurnKey = "pangtong_iron_chain_pending_activation_turn";
    internal const string ActiveStateKey = "pangtong_iron_chain_active";

    public TriggerTiming Timing => TriggerTiming.OnBattlePhase;
    public EffectPriority Priority => EffectPriority.High;

    public void Execute(BattleContext context)
    {
        if (context.PlayerAction?.Type != CardType.IronChain || context.PlayerActionCancelled)
        {
            return;
        }

        // 铁索连环是可被【无懈可击】反制的全场锦囊。反制只取消这一次尚未在下回合
        // 生效的连锁；此前已经生效的连锁和已获得的护盾均不受影响。
        if (IsCounteredByUnassailable(context))
        {
            context.PlayerActionCancelled = true;
            ClearPendingChainActivation(context.Player);
            context.RoundResult.AddLine("无懈可击：铁索连环被反制，本次下回合连锁不会生效。");
            context.AddTriggerLog("[铁索连环] 被无懈可击反制，仅清除本次待激活连锁状态。");
            return;
        }

        context.Player.RuntimeStates[ShieldStateKey] = true;
        context.Player.RuntimeStates[PendingActivationTurnKey] = context.TurnNumber + 1;
        context.RoundResult.AddLine("铁索连环：获得1层护盾；所有单位将在下回合起进入连锁。");
        context.AddTriggerLog("[铁索连环] Trigger: OnBattlePhase / High，护盾已获得，连锁待下回合激活。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.PangTongIronChain, Timing, Priority, "queued");
    }

    private static bool IsCounteredByUnassailable(BattleContext context)
    {
        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return false;
        }

        foreach (var entry in context.EnemyActions)
        {
            if (!entry.Enemy.IsDead && !entry.ActionCancelled && entry.Action.IsUnassailable)
            {
                return true;
            }
        }

        return false;
    }

    internal static void ClearPendingChainActivation(Player player)
    {
        player.RuntimeStates.Remove(PendingActivationTurnKey);
    }
}

/// <summary>
/// 在下一回合开始启用连锁。连锁持续到本场战斗结束，护盾被击破不会移除该状态。
/// </summary>
public sealed class PangTongIronChainActivateNextTurnEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.Highest;

    public void Execute(BattleContext context)
    {
        if (!context.Player.RuntimeStates.TryGetValue(PangTongIronChainActivateEffect.PendingActivationTurnKey, out var value)
            || value is not int activationTurn
            || context.TurnNumber < activationTurn)
        {
            return;
        }

        context.Player.RuntimeStates.Remove(PangTongIronChainActivateEffect.PendingActivationTurnKey);
        context.Player.RuntimeStates[PangTongIronChainActivateEffect.ActiveStateKey] = true;
        context.RoundResult.AddLine("铁索连环生效：本场战斗起，所有存活单位共享每回合第一段伤害。");
        context.AddTriggerLog("[铁索连环] Trigger: OnTurnStart / Highest，连锁已激活。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.PangTongIronChain, Timing, Priority, "active");
    }
}

/// <summary>
/// 铁索连环给予的单层护盾。它独立于连锁状态，抵挡一次合法伤害后只移除护盾。
/// </summary>
public sealed class PangTongIronChainShieldEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.High;

    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Amount <= 0
            || damage.OnlyAllowCounterOrCardShields
            || !ReferenceEquals(damage.Target, context.Player)
            || !context.Player.RuntimeStates.TryGetValue(PangTongIronChainActivateEffect.ShieldStateKey, out var shield)
            || shield is not true)
        {
            return;
        }

        damage.CancelAsFullyBlocked();
        context.Player.RuntimeStates.Remove(PangTongIronChainActivateEffect.ShieldStateKey);
        context.RoundResult.AddLine("铁索连环护盾：抵挡本次伤害；连锁状态未被切断。");
        context.AddTriggerLog("[铁索连环] 护盾吸收伤害，连锁保持。");
    }
}

/// <summary>
/// 在伤害实际扣除后复制本回合的第一段伤害。复制伤害不再进入伤害触发队列，
/// 从而保证每回合只结算一次，并让这次结算早于后续反伤技能。
/// </summary>
public sealed class PangTongIronChainShareDamageEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Mid;

    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || context.IronChainSharedDamageSettledThisRound || !IsChainActive(context))
        {
            return;
        }

        // 常规情形复制目标实际受到的伤害；涅槃无敌则只免除庞统自身扣血，仍将这次
        // 本应命中的原始伤害溢出给其余连锁单位。
        var sharedDamage = damage.ActualDamageDealt > 0
            ? damage.ActualDamageDealt
            : damage.PropagateThroughIronChainWhenCancelled ? damage.Amount : 0;
        if (sharedDamage <= 0)
        {
            return;
        }

        context.IronChainSharedDamageSettledThisRound = true;
        var units = GetLivingUnits(context);
        var sharedTargets = 0;
        foreach (var target in units)
        {
            if (ReferenceEquals(target, damage.Target))
            {
                continue;
            }

            var hpBefore = target.CurrentHP;
            target.TakeDamage(sharedDamage);
            var actual = Math.Max(0, hpBefore - target.CurrentHP);
            context.RoundResult.AddDamage(target, actual);
            context.RecordDirectDamage(
                target,
                sharedDamage,
                actual,
                hpBefore,
                target.CurrentHP,
                new HealthChangeSource(
                    HealthChangeSourceKind.Skill,
                    "铁索连环",
                    SkillIds.PangTongIronChain,
                    context.Player));
            sharedTargets++;

            if (target.Health <= 0 && !target.IsDead)
            {
                var previousDamage = context.DamageEvent;
                context.DamageEvent = new DamageEvent(
                    damage.Source,
                    target,
                    CardType.IronChain,
                    sharedDamage,
                    overrideDamageType: DamageType.Mechanic,
                    origin: new HealthChangeSource(
                        HealthChangeSourceKind.Skill,
                        "铁索连环",
                        SkillIds.PangTongIronChain,
                        context.Player))
                {
                    ActualDamageDealt = actual
                };
                context.RaiseOnDying();
                context.DamageEvent = previousDamage;
            }
        }

        context.RoundResult.AddLine($"铁索连环：{sharedDamage}点伤害同步至其余{sharedTargets}名连锁单位（本回合不再重复结算）。");
        context.AddTriggerLog($"[铁索连环] 首段共享伤害={sharedDamage}，同步目标={sharedTargets}。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.PangTongIronChain, Timing, Priority, "share_first_damage");
    }

    private static bool IsChainActive(BattleContext context)
        => context.Player.RuntimeStates.TryGetValue(PangTongIronChainActivateEffect.ActiveStateKey, out var active)
           && active is true;

    private static List<Player> GetLivingUnits(BattleContext context)
    {
        var units = new List<Player>();
        if (!context.Player.IsDead)
        {
            units.Add(context.Player);
        }

        var enemies = context.Encounter?.Enemies;
        if (enemies != null)
        {
            foreach (var enemy in enemies)
            {
                if (!enemy.IsDead)
                {
                    units.Add(enemy);
                }
            }
        }

        return units;
    }
}

public sealed class PangTongNirvanaEffect : ISkillEffect
{
    public string SkillId => SkillIds.PangTongNirvana;
    public void Register(TriggerManager triggerManager, Player owner) { }
}

public sealed class PangTongIronChainEffect : ISkillEffect
{
    public string SkillId => SkillIds.PangTongIronChain;
    public void Register(TriggerManager triggerManager, Player owner) { }
}
