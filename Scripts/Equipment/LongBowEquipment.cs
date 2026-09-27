//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/LongBowEquipment.cs
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

// 长弓（普通）：本场战斗第一张普通杀无视敌方闪，且造成双倍伤害。
//
// 实现分两个效果：
//   LongBowBeforeDamageEffect（OnBeforeDamage, Highest，注册在 DefenseBeforeDamageEffect 之前）：
//     设置 context.IgnoreDefenderDodgeForThisAttack = true，由 TryConsumeDodge 读取并自动清除。
//   LongBowDamageBonusEffect（OnDamage, High）：
//     添加 ×2 特殊乘数并标记已使用。

/// <summary>
/// Equipment System 的公开类：LongBowBeforeDamageEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class LongBowBeforeDamageEffect : IBattleEffect
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
        if (damage == null || damage.Cancelled)
        {
            return;
        }

        if (damage.AttackType != CardType.Kill
            || damage.Source != context.Player
            || !GameManager.HasEquipment(EquipmentIds.LongBow)
            || context.Player.LongBowUsed)
        {
            return;
        }

        context.IgnoreDefenderDodgeForThisAttack = true;
        context.RoundResult.AddLine("长弓：首张普通杀无视闪。");
        context.AddTriggerLog("[Equipment/LongBow]");
        context.AddTriggerLog("长弓：闪无视激活。");
    }
}

/// <summary>
/// Equipment System 的公开类：LongBowDamageBonusEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class LongBowDamageBonusEffect : IBattleEffect
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
        if (damage == null || damage.Cancelled)
        {
            return;
        }

        if (damage.AttackType != CardType.Kill
            || damage.Source != context.Player
            || !GameManager.HasEquipment(EquipmentIds.LongBow)
            || context.Player.LongBowUsed)
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "长弓双倍",
            DamageModifierPriority.SpecialMultiplier,
            DamageModifierOperation.Multiply,
            2));
        if (context.IsDamagePreview)
        {
            return;
        }

        context.Player.ConsumeLongBow();
        context.RoundResult.AddLine("长弓：首张普通杀伤害×2。");
        context.AddTriggerLog("[Equipment/LongBow]");
        context.AddTriggerLog("长弓：伤害×2，效果已消耗。");
    }
}
