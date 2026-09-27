//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/CombatFeedFormatter.cs
//
// 模块：Battle Log Presentation
//
// 职责：
// 1. 将当前回合的统一日志事件投影为局内精简战报。
// 2. 按行动、克制、技能、伤害、回合结束的固定顺序分组。
// 3. 限制单回合信息量，避免左侧战报刷屏。
//
// 不负责：
// × 记录或修改战斗结果。
// × 展示 Trigger、Priority、EffectQueue 等开发信息。
// × 创建 Godot UI 节点。
//
// 主要依赖：
// BattleLogEntry
// Localization
//////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

/// <summary>
/// 局内精简战报的一个固定分区。
/// </summary>
public sealed class CombatFeedSection
{
    /// <summary>
    /// 创建一个带本地化标题和显示颜色的战报分区。
    /// </summary>
    public CombatFeedSection(string titleKey, string colorHex)
    {
        TitleKey = titleKey;
        ColorHex = colorHex;
    }

    public string TitleKey { get; }
    public string ColorHex { get; }
    public List<string> Items { get; } = new();
}

/// <summary>
/// Combat Feed 的纯数据格式化器。
///
/// Battle History 和 Developer Report 保留完整事件；本类只生成玩家在战斗中
/// 快速阅读的当前回合摘要，因此不会把内部调试条目或原始类名带进主界面。
/// </summary>
public static class CombatFeedFormatter
{
    public const int MaxItemsPerRound = 10;

    /// <summary>
    /// 按固定顺序构建指定回合的精简战报。
    /// </summary>
    public static IReadOnlyList<CombatFeedSection> BuildRound(
        IReadOnlyList<BattleLogEntry> entries,
        int round,
        string playerDisplayName)
    {
        var playerActions = new CombatFeedSection("combat_feed.player_actions", "#58d9e8");
        var enemyActions = new CombatFeedSection("combat_feed.enemy_actions", "#ff8378");
        var counters = new CombatFeedSection("combat_feed.counter_results", "#d7c68a");
        var skills = new CombatFeedSection("combat_feed.skills", "#82b7ff");
        var damage = new CombatFeedSection("combat_feed.damage", "#ff6860");
        var roundEnd = new CombatFeedSection("combat_feed.round_end", "#78d69a");
        var counterRelations = new List<string>();
        var counterDamage = new List<string>();
        var counterResolutions = new List<string>();
        var enemyActionCount = CountEnemyActions(entries, round);
        var enemyActionIndex = 0;
        var enemyPositions = BuildEnemyPositionMap(entries, round, enemyActionCount);

        foreach (var entry in entries)
        {
            if (entry.Round != round || entry.DebugOnly || entry.Category == BattleLogCategory.Debug)
            {
                continue;
            }

            switch (entry.Kind)
            {
                case BattleLogEventKind.PlayerAction:
                    AddUnique(playerActions, FormatAction(entry, playerDisplayName));
                    break;
                case BattleLogEventKind.EnemyAction:
                    AddUnique(enemyActions, FormatEnemyAction(
                        entry,
                        playerDisplayName,
                        enemyActionIndex,
                        enemyActionCount));
                    enemyActionIndex++;
                    break;
                case BattleLogEventKind.Counter:
                    AddUnique(counterRelations, FormatResult(entry));
                    break;
                case BattleLogEventKind.CardResolution:
                    AddUnique(counterResolutions, entry.GetText());
                    break;
                case BattleLogEventKind.Skill:
                    AddUnique(skills, FormatSkill(entry));
                    break;
                case BattleLogEventKind.Damage:
                    if (!string.IsNullOrWhiteSpace(entry.Card)
                        && entry.DamageAfter.GetValueOrDefault() > 0)
                    {
                        AddUnique(counterDamage, FormatCardDamage(entry, playerDisplayName));
                    }
                    AddUnique(damage, FormatDamage(entry, playerDisplayName));
                    break;
                case BattleLogEventKind.Heal:
                case BattleLogEventKind.Shield:
                    AddUnique(damage, FormatUnitResult(entry, enemyPositions));
                    break;
                case BattleLogEventKind.Energy:
                case BattleLogEventKind.Buff:
                    AddUnique(roundEnd, FormatUnitResult(entry, enemyPositions));
                    break;
            }
        }

        foreach (var item in counterRelations)
        {
            AddUnique(counters, item);
        }
        foreach (var item in counterDamage)
        {
            AddUnique(counters, item);
        }
        foreach (var item in counterResolutions)
        {
            AddUnique(counters, item);
        }

        // 只有没有费用或 Buff 等明确结果时才补“结算完成”。否则它会与已有
        // 回合结束内容并列，既重复标题，也没有为玩家提供额外信息。
        if (entries.Count > 0 && roundEnd.Items.Count == 0)
        {
            AddUnique(roundEnd, Localization.Get("combat_feed.round_complete"));
        }

        var orderedContent = new[]
        {
            playerActions,
            enemyActions,
            counters,
            skills
        };

        var result = new List<CombatFeedSection>();
        // “伤害”是玩家判断死亡原因的核心信息。克制明细较多时如果按普通顺序截断，
        // 最后生成的玩家受伤记录会被挤出左侧战报，因此先为全部伤害条目预留预算。
        var reservedDamageCount = Math.Min(damage.Items.Count, MaxItemsPerRound - 1);
        var remaining = MaxItemsPerRound - 1 - reservedDamageCount;
        foreach (var section in orderedContent)
        {
            if (section.Items.Count == 0 || remaining <= 0)
            {
                continue;
            }

            if (section.Items.Count > remaining)
            {
                section.Items.RemoveRange(remaining, section.Items.Count - remaining);
            }

            remaining -= section.Items.Count;
            result.Add(section);
        }

        if (reservedDamageCount > 0)
        {
            if (damage.Items.Count > reservedDamageCount)
            {
                damage.Items.RemoveRange(reservedDamageCount, damage.Items.Count - reservedDamageCount);
            }

            result.Add(damage);
        }

        result.Add(roundEnd);
        return result;
    }

