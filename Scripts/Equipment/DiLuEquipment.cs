//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/DiLuEquipment.cs
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

public sealed class DiLuDamageImmunityEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.Highest;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Amount <= 0 || damage.OnlyAllowCounterOrCardShields)
        {
            return;
        }

        if (TryConsumeForPlayer(context, damage))
        {
            return;
        }

        TryConsumeForEnemy(context, damage);
    }

    private static bool TryConsumeForPlayer(BattleContext context, DamageEvent damage)
    {
        if (damage.Target != context.Player
            || !GameManager.HasEquipment(EquipmentIds.DiLu)
            || !context.Player.DiLuFirstDamageImmuneAvailable)
        {
            return false;
        }

        context.Player.ConsumeDiLuFirstDamageImmune();
        damage.CancelAsFullyBlocked();
        context.RoundResult.AddLine("的卢触发：免疫本场战斗中受到的第一次伤害。");
        context.AddTriggerLog("[Equipment/的卢]");
        context.AddTriggerLog("首次受到伤害被完全免疫；不会进入受到伤害后触发。");
        return true;
    }

    private static bool TryConsumeForEnemy(BattleContext context, DamageEvent damage)
    {
        if (damage.Target is not EnemyInstance enemy || !enemy.HasEquipment(EquipmentIds.DiLu))
        {
            return false;
        }

        if (enemy.RuntimeStates.TryGetValue("di_lu_damage_immunity_used", out var used) && used is true)
        {
            return false;
        }

        enemy.RuntimeStates["di_lu_damage_immunity_used"] = true;
        damage.CancelAsFullyBlocked();
        context.RoundResult.AddLine($"的卢触发：{enemy.DisplayName}免疫本场战斗中受到的第一次伤害。");
        context.AddTriggerLog("[Equipment/的卢]");
        context.AddTriggerLog($"{enemy.DisplayName}首次受到伤害被完全免疫；不会进入受到伤害后触发。");
        return true;
    }
}
