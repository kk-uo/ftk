//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/VampiricFangEquipment.cs
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

// 吸血之牙（史诗·武器）
// 每回合首次主动攻击造成伤害后，回复等同于本次伤害25%的生命值（向下取整）。
// 仅统计主动攻击造成的实际伤害：毒伤、Buff持续伤害、反伤、非主动的真实伤害、间接伤害均不触发。

/// <summary>
/// Equipment System 的公开类：VampiricFangHealEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class VampiricFangHealEffect : IBattleEffect
{
    private const string TriggeredThisTurnKey = "vampiric_fang_triggered";

    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.ActualDamageDealt <= 0 || !damage.IsDirectAttackDamage)
        {
            return;
        }

        // 只统计主动攻击牌造成的伤害：毒/持续/反伤/非主动真实伤害/间接伤害都不是"攻击牌"，天然被这条排除。
        if (!BattleRules.IsAnyAttackCard(damage.AttackType))
        {
            return;
        }

        var owner = damage.Source;
        var hasFang = owner is EnemyInstance enemyOwner
            ? enemyOwner.HasEquipment(EquipmentIds.VampiricFang)
            : GameManager.HasEquipment(EquipmentIds.VampiricFang);
        if (!hasFang)
        {
            return;
        }

        if (owner.RuntimeStates.TryGetValue(TriggeredThisTurnKey, out var used) && used is true)
        {
            return;
        }

        owner.RuntimeStates[TriggeredThisTurnKey] = true;

        var healAmount = damage.ActualDamageDealt / 4; // 25%，整数除法天然向下取整
        if (healAmount <= 0)
        {
            return;
        }

        var healing = BattleHealing.Apply(
            context,
            owner,
            healAmount,
            false,
            new HealthChangeSource(HealthChangeSourceKind.Equipment, "吸血之牙", EquipmentIds.VampiricFang, owner));
        if (healing.HealedAmount <= 0 && !healing.WasConverted)
        {
            return;
        }

        context.RoundResult.AddLine(healing.WasConverted
            ? $"{owner.DisplayName}【吸血之牙】触发：回复效果被毒丹转换。"
            : $"{owner.DisplayName}【吸血之牙】触发：回复{healing.HealedAmount}点生命（本次伤害{damage.ActualDamageDealt}的25%）。");
        context.AddTriggerLog("[Equipment/VampiricFang]");
        context.AddTriggerLog($"OnDamageTaken：伤害{damage.ActualDamageDealt}×25%→请求回复{healAmount}。");
    }
}

/// <summary>
/// Equipment System 的公开类：VampiricFangTurnStartEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class VampiricFangTurnStartEffect : IBattleEffect
{
    private const string TriggeredThisTurnKey = "vampiric_fang_triggered";

    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        context.Player.RuntimeStates.Remove(TriggeredThisTurnKey);
        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            enemy.RuntimeStates.Remove(TriggeredThisTurnKey);
        }
    }
}
