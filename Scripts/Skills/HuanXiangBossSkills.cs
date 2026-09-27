//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/HuanXiangBossSkills.cs
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

// 幻象触手 Boss 专属技能：幻象（传奇被动）+ 粘液（稀有被动）

internal static class HuanXiangKeys
{
    internal const string IllusionCardType = "illusion_card_type";
    internal const string AiMode          = "illusion_ai_mode";
    internal const string AiModeAttack    = "attack";
    internal const string AiModeDefend    = "defend";
    internal const string SlimeDebuff     = "slime_debuff";

    internal static bool IsTrackableAttack(DamageEvent damage) =>
        damage.Origin.Kind == HealthChangeSourceKind.AttackAction
        && new Card(damage.AttackType).IsAttack;
}

// ─────────────────────────────────────────────────────────────────────────────
// 幻象技能 ISkillEffect 入口
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Skill System 的公开类：IllusionSkillEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class IllusionSkillEffect : ISkillEffect
{
    public string SkillId => SkillIds.Illusion;

    /// <summary>
    /// Skill System 的公开入口：Register。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Register(TriggerManager triggerManager, Player owner)
    {
        triggerManager.Register(new IllusionImmunityEffect(owner));
        triggerManager.Register(new IllusionRecordEffect(owner));
        triggerManager.Register(new IllusionAiModeEffect(owner));
    }
}

// 幻象只响应“攻击牌实际结算”的伤害：攻击牌默认来源为 AttackAction；装备、技能、
// 环境伤害即使复用了某个 CardType，也不会被免疫、不会覆盖已记录的牌型。
// 这里不能再只检查杀系；火攻、万箭、南蛮等攻击牌也应按各自 CardType 独立记录。

// OnBeforeDamage / Highest：若攻击牌类型与记录一致，取消本次伤害
/// <summary>
/// Skill System 的公开类：IllusionImmunityEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class IllusionImmunityEffect : IBattleEffect
{
    private readonly Player? _owner;

    /// <summary>
    /// Skill System 的公开入口：IllusionImmunityEffect。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public IllusionImmunityEffect(Player? owner = null) => _owner = owner;

    public TriggerTiming Timing   => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.Highest;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || !HuanXiangKeys.IsTrackableAttack(damage)) return;
        var owner = _owner ?? damage.Target;
        if (damage.Target != owner || owner is not EnemyInstance enemy) return;
        if (!enemy.HasSkill(SkillIds.Illusion)) return;

        if (!enemy.RuntimeStates.TryGetValue(HuanXiangKeys.IllusionCardType, out var stored)
            || stored is not CardType illusionType)
            return;

        if (damage.AttackType != illusionType) return;

        damage.CancelAsFullyBlocked();
        context.RoundResult.AddLine(Localization.GetFmt(
            "battle.skill.illusion.immune_fmt",
            enemy.DisplayName,
            BattleRules.GetCardName(illusionType)));
        context.AddTriggerLog(Localization.GetFmt("battle.skill.illusion.immune_log_fmt", enemy.DisplayName, illusionType));
    }
}

// OnDamageTaken / Low：成功受到攻击牌伤害后更新免疫记录
/// <summary>
/// Skill System 的公开类：IllusionRecordEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class IllusionRecordEffect : IBattleEffect
{
    private readonly Player? _owner;

    /// <summary>
    /// Skill System 的公开入口：IllusionRecordEffect。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public IllusionRecordEffect(Player? owner = null) => _owner = owner;

    public TriggerTiming Timing   => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || !HuanXiangKeys.IsTrackableAttack(damage)) return;
        var owner = _owner ?? damage.Target;
        if (damage.Target != owner || owner is not EnemyInstance enemy) return;
        if (!enemy.HasSkill(SkillIds.Illusion)) return;
        if (damage.ActualDamageDealt <= 0) return;

        enemy.RuntimeStates[HuanXiangKeys.IllusionCardType] = damage.AttackType;
        context.RoundResult.AddLine(Localization.GetFmt(
            "battle.skill.illusion.record_fmt",
            enemy.DisplayName,
            BattleRules.GetCardName(damage.AttackType)));
        context.AddTriggerLog(Localization.GetFmt("battle.skill.illusion.record_log_fmt", damage.AttackType));
    }
}

