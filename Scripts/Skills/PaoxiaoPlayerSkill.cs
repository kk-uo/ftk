//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/PaoxiaoPlayerSkill.cs
//
// 模块：Skill System
//
// 职责：
// 1. 承载角色技能、Boss 技能与技能触发效果相关代码。
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

using System.Linq;

// 咆哮（张飞角色版，区别于Boss版）
//
// 层数逻辑：
//   - 当回合使用杀 → PostPhase 时 paoxiao_player_stacks += count（不影响本回合伤害）
//   - 下回合起，每层贡献 +5 基础伤害（通过 FlatBonus 在 OnDamage 叠加，含永久加成）
//   - 战斗结束后 RuntimeStates 自动清零（ResetForNewBattle）
//
// 永久加成：每场胜利 GameManager.PaoxiaoPlayerKillBonus += 5，新局重置。

/// <summary>
/// Skill System 的公开类：PaoxiaoPlayerDamageEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class PaoxiaoPlayerDamageEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled) return;
        if (!BattleRules.IsShaAttack(damage.AttackType)) return;
        if (damage.Source != context.Player) return;
        if (!context.Player.HasSkill(SkillIds.PaoxiaoPlayer)) return;

        var battleStacks = context.Player.RuntimeStates.TryGetValue("paoxiao_player_stacks", out var v) ? (int)v : 0;
        var permanent = GameManager.PaoxiaoPlayerKillBonus;
        var total = battleStacks * 5 + permanent;
        if (total <= 0) return;

        damage.AddModifier(new DamageModifier(
            $"咆哮（+{total}）",
            DamageModifierPriority.FlatBonus,
            DamageModifierOperation.Add,
            total));
        context.AddTriggerLog($"[咆哮] 战斗{battleStacks}层×5={battleStacks * 5}，永久+{permanent}，本次+{total}。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.PaoxiaoPlayer, Timing, Priority, "damage_bonus");
    }
}

// 每回合结算后，将本回合使用的杀次数计入战斗层数（下回合起生效）。
/// <summary>
/// Skill System 的公开类：PaoxiaoPlayerStackEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class PaoxiaoPlayerStackEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBattlePostPhase;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (!context.Player.HasSkill(SkillIds.PaoxiaoPlayer)) return;
        if (context.PlayerActionCancelled) return;
        var action = context.PlayerAction;
        if (action == null || !BattleRules.IsShaAttack(action.Type)) return;

        var add = System.Math.Max(1, action.Count);
        context.Player.RuntimeStates.TryGetValue("paoxiao_player_stacks", out var v);
        var next = (v is int n ? n : 0) + add;
        context.Player.RuntimeStates["paoxiao_player_stacks"] = next;

        var permanent = GameManager.PaoxiaoPlayerKillBonus;
        context.RoundResult.AddLine(
            $"咆哮：战斗层数+{add}（共{next}层），下回合杀伤+{next * 5 + permanent}（层×5={next * 5}，永久={permanent}）。");
        context.AddTriggerLog($"[咆哮] 战斗stacks→{next}。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.PaoxiaoPlayer, Timing, Priority, "stack");
    }
}

// 战斗结束：玩家存活且所有敌人阵亡则判定为胜利，永久加成+5。
// 本战斗层数由 RuntimeStates 随新战斗自动清零，无需手动重置。
/// <summary>
/// Skill System 的公开类：PaoxiaoPlayerBattleEndEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class PaoxiaoPlayerBattleEndEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBattleEnd;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (!context.Player.HasSkill(SkillIds.PaoxiaoPlayer)) return;
        var allEnemiesDead = context.Encounter?.Enemies?.All(e => e.IsDead) == true;
        if (!context.Player.IsDead && allEnemiesDead)
        {
            GameManager.AddPaoxiaoPlayerKillBonus(5);
            context.RoundResult.AddLine(
                $"咆哮（胜利）：杀系永久加成+5（本局累计：+{GameManager.PaoxiaoPlayerKillBonus}）。");
            context.AddTriggerLog($"[咆哮] 永久加成→{GameManager.PaoxiaoPlayerKillBonus}。");
            context.ReportPlayerCharacterSkillTriggered(
                context.Player, SkillIds.PaoxiaoPlayer, Timing, Priority, "victory");
        }
    }
}
