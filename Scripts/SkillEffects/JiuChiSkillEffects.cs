//////////////////////////////////////////////////////////
// 文件：Scripts/SkillEffects/JiuChiSkillEffects.cs
//
// 模块：Skill Effect System
//
// 职责：
// 1. 承载技能效果实现与 Trigger 接入相关代码。
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

// 酒池（董卓专属·传奇）：
// 每回合开始时，将 FreeWineUsesRemaining 重置为3。
// 实际免费结算在 BattleResolver.PayActionCost 中完成。
/// <summary>
/// Skill Effect System 的公开类：JiuChiTurnStartEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class JiuChiTurnStartEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        Reset(context, context.Player);
        var enemies = context.Encounter?.Enemies;
        if (enemies == null) return;
        foreach (var enemy in enemies)
            Reset(context, enemy);
    }

    private static void Reset(BattleContext context, Player unit)
    {
        if (!unit.HasSkill(SkillIds.JiuChi)) return;
        unit.ResetFreeWineUses();
        context.AddTriggerLog("[酒池]");
        context.AddTriggerLog($"{unit.DisplayName} 酒池：本回合免费酒次数重置为3。");
        context.ReportPlayerCharacterSkillTriggered(
            unit, SkillIds.JiuChi, TriggerTiming.OnTurnStart, EffectPriority.High, "reset");
    }
}
