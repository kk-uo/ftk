//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/ConductorEquipment.cs
//
// 模块：Equipment System
//
// 职责：
// 1. 承载装备触发效果与装备战斗逻辑相关代码。
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

// 导体（史诗护甲）：持有者所受Thunder（雷属性）伤害-75%。
// 实现：在 OnDamage/Mid 时添加 MultiplyFloat=0.25 的修饰符，使最终伤害减至25%，
// 保持 damage.Cancelled=false，允许【黄天】等监听Thunder事件的技能继续触发。
/// <summary>
/// Equipment System 的公开类：ConductorEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ConductorEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled) return;
        if (!damage.DamageType.HasFlag(DamageType.Thunder)) return;

        bool targetHasConductor;
        if (damage.Target == context.Player)
        {
            targetHasConductor = GameManager.HasEquipment(EquipmentIds.Conductor);
        }
        else if (damage.Target is EnemyInstance enemyTarget)
        {
            targetHasConductor = enemyTarget.HasEquipment(EquipmentIds.Conductor);
        }
        else
        {
            return;
        }

        if (!targetHasConductor) return;

        damage.AddModifier(new DamageModifier(
            "导体（雷伤-75%）",
            DamageModifierPriority.VulnerableOrReduction,
            DamageModifierOperation.MultiplyFloat,
            0.25));
        context.RoundResult.AddLine($"导体触发（{damage.Target.DisplayName}）：Thunder伤害×25%（黄天仍可触发）。");
        context.AddTriggerLog("[导体]");
        context.AddTriggerLog($"导体：{damage.Target.DisplayName}本次Thunder伤害削减75%。");
    }
}
