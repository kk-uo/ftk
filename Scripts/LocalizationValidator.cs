//////////////////////////////////////////////////////////
// 文件：Scripts/LocalizationValidator.cs
//
// 模块：Localization System
//
// 职责：
// 1. 承载本地化文本加载、校验与显示适配相关代码。
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
using System.Text;
using System.Text.Json;

// ──────────────────────────────────────────────────────────────────────────────
// LocalizationValidator — developer-only diagnostics tool.
// Checks all ILocalizedDefinition objects against zh_CN.json / en_US.json.
// Auto-runs when DeveloperMode is on; can also be triggered via the debug UI.
// ──────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Localization System 的公开类：LocalizationValidator。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class LocalizationValidator
{
    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Localization System 的公开入口：RunIfDeveloperMode。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static LocalizationReport RunIfDeveloperMode()
    {
        if (!DeveloperModeManager.IsDeveloperMode) return new LocalizationReport();
        return Run();
    }

    /// <summary>
    /// Localization System 的公开入口：Run。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static LocalizationReport Run()
    {
        var report = new LocalizationReport();

        var zhPairs = LoadRawPairs("zh_CN");
        var enPairs = LoadRawPairs("en_US");
        var zhDict = PairsToDict(zhPairs);
        var enDict = PairsToDict(enPairs);
        report.TotalJsonKeys = zhDict.Count;

        // ⑤ Duplicate keys (raw pairs preserve duplicates)
        FindDuplicates(report, zhPairs, "zh_CN");
        FindDuplicates(report, enPairs, "en_US");

        // ④ Empty values
        FindEmptyValues(report, zhDict, "zh_CN");
        FindEmptyValues(report, enDict, "en_US");

        // Collect all definition-derived key references
        var refs = CollectAllRefs(report);
        var referencedKeys = BuildReferencedKeySet(refs);

        // ①②③⑦⑧ Per-definition checks
        CheckAllRefs(report, refs, zhDict, enDict);

        // ⑥ Unused keys
        FindUnusedKeys(report, referencedKeys, zhDict);

        // ⑦ Glossary compliance (banned patterns in en values)
        CheckGlossaryCompliance(report, enDict);

        // ⑦ Concept consistency (same zh concept must not map to multiple en terms)
        CheckConceptConsistency(report, zhDict, enDict);

        // ⑧-AS Asset code integrity (equipment + skills)
        CheckAssetCodes(report);

        PrintReport(report);
        SaveReport(report);
        return report;
    }

    // ── JSON loading ───────────────────────────────────────────────────────────

    private static List<(string Key, string Value)> LoadRawPairs(string code)
    {
        var path = $"res://Localization/{code}.json";
        if (!FileAccess.FileExists(path))
        {
            GD.PrintErr($"[LocalizationValidator] File not found: {path}");
            return new List<(string, string)>();
        }
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null) return new List<(string, string)>();
        var json = file.GetAsText();
        var pairs = new List<(string, string)>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
                pairs.Add((prop.Name, prop.Value.GetString() ?? string.Empty));
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[LocalizationValidator] Parse error in {code}.json: {ex.Message}");
        }
        return pairs;
    }

    private static Dictionary<string, string> PairsToDict(List<(string Key, string Value)> pairs)
    {
        var dict = new Dictionary<string, string>();
        foreach (var (k, v) in pairs) dict[k] = v;
        return dict;
    }

    // ── ⑤ Duplicate keys ───────────────────────────────────────────────────────

    private static void FindDuplicates(LocalizationReport r, List<(string Key, string Value)> pairs, string lang)
    {
        var seen = new HashSet<string>();
        foreach (var (key, _) in pairs)
            if (!seen.Add(key)) r.DuplicateKeys.Add($"[{lang}] {key}");
    }

    // ── ④ Empty values ─────────────────────────────────────────────────────────

    private static void FindEmptyValues(LocalizationReport r, Dictionary<string, string> dict, string lang)
    {
        foreach (var (key, val) in dict)
            if (string.IsNullOrWhiteSpace(val))
                r.EmptyTranslations.Add($"[{lang}] {key}");
    }

    // ── Definition collection ──────────────────────────────────────────────────

    private readonly record struct KeyRef(
        string Category,
        string DefinitionId,
        string Key,
        string KeyType,           // "name" | "desc" | "result" | "detail"
        bool Required = true);

    private static List<KeyRef> CollectAllRefs(LocalizationReport r)
    {
        var refs = new List<KeyRef>();

        // Characters (DescriptionKey allowed empty)
        foreach (var c in CharacterDatabase.GetAllCharacters())
        {
            r.CharacterCount++;
            refs.Add(new("Character", c.Id, c.NameKey, "name", true));
            if (!string.IsNullOrEmpty(c.DescriptionKey))
                refs.Add(new("Character", c.Id, c.DescriptionKey, "desc", false));
        }

        // Skills
        foreach (var s in SkillDatabase.All())
        {
            r.SkillCount++;
            refs.Add(new("Skill", s.Id, s.NameKey, "name", true));
            refs.Add(new("Skill", s.Id, s.DescriptionKey, "desc", true));
        }

        // Equipment
        foreach (var e in EquipmentDatabase.GetAllEquipments())
        {
            r.EquipmentCount++;
            refs.Add(new("Equipment", e.Id, e.NameKey, "name", true));
            refs.Add(new("Equipment", e.Id, e.DescriptionKey, "desc", true));
        }

        // Events
        foreach (var ev in EventDatabase.GetAllEvents())
        {
            r.EventCount++;
            refs.Add(new("Event", ev.Id, ev.NameKey, "name", true));
            if (!string.IsNullOrEmpty(ev.DescriptionKey))
                refs.Add(new("Event", ev.Id, ev.DescriptionKey, "desc", false));

            foreach (var opt in ev.Options)
            {
                if (!string.IsNullOrEmpty(opt.NameKey))
                    refs.Add(new("EventOption", $"{ev.Id}/{opt.Name}", opt.NameKey, "name", false));
                if (!string.IsNullOrEmpty(opt.DescriptionKey))
                    refs.Add(new("EventOption", $"{ev.Id}/{opt.Name}", opt.DescriptionKey, "desc", false));
                if (!string.IsNullOrEmpty(opt.ResultTextKey))
                    refs.Add(new("EventOption", $"{ev.Id}/{opt.Name}", opt.ResultTextKey, "result", false));
            }
        }

        // Enemies (DescriptionKey allowed empty)
        foreach (var e in EnemyDatabase.GetAllEnemies())
        {
            r.EnemyCount++;
            refs.Add(new("Enemy", e.Id, e.NameKey, "name", true));
        }

        // Cards — card name keys from BattleRules; desc keys by convention
        foreach (CardType type in Enum.GetValues<CardType>())
        {
            var nameKey = BattleRules.GetCardNameKey(type);
            if (string.IsNullOrEmpty(nameKey)) continue;
            r.CardCount++;
            refs.Add(new("Card", type.ToString(), nameKey, "name", true));
            var id = nameKey.Replace(".name", string.Empty); // e.g. card.kill
            refs.Add(new("Card", type.ToString(), $"{id}.desc", "desc", true));
        }

        // RunBuffs
        foreach (var rb in RunBuffDatabase.GetAll())
        {
            r.RunBuffCount++;
            refs.Add(new("RunBuff", rb.Id, rb.NameKey, "name", true));
            refs.Add(new("RunBuff", rb.Id, rb.DescriptionKey, "desc", true));
        }

        // InitialEvents (not ILocalizedDefinition, handled separately)
        foreach (var ie in InitialEventPool.AllEntries)
        {
            r.InitialEventCount++;
            if (!string.IsNullOrEmpty(ie.DescriptionKey))
                refs.Add(new("InitialEvent", ie.Id, ie.DescriptionKey, "desc", false));
            if (!string.IsNullOrEmpty(ie.EffectDetailKey))
                refs.Add(new("InitialEvent", ie.Id, ie.EffectDetailKey, "detail", false));
        }

        return refs;
    }

    private static HashSet<string> BuildReferencedKeySet(List<KeyRef> refs)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in refs)
            if (!string.IsNullOrEmpty(r.Key)) set.Add(r.Key);
        return set;
    }

    // ── ①②③⑦⑧ Per-definition checks ──────────────────────────────────────────

    private static void CheckAllRefs(
        LocalizationReport r,
        List<KeyRef> refs,
        Dictionary<string, string> zh,
        Dictionary<string, string> en)
    {
        foreach (var def in refs)
        {
            // ① Missing key field on definition
            if (string.IsNullOrEmpty(def.Key))
            {
                if (def.Required)
                {
                    if (def.KeyType == "name")
                        r.MissingNameKey.Add($"[{def.Category}] {def.DefinitionId}");
                    else if (def.KeyType == "desc")
                        r.MissingDescriptionKey.Add($"[{def.Category}] {def.DefinitionId}");
                }
                continue;
            }

            // ③ Check JSON contains the key
            if (!zh.ContainsKey(def.Key)) r.MissingZhCnKey.Add(def.Key);
            if (!en.ContainsKey(def.Key)) r.MissingEnUsKey.Add(def.Key);

            // ⑧ Naming convention
            if (!KeyMatchesConvention(def.Category, def.Key, def.KeyType))
                r.NamingWarnings.Add($"[{def.Category}:{def.KeyType}] {def.DefinitionId} → {def.Key}");
        }
    }

    // ── ⑧ Naming convention ────────────────────────────────────────────────────

    private static bool KeyMatchesConvention(string category, string key, string keyType)
    {
        var suffix = keyType switch
        {
            "name" => ".name",
            "desc" => ".desc",
            "result" => ".result",
            "detail" => ".detail",
            _ => string.Empty
        };
        if (!string.IsNullOrEmpty(suffix) && !key.EndsWith(suffix, StringComparison.Ordinal))
            return false;

        return category switch
        {
            "Skill" => key.StartsWith("skill.", StringComparison.Ordinal),
            "Equipment" => key.StartsWith("equipment.", StringComparison.Ordinal),
            "Event" => key.StartsWith("event.", StringComparison.Ordinal),
            "EventOption" => key.StartsWith("event.", StringComparison.Ordinal),
            "Character" => key.StartsWith("character.", StringComparison.Ordinal),
            "Enemy" => key.StartsWith("enemy.", StringComparison.Ordinal),
            "RunBuff" => key.StartsWith("runbuff.", StringComparison.Ordinal) || key.StartsWith("buff.", StringComparison.Ordinal),
            "Card" => key.StartsWith("card.", StringComparison.Ordinal),
            "InitialEvent" => key.StartsWith("initial_event.", StringComparison.Ordinal),
            _ => true
        };
    }

    // ── ⑥ Unused keys ─────────────────────────────────────────────────────────

    // Keys with these prefixes are used by code (UI, formats, etc.) not definitions.
    // They are excluded from the "unused" report to reduce noise.
    private static readonly HashSet<string> KnownCodePrefixes = new(StringComparer.Ordinal)
    {
        "ui.", "shop.", "battle.", "reward.", "run.", "chapter.", "mainflow.",
        "rarity.", "skill.kind.", "equip.", "map.", "initial_event.", "char.",
        "menu.", "status.", "element.", "chip.", "event.category.", "event.type.",
        "card.type.", "card.ui.", "codex.", "buff."
    };

    private static void FindUnusedKeys(
        LocalizationReport r,
        HashSet<string> referenced,
        Dictionary<string, string> zhDict)
    {
        foreach (var key in zhDict.Keys)
        {
            if (referenced.Contains(key)) continue;
            if (HasKnownCodePrefix(key)) continue;
            r.UnusedKeys.Add(key);
        }
    }

    private static bool HasKnownCodePrefix(string key)
    {
        foreach (var prefix in KnownCodePrefixes)
            if (key.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
    }

    // ── ⑦ Glossary compliance ──────────────────────────────────────────────────

    private static void CheckGlossaryCompliance(LocalizationReport r, Dictionary<string, string> enDict)
    {
        foreach (var (key, value) in enDict)
        {
            if (LocalizationGlossary.BannedPatternExceptions.Contains(key)) continue;
            foreach (var (pattern, reason) in LocalizationGlossary.BannedPatterns)
            {
                if (value.Contains(pattern, StringComparison.Ordinal))
                    r.GlossaryViolations.Add($"[{pattern}] {key} — {reason}");
            }
        }
    }

    private static void CheckConceptConsistency(
        LocalizationReport r,
        Dictionary<string, string> zhDict,
        Dictionary<string, string> enDict)
    {
        foreach (var (key, zhValue) in zhDict)
        {
            if (!enDict.TryGetValue(key, out var enValue)) continue;
            if (LocalizationGlossary.BannedPatternExceptions.Contains(key)) continue;

            foreach (var (zhContains, enExpected, enForbidden) in LocalizationGlossary.ConceptPairs)
            {
                if (!zhValue.Contains(zhContains, StringComparison.Ordinal)) continue;
                foreach (var forbidden in enForbidden)
                {
                    if (enValue.Contains(forbidden, StringComparison.Ordinal))
                    {
                        r.ConceptInconsistencies.Add(
                            $"[zh:{zhContains} → expected en:{enExpected}] {key} uses '{forbidden}'");
                    }
                }
            }
        }
    }

    // ── Output ─────────────────────────────────────────────────────────────────

    // ── Asset Code Validator ───────────────────────────────────────────────────

    private static void CheckAssetCodes(LocalizationReport report)
    {
        CheckEquipmentAssetCodes(report);
        CheckSkillAssetCodes(report);
    }

    private static void CheckEquipmentAssetCodes(LocalizationReport report)
    {
        var all = EquipmentDatabase.GetAllEquipments();
        var seenCodes = new Dictionary<string, string>(StringComparer.Ordinal);
        var seenIds   = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var def in all)
        {
            // ③ Internal Id duplicate
            if (seenIds.TryGetValue(def.Id, out var prevName))
                report.AssetCodeIssues.Add($"[③ DupId] Equipment Id '{def.Id}' shared by '{prevName}' and '{def.DisplayName}'");
            else
                seenIds[def.Id] = def.DisplayName;

            // ① Empty AssetCode
            if (string.IsNullOrEmpty(def.AssetCode))
            {
                report.AssetCodeIssues.Add($"[① Empty] Equipment '{def.Id}' has no AssetCode");
                continue;
            }

            // Code format: EQ + 5 digits = 7 chars total
            if (def.AssetCode.Length != 7
                || !def.AssetCode.StartsWith("EQ", StringComparison.Ordinal)
                || !long.TryParse(def.AssetCode[2..], out _))
            {
                report.AssetCodeIssues.Add($"[Format] Equipment '{def.Id}' has malformed code '{def.AssetCode}' (expected EQ + 5 digits)");
                continue;
            }

            // ② Duplicate AssetCode
            if (seenCodes.TryGetValue(def.AssetCode, out var prevId))
                report.AssetCodeIssues.Add($"[② DupCode] '{def.AssetCode}' used by both '{prevId}' and '{def.Id}'");
            else
                seenCodes[def.AssetCode] = def.Id;

            // ④ NameKey missing
            if (string.IsNullOrEmpty(def.NameKey))
                report.AssetCodeIssues.Add($"[④ NameKey] Equipment '{def.Id}' ({def.AssetCode}) missing NameKey");

            // ⑤ DescriptionKey missing
            if (string.IsNullOrEmpty(def.DescriptionKey))
                report.AssetCodeIssues.Add($"[⑤ DescKey] Equipment '{def.Id}' ({def.AssetCode}) missing DescriptionKey");

            // ⑥ Type digit vs primary EquipmentType
            var expectedDigit = GetExpectedTypeDigit(def.Types);
            var actualDigit = def.AssetCode[2];
            if (actualDigit != expectedDigit)
                report.AssetCodeIssues.Add(
                    $"[⑥ TypeMismatch] Equipment '{def.Id}' ({def.AssetCode}) code type='{actualDigit}' but expected '{expectedDigit}'");
        }
    }

    private static void CheckSkillAssetCodes(LocalizationReport report)
    {
        var all = SkillDatabase.All();
        var seenCodes = new Dictionary<string, string>(StringComparer.Ordinal);
        var seenIds   = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var skill in all)
        {
            // ③ Internal Id duplicate
            if (seenIds.TryGetValue(skill.Id, out var prevName))
                report.AssetCodeIssues.Add($"[③ DupId] Skill Id '{skill.Id}' shared by '{prevName}' and '{skill.DisplayName}'");
            else
                seenIds[skill.Id] = skill.DisplayName;

            // ① Empty AssetCode
            if (string.IsNullOrEmpty(skill.AssetCode))
            {
                report.AssetCodeIssues.Add($"[① Empty] Skill '{skill.Id}' has no AssetCode");
                continue;
            }

            // Code format: SK + 5 digits = 7 chars total
            if (skill.AssetCode.Length != 7
                || !skill.AssetCode.StartsWith("SK", StringComparison.Ordinal)
                || !long.TryParse(skill.AssetCode[2..], out _))
            {
                report.AssetCodeIssues.Add($"[Format] Skill '{skill.Id}' has malformed code '{skill.AssetCode}' (expected SK + 5 digits)");
                continue;
            }

            // ② Duplicate AssetCode
            if (seenCodes.TryGetValue(skill.AssetCode, out var prevId))
                report.AssetCodeIssues.Add($"[② DupCode] '{skill.AssetCode}' used by both '{prevId}' and '{skill.Id}'");
            else
                seenCodes[skill.AssetCode] = skill.Id;

            // ④ NameKey missing
            if (string.IsNullOrEmpty(skill.NameKey))
                report.AssetCodeIssues.Add($"[④ NameKey] Skill '{skill.Id}' ({skill.AssetCode}) missing NameKey");
        }
    }

    private static char GetExpectedTypeDigit(IReadOnlyList<EquipmentType> types)
    {
        foreach (var t in types)
        {
            if (t == EquipmentType.Weapon)   return '1';
            if (t == EquipmentType.Armor)    return '2';
        }
        foreach (var t in types)
        {
            if (t == EquipmentType.Accessory) return '3';
        }
        foreach (var t in types)
        {
            if (t == EquipmentType.Mount || t == EquipmentType.Vehicle) return '4';
        }
        return '5';
    }

    private static void PrintReport(LocalizationReport r)
    {
        GD.Print(r.ToFullText());
    }

    private static void SaveReport(LocalizationReport r)
    {
        var text = r.ToFullText();
        try
        {
            var path = OS.GetUserDataDir().PathJoin("localization_report.txt");
            using var file = FileAccess.Open("user://localization_report.txt", FileAccess.ModeFlags.Write);
            if (file != null)
            {
                file.StoreString(text);
                GD.Print($"[LocalizationValidator] Report saved → {path}");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[LocalizationValidator] Failed to save report: {ex.Message}");
        }
    }
}

