//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/LianyingSkill.cs
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

public sealed class LianyingBattlePrePhaseEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBattlePrePhase;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        ApplyLianying(context, context.Player);
    }

    private static void ApplyLianying(BattleContext context, Player player)
    {
        if (!player.HasSkill(SkillIds.Lianying) || player.IsDead || context.GameOver || !player.LianyingPrepared)
        {
            return;
        }

        player.ActivateLianyingFreeKill();
        // 技能大字特效放在"费用非0→0"真正触发的那一刻（见下方
        // LianyingResourceChangedEffect），这里只是"资格在下一回合正式生效"，
        // 用既有的状态角标（state.lianying_free / status.lianying_free）
        // 提示即可，不重复播放大字特效。
        context.RoundResult.AddLine($"{player.DisplayName}连营：本回合第一张普通杀免费。");
        context.AddTriggerLog("[连营]");
        context.AddTriggerLog("Trigger: OnBattlePrePhase");
        context.AddTriggerLog("Priority: High");
        context.AddTriggerLog($"{player.DisplayName}获得一次免费使用普通杀的机会。");
    }
}

/// <summary>
/// Skill System 的公开类：LianyingEnemyBattlePrePhaseEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class LianyingEnemyBattlePrePhaseEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBattlePrePhase;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            if (!enemy.HasSkill(SkillIds.Lianying) || enemy.IsDead || context.GameOver || !enemy.LianyingPrepared)
            {
                continue;
            }

            enemy.ActivateLianyingFreeKill();
            context.RoundResult.AddLine($"{enemy.DisplayName}连营：本回合第一张普通杀免费。");
            context.AddTriggerLog("[连营]");
            context.AddTriggerLog("Trigger: OnBattlePrePhase");
            context.AddTriggerLog("Priority: High");
            context.AddTriggerLog($"{enemy.DisplayName}获得一次免费使用普通杀的机会。");
        }
    }
}

/// <summary>
/// Skill System 的公开类：LianyingResourceChangedEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class LianyingResourceChangedEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnResourceChanged;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var resource = context.ResourceChangeEvent;
        if (resource == null)
        {
            return;
        }

        var player = resource.Owner;
        // 触发条件：费用真正从"非0"变成"0"（Before>0 且 After<=0），且当前
        // 处于"已充能"状态（LianyingArmed）。0→0（Before<=0）不会触发——
        // Before<=0 直接被下面的条件挡掉。一局内可重复触发：LianyingArmed
        // 触发后立即变 false（见 PrepareLianying），必须等费用离开0一次
        // （GainMana → RearmLianying）才会重新变 true，不再是"整场战斗只能
        // 触发一次"的永久栓。
        if (!resource.IsCostPayment
            || !player.HasSkill(SkillIds.Lianying)
            || !player.LianyingArmed
            || player.IsDead
            || context.GameOver
            || resource.Before <= 0
            || resource.After > 0)
        {
            return;
        }

        player.PrepareLianying();
        context.ReportPlayerCharacterSkillTriggered(player, SkillIds.Lianying, TriggerTiming.OnResourceChanged, EffectPriority.High);
        context.RoundResult.AddLine($"{player.DisplayName}连营触发：连营已准备。");
        context.AddTriggerLog("[连营]");
        context.AddTriggerLog("Trigger: OnResourceChanged");
        context.AddTriggerLog("Priority: High");
        context.AddTriggerLog($"{player.DisplayName}费用变为0，连营已准备（下回合第一张普通杀免费）。");
    }
}
