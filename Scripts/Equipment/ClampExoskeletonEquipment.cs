//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/ClampExoskeletonEquipment.cs
//
// 模块：Equipment System
//
// 职责：
// 1. 承载装备触发效果与装备战斗逻辑相关代码。
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

// 钳制机械外骨骼（传奇）：
// 触发条件：生命值首次跌至最大生命值的50%及以下。
// 触发效果：拦截当次死亡、当回合无敌、对敌方使用顺手牵羊并进入修复待机。
// 修复效果：下一个回合开始时恢复至满生命值。
// 每场战斗仅触发一次。
/// <summary>
/// Equipment System 的公开类：ClampExoskeletonTriggerEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ClampExoskeletonTriggerEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    // Low：在 ApplyDamageEffect（Mid）之后、DamageTakenEffect（Low）之前执行（注册顺序靠前）。
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Amount <= 0)
        {
            return;
        }

        CheckAndTrigger(context, damage.Target, damage.Amount, damageAlreadyApplied: true);
    }

    internal static bool CheckAndTrigger(BattleContext context, Player target, int damageDealt, bool damageAlreadyApplied)
    {
        var threshold = target.MaxHealth / 2;
        var hpBefore = damageAlreadyApplied ? target.Health + damageDealt : target.Health;
        var hpAfter = damageAlreadyApplied ? target.Health : System.Math.Max(0, target.Health - damageDealt);
        if (hpBefore <= threshold || hpAfter > threshold)
        {
            return false;
        }

        if (target == context.Player)
        {
            if (!GameManager.HasEquipment(EquipmentIds.ClampExoskeleton) || !context.Player.ClampExoskeletonActive)
            {
                return false;
            }

            context.Player.ConsumeClampExoskeleton();
            if (target.Health <= 0)
            {
                target.DebugSetHealth(1);
            }
            context.RoundResult.AddLine(Localization.GetFmt("battle.equipment.clamp_trigger_fmt", target.DisplayName, target.Health, target.MaxHealth));
            ExecuteStealFromFirstAliveEnemy(context, context.Player);
            context.RoundResult.AddLine(Localization.GetFmt("battle.equipment.clamp_repair_state_fmt", target.DisplayName));
            context.AddTriggerLog("[Equipment/ClampExoskeleton]");
            context.AddTriggerLog($"触发：{target.DisplayName} HP={target.Health}/{target.MaxHealth}，拦截死亡+本回合无敌+顺手牵羊+修复待机。");
            return true;
        }
        else if (target is EnemyInstance enemyTarget)
        {
            if (!enemyTarget.HasEquipment(EquipmentIds.ClampExoskeleton))
            {
                return false;
            }

            if (!enemyTarget.RuntimeStates.TryGetValue("clamp_exo_active", out var activeVal) || activeVal is not true)
            {
                return false;
            }

            enemyTarget.RuntimeStates["clamp_exo_active"] = false;
            enemyTarget.RuntimeStates["clamp_exo_repair_pending"] = true;
            enemyTarget.RuntimeStates["clamp_exo_invincible_this_round"] = true;
            if (target.Health <= 0)
            {
                target.DebugSetHealth(1);
            }
            context.RoundResult.AddLine(Localization.GetFmt("battle.equipment.clamp_trigger_fmt", target.DisplayName, target.Health, target.MaxHealth));
            ExecuteStealFromPlayer(context, enemyTarget);
            context.RoundResult.AddLine(Localization.GetFmt("battle.equipment.clamp_repair_state_fmt", target.DisplayName));
            context.AddTriggerLog("[Equipment/ClampExoskeleton]");
            context.AddTriggerLog($"触发：{target.DisplayName} HP={target.Health}/{target.MaxHealth}，拦截死亡+本回合无敌+顺手牵羊+修复待机。");
            return true;
        }

        return false;
    }

    private static void ExecuteStealFromPlayer(BattleContext context, EnemyInstance thief)
    {
        if (context.Player.InShadowState)
        {
            context.RoundResult.AddLine(Localization.GetFmt("battle.equipment.clamp_steal_shadow_fmt", context.Player.DisplayName));
            context.AddTriggerLog("[影袭]");
            context.AddTriggerLog($"{context.Player.DisplayName}影袭状态：钳制机械外骨骼顺手牵羊无效。");
            return;
        }

        var stealAmount = context.Player.GetStealableMana();
        if (stealAmount <= 0)
        {
            context.RoundResult.AddLine(Localization.GetFmt("battle.equipment.clamp_no_mana_fmt", context.Player.DisplayName));
            return;
        }

        BattleRules.PayManaAndRaiseResourceChanged(context, context.Player, stealAmount, false);
        thief.GainProtectedStealMana(stealAmount);
        context.RoundResult.AddSteal(thief, stealAmount);
    }

    private static void ExecuteStealFromFirstAliveEnemy(BattleContext context, Player thief)
    {
        var target = context.GetFirstAliveEnemy();
        if (target == null)
        {
            context.RoundResult.AddLine(Localization.Get("battle.equipment.clamp_no_enemy"));
            return;
        }

        if (target.InShadowState)
        {
            context.RoundResult.AddLine(Localization.GetFmt("battle.equipment.clamp_steal_shadow_fmt", target.DisplayName));
            context.AddTriggerLog("[影袭]");
            context.AddTriggerLog($"{target.DisplayName}影袭状态：钳制机械外骨骼顺手牵羊无效。");
            return;
        }

        var stealAmount = target.GetStealableMana();
        if (stealAmount <= 0)
        {
            context.RoundResult.AddLine(Localization.GetFmt("battle.equipment.clamp_no_mana_fmt", target.DisplayName));
            return;
        }

        BattleRules.PayManaAndRaiseResourceChanged(context, target, stealAmount, false);
        thief.GainProtectedStealMana(stealAmount);
        context.RoundResult.AddSteal(thief, stealAmount);
    }
}

