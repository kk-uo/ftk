//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/IronHeavyArmorEquipment.cs
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

// 铁卫重甲：每场战斗首次受到超过20点的单次伤害时，该伤害归零（仅触发一次）。
// 优先级高于鳞甲封顶：使用 ResolvePreCap 检查封顶前的真实伤害量，
// 避免被鳞甲先压缩到15后漏判（90→鳞甲→15 <= 20 = 错误不触发）。
// 触发时机：OnDamageTaken, Priority.Highest（在 ApplyDamageEffect.Mid 之前）。
/// <summary>
/// Equipment System 的公开类：IronHeavyArmorEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class IronHeavyArmorEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
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

        if (damage.Target == context.Player)
        {
            if (!GameManager.HasEquipment(EquipmentIds.IronHeavyArmor) || !context.Player.IronHeavyArmorActive)
            {
                return;
            }

            var preCap = damage.Modifiers.ResolvePreCap(damage.BaseAmount);
            if (preCap <= 20)
            {
                return;
            }

            damage.CancelAsFullyBlocked();
            context.Player.ConsumeIronHeavyArmor();
            context.RoundResult.AddLine($"铁卫重甲触发：受到{preCap}点伤害（超过20点），伤害归零。");
            context.AddTriggerLog("[Equipment/IronHeavyArmor]");
            context.AddTriggerLog($"铁卫重甲：玩家受到{preCap}点伤害，归零，效果消耗。");
        }
        else if (damage.Target is EnemyInstance enemyTarget)
        {
            if (!enemyTarget.HasEquipment(EquipmentIds.IronHeavyArmor))
            {
                return;
            }

            if (!enemyTarget.RuntimeStates.TryGetValue("iron_heavy_armor_active", out var stateObj) || stateObj is not true)
            {
                return;
            }

            var preCap = damage.Modifiers.ResolvePreCap(damage.BaseAmount);
            if (preCap <= 20)
            {
                return;
            }

            damage.CancelAsFullyBlocked();
            enemyTarget.RuntimeStates["iron_heavy_armor_active"] = false;
            context.RoundResult.AddLine($"铁卫重甲触发（{enemyTarget.DisplayName}）：受到{preCap}点伤害（超过20点），伤害归零。");
            context.AddTriggerLog("[Equipment/IronHeavyArmor]");
            context.AddTriggerLog($"铁卫重甲：{enemyTarget.DisplayName}受到{preCap}点伤害，归零，效果消耗。");
        }
    }
}
