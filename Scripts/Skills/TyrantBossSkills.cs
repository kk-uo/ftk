//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/TyrantBossSkills.cs
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
/// Skill System 的公开类：BaoNueEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BaoNueEffect : IBattleEffect
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
        if (damage == null || damage.Cancelled || damage.Amount <= 0)
        {
            return;
        }

        if (!IsQunFaction(context, damage.Source))
        {
            return;
        }

        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            if (enemy.IsDead || enemy == damage.Source || !enemy.HasSkill(SkillIds.BaoNue))
            {
                continue;
            }

            var key = $"baonue_trigger_turn_{context.TurnCounter}";
            if (enemy.RuntimeStates.ContainsKey(key))
            {
                continue;
            }

            enemy.RuntimeStates[key] = true;
            enemy.GainMana(1);
            context.RoundResult.AddLine($"{enemy.DisplayName}【暴虐】生效：获得1费。");
            context.AddTriggerLog("[暴虐]");
            context.AddTriggerLog($"{damage.Source.DisplayName}造成伤害后，{enemy.DisplayName}获得1费。");
        }
    }

    private static bool IsQunFaction(BattleContext context, Player unit)
    {
        if (unit is EnemyInstance enemy)
        {
            return enemy.Definition.Tags.Contains(EnemyTag.Qun);
        }

        var faction = unit == context.Player
            ? GameManager.CurrentCharacter?.Faction ?? unit.Character.Data.Faction
            : unit.Character.Data.Faction;
        return faction == Faction.Qun;
    }
}

/// <summary>
/// Skill System 的公开类：RouLinHelper。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class RouLinHelper
{
    /// <summary>
    /// Skill System 的公开入口：TryTrigger。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void TryTrigger(BattleContext context, DamageEvent damage)
    {
        if (damage.Source.IsDead
            || damage.Target.IsDead
            || !damage.Source.HasSkill(SkillIds.RouLin)
            || !BattleRules.IsAnyAttackCard(damage.AttackType))
        {
            return;
        }

        var key = $"roulin_trigger_turn_{context.TurnCounter}";
        if (damage.Source.RuntimeStates.ContainsKey(key))
        {
            return;
        }

        damage.Source.RuntimeStates[key] = true;
        var before = damage.Target.CurrentMana;
        BattleRules.PayManaAndRaiseResourceChanged(context, damage.Target, 0.5, false);
        var lost = Math.Max(0, before - damage.Target.CurrentMana);
        if (lost <= 0)
        {
            return;
        }

        context.RoundResult.AddLine($"{damage.Source.DisplayName}【肉林】生效：{damage.Target.DisplayName}失去{BattleRules.FormatMana(lost)}费。");
        context.AddTriggerLog("[肉林]");
        context.AddTriggerLog($"{damage.Source.DisplayName}攻击未命中，{damage.Target.DisplayName}失去{BattleRules.FormatMana(lost)}费。");
        context.ReportPlayerCharacterSkillTriggered(
            damage.Source, SkillIds.RouLin, TriggerTiming.OnBeforeDamage, EffectPriority.Low, "mana_loss");
    }
}
