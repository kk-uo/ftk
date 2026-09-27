//////////////////////////////////////////////////////////
// 文件：Scripts/SkillEffects/ChangZuiEffect.cs
//
// 模块：Skill Effect System
//
// 职责：
// 1. 承载技能效果实现与 Trigger 接入相关代码。
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

// 长醉：酒层数不因回合结束清除，仅在发动杀系攻击后消耗。
// 实现：OnTurnEnd（High）保存未攻击时的酒层到RuntimeState；
//       OnTurnStart（Immediate）在TurnStartWineEffect激活前恢复入队列。
/// <summary>
/// Skill Effect System 的公开类：ChangZuiEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ChangZuiEffect : ISkillEffect
{
    public string SkillId => SkillIds.ChangZui;

    /// <summary>
    /// Skill Effect System 的公开入口：Register。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Register(TriggerManager triggerManager, Player owner) { }
}

// 回合结束前：将未攻击时的酒层保存至 RuntimeState，TurnEndCleanupEffect 清除后由下回合还原。
/// <summary>
/// Skill Effect System 的公开类：ChangZuiPreserveEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ChangZuiPreserveEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnEnd;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var enemies = context.Encounter?.Enemies;
        if (enemies == null) return;

        foreach (var enemy in enemies)
        {
            if (enemy.IsDead || !enemy.HasSkill(SkillIds.ChangZui)) continue;

            var totalWine = enemy.WinePower + enemy.PendingWinePower;
            if (totalWine <= 0) continue;

            var actionEntry = context.GetEnemyActionEntry(enemy);
            var usedKillAttack = actionEntry != null
                && !actionEntry.ActionCancelled
                && BattleRules.IsShaAttack(actionEntry.Action.Type);

            if (!usedKillAttack)
            {
                enemy.RuntimeStates["changzui_carryover"] = totalWine;
                context.AddTriggerLog($"[长醉] {enemy.DisplayName}：{totalWine}层酒保留至下回合。");
            }
        }
    }
}

// 回合开始（Immediate，在TurnStartWineEffect=Low之前）：将保存的酒层重新入队列。
/// <summary>
/// Skill Effect System 的公开类：ChangZuiRestoreEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ChangZuiRestoreEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.Immediate;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var enemies = context.Encounter?.Enemies;
        if (enemies == null) return;

        foreach (var enemy in enemies)
        {
            if (enemy.IsDead || !enemy.HasSkill(SkillIds.ChangZui)) continue;
            if (!enemy.RuntimeStates.TryGetValue("changzui_carryover", out var v) || v is not int carryover || carryover <= 0)
                continue;

            enemy.RuntimeStates.Remove("changzui_carryover");
            enemy.QueueWinePower(carryover);
            context.AddTriggerLog($"[长醉] {enemy.DisplayName}：延续{carryover}层酒至本回合。");
        }
    }
}
