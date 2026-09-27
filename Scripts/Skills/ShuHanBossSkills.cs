//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/ShuHanBossSkills.cs
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

using System.Collections.Generic;

// ────────────────────────────────────────────────────────────────────────────
// 蜀汉共生体 Boss 技能效果（仁德 / 桃园结义 / 武圣 / 义绝 / 咆哮）
// 以及 Boss 专属武器效果（丈八蛇矛 / 青龙偃月刀）
// ────────────────────────────────────────────────────────────────────────────

// 仁德：每回合开始时，刘备刷新2层本回合护盾。
// 护盾在 RendeShieldAbsorbEffect（OnBeforeDamage/High）中逐次消耗。
/// <summary>
/// Skill System 的公开类：RendeShieldReplenishEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RendeShieldReplenishEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.Encounter == null) return;
        foreach (var enemy in context.Encounter.Enemies)
        {
            if (enemy.IsDead || !enemy.HasSkill(SkillIds.Rende)) continue;
            var baseAdd = GetRendeShieldGain(enemy);
            enemy.RuntimeStates["rende_shield"] = baseAdd;
            context.RoundResult.AddLine($"仁德：{enemy.DisplayName}获得{baseAdd}层本回合护盾。");
            context.AddTriggerLog($"[仁德] {enemy.DisplayName}本回合护盾刷新为{baseAdd}层。");
        }
    }

    private static int GetRendeShieldGain(EnemyInstance enemy)
    {
        return 2;
    }
}

// 仁德护盾吸收：当持有仁德技能的敌人被攻击时，一层抵挡一次完整伤害。
/// <summary>
/// Skill System 的公开类：RendeShieldAbsorbEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RendeShieldAbsorbEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.OnlyAllowCounterOrCardShields) return;
        var target = damage.Target as EnemyInstance;
        if (target == null || !target.HasSkill(SkillIds.Rende)) return;
        if (!target.RuntimeStates.TryGetValue("rende_shield", out var shieldObj)) return;
        var shield = (int)shieldObj;
        if (shield <= 0) return;

        if (damage.BaseAmount <= 0) return;

        target.RuntimeStates["rende_shield"] = shield - 1;
        damage.CancelAsFullyBlocked();
        context.RoundResult.AddLine($"仁德护盾：抵挡本次伤害（剩余{shield - 1}层）。");
        context.AddTriggerLog($"[仁德护盾] 抵挡一次完整伤害，消耗1层，剩余{shield - 1}层。");

        // 如影随行：仁德护盾完全抵消玩家伤害时，触发黄月英+1费。
        if (ReferenceEquals(damage.Source, context.Player))
            context.PlayerAttackAbsorbedByShield = true;
    }
}

// 桃园结义：共享生命池首次降至150以下时，恢复100点生命。
/// <summary>
/// Skill System 的公开类：TaoyuanJiyiEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class TaoyuanJiyiEffect : IBattleEffect
{
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
        if (damage == null || damage.Cancelled || damage.Amount <= 0) return;
        var target = damage.Target as EnemyInstance;
        if (target == null || target.SharedPool == null) return;
        if (context.Encounter == null) return;

        EnemyInstance? liuBei = null;
        foreach (var e in context.Encounter.Enemies)
        {
            if (e.HasSkill(SkillIds.TaoyuanJiyi)) { liuBei = e; break; }
        }
        if (liuBei == null || liuBei.IsDead) return;

        if (liuBei.RuntimeStates.TryGetValue("taoyuan_triggered", out var flag) && (bool)flag) return;
        if (target.SharedPool.CurrentHP >= 150) return;

        liuBei.RuntimeStates["taoyuan_triggered"] = true;
        var healed = target.SharedPool.Heal(100);
        context.RoundResult.AddLine($"桃园结义：三兄弟齐心，共享生命池恢复{healed}点！");
        context.AddTriggerLog($"[桃园结义] 共享池回复{healed}。");
    }
}

// 武圣：关羽的杀系攻击固定+5伤害。
/// <summary>
/// Skill System 的公开类：WushengEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class WushengEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled) return;
        if (!BattleRules.IsShaAttack(damage.AttackType)) return;
        var source = damage.Source as EnemyInstance;
        if (source == null || !source.HasSkill(SkillIds.Wusheng)) return;

        damage.AddModifier(new DamageModifier(
            "武圣",
            DamageModifierPriority.FlatBonus,
            DamageModifierOperation.Add,
            5));
        context.AddTriggerLog("[武圣] Kill +5。");
    }
}

// 义绝：若玩家上回合受到过伤害，关羽本回合Kill伤害×2。
/// <summary>
/// Skill System 的公开类：YijueEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class YijueEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled) return;
        if (!BattleRules.IsShaAttack(damage.AttackType)) return;
        var source = damage.Source as EnemyInstance;
        if (source == null || !source.HasSkill(SkillIds.Yijue)) return;
        if (!context.Player.RuntimeStates.TryGetValue("yijue_player_hit_last_round", out var v) || !(bool)v) return;

        damage.AddModifier(new DamageModifier(
            "义绝",
            DamageModifierPriority.SpecialMultiplier,
            DamageModifierOperation.Multiply,
            2));
        context.RoundResult.AddLine($"义绝：{source.DisplayName}玩家上回合受伤，Kill伤害×2！");
        context.AddTriggerLog("[义绝] ×2。");
    }
}

