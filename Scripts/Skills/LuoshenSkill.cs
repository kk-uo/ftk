//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/LuoshenSkill.cs
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

public sealed class LuoshenEffect : ISkillEffect
{
    public string SkillId => SkillIds.Luoshen;

    /// <summary>
    /// Skill System 的公开入口：Register。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Register(TriggerManager triggerManager, Player owner)
    {
        triggerManager.Register(new LuoshenDecayEffect(owner));
    }
}

/// <summary>
/// Skill System 的公开类：LuoshenDecayEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class LuoshenDecayEffect : IBattleEffect
{
    // 默认触发器在创建时尚未有具体玩家；保留可选 owner 仅兼容旧的技能注册入口。
    private readonly Player? _owner;

    /// <summary>
    /// Skill System 的公开入口：LuoshenDecayEffect。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public LuoshenDecayEffect(Player? owner = null)
    {
        _owner = owner;
    }

    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var owner = _owner ?? context.Player;
        if (!owner.HasSkill(SkillIds.Luoshen) || owner.IsDead || context.GameOver)
            return;

        var (before, after, basis) = owner.LuoshenApplyDecay();
        if (before == after) return;

        context.AddTriggerLog("[洛神]");
        context.AddTriggerLog($"上回合未出费（衰减基准{BattleRules.FormatMana(basis)}费）：" +
            $"{BattleRules.FormatMana(before)} → {BattleRules.FormatMana(after)}。");
        context.RoundResult.AddLine($"洛神：上回合未使用费，当前费用{BattleRules.FormatMana(before)} → {BattleRules.FormatMana(after)}。");
        context.ReportPlayerCharacterSkillTriggered(
            owner, SkillIds.Luoshen, Timing, Priority, "mana_decay");
    }
}