/// <summary>
/// 预留外骨骼的濒死保护。死亡检查在实际扣血阶段内同步执行，
/// 因此致死伤害必须在扣血前截断为至少保留 1 点生命。
/// </summary>
public sealed class ClampExoskeletonPreDamageEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.Lowest;

    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Amount <= 0)
        {
            return;
        }

        var activated = ClampExoskeletonTriggerEffect.CheckAndTrigger(
            context,
            damage.Target,
            damage.Amount,
            damageAlreadyApplied: false);
        if (activated && damage.Amount >= damage.Target.Health)
        {
            damage.AddModifier(new DamageModifier(
                "钳制机械外骨骼·濒死保护",
                DamageModifierPriority.DamageCap,
                DamageModifierOperation.Cap,
                System.Math.Max(0, damage.Target.Health - 1)));
        }
    }
}

/// <summary>钳制机械外骨骼在触发回合内拦截后续伤害，并在回合结束时失效。</summary>
public sealed class ClampExoskeletonInvincibilityEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.Highest;

    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Amount <= 0)
        {
            return;
        }

        var active = damage.Target == context.Player
            ? context.Player.ClampExoskeletonInvincibleThisRound
            : damage.Target is EnemyInstance enemy
                && enemy.RuntimeStates.TryGetValue("clamp_exo_invincible_this_round", out var value)
                && value is true;
        if (!active)
        {
            return;
        }

        damage.Cancelled = true;
        context.RoundResult.AddLine(Localization.GetFmt("battle.equipment.clamp_invincible_fmt", damage.Target.DisplayName));
        context.AddTriggerLog("[Equipment/ClampExoskeleton] 本回合无敌抵消伤害。");
    }
}

// 钳制机械外骨骼修复效果：回合开始时检查修复待机标记，有则恢复至满生命值。
/// <summary>
/// Equipment System 的公开类：ClampExoskeletonRepairEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ClampExoskeletonRepairEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        context.Player.ClearClampExoskeletonInvincibility();
        // 玩家侧
        if (GameManager.HasEquipment(EquipmentIds.ClampExoskeleton) && context.Player.ClampExoskeletonRepairPending)
        {
            context.Player.ClearClampRepairPending();
            var heal = context.Player.MaxHealth - context.Player.Health;
            if (heal > 0)
            {
                var healed = BattleHealing.Apply(
                    context,
                    context.Player,
                    heal,
                    false,
                    new HealthChangeSource(HealthChangeSourceKind.Equipment, "钳制机械外骨骼", EquipmentIds.ClampExoskeleton, context.Player)).HealedAmount;
                context.RoundResult.AddLine(Localization.GetFmt("battle.equipment.clamp_full_heal_fmt", context.Player.DisplayName, healed));
                context.AddTriggerLog("[Equipment/ClampExoskeleton]");
                context.AddTriggerLog($"修复：{context.Player.DisplayName} +{healed} HP，满血。");
            }
        }

        // 敌方侧
        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            enemy.RuntimeStates["clamp_exo_invincible_this_round"] = false;
            if (enemy.IsDead || !enemy.HasEquipment(EquipmentIds.ClampExoskeleton))
            {
                continue;
            }

            if (!enemy.RuntimeStates.TryGetValue("clamp_exo_repair_pending", out var pendingVal) || pendingVal is not true)
            {
                continue;
            }

            enemy.RuntimeStates["clamp_exo_repair_pending"] = false;
            var heal = enemy.MaxHealth - enemy.Health;
            if (heal > 0)
            {
                var healed = BattleHealing.Apply(
                    context,
                    enemy,
                    heal,
                    false,
                    new HealthChangeSource(HealthChangeSourceKind.Equipment, "钳制机械外骨骼", EquipmentIds.ClampExoskeleton, enemy)).HealedAmount;
                context.RoundResult.AddLine(Localization.GetFmt("battle.equipment.clamp_full_heal_fmt", enemy.DisplayName, healed));
                context.AddTriggerLog("[Equipment/ClampExoskeleton]");
                context.AddTriggerLog($"修复：{enemy.DisplayName} +{healed} HP，满血。");
            }
        }
    }
}
