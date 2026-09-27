//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/QianXinSkill.cs
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

// 虔信（通用·普通）：持有者受到Thunder伤害时，获得2层【亢奋】Buff并回复5点生命值。
// 无触发次数限制，每次Thunder伤害事件独立触发（即使伤害被Conductor归零，只要未Cancelled就触发）。
//
// QianXinEffect（OnDamageTaken, Low）：
//   检查 target 是否持有虔信 && DamageType 包含 Thunder && !damage.Cancelled
//   → 给 target +2 亢奋层 + Heal 5（经 OnHeal 链，万殺会抵消回血）。
//
// FrenzyDamageEffect（OnDamage, Mid）：
//   若 source 有亢奋层 && 当前为攻击牌 → 添加 ×1.5^stacks 的 MultiplyFloat 修正。
//
// FrenzyDecayEffect（OnTurnEnd, Mid）：
//   玩家和所有存活敌人的亢奋层各减少1（下限0）。
//
// FrenzyBuffHelper：RuntimeStates 键 "frenzy_stacks"（int）的读写工具。

/// <summary>
/// Skill System 的公开类：QianXinEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class QianXinEffect : IBattleEffect
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
        if (context.GameOver) return;
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled) return;
        if (!damage.DamageType.HasFlag(DamageType.Thunder)) return;

        var target = damage.Target;
        if (target.IsDead) return;

        bool targetHasQianXin;
        if (target == context.Player)
        {
            targetHasQianXin = context.Player.HasSkill(SkillIds.QianXin);
        }
        else if (target is EnemyInstance enemyTarget)
        {
            targetHasQianXin = enemyTarget.HasSkill(SkillIds.QianXin);
        }
        else
        {
            return;
        }

        if (!targetHasQianXin) return;

        FrenzyBuffHelper.AddStacks(target, 2);
        var totalStacks = FrenzyBuffHelper.GetStacks(target);
        context.RoundResult.AddLine($"【虔信】：{target.DisplayName}受到Thunder伤害，获得2层【亢奋】（当前{totalStacks}层）。");
        context.AddTriggerLog("[虔信]");
        context.AddTriggerLog($"虔信：{target.DisplayName} 亢奋+2 → {totalStacks}层。");

        if (!target.IsDead && !context.GameOver)
        {
            var healed = BattleHealing.Apply(
                context,
                target,
                5,
                false,
                new HealthChangeSource(HealthChangeSourceKind.Skill, "虔信", SkillIds.QianXin, target)).HealedAmount;
            context.RoundResult.AddLine($"【虔信】：{target.DisplayName}回复{healed}点生命值（{target.Health}/{target.MaxHealth}）。");
            context.AddTriggerLog($"虔信：{target.DisplayName} 回血+{healed} → {target.Health}/{target.MaxHealth}");

        }
    }
}

/// <summary>
/// Skill System 的公开类：FrenzyDamageEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class FrenzyDamageEffect : IBattleEffect
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
        if (!BattleRules.IsAnyAttackCard(damage.AttackType)) return;

        var source = damage.Source;
        if (source == null) return;

        var stacks = FrenzyBuffHelper.GetStacks(source);
        if (stacks <= 0) return;

        var multiplier = System.Math.Pow(1.5, stacks);
        damage.AddModifier(new DamageModifier(
            $"亢奋×{stacks}（×{multiplier:F2}）",
            DamageModifierPriority.SpecialMultiplier,
            DamageModifierOperation.MultiplyFloat,
            multiplier));

        context.RoundResult.AddLine($"【亢奋】：{source.DisplayName}有{stacks}层亢奋，攻击伤害×{multiplier:F2}。");
        context.AddTriggerLog("[亢奋]");
        context.AddTriggerLog($"亢奋：{source.DisplayName} {stacks}层 → ×{multiplier:F2}。");
    }
}

/// <summary>
/// Skill System 的公开类：FrenzyDecayEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class FrenzyDecayEffect : IBattleEffect
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
        var playerStacks = FrenzyBuffHelper.GetStacks(context.Player);
        if (playerStacks > 0)
        {
            FrenzyBuffHelper.AddStacks(context.Player, -1);
            context.AddTriggerLog($"亢奋衰减：{context.Player.DisplayName} {playerStacks} → {FrenzyBuffHelper.GetStacks(context.Player)}层。");
        }

        if (context.Encounter?.Enemies == null) return;
        foreach (var enemy in context.Encounter.Enemies)
        {
            if (enemy.IsDead) continue;
            var stacks = FrenzyBuffHelper.GetStacks(enemy);
            if (stacks > 0)
            {
                FrenzyBuffHelper.AddStacks(enemy, -1);
                context.AddTriggerLog($"亢奋衰减：{enemy.DisplayName} {stacks} → {FrenzyBuffHelper.GetStacks(enemy)}层。");
            }
        }
    }
}

/// <summary>
/// Skill System 的公开类：FrenzyBuffHelper。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class FrenzyBuffHelper
{
    private const string Key = "frenzy_stacks";

    /// <summary>
    /// Skill System 的公开入口：GetStacks。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetStacks(Player player) =>
        player.RuntimeStates.TryGetValue(Key, out var v) && v is int i ? i : 0;

    /// <summary>
    /// Skill System 的公开入口：AddStacks。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AddStacks(Player player, int delta)
    {
        var current = GetStacks(player);
        var next = System.Math.Max(0, current + delta);
        if (next == 0)
            player.RuntimeStates.Remove(Key);
        else
            player.RuntimeStates[Key] = next;
    }
}
