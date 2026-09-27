//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/MonumentEquipment.cs
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

public sealed class ForgottenStoneEffect : IBattleEffect
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
        if (damage == null || damage.Cancelled || damage.OnlyAllowCounterOrCardShields || damage.Target != context.Player)
        {
            return;
        }

        if (context.TurnNumber != 1 || !GameManager.HasEquipment(EquipmentIds.ForgottenStone))
        {
            return;
        }

        damage.CancelAsFullyBlocked();
        context.RoundResult.AddLine("遗忘之石：第一回合免疫本次伤害。");
        context.AddTriggerLog("[Equipment/ForgottenStone]");
        context.AddTriggerLog("遗忘之石：第一回合伤害归零。");
    }
}

/// <summary>
/// Equipment System 的公开类：StatueCoreSplashEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class StatueCoreSplashEffect : IBattleEffect
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
        if (damage == null || damage.Cancelled || damage.Amount <= 0)
        {
            return;
        }

        if (damage.Source != context.Player || !GameManager.HasEquipment(EquipmentIds.StatueCore))
        {
            return;
        }

        if (!BattleRules.IsAnyAttackCard(damage.AttackType))
        {
            return;
        }

        if (context.Player.StatueCoreAttacksTriggered >= 3)
        {
            return;
        }

        if (damage.Target is not EnemyInstance primaryTarget)
        {
            return;
        }

        var encounter = context.Encounter;
        if (encounter == null)
        {
            return;
        }

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
            context.RoundResult.AddLine($"雕像核心溅射：{enemy.DisplayName}受到{splashAmount}点伤害（{damage.Amount}×50%）。");
            context.AddTriggerLog("[Equipment/StatueCore]");
            context.AddTriggerLog($"雕像核心溅射：→{enemy.DisplayName} -{splashAmount}");

            if (enemy.Health <= 0 && !enemy.IsDead && !context.GameOver)
            {
                var savedDamage = context.DamageEvent;
                context.DamageEvent = new DamageEvent(context.Player, enemy, damage.AttackType, splashAmount);
                context.RaiseOnDying();
                context.DamageEvent = savedDamage;
            }
        }

        context.Player.IncrementStatueCoreAttacks();
        var remaining = 3 - context.Player.StatueCoreAttacksTriggered;
        if (splashCount > 0)
        {
            context.RoundResult.AddLine($"雕像核心触发（剩余{remaining}次）：溅射完成。");
        }
        else
        {
            context.RoundResult.AddLine($"雕像核心触发（剩余{remaining}次）：没有可溅射的其他目标。");
        }
    }
}
