//////////////////////////////////////////////////////////
// 文件：Scripts/LocalizationGlossary.cs
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

using System;
using System.Collections.Generic;

// ──────────────────────────────────────────────────────────────────────────────
// Project-wide official localization term registry.
// ALL new text, JSON, definitions, UI, BattleLog, and Codex entries MUST use
// terms from this class. No free-form translation of game-specific vocabulary.
// ──────────────────────────────────────────────────────────────────────────────
/// <summary>
/// Localization System 的公开类：LocalizationGlossary。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class LocalizationGlossary
{
    // ── Resources ──────────────────────────────────────────────────────────────
    // 费 → Mana   (Forbidden: MP, Energy, AP, Cost, Resource Point)
    public const string Mana = "Mana";

    // ── Slay variants (杀) ─────────────────────────────────────────────────────
    // Forbidden as 杀 names: Strike, Slash, Attack
    public const string Slay        = "Slay";         // 杀
    public const string FireSlay    = "Fire Slay";    // 火杀
    public const string ThunderSlay = "Thunder Slay"; // 雷杀
    public const string IceSlay     = "Ice Slay";     // 冰杀
    public const string StormSlay   = "Storm Slay";   // 火雷杀
    public const string TrueSlay    = "True Slay";    // 必中杀
    public const string ShadowSlay  = "Shadow Slay";  // 影杀
    public const string PoisonSlay  = "Poison Slay";  // 毒杀

    // ── Card Types ─────────────────────────────────────────────────────────────
    // No "Card" suffix. Forbidden: Attack Card, Defense Card, etc.
    public const string CardTypeAttack      = "Attack";      // 攻击牌
    public const string CardTypeDefense     = "Defense";     // 防御牌
    public const string CardTypeRecovery    = "Recovery";    // 恢复牌
    public const string CardTypeResource    = "Resource";    // 资源牌
    public const string CardTypeEnhancement = "Enhancement"; // 强化牌
    public const string CardTypeStratagem   = "Stratagem";   // 锦囊牌
    public const string CardTypeSkill       = "Skill";       // 技能牌
    public const string CardTypeStargazing  = "Stargazing";  // 观星牌

    // ── Status Effects ─────────────────────────────────────────────────────────
    public const string Buff      = "Buff";
    public const string Debuff    = "Debuff";
    public const string Shield    = "Shield";
    public const string Block     = "Block";
    public const string Immune    = "Immune";
    public const string Freeze    = "Freeze";
    public const string Burn      = "Burn";
    public const string Poison    = "Poison";
    public const string Shock     = "Shock";
    public const string Charm     = "Charm";
    public const string Bleed     = "Bleed";
    public const string Darkness  = "Darkness";

    // ── Action Verbs (imperative, no trailing 's') ─────────────────────────────
    // Forbidden: Deals, Restores, Heals, Heal (for HP), Restore (for HP)
    public const string ActionDeal        = "Deal";         // 造成
    public const string ActionRecover     = "Recover HP";   // 恢复生命值
    public const string ActionGainMana    = "Gain Mana";    // 获得费
    public const string ActionLoseMana    = "Lose Mana";    // 失去费
    public const string ActionStealMana   = "Steal Mana";   // 偷取费
    public const string ActionGain        = "Gain";         // 获得
    public const string ActionLose        = "Lose";         // 失去
    public const string ActionBlock       = "Block";        // 防御
    public const string ActionImmune      = "Immune";       // 免疫
    public const string ActionCounter     = "Counter";      // 无懈
    public const string ActionIgnoreArmor = "Ignore Armor"; // 无视护甲
    public const string ActionIgnoreDodge = "Ignore Dodge"; // 无视闪避

    // ── Equipment Types ────────────────────────────────────────────────────────
    public const string EquipWeapon    = "Weapon";    // 武器
    public const string EquipArmor     = "Armor";     // 防具
    public const string EquipAccessory = "Accessory"; // 配件
    public const string EquipMount     = "Mount";     // 坐骑
    public const string EquipChip      = "Chip";      // 芯片

    // ── Rarity ────────────────────────────────────────────────────────────────
    public const string RarityCommon    = "Common";    // 普通
    public const string RarityRare      = "Rare";      // 稀有
    public const string RarityEpic      = "Epic";      // 史诗
    public const string RarityLegendary = "Legendary"; // 传说

    // ── Term lookup ───────────────────────────────────────────────────────────

    private static readonly Dictionary<string, string> _allTerms =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["mana"]             = Mana,
        ["slay"]             = Slay,
        ["fire_slay"]        = FireSlay,
        ["thunder_slay"]     = ThunderSlay,
        ["ice_slay"]         = IceSlay,
        ["storm_slay"]       = StormSlay,
        ["true_slay"]        = TrueSlay,
        ["shadow_slay"]      = ShadowSlay,
        ["poison_slay"]      = PoisonSlay,
        ["card_attack"]      = CardTypeAttack,
        ["card_defense"]     = CardTypeDefense,
        ["card_recovery"]    = CardTypeRecovery,
        ["card_resource"]    = CardTypeResource,
        ["card_enhancement"] = CardTypeEnhancement,
        ["card_stratagem"]   = CardTypeStratagem,
        ["card_skill"]       = CardTypeSkill,
        ["card_stargazing"]  = CardTypeStargazing,
        ["buff"]             = Buff,
        ["debuff"]           = Debuff,
        ["shield"]           = Shield,
        ["block"]            = Block,
        ["immune"]           = Immune,
        ["freeze"]           = Freeze,
        ["burn"]             = Burn,
        ["poison"]           = Poison,
        ["shock"]            = Shock,
        ["charm"]            = Charm,
        ["bleed"]            = Bleed,
        ["darkness"]         = Darkness,
        ["weapon"]           = EquipWeapon,
        ["armor"]            = EquipArmor,
        ["accessory"]        = EquipAccessory,
        ["mount"]            = EquipMount,
        ["chip"]             = EquipChip,
        ["common"]           = RarityCommon,
        ["rare"]             = RarityRare,
        ["epic"]             = RarityEpic,
        ["legendary"]        = RarityLegendary,
    };

    public static int TotalTermCount => _allTerms.Count;

    /// <summary>
    /// Localization System 的公开入口：GetTerm。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetTerm(string key) =>
        _allTerms.TryGetValue(key, out var v) ? v : key;

    /// <summary>
    /// Localization System 的公开入口：GetCardType。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetCardType(string key) => key switch
    {
        "attack"      => CardTypeAttack,
        "defense"     => CardTypeDefense,
        "recovery"    => CardTypeRecovery,
        "resource"    => CardTypeResource,
        "enhancement" => CardTypeEnhancement,
        "stratagem"   => CardTypeStratagem,
        "skill"       => CardTypeSkill,
        "stargazing"  => CardTypeStargazing,
        _ => key
    };

    /// <summary>
    /// Localization System 的公开入口：GetDamageType。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetDamageType(string key) => key switch
    {
        "slay"         => Slay,
        "fire_slay"    => FireSlay,
        "thunder_slay" => ThunderSlay,
        "ice_slay"     => IceSlay,
        "storm_slay"   => StormSlay,
        "true_slay"    => TrueSlay,
        "shadow_slay"  => ShadowSlay,
        "poison_slay"  => PoisonSlay,
        _ => key
    };

    /// <summary>
    /// Localization System 的公开入口：GetKeyword。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetKeyword(string key) => key switch
    {
        "freeze"   => Freeze,
        "burn"     => Burn,
        "poison"   => Poison,
        "shock"    => Shock,
        "charm"    => Charm,
        "bleed"    => Bleed,
        "darkness" => Darkness,
        "buff"     => Buff,
        "debuff"   => Debuff,
        "shield"   => Shield,
        "block"    => Block,
        "immune"   => Immune,
        _ => key
    };

    // ── Validator: banned term patterns ────────────────────────────────────────

    public static readonly IReadOnlyList<(string Pattern, string Reason)> BannedPatterns =
        new (string Pattern, string Reason)[]
        {
            (" MP",           "Use 'Mana', not 'MP'"),
            ("MP:",           "Use 'Mana', not 'MP'"),
            (" AP",           "Use 'Mana', not 'AP'"),
            ("AP.",           "Use 'Mana', not 'AP'"),
            ("AP)",           "Use 'Mana', not 'AP'"),
            (" Energy",       "Use 'Mana', not 'Energy'"),
            ("Attack Card",   "Use 'Attack' (no 'Card' suffix)"),
            ("Defense Card",  "Use 'Defense' (no 'Card' suffix)"),
            ("Recovery Card", "Use 'Recovery' (no 'Card' suffix)"),
            ("Resource Card", "Use 'Resource' (no 'Card' suffix)"),
            ("Response Card", "Use 'Response' (no 'Card' suffix)"),
            ("Stratagem Card","Use 'Stratagem' (no 'Card' suffix)"),
            ("Skill Card",    "Use 'Skill' (no 'Card' suffix)"),
            ("Stargazing Card","Use 'Stargazing' (no 'Card' suffix)"),
            ("Deals ",        "Use imperative 'Deal', not 'Deals'"),
            ("Restores",      "Use 'Recover HP', not 'Restores'"),
            ("Heals",         "Use 'Recover HP', not 'Heals'"),
            ("Strike-type",   "Use 'Slay' for 杀-type cards, not 'Strike'"),
            ("Slash-type",    "Use 'Slay' for 杀-type cards, not 'Slash'"),
        };

    // Keys that legitimately contain otherwise-banned patterns
    public static readonly IReadOnlySet<string> BannedPatternExceptions =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "skill.yingxi.name",          // 影袭 — Shadow Ambush skill, not a 杀 card
            "card.yingxiactivate.name",   // 影袭发动 — activation card, not a 杀 card
            "skill.bizhong.name",         // 必中 — sure-hit skill, not a 杀 card
            "skill.leiji.name",           // 雷击 — lightning skill, not a 杀 card
            "skill.yingxi.desc",          // describes Shadow Ambush mechanics
            "event.han_shi_zong_ci.desc", // "Restore the Han" — narrative historical phrase
            "status.dur.end_of_battle",   // "Restored after battle" — state mechanic, not HP
        };

    // ── Validator: concept consistency pairs ───────────────────────────────────
    // (zh_pattern, en_expected, en_forbidden[])
    // For any key whose zh value contains zh_pattern, the en value must not
    // contain any of en_forbidden.
    public static readonly IReadOnlyList<(string ZhContains, string EnExpected, string[] EnForbidden)>
        ConceptPairs = new (string, string, string[])[]
        {
            ("杀",    "Slay",      new[] { "Strike", "Slash" }),
            ("费",    "Mana",      new[] { " MP", " AP", " Energy" }),
            ("攻击牌","Attack",    new[] { "Attack Card" }),
            ("防御牌","Defense",   new[] { "Defense Card" }),
            ("恢复牌","Recovery",  new[] { "Recovery Card" }),
            ("资源牌","Resource",  new[] { "Resource Card" }),
            ("锦囊牌","Stratagem", new[] { "Stratagem Card" }),
            ("技能牌","Skill",     new[] { "Skill Card" }),
            ("观星牌","Stargazing",new[] { "Stargazing Card" }),
            ("响应牌","Response",  new[] { "Response Card" }),
        };
}
