//////////////////////////////////////////////////////////
// 文件：Scripts/SkillEffects/AncestorBlessingEffect.cs
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

public sealed class AncestorBlessingDyingEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDying;
    public EffectPriority Priority => EffectPriority.Immediate;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var target = context.DamageEvent?.Target;
        if (target != context.Player || target.Health > 0 || context.GameOver)
        {
            return;
        }

        var stacks = RunBuffManager.CountStacks(RunBuffIds.AncestorBlessing);
        if (stacks <= 0)
        {
            return;
        }

        RunBuffManager.RemoveStacks(RunBuffIds.AncestorBlessing, 1);
        context.Player.DebugSetHealth(context.Player.MaxHealth);
        GameManager.IncrementAttackChipCount();

        var remaining = RunBuffManager.CountStacks(RunBuffIds.AncestorBlessing);
        context.RoundResult.AddLine($"先祖的赐福触发：玩家恢复至满生命，获得攻击芯片×1。剩余复活次数：{remaining}。");
        context.AddTriggerLog("[先祖的赐福]");
        context.AddTriggerLog($"OnDying优先触发：恢复至{context.Player.Health}/{context.Player.MaxHealth}，攻击芯片+1，层数 {stacks}->{remaining}。");
    }
}
