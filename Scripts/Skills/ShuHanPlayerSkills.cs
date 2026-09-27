//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/ShuHanPlayerSkills.cs
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

// ────────────────────────────────────────────────────────────────────────────
// 刘备/关羽（玩家可选角色）专属技能效果：仁德 / 桃园结义 / 武圣 / 义绝
//
// Scripts/Skills/ShuHanBossSkills.cs 里的同名效果是为"蜀汉共生体"Boss（多个
// EnemyInstance + 共享生命池）设计的，内部全部显式 as EnemyInstance / 遍历
// context.Encounter.Enemies，玩家角色持有时这些效果恒为空转。这里为玩家角色
// 重新实现同名技能，语义对齐但适配单体玩家：
// - 仁德：与 Boss 版一致（每回合开始获得2层护盾，每层抵挡一次完整伤害）。
// - 桃园结义：Boss 版基于"共享生命池首次<150回100"，数值是多个敌人共用的
//   血量池规模，与玩家角色的单体血量（40上限）不成比例，因此按比例重新设计：
//   本场战斗首次生命值降至最大生命值40%以下时，回复最大生命值25%（仅一次）。
// - 武圣：与 Boss 版一致（杀系攻击基础伤害+5）。
// - 义绝：Boss 版是"若玩家上回合受伤，则本回合杀系伤害×2"（从敌方视角设计，
//   惩罚受伤的玩家）；玩家侧对称地改为"若敌方上回合受到过伤害，则本回合杀系
//   伤害×2"（延续"痛击新负伤之敌"的立意）。
// ────────────────────────────────────────────────────────────────────────────

// 仁德（玩家侧）：每回合开始时，若玩家持有仁德，刷新2层本回合护盾。
/// <summary>
/// Skill System 的公开类：RendePlayerShieldReplenishEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RendePlayerShieldReplenishEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.Player.IsDead || !context.Player.HasSkill(SkillIds.Rende)) return;

        // 仁德的“两层”是技能本身的层数规则，不属于可被阵营增幅翻倍的数值奖励。
        // 每回合覆盖旧值，避免上一回合未消耗的护盾累积。
        const int perTurnGain = 2;
        context.Player.RuntimeStates["rende_shield_player"] = perTurnGain;
        context.RoundResult.AddLine($"仁德：{context.Player.DisplayName}获得{perTurnGain}层本回合护盾。");
        context.AddTriggerLog($"[仁德] 本回合护盾刷新为{perTurnGain}层。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.Rende, Timing, Priority, "shield_gain");
    }
}

// 仁德（玩家侧）护盾吸收：玩家受到伤害时，一层抵挡一次完整伤害。
/// <summary>
/// Skill System 的公开类：RendePlayerShieldAbsorbEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RendePlayerShieldAbsorbEffect : IBattleEffect
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
        if (damage == null || damage.Cancelled || damage.OnlyAllowCounterOrCardShields) return;
        if (damage.Target != context.Player || !context.Player.HasSkill(SkillIds.Rende)) return;
        if (!context.Player.RuntimeStates.TryGetValue("rende_shield_player", out var shieldObj)) return;
        var shield = (int)shieldObj;
        if (shield <= 0) return;

        if (damage.BaseAmount <= 0) return;

        context.Player.RuntimeStates["rende_shield_player"] = shield - 1;
        damage.CancelAsFullyBlocked();
        context.RoundResult.AddLine($"仁德护盾：抵挡本次伤害（剩余{shield - 1}层）。");
        context.AddTriggerLog($"[仁德护盾] 抵挡一次完整伤害，消耗1层，剩余{shield - 1}层。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.Rende, Timing, Priority, "shield_block");
    }
}

// 桃园结义（玩家侧）：本场战斗首次生命值降至最大生命值40%以下时，回复最大生命值25%（仅一次）。
/// <summary>
/// Skill System 的公开类：TaoyuanJiyiPlayerEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class TaoyuanJiyiPlayerEffect : IBattleEffect
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
        if (damage == null || damage.Cancelled || damage.Amount <= 0) return;
        if (damage.Target != context.Player || !context.Player.HasSkill(SkillIds.TaoyuanJiyi)) return;
        if (context.Player.RuntimeStates.TryGetValue("taoyuan_jiyi_player_triggered", out var flag) && (bool)flag) return;

        var threshold = System.Math.Ceiling(context.Player.MaxHealth * 0.4);
        if (context.Player.Health >= threshold) return;

        context.Player.RuntimeStates["taoyuan_jiyi_player_triggered"] = true;
        var healAmount = (int)System.Math.Ceiling(context.Player.MaxHealth * 0.25);
        var healed = BattleHealing.Apply(
            context,
            context.Player,
            healAmount,
            false,
            new HealthChangeSource(HealthChangeSourceKind.Skill, "桃园结义", SkillIds.TaoyuanJiyi, context.Player)).HealedAmount;
        context.RoundResult.AddLine($"桃园结义：三兄弟齐心，{context.Player.DisplayName}恢复{healed}点生命！");
        context.AddTriggerLog($"[桃园结义] 生命值<{threshold}，回复{healed}。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.TaoyuanJiyi, Timing, Priority, "heal");
    }
}

// 武圣（玩家侧）：玩家的杀系攻击基础伤害额外+5。
/// <summary>
/// Skill System 的公开类：WushengPlayerEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class WushengPlayerEffect : IBattleEffect
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
        if (damage.Source != context.Player || !context.Player.HasSkill(SkillIds.Wusheng)) return;

        damage.AddModifier(new DamageModifier(
            "武圣",
            DamageModifierPriority.FlatBonus,
            DamageModifierOperation.Add,
            5));
        context.AddTriggerLog("[武圣] Kill +5。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.Wusheng, Timing, Priority, "damage_bonus");
    }
}

// 义绝（玩家侧）：若敌方上回合受到过伤害，玩家本回合杀系伤害×2。
/// <summary>
/// Skill System 的公开类：YijuePlayerDamageEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class YijuePlayerDamageEffect : IBattleEffect
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
        if (damage.Source != context.Player || !context.Player.HasSkill(SkillIds.Yijue)) return;
        if (!context.AnyEnemyHitLastRound) return;

        damage.AddModifier(new DamageModifier(
            "义绝",
            DamageModifierPriority.SpecialMultiplier,
            DamageModifierOperation.Multiply,
            2));
        context.RoundResult.AddLine($"义绝：敌方上回合受伤，{context.Player.DisplayName}Kill伤害×2！");
        context.AddTriggerLog("[义绝] ×2。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.Yijue, Timing, Priority, "damage_bonus");
    }
}

// 义绝（玩家侧）追踪：任意敌人受到伤害时，标记本回合已发生。
/// <summary>
/// Skill System 的公开类：YijuePlayerTrackEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class YijuePlayerTrackEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Amount <= 0) return;
        if (damage.Target is not EnemyInstance) return;
        context.AnyEnemyHitThisRound = true;
    }
}

// 义绝（玩家侧）回合切换：回合结束时把"本回合"标记转移为"上回合"。
/// <summary>
/// Skill System 的公开类：YijuePlayerTurnShiftEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class YijuePlayerTurnShiftEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBattlePostPhase;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (!context.Player.HasSkill(SkillIds.Yijue)) return;

        context.AnyEnemyHitLastRound = context.AnyEnemyHitThisRound;
        context.AnyEnemyHitThisRound = false;
    }
}
