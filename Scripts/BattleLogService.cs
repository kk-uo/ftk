//////////////////////////////////////////////////////////
// 文件：Scripts/BattleLogService.cs
//
// 模块：Battle Log System
//
// 职责：
// 1. 接收所有战斗日志事件并建立报告层级。
// 2. 为 Combat Feed、Battle History、Developer Report 提供投影。
// 3. 在整个 Run 内保留报告，并导出 TXT / JSON。
//
// 不负责：
// × 判断卡牌克制或伤害规则。
// × 创建日志窗口。
// × 通过显示文本反推结构化字段。
//
// 主要依赖：
// BattleLogReports
// Localization
//////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Godot;

/// <summary>
/// 战斗日志统一服务。
///
/// 所有界面只读取该服务的不同投影，不维护独立日志副本。
/// </summary>
public sealed class BattleLogService
{
    public const int MaxCombatFeedEntries = 1000;
    public const int MaxRetainedRuns = 2;

    private readonly List<BattleLogEntry> _allEntries = new();
    private readonly List<BattleLogEntry> _combatFeed = new();
    private readonly List<BattleRunReport> _runs = new();
    private readonly Stack<BattleLogEntry> _entryPool = new();
    private int _nextEventId;
    private int _nextBattleId;
    private int _nextRunId;

    /// <summary>
    /// 创建服务并初始化第一个 Run 报告。
    /// </summary>
    public BattleLogService()
    {
        CurrentRun = new BattleRunReport(++_nextRunId);
        _runs.Add(CurrentRun);
    }

    public BattleRunReport CurrentRun { get; private set; }
    public BattleReport? CurrentBattle { get; private set; }
    public IReadOnlyList<BattleLogEntry> CombatFeed => _combatFeed;
    public IReadOnlyList<BattleRunReport> Runs => _runs;

    /// <summary>
    /// 开始新 Run，并仅回收超出保留上限的最旧 Run。
    ///
    /// 空 Run 不会重复创建，避免应用初始化与正式开局连续调用时产生空历史。
    /// </summary>
    public void BeginRun()
    {
        _combatFeed.Clear();
        CurrentBattle = null;
        if (CurrentRun.Battles.Count == 0)
        {
            return;
        }

        CurrentRun = new BattleRunReport(++_nextRunId);
        _runs.Add(CurrentRun);
        while (_runs.Count > MaxRetainedRuns)
        {
            RecycleRun(_runs[0]);
            _runs.RemoveAt(0);
        }
    }

    /// <summary>
    /// 开始一场新战斗。
    ///
    /// 该操作只清理实时 Feed，不删除当前 Run 已完成战斗的历史。
    /// </summary>
    public BattleReport BeginBattle(string encounterId)
    {
        _combatFeed.Clear();
        CurrentBattle = new BattleReport(++_nextBattleId, encounterId);
        CurrentRun.Add(CurrentBattle);
        return CurrentBattle;
    }

