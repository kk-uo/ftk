//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/ShortBowEquipment.cs
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

// 短弓（普通）：本场战斗前3次攻击命中后，对另外最多2个存活敌人造成本次伤害50%的溅射。
// 每回合最多触发一次；3次总触发后失效。
//
// ShortBowSplashEffect（OnDamageTaken, Lowest）：检查并执行溅射。
// ShortBowRoundResetEffect（OnTurnStart, Low）：每回合开始时重置本回合触发标记。
/// <summary>
/// Equipment System 的公开类：ShortBowSplashEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ShortBowSplashEffect : IBattleEffect
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
        if (context.GameOver)
        {
            return;
        }

        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Amount <= 0)
        {
            return;
        }

        if (damage.Source != context.Player)
        {
            return;
        }

        if (!BattleRules.IsAnyAttackCard(damage.AttackType))
        {
            return;
        }

        if (!GameManager.HasEquipment(EquipmentIds.ShortBow))
        {
            return;
        }

        var player = context.Player;
        if (player.ShortBowBattleTriggers >= 3 || player.ShortBowTriggeredThisRound)
        {
            return;
        }

        var encounter = context.Encounter;
        if (encounter == null)
        {
            return;
        }

        var primaryTarget = damage.Target;
        var splashAmount = System.Math.Max(1, damage.Amount / 2);
        var splashCount = 0;

        foreach (var enemy in encounter.Enemies)
        {
            if (enemy.IsDead || enemy == primaryTarget || splashCount >= 2)
            {
                continue;
            }

            splashCount++;
            enemy.TakeDamage(splashAmount);
            context.RoundResult.AddLine($"短弓溅射：{enemy.DisplayName}受到{splashAmount}点伤害（{damage.Amount}×50%）。");
            context.AddTriggerLog("[Equipment/ShortBow]");
            context.AddTriggerLog($"短弓溅射：→{enemy.DisplayName} -{splashAmount}");

            if (enemy.Health <= 0 && !enemy.IsDead && !context.GameOver)
            {
                var savedDamage = context.DamageEvent;
                context.DamageEvent = new DamageEvent(context.Player, enemy, damage.AttackType, splashAmount);
                context.RaiseOnDying();
                context.DamageEvent = savedDamage;
            }
        }

        if (splashCount > 0)
        {
            player.TriggerShortBow();
            var remaining = 3 - player.ShortBowBattleTriggers;
            context.RoundResult.AddLine($"短弓触发（{player.ShortBowBattleTriggers}/3），剩余{remaining}次。");
        }
    }
}

/// <summary>
/// Equipment System 的公开类：ShortBowRoundResetEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ShortBowRoundResetEffect : IBattleEffect
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
        if (GameManager.HasEquipment(EquipmentIds.ShortBow))
        {
            context.Player.ResetShortBowRound();
        }
    }
}
