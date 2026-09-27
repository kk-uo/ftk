//////////////////////////////////////////////////////////
// 文件：Scripts/Codex/CodexService.cs
//
// 模块：Codex System（跨Run永久图鉴）
//
// 为什么存在：
// 项目里此前没有任何"跨Run永久存档"机制（唯一的持久化是 user://settings.cfg
// 里的几个开关型标记，例如教程完成状态）。图鉴需要保存成百上千条统计，
// ConfigFile 的扁平 section/key 模型不适合，这里改用 JSON 文件
// （user://codex_save.json），只在关键节点（战斗结束/事件结束/装备获得/
// Boss击败/Run结束/返回主菜单）落盘，不在每次卡牌使用时写硬盘。
//
// 职责：
// 1. 持有内存中的 CodexSaveData，提供 Record* 系列方法供真实游戏系统调用
//    （不是UI，UI只读取，不写入——见文件头部各 Record 方法的调用点约定）。
// 2. 统一"是否应该计入永久统计"的开关：教学战斗（TutorialManager.IsActive）
//    和开发者调试战斗（DebugBattleActive，由 BattleManager.ConfigureDebugMode
//    写入）都不计入，避免测试/教学数据污染正式图鉴。
// 3. 负责存档读写与版本迁移。
//
// 不负责：
// × 判断"什么算使用一次/获得一次/遭遇一次"这些业务规则本身——调用方
//   （BattlePhaseResolutionEffect、DeathEffect、GameManager.AddEquipment等）
//   已经在正确的时机调用，这里只管记账。
// × 图鉴UI展示（见 CodexController）。
//
// 主要依赖：
// Godot.FileAccess / System.Text.Json
//////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

/// <summary>
/// Codex System 的公开类：CodexService。
/// </summary>
public static class CodexService
{
    public const int CurrentSaveVersion = 1;
    private const string SavePath = "user://codex_save.json";

    private static CodexSaveData _data = new();
    private static bool _dirty;

    /// <summary>教学战斗期间由 TutorialManager.IsActive 自动挡住；这里单独暴露调试战斗开关，
    /// 由 BattleManager.ConfigureDebugMode 在进入开发者调试战斗时设为 true，正常战斗时设为 false。</summary>
    public static bool DebugBattleActive { get; set; }

    /// <summary>当前 Run 的编号，用于 EquipmentCodexEntry.RunsObtainedIn 去重统计"完成过的Run数量"。
    /// 由 GameManager.BeginNewRun 递增写入。</summary>
    public static int CurrentRunId { get; set; }

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private static bool ShouldTrack => !TutorialManager.IsActive && !DebugBattleActive;

    // ======================================================
    // 存档读写
    // ======================================================

    /// <summary>应在游戏启动时调用一次。旧存档没有Codex文件时自动初始化为空图鉴，不影响其它系统。</summary>
    public static void Initialize()
    {
        _data = Load();
        _dirty = false;
    }

