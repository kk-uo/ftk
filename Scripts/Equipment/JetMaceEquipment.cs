//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/JetMaceEquipment.cs
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

// 喷气式狼牙棒：每场战斗第一次攻击造成三倍伤害（仅触发一次）。
// 适用于全部攻击牌（杀系 + 万箭齐发 + 南蛮入侵）。
// 触发时机：OnDamage, Priority.High（在伤害修正入队时）。
/// <summary>
/// Equipment System 的公开类：JetMaceEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class JetMaceEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || !BattleRules.IsAnyAttackCard(damage.AttackType))
        {
            return;
        }

        if (damage.Source == context.Player)
        {
            if (!GameManager.HasEquipment(EquipmentIds.JetMace) || !context.Player.JetMaceActive)
            {
                return;
            }

            damage.AddModifier(new DamageModifier(
                "喷气式狼牙棒",
                DamageModifierPriority.SpecialMultiplier,
                DamageModifierOperation.Multiply,
                3));
            if (context.IsDamagePreview)
            {
                return;
            }

            context.Player.ConsumeJetMace();
            context.RoundResult.AddLine($"喷气式狼牙棒触发：本场战斗第一次攻击伤害×3。");
            context.AddTriggerLog("[Equipment/JetMace]");
            context.AddTriggerLog($"喷气式狼牙棒：{BattleRules.GetCardName(damage.AttackType)} ×3，效果消耗。");
        }
        else if (damage.Source is EnemyInstance enemySource)
        {
            if (!enemySource.HasEquipment(EquipmentIds.JetMace))
            {
                return;
            }

            if (!enemySource.RuntimeStates.TryGetValue("jet_mace_active", out var stateObj) || stateObj is not true)
            {
                return;
            }

            damage.AddModifier(new DamageModifier(
                "喷气式狼牙棒",
                DamageModifierPriority.SpecialMultiplier,
                DamageModifierOperation.Multiply,
                3));
            if (context.IsDamagePreview)
            {
                return;
            }

            enemySource.RuntimeStates["jet_mace_active"] = false;
            context.RoundResult.AddLine($"喷气式狼牙棒触发（{enemySource.DisplayName}）：本场战斗第一次攻击伤害×3。");
            context.AddTriggerLog("[Equipment/JetMace]");
            context.AddTriggerLog($"喷气式狼牙棒：{enemySource.DisplayName} {BattleRules.GetCardName(damage.AttackType)} ×3，效果消耗。");
        }
    }
}
