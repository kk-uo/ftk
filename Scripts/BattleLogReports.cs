//////////////////////////////////////////////////////////
// 文件：Scripts/BattleLogReports.cs
//
// 模块：Battle Log System
//
// 职责：
// 1. 定义 Run、Battle、Round、Phase 四级战斗报告。
// 2. 保存统一 BattleLogEntry 引用，供不同日志界面投影。
// 3. 提供日志范围与开发者分类等查询概念。
//
// 不负责：
// × 生成 UI 控件。
// × 修改战斗状态。
// × 解析本地化文本推断战斗规则。
//
// 主要依赖：
// BattleLogEntry
//////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

/// <summary>
/// 统一日志事件的业务类型。
///
/// Category 负责玩家侧颜色与粗粒度过滤；Kind 负责开发报告的精确过滤。
/// </summary>
public enum BattleLogEventKind
{
    PlayerAction,
    EnemyAction,
    Reveal,
    Counter,
    CardResolution,
    Skill,
    Equipment,
    Buff,
    Damage,
    Heal,
    Shield,
    Energy,
    Status,
    RoundStart,
    RoundEnd,
    BattleStart,
    BattleEnd,
    TriggerPhase,
    EffectQueue,
    EffectExecute,
    DamagePipeline,
    AiDecision,
    Warning,
    Error,
    Debug
}

/// <summary>
/// 战斗日志查询范围。
/// </summary>
public enum BattleLogScope
{
    CurrentRound,
    CurrentBattle,
    AllRun
}

/// <summary>
/// 开发者报告的精确分类。
/// </summary>
public enum DeveloperLogFilter
{
    All,
    Trigger,
    Damage,
    Skill,
    Equipment,
    Buff,
    Ai,
    Warning,
    Error
}

/// <summary>
/// 单个阶段的日志节点。
/// </summary>
public sealed class BattlePhaseReport
{
    private readonly List<BattleLogEntry> _entries = new();

    /// <summary>
    /// 创建阶段报告。
    /// </summary>
    public BattlePhaseReport(string phase)
    {
        Phase = phase;
    }

    public string Phase { get; }
    public IReadOnlyList<BattleLogEntry> Entries => _entries;

    /// <summary>
    /// 将统一事件引用加入当前阶段。
    /// </summary>
    internal void Add(BattleLogEntry entry)
    {
        _entries.Add(entry);
    }
}

/// <summary>
/// 单个回合的日志节点。
/// </summary>
public sealed class BattleRoundReport
{
    private readonly List<BattlePhaseReport> _phases = new();
    private readonly Dictionary<string, BattlePhaseReport> _phaseByName = new(StringComparer.Ordinal);

    /// <summary>
    /// 创建回合报告。
    /// </summary>
    public BattleRoundReport(int round)
    {
        Round = round;
    }

    public int Round { get; }
    public IReadOnlyList<BattlePhaseReport> Phases => _phases;

    /// <summary>
    /// 获取或创建阶段节点，并保持阶段首次出现的顺序。
    /// </summary>
    internal BattlePhaseReport GetOrCreatePhase(string phase)
    {
        var key = string.IsNullOrWhiteSpace(phase) ? "Unknown" : phase;
        if (_phaseByName.TryGetValue(key, out var report))
        {
            return report;
        }

        report = new BattlePhaseReport(key);
        _phaseByName.Add(key, report);
        _phases.Add(report);
        return report;
    }
}

/// <summary>
/// 单场战斗的日志节点。
/// </summary>
public sealed class BattleReport
{
    private readonly List<BattleRoundReport> _rounds = new();
    private readonly Dictionary<int, BattleRoundReport> _roundByNumber = new();
    private readonly List<BattleLogEntry> _entries = new();

    /// <summary>
    /// 创建战斗报告。
    /// </summary>
    public BattleReport(int battleId, string encounterId)
    {
        BattleId = battleId;
        EncounterId = encounterId;
        StartedAt = DateTime.UtcNow;
    }

    public int BattleId { get; }
    public string EncounterId { get; }
    public DateTime StartedAt { get; }
    public DateTime? EndedAt { get; internal set; }
    public string Result { get; internal set; } = string.Empty;
    public IReadOnlyList<BattleRoundReport> Rounds => _rounds;
    public IReadOnlyList<BattleLogEntry> Entries => _entries;

    /// <summary>
    /// 将事件加入战斗，并建立对应的回合和阶段索引。
    /// </summary>
    internal void Add(BattleLogEntry entry)
    {
        _entries.Add(entry);
        if (!_roundByNumber.TryGetValue(entry.Round, out var round))
        {
            round = new BattleRoundReport(entry.Round);
            _roundByNumber.Add(entry.Round, round);
            _rounds.Add(round);
        }

        round.GetOrCreatePhase(entry.Phase).Add(entry);
    }
}

/// <summary>
/// 当前 Run 的完整战斗报告。
///
/// BattleManager 场景销毁后该对象仍由 BattleLogService 持有，直到新 Run 开始。
/// </summary>
public sealed class BattleRunReport
{
    private readonly List<BattleReport> _battles = new();

    /// <summary>
    /// 创建 Run 报告。
    /// </summary>
    public BattleRunReport(int runId)
    {
        RunId = runId;
        StartedAt = DateTime.UtcNow;
    }

    public int RunId { get; }
    public DateTime StartedAt { get; }
    public IReadOnlyList<BattleReport> Battles => _battles;

    /// <summary>
    /// 添加一场战斗。
    /// </summary>
    internal void Add(BattleReport battle)
    {
        _battles.Add(battle);
    }
}
