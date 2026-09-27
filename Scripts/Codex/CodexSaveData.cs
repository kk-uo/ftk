//////////////////////////////////////////////////////////
// 文件：Scripts/Codex/CodexSaveData.cs
//
// 模块：Codex System（跨Run永久图鉴）
//
// 职责：
// 1. 定义图鉴永久存档的数据结构（纯数据，无行为）。
// 2. 所有字典一律以稳定ID为键（CardType枚举名/EnemyDefinition.Id/
//    EquipmentDefinition.Id/EventData.Id/CharacterIds/SkillIds），
//    绝不使用显示名称，避免文案改名导致记录丢失。
//
// 不负责：
// × 读写文件、版本迁移（见 CodexService）。
// × 判断"什么时候算触发一次"（见各系统的实际调用点）。
//
// 主要依赖：
// System.Text.Json（序列化）
//////////////////////////////////////////////////////////

using System.Collections.Generic;

/// <summary>
/// Codex System 的公开类：CodexSaveData。
///
/// 图鉴永久存档根节点，序列化为 user://codex_save.json。
/// </summary>
public sealed class CodexSaveData
{
    public int Version { get; set; } = CodexService.CurrentSaveVersion;
    public Dictionary<string, CardCodexEntry> Cards { get; set; } = new();
    public Dictionary<string, EnemyCodexEntry> Enemies { get; set; } = new();
    public Dictionary<string, EquipmentCodexEntry> Equipment { get; set; } = new();
    public Dictionary<string, EventCodexEntry> Events { get; set; } = new();
    public Dictionary<string, CharacterCodexEntry> Characters { get; set; } = new();
    public Dictionary<string, ChipCodexEntry> Chips { get; set; } = new();
    public GlobalCodexStats Global { get; set; } = new();
}

/// <summary>
/// 卡牌图鉴条目。字段是通用数值槽位，具体某种卡牌用哪几个槽位由 UI 按 CardType 决定
/// （例如【费】只看 TotalValue，【杀】看 TotalDamage/MaxSingleDamage/KillCount），
/// 不为每种卡牌单独开字段，避免枚举新增卡牌时结构跟着爆炸。
/// </summary>
public sealed class CardCodexEntry
{
    public bool Discovered { get; set; }
    public int TimesUsed { get; set; }
    public int TimesHit { get; set; }
    public int TimesBlocked { get; set; }
    public int TotalDamage { get; set; }
    public int MaxSingleDamage { get; set; }
    public int KillCount { get; set; }
    /// <summary>通用数值累计：费的费用获得量、桃/酒的回复量、顺手牵羊偷取的费用等，按卡牌类型解释。</summary>
    public double TotalValue { get; set; }
    /// <summary>通用次级计数：桃的濒死使用次数、酒的濒死复活次数、顺手牵羊的成功次数等。</summary>
    public int SpecialCount { get; set; }
}

public sealed class EnemyCodexEntry
{
    public bool Discovered { get; set; }
    public int EncounterCount { get; set; }
    public int DefeatedCount { get; set; }
    public int PlayerDefeatedByCount { get; set; }
    public int DamageDealtToEnemy { get; set; }
    public int DamageTakenFromEnemy { get; set; }
    public int FastestDefeatTurn { get; set; } = int.MaxValue;
    public int FirstSeenChapter { get; set; }
    // Boss 专属扩展字段（普通/精英敌人始终为空/0）。
    public int ChallengeCount { get; set; }
    public HashSet<string> DefeatedByCharacterIds { get; set; } = new();
}

public sealed class EquipmentCodexEntry
{
    public bool Seen { get; set; }
    public bool Obtained { get; set; }
    public int ObtainedCount { get; set; }
    public int EquippedCount { get; set; }
    public int SoldCount { get; set; }
    public int TriggeredCount { get; set; }
    public int TotalEnergyProvided { get; set; }
    public int MaxEnergyProvidedInBattle { get; set; }
    public HashSet<int> RunsObtainedIn { get; set; } = new();
}

public sealed class EventCodexEntry
{
    public bool Discovered { get; set; }
    public int EncounterCount { get; set; }
    public int CompletedCount { get; set; }
    /// <summary>键 = 选项在 EventData.Options 里的下标（该事件定义生命周期内视为稳定）。</summary>
    public Dictionary<int, int> ChoiceCounts { get; set; } = new();
    public HashSet<int> DiscoveredOptionIndexes { get; set; } = new();
}

public sealed class CharacterCodexEntry
{
    public bool Unlocked { get; set; }
    public int RunsStarted { get; set; }
    public int BattlesFought { get; set; }
    public int BattlesWon { get; set; }
    public int BossesDefeated { get; set; }
    public int Deaths { get; set; }
    public int RunsCompleted { get; set; }
    public int HighestChapterReached { get; set; }
    public long DamageDealt { get; set; }
    public long DamageTaken { get; set; }
    public long HealingDone { get; set; }
    public Dictionary<string, int> SkillTriggerCounts { get; set; } = new();
}

public sealed class ChipCodexEntry
{
    public int ObtainedCount { get; set; }
}

public sealed class GlobalCodexStats
{
    public int TotalRuns { get; set; }
    public int RunsCompleted { get; set; }
    public int TotalBattles { get; set; }
    public int TotalVictories { get; set; }
    public int TotalDeaths { get; set; }
    public int TotalEnemiesDefeated { get; set; }
    public int TotalBossesDefeated { get; set; }
    public int TotalCardsUsed { get; set; }
    public long TotalDamageDealt { get; set; }
    public long TotalDamageTaken { get; set; }
    public long TotalHealingDone { get; set; }
    public long TotalGoldEarned { get; set; }
    public long TotalGoldSpent { get; set; }
    public int TotalEquipmentObtained { get; set; }
}
