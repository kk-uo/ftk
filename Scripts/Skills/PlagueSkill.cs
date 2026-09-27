//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/PlagueSkill.cs
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

// 瘟疫技能系统
// PlagueAddEffect（OnTurnStart, Low）：持有者每回合开始给所有对手追加1层瘟疫。
//   达到10层时立即发作：真实毒素伤害10点，清除本次触发的10层。
// PlagueHealEffect（OnHeal, Low）：受治疗角色清除5层瘟疫叠加。
/// <summary>
/// Skill System 的公开类：PlagueAddEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class PlagueAddEffect : IBattleEffect
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
        var enemies = context.Encounter?.Enemies;
        if (enemies == null) return;

        // 敌人持有瘟疫技能 → 对玩家叠加
        foreach (var enemy in enemies)
        {
            if (enemy.IsDead) continue;
            if (!enemy.HasSkill(SkillIds.Plague)) continue;
            AddPlagueStack(context.Player, enemy, context);
        }

        // 玩家持有瘟疫技能 → 对所有存活敌人叠加
        if (context.Player.HasSkill(SkillIds.Plague))
        {
            foreach (var enemy in enemies)
            {
                if (enemy.IsDead) continue;
                AddPlagueStack(enemy, context.Player, context);
            }
        }
    }

    private static void AddPlagueStack(Player target, Player source, BattleContext context)
    {
        if (target.IsCombatDebuffImmune)
        {
            context.AddTriggerLog($"[泉水精华] {target.DisplayName}免疫瘟疫。");
            return;
        }

        target.RuntimeStates.TryGetValue("plague_stacks", out var existing);
        var prev = existing is int n ? n : 0;
        var next = prev + 1;
        target.RuntimeStates["plague_stacks"] = next;

        context.RoundResult.AddLine($"瘟疫：{target.DisplayName}瘟疫叠加至{next}层。");
        context.AddTriggerLog($"[Skill/Plague] {target.DisplayName} plague_stacks={next}");

        if (next < 10) return;

        target.RuntimeStates["plague_stacks"] = next - 10;
        var healthBefore = target.Health;
        target.TakeDamage(10);
        context.RecordDirectDamage(target, 10, System.Math.Max(0, healthBefore - target.Health), healthBefore, target.Health,
            new HealthChangeSource(HealthChangeSourceKind.Skill, "瘟疫", SkillIds.Plague, source));
        context.RoundResult.AddLine($"瘟疫发作：{target.DisplayName}受到10点毒素真实伤害（剩余{next - 10}层）。");
        context.AddTriggerLog($"[Skill/Plague] 发作 {target.DisplayName} -10HP poison");

        if (target.Health <= 0 && !target.IsDead && !context.GameOver)
        {
            var savedDamage = context.DamageEvent;
            context.DamageEvent = new DamageEvent(source, target, CardType.Kill, 10, overrideDamageType: DamageType.Poison,
                origin: new HealthChangeSource(HealthChangeSourceKind.Skill, "瘟疫", SkillIds.Plague, source));
            context.RaiseOnDying();
            context.DamageEvent = savedDamage;
        }
    }
}

/// <summary>
/// Skill System 的公开类：PlagueHealEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class PlagueHealEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnHeal;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var healed = context.HealEvent?.Healer;
        if (healed == null) return;

        if (!healed.RuntimeStates.TryGetValue("plague_stacks", out var val) || val is not int stacks || stacks <= 0)
            return;

        var removed = System.Math.Min(stacks, 5);
        healed.RuntimeStates["plague_stacks"] = stacks - removed;

        context.RoundResult.AddLine($"治疗消散瘟疫：{healed.DisplayName}清除{removed}层瘟疫（剩余{stacks - removed}层）。");
        context.AddTriggerLog($"[Skill/Plague] heal removed {removed} stacks from {healed.DisplayName}");
    }
}
