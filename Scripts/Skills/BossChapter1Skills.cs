//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/BossChapter1Skills.cs
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

// 第一章 Boss 专属技能效果
// ==============================================
// 医者·强化状态（boss_healer_passive）
//   OnHeal: HP ≥ 120（含临时生命）→ 强化，改用必中杀，停止回血。
//   OnDamage: 强化期间杀系攻击基础伤害 +5。
//
// 背叛者·连击状态（boss_traitor_combo）
//   OnTurnEnd: 追踪连续攻击回合数；≥2回合 → 进入连击；连续2回合不攻击 → 退出
//   OnDamage: 连击期间普通/火/雷杀基础伤害 +5，日志"【背叛者】进入连击状态！"
//
// 不屈（budao）
//   OnDying: 第一次HP≤0 → 恢复至1HP，+2费，下回合全免伤，日志"【卖艺人】不屈触发！"
//   OnBeforeDamage: 不屈无敌期间取消所有伤害
//   OnTurnStart: 将待激活无敌转为生效
//   OnTurnEnd: 清除生效无敌标记
// ==============================================

// ── 医者·强化检测 ─────────────────────────────
public sealed class BossHealerEnhancedCheckEffect : IBattleEffect
{
    private const int HealthThreshold = 120;

    public TriggerTiming Timing => TriggerTiming.OnHeal;
    public EffectPriority Priority => EffectPriority.Mid;

    public void Execute(BattleContext context)
    {
        var heal = context.HealEvent;
        if (heal == null)
        {
            return;
        }

        var enemy = heal.Healer as EnemyInstance;
        if (enemy == null
            || !enemy.HasSkill(SkillIds.BossHealerPassive)
            || enemy.RuntimeStates.TryGetValue("healer_enhanced", out var enhanced) && enhanced is true
            || enemy.Health < HealthThreshold)
        {
            return;
        }

        enemy.RuntimeStates["healer_enhanced"] = true;
        context.RoundResult.AddLine($"【医者】神秘补剂已生效！生命达到{HealthThreshold}，进入强化状态：杀攻击变为必中杀，伤害+5。");
        context.AddTriggerLog("[医者·强化]");
        context.AddTriggerLog($"医者HP={enemy.Health}≥{HealthThreshold}，神秘补剂触发，进入强化状态。");
    }
}

// ── 医者·强化伤害加成 ─────────────────────────
public sealed class BossHealerEnhancedDamageEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.High;

    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.OnlyAllowCounterOrCardShields)
        {
            return;
        }

        var enemy = damage.Source as EnemyInstance;
        if (enemy == null
            || !enemy.HasSkill(SkillIds.BossHealerPassive)
            || !enemy.RuntimeStates.TryGetValue("healer_enhanced", out var enhanced)
            || enhanced is not true
            || !BattleRules.IsShaAttack(damage.AttackType))
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "医者强化",
            DamageModifierPriority.FlatBonus,
            DamageModifierOperation.Add,
            5));
        context.AddTriggerLog("[医者·强化] 攻击伤害 +5。");
    }
}

// ── 背叛者·连击追踪 ──────────────────────────
/// <summary>
/// Skill System 的公开类：BossTraitorComboEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BossTraitorComboEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnEnd;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        EnemyInstance? traitor = null;
        bool attackedThisTurn = false;

        // 找到背叛者并判断本回合是否出攻击牌
        foreach (var entry in context.EnemyActions)
        {
            if (!entry.Enemy.HasSkill(SkillIds.BossTraitorCombo)) continue;
            traitor = entry.Enemy;
            attackedThisTurn = entry.Action.IsAttack && !entry.ActionCancelled;
            break;
        }
        if (traitor == null || traitor.IsDead) return;

        traitor.RuntimeStates.TryGetValue("traitor_attack_streak", out var rawStreak);
        traitor.RuntimeStates.TryGetValue("traitor_no_attack_streak", out var rawNoStreak);
        traitor.RuntimeStates.TryGetValue("traitor_combo_active", out var rawCombo);
        int streak = rawStreak is int s ? s : 0;
        int noStreak = rawNoStreak is int ns ? ns : 0;
        bool comboActive = rawCombo is true;

        if (attackedThisTurn)
        {
            streak++;
            noStreak = 0;

            if (!comboActive && streak >= 2)
            {
                comboActive = true;
                context.RoundResult.AddLine("【背叛者】进入连击状态！杀伤害+5，偏向火/雷。");
                context.AddTriggerLog("[背叛者·连击] 连续攻击≥2回合，连击状态激活。");
            }
        }
        else
        {
            streak = 0;
            noStreak++;

            if (comboActive && noStreak >= 2)
            {
                comboActive = false;
                noStreak = 0;
                context.RoundResult.AddLine("【背叛者】连击状态解除。");
                context.AddTriggerLog("[背叛者·连击] 连续2回合未攻击，连击状态解除。");
            }
        }

        traitor.RuntimeStates["traitor_attack_streak"] = streak;
        traitor.RuntimeStates["traitor_no_attack_streak"] = noStreak;
        traitor.RuntimeStates["traitor_combo_active"] = comboActive;
    }
}