// ──────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Localization System 的公开类：LocalizationReport。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class LocalizationReport
{
    // Statistics
    public int CharacterCount;
    public int SkillCount;
    public int EquipmentCount;
    public int EventCount;
    public int EnemyCount;
    public int CardCount;
    public int RunBuffCount;
    public int InitialEventCount;
    public int TotalJsonKeys;

    // Issues
    public readonly List<string> MissingNameKey = new();
    public readonly List<string> MissingDescriptionKey = new();
    public readonly List<string> MissingZhCnKey = new();
    public readonly List<string> MissingEnUsKey = new();
    public readonly List<string> EmptyTranslations = new();
    public readonly List<string> DuplicateKeys = new();
    public readonly List<string> UnusedKeys = new();
    public readonly List<string> NamingWarnings = new();
    public readonly List<string> GlossaryViolations = new();
    public readonly List<string> ConceptInconsistencies = new();
    public readonly List<string> AssetCodeIssues = new();

    public int TotalMissing => MissingNameKey.Count + MissingDescriptionKey.Count + MissingZhCnKey.Count + MissingEnUsKey.Count;

    /// <summary>
    /// Localization System 的公开入口：ToFullText。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public string ToFullText()
    {
        var sb = new StringBuilder();
        var line = new string('=', 50);
        var dash = new string('-', 50);

        sb.AppendLine(line);
        sb.AppendLine("  Localization Report");
        sb.AppendLine(line);
        sb.AppendLine();
        sb.AppendLine("  Summary");
        sb.AppendLine(dash);
        sb.AppendLine($"  Characters      : {CharacterCount}");
        sb.AppendLine($"  Skills          : {SkillCount}");
        sb.AppendLine($"  Equipments      : {EquipmentCount}");
        sb.AppendLine($"  Events          : {EventCount}");
        sb.AppendLine($"  Enemies         : {EnemyCount}");
        sb.AppendLine($"  Cards           : {CardCount}");
        sb.AppendLine($"  RunBuffs        : {RunBuffCount}");
        sb.AppendLine($"  InitialEvents   : {InitialEventCount}");
        sb.AppendLine($"  Total JSON Keys         : {TotalJsonKeys}");
        sb.AppendLine($"  Missing Keys            : {TotalMissing}");
        sb.AppendLine($"  Empty Values            : {EmptyTranslations.Count}");
        sb.AppendLine($"  Duplicate Keys          : {DuplicateKeys.Count}");
        sb.AppendLine($"  Unused Keys             : {UnusedKeys.Count}");
        sb.AppendLine($"  Naming Warnings         : {NamingWarnings.Count}");
        sb.AppendLine($"  Glossary Terms          : {LocalizationGlossary.TotalTermCount}");
        sb.AppendLine($"  Glossary Violations     : {GlossaryViolations.Count}");
        sb.AppendLine($"  Concept Inconsistencies : {ConceptInconsistencies.Count}");
        sb.AppendLine($"  AssetCode Issues        : {AssetCodeIssues.Count}");
        sb.AppendLine();

        AppendSection(sb, "① Missing NameKey", MissingNameKey);
        AppendSection(sb, "② Missing DescriptionKey", MissingDescriptionKey);
        AppendSection(sb, "③ Missing zh_CN Key", MissingZhCnKey);
        AppendSection(sb, "③ Missing en_US Key", MissingEnUsKey);
        AppendSection(sb, "④ Empty Translation Values", EmptyTranslations);
        AppendSection(sb, "⑤ Duplicate Keys", DuplicateKeys);
        AppendSection(sb, "⑥ Unused Keys (not referenced by any definition)", UnusedKeys);
        AppendSection(sb, "⑧ Naming Convention Warnings", NamingWarnings);
        AppendSection(sb, "⑦-A Glossary Violations (banned terms in en_US)", GlossaryViolations);
        AppendSection(sb, "⑦-B Concept Inconsistencies (same zh → multiple en)", ConceptInconsistencies);
        AppendSection(sb, "⑧-AS Asset Code Issues (Equipment + Skills)", AssetCodeIssues);

        sb.AppendLine(line);
        return sb.ToString();
    }

    private static void AppendSection(StringBuilder sb, string title, List<string> items)
    {
        if (items.Count == 0) return;
        var dash = new string('-', 50);
        sb.AppendLine(dash);
        sb.AppendLine($"  {title} ({items.Count})");
        sb.AppendLine(dash);
        foreach (var item in items)
            sb.AppendLine($"  {item}");
        sb.AppendLine();
    }
}
