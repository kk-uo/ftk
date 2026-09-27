//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/WarDrumEquipment.cs
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

// 战鼓：本场战斗第一次打出攻击牌后，下一回合起所有攻击牌基础伤害+5；
// 本回合未打出攻击牌时立即移除。同时对玩家和敌方生效。
/// <summary>
/// Equipment System 的公开类：WarDrumEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class WarDrumEffect : IBattleEffect
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
        if (damage == null || damage.Cancelled || !IsWarDrumAttack(damage.AttackType))
        {
            return;
        }

        var source = damage.Source;
        if (!SourceHasWarDrum(context, source))
        {
            return;
        }

        // 首次攻击触发：进入待激活（下回合生效）
        if (!source.WarDrumFirstAttackPlayed)
        {
            if (!context.IsDamagePreview)
            {
                source.TriggerWarDrum();
                context.RoundResult.AddLine($"战鼓触发：{source.DisplayName}下回合起所有攻击牌基础伤害+5。");
                context.AddTriggerLog("[Equipment/战鼓]");
                context.AddTriggerLog($"战鼓：{source.DisplayName}首次攻击触发，下回合生效。");
            }
        }

        // 激活期间：追踪本回合已出攻击 + 附加+5基础伤害
        if (source.WarDrumActive)
        {
            if (!context.IsDamagePreview)
            {
                source.SetWarDrumAttackedThisTurn();
            }
            damage.AddModifier(new DamageModifier(
                "战鼓",
                DamageModifierPriority.FlatBonus,
                DamageModifierOperation.Add,
                5));
            context.RoundResult.AddLine($"战鼓：{source.DisplayName}{BattleRules.GetCardName(damage.AttackType)}基础伤害+5。");
            context.AddTriggerLog("[Equipment/战鼓]");
            context.AddTriggerLog($"战鼓激活：{BattleRules.GetCardName(damage.AttackType)} +5。");
        }
    }

    private static bool IsWarDrumAttack(CardType type) =>
        BattleRules.IsShaAttack(type)
        || type == CardType.ArrowBarrage
        || type == CardType.NanmanInvasion
        || type == CardType.Tuxi;

    private static bool SourceHasWarDrum(BattleContext context, Player source)
    {
        if (source == context.Player)
        {
            return GameManager.HasEquipment(EquipmentIds.WarDrum);
        }

        return source is EnemyInstance enemySource && enemySource.HasEquipment(EquipmentIds.WarDrum);
    }
}

// 战鼓激活：回合开始时，将待激活（Pending）转为活跃（Active），并重置本回合攻击追踪。
/// <summary>
/// Equipment System 的公开类：WarDrumActivationEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class WarDrumActivationEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        ActivateForUnit(context, context.Player);
        var enemies = context.Encounter?.Enemies;
        if (enemies != null)
        {
            foreach (var enemy in enemies)
            {
                if (!enemy.IsDead)
                {
                    ActivateForUnit(context, enemy);
                }
            }
        }
    }

    private static void ActivateForUnit(BattleContext context, Player unit)
    {
        if (!unit.WarDrumPending)
        {
            return;
        }

        unit.ActivateWarDrum();
        context.RoundResult.AddLine($"战鼓生效：{unit.DisplayName}本回合所有攻击牌基础伤害+5。");
        context.AddTriggerLog("[Equipment/战鼓]");
        context.AddTriggerLog($"战鼓：{unit.DisplayName}进入激活状态。");
    }
}

// 战鼓维持：回合结束时，若本回合未打出攻击牌则立即移除战鼓状态。
/// <summary>
/// Equipment System 的公开类：WarDrumMaintenanceEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class WarDrumMaintenanceEffect : IBattleEffect
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
        CheckUnit(context, context.Player);
        var enemies = context.Encounter?.Enemies;
        if (enemies != null)
        {
            foreach (var enemy in enemies)
            {
                CheckUnit(context, enemy);
            }
        }
    }

    private static void CheckUnit(BattleContext context, Player unit)
    {
        if (!unit.WarDrumActive)
        {
            return;
        }

        if (unit.WarDrumAttackedThisTurn)
        {
            return;
        }

        unit.DeactivateWarDrum();
        context.RoundResult.AddLine($"战鼓移除：{unit.DisplayName}本回合未打出攻击牌，战鼓状态消失。");
        context.AddTriggerLog("[Equipment/战鼓]");
        context.AddTriggerLog($"战鼓：{unit.DisplayName}战鼓状态移除。");
    }
}
