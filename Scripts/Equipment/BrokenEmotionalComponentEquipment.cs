//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/BrokenEmotionalComponentEquipment.cs
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

// 破碎情感组件（史诗）：每个回合结束时，场上所有存活单位（玩家+全部敌人）各恢复10点生命值。
// 与丹/生命芯片等其它治疗效果叠加计算，无上限。
/// <summary>
/// Equipment System 的公开类：BrokenEmotionalComponentEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BrokenEmotionalComponentEffect : IBattleEffect
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
        // 场上任何一方（玩家或任意存活敌人，例如精英"失心者"）持有本装备都应触发这个
        // "全场回血"效果——不能只检查玩家是否持有，否则给敌人配这件装备会完全不生效。
        var hasComponent = GameManager.HasEquipment(EquipmentIds.BrokenEmotionalComponent);
        if (!hasComponent)
        {
            var enemiesForCheck = context.Encounter?.Enemies;
            if (enemiesForCheck != null)
            {
                foreach (var enemyCheck in enemiesForCheck)
                {
                    if (!enemyCheck.IsDead && enemyCheck.HasEquipment(EquipmentIds.BrokenEmotionalComponent))
                    {
                        hasComponent = true;
                        break;
                    }
                }
            }
        }

        if (!hasComponent)
        {
            return;
        }

        const int healAmount = 10;

        if (context.Player.Health > 0 && !context.GameOver)
        {
            var healed = BattleHealing.Apply(
                context,
                context.Player,
                healAmount,
                false,
                new HealthChangeSource(HealthChangeSourceKind.Equipment, "破碎情感组件", EquipmentIds.BrokenEmotionalComponent, context.Player)).HealedAmount;
            if (healed > 0)
            {
                context.RoundResult.AddLine($"破碎情感组件：{context.Player.DisplayName}恢复{healed}点生命值。");
                context.AddTriggerLog($"[Equipment/破碎情感组件] {context.Player.DisplayName}恢复{healed}点生命值。");
            }
        }

        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            if (enemy.IsDead || context.GameOver)
            {
                continue;
            }

            var healed = BattleHealing.Apply(
                context,
                enemy,
                healAmount,
                false,
                new HealthChangeSource(HealthChangeSourceKind.Equipment, "破碎情感组件", EquipmentIds.BrokenEmotionalComponent, enemy)).HealedAmount;
            if (healed > 0)
            {
                context.RoundResult.AddLine($"破碎情感组件：{enemy.DisplayName}恢复{healed}点生命值。");
                context.AddTriggerLog($"[Equipment/破碎情感组件] {enemy.DisplayName}恢复{healed}点生命值。");
            }
        }
    }
}
