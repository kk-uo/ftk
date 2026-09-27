//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/LongdanSkill.cs
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

public sealed class LongdanReactionEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBattlePostPhase;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        // 龙胆触发条件：赵云打出【闪】并且当前存在攻击性敌方行动。
        // 不检查闪是否最终成功挡住伤害——只要出闪响应攻击即触发，无论：
        //   • 攻击被无懈可击抵消（双方无伤）
        //   • 闪成功（受到0伤害）
        //   • 闪失败（如雷杀、必中杀穿透）
        //   • 万箭齐发被闪挡住
        // 均视为"成功发动龙胆"。
        if (!context.Player.HasSkill(SkillIds.Longdan)
            || context.GameOver
            || context.Player.IsDead
            || context.PlayerAction?.IsDodge != true
            || !LongdanReaction.HasTriggeringEnemyAction(context))
        {
            return;
        }

        context.Reactions.Enqueue(new LongdanReaction(context.Player.CurrentMana));
        context.ReportPlayerCharacterSkillTriggered(context.Player, SkillIds.Longdan, Timing, Priority, "reaction_window");
        context.AddTriggerLog("[龙胆]");
        context.AddTriggerLog("Trigger: OnBattlePostPhase");
        context.AddTriggerLog("Priority: High");
        context.AddTriggerLog("ReactionWindow触发：龙胆");
        context.AddTriggerLog("[Reaction]");
        context.AddTriggerLog("LongDan Triggered");
        context.AddTriggerLog($"Current Cost: {BattleRules.FormatMana(context.Player.CurrentMana)}");
        context.AddTriggerLog("Available:");
        foreach (var cardType in LongdanReaction.GetAvailableCardTypes(context.Player.CurrentMana))
        {
            context.AddTriggerLog($"- {BattleRules.GetCardName(cardType)}");
        }
    }
}