// OnDamageTaken / Lowest：根据伤害结果切换 AI 模式
/// <summary>
/// Skill System 的公开类：IllusionAiModeEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class IllusionAiModeEffect : IBattleEffect
{
    private readonly Player? _owner;

    /// <summary>
    /// Skill System 的公开入口：IllusionAiModeEffect。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public IllusionAiModeEffect(Player? owner = null) => _owner = owner;

    public TriggerTiming Timing   => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null) return;
        var owner = _owner
            ?? (damage.Target is EnemyInstance targetEnemy && targetEnemy.HasSkill(SkillIds.Illusion)
                ? targetEnemy
                : damage.Source is EnemyInstance sourceEnemy && sourceEnemy.HasSkill(SkillIds.Illusion)
                    ? sourceEnemy
                    : null);
        if (owner is not EnemyInstance enemy) return;
        if (!enemy.HasSkill(SkillIds.Illusion)) return;

        // 无论伤害是否被幻象免疫，只要 Boss 被杀系攻击，就立即转为攻击倾向。
        if (damage.Target == owner && HuanXiangKeys.IsTrackableAttack(damage))
        {
            enemy.RuntimeStates[HuanXiangKeys.AiMode] = HuanXiangKeys.AiModeAttack;
            context.AddTriggerLog(Localization.Get("battle.skill.illusion.ai_attack_mode"));
            return;
        }

        if (damage.Source == owner && damage.ActualDamageDealt > 0)
        {
            enemy.RuntimeStates[HuanXiangKeys.AiMode] = HuanXiangKeys.AiModeDefend;
            context.AddTriggerLog(Localization.Get("battle.skill.illusion.ai_defend_mode"));
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 粘液技能 ISkillEffect 入口
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Skill System 的公开类：SlimeSkillEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class SlimeSkillEffect : ISkillEffect
{
    public string SkillId => SkillIds.Slime;

    /// <summary>
    /// Skill System 的公开入口：Register。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Register(TriggerManager triggerManager, Player owner)
    {
        triggerManager.Register(new SlimeBlockDetectEffect(owner));
        triggerManager.Register(new SlimeWeakenEffect(owner));
    }
}

// OnDamageTaken / Lowest：玩家杀系攻击被完全抵挡时（ActualDamageDealt==0），给玩家施加粘液虚弱
/// <summary>
/// Skill System 的公开类：SlimeBlockDetectEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class SlimeBlockDetectEffect : IBattleEffect
{
    private readonly Player _owner;

    /// <summary>
    /// Skill System 的公开入口：SlimeBlockDetectEffect。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public SlimeBlockDetectEffect(Player owner) => _owner = owner;

    public TriggerTiming Timing   => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Target != _owner) return;
        if (_owner is not EnemyInstance enemy) return;
        if (!enemy.HasSkill(SkillIds.Slime)) return;
        if (!BattleRules.IsShaAttack(damage.AttackType)) return;
        if (damage.Source.Team != BattleTeam.Player) return;
        if (damage.ActualDamageDealt > 0) return;

        if (context.Player.IsCombatDebuffImmune)
        {
            context.AddTriggerLog("[泉水精华] 免疫粘液虚弱。");
            return;
        }

        // 不叠加
        if (context.Player.RuntimeStates.TryGetValue(HuanXiangKeys.SlimeDebuff, out var existing) && existing is true)
            return;

        context.Player.RuntimeStates[HuanXiangKeys.SlimeDebuff] = true;
        context.RoundResult.AddLine(Localization.GetFmt("battle.skill.slime.block_fmt", enemy.DisplayName));
        context.AddTriggerLog(Localization.Get("battle.skill.slime.applied_log"));
    }
}

// OnDamage / High：玩家使用攻击牌时若携带粘液虚弱，×0.5 并移除
/// <summary>
/// Skill System 的公开类：SlimeWeakenEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class SlimeWeakenEffect : IBattleEffect
{
    private readonly Player _owner;

    /// <summary>
    /// Skill System 的公开入口：SlimeWeakenEffect。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public SlimeWeakenEffect(Player owner) => _owner = owner;

    public TriggerTiming Timing   => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.OnlyAllowCounterOrCardShields) return;
        if (damage.Source != context.Player) return;
        if (!BattleRules.IsShaAttack(damage.AttackType)) return;

        if (!context.Player.RuntimeStates.TryGetValue(HuanXiangKeys.SlimeDebuff, out var debuffVal)
            || debuffVal is not true)
            return;

        context.Player.RuntimeStates.Remove(HuanXiangKeys.SlimeDebuff);
        damage.AddModifier(new DamageModifier(
            "粘液虚弱",
            DamageModifierPriority.VulnerableOrReduction,
            DamageModifierOperation.MultiplyFloat,
            0.5));
        context.RoundResult.AddLine(Localization.Get("battle.skill.slime.weakened"));
        context.AddTriggerLog(Localization.Get("battle.skill.slime.consumed_log"));
    }
}
