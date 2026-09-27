//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/PoisonEquipment.cs
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

// 毒药（普通饰品）：玩家的杀（普通杀/火杀/雷杀/火雷杀）命中后，
// 目标在下一回合开始时失去5点生命值（若命中多次，每次独立计入）。
//
// PoisonMarkEffect（OnDamageTaken, Lowest）：命中时在目标 RuntimeStates["poison_count"] 计数。
// PoisonApplyEffect（OnTurnStart, Low）：回合开始时对所有带毒计数的敌人扣血并清除计数。
/// <summary>
/// Equipment System 的公开类：PoisonMarkEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class PoisonMarkEffect : IBattleEffect
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

        if (damage.Source != context.Player)
        {
            return;
        }

        if (damage.AttackType is not (CardType.Kill or CardType.FireKill
            or CardType.ThunderKill or CardType.FireThunderKill))
        {
            return;
        }

        if (!GameManager.HasEquipment(EquipmentIds.Poison))
        {
            return;
        }

        if (!(damage.Target is EnemyInstance targetEnemy) || targetEnemy.IsDead)
        {
            return;
        }

        targetEnemy.RuntimeStates.TryGetValue("poison_count", out var existing);
        var prev = existing is int n ? n : 0;
        targetEnemy.RuntimeStates["poison_count"] = prev + 1;

        context.RoundResult.AddLine($"毒药：{targetEnemy.DisplayName}中毒（下回合扣{5 * (prev + 1)}点血，共{prev + 1}层）。");
        context.AddTriggerLog("[Equipment/Poison]");
        context.AddTriggerLog($"毒药：{targetEnemy.DisplayName} poison_count={prev + 1}");
    }
}

/// <summary>
/// Equipment System 的公开类：PoisonApplyEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class PoisonApplyEffect : IBattleEffect
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
        if (!GameManager.HasEquipment(EquipmentIds.Poison))
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
            if (enemy.IsDead)
            {
                continue;
            }

            if (!enemy.RuntimeStates.TryGetValue("poison_count", out var val) || val is not int count || count <= 0)
            {
                continue;
            }

            enemy.RuntimeStates["poison_count"] = 0;
            var poisonDamage = 5 * count;
            var healthBefore = enemy.Health;
            enemy.TakeDamage(poisonDamage);
            var actualDamage = System.Math.Max(0, healthBefore - enemy.Health);
            context.RecordDirectDamage(enemy, poisonDamage, actualDamage, healthBefore, enemy.Health,
                new HealthChangeSource(HealthChangeSourceKind.Equipment, "毒药", EquipmentIds.Poison, context.Player));
            context.RoundResult.AddLine($"毒药发作：{enemy.DisplayName}失去{poisonDamage}点Poison（毒素）伤害（{count}层×5）。");
            context.AddTriggerLog("[Equipment/Poison]");
            context.AddTriggerLog($"毒药发作：{enemy.DisplayName} -{poisonDamage}（{count}层，DamageType.Poison）");

            if (enemy.Health <= 0 && !enemy.IsDead && !context.GameOver)
            {
                var savedDamage = context.DamageEvent;
                context.DamageEvent = new DamageEvent(context.Player, enemy, CardType.Kill, poisonDamage, overrideDamageType: DamageType.Poison,
                    origin: new HealthChangeSource(HealthChangeSourceKind.Equipment, "毒药", EquipmentIds.Poison, context.Player));
                context.RaiseOnDying();
                context.DamageEvent = savedDamage;
            }
        }
    }
}
