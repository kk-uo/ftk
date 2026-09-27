//////////////////////////////////////////////////////////
// 文件：Scripts/HeroUnlock/HeroUnlockTrackingEffects.cs
//
// 模块：Hero Unlock System
//
// 为什么存在：
// "击败指定敌人"、"累计使用杀"、"累计回复生命"、"连续使用费"这几种解锁条件
// 需要在战斗真正发生这些事情的时候记录一次，而这些时机（敌人死亡、造成伤害、
// 回复生命、这一回合出了什么牌）只存在于既有的 Trigger 系统里。
//
// 本文件按照项目里其它角色专属被动技能完全相同的模式（IBattleEffect + 通过
// BattleRules.cs 注册），新增几个"只做记录、不影响任何战斗结算"的效果：
// 它们只在 Execute 里调用 HeroUnlockProgress 的记录方法，不修改 DamageEvent、
// 不修改 HP/费用、不新增 DamageModifier，纯粹是旁路的观察者。
//
// 职责：
// 1. HeroUnlockEnemyKilledTrackingEffect：敌人死亡时记录 EnemyId。
// 2. HeroUnlockKillCardTrackingEffect：玩家使用杀系卡牌时累计成就计数。
// 3. HeroUnlockHealingTrackingEffect：玩家恢复生命时累计成就计数。
// 4. HeroUnlockFeeStreakTrackingEffect：记录这一回合是否使用了【费】，
//    维护"历史最高连续使用【费】回合数"成就。
//
// 不负责：
// × 判断战斗结算、伤害计算、卡牌是否合法（完全不修改 BattleContext 的
//   战斗相关字段，只读取只记录）。
// × 保存进度本身（由 HeroUnlockProgress 负责）。
//
// 主要依赖：
// IBattleEffect / TriggerManager / HeroUnlockProgress / BattleRules
//////////////////////////////////////////////////////////

/// <summary>
/// 敌人死亡时记录一次"击败过这个敌人"，供【击败指定敌人】类型的解锁条件使用。
/// </summary>
public sealed class HeroUnlockEnemyKilledTrackingEffect : IBattleEffect
{
    // 排在 DeathEffect（Immediate）之后执行，保证读取到的是"确实已经死亡"的状态。
    public EffectPriority Priority => EffectPriority.Low;
    public TriggerTiming Timing => TriggerTiming.OnDeath;

    /// <summary>
    /// Hero Unlock System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var target = context.DamageEvent?.Target;
        if (target is not EnemyInstance enemy)
        {
            return;
        }

        HeroUnlockProgress.RecordEnemyKilled(enemy.Definition.Id);
    }
}

/// <summary>
/// 玩家使用杀系攻击卡牌（普通杀/火杀/雷杀/必中杀/冰杀等，复用既有的
/// <see cref="BattleRules.IsShaAttack"/> 判断）时，累计
/// <see cref="AchievementType.KillCardsPlayed"/> 成就进度。
///
/// 按"是否使用"计数，不要求命中或造成伤害——和"使用100次杀"这个描述一致。
/// </summary>
public sealed class HeroUnlockKillCardTrackingEffect : IBattleEffect
{
    public EffectPriority Priority => EffectPriority.Low;
    public TriggerTiming Timing => TriggerTiming.OnBattlePhase;

    /// <summary>
    /// Hero Unlock System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var action = context.PlayerAction;
        if (action == null || !BattleRules.IsShaAttack(action.Type))
        {
            return;
        }

        HeroUnlockProgress.AddAchievementProgress(AchievementType.KillCardsPlayed, 1);
    }
}

/// <summary>
/// 玩家恢复生命时，累计 <see cref="AchievementType.HealingDone"/> 成就进度。
/// </summary>
public sealed class HeroUnlockHealingTrackingEffect : IBattleEffect
{
    public EffectPriority Priority => EffectPriority.Low;
    public TriggerTiming Timing => TriggerTiming.OnHeal;

    /// <summary>
    /// Hero Unlock System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var heal = context.HealEvent;
        if (heal == null || heal.Healer != context.Player || heal.Amount <= 0)
        {
            return;
        }

        HeroUnlockProgress.AddAchievementProgress(AchievementType.HealingDone, heal.Amount);
    }
}

/// <summary>
/// 每回合记录玩家是否使用了【费】，维护
/// <see cref="AchievementType.ConsecutiveFeeTurns"/>（历史最高连续回合数）。
/// </summary>
public sealed class HeroUnlockFeeStreakTrackingEffect : IBattleEffect
{
    public EffectPriority Priority => EffectPriority.Low;
    public TriggerTiming Timing => TriggerTiming.OnBattlePhase;

    /// <summary>
    /// Hero Unlock System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var action = context.PlayerAction;
        if (action == null)
        {
            return;
        }

        HeroUnlockProgress.RecordFeeTurn(action.IsFee);
    }
}
