//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/ElectricShackleEquipment.cs
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

// 电击镣铐（普通增益饰品）：每回合开始时先对自身造成1点Thunder自伤（装备自伤，不触发血债血偿），
// 随后本回合承受的所有外部来源伤害降低10%。
//
// ElectricShackleEffect（OnTurnStart, Mid）：
//   1. 清除 electric_shackle_active 标志（确保自伤不享受减伤）
//   2. 通过完整伤害链对自身造成1点Thunder自伤（source==target，不触发血债血偿）
//   3. 设置 electric_shackle_active = true
//
// ElectricShackleReductionEffect（OnDamage, Mid）：
//   若 target 持有电击镣铐 && electric_shackle_active == true && source != target → 添加 ×0.9 减伤修正。

/// <summary>
/// Equipment System 的公开类：ElectricShackleEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ElectricShackleEffect : IBattleEffect
{
    private const string ActiveKey = "electric_shackle_active";

    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (GameManager.HasEquipment(EquipmentIds.ElectricShackle) && !context.Player.IsDead)
        {
            ApplySelfDamage(context, context.Player);
        }

        if (context.Encounter?.Enemies == null) return;
        foreach (var enemy in context.Encounter.Enemies)
        {
            if (!enemy.IsDead && enemy.HasEquipment(EquipmentIds.ElectricShackle))
            {
                ApplySelfDamage(context, enemy);
            }
        }
    }

    private static void ApplySelfDamage(BattleContext context, Player holder)
    {
        if (context.GameOver) return;

        holder.RuntimeStates.Remove(ActiveKey);

        context.RoundResult.AddLine($"【电击镣铐】{holder.DisplayName}受到1点Thunder自伤。");
        context.AddTriggerLog("[电击镣铐·自伤]");

        var savedDamage = context.DamageEvent;
        context.DamageEvent = new DamageEvent(holder, holder, CardType.LightningStrike, 1, overrideDamageType: DamageType.Thunder);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
        context.DamageEvent = savedDamage;

        if (!holder.IsDead)
        {
            holder.RuntimeStates[ActiveKey] = true;
            context.RoundResult.AddLine($"【电击镣铐】{holder.DisplayName}激活本回合外部伤害-10%减免。");
            context.AddTriggerLog($"电击镣铐：{holder.DisplayName}减伤状态激活。");
        }
    }
}

/// <summary>
/// Equipment System 的公开类：ElectricShackleReductionEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ElectricShackleReductionEffect : IBattleEffect
{
    private const string ActiveKey = "electric_shackle_active";

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
        if (damage.Source == damage.Target) return;

        Player? target = null;
        bool targetHasShackle;

        if (damage.Target == context.Player)
        {
            targetHasShackle = GameManager.HasEquipment(EquipmentIds.ElectricShackle);
            target = context.Player;
        }
        else if (damage.Target is EnemyInstance enemyTarget)
        {
            targetHasShackle = enemyTarget.HasEquipment(EquipmentIds.ElectricShackle);
            target = enemyTarget;
        }
        else
        {
            return;
        }

        if (!targetHasShackle || target == null) return;
        if (!(target.RuntimeStates.TryGetValue(ActiveKey, out var v) && v is true)) return;

        damage.AddModifier(new DamageModifier(
            "电击镣铐（减伤10%）",
            DamageModifierPriority.VulnerableOrReduction,
            DamageModifierOperation.MultiplyFloat,
            0.9));

        context.RoundResult.AddLine($"电击镣铐：{target.DisplayName}受到伤害-10%。");
        context.AddTriggerLog("[电击镣铐·减伤]");
        context.AddTriggerLog($"电击镣铐减伤：{target.DisplayName} 本次伤害×0.9。");
    }
}