// ── 背叛者·连击伤害加成 ──────────────────────
/// <summary>
/// Skill System 的公开类：BossTraitorComboDamageEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BossTraitorComboDamageEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled) return;

        var enemy = damage.Source as EnemyInstance;
        if (enemy == null || !enemy.HasSkill(SkillIds.BossTraitorCombo)) return;
        if (!enemy.RuntimeStates.TryGetValue("traitor_combo_active", out var c) || c is not true) return;
        if (!BattleRules.IsShaAttack(damage.AttackType)) return;

        damage.AddModifier(new DamageModifier(
            "背叛者连击",
            DamageModifierPriority.FlatBonus,
            DamageModifierOperation.Add,
            5));
        context.AddTriggerLog("[背叛者·连击] 攻击伤害 +5。");
    }
}

// ── 不屈·死亡拦截 ───────────────────────────
/// <summary>
/// Skill System 的公开类：BuDaoEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BuDaoEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDying;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null) return;
        if (damage.Source == damage.Target) return;

        var enemy = damage.Target as EnemyInstance;
        if (enemy == null || !enemy.HasSkill(SkillIds.BuDao) || enemy.Health > 0) return;

        if (enemy.RuntimeStates.TryGetValue("budao_triggered", out var t) && t is true) return;

        enemy.RuntimeStates["budao_triggered"] = true;
        enemy.DebugSetHealth(1);
        enemy.GainMana(2);
        enemy.RuntimeStates["budao_invincible_pending"] = true;

        context.RoundResult.AddLine($"【{enemy.DisplayName}·不屈】：拦截死亡！恢复1生命，获得2费，下回合免疫所有伤害。");
        context.AddTriggerLog("[不屈]");
        context.AddTriggerLog($"不屈触发：{enemy.DisplayName} HP→1，+2费，下回合无敌。");
    }
}

// ── 不屈·回合开始激活无敌 ───────────────────
/// <summary>
/// Skill System 的公开类：BuDaoTurnStartEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BuDaoTurnStartEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var enemies = context.Encounter?.Enemies;
        if (enemies == null) return;
        foreach (var enemy in enemies)
        {
            if (!enemy.HasSkill(SkillIds.BuDao)) continue;
            if (!enemy.RuntimeStates.TryGetValue("budao_invincible_pending", out var p) || p is not true) continue;
            enemy.RuntimeStates.Remove("budao_invincible_pending");
            enemy.RuntimeStates["budao_invincible"] = true;
            context.RoundResult.AddLine($"【{enemy.DisplayName}·不屈】：本回合免疫所有伤害。");
            context.AddTriggerLog($"[不屈] {enemy.DisplayName} 进入不屈无敌状态。");
        }
    }
}

// ── 不屈·无敌拦截伤害 ──────────────────────
/// <summary>
/// Skill System 的公开类：BuDaoInvincibilityEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BuDaoInvincibilityEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.Immediate;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled) return;

        var enemy = damage.Target as EnemyInstance;
        if (enemy == null || !enemy.HasSkill(SkillIds.BuDao)) return;
        if (!enemy.RuntimeStates.TryGetValue("budao_invincible", out var v) || v is not true) return;

        damage.CancelAsFullyBlocked();
        context.AddTriggerLog($"[不屈] {enemy.DisplayName} 不屈无敌：免疫本次伤害。");
    }
}

// ── 不屈·回合结束清除无敌 ──────────────────
/// <summary>
/// Skill System 的公开类：BuDaoTurnEndEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BuDaoTurnEndEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnEnd;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var enemies = context.Encounter?.Enemies;
        if (enemies == null) return;
        foreach (var enemy in enemies)
        {
            if (!enemy.HasSkill(SkillIds.BuDao)) continue;
            if (!enemy.RuntimeStates.TryGetValue("budao_invincible", out var v) || v is not true) continue;
            enemy.RuntimeStates.Remove("budao_invincible");
            context.RoundResult.AddLine($"【{enemy.DisplayName}·不屈】：无敌状态结束。");
            context.AddTriggerLog($"[不屈] {enemy.DisplayName} 不屈无敌结束。");
        }
    }
}
