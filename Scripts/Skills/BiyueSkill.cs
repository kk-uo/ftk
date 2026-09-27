//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/BiyueSkill.cs
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

// 闭月：第1回合结束获得1费，之后每2回合获得1费（独立计数，不受TurnCounter影响）。
// biyue_ticks 存储在 RuntimeStates，战斗开始自动清零；奇数时触发。
/// <summary>
/// Skill System 的公开类：BiyueSkillEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BiyueSkillEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBattlePostPhase;
    public EffectPriority Priority => EffectPriority.Highest;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        ApplyBiyue(context, context.Player);
        var enemies = context.Encounter?.Enemies;
        if (enemies != null)
        {
            foreach (var enemy in enemies)
            {
                ApplyBiyue(context, enemy);
            }
        }
    }

    private static void ApplyBiyue(BattleContext context, Player player)
    {
        if (!player.HasSkill(SkillIds.Biyue) || player.IsDead || context.GameOver)
        {
            return;
        }

        player.RuntimeStates.TryGetValue("biyue_ticks", out var val);
        var ticks = (val is int n ? n : 0) + 1;
        player.RuntimeStates["biyue_ticks"] = ticks;

        if (ticks % 2 == 0) return;

        player.GainMana();
        context.ReportPlayerCharacterSkillTriggered(player, SkillIds.Biyue, TriggerTiming.OnBattlePostPhase, EffectPriority.Highest);
        context.AddTriggerLog("[闭月]");
        context.AddTriggerLog($"biyue_ticks={ticks} → {player.DisplayName}获得1费。");
    }
}
