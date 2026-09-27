//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/ChangYinSkill.cs
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

// 畅饮（OnBattlePhase / Low）：持有者使用酒时，所有友方额外获得1层酒Buff（不含酒护盾）。
/// <summary>
/// Skill System 的公开类：ChangYinEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ChangYinEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBattlePhase;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        // 检查是否有持有畅饮技能的己方单位本回合使用了酒
        // ── 玩家方 ──
        if (context.PlayerAction != null
            && BattleRules.ShouldApplyWineEffect(context.Player, context.PlayerAction.Type)
            && !context.IsActionCancelled(context.Player)
            && context.Player.HasSkill(SkillIds.ChangYin))
        {
            // 玩家使用酒时，给玩家额外+1酒Buff（无护盾）
            context.Player.QueueWinePower(1);
            context.AddTriggerLog("[畅饮]");
            context.AddTriggerLog($"{context.Player.DisplayName}畅饮触发：自身额外获得1层酒Buff。");
        }

        // ── 敌方单位 ──
        foreach (var entry in context.EnemyActions)
        {
            if (!entry.Action.IsWine) continue;
            if (context.IsActionCancelled(entry.Enemy)) continue;
            if (!entry.Enemy.HasSkill(SkillIds.ChangYin)) continue;

            // 给所有同阵营友方（全体敌人列表）额外+1酒Buff，不加酒护盾
            foreach (var ally in context.EnemyActions)
            {
                if (ally.Enemy.IsDead) continue;
                ally.Enemy.QueueWinePower(1);
            }

            context.AddTriggerLog("[畅饮]");
            context.AddTriggerLog($"{entry.Enemy.DisplayName}畅饮触发：所有友方额外获得1层酒Buff。");
        }
    }
}
