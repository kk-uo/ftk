//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/ManzuWangSkill.cs
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

// 蛮族之王（OnDamage）：孟获南蛮入侵伤害 ×2。
/// <summary>
/// Skill System 的公开类：ManzuWangDamageEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ManzuWangDamageEffect : IBattleEffect
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
        if (damage == null || damage.Cancelled
            || damage.Source != context.Player
            || !context.Player.HasSkill(SkillIds.ManzuWang)
            || damage.AttackType != CardType.NanmanInvasion)
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "蛮族之王",
            DamageModifierPriority.SpecialMultiplier,
            DamageModifierOperation.MultiplyFloat,
            2.0));
        context.AddTriggerLog("[蛮族之王]");
        context.AddTriggerLog("南蛮入侵伤害 ×2。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.ManzuWang, Timing, Priority, "damage_bonus");
    }
}

// 蛮族之王（OnDamageTaken）：南蛮入侵每次命中后回复5生命和0.5费。
/// <summary>
/// Skill System 的公开类：ManzuWangHealOnHitEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ManzuWangHealOnHitEffect : IBattleEffect
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
        if (damage == null || damage.Cancelled || damage.Amount <= 0
            || damage.Source != context.Player
            || !context.Player.HasSkill(SkillIds.ManzuWang)
            || damage.AttackType != CardType.NanmanInvasion)
        {
            return;
        }

        BattleHealing.Apply(
            context,
            context.Player,
            5,
            false,
            new HealthChangeSource(HealthChangeSourceKind.Skill, "蛮族之王", SkillIds.ManzuWang, context.Player));
        context.Player.GainMana(0.5);
        context.RoundResult.AddLine($"蛮族之王：命中{damage.Target.DisplayName}，回复5生命和0.5费。");
        context.AddTriggerLog("[蛮族之王]");
        context.AddTriggerLog($"命中{damage.Target.DisplayName}：+5生命 +0.5费。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.ManzuWang, Timing, Priority, "on_hit");
    }
}