    private static CodexSaveData Load()
    {
        if (!FileAccess.FileExists(SavePath))
        {
            return new CodexSaveData();
        }

        try
        {
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            if (file == null)
            {
                return new CodexSaveData();
            }

            var text = file.GetAsText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return new CodexSaveData();
            }

            var loaded = JsonSerializer.Deserialize<CodexSaveData>(text);
            return loaded == null ? new CodexSaveData() : MigrateIfNeeded(loaded);
        }
        catch (Exception e)
        {
            GD.PushError($"[CodexService] 读取存档失败，回退到空图鉴：{e}");
            return new CodexSaveData();
        }
    }

    // 版本迁移：新增字段时 System.Text.Json 会自动补默认值，通常不需要手写迁移逻辑；
    // 这里只处理"字段语义变化"级别的迁移（目前只有 Version 1，占位以后扩展）。
    private static CodexSaveData MigrateIfNeeded(CodexSaveData data)
    {
        if (data.Version < CurrentSaveVersion)
        {
            data.Version = CurrentSaveVersion;
        }

        return data;
    }

    /// <summary>在关键节点（战斗结束/事件结束/装备获得/Boss击败/Run结束/返回主菜单）调用，
    /// 而不是每次卡牌使用都调用——避免频繁磁盘IO。异常退出只会丢失最近一次未落盘的批次。</summary>
    public static void SaveIfDirty()
    {
        if (!_dirty)
        {
            return;
        }

        try
        {
            using var file = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
            if (file == null)
            {
                GD.PushError("[CodexService] 无法打开存档文件写入。");
                return;
            }

            file.StoreString(JsonSerializer.Serialize(_data, SerializerOptions));
            _dirty = false;
        }
        catch (Exception e)
        {
            GD.PushError($"[CodexService] 保存存档失败：{e}");
        }
    }

    private static void MarkDirty() => _dirty = true;

    // ======================================================
    // 卡牌
    // ======================================================

    private static CardCodexEntry GetOrCreateCard(CardType type)
    {
        var key = type.ToString();
        if (!_data.Cards.TryGetValue(key, out var entry))
        {
            entry = new CardCodexEntry();
            _data.Cards[key] = entry;
        }

        return entry;
    }

    /// <summary>调用点：BattlePhaseResolutionEffect 结算玩家已确认提交的行动时（每次结算调用一次，
    /// <paramref name="count"/> 对应同一张牌在这次行动里被重复打出的份数——例如一回合打出2张酒
    /// 算使用2次；但多目标卡牌如万箭齐发命中3个目标仍然只是1次行动，调用方传 count=1）。</summary>
    public static void RecordCardUsed(CardType type, int count = 1)
    {
        if (!ShouldTrack || count <= 0) return;
        var entry = GetOrCreateCard(type);
        entry.Discovered = true;
        entry.TimesUsed += count;
        _data.Global.TotalCardsUsed += count;
        MarkDirty();
    }

    /// <summary>调用点：ApplyDamageEffect 对某个目标实际结算伤害成功（未被完全格挡）时，
    /// 按命中目标数逐次调用——万箭齐发命中3个目标就调用3次。</summary>
    public static void RecordCardHit(CardType type, int damage, bool killedTarget)
    {
        if (!ShouldTrack) return;
        var entry = GetOrCreateCard(type);
        entry.TimesHit++;
        if (damage > 0)
        {
            entry.TotalDamage += damage;
            if (damage > entry.MaxSingleDamage) entry.MaxSingleDamage = damage;
        }
        if (killedTarget) entry.KillCount++;
        MarkDirty();
    }

    /// <summary>调用点：DefenseBeforeDamageEffect 等防御效果完全抵消一次伤害时。</summary>
    public static void RecordCardBlocked(CardType type)
    {
        if (!ShouldTrack) return;
        GetOrCreateCard(type).TimesBlocked++;
        MarkDirty();
    }

    /// <summary>通用数值累计：费的费用获得量、桃/酒的回复量、顺手牵羊偷取的费用等。</summary>
    public static void RecordCardValue(CardType type, double value)
    {
        if (!ShouldTrack || value == 0) return;
        GetOrCreateCard(type).TotalValue += value;
        MarkDirty();
    }

    /// <summary>通用次级计数：桃的濒死使用次数、酒的濒死复活次数、顺手牵羊成功次数等。</summary>
    public static void RecordCardSpecial(CardType type)
    {
        if (!ShouldTrack) return;
        GetOrCreateCard(type).SpecialCount++;
        MarkDirty();
    }

    public static CardCodexEntry? GetCard(CardType type) => _data.Cards.GetValueOrDefault(type.ToString());

    // ======================================================
    // 敌人
    // ======================================================

    private static EnemyCodexEntry GetOrCreateEnemy(string enemyId)
    {
        if (!_data.Enemies.TryGetValue(enemyId, out var entry))
        {
            entry = new EnemyCodexEntry();
            _data.Enemies[enemyId] = entry;
        }

        return entry;
    }

    /// <summary>调用点：EnemyFactory.CreateEnemy 生成一个正式战斗敌人实例时（不要求击败即可发现）。</summary>
    public static void RecordEnemyEncounter(string enemyId, int chapter)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(enemyId)) return;
        var entry = GetOrCreateEnemy(enemyId);
        var firstTime = !entry.Discovered;
        entry.Discovered = true;
        entry.EncounterCount++;
        if (firstTime || entry.FirstSeenChapter == 0)
        {
            entry.FirstSeenChapter = chapter;
        }
        MarkDirty();
    }

    /// <summary>调用点：DeathEffect 处理某个 EnemyInstance 真正死亡时，无论本场战斗最终输赢
    /// 都计入（默认规则：敌人真正死亡就计入击败次数，见实现报告"重点确认规则"第10条）。
    /// 共享HP Pool去重由调用方（BattleManager/DeathEffect）负责——同一次血池耗尽事件里，
    /// 每个 EnemyDefinition.Id 只应该调用一次，不按显示实体数量重复调用。</summary>
    public static void RecordEnemyDefeated(string enemyId, int turnNumber)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(enemyId)) return;
        var entry = GetOrCreateEnemy(enemyId);
        entry.DefeatedCount++;
        if (turnNumber > 0 && turnNumber < entry.FastestDefeatTurn)
        {
            entry.FastestDefeatTurn = turnNumber;
        }
        _data.Global.TotalEnemiesDefeated++;
        MarkDirty();
    }

    public static void RecordEnemyDefeatedByBoss(string enemyId, string characterId, EnemyType type)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(enemyId)) return;
        if (type != EnemyType.Boss) return;
        var entry = GetOrCreateEnemy(enemyId);
        entry.DefeatedByCharacterIds.Add(characterId);
        _data.Global.TotalBossesDefeated++;
        MarkDirty();
    }

    public static void RecordBossChallenge(string enemyId)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(enemyId)) return;
        GetOrCreateEnemy(enemyId).ChallengeCount++;
        MarkDirty();
    }

    /// <summary>调用点：战斗以玩家失败结束时，对本场遭遇到的每个存活到最后的敌方 EnemyDefinition.Id 调用。</summary>
    public static void RecordPlayerDefeatedBy(string enemyId)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(enemyId)) return;
        GetOrCreateEnemy(enemyId).PlayerDefeatedByCount++;
        MarkDirty();
    }

    public static void RecordEnemyDamageDealt(string enemyId, int amount)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(enemyId) || amount <= 0) return;
        GetOrCreateEnemy(enemyId).DamageDealtToEnemy += amount;
        MarkDirty();
    }

    public static void RecordEnemyDamageTaken(string enemyId, int amount)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(enemyId) || amount <= 0) return;
        GetOrCreateEnemy(enemyId).DamageTakenFromEnemy += amount;
        MarkDirty();
    }

    public static EnemyCodexEntry? GetEnemy(string enemyId) => _data.Enemies.GetValueOrDefault(enemyId);

    // ======================================================
    // 装备
    // ======================================================

    private static EquipmentCodexEntry GetOrCreateEquipment(string equipmentId)
    {
        if (!_data.Equipment.TryGetValue(equipmentId, out var entry))
        {
            entry = new EquipmentCodexEntry();
            _data.Equipment[equipmentId] = entry;
        }

        return entry;
    }

    /// <summary>调用点：商店/事件选项等第一次把某件装备的存在展示给玩家时（不要求真正获得）。
    /// 第一版按用户建议的简化方案：Seen 只解锁基础展示，effect仍要 Obtained 才显示。</summary>
    public static void RecordEquipmentSeen(string equipmentId)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(equipmentId)) return;
        GetOrCreateEquipment(equipmentId).Seen = true;
        MarkDirty();
    }

    /// <summary>调用点：GameManager.AddEquipment 真正把装备写入背包成功时（商店购买/事件奖励/
    /// Boss掉落/敌人掉落/三选一/初始事件/装备升级转换后的新装备）。刷新商店看到、事件预览、
    /// 图鉴查看、掉落生成但未领取都不应该调用这里。</summary>
    public static void RecordEquipmentObtained(string equipmentId)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(equipmentId)) return;
        var entry = GetOrCreateEquipment(equipmentId);
        entry.Seen = true;
        entry.Obtained = true;
        entry.ObtainedCount++;
        entry.RunsObtainedIn.Add(CurrentRunId);
        _data.Global.TotalEquipmentObtained++;
        MarkDirty();
    }

    public static void RecordEquipmentEquipped(string equipmentId)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(equipmentId)) return;
        GetOrCreateEquipment(equipmentId).EquippedCount++;
        MarkDirty();
    }

    public static void RecordEquipmentSold(string equipmentId)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(equipmentId)) return;
        GetOrCreateEquipment(equipmentId).SoldCount++;
        MarkDirty();
    }

    public static void RecordEquipmentTriggered(string equipmentId)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(equipmentId)) return;
        GetOrCreateEquipment(equipmentId).TriggeredCount++;
        MarkDirty();
    }

    /// <summary>
    /// 记录装备实际提供的电量。
    ///
    /// 只有真实增加电量时才算一次有效触发，容量溢出的理论收益不会进入累计值。
    /// </summary>
    public static void RecordEquipmentEnergyProvided(string equipmentId, int actualEnergyGain)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(equipmentId) || actualEnergyGain <= 0)
        {
            return;
        }

        var entry = GetOrCreateEquipment(equipmentId);
        entry.TriggeredCount++;
        entry.TotalEnergyProvided += actualEnergyGain;
        entry.MaxEnergyProvidedInBattle = Math.Max(entry.MaxEnergyProvidedInBattle, actualEnergyGain);
        MarkDirty();
    }

    public static EquipmentCodexEntry? GetEquipment(string equipmentId) => _data.Equipment.GetValueOrDefault(equipmentId);

    // ======================================================
    // 事件
    // ======================================================

    private static EventCodexEntry GetOrCreateEvent(string eventId)
    {
        if (!_data.Events.TryGetValue(eventId, out var entry))
        {
            entry = new EventCodexEntry();
            _data.Events[eventId] = entry;
        }

        return entry;
    }

    public static void RecordEventEncounter(string eventId)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(eventId)) return;
        var entry = GetOrCreateEvent(eventId);
        entry.Discovered = true;
        entry.EncounterCount++;
        MarkDirty();
    }

    /// <summary>optionIndex = 该选项在 EventData.Options 里的下标，事件定义生命周期内视为稳定ID
    /// （EventOption 本身没有独立的稳定字符串ID字段，见实现报告的已知限制）。</summary>
    public static void RecordEventChoice(string eventId, int optionIndex)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(eventId)) return;
        var entry = GetOrCreateEvent(eventId);
        entry.DiscoveredOptionIndexes.Add(optionIndex);
        entry.ChoiceCounts[optionIndex] = entry.ChoiceCounts.GetValueOrDefault(optionIndex) + 1;
        entry.CompletedCount++;
        MarkDirty();
    }

    public static EventCodexEntry? GetEvent(string eventId) => _data.Events.GetValueOrDefault(eventId);

    // ======================================================
    // 角色 / 技能
    // ======================================================

    private static CharacterCodexEntry GetOrCreateCharacter(string characterId)
    {
        if (!_data.Characters.TryGetValue(characterId, out var entry))
        {
            entry = new CharacterCodexEntry();
            _data.Characters[characterId] = entry;
        }

        return entry;
    }

    public static void RecordCharacterRunStart(string characterId)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(characterId)) return;
        var entry = GetOrCreateCharacter(characterId);
        entry.Unlocked = true;
        entry.RunsStarted++;
        _data.Global.TotalRuns++;
        MarkDirty();
    }

    public static void RecordCharacterBattleResult(string characterId, bool won)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(characterId)) return;
        var entry = GetOrCreateCharacter(characterId);
        entry.BattlesFought++;
        if (won) entry.BattlesWon++;
        _data.Global.TotalBattles++;
        if (won) _data.Global.TotalVictories++;
        MarkDirty();
    }

    public static void RecordCharacterBossDefeated(string characterId)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(characterId)) return;
        GetOrCreateCharacter(characterId).BossesDefeated++;
        MarkDirty();
    }

    public static void RecordCharacterDeath(string characterId)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(characterId)) return;
        GetOrCreateCharacter(characterId).Deaths++;
        _data.Global.TotalDeaths++;
        MarkDirty();
    }

    public static void RecordCharacterRunCompleted(string characterId)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(characterId)) return;
        GetOrCreateCharacter(characterId).RunsCompleted++;
        _data.Global.RunsCompleted++;
        MarkDirty();
    }

    public static void RecordCharacterChapterReached(string characterId, int chapter)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(characterId)) return;
        var entry = GetOrCreateCharacter(characterId);
        if (chapter > entry.HighestChapterReached)
        {
            entry.HighestChapterReached = chapter;
            MarkDirty();
        }
    }

    public static void RecordCharacterDamageDealt(string characterId, int amount)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(characterId) || amount <= 0) return;
        GetOrCreateCharacter(characterId).DamageDealt += amount;
        _data.Global.TotalDamageDealt += amount;
        MarkDirty();
    }

    public static void RecordCharacterDamageTaken(string characterId, int amount)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(characterId) || amount <= 0) return;
        GetOrCreateCharacter(characterId).DamageTaken += amount;
        _data.Global.TotalDamageTaken += amount;
        MarkDirty();
    }

    public static void RecordCharacterHealing(string characterId, int amount)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(characterId) || amount <= 0) return;
        GetOrCreateCharacter(characterId).HealingDone += amount;
        _data.Global.TotalHealingDone += amount;
        MarkDirty();
    }

    /// <summary>调用点：唯一入口是 BattleContext.ReportPlayerCharacterSkillTriggered（技能已经
    /// 真正生效后才会走到那个方法），不要在各个技能效果里各自增加计数——那个方法已经统一
    /// 过滤了"仅完成条件检查"、"敌方/装备技能"等不应计入的情况。</summary>
    public static void RecordSkillTriggered(string characterId, string skillId)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(skillId)) return;
        var entry = GetOrCreateCharacter(characterId);
        entry.SkillTriggerCounts[skillId] = entry.SkillTriggerCounts.GetValueOrDefault(skillId) + 1;
        MarkDirty();
    }

    public static CharacterCodexEntry? GetCharacter(string characterId) => _data.Characters.GetValueOrDefault(characterId);

    // ======================================================
    // 芯片
    // ======================================================

    public static void RecordChipObtained(string chipType)
    {
        if (!ShouldTrack || string.IsNullOrEmpty(chipType)) return;
        if (!_data.Chips.TryGetValue(chipType, out var entry))
        {
            entry = new ChipCodexEntry();
            _data.Chips[chipType] = entry;
        }
        entry.ObtainedCount++;
        MarkDirty();
    }

    public static ChipCodexEntry? GetChip(string chipType) => _data.Chips.GetValueOrDefault(chipType);

    // ======================================================
    // 全局
    // ======================================================

    public static GlobalCodexStats Global => _data.Global;

    public static void RecordGoldEarned(int amount)
    {
        if (!ShouldTrack || amount <= 0) return;
        _data.Global.TotalGoldEarned += amount;
        MarkDirty();
    }

    public static void RecordGoldSpent(int amount)
    {
        if (!ShouldTrack || amount <= 0) return;
        _data.Global.TotalGoldSpent += amount;
        MarkDirty();
    }

    // ======================================================
    // 只读访问（供UI遍历）
    // ======================================================

    public static IReadOnlyDictionary<string, CardCodexEntry> AllCards => _data.Cards;
    public static IReadOnlyDictionary<string, EnemyCodexEntry> AllEnemies => _data.Enemies;
    public static IReadOnlyDictionary<string, EquipmentCodexEntry> AllEquipment => _data.Equipment;
    public static IReadOnlyDictionary<string, EventCodexEntry> AllEvents => _data.Events;
    public static IReadOnlyDictionary<string, CharacterCodexEntry> AllCharacters => _data.Characters;

    // ======================================================
    // Developer / Debug
    // ======================================================

    /// <summary>解锁全部卡牌/敌人/装备/事件的"发现"状态（不伪造使用次数等统计），供开发者预览用。</summary>
    public static void DebugUnlockAll()
    {
        foreach (var type in Enum.GetValues<CardType>())
        {
            GetOrCreateCard(type).Discovered = true;
        }
        foreach (var def in EnemyDatabase.GetAllEnemies())
        {
            var entry = GetOrCreateEnemy(def.Id);
            entry.Discovered = true;
            if (entry.FirstSeenChapter == 0) entry.FirstSeenChapter = 1;
        }
        foreach (var def in EquipmentDatabase.GetAllEquipments())
        {
            var entry = GetOrCreateEquipment(def.Id);
            entry.Seen = true;
            entry.Obtained = true;
        }
        foreach (var def in EventDatabase.GetAllEvents())
        {
            var entry = GetOrCreateEvent(def.Id);
            entry.Discovered = true;
            for (var i = 0; i < def.Options.Count; i++)
            {
                entry.DiscoveredOptionIndexes.Add(i);
            }
        }
        MarkDirty();
        SaveIfDirty();
    }

    /// <summary>清空整个图鉴存档（不影响角色解锁、设置等其它系统）。</summary>
    public static void DebugReset()
    {
        _data = new CodexSaveData();
        MarkDirty();
        SaveIfDirty();
    }

    /// <summary>孤儿统计检查：存档里存在、但当前数据库已经没有对应Definition的ID。
    /// 供 Developer 面板一次性报告使用，不在正常运行路径调用。</summary>
    public static List<string> FindOrphanEntries()
    {
        var orphans = new List<string>();
        foreach (var id in _data.Enemies.Keys)
        {
            if (EnemyDatabase.GetEnemy(id) == null) orphans.Add($"enemy:{id}");
        }
        foreach (var id in _data.Equipment.Keys)
        {
            if (EquipmentDatabase.GetEquipment(id) == null) orphans.Add($"equipment:{id}");
        }
        foreach (var id in _data.Events.Keys)
        {
            if (EventDatabase.GetEvent(id) == null) orphans.Add($"event:{id}");
        }
        return orphans;
    }

    public static string ExportJson() => JsonSerializer.Serialize(_data, SerializerOptions);

    /// <summary>供 headless 测试使用：重置为一份全新的内存态存档，不落盘、不影响真实存档文件。</summary>
    internal static void ResetForTest()
    {
        _data = new CodexSaveData();
        _dirty = false;
        DebugBattleActive = false;
        CurrentRunId = 0;
    }
}
