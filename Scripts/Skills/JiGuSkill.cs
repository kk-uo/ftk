//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/JiGuSkill.cs
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

// 击鼓无敌：当击鼓激活时，受击方受到的所有伤害归零（OnBeforeDamage/Immediate，最高优先级）。
// 同时对玩家和敌方单位生效——只要 Target.JiGuActive 即触发。
/// <summary>
/// Skill System 的公开类：JiGuInvincibilityEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class JiGuInvincibilityEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.Immediate;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.OnlyAllowCounterOrCardShields || !damage.Target.JiGuActive)
        {
            return;
        }

        damage.CancelAsFullyBlocked();
        context.RoundResult.AddLine($"击鼓无敌：{damage.Target.DisplayName}免疫{BattleRules.GetCardName(damage.AttackType)}伤害。");
        context.AddTriggerLog("[击鼓]");
        context.AddTriggerLog("Trigger: OnBeforeDamage");
        context.AddTriggerLog("Priority: Immediate");
        context.AddTriggerLog($"击鼓无敌：{damage.Target.DisplayName}伤害归零。");
        context.ReportPlayerCharacterSkillTriggered(
            damage.Target, SkillIds.JiGu, Timing, Priority, "invincible");
    }
}

// 击鼓触发：每次实际受到伤害均可触发，直到本场战斗首次发动成功为止。
// 玩家：队列反应条让玩家选择发动/放弃；放弃后下一次受伤可再次触发。
// 敌方：AI自动决策（HP<40必发；HP≥40且玩家费≥7时75%概率发）。
/// <summary>
/// Skill System 的公开类：JiGuTriggerEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class JiGuTriggerEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    // 必须先于 DamageTakenEffect 的濒死检查建立反应请求；否则致死伤害会同步完成死亡结算，
    // BattleManager 没有机会展示击鼓反应条。
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || context.GameOver)
        {
            return;
        }

        if (damage.Target == context.Player)
        {
            var player = context.Player;
            if (player.JiGuTriggered || player.JiGuReactionPending || player.IsDead || !player.HasSkill(SkillIds.JiGu))
            {
                return;
            }

            player.QueueJiGuReaction();
            context.Reactions.Enqueue(new JiGuReaction(damage));
            context.AddTriggerLog("[击鼓]");
            context.AddTriggerLog("Trigger: OnDamageTaken");
            context.AddTriggerLog("Priority: Low");
            context.AddTriggerLog("击鼓触发：受到实际伤害，反应条已激活；发动前仍可在后续受伤时再次触发。");
            context.ReportPlayerCharacterSkillTriggered(
                player, SkillIds.JiGu, Timing, Priority, "reaction_window");
            return;
        }

        if (damage.Target is EnemyInstance enemyTarget
            && !enemyTarget.JiGuTriggered
            && !enemyTarget.IsDead
            && enemyTarget.HasSkill(SkillIds.JiGu))
        {
            enemyTarget.TriggerJiGu();
            if (ShouldEnemyActivateJiGu(context, enemyTarget))
            {
                enemyTarget.ActivateJiGu();
                context.RoundResult.AddLine($"{enemyTarget.DisplayName}击鼓发动：进入击鼓状态（3回合），所有受到的伤害归零，造成伤害时恢复满血。");
                context.AddTriggerLog("[击鼓]");
                context.AddTriggerLog($"{enemyTarget.DisplayName}（AI）选择：发动击鼓");
                context.AddTriggerLog($"击鼓激活：{enemyTarget.DisplayName}进入无敌状态，剩余{enemyTarget.JiGuTurnsRemaining}计数。");
            }
            else
            {
                context.RoundResult.AddLine($"{enemyTarget.DisplayName}放弃击鼓。");
                context.AddTriggerLog("[击鼓]");
                context.AddTriggerLog($"{enemyTarget.DisplayName}（AI）选择：放弃");
            }
        }
    }

    private static bool ShouldEnemyActivateJiGu(BattleContext context, EnemyInstance enemy)
    {
        if (enemy.Health < 40)
        {
            return true;
        }
        if (context.Player.CurrentMana >= 7)
        {
            return System.Random.Shared.NextDouble() < 0.75;
        }
        return false;
    }
}

// 击鼓恢复：击鼓激活期间，造成任意伤害后立即恢复满血（OnDamageTaken/Low，标准回复事件）。
// 同时对玩家和敌方单位生效——检查伤害来源（Source）是否处于击鼓状态。
/// <summary>
/// Skill System 的公开类：JiGuHealOnDealEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class JiGuHealOnDealEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || context.GameOver)
        {
            return;
        }

        var source = damage.Source;
        if (!source.JiGuActive || source.IsDead)
        {
            return;
        }

        var healAmount = source.MaxHealth - source.Health;
        if (healAmount <= 0)
        {
            return;
        }

        var healed = BattleHealing.Apply(
            context,
            source,
            healAmount,
            false,
            new HealthChangeSource(HealthChangeSourceKind.Skill, "击鼓", SkillIds.JiGu, source)).HealedAmount;
        context.RoundResult.AddLine($"击鼓：{source.DisplayName}造成伤害，恢复至{source.Health}/{source.MaxHealth}。");
        context.AddTriggerLog("[击鼓]");
        context.AddTriggerLog("Trigger: OnDamageTaken");
        context.AddTriggerLog("Priority: Low");
        context.AddTriggerLog($"击鼓恢复：{source.DisplayName}恢复{healed}生命。");
        context.ReportPlayerCharacterSkillTriggered(
            source, SkillIds.JiGu, Timing, Priority, "heal");

    }
}

// 击鼓倒计时：每回合结束时减少剩余回合数，归零时关闭无敌（OnTurnEnd/Low）。
// 同时对玩家和所有敌方单位生效。
/// <summary>
/// Skill System 的公开类：JiGuTurnCountdownEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class JiGuTurnCountdownEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnEnd;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        DecrementForUnit(context, context.Player);
        var enemies = context.Encounter?.Enemies;
        if (enemies != null)
        {
            foreach (var enemy in enemies)
            {
                DecrementForUnit(context, enemy);
            }
        }
    }

    private static void DecrementForUnit(BattleContext context, Player unit)
    {
        if (!unit.JiGuActive)
        {
            return;
        }

        unit.DecrementJiGu();
        if (!unit.JiGuActive)
        {
            context.RoundResult.AddLine($"{unit.DisplayName}击鼓状态结束：无敌与恢复效果消失。");
            context.AddTriggerLog("[击鼓]");
            context.AddTriggerLog("Trigger: OnTurnEnd");
            context.AddTriggerLog($"{unit.DisplayName}击鼓状态结束。");
        }
        else
        {
            context.AddTriggerLog("[击鼓]");
            context.AddTriggerLog($"{unit.DisplayName}击鼓剩余回合：{unit.JiGuTurnsRemaining}。");
        }
    }
}
