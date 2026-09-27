//////////////////////////////////////////////////////////
// 文件：Scripts/TriggerManager.cs
//
// 模块：Trigger System
//
// 职责：
// 1. 承载触发时机、触发队列与优先级执行相关代码。
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
using System.Linq;

/// <summary>
/// Trigger System 的公开接口：IBattleEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public interface IBattleEffect
{
    EffectPriority Priority { get; }
    TriggerTiming Timing { get; }
    void Execute(BattleContext context);
}

/// <summary>
/// Trigger System 的公开类：EffectQueue。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EffectQueue
{
    private readonly List<IBattleEffect> _effects = new();
    private readonly TriggerTiming _timing;

    /// <summary>
    /// Trigger System 的公开入口：EffectQueue。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public EffectQueue(TriggerTiming timing)
    {
        _timing = timing;
    }

    /// <summary>
    /// Trigger System 的公开入口：Enqueue。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Enqueue(IEnumerable<IBattleEffect> effects)
    {
        _effects.AddRange(effects);
    }

    /// <summary>
    /// Trigger System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        // ======================================================
        // Trigger 优先级
        // ======================================================
        // 所有技能、装备、Buff 都先进入同一条 EffectQueue，再按 EffectPriority 排序执行。
        // 这样可以避免“谁先注册谁先触发”的隐式规则扩散到各个系统里。
        //
        // 设计约束：
        // - 新效果只声明 Timing / Priority，不直接调用其它效果。
        // - Debug 日志记录最终顺序，方便排查复杂连锁。
        // - 队列本身不理解技能或装备含义，只负责调度。
        var orderedEffects = _effects
            .OrderBy(effect => effect.Priority)
            .ToList();

        context.RecordTriggerPhase(_timing);
        for (var index = 0; index < orderedEffects.Count; index++)
        {
            var effect = orderedEffects[index];
            context.RecordEffectQueued(_timing, effect, index);
            var damageBefore = context.DamageEvent?.Amount;
            var targetBefore = context.DamageEvent?.Target;
            var hpBefore = targetBefore?.CurrentHP;
            try
            {
                effect.Execute(context);
                context.RecordEffectExecuted(
                    _timing,
                    effect,
                    damageBefore,
                    context.DamageEvent?.Amount,
                    hpBefore,
                    targetBefore?.CurrentHP,
                    "Executed");
            }
            catch (System.Exception exception)
            {
                context.RecordEffectExecuted(
                    _timing,
                    effect,
                    damageBefore,
                    context.DamageEvent?.Amount,
                    hpBefore,
                    targetBefore?.CurrentHP,
                    $"Error: {exception.GetType().Name}");
                throw;
            }
        }

        context.AddEffectDebugLog(_timing, orderedEffects);
    }
}

/// <summary>
/// Trigger System 的公开类：TriggerManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class TriggerManager
{
    private readonly List<IBattleEffect> _effects = new();

    /// <summary>
    /// Trigger System 的公开入口：Register。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Register(IBattleEffect effect)
    {
        _effects.Add(effect);
    }

    /// <summary>
    /// Trigger System 的公开入口：RaiseTrigger。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void RaiseTrigger(TriggerTiming timing, BattleContext context)
    {
        var previousChainId = context.CurrentResolutionChainId;
        var previousTiming = context.CurrentTriggerTiming;
        context.CurrentResolutionChainId = context.CreateResolutionChainId();
        context.CurrentTriggerTiming = timing;
        var queue = new EffectQueue(timing);
        queue.Enqueue(_effects.Where(effect => effect.Timing == timing));
        try
        {
            queue.Execute(context);
        }
        finally
        {
            context.CurrentResolutionChainId = previousChainId;
            context.CurrentTriggerTiming = previousTiming;
        }
    }
}
