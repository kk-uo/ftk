//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/LuoyiSkill.cs
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

// 裸衣的攻击穿透与“攻击视为出费”由 BattleResolver / DefenseBeforeDamageEffect
// 在行动与防御关系层处理；这里仅处理“本回合使用攻击牌时受到伤害×2”的代价。
/// <summary>
/// Skill System 的公开类：LuoyiVulnerableEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class LuoyiVulnerableEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        var playerAction = context.PlayerAction;
        if (damage == null || damage.Cancelled || damage.Target != context.Player)
        {
            return;
        }

        if (!context.Player.HasSkill(SkillIds.Luoyi)
            || playerAction is not { IsAttack: true }
            || context.PlayerActionCancelled)
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "裸衣代价",
            DamageModifierPriority.VulnerableOrReduction,
            DamageModifierOperation.Multiply,
            2));
        context.RoundResult.AddLine($"{damage.Target.DisplayName}【裸衣代价】：本回合使用{BattleRules.GetCardName(playerAction.Type)}，受到伤害翻倍。");
        context.AddTriggerLog("[裸衣]");
        context.AddTriggerLog("Trigger: OnDamage");
        context.AddTriggerLog("Priority: Lowest");
        context.AddTriggerLog($"{damage.Target.DisplayName}裸衣代价：本回合攻击，受到伤害×2。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.Luoyi, Timing, Priority, "vulnerable");
    }
}
