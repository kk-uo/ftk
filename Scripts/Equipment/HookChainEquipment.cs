//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/HookChainEquipment.cs
//
// 模块：Equipment System
//
// 职责：
// 1. 承载装备触发效果与装备战斗逻辑相关代码。
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

// 钩索（稀有·武器）
// 连续使用两张杀系牌后，若两张牌均未命中，则立刻使双方各受到5点真实伤害。
// 仅统计连续打出的杀系牌；一旦打出非杀系牌，连续计数立即重置。
/// <summary>
/// Equipment System 的公开类：HookChainEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class HookChainEffect : IBattleEffect
{
    private const int HookThreshold = 2;
    private const int HookDamage = 5;
    private readonly Dictionary<string, int> _consecutiveMisses = new();
    private readonly Dictionary<string, bool> _currentActionHasHit = new();

    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || !BattleRules.IsShaAttack(damage.AttackType))
        {
            return;
        }

        var source = damage.Source;
        if (!SourceHasHookChain(context, source))
        {
            return;
        }

        var sourceKey = BattleContext.GetUnitStateKey(source);
        if (!damage.Cancelled && damage.ActualDamageDealt > 0)
        {
            _currentActionHasHit[sourceKey] = true;
        }
        else if (!_currentActionHasHit.ContainsKey(sourceKey))
        {
            _currentActionHasHit[sourceKey] = false;
        }
    }

    /// <summary>
    /// 在本轮完整行动结束后，按“实际打出的牌数”结算钩索连续未命中。
    ///
    /// 多目标攻击会为同一张牌生成多个 DamageEvent；若在 OnDamageTaken 中直接累加，
    /// 一张范围杀可能被误算成连续两张牌。因此伤害阶段只记录是否至少命中过目标，
    /// 到 BattleEnd 再依据 BattleAction.Count 统一推进连续计数。
    /// </summary>
    public void FinalizeAction(BattleContext context, Player source, BattleAction? action, Player? target)
    {
        var sourceKey = BattleContext.GetUnitStateKey(source);
        if (!SourceHasHookChain(context, source))
        {
            Reset(sourceKey);
            return;
        }

        if (action == null || !BattleRules.IsShaAttack(action.Type))
        {
            var hadPendingChain = HasPendingMissChain(sourceKey);
            Reset(sourceKey);
            if (hadPendingChain)
            {
                context.AddTriggerLog($"[钩索] {source.DisplayName} 本回合未连续打出杀系牌，计数重置");
            }
            return;
        }

        var actionHit = _currentActionHasHit.TryGetValue(sourceKey, out var hit) && hit;
        _currentActionHasHit.Remove(sourceKey);
        if (actionHit)
        {
            if (HasPendingMissChain(sourceKey))
            {
                _consecutiveMisses[sourceKey] = 0;
                context.AddTriggerLog($"[钩索] {source.DisplayName} 杀系牌命中，连续未命中计数重置");
            }
            return;
        }

        var currentMisses = _consecutiveMisses.TryGetValue(sourceKey, out var pending) ? pending : 0;
        currentMisses += System.Math.Max(1, action.Count);
        context.AddTriggerLog($"[钩索] {source.DisplayName} 连续未命中 {currentMisses}/{HookThreshold}");

        while (currentMisses >= HookThreshold && !context.GameOver && target != null)
        {
            currentMisses -= HookThreshold;
            ResolveHookChainTrueDamage(context, source, target);
        }

        _consecutiveMisses[sourceKey] = currentMisses;
    }

    private static bool SourceHasHookChain(BattleContext context, Player source)
    {
        return source == context.Player
            ? GameManager.HasEquipment(EquipmentIds.HookChain)
            : source is EnemyInstance enemy && enemy.HasEquipment(EquipmentIds.HookChain);
    }

    private static void ResolveHookChainTrueDamage(BattleContext context, Player source, Player target)
    {
        context.RoundResult.AddLine($"钩索触发：{source.DisplayName}连续两张杀系牌均未命中，双方各受到{HookDamage}点真实伤害。");
        context.AddTriggerLog("[Equipment]");
        context.AddTriggerLog($"钩索：{source.DisplayName}与{target.DisplayName}各受{HookDamage}点真实伤害。");

        source.TakeDamage(HookDamage);
        target.TakeDamage(HookDamage);

        ResolveDyingIfNeeded(context, target, source, CardType.Kill);
        ResolveDyingIfNeeded(context, source, source, CardType.Kill);
    }

    private static void ResolveDyingIfNeeded(BattleContext context, Player damagedUnit, Player source, CardType attackType)
    {
        if (damagedUnit.Health > 0 || context.GameOver)
        {
            return;
        }

        var savedEvent = context.DamageEvent;
        context.DamageEvent = new DamageEvent(source, damagedUnit, attackType, HookDamage);
        context.RaiseOnDying();
        context.DamageEvent = savedEvent;
    }

    /// <summary>
    /// Equipment System 的公开入口：HasPendingMissChain。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public bool HasPendingMissChain(string sourceId)
    {
        return _consecutiveMisses.TryGetValue(sourceId, out var count) && count > 0;
    }

    /// <summary>
    /// Equipment System 的公开入口：Reset。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Reset(string sourceId)
    {
        _consecutiveMisses[sourceId] = 0;
        _currentActionHasHit.Remove(sourceId);
    }
}

/// <summary>
/// Equipment System 的公开类：HookChainResetEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class HookChainResetEffect : IBattleEffect
{
    private readonly HookChainEffect _tracker;

    /// <summary>
    /// Equipment System 的公开入口：HookChainResetEffect。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public HookChainResetEffect(HookChainEffect tracker)
    {
        _tracker = tracker;
    }

    public TriggerTiming Timing => TriggerTiming.OnBattleEnd;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        _tracker.FinalizeAction(
            context,
            context.Player,
            context.PlayerAction,
            context.PlayerAction?.Target as Player ?? GetFirstAliveEnemy(context));

        foreach (var entry in context.EnemyActions)
        {
            _tracker.FinalizeAction(
                context,
                entry.Enemy,
                entry.Action,
                entry.Action.Target as Player ?? context.Player);
        }
    }

    private static Player? GetFirstAliveEnemy(BattleContext context)
    {
        if (context.Encounter == null)
        {
            return null;
        }

        foreach (var enemy in context.Encounter.Enemies)
        {
            if (!enemy.IsDead)
            {
                return enemy;
            }
        }

        return null;
    }
}