// 义绝追踪：玩家受伤时标记；回合结束时将「本回合」标记转移为「上回合」。
/// <summary>
/// Skill System 的公开类：YijueTrackEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class YijueTrackEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Amount <= 0) return;
        if (damage.Target != context.Player) return;
        context.Player.RuntimeStates["yijue_player_hit_this_round"] = true;
    }
}

/// <summary>
/// Skill System 的公开类：YijueTurnShiftEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class YijueTurnShiftEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBattlePostPhase;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.Encounter == null) return;
        var hasYijue = false;
        foreach (var e in context.Encounter.Enemies)
        {
            if (e.HasSkill(SkillIds.Yijue)) { hasYijue = true; break; }
        }
        if (!hasYijue) return;

        var hitThisRound = context.Player.RuntimeStates.TryGetValue("yijue_player_hit_this_round", out var v) && (bool)v;
        context.Player.RuntimeStates["yijue_player_hit_last_round"] = hitThisRound;
        context.Player.RuntimeStates["yijue_player_hit_this_round"] = false;
    }
}

// 咆哮（伤害加成）：张飞每次杀系命中后永久+5基础伤害（本战斗内叠加）。
/// <summary>
/// Skill System 的公开类：PaoxiaoDamageEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class PaoxiaoDamageEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled) return;
        if (!BattleRules.IsShaAttack(damage.AttackType)) return;
        var source = damage.Source as EnemyInstance;
        if (source == null || !source.HasSkill(SkillIds.Paoxiao)) return;
        var stacks = source.RuntimeStates.TryGetValue("paoxiao_stacks", out var v) ? (int)v : 0;
        if (stacks <= 0) return;

        damage.AddModifier(new DamageModifier(
            $"咆哮（{stacks}层）",
            DamageModifierPriority.FlatBonus,
            DamageModifierOperation.Add,
            stacks * 5));
        context.AddTriggerLog($"[咆哮] +{stacks * 5}（{stacks}层）。");
    }
}

// 咆哮（层数叠加）：命中后增加层数。
/// <summary>
/// Skill System 的公开类：PaoxiaoStackEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class PaoxiaoStackEffect : IBattleEffect
{
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
        if (damage == null || damage.Cancelled || damage.Amount <= 0) return;
        if (!BattleRules.IsShaAttack(damage.AttackType)) return;
        var source = damage.Source as EnemyInstance;
        if (source == null || !source.HasSkill(SkillIds.Paoxiao)) return;

        var current = source.RuntimeStates.TryGetValue("paoxiao_stacks", out var v) ? (int)v : 0;
        source.RuntimeStates["paoxiao_stacks"] = current + 1;
        context.RoundResult.AddLine($"咆哮：命中层数+1（共{current + 1}层，下次Kill额外+{(current + 1) * 5}）。");
        context.AddTriggerLog($"[咆哮] 层数→{current + 1}。");
    }
}

// 丈八蛇矛（连续出杀）：连续两回合使用杀系攻击后，回合结束获得0.5费。
/// <summary>
/// Skill System 的公开类：ZhangBaSheMaoEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ZhangBaSheMaoEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBattlePostPhase;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.Encounter == null) return;
        foreach (var enemy in context.Encounter.Enemies)
        {
            if (enemy.IsDead || !enemy.HasEquipment(EquipmentIds.ZhangBaSheMao)) continue;

            var usedKillThisRound = enemy.RuntimeStates.TryGetValue("zhang_ba_kill_this_round", out var v1) && (bool)v1;
            var usedKillLastRound = enemy.RuntimeStates.TryGetValue("zhang_ba_kill_last_round", out var v2) && (bool)v2;

            if (usedKillThisRound && usedKillLastRound)
            {
                enemy.GainMana(0.5);
                context.RoundResult.AddLine($"丈八蛇矛：{enemy.DisplayName}连续出杀，获得0.5费。");
                context.AddTriggerLog($"[丈八蛇矛] +0.5费。");
            }

            enemy.RuntimeStates["zhang_ba_kill_last_round"] = usedKillThisRound;
            enemy.RuntimeStates["zhang_ba_kill_this_round"] = false;
        }
    }
}

// 丈八蛇矛（追踪）：当持有者使用杀系攻击命中时，标记本回合已出杀。
/// <summary>
/// Skill System 的公开类：ZhangBaSheMaoTrackEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ZhangBaSheMaoTrackEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Amount <= 0) return;
        if (!BattleRules.IsShaAttack(damage.AttackType)) return;
        var source = damage.Source as EnemyInstance;
        if (source == null || !source.HasEquipment(EquipmentIds.ZhangBaSheMao)) return;
        source.RuntimeStates["zhang_ba_kill_this_round"] = true;
    }
}

// 青龙偃月刀（闪避返费）：攻击被闪避时返还1费给持有者。
/// <summary>
/// Skill System 的公开类：QingLongYanYueDaoEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class QingLongYanYueDaoEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || !damage.Cancelled) return;
        if (!BattleRules.IsShaAttack(damage.AttackType)) return;
        var source = damage.Source as EnemyInstance;
        if (source == null || !source.HasEquipment(EquipmentIds.QingLongYanYueDao)) return;

        source.GainMana(1);
        context.RoundResult.AddLine($"青龙偃月刀：攻击被闪避，{source.DisplayName}返还1费。");
        context.AddTriggerLog("[青龙偃月刀] 闪避返还1费。");
    }
}
