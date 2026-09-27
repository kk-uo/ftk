//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/ZiBaoSkill.cs
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

public sealed class ZiBaoEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDying;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null)
        {
            return;
        }

        var enemy = damage.Target as EnemyInstance;
        if (enemy == null || !enemy.HasSkill(SkillIds.ZiBao) || enemy.Health > 0)
        {
            return;
        }

        // 自爆每场战斗只触发一次。爆炸释放后保留该标记，后续濒死回到正常死亡流程。
        if (enemy.RuntimeStates.TryGetValue("zibao_triggered", out var triggered) && triggered is true)
        {
            return;
        }

        // 拦截第一次死亡：设置自爆待发标志，HP恢复至1，阻止DyingEffect继续。
        enemy.RuntimeStates["zibao_pending"] = true;
        enemy.DebugSetHealth(1);
        context.RoundResult.AddLine($"【自爆】触发：{enemy.DisplayName}生命恢复至1，下回合释放爆炸攻击！");
        context.AddTriggerLog("[自爆]");
        context.AddTriggerLog($"{enemy.DisplayName} 自爆首次濒死：HP恢复至1，下回合爆炸。");
    }
}