    /// <summary>
    /// 结束当前战斗并保存结果。
    /// </summary>
    public void EndBattle(string result)
    {
        if (CurrentBattle == null || CurrentBattle.EndedAt.HasValue)
        {
            return;
        }

        CurrentBattle.Result = result;
        CurrentBattle.EndedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// 从对象池取得一个空日志事件。
    ///
    /// 事件在 Run 结束前不会回收，保证所有 UI 投影引用同一个稳定对象。
    /// </summary>
    public BattleLogEntry RentEntry()
    {
        return _entryPool.Count > 0 ? _entryPool.Pop() : new BattleLogEntry();
    }

    /// <summary>
    /// 记录统一事件并同步更新报告与 Combat Feed 投影。
    /// </summary>
    public BattleLogEntry Record(BattleLogEntry entry)
    {
        EnsureBattle();
        entry.EventId = ++_nextEventId;
        entry.BattleId = CurrentBattle!.BattleId;
        entry.Timestamp = DateTime.UtcNow;
        entry.Phase = string.IsNullOrWhiteSpace(entry.Phase) ? "Unknown" : entry.Phase;

        var previous = _allEntries.Count > 0 ? _allEntries[^1] : null;
        if (previous != null && previous.Matches(entry))
        {
            previous.StackCount += 1;
            previous.Timestamp = entry.Timestamp;
            entry.Reset();
            _entryPool.Push(entry);
            return previous;
        }

        _allEntries.Add(entry);
        CurrentBattle.Add(entry);
        if (!entry.DebugOnly)
        {
            _combatFeed.Add(entry);
            while (_combatFeed.Count > MaxCombatFeedEntries)
            {
                _combatFeed.RemoveAt(0);
            }
        }

        return entry;
    }

    /// <summary>
    /// 记录带完整上下文的结构化事件。
    ///
    /// 战斗系统传入原始字段，显示文本仍通过 Localization Key 生成。
    /// </summary>
    public BattleLogEntry RecordStructured(
        BattleLogEventKind kind,
        int round,
        string phase,
        string textKey,
        string[]? textArgs = null,
        BattleLogCategory? category = null,
        bool debugOnly = false,
        TriggerTiming? timing = null,
        EffectPriority? priority = null,
        string actor = "",
        string target = "",
        string source = "",
        string skill = "",
        string equipment = "",
        string card = "",
        string buff = "",
        double? damageBefore = null,
        double? damageAfter = null,
        double? energyBefore = null,
        double? energyAfter = null,
        int? hpBefore = null,
        int? hpAfter = null,
        string result = "",
        string debugMessage = "")
    {
        var resolvedCategory = category ?? GetCategory(kind);
        var entry = RentEntry();
        entry.Kind = kind;
        entry.Category = resolvedCategory;
        entry.Round = round;
        entry.Phase = phase;
        entry.TriggerTiming = timing;
        entry.Priority = priority;
        entry.Actor = actor;
        entry.Target = target;
        entry.Source = source;
        entry.Skill = skill;
        entry.Equipment = equipment;
        entry.Card = card;
        entry.Buff = buff;
        entry.DamageBefore = damageBefore;
        entry.DamageAfter = damageAfter;
        entry.EnergyBefore = energyBefore;
        entry.EnergyAfter = energyAfter;
        entry.HpBefore = hpBefore;
        entry.HpAfter = hpAfter;
        entry.Result = result;
        entry.DebugMessage = debugMessage;
        entry.TextKey = textKey;
        entry.TextArgs = textArgs ?? Array.Empty<string>();
        entry.Color = GetColor(resolvedCategory, priority);
        entry.IconType = GetIconType(resolvedCategory);
        entry.DebugOnly = debugOnly;
        return Record(entry);
    }

    /// <summary>
    /// 清空实时 Feed，但保留 Battle History 与 Developer Report。
    /// </summary>
    public void ClearCombatFeed()
    {
        _combatFeed.Clear();
    }

    /// <summary>
    /// 按范围、模式、搜索词和开发分类查询统一事件。
    /// </summary>
    public IReadOnlyList<BattleLogEntry> Query(
        BattleLogScope scope,
        int round,
        bool developerMode,
        string query = "",
        DeveloperLogFilter developerFilter = DeveloperLogFilter.All,
        int? battleId = null,
        int? runId = null)
    {
        var selectedRun = runId.HasValue
            ? _runs.FirstOrDefault(run => run.RunId == runId.Value)
            : CurrentRun;
        var selectedBattle = battleId.HasValue
            ? _runs.SelectMany(run => run.Battles)
                .FirstOrDefault(battle => battle.BattleId == battleId.Value)
            : selectedRun == CurrentRun
                ? CurrentBattle
                : selectedRun?.Battles.LastOrDefault();
        IEnumerable<BattleLogEntry> entries = scope switch
        {
            BattleLogScope.CurrentRound => selectedBattle?.Entries.Where(entry => entry.Round == round)
                ?? Enumerable.Empty<BattleLogEntry>(),
            BattleLogScope.CurrentBattle => selectedBattle?.Entries ?? Enumerable.Empty<BattleLogEntry>(),
            BattleLogScope.AllRun => selectedRun != null && runId.HasValue
                ? selectedRun.Battles.SelectMany(battle => battle.Entries)
                : _allEntries,
            _ => _allEntries
        };

        if (!developerMode)
        {
            entries = entries.Where(entry => !entry.DebugOnly);
        }
        else
        {
            entries = entries.Where(entry => MatchesDeveloperFilter(entry, developerFilter));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            entries = entries.Where(entry =>
                entry.GetSearchText().Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return entries.ToList();
    }

    /// <summary>
    /// 判断当前战斗指定回合是否已有某类结构化事件。
    /// </summary>
    public bool HasCurrentBattleEvent(BattleLogEventKind kind, int round, string target = "")
    {
        return CurrentBattle?.Entries.Any(entry =>
            entry.Kind == kind
            && entry.Round == round
            && (string.IsNullOrWhiteSpace(target)
                || string.Equals(entry.Target, target, StringComparison.Ordinal))) == true;
    }

    /// <summary>
    /// 导出当前保留的 Run 的人类可读 TXT 报告。
    /// </summary>
    public string ExportText()
    {
        var builder = new StringBuilder();
        foreach (var run in _runs)
        {
            builder.AppendLine($"Run {run.RunId}");
            foreach (var battle in run.Battles)
            {
                builder.AppendLine($"Battle {battle.BattleId} [{battle.EncounterId}] Result={battle.Result}");
                foreach (var round in battle.Rounds)
                {
                    builder.AppendLine($"  Round {round.Round}");
                    foreach (var phase in round.Phases)
                    {
                        builder.AppendLine($"    Phase {phase.Phase}");
                        foreach (var entry in phase.Entries)
                        {
                            builder.AppendLine(
                                $"      {entry.Timestamp:HH:mm:ss.fff} [{entry.Kind}] {entry.GetText()}"
                                + (entry.StackCount > 1 ? $" x{entry.StackCount}" : string.Empty));
                        }
                    }
                }
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// 导出当前保留的 Run 的结构化 JSON 报告。
    /// </summary>
    public string ExportJson()
    {
        var payload = new
        {
            currentRunId = CurrentRun.RunId,
            runs = _runs.Select(run => new
            {
                runId = run.RunId,
                startedAt = run.StartedAt,
                battles = run.Battles.Select(battle => new
                {
                    battleId = battle.BattleId,
                    encounterId = battle.EncounterId,
                    battle.StartedAt,
                    battle.EndedAt,
                    battle.Result,
                    rounds = battle.Rounds.Select(round => new
                    {
                        round = round.Round,
                        phases = round.Phases.Select(phase => new
                        {
                            phase = phase.Phase,
                            events = phase.Entries.Select(ToExportObject)
                        })
                    })
                })
            })
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object ToExportObject(BattleLogEntry entry)
    {
        return new
        {
            entry.EventId,
            entry.BattleId,
            entry.Timestamp,
            entry.Round,
            entry.Phase,
            triggerTiming = entry.TriggerTiming?.ToString(),
            priority = entry.Priority?.ToString(),
            category = entry.Category.ToString(),
            kind = entry.Kind.ToString(),
            entry.Actor,
            entry.Target,
            entry.Source,
            entry.Skill,
            entry.Equipment,
            entry.Card,
            entry.Buff,
            entry.DamageBefore,
            entry.DamageAfter,
            entry.EnergyBefore,
            entry.EnergyAfter,
            entry.HpBefore,
            entry.HpAfter,
            entry.Result,
            entry.DebugMessage,
            entry.TextKey,
            entry.TextArgs,
            entry.DebugOnly,
            entry.StackCount
        };
    }

    private static bool MatchesDeveloperFilter(BattleLogEntry entry, DeveloperLogFilter filter)
    {
        return filter switch
        {
            DeveloperLogFilter.All => true,
            DeveloperLogFilter.Trigger => entry.Kind is BattleLogEventKind.TriggerPhase
                or BattleLogEventKind.EffectQueue
                or BattleLogEventKind.EffectExecute,
            DeveloperLogFilter.Damage => entry.Kind is BattleLogEventKind.Damage
                or BattleLogEventKind.DamagePipeline,
            DeveloperLogFilter.Skill => entry.Kind == BattleLogEventKind.Skill,
            DeveloperLogFilter.Equipment => entry.Kind == BattleLogEventKind.Equipment,
            DeveloperLogFilter.Buff => entry.Kind == BattleLogEventKind.Buff,
            DeveloperLogFilter.Ai => entry.Kind == BattleLogEventKind.AiDecision,
            DeveloperLogFilter.Warning => entry.Kind == BattleLogEventKind.Warning,
            DeveloperLogFilter.Error => entry.Kind == BattleLogEventKind.Error,
            _ => true
        };
    }

    private void RecycleRun(BattleRunReport run)
    {
        foreach (var entry in run.Battles.SelectMany(battle => battle.Entries))
        {
            _allEntries.Remove(entry);
            entry.Reset();
            _entryPool.Push(entry);
        }
    }

    private static BattleLogCategory GetCategory(BattleLogEventKind kind)
    {
        return kind switch
        {
            BattleLogEventKind.Damage or BattleLogEventKind.DamagePipeline => BattleLogCategory.Damage,
            BattleLogEventKind.Heal => BattleLogCategory.Recover,
            BattleLogEventKind.Skill or BattleLogEventKind.Buff => BattleLogCategory.Buff,
            BattleLogEventKind.Error => BattleLogCategory.Error,
            BattleLogEventKind.TriggerPhase
                or BattleLogEventKind.EffectQueue
                or BattleLogEventKind.EffectExecute
                or BattleLogEventKind.AiDecision
                or BattleLogEventKind.Debug
                or BattleLogEventKind.Warning => BattleLogCategory.Debug,
            BattleLogEventKind.RoundStart
                or BattleLogEventKind.RoundEnd
                or BattleLogEventKind.BattleStart
                or BattleLogEventKind.BattleEnd => BattleLogCategory.System,
            _ => BattleLogCategory.Action
        };
    }

    private static Color GetColor(BattleLogCategory category, EffectPriority? priority)
    {
        if (category == BattleLogCategory.Debug && priority.HasValue)
        {
            return priority.Value switch
            {
                EffectPriority.Immediate => new Color(1.00f, 0.30f, 0.26f),
                EffectPriority.Highest => new Color(1.00f, 0.55f, 0.22f),
                EffectPriority.High => new Color(1.00f, 0.82f, 0.25f),
                EffectPriority.Mid => Colors.White,
                EffectPriority.Low => new Color(0.60f, 0.63f, 0.68f),
                EffectPriority.Lowest => new Color(0.38f, 0.40f, 0.44f),
                _ => Colors.White
            };
        }

        return category switch
        {
            BattleLogCategory.Action => Colors.White,
            BattleLogCategory.Damage => new Color(1.00f, 0.28f, 0.24f),
            BattleLogCategory.Recover => new Color(0.34f, 1.00f, 0.54f),
            BattleLogCategory.Buff => new Color(0.48f, 0.58f, 1.00f),
            BattleLogCategory.System => new Color(0.62f, 0.65f, 0.70f),
            BattleLogCategory.Debug => new Color(1.00f, 0.86f, 0.25f),
            BattleLogCategory.Error => new Color(1.00f, 0.52f, 0.20f),
            _ => Colors.White
        };
    }

    private static BattleLogIconType GetIconType(BattleLogCategory category)
    {
        return category switch
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
    }

    private void EnsureBattle()
    {
        if (CurrentBattle == null)
        {
            BeginBattle("unknown");
        }
    }
}

/// <summary>
/// 跨场景共享的日志服务入口。
/// </summary>
public static class BattleLogRuntime
{
    public static BattleLogService Service { get; } = new();
}
