//////////////////////////////////////////////////////////
// 文件：Scripts/Localization.cs
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
using System.Linq;
using System.Text.Json;

// Localization.Get(key) — fallback chain: current language → zh_CN → "【Missing: key】"
// SetLanguage() saves to user://settings.cfg and fires LanguageChanged for all UI to refresh.
// To add a new language: drop a new JSON file in res://Localization/ and add its code to SupportedLanguages.
/// <summary>
/// Localization System 的公开类：Localization。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class Localization
{
    // ======================================================
    // 本地化加载策略
    // ======================================================
    // zh_CN 是稳定 fallback，当前语言只保存差异文本。任何 UI 都应监听
    // LanguageChanged 并刷新自身显示，不应缓存已经翻译后的字符串。
    //
    // 设计原因：
    // - 缺失 key 时优先显示中文，方便开发阶段定位问题。
    // - 切换语言不重启场景，Inventory / Map / Settings 等界面可即时刷新。
    // - 数据定义只保存 key，显示层通过 GetName / GetDescription 统一取文本。
    private static Dictionary<string, string> _current = new();
    private static Dictionary<string, string> _fallback = new();
    private const string DefaultLanguageCode = "en_US";
    private static string _languageCode = DefaultLanguageCode;

    public static string CurrentLanguage => _languageCode;

    public static readonly string[] SupportedLanguages = { "zh_CN", "en_US" };

    private const string ConfigPath = "user://settings.cfg";
    private const string ConfigSection = "localization";
    private const string ConfigKey = "language";

    public static event Action? LanguageChanged;

    /// <summary>
    /// Localization System 的公开入口：Initialize。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void Initialize()
    {
        _fallback = LoadFile("zh_CN");
        var saved = LoadSavedLanguage();
        _languageCode = saved;
        _current = _languageCode == "zh_CN" ? _fallback : LoadFile(_languageCode);
        GD.Print($"[Localization] Initialized with language={_languageCode}");
    }

    /// <summary>
    /// Localization System 的公开入口：SetLanguage。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetLanguage(string code)
    {
        if (_languageCode == code) return;
        _languageCode = code;
        _current = code == "zh_CN" ? _fallback : LoadFile(code);
        SaveLanguage(code);
        GD.Print($"[Localization] Language changed to {code}");
        LanguageChanged?.Invoke();
    }

    /// <summary>
    /// Localization System 的公开入口：Get。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string Get(string key)
    {
        if (_current.TryGetValue(key, out var val)) return val;
        if (_fallback.TryGetValue(key, out var fb)) return fb;
        return $"【Missing: {key}】";
    }

    /// <summary>
    /// Localization System 的公开入口：GetFmt。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetFmt(string key, params object[] args)
    {
        return string.Format(Get(key), args);
    }

    /// <summary>
    /// Localization System 的公开入口：GetOrFallback。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetOrFallback(string? key, string fallback)
    {
        if (!string.IsNullOrEmpty(key))
        {
            var val = Get(key);
            if (!val.StartsWith("【Missing:")) return val;
        }
        return fallback;
    }

    /// <summary>
    /// Localization System 的公开入口：GetName。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetName(ILocalizedDefinition def)
        => string.IsNullOrEmpty(def.NameKey) ? string.Empty : Get(def.NameKey);

    /// <summary>
    /// Localization System 的公开入口：GetDescription。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetDescription(ILocalizedDefinition def)
        => string.IsNullOrEmpty(def.DescriptionKey) ? string.Empty : Get(def.DescriptionKey);

    /// <summary>
    /// 返回只描述装备外观与佩戴方式的文本，不混入装备数值效果。
    ///
    /// 专属 flavor 文本允许逐件补充；缺失时按主槽位生成稳定的本地化说明，
    /// 因而旧装备不需要修改构造参数，也不会退回到功能描述。
    /// </summary>
    public static string GetEquipmentFlavorDescription(EquipmentDefinition definition)
    {
        var dedicated = GetOrFallback(definition.FlavorDescriptionKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(dedicated))
        {
            return dedicated;
        }

        var templateKey = definition.Types.Contains(EquipmentType.Weapon)
            ? "codex.equip.flavor.weapon_fmt"
            : definition.Types.Contains(EquipmentType.Armor)
                ? "codex.equip.flavor.armor_fmt"
                : definition.Types.Contains(EquipmentType.Accessory)
                    ? "codex.equip.flavor.accessory_fmt"
                    : definition.Types.Contains(EquipmentType.Vehicle)
                        ? "codex.equip.flavor.vehicle_fmt"
                        : definition.Types.Contains(EquipmentType.Mount)
                            ? "codex.equip.flavor.mount_fmt"
                            : "codex.equip.flavor.general_fmt";
        return GetFmt(templateKey, GetName(definition));
    }

    /// <summary>
    /// 返回指定装备效果的本地化文本。
    ///
    /// 效果键缺失时只回退到该条效果定义，不会回退到装备简介。
    /// </summary>
    public static string GetEquipmentEffectDescription(EquipmentDefinition definition, int effectIndex)
    {
        if (effectIndex < 0 || effectIndex >= definition.Effects.Count)
        {
            return string.Empty;
        }

        var stem = definition.DescriptionKey.EndsWith(".desc", StringComparison.Ordinal)
            ? definition.DescriptionKey[..^".desc".Length]
            : $"equipment.{definition.Id}";
        return GetOrFallback($"{stem}.effect.{effectIndex + 1}", definition.Effects[effectIndex].Description);
    }

    /// <summary>Returns all equipment effect lines in the current UI language.</summary>
    public static string GetEquipmentEffectDescriptions(EquipmentDefinition definition, string separator)
    {
        return string.Join(separator, definition.Effects
            .Select((_, index) => GetEquipmentEffectDescription(definition, index)));
    }

    /// <summary>
    /// Returns one equipment source/unlock condition for display.  Equipment
    /// definitions keep their original source strings because some gameplay
    /// systems use them as internal tags; UI must go through this method so
    /// those internal Chinese strings never leak into another language.
    /// </summary>
    public static string GetEquipmentUnlockCondition(EquipmentDefinition definition, int conditionIndex)
    {
        if (conditionIndex < 0 || conditionIndex >= definition.UnlockConditions.Count)
        {
            return string.Empty;
        }

        return GetOrFallback(
            $"equipment.{definition.Id}.source.{conditionIndex + 1}",
            definition.UnlockConditions[conditionIndex]);
    }

    /// <summary>Returns all displayable equipment sources in their current UI language.</summary>
    public static string GetEquipmentUnlockConditions(EquipmentDefinition definition, string separator)
    {
        return string.Join(separator, definition.UnlockConditions
            .Select((_, index) => GetEquipmentUnlockCondition(definition, index)));
    }

    /// <summary>
    /// Localization System 的公开入口：GetRarityName。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetRarityName(SkillRarity rarity) => rarity switch
    {
        SkillRarity.Common => Get("rarity.common"),
        SkillRarity.Rare => Get("rarity.rare"),
        SkillRarity.Epic => Get("rarity.epic"),
        SkillRarity.Legendary => Get("rarity.legendary"),
        _ => rarity.ToString()
    };

    /// <summary>
    /// Localization System 的公开入口：GetRarityName。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetRarityName(EquipmentRarity rarity) => rarity switch
    {
        EquipmentRarity.Common => Get("rarity.common"),
        EquipmentRarity.Rare => Get("rarity.rare"),
        EquipmentRarity.Epic => Get("rarity.epic"),
        EquipmentRarity.Legendary => Get("rarity.legendary"),
        _ => rarity.ToString()
    };

    /// <summary>
    /// Localization System 的公开入口：GetSkillKindName。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetSkillKindName(SkillKind kind) => kind switch
    {
        SkillKind.Passive => Get("skill.kind.passive"),
        SkillKind.Active => Get("skill.kind.active"),
        SkillKind.Card => Get("skill.kind.card"),
        SkillKind.Enchantment => Get("skill.kind.enchantment"),
        _ => kind.ToString()
    };

    /// <summary>
    /// Localization System 的公开入口：GetEquipmentTypeName。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetEquipmentTypeName(EquipmentType type) => type switch
    {
        EquipmentType.Buff => Get("equip.type.buff"),
        EquipmentType.Weapon => Get("equip.type.weapon"),
        EquipmentType.Armor => Get("equip.type.armor"),
        EquipmentType.Vehicle => Get("equip.type.vehicle"),
        EquipmentType.Mount => Get("equip.type.mount"),
        EquipmentType.Defense => Get("equip.type.defense"),
        EquipmentType.Attack => Get("equip.type.attack"),
        EquipmentType.Accessory => Get("equip.type.accessory"),
        _ => type.ToString()
    };

    private static Dictionary<string, string> LoadFile(string code)
    {
        var path = $"res://Localization/{code}.json";
        if (!FileAccess.FileExists(path))
        {
            GD.PrintErr($"[Localization] File not found: {path}");
            return new Dictionary<string, string>();
        }
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr($"[Localization] Cannot open: {path}");
            return new Dictionary<string, string>();
        }
        var json = file.GetAsText();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[Localization] Parse error in {path}: {ex.Message}");
            return new Dictionary<string, string>();
        }
    }

    private static string LoadSavedLanguage()
    {
        var config = new ConfigFile();
        if (config.Load(ConfigPath) == Error.Ok)
        {
            var val = config.GetValue(ConfigSection, ConfigKey, DefaultLanguageCode);
            var code = val.AsString();
            // Validate that the saved code is actually supported
            foreach (var lang in SupportedLanguages)
                if (lang == code) return code;
        }
        return DefaultLanguageCode;
    }

    private static void SaveLanguage(string code)
    {
        var config = new ConfigFile();
        config.Load(ConfigPath);
        config.SetValue(ConfigSection, ConfigKey, code);
        config.Save(ConfigPath);
    }
}
