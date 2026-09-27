//////////////////////////////////////////////////////////
// 文件：Scripts/BattleLogSystem.cs
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

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Core System 的公开枚举：BattleLogCategory。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum BattleLogCategory
{
    Action,
    Damage,
    Recover,
    Buff,
    System,
    Debug,
    Error
}

/// <summary>
/// Core System 的公开枚举：BattleLogIconType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum BattleLogIconType
{
    Action,
    Damage,
    Shield,
    Buff,
    System,
    Debug,
    Error
}

/// <summary>
/// Core System 的公开枚举：BattleLogFilter。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum BattleLogFilter
{
    All,
    Action,
    Damage,
    System,
    Debug
}

/// <summary>
/// Core System 的公开类：BattleLogEntry。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BattleLogEntry
{
    public int EventId { get; set; }
    public int BattleId { get; set; }
    public BattleLogCategory Category { get; set; }
    public BattleLogEventKind Kind { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int Round { get; set; }
    public string Phase { get; set; } = string.Empty;
    public TriggerTiming? TriggerTiming { get; set; }
    public EffectPriority? Priority { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Skill { get; set; } = string.Empty;
    public string Equipment { get; set; } = string.Empty;
    public string Card { get; set; } = string.Empty;
    public string Buff { get; set; } = string.Empty;
    public HealthChangeKind? HealthChangeKind { get; set; }
    public HealthChangeSourceKind? HealthChangeSourceKind { get; set; }
    public HealthChangeSide? SourceSide { get; set; }
    public HealthChangeSide? TargetSide { get; set; }
    public double? DamageBefore { get; set; }
    public double? DamageAfter { get; set; }
    public double? EnergyBefore { get; set; }
    public double? EnergyAfter { get; set; }
    public int? HpBefore { get; set; }
    public int? HpAfter { get; set; }
    public string Result { get; set; } = string.Empty;
    public string DebugMessage { get; set; } = string.Empty;
    public string TextKey { get; set; } = string.Empty;
    public string[] TextArgs { get; set; } = Array.Empty<string>();
    public Color Color { get; set; }
    public BattleLogIconType IconType { get; set; }
    public bool DebugOnly { get; set; }
    public int StackCount { get; set; } = 1;

    /// <summary>
    /// Core System 的公开入口：Matches。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public bool Matches(BattleLogEntry other)
    {
        return BattleId == other.BattleId
            && Category == other.Category
            && Round == other.Round
            && Actor == other.Actor
            && Target == other.Target
            && TextKey == other.TextKey
            && Phase == other.Phase
            && Kind == other.Kind
            && TriggerTiming == other.TriggerTiming
            && Priority == other.Priority
            && DebugOnly == other.DebugOnly
            && TextArgs.SequenceEqual(other.TextArgs);
    }

    /// <summary>
    /// Core System 的公开入口：GetText。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public string GetText()
    {
        return TextArgs.Length == 0
            ? Localization.Get(TextKey)
            : Localization.GetFmt(TextKey, TextArgs.Cast<object>().ToArray());
    }

    /// <summary>
    /// Core System 的公开入口：GetSearchText。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public string GetSearchText()
    {
        return $"{BattleId} {Round} {Phase} {TriggerTiming} {Priority} {Kind} "
            + $"{Actor} {Target} {Source} {Skill} {Equipment} {Card} {Buff} "
            + $"{Result} {DebugMessage} {GetText()}";
    }

    /// <summary>
    /// 清理对象池复用前的所有字段。
    /// </summary>
    internal void Reset()
    {
        EventId = 0;
        BattleId = 0;
        Category = BattleLogCategory.System;
        Kind = BattleLogEventKind.Debug;
        Timestamp = DateTime.UtcNow;
        Round = 0;
        Phase = string.Empty;
        TriggerTiming = null;
        Priority = null;
        Actor = string.Empty;
        Target = string.Empty;
        Source = string.Empty;
        Skill = string.Empty;
        Equipment = string.Empty;
        Card = string.Empty;
        Buff = string.Empty;
        HealthChangeKind = null;
        HealthChangeSourceKind = null;
        SourceSide = null;
        TargetSide = null;
        DamageBefore = null;
        DamageAfter = null;
        EnergyBefore = null;
        EnergyAfter = null;
        HpBefore = null;
        HpAfter = null;
        Result = string.Empty;
        DebugMessage = string.Empty;
        TextKey = string.Empty;
        TextArgs = Array.Empty<string>();
        Color = Colors.White;
        IconType = BattleLogIconType.System;
        DebugOnly = false;
        StackCount = 1;
    }
}

/// <summary>
/// Core System 的公开类：BattleLogManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BattleLogManager
{
    public const int MaxEntries = BattleLogService.MaxCombatFeedEntries;
    public BattleLogService Service => BattleLogRuntime.Service;
    public IReadOnlyList<BattleLogEntry> Entries
        => Service.CurrentBattle?.Entries ?? Array.Empty<BattleLogEntry>();

    /// <summary>
    /// Core System 的公开入口：Clear。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Clear()
    {
        Service.ClearCombatFeed();
    }

    /// <summary>
    /// 开始新战斗报告。
    /// </summary>
    public void BeginBattle(string encounterId)
    {
        Service.BeginBattle(encounterId);
    }

    /// <summary>
    /// 结束当前战斗报告。
    /// </summary>
    public void EndBattle(string result)
    {
        Service.EndBattle(result);
    }

    /// <summary>
    /// Core System 的公开入口：Add。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Add(BattleLogEntry entry)
    {
        Service.Record(entry);
    }

    /// <summary>
    /// Core System 的公开入口：AddAction。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddAction(int round, string key, params string[] args)
        => Add(Create(round, BattleLogCategory.Action, key, args));

    /// <summary>
    /// Core System 的公开入口：AddDamage。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddDamage(int round, string key, params string[] args)
        => Add(Create(round, BattleLogCategory.Damage, key, args));

    /// <summary>
    /// Core System 的公开入口：AddRecover。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddRecover(int round, string key, params string[] args)
        => Add(Create(round, BattleLogCategory.Recover, key, args));

    /// <summary>
    /// Core System 的公开入口：AddBuff。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddBuff(int round, string key, params string[] args)
        => Add(Create(round, BattleLogCategory.Buff, key, args));

    /// <summary>
    /// Core System 的公开入口：AddSystem。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddSystem(int round, string key, params string[] args)
        => Add(Create(round, BattleLogCategory.System, key, args));

    /// <summary>
    /// Core System 的公开入口：AddDebug。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddDebug(int round, string key, params string[] args)
        => Add(Create(round, BattleLogCategory.Debug, key, args, debugOnly: true));

    /// <summary>
    /// Core System 的公开入口：AddError。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void AddError(int round, string key, params string[] args)
        => Add(Create(round, BattleLogCategory.Error, key, args));

    private BattleLogEntry Create(
        int round,
        BattleLogCategory category,
        string key,
        string[] args,
        bool debugOnly = false)
    {
        var entry = Service.RentEntry();
        entry.Category = category;
        entry.Kind = GetDefaultKind(category);
        entry.Round = round;
        entry.TextKey = key;
        entry.TextArgs = args;
        entry.Color = GetColor(category);
        entry.IconType = GetIconType(category);
        entry.DebugOnly = debugOnly;
        return entry;
    }

    /// <summary>
    /// 创建可补充结构化字段的日志事件。
    /// </summary>
    public BattleLogEntry CreateEntry(
        int round,
        BattleLogCategory category,
        BattleLogEventKind kind,
        string phase,
        string key,
        params string[] args)
    {
        var entry = Create(round, category, key, args, category == BattleLogCategory.Debug);
        entry.Kind = kind;
        entry.Phase = phase;
        return entry;
    }

    private static Color GetColor(BattleLogCategory category)
        => category switch
        {
            BattleLogCategory.Action => Colors.White,
            BattleLogCategory.Damage => new Color(1.00f, 0.28f, 0.24f),
            BattleLogCategory.Recover => new Color(0.34f, 1.00f, 0.54f),
            BattleLogCategory.Buff => new Color(0.36f, 0.68f, 1.00f),
            BattleLogCategory.System => new Color(0.62f, 0.65f, 0.70f),
            BattleLogCategory.Debug => new Color(1.00f, 0.86f, 0.25f),
            BattleLogCategory.Error => new Color(1.00f, 0.52f, 0.20f),
            _ => Colors.White
        };

    private static BattleLogIconType GetIconType(BattleLogCategory category)
        => category switch
        {
            BattleLogCategory.Action => BattleLogIconType.Action,
            BattleLogCategory.Damage => BattleLogIconType.Damage,
            BattleLogCategory.Recover => BattleLogIconType.Buff,
            BattleLogCategory.Buff => BattleLogIconType.Buff,
            BattleLogCategory.System => BattleLogIconType.System,
            BattleLogCategory.Debug => BattleLogIconType.Debug,
            BattleLogCategory.Error => BattleLogIconType.Error,
            _ => BattleLogIconType.System
        };

    private static BattleLogEventKind GetDefaultKind(BattleLogCategory category)
        => category switch
        {
            BattleLogCategory.Action => BattleLogEventKind.Status,
            BattleLogCategory.Damage => BattleLogEventKind.Damage,
            BattleLogCategory.Recover => BattleLogEventKind.Heal,
            BattleLogCategory.Buff => BattleLogEventKind.Buff,
            BattleLogCategory.System => BattleLogEventKind.Status,
            BattleLogCategory.Debug => BattleLogEventKind.Debug,
            BattleLogCategory.Error => BattleLogEventKind.Error,
            _ => BattleLogEventKind.Debug
        };
}
