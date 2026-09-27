//////////////////////////////////////////////////////////
// 文件：Scripts/SkillEffects/RuYingSuiXingEffect.cs
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

// 如影随行（黄月英专属）ISkillEffect 注册存根。
// 效果一：无法获得额外最大生命值 → 由 GameManager.AddMaxHp 检查实现。
// 效果二：扣血无效（含当前生命值与最大生命值）→ 由 GameManager.AddCurrentHp / AddMaxHp 检查实现。
// 效果三（下方）：场上任意单位受到实际伤害时，全场费用设为1。
// 效果四（下方）：伤害被护盾抵消，+1费。
/// <summary>
/// Skill Effect System 的公开类：RuYingSuiXingEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RuYingSuiXingEffect : ISkillEffect
{
    public string SkillId => SkillIds.RuYingSuiXing;
    /// <summary>
    /// Skill Effect System 的公开入口：Register。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Register(TriggerManager triggerManager, Player owner) { }
}

// 如影随行：每回合开始时重置首次伤害触发标记（OnTurnStart/High）。
/// <summary>
/// Skill Effect System 的公开类：RuYingSuiXingTurnResetEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RuYingSuiXingTurnResetEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (!context.Player.HasSkill(SkillIds.RuYingSuiXing)) return;
        context.Player.ResetYueYingCostResetPerTurn();
    }
}

// 如影随行效果三：场上任意单位受到实际伤害时，标记本轮结束前全场费用设为1（OnDamageTaken/Low）。
// 护盾完全抵消、无懈取消、无敌免疫等 ActualDamageDealt=0 的情况不算“受伤”。
/// <summary>
/// Skill Effect System 的公开类：RuYingSuiXingCostResetEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RuYingSuiXingCostResetEffect : IBattleEffect
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
        if (!context.Player.HasSkill(SkillIds.RuYingSuiXing)) return;
        if (context.GameOver) return;

        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled) return;
        if (damage.ActualDamageDealt <= 0) return;

        context.RuYingSuiXingResourceResetPending = true;
        context.AddTriggerLog("[如影随行]");
        context.AddTriggerLog("Trigger: OnDamageTaken / Low");
        context.AddTriggerLog("场上发生实际伤害：等待战斗动作结算后全场费用设为1。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.RuYingSuiXing, Timing, Priority, "mana_reset");
    }
}

// 如影随行效果四：玩家攻击被护盾完全抵消时，获得1费（OnDamageTaken/Lowest）。
// 触发条件由 DefenseBeforeDamageEffect 和 RendeShieldAbsorbEffect 设置 PlayerAttackAbsorbedByShield 标记。
/// <summary>
/// Skill Effect System 的公开类：RuYingSuiXingShieldGainEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RuYingSuiXingShieldGainEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (!context.Player.HasSkill(SkillIds.RuYingSuiXing)) return;
        if (!context.PlayerAttackAbsorbedByShield) return;
        if (context.GameOver) return;

        context.PlayerAttackAbsorbedByShield = false;
        context.Player.GainMana(1);
        context.RoundResult.AddLine("如影随行：攻击被护盾抵消，获得1费。");
        context.AddTriggerLog("[如影随行]");
        context.AddTriggerLog("Trigger: OnDamageTaken / Lowest");
        context.AddTriggerLog("护盾抵消：黄月英获得1费。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.RuYingSuiXing, Timing, Priority, "shield_mana");
    }
}