    private static string FormatAction(BattleLogEntry entry, string playerDisplayName)
    {
        var actor = ResolveActor(entry.Actor, playerDisplayName);
        var card = string.IsNullOrWhiteSpace(entry.Card) ? entry.GetText() : entry.Card;
        return string.IsNullOrWhiteSpace(actor) ? card : $"{actor}：{card}";
    }

    private static string FormatEnemyAction(
        BattleLogEntry entry,
        string playerDisplayName,
        int enemyIndex,
        int enemyCount)
    {
        var action = FormatAction(entry, playerDisplayName);
        var position = ResolveEnemyPosition(enemyIndex, enemyCount);
        return string.IsNullOrWhiteSpace(position) ? action : $"{position}·{action}";
    }

    private static int CountEnemyActions(IReadOnlyList<BattleLogEntry> entries, int round)
    {
        var count = 0;
        foreach (var entry in entries)
        {
            if (entry.Round == round
                && !entry.DebugOnly
                && entry.Category != BattleLogCategory.Debug
                && entry.Kind == BattleLogEventKind.EnemyAction)
            {
                count++;
            }
        }

        return count;
    }

    private static string ResolveEnemyPosition(int enemyIndex, int enemyCount)
    {
        if (enemyCount <= 0)
        {
            return string.Empty;
        }

        if (enemyCount == 1)
        {
            return Localization.Get("combat_feed.position.center");
        }

        if (enemyIndex <= 0)
        {
            return Localization.Get("combat_feed.position.left");
        }

        if (enemyIndex >= enemyCount - 1)
        {
            return Localization.Get("combat_feed.position.right");
        }

        return Localization.Get("combat_feed.position.center");
    }

    private static Dictionary<string, string> BuildEnemyPositionMap(
        IReadOnlyList<BattleLogEntry> entries,
        int round,
        int enemyCount)
    {
        var positions = new Dictionary<string, string>(StringComparer.Ordinal);
        var enemyIndex = 0;
        foreach (var entry in entries)
        {
            if (entry.Round != round
                || entry.DebugOnly
                || entry.Kind != BattleLogEventKind.EnemyAction)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.Source))
            {
                positions[entry.Source] = ResolveEnemyPosition(enemyIndex, enemyCount);
            }

            enemyIndex++;
        }

        return positions;
    }

    private static string FormatUnitResult(
        BattleLogEntry entry,
        IReadOnlyDictionary<string, string> enemyPositions)
    {
        var result = FormatResult(entry);
        if (string.IsNullOrWhiteSpace(entry.Source)
            || !enemyPositions.TryGetValue(entry.Source, out var position)
            || string.IsNullOrWhiteSpace(position))
        {
            return result;
        }

        return $"{position}·{result}";
    }

    private static string FormatDamage(BattleLogEntry entry, string playerDisplayName)
    {
        var target = ResolveActor(entry.Target, playerDisplayName);
        if (entry.DamageAfter.HasValue)
        {
            var amount = Math.Max(0, (int)Math.Round(entry.DamageAfter.Value));
            return amount > 0
                ? $"{target}：-{amount}HP"
                : $"{target}：{Localization.Get("combat_feed.no_damage")}";
        }

        return FormatResult(entry);
    }

    private static string FormatCardDamage(BattleLogEntry entry, string playerDisplayName)
    {
        var target = ResolveActor(entry.Target, playerDisplayName);
        var amount = Math.Max(0, (int)Math.Round(entry.DamageAfter.GetValueOrDefault()));
        return Localization.GetFmt("combat_feed.card_damage", entry.Card, target, amount);
    }

    private static string FormatSkill(BattleLogEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.Skill) ? FormatResult(entry) : entry.Skill;
    }

    private static string FormatResult(BattleLogEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Result))
        {
            return entry.Result;
        }

        return entry.GetText();
    }

    private static string ResolveActor(string actor, string playerDisplayName)
    {
        var genericPlayer = Localization.Get("battlelog.actor.player");
        if (string.IsNullOrWhiteSpace(actor)
            || string.Equals(actor, genericPlayer, StringComparison.Ordinal)
            || string.Equals(actor, "玩家", StringComparison.Ordinal)
            || string.Equals(actor, "Player", StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(playerDisplayName) ? genericPlayer : playerDisplayName;
        }

        return actor;
    }

    private static void AddUnique(CombatFeedSection section, string text)
    {
        if (!string.IsNullOrWhiteSpace(text) && !section.Items.Contains(text))
        {
            section.Items.Add(text);
        }
    }

    private static void AddUnique(List<string> items, string text)
    {
        if (!string.IsNullOrWhiteSpace(text) && !items.Contains(text))
        {
            items.Add(text);
        }
    }
}
