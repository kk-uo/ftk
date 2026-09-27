//////////////////////////////////////////////////////////
// 文件：Scripts/Damage/NanmanMultiplierEffect.cs
//
// 模块：Damage System
//
// 职责：
// 1. 承载伤害事件、伤害修正与命中后效果相关代码。
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

public sealed class NanmanMultiplierEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Source != context.Player)
        {
            return;
        }

        if (damage.AttackType != CardType.NanmanInvasion)
        {
            return;
        }

        var multiplier = GameManager.NanmanDamageMultiplier;
        if (multiplier <= 1.0)
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "招募援军",
            DamageModifierPriority.SpecialMultiplier,
            DamageModifierOperation.MultiplyFloat,
            multiplier));
        context.RoundResult.AddLine($"招募援军：南蛮入侵伤害 ×{multiplier:0.##}。");
        context.AddTriggerLog("[招募援军]");
        context.AddTriggerLog($"南蛮入侵伤害 ×{multiplier:0.##}");
    }
}
