//////////////////////////////////////////////////////////
// 文件：Scripts/Damage/IceKillFreezeEffect.cs
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

// 冰杀命中后施加【冰冻】Buff。
// OnDamageTaken/Low：在 ApplyDamageEffect（Mid）结算后、DamageTakenEffect（Low）之前运行。
// 观星重复阶段免疫冰冻（已被强制锁定出牌，再施加冻结无意义且与规则矛盾）。
/// <summary>
/// Damage System 的公开类：IceKillFreezeEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class IceKillFreezeEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Damage System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.AttackType != CardType.IceKill) return;
        if (damage.Amount <= 0 || !damage.IsResolved) return;

        ApplyFreeze(context, damage.Target);
    }

    /// <summary>
    /// Damage System 的程序集内部入口：ApplyFreeze。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    internal static void ApplyFreeze(BattleContext context, Player target)
    {
        // 观星重复阶段免疫冰冻。
        if (target.GuanxingPhase == GuanxingPhase.Repeating) return;
        if (target.IsCombatDebuffImmune)
        {
            context.AddTriggerLog($"[泉水精华] {target.DisplayName}免疫冰冻。");
            return;
        }
        // 冰冻不叠加：若已冻结则刷新。
        target.SetFrozen(1);
        context.RoundResult.AddLine($"{target.DisplayName}【冰冻】：下回合只能出费。");
        context.AddTriggerLog($"[冰杀] {target.DisplayName} 冰冻1回合。");
    }
}
