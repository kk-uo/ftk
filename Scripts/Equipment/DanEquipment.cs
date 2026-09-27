//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/DanEquipment.cs
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

using System;

// 丹：持有方在战斗中任意恢复生命时，对"距离持有方最近的存活敌人"造成本次恢复量 × 50% 的伤害。
// • 玩家持有：最近敌人 = context.GetFirstAliveEnemy()（encounter 中最左侧存活单位）。
// • 敌方持有：最近敌人 = 玩家（敌方视角下玩家即是敌人）。
// 恢复量 × 50% 向下取整，最低 1 点；无存活目标则不触发。
// 触发时机：OnHeal（桃使用后、青囊回复后均可触发）。
/// <summary>
/// Equipment System 的公开类：DanEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class DanEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnHeal;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var heal = context.HealEvent;
        if (heal == null || heal.Amount <= 0 || context.GameOver)
        {
            return;
        }

        var healer = heal.Healer;
        Player? danTarget;
        bool healerHasDan;

        if (healer == context.Player)
        {
            healerHasDan = GameManager.HasEquipment(EquipmentIds.Dan);
            danTarget = context.GetFirstAliveEnemy();
        }
        else if (healer is EnemyInstance enemyHealer)
        {
            healerHasDan = enemyHealer.HasEquipment(EquipmentIds.Dan);
            // 敌方视角的"最近存活敌人"即为玩家。
            danTarget = context.Player.IsDead ? null : context.Player;
        }
        else
        {
            return;
        }

        if (!healerHasDan || danTarget == null || danTarget.IsDead)
        {
            return;
        }

        var danDamage = Math.Max(1, heal.Amount / 2);
        var healthBefore = danTarget.Health;
        danTarget.TakeDamage(danDamage);
        context.RecordDirectDamage(danTarget, danDamage, Math.Max(0, healthBefore - danTarget.Health), healthBefore, danTarget.Health,
            new HealthChangeSource(HealthChangeSourceKind.Equipment, "丹", EquipmentIds.Dan, healer));
        context.RoundResult.AddLine($"丹触发：{healer.DisplayName}恢复{heal.Amount}点生命，对{danTarget.DisplayName}造成{danDamage}点伤害（×50%）。");
        context.AddTriggerLog("[Equipment/Dan]");
        context.AddTriggerLog($"丹：恢复量{heal.Amount} → 伤害{danDamage}（向下取整，最低1）。");

        // 若目标因丹的伤害濒死，触发完整的濒死/死亡链。
        if (danTarget.Health <= 0 && !context.GameOver)
        {
            var savedHeal = context.HealEvent;
            context.HealEvent = null;
            context.DamageEvent = new DamageEvent(healer, danTarget, CardType.Kill, danDamage,
                origin: new HealthChangeSource(HealthChangeSourceKind.Equipment, "丹", EquipmentIds.Dan, healer));
            context.RaiseOnDying();
            context.DamageEvent = null;
            context.HealEvent = savedHeal;
        }
    }
}
