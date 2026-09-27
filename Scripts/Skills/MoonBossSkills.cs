//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/MoonBossSkills.cs
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

public sealed class MoonGazeEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.Highest;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            if (enemy.IsDead || !enemy.HasSkill(SkillIds.MoonGaze))
            {
                continue;
            }

            if (context.TurnCounter == 15)
            {
                context.Player.SetStun(1);
                context.RoundResult.AddLine("【月之凝视】生效：玩家获得【眩晕】。");
                context.AddTriggerLog("[月之凝视]");
                context.AddTriggerLog("第15回合：玩家获得眩晕1回合。");
            }

            if (context.TurnCounter == 49 && !context.Player.IsDead && !context.GameOver)
            {
                context.Player.DebugSetHealth(0);
                context.Player.MarkDead();
                context.Player.ClearStatuses();
                context.GameOver = true;
                context.Outcome = BattleOutcome.Defeat;
                context.GameOverText = Localization.Get("battle.gameover.defeat");
                context.RoundResult.AddLine("【月之凝视】生效：第49回合到来，机制杀触发，玩家立刻死亡。");
                context.AddTriggerLog("[月之凝视]");
                context.AddTriggerLog("第49回合：机制杀触发，跳过濒死/复活/无敌结算。");
            }
        }
    }
}
