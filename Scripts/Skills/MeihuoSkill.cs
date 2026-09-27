//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/MeihuoSkill.cs
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

// 魅惑技能系统（貂蝉专属）
//
// MeihuoApplyEffect（OnBattlePhase, High）：
//   玩家使用【魅惑】锦囊牌时，自身获得 meihuo_shield，并记录被魅惑的目标。
//   不可叠加：再次使用时刷新护盾并切换目标。
//
// MeihuoShieldEffect（OnBeforeDamage, High）：
//   玩家拥有魅惑护盾时：吸收玩家受到的下一次有效伤害，只消耗护盾；魅惑控制独立保留。
//
// MeihuoActivateEffect（OnTurnStart, High）：
//   回合开始时检查：若目标护盾尚存，则立即消耗护盾并对目标施加冰冻（该回合只能出费）。

/// <summary>
/// Skill System 的公开类：MeihuoApplyEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class MeihuoApplyEffect : IBattleEffect
{
    internal const string ShieldStateKey = "meihuo_shield";
    internal const string CharmedTargetStateKey = "meihuo_charmed_target";
    internal const string CharmedStateKey = "meihuo_charmed";

    public TriggerTiming Timing => TriggerTiming.OnBattlePhase;
    // 必须先于 BattlePhaseResolutionEffect（Mid）执行：魅惑与敌方攻击同回合
    // 揭示时，护盾应在攻击伤害进入 OnBeforeDamage 之前就已存在。
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.PlayerAction?.IsMeihuo != true || context.PlayerActionCancelled) return;

        // 指向性卡牌通常把目标写在 BattleAction 中；部分统一出牌入口只保留
        // PlayerLockedTarget。两者都必须指向 Encounter 内的同一个敌人实例，
        // 否则魅惑会正常扣费和播放卡牌，却没有任何单位收到护盾。
        var meihuoTarget = context.PlayerAction.Target as EnemyInstance
            ?? context.PlayerLockedTarget;
        if (meihuoTarget == null || meihuoTarget.IsDead) return;

        ClearPreviousCharmTarget(context.Player);
        context.Player.RuntimeStates[ShieldStateKey] = true;
        context.Player.RuntimeStates[CharmedTargetStateKey] = meihuoTarget;
        meihuoTarget.RuntimeStates[CharmedStateKey] = true;
        context.Player.ConsumeMeihuoUse();
        context.RoundResult.AddLine($"魅惑：玩家获得1层魅惑护盾，{meihuoTarget.DisplayName}将在下回合只能使用【费】；护盾被击破不会取消魅惑。（本场战斗还可使用{context.Player.MeihuoUsesRemaining}次）");
        context.AddTriggerLog($"[Skill/Meihuo] player meihuo_shield=true, target={meihuoTarget.DisplayName}");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.Meihuo, Timing, Priority, "applied");
    }

    internal static void ClearCharmState(Player player)
    {
        player.RuntimeStates.Remove(ShieldStateKey);
        ClearPreviousCharmTarget(player);
    }

    private static void ClearPreviousCharmTarget(Player player)
    {
        if (player.RuntimeStates.TryGetValue(CharmedTargetStateKey, out var targetValue)
            && targetValue is EnemyInstance previousTarget)
        {
            previousTarget.RuntimeStates.Remove(CharmedStateKey);
        }

        player.RuntimeStates.Remove(CharmedTargetStateKey);
    }
}

/// <summary>
/// Skill System 的公开类：MeihuoShieldEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class MeihuoShieldEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Amount <= 0 || damage.OnlyAllowCounterOrCardShields) return;

        if (!ReferenceEquals(damage.Target, context.Player)) return;
        if (!context.Player.RuntimeStates.TryGetValue(MeihuoApplyEffect.ShieldStateKey, out var val) || val is not true) return;

        // 魅惑护盾的契约是“抵挡一次伤害”，不能再按 Card.IsAttack 过滤。雷击、技能和
        // 装备等非攻击牌伤害只要进入正式 DamageEvent 管线，同样应由这一层护盾吸收。
        // 已取消或数值为0的事件不会走到这里，因此不会无故消耗唯一的一次格挡。
        damage.CancelAsFullyBlocked();
        var charmedTargetName = context.Player.RuntimeStates.TryGetValue(
                MeihuoApplyEffect.CharmedTargetStateKey,
                out var targetValue)
            && targetValue is EnemyInstance charmedTarget
                ? charmedTarget.DisplayName
                : "目标";
        // 护盾与控制是同一次技能产生的两个独立效果。消耗护盾只删除护盾标记，目标的
        // 魅惑状态继续保留到下回合开始，避免“成功帮玩家挡伤反而让控制失效”。
        context.Player.RuntimeStates.Remove(MeihuoApplyEffect.ShieldStateKey);
        context.RoundResult.AddLine($"魅惑护盾：玩家抵挡了本次伤害；对{charmedTargetName}的魅惑仍然生效。");
        context.AddTriggerLog($"[Skill/Meihuo] 玩家护盾被击破，target={charmedTargetName}，魅惑控制保留。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.Meihuo, Timing, Priority, "shield");
    }
}

/// <summary>
/// Skill System 的公开类：MeihuoActivateEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class MeihuoActivateEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var enemy = context.Player.RuntimeStates.TryGetValue(
                MeihuoApplyEffect.CharmedTargetStateKey,
                out var targetValue)
            ? targetValue as EnemyInstance
            : null;
        if (enemy == null
            || !enemy.RuntimeStates.TryGetValue(MeihuoApplyEffect.CharmedStateKey, out var charmed)
            || charmed is not true)
        {
            return;
        }

        MeihuoApplyEffect.ClearCharmState(context.Player);
        if (enemy.IsDead) return;

        enemy.SetFrozen(1);
        context.RoundResult.AddLine($"魅惑生效：{enemy.DisplayName}本回合只能使用【费】。");
        context.AddTriggerLog($"[Skill/Meihuo] 魅惑状态进入下回合 → {enemy.DisplayName}冰冻1回合。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.Meihuo, Timing, Priority, "freeze");
    }
}
