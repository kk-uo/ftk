//////////////////////////////////////////////////////////
// 文件：Scripts/Reaction.cs
//
// 模块：Core System
//
// 职责：
// 1. 承载核心数据结构、通用规则与跨模块协作相关代码。
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

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Core System 的公开接口：IReaction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public interface IReaction
{
    string Id { get; }
    TriggerTiming Timing { get; }
    EffectPriority Priority { get; }
    string Title { get; }
    List<ReactionOption> Options { get; }
}

/// <summary>
/// 需要在玩家完成反应选择后再继续濒死结算的反应。
///
/// 伤害系统只识别这个通用契约，不依赖具体技能；选择结束后仍重新进入统一
/// OnDying Trigger，保证桃、酒、复活技能和最终死亡顺序保持不变。
/// </summary>
public interface IDeferredDyingReaction : IReaction
{
    BattleUnit Target { get; }
    DamageEvent TriggeringDamage { get; }
}

/// <summary>
/// Core System 的公开类：ReactionIds。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class ReactionIds
{
    public const string Longdan = "longdan";
    public const string JiGu = "jigu";
    public const string BreakArmy = "break_army";
}

/// <summary>
/// Core System 的公开类：ReactionOption。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ReactionOption
{
    /// <summary>
    /// Core System 的公开入口：ReactionOption。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public ReactionOption(string id, string text, bool enabled, Action<BattleContext> resolve, CardType? cardType = null, double? reactionCost = null)
    {
        Id = id;
        Text = text;
        Enabled = enabled;
        Resolve = resolve;
        CardType = cardType;
        ReactionCost = reactionCost;
    }

    public string Id { get; }
    public string Text { get; }
    public bool Enabled { get; }
    public Action<BattleContext> Resolve { get; }
    public CardType? CardType { get; }
    public double? ReactionCost { get; }
    public string DisplayText => Text;
}

/// <summary>
/// Core System 的公开类：ReactionQueue。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ReactionQueue
{
    private readonly List<IReaction> _reactions = new();

    public bool HasPending => _reactions.Count > 0;

    /// <summary>
    /// 判断指定单位是否正在等待一个必须先于濒死结算完成的反应。
    /// </summary>
    public bool HasDeferredDyingReaction(BattleUnit target)
    {
        return _reactions.Any(reaction =>
            reaction is IDeferredDyingReaction deferred
            && ReferenceEquals(deferred.Target, target));
    }

    /// <summary>
    /// Core System 的公开入口：Enqueue。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Enqueue(IReaction reaction)
    {
        _reactions.Add(reaction);
    }

    /// <summary>
    /// Core System 的公开入口：Dequeue。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public IReaction? Dequeue()
    {
        if (_reactions.Count == 0)
        {
            return null;
        }

        var reaction = _reactions
            .OrderBy(item => item.Priority)
            .First();
        _reactions.Remove(reaction);
        return reaction;
    }

    /// <summary>
    /// Core System 的公开入口：Clear。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Clear()
    {
        _reactions.Clear();
    }
}
