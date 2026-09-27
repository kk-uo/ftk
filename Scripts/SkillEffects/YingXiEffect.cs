//////////////////////////////////////////////////////////
// 文件：Scripts/SkillEffects/YingXiEffect.cs
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

// 影袭（黄月英专属）ISkillEffect 注册存根。
// 实际效果由全局 IBattleEffect 实现，注册在 BattleTriggerEffects.CreateDefaultManager()。
/// <summary>
/// Skill Effect System 的公开类：YingXiEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class YingXiEffect : ISkillEffect
{
    public string SkillId => SkillIds.YingXi;
    /// <summary>
    /// Skill Effect System 的公开入口：Register。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Register(TriggerManager triggerManager, Player owner) { }
}

// 影袭无敌：影袭状态下，目标受到的所有伤害归零（OnBeforeDamage/Immediate）。
// 对玩家和敌方单位均生效——只要 Target.InShadowState 即触发。
/// <summary>
/// Skill Effect System 的公开类：YingXiInvincibilityEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class YingXiInvincibilityEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.Immediate;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled) return;

        var targetPlayer = damage.Target as Player;
        if (targetPlayer == null || !targetPlayer.InShadowState) return;

        damage.CancelAsFullyBlocked();
        context.RoundResult.AddLine($"影袭：{damage.Target.DisplayName}处于影袭无敌状态，免疫伤害。");
        context.AddTriggerLog("[影袭]");
        context.AddTriggerLog($"Trigger: OnBeforeDamage / Immediate");
        context.AddTriggerLog($"影袭无敌：{damage.Target.DisplayName}伤害归零。");
        context.ReportPlayerCharacterSkillTriggered(
            targetPlayer, SkillIds.YingXi, Timing, Priority, "invincible");
    }
}

// 影袭控制免疫：影袭状态下，每回合开始时清除冰冻等控制效果（OnTurnStart/Immediate）。
/// <summary>
/// Skill Effect System 的公开类：YingXiControlImmunityEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class YingXiControlImmunityEffect : IBattleEffect
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
        ClearControlForUnit(context, context.Player);
        var enemies = context.Encounter?.Enemies;
        if (enemies == null) return;
        foreach (var enemy in enemies)
            ClearControlForUnit(context, enemy);
    }

    private static void ClearControlForUnit(BattleContext context, Player unit)
    {
        if (!unit.InShadowState || !unit.IsFrozen) return;
        unit.ClearFreeze();
        context.RoundResult.AddLine($"影袭：{unit.DisplayName}处于影袭状态，冰冻效果被解除。");
        context.AddTriggerLog("[影袭]");
        context.AddTriggerLog($"{unit.DisplayName}影袭控制免疫：冰冻解除。");
        context.ReportPlayerCharacterSkillTriggered(
            unit, SkillIds.YingXi, TriggerTiming.OnTurnStart, EffectPriority.Immediate, "control_immunity");
    }
}

// 影袭倒计时：每回合结束时减少剩余回合数，归零时退出影袭状态（OnTurnEnd/Low）。
// 对玩家和所有敌方单位生效。
/// <summary>
/// Skill Effect System 的公开类：YingXiTurnCountdownEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class YingXiTurnCountdownEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnEnd;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        DecrementForUnit(context, context.Player);
        var enemies = context.Encounter?.Enemies;
        if (enemies == null) return;
        foreach (var enemy in enemies)
            DecrementForUnit(context, enemy);
    }

    private static void DecrementForUnit(BattleContext context, Player unit)
    {
        if (!unit.InShadowState) return;
        unit.DecrementShadowTurns();
        if (!unit.InShadowState)
        {
            context.RoundResult.AddLine($"{unit.DisplayName}影袭状态结束：无敌与控制免疫消失。");
            if (unit.ShadowCost >= 2.0)
                context.RoundResult.AddLine("影袭杀未造成伤害，影袭费用提高至2。");
            else if (unit.ShadowCost <= 0.0)
                context.RoundResult.AddLine("影袭杀造成伤害，可以再次发动影袭。");
            context.AddTriggerLog("[影袭]");
            context.AddTriggerLog($"Trigger: OnTurnEnd");
            context.AddTriggerLog($"{unit.DisplayName}影袭状态结束，ShadowCost={unit.ShadowCost}。");
        }
        else
        {
            context.AddTriggerLog("[影袭]");
            context.AddTriggerLog($"{unit.DisplayName}影袭剩余回合：{unit.RemainingTurns}。");
        }
    }
}

// 影袭杀伤害追踪：追踪影袭杀是否命中（!Cancelled）及是否实际造成伤害（OnDamageTaken/Low）。
// ShadowSlashUsed 已在 BattleResolver.ApplyResourceAndHealing 出牌时设置，此处只追踪命中与伤害。
/// <summary>
/// Skill Effect System 的公开类：YingXiShadowAttackTrackEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class YingXiShadowAttackTrackEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null) return;
        if (damage.AttackType != CardType.ShadowKill) return;

        Player? attacker = null;
        if (ReferenceEquals(damage.Source, context.Player) && context.Player.InShadowState)
        {
            attacker = context.Player;
        }
        else if (damage.Source is Player enemyAttacker
            && !ReferenceEquals(enemyAttacker, context.Player)
            && enemyAttacker.InShadowState)
        {
            attacker = enemyAttacker;
        }

        if (attacker == null) return;

        if (!damage.Cancelled)
        {
            attacker.MarkShadowSlashHit();
            if (damage.ActualDamageDealt > 0)
            {
                attacker.MarkShadowSlashDealDamage();
                context.AddTriggerLog("[影袭]");
                context.AddTriggerLog($"{attacker.DisplayName}影袭杀命中并造成伤害，ShadowSlashDealDamage=true。");
                context.ReportPlayerCharacterSkillTriggered(
                    attacker, SkillIds.YingXi, Timing, Priority, "shadow_kill");
            }
            else
            {
                context.AddTriggerLog("[影袭]");
                context.AddTriggerLog($"{attacker.DisplayName}影袭杀命中但伤害为0（免疫/吸收）。");
            }
        }
        else
        {
            context.AddTriggerLog("[影袭]");
            context.AddTriggerLog($"{attacker.DisplayName}影袭杀被取消（被无懈/护盾抵消），ShadowSlashHit=false。");
        }
    }
}
