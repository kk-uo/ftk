//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/BigBoneClubEquipment.cs
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

public sealed class BigBoneClubDamageEffect : IBattleEffect
{
    private const int DamageBonus = 9;

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
        if (damage == null || damage.Cancelled || !BattleRules.IsShaAttack(damage.AttackType))
        {
            return;
        }

        if (!BattleRules.HasEquipment(damage.Source, EquipmentIds.BigBoneClub))
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "大骨棒",
            DamageModifierPriority.FlatBonus,
            DamageModifierOperation.Add,
            DamageBonus));
        context.RoundResult.AddLine($"大骨棒：{damage.Source.DisplayName}{BattleRules.GetCardName(damage.AttackType)}伤害+{DamageBonus}。");
        context.AddTriggerLog("[Equipment/大骨棒]");
        context.AddTriggerLog($"{BattleRules.GetCardName(damage.AttackType)} +{DamageBonus}");
    }
}

/// <summary>
/// Equipment System 的公开类：BigBoneClubStunEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BigBoneClubStunEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null
            || damage.Cancelled
            || !damage.IsResolved
            || damage.ActualDamageDealt <= 0
            || !BattleRules.IsShaAttack(damage.AttackType))
        {
            return;
        }

        if (!BattleRules.HasEquipment(damage.Source, EquipmentIds.BigBoneClub))
        {
            return;
        }

        if (damage.Target.IsDead)
        {
            return;
        }
        if (damage.Target.IsCombatDebuffImmune)
        {
            context.AddTriggerLog($"[泉水精华] {damage.Target.DisplayName}免疫眩晕。");
            return;
        }

        damage.Target.SetStun(damage.Target.StunTurnsRemaining + 1);
        context.RoundResult.AddLine($"大骨棒：{damage.Target.DisplayName}获得1层【眩晕】。");
        context.AddTriggerLog("[Equipment/大骨棒]");
        context.AddTriggerLog($"{damage.Target.DisplayName} Stun={damage.Target.StunTurnsRemaining}");
    }
}
