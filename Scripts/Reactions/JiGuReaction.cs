//////////////////////////////////////////////////////////
// 文件：Scripts/Reactions/JiGuReaction.cs
//
// 模块：Reaction System
//
// 职责：
// 1. 承载实时反应窗口与响应式出牌相关代码。
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

/// <summary>
/// Reaction System 的公开类：JiGuReaction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class JiGuReaction : IDeferredDyingReaction
{
    /// <summary>
    /// Reaction System 的公开入口：JiGuReaction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public JiGuReaction(DamageEvent triggeringDamage)
    {
        TriggeringDamage = triggeringDamage;
        Target = triggeringDamage.Target;
        Options = new List<ReactionOption>
        {
            new("activate", Localization.Get("reaction.option.activate"), true, Activate, CardType.JiGuActivate),
            new("pass", Localization.Get("reaction.option.pass"), true, Pass)
        };
    }

    public string Id => ReactionIds.JiGu;
    public BattleUnit Target { get; }
    public DamageEvent TriggeringDamage { get; }
    public TriggerTiming Timing => TriggerTiming.OnBattlePostPhase;
    public EffectPriority Priority => EffectPriority.High;
    public string Title => Localization.Get("reaction.jigu.title");
    public List<ReactionOption> Options { get; }

    private static void Activate(BattleContext context)
    {
        context.Player.TriggerJiGu();
        context.Player.ActivateJiGu();
        context.RoundResult.AddLine("击鼓发动：进入击鼓状态（3回合），所有受到的伤害归零，造成伤害时恢复满血。");
        context.AddTriggerLog("[击鼓]");
        context.AddTriggerLog("玩家选择：发动击鼓");
        context.AddTriggerLog($"击鼓激活：{context.Player.DisplayName}进入无敌状态，剩余{context.Player.JiGuTurnsRemaining}计数。");
    }

    private static void Pass(BattleContext context)
    {
        context.Player.ClearJiGuReactionPending();
        context.RoundResult.AddLine("放弃击鼓。");
        context.AddTriggerLog("[击鼓]");
        context.AddTriggerLog("玩家选择：放弃");
    }
}
