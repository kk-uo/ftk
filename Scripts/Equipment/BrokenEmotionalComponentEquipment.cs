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
        // 场上任何存活单位持有本装备都应触发全场治疗。这里显式枚举持有者，避免
        // 仅通过 GameManager（只保存玩家背包）判断时漏掉敌方精英/首领的装备。
        var owner = FindActiveOwner(context);
        if (owner == null)
        {
            return;
        }

        const int healAmount = 10;
        context.RoundResult.AddLine(Localization.GetFmt("battle.equipment.broken_emotion_start_fmt", owner.DisplayName, healAmount));
        context.AddTriggerLog("[Equipment/破碎情感组件]");
        context.AddTriggerLog($"持有者：{owner.DisplayName}；结算全场恢复{healAmount}点生命值。");

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
                context.RoundResult.AddLine(Localization.GetFmt("battle.equipment.broken_emotion_heal_fmt", context.Player.DisplayName, healed));
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
                context.RoundResult.AddLine(Localization.GetFmt("battle.equipment.broken_emotion_heal_fmt", enemy.DisplayName, healed));
                context.AddTriggerLog($"[Equipment/破碎情感组件] {enemy.DisplayName}恢复{healed}点生命值。");
            }
        }
    }

    private static Player? FindActiveOwner(BattleContext context)
    {
        if (!context.Player.IsDead && GameManager.HasEquipment(EquipmentIds.BrokenEmotionalComponent))
        {
            return context.Player;
        }

        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return null;
        }

        foreach (var enemy in enemies)
        {
            if (!enemy.IsDead && enemy.HasEquipment(EquipmentIds.BrokenEmotionalComponent))
            {
                return enemy;
            }
        }

        return null;
    }
}
