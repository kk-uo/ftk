//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/StinkyMushroomEquipment.cs
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

/// <summary>
/// 恶臭蘑菇·红（造成方向）：玩家造成的最终伤害×0.75。
/// 与 StinkyRedIncomingEffect 分别覆盖“造成”和“受到”两个方向，互不影响。
/// </summary>
public sealed class StinkyRedOutgoingEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
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

        if (!GameManager.HasEquipment(EquipmentIds.StinkyMushroomRed))
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "恶臭蘑菇·红（迟缓·造成）",
            DamageModifierPriority.ArmorEquipmentMultiplier,
            DamageModifierOperation.MultiplyFloat,
            0.75));
        context.AddTriggerLog("[恶臭蘑菇·红] 造成伤害×0.75。");
    }
}

/// <summary>
/// 恶臭蘑菇·红（承受方向）：玩家受到的最终伤害×0.75。
/// </summary>
public sealed class StinkyRedIncomingEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Target != context.Player)
        {
            return;
        }

        if (!GameManager.HasEquipment(EquipmentIds.StinkyMushroomRed))
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "恶臭蘑菇·红（迟缓·承受）",
            DamageModifierPriority.ArmorEquipmentMultiplier,
            DamageModifierOperation.MultiplyFloat,
            0.75));
        context.AddTriggerLog("[恶臭蘑菇·红] 受到伤害×0.75。");
    }
}

/// <summary>
/// 恶臭蘑菇·绿：所有角色（无论是玩家还是敌人，无论是造成方还是承受方）受到的最终伤害×1.25。
/// 不区分 Source/Target，只要玩家持有该装备，本场战斗中任何一次伤害结算都会叠加该乘区。
/// </summary>
public sealed class StinkyGreenEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Low;

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

        if (!GameManager.HasEquipment(EquipmentIds.StinkyMushroomGreen))
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "恶臭蘑菇·绿（腐蚀）",
            DamageModifierPriority.ArmorEquipmentMultiplier,
            DamageModifierOperation.MultiplyFloat,
            1.25));
        context.AddTriggerLog("[恶臭蘑菇·绿] 受到伤害×1.25。");
    }
}

/// <summary>
/// 恶臭蘑菇·黄：每场战斗开始时（第一回合），玩家与所有敌人的初始费用都被强制设为-1。
/// 必须早于冰蓝装甲（High）/蜀·先机（Lowest）等所有"战斗开始加费"效果结算，
/// 因此使用 EffectPriority.Immediate——同一个 OnBattlePrePhase timing 内最先执行；
/// 注册顺序紧跟 BattlePrePhaseEffect（负责 context.BeginRoundResult()，同样是 Immediate），
/// 保证 RoundResult 已初始化后再写入战报文本。
/// </summary>
public sealed class StinkyYellowInitialManaOverrideEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBattlePrePhase;
    public EffectPriority Priority => EffectPriority.Immediate;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.TurnNumber != 1)
        {
            return;
        }

        if (!GameManager.HasEquipment(EquipmentIds.StinkyMushroomYellow))
        {
            return;
        }

        context.Player.GainMana(-1 - context.Player.CurrentMana);
        context.RoundResult.AddLine("【恶臭蘑菇·黄】：你的初始费用被强制设为-1。");
        context.AddTriggerLog("[恶臭蘑菇·黄] 玩家初始费用→-1。");

        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            if (enemy.IsDead)
            {
                continue;
            }

            enemy.GainMana(-1 - enemy.CurrentMana);
            context.AddTriggerLog($"[恶臭蘑菇·黄] {enemy.DisplayName} 初始费用→-1。");
        }
    }
}
