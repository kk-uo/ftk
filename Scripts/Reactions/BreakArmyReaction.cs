//////////////////////////////////////////////////////////
// 文件：Scripts/Reactions/BreakArmyReaction.cs
//
// 模块：Reaction System
//
// 职责：
// 1. 承载实时反应窗口与响应式出牌相关代码。
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

using System.Collections.Generic;

/// <summary>
/// Reaction System 的公开类：BreakArmyReaction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BreakArmyReaction : IReaction
{
    private const double ManaCostPerTrigger = 0.5;
    private readonly int _baseDamage;
    private readonly CardType _attackType;
    private readonly Player _target;
    private readonly int _currentMultiplier;

    /// <summary>
    /// Reaction System 的公开入口：BreakArmyReaction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public BreakArmyReaction(double currentMana, int baseDamage, CardType attackType, Player target, int currentMultiplier = 1)
    {
        _baseDamage = baseDamage;
        _attackType = attackType;
        _target = target;
        _currentMultiplier = currentMultiplier;

        var canSpend = currentMana >= ManaCostPerTrigger;
        Options = new List<ReactionOption>
        {
            new("spend", canSpend
                ? Localization.Get("reaction.pojun.add_mana")
                : Localization.GetFmt("reaction.option.insufficient_cost_fmt", Localization.Get("reaction.pojun.add_mana")),
                canSpend, ResolveSpend, CardType.PoJunBoost, ManaCostPerTrigger),
            new("pass", Localization.Get("reaction.option.finish"), true, ResolveStop)
        };
    }

    public string Id => ReactionIds.BreakArmy;
    // 破军是命中后的即时追击，窗口在 OnBattlePhase 内、普通收尾前显示。
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.High;
    public string Title => Localization.Get("reaction.pojun.title");
    public List<ReactionOption> Options { get; }
    public int CurrentMultiplier => _currentMultiplier;
    public int BaseDamage => _baseDamage;

    private static bool IsTargetDead(Player target) => target is EnemyInstance ei && ei.IsDead;

    private void ResolveSpend(BattleContext context)
    {
        if (IsTargetDead(_target) || context.GameOver) return;

        BattleRules.PayManaAndRaiseResourceChanged(context, context.Player, ManaCostPerTrigger, true);
        if (context.GameOver || context.Player.IsDead || IsTargetDead(_target)) return;
        ResolveAdditionalOriginalDamage(context);

        context.RoundResult.AddLine($"破军（×{_currentMultiplier + 1}）：失去0.5费，追加{_baseDamage}点{BattleRules.GetCardName(_attackType)}原始伤害。");
        context.AddTriggerLog($"[破军] 追加伤害 ×{_currentMultiplier + 1}");

        if (context.GameOver || IsTargetDead(_target)) return;

        var newMana = context.Player.CurrentMana;
        if (newMana >= ManaCostPerTrigger)
            context.Reactions.Enqueue(new BreakArmyReaction(newMana, _baseDamage, _attackType, _target, _currentMultiplier + 1));
    }

    private void ResolveAdditionalOriginalDamage(BattleContext context)
    {
        // 破军是“同一张杀增加一份已经命中的原始伤害”，不是重新打出一张普通杀。
        // 因此不重新经过闪/护盾、属性改写、装备加伤或命中判定；它只把上一次已实际
        // 造成的伤害追加给同一目标，保留原杀的招式类型用于日志和后续命中类效果。
        var previousDamage = context.DamageEvent;
        context.IgnorePoJunCapture = true;
        try
        {
            context.DamageEvent = new DamageEvent(
                context.Player,
                _target,
                _attackType,
                _baseDamage,
                origin: new HealthChangeSource(HealthChangeSourceKind.Skill, "破军", SkillIds.PoJun, context.Player));
            context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
        }
        finally
        {
            context.DamageEvent = previousDamage;
            context.IgnorePoJunCapture = false;
        }
    }

    private void ResolveStop(BattleContext context)
    {
        context.RoundResult.AddLine($"破军结束（累计×{_currentMultiplier}）。");
        context.AddTriggerLog($"[破军] 结束，最终倍率×{_currentMultiplier}");
    }
}
