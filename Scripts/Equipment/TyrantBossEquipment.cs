//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/TyrantBossEquipment.cs
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

public sealed class XianNiangEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnEnd;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        Apply(context, context.Player);
        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            Apply(context, enemy);
        }
    }

    private static void Apply(BattleContext context, Player unit)
    {
        if (!BattleRules.HasEquipment(unit, EquipmentIds.XianNiang))
        {
            return;
        }

        const string key = "xian_niang_zero_wine_used";
        if (!unit.RuntimeStates.TryGetValue(key, out var used) || used is not true)
        {
            return;
        }

        unit.RuntimeStates.Remove(key);
        unit.GainMana(1);
        context.RoundResult.AddLine($"{unit.DisplayName}【仙酿】生效：回合结束获得1费。");
        context.AddTriggerLog("[仙酿]");
        context.AddTriggerLog($"{unit.DisplayName}本回合使用过0费酒，回合结束获得1费。");
    }
}

/// <summary>
/// Equipment System 的公开类：TyrantCrownGuardEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class TyrantCrownGuardEffect : IBattleEffect
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
        if (damage == null || damage.Cancelled || damage.Target is not EnemyInstance targetEnemy || targetEnemy.IsDead)
        {
            return;
        }

        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            if (enemy.IsDead || enemy == targetEnemy || !enemy.HasEquipment(EquipmentIds.TyrantCrown))
            {
                continue;
            }

            damage.CancelAsFullyBlocked();
            context.RoundResult.AddLine($"暴虐皇冠生效：{targetEnemy.DisplayName}受到保护，本次伤害无效。");
            context.AddTriggerLog("[暴虐皇冠]");
            context.AddTriggerLog($"{enemy.DisplayName}存活时，队友{targetEnemy.DisplayName}免疫本次伤害。");
            return;
        }
    }
}

/// <summary>
/// Equipment System 的公开类：TyrantCrownVulnerableEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class TyrantCrownVulnerableEffect : IBattleEffect
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
        if (damage == null || damage.Cancelled || !BattleRules.HasEquipment(damage.Target, EquipmentIds.TyrantCrown))
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "暴虐皇冠",
            DamageModifierPriority.VulnerableOrReduction,
            DamageModifierOperation.Multiply,
            2));
        context.RoundResult.AddLine($"{damage.Target.DisplayName}【暴虐皇冠】生效：受到伤害×2。");
        context.AddTriggerLog("[暴虐皇冠]");
        context.AddTriggerLog($"{damage.Target.DisplayName}受到的伤害因暴虐皇冠翻倍。");
    }
}
