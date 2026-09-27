//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/NightVisionGogglesEquipment.cs
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

// 夜视镜（史诗，商店获得）：
//   效果一：装备者的单体伤害攻击牌（杀系：杀/火杀/雷杀/火雷杀/必中杀/冰杀）造成伤害×1.2。
//   效果二：装备者的群体伤害攻击牌（万箭齐发/南蛮入侵/突袭/天体撞击）不再对场上所有敌人
//     分别造成伤害，而是集中命中其中一名敌人，伤害×场上存活敌人数（有几人就几倍）。
//
// 实现方式：
//   两个效果都只改写 DamageEvent 的伤害倍率，不改变 BattleResolver 里"逐个敌人结算"的
//   循环结构本身——群体牌仍然会对每个存活敌人各自结算一次响应判定（闪避/反震等），
//   但除了被锁定的目标外，其余命中一律通过 ×0 修正把最终伤害归零，观感上等价于
//   "只打中了一个人"。已知的、可接受的简化：其余敌人对这次攻击做出的响应判定
//   （例如消耗一次持续闪避）仍会正常结算，即便它们本来就不会受到伤害。
//
//   目标锁定（BattleContext.NightVisionAoeLockedTarget/NightVisionAoeEnemyCount）在
//   本回合第一次遇到玩家群体伤害攻击命中时惰性计算并缓存，回合开始时清空
//   （见 BattleContext.BeginRoundResult）；每回合玩家只会打出一张牌，因此这个缓存
//   在同一次群体伤害结算过程中始终指向同一个目标，不会中途改变。
//   锁定规则：场上存活敌人中当前生命值最高者（优先集中火力在最"扛"的敌人身上）。

/// <summary>
/// Equipment System 的公开类：NightVisionGogglesSingleTargetBonusEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class NightVisionGogglesSingleTargetBonusEffect : IBattleEffect
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

        if (damage.Source != context.Player
            || !BattleRules.IsShaAttack(damage.AttackType)
            || !GameManager.HasEquipment(EquipmentIds.NightVisionGoggles))
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "夜视镜精准打击",
            DamageModifierPriority.SpecialMultiplier,
            DamageModifierOperation.MultiplyFloat,
            1.2));
        context.RoundResult.AddLine("夜视镜：单体伤害攻击牌伤害×1.2。");
        context.AddTriggerLog("[Equipment/NightVisionGoggles]");
        context.AddTriggerLog("夜视镜：单体伤害×1.2。");
    }
}

/// <summary>
/// Equipment System 的公开类：NightVisionGogglesGroupFocusEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class NightVisionGogglesGroupFocusEffect : IBattleEffect
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

        if (damage.Source != context.Player
            || damage.Target is not EnemyInstance target
            || !IsGroupDamageCard(damage.AttackType)
            || !GameManager.HasEquipment(EquipmentIds.NightVisionGoggles))
        {
            return;
        }

        if (context.NightVisionAoeLockedTarget == null)
        {
            EnemyInstance? best = null;
            var aliveCount = 0;
            foreach (var enemy in context.Encounter?.Enemies ?? new System.Collections.Generic.List<EnemyInstance>())
            {
                if (enemy.IsDead)
                {
                    continue;
                }

                aliveCount++;
                if (best == null || enemy.Health > best.Health)
                {
                    best = enemy;
                }
            }

            if (best == null)
            {
                return;
            }

            context.NightVisionAoeLockedTarget = best;
            context.NightVisionAoeEnemyCount = aliveCount;
        }

        if (target != context.NightVisionAoeLockedTarget)
        {
            damage.AddModifier(new DamageModifier(
                "夜视镜集火（非目标）",
                DamageModifierPriority.SpecialMultiplier,
                DamageModifierOperation.Multiply,
                0));
            return;
        }

        damage.AddModifier(new DamageModifier(
            "夜视镜集火",
            DamageModifierPriority.SpecialMultiplier,
            DamageModifierOperation.Multiply,
            context.NightVisionAoeEnemyCount));
        context.RoundResult.AddLine($"夜视镜：群体伤害攻击牌集中命中，伤害×{context.NightVisionAoeEnemyCount}。");
        context.AddTriggerLog("[Equipment/NightVisionGoggles]");
        context.AddTriggerLog($"夜视镜：群体伤害集火，伤害×{context.NightVisionAoeEnemyCount}。");
    }

    private static bool IsGroupDamageCard(CardType type)
    {
        return type is CardType.ArrowBarrage or CardType.NanmanInvasion or CardType.Tuxi or CardType.CelestialImpact;
    }
}
