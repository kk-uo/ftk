//////////////////////////////////////////////////////////
// 文件：Scripts/Skill.cs
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

using System.Collections.Generic;

/// <summary>
/// Core System 的公开枚举：SkillCategory。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum SkillCategory
{
    General,
    CharacterExclusive
}

/// <summary>
/// Core System 的公开枚举：SkillRarity。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum SkillRarity
{
    Common,
    Rare,
    Epic,
    Legendary
}

/// <summary>
/// Core System 的公开枚举：SkillKind。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum SkillKind
{
    Passive,
    Active,
    Card,
    Enchantment
}

/// <summary>
/// Core System 的公开枚举：SkillSource。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum SkillSource
{
    Character,
    Event,
    Shop,
    Equipment,
    Boss
}

/// <summary>
/// Core System 的公开类：Skill。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class Skill : IAssetDefinition
{
    /// <summary>
    /// Core System 的公开入口：Skill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public Skill(
        string id,
        string name,
        SkillCategory category,
        SkillRarity rarity,
        IReadOnlyList<SkillKind> kinds,
        string description,
        string? characterId,
        TriggerTiming? timing,
        EffectPriority? priority,
        string nameKey = "",
        string descriptionKey = "",
        string assetCode = "",
        bool canAppearInRandomChoicePool = true)
        : this(
            id,
            name,
            category,
            rarity,
            kinds,
            description,
            characterId,
            SkillSource.Character,
            timing,
            priority,
            nameKey,
            descriptionKey,
            assetCode,
            canAppearInRandomChoicePool)
    {
    }

    /// <summary>
    /// Core System 的公开入口：Skill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public Skill(
        string id,
        string name,
        SkillCategory category,
        SkillRarity rarity,
        IReadOnlyList<SkillKind> kinds,
        string description,
        string? characterId = null,
        SkillSource source = SkillSource.Character,
        TriggerTiming? timing = null,
        EffectPriority? priority = null,
        string nameKey = "",
        string descriptionKey = "",
        string assetCode = "",
        bool canAppearInRandomChoicePool = true)
    {
        Id = id;
        Name = name;
        Category = category;
        Rarity = rarity;
        Kinds = kinds;
        Description = description;
        CharacterId = characterId;
        Source = source;
        Timing = timing;
        Priority = priority;
        NameKey = nameKey;
        DescriptionKey = descriptionKey;
        AssetCode = assetCode;
        CanAppearInRandomChoicePool = canAppearInRandomChoicePool;
    }

    public string Id { get; }
    // Permanent unique identifier in SK-TNNNN format. Never modify after creation.
    public string AssetCode { get; }
    public string Name { get; }
    public string NameKey { get; }
    public SkillCategory Category { get; }
    public SkillRarity Rarity { get; }
    public IReadOnlyList<SkillKind> Kinds { get; }
    public string Description { get; }
    public string DescriptionKey { get; }
    public string? CharacterId { get; }
    public SkillSource Source { get; }
    public TriggerTiming? Timing { get; }
    public EffectPriority? Priority { get; }
    /// <summary>
    /// 是否允许进入随机技能二选一、三选一等 ChoiceProvider 候选池。
    ///
    /// 固定技能奖励和 Developer Mode 仍可直接按 Id 获取该技能。
    /// </summary>
    public bool CanAppearInRandomChoicePool { get; }
    public string DisplayName => Localization.GetOrFallback(NameKey, Name);
    public string DisplayDescription => Localization.GetOrFallback(DescriptionKey, Description);
    public string IconText => DisplayName.Length <= 2 ? DisplayName : DisplayName[..2];

    public string? ExclusiveCharacter
    {
        get
        {
            if (string.IsNullOrWhiteSpace(CharacterId))
            {
                return null;
            }

            return CharacterDatabase.GetCharacter(CharacterId).Name;
        }
    }
}

/// <summary>
/// Core System 的公开类：SkillIds。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class SkillIds
{
    public const string Wushuang = "wushuang";
    public const string Biyue = "biyue";
    public const string Bizhong = "bizhong";
    public const string Lianying = "lianying";
    public const string Longdan = "longdan";
    public const string Qingnang = "qingnang";
    public const string Keji = "keji";
    public const string Manzu = "manzu";
    public const string Guanxing = "guanxing";
    public const string Luoyi = "luoyi";
    public const string JiGu = "jigu";
    public const string Qianxun = "qianxun";
    public const string Luoshen = "luoshen";
    public const string Wansha = "wansha";
    public const string XueZhaiXueChou = "xuezhaixuechou";
    public const string XueZhaiXueChouNearDeath = "xuezhaixuechou_near_death";
    public const string HuaXing = "huaxing";
    public const string PoJun = "pojun";
    public const string YiCheng = "yicheng";
    public const string ChangZui = "changzui";
    public const string JiHan = "jihan";
    public const string JiuChi = "jiuchi";
    public const string RouLin = "roulin";
    public const string BaoNue = "baonue";
    public const string ManzuWang = "manzu_wang";
    public const string MoonGaze = "moon_gaze";
    public const string CelestialImpact = "celestial_impact";
    public const string Tuxi = "tuxi";

    // 黄月英专属技能
    public const string YingXi = "yingxi";
    public const string RuYingSuiXing = "ruyingsuixing";

    // 周泰专属技能
    public const string ZhouTaiBuQu = "zhoutai_buqu";
    public const string ZhouTaiFenJi = "zhoutai_fenji";
    // 庞统专属技能
    public const string PangTongNirvana = "pangtong_nirvana";
    public const string PangTongIronChain = "pangtong_iron_chain";

    // 七星坛事件：诸葛亮专属升级技能
    public const string TianJi = "tianji";

    // 董卓专属技能
    public const string DongZhuoBengHuai = "dongzhuo_benghuai";

    // 禁书库事件：吕蒙专属升级技能
    public const string KejiUnlimited = "keji_unlimited";

    // 蜀汉共生体 Boss 技能
    public const string Rende = "rende";
    public const string TaoyuanJiyi = "taoyuan_jiyi";
    public const string Wusheng = "wusheng";
    public const string Yijue = "yijue";
    public const string Paoxiao = "paoxiao";
    public const string PaoxiaoPlayer = "paoxiao_player";

    // 巨型脓包专属技能
    public const string ZiBao = "zibao";

    // 张角专属技能
    public const string LeiJi = "leiji";
    public const string HuangTian = "huangtian";

    // 幻象触手 Boss 技能
    public const string Illusion = "illusion";
    public const string Slime = "slime";

    // 第一章 Boss 专属技能
    public const string BossHealerPassive = "boss_healer_passive";
    public const string BossTraitorCombo = "boss_traitor_combo";
    public const string BuDao = "budao";

    // 通用技能
    public const string QianXin = "qianxin_devotion";
    public const string ChangYin = "chang_yin_jolly_drinking";
    public const string Plague = "plague_pestilence";
    public const string Meihuo = "meihuo_charm";
    public const string SiDi = "si_di_enemy_read";
    public const string JiAng = "jiang";
    public const string HunZi = "hunzi";

    /// <summary>孙尚香专属传奇被动：武库。统计背包装备转化为战斗/资源加成，详见 WuKuEffect.cs。</summary>
    public const string WuKu = "wuku";

    // 周瑜专属技能
    public const string YingZi = "ying_zi";
    public const string YeYan = "ye_yan";
}

/// <summary>
/// Core System 的公开类：SkillDatabase。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class SkillDatabase
{
    /// <summary>
    /// Core System 的公开入口：All。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static IReadOnlyList<Skill> All()
    {
        return new[]
        {
            Wushuang(),
            Biyue(),
            BizhongSkill(),
            LianyingSkill(),
            LongdanSkill(),
            QingnangSkill(),
            KejiSkill(),
            ManzuSkill(),
            ManzuWangSkill(),
            GuanxingSkill(),
            LuoyiSkill(),
            JiGuSkill(),
            QianxunSkill(),
            LuoshenSkill(),
            WanshaSkill(),
            XueZhaiXueChouSkill(),
            XueZhaiXueChouNearDeathSkill(),
            HuaXingSkill(),
            PoJunSkill(),
            YiChengSkill(),
            ChangZuiSkill(),
            JiHanSkill(),
            JiuChiSkill(),
            RouLinSkill(),
            BaoNueSkill(),
            MoonGazeSkill(),
            CelestialImpactSkill(),
            RendeSkill(),
            TaoyuanJiyiSkill(),
            WushengSkill(),
            YijueSkill(),
            PaoxiaoSkill(),
            YingXiSkill(),
            RuYingSuiXingSkill(),
            ZhouTaiBuQuSkill(),
            ZhouTaiFenJiSkill(),
            PangTongNirvanaSkill(),
            PangTongIronChainSkill(),
            KejiUnlimitedSkill(),
            TianJiSkill(),
            DongZhuoBengHuaiSkill(),
            ZiBaoSkill(),
            LeiJiSkill(),
            HuangTianSkill(),
            QianXinSkill(),
            ChangYinSkill(),
            PlagueSkill(),
            MeihuoSkill(),
            SiDiSkill(),
            TuxiSkill(),
            JiAngSkill(),
            HunZiSkill(),
            PaoxiaoPlayerSkill(),
            IllusionSkill(),
            SlimeSkill(),
            BossHealerPassiveSkill(),
            BossTraitorComboSkill(),
            BuDaoSkill(),
            WuKuSkill(),
            YingZiSkill(),
            YeYanSkill()
        };
    }

    /// <summary>
    /// Core System 的公开入口：GetSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill? GetSkill(string id)
    {
        foreach (var skill in All())
        {
            if (skill.Id == id)
            {
                return skill;
            }
        }

        return null;
    }

    /// <summary>
    /// Core System 的公开入口：Wushuang。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill Wushuang()
    {
        return new Skill(
            SkillIds.Wushuang,
            "无双",
            SkillCategory.CharacterExclusive,
            SkillRarity.Legendary,
            new[] { SkillKind.Passive, SkillKind.Enchantment },
            "所有杀系攻击牌的最终伤害 ×2。包含杀、火杀、雷杀、必中杀以及未来新增的杀系攻击牌。",
            CharacterIds.LuBu,
            TriggerTiming.OnDamage,
            EffectPriority.Lowest,
            nameKey: "skill.wushuang.name",
            descriptionKey: "skill.wushuang.desc",
            assetCode: "SK50001");
    }

    /// <summary>
    /// Core System 的公开入口：Biyue。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill Biyue()
    {
        return new Skill(
            SkillIds.Biyue,
            "闭月",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Passive },
            "【被动技能】战斗开始后，第一个回合结束时获得1点费用；之后每经过两个完整回合，再次获得1点费用。采用独立计数，不受额外回合、跳过回合、结束回合等效果影响。",
            CharacterIds.DiaoChan,
            TriggerTiming.OnBattlePostPhase,
            EffectPriority.Highest,
            nameKey: "skill.biyue.name",
            descriptionKey: "skill.biyue.desc",
            assetCode: "SK50002");
    }

    /// <summary>
    /// Core System 的公开入口：BizhongSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill BizhongSkill()
    {
        return new Skill(
            SkillIds.Bizhong,
            "必中",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Card },
            "马超的专属技能，代表其绝技【必中杀】：花费1费，对目标造成10点伤害，无法被【闪】格挡，招招命中。",
            CharacterIds.MaChao,
            nameKey: "skill.bizhong.name",
            descriptionKey: "skill.bizhong.desc",
            assetCode: "SK20001");
    }

    /// <summary>
    /// Core System 的公开入口：LianyingSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill LianyingSkill()
    {
        return new Skill(
            SkillIds.Lianying,
            "连营",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Passive },
            "当自己在本局游戏中第一次将费用消耗至0时，获得连营状态。下一回合开始时，本回合第一张普通杀免费；若未使用，回合结束后失效。",
            CharacterIds.LuXun,
            TriggerTiming.OnResourceChanged,
            EffectPriority.High,
            nameKey: "skill.lianying.name",
            descriptionKey: "skill.lianying.desc",
            assetCode: "SK40001");
    }

    /// <summary>
    /// Core System 的公开入口：LongdanSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill LongdanSkill()
    {
        return new Skill(
            SkillIds.Longdan,
            "龙胆",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Passive, SkillKind.Card },
            "当自己使用闪成功防御攻击后，可在战斗回合后发动。选择杀、火杀或雷杀，将本次对抗视为所选攻击牌继续结算；杀免费，火杀和雷杀各消耗1费。",
            CharacterIds.ZhaoYun,
            TriggerTiming.OnBattlePostPhase,
            EffectPriority.High,
            nameKey: "skill.longdan.name",
            descriptionKey: "skill.longdan.desc",
            assetCode: "SK20002");
    }

    /// <summary>
    /// Core System 的公开入口：MoonGazeSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill MoonGazeSkill()
    {
        return new Skill(
            SkillIds.MoonGaze,
            "月之凝视",
            SkillCategory.CharacterExclusive,
            SkillRarity.Legendary,
            new[] { SkillKind.Passive },
            "当战斗进行到第15回合时，使敌方获得【眩晕】。当战斗进行到第49回合时，敌方立刻死亡。该死亡属于机制杀，不可被任何效果免疫。",
            null,
            SkillSource.Boss,
            TriggerTiming.OnTurnStart,
            EffectPriority.Highest,
            nameKey: "skill.moon_gaze.name",
            descriptionKey: "skill.moon_gaze.desc",
            assetCode: "SK60004");
    }

    /// <summary>
    /// Core System 的公开入口：CelestialImpactSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill CelestialImpactSkill()
    {
        return new Skill(
            SkillIds.CelestialImpact,
            "天体撞击",
            SkillCategory.CharacterExclusive,
            SkillRarity.Common,
            new[] { SkillKind.Active, SkillKind.Card },
            "将自身攻击牌替换为【天体撞击】。天体撞击为1费的南蛮入侵，对敌方全体造成10点伤害。",
            null,
            SkillSource.Boss,
            TriggerTiming.OnBattlePhase,
            EffectPriority.Mid,
            nameKey: "skill.celestial_impact.name",
            descriptionKey: "skill.celestial_impact.desc",
            assetCode: "SK60005");
    }

    /// <summary>
    /// Core System 的公开入口：QingnangSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill QingnangSkill()
    {
        return new Skill(
            SkillIds.Qingnang,
            "青囊",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Passive },
            "桃的费用变为1费；桃成功格挡伤害时仍会恢复生命；桃的恢复可使当前生命超过最大生命值，超出部分仅在本关战斗中保留。",
            CharacterIds.HuaTuo,
            TriggerTiming.OnBeforeDamage,
            EffectPriority.Highest,
            nameKey: "skill.qingnang.name",
            descriptionKey: "skill.qingnang.desc",
            assetCode: "SK50003");
    }

    /// <summary>
    /// Core System 的公开入口：KejiSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill KejiSkill()
    {
        return new Skill(
            SkillIds.Keji,
            "克己",
            SkillCategory.CharacterExclusive,
            SkillRarity.Legendary,
            new[] { SkillKind.Passive },
            "费用不会因关卡切换而重置，且最高为15。",
            CharacterIds.LuMeng,
            TriggerTiming.OnDamage,
            EffectPriority.Lowest,
            nameKey: "skill.keji.name",
            descriptionKey: "skill.keji.desc",
            assetCode: "SK40002");
    }

    /// <summary>
    /// Core System 的公开入口：KejiUnlimitedSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill KejiUnlimitedSkill()
    {
        return new Skill(
            SkillIds.KejiUnlimited,
            "克己·无限制协议",
            SkillCategory.CharacterExclusive,
            SkillRarity.Legendary,
            new[] { SkillKind.Passive },
            "权限验证通过。资源限制协议已解除。\n" +
            "1. 费用上限提升至20（替换克己的15上限）。\n" +
            "2. 每拥有1点费用，造成伤害+10%（最多+200%，即×3）。\n" +
            "3. 免疫【顺手牵羊】。\n" +
            "4. 保留原【克己】全部基础功能（费用不重置）。",
            CharacterIds.LuMeng,
            TriggerTiming.OnDamage,
            EffectPriority.Mid,
            nameKey: "skill.keji_unlimited.name",
            descriptionKey: "skill.keji_unlimited.desc",
            assetCode: "SK40005");
    }

    /// <summary>
    /// Core System 的公开入口：ManzuSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill ManzuSkill()
    {
        return new Skill(
            SkillIds.Manzu,
            "蛮族",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Passive, SkillKind.Card },
            "初始牌组中额外拥有南蛮入侵；本局内南蛮入侵的费用变为2（仅对孟获自己生效）。",
            CharacterIds.MengHuo,
            nameKey: "skill.manzu.name",
            descriptionKey: "skill.manzu.desc",
            assetCode: "SK20003");
    }

    /// <summary>
    /// Core System 的公开入口：ManzuWangSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill ManzuWangSkill()
    {
        return new Skill(
            SkillIds.ManzuWang,
            "蛮族之王",
            SkillCategory.CharacterExclusive,
            SkillRarity.Legendary,
            new[] { SkillKind.Passive },
            "南蛮入侵伤害 ×2；每次南蛮入侵命中敌人后回复5点生命和0.5费。",
            CharacterIds.MengHuo,
            TriggerTiming.OnDamageTaken,
            EffectPriority.Low,
            nameKey: "skill.manzu_wang.name",
            descriptionKey: "skill.manzu_wang.desc",
            assetCode: "SK20004");
    }

    /// <summary>
    /// Core System 的公开入口：GuanxingSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill GuanxingSkill()
    {
        return new Skill(
            SkillIds.Guanxing,
            "观星",
            SkillCategory.CharacterExclusive,
            SkillRarity.Legendary,
            new[] { SkillKind.Active },
            "在出牌栏新增一张【观星】（0费）。使用后进入观星状态：下一回合正常出牌将被记录，之后两回合强制重复该出牌（费用免除，目标可重选）。观星不受顺手牵羊影响。",
            CharacterIds.ZhuGeLiang,
            nameKey: "skill.guanxing.name",
            descriptionKey: "skill.guanxing.desc",
            assetCode: "SK20005");
    }

    /// <summary>
    /// Core System 的公开入口：LuoyiSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill LuoyiSkill()
    {
        return new Skill(
            SkillIds.Luoyi,
            "裸衣",
            SkillCategory.CharacterExclusive,
            SkillRarity.Common,
            new[] { SkillKind.Passive },
            "你的所有攻击无视敌方闪/无懈等招式防御关系，但护盾仍可抵挡。使用攻击牌时，自身受到的伤害×2；该攻击视为出费但不获得费用，不会抵消、反制或无视敌方造成的伤害。",
            CharacterIds.MiHeng,
            TriggerTiming.OnDamage,
            EffectPriority.Lowest,
            nameKey: "skill.luoyi.name",
            descriptionKey: "skill.luoyi.desc",
            assetCode: "SK50004");
    }

    /// <summary>
    /// Core System 的公开入口：QianxunSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill QianxunSkill()
    {
        return new Skill(
            SkillIds.Qianxun,
            "谦逊",
            SkillCategory.CharacterExclusive,
            SkillRarity.Common,
            new[] { SkillKind.Passive, SkillKind.Enchantment },
            "免疫顺手牵羊（对持有者无效）；所有非攻击类锦囊（酒/桃/顺手牵羊/无懈可击/观星）费用-0.5（最低0）。",
            CharacterIds.LuXun,
            nameKey: "skill.qianxun.name",
            descriptionKey: "skill.qianxun.desc",
            assetCode: "SK40003");
    }

    /// <summary>
    /// Core System 的公开入口：JiGuSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill JiGuSkill()
    {
        return new Skill(
            SkillIds.JiGu,
            "击鼓",
            SkillCategory.CharacterExclusive,
            SkillRarity.Epic,
            new[] { SkillKind.Active, SkillKind.Card },
            "每次受到实际伤害时触发反应条；本场战斗发动一次后不再触发。发动后进入击鼓状态（3回合）：所有受到的伤害归零，每次造成伤害后立即恢复全部生命。",
            CharacterIds.MiHeng,
            TriggerTiming.OnDamageTaken,
            EffectPriority.Low,
            nameKey: "skill.jigu.name",
            descriptionKey: "skill.jigu.desc",
            assetCode: "SK50005");
    }

    /// <summary>
    /// Core System 的公开入口：LuoshenSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill LuoshenSkill()
    {
        return new Skill(
            SkillIds.Luoshen,
            "洛神",
            SkillCategory.CharacterExclusive,
            SkillRarity.Epic,
            new[] { SkillKind.Passive },
            "【被动技能】连续出费可获得递增费用：第1回合出费得1费，连续第2回合出费得2费，以此类推。若本回合没有使用【费】牌，则下回合开始时费用减半（向下取整；当前费用≤2时不触发）。",
            CharacterIds.ZhenJi,
            TriggerTiming.OnTurnStart,
            EffectPriority.High,
            nameKey: "skill.luoshen.name",
            descriptionKey: "skill.luoshen.desc",
            assetCode: "SK30001");
    }
    /// <summary>
    /// Core System 的公开入口：WanshaSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill WanshaSkill()
    {
        return new Skill(
            SkillIds.Wansha,
            "完杀",
            SkillCategory.CharacterExclusive,
            SkillRarity.Epic,
            new[] { SkillKind.Passive },
            "【被动技能】当任意角色通过治疗恢复生命值时，对其造成等同于本次治疗量的真实伤害（无视护甲、无视减伤、无视闪避，不属于攻击伤害）。若实际恢复量为0，则不触发。触发来源包含桃、酒、丹、破碎情感组件、发芽盆栽强化后的桃及所有生命回复事件。",
            CharacterIds.JiaXu,
            TriggerTiming.OnHeal,
            EffectPriority.Low,
            nameKey: "skill.wansha.name",
            descriptionKey: "skill.wansha.desc",
            assetCode: "SK50006");
    }

    /// <summary>
    /// Core System 的公开入口：XueZhaiXueChouSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill XueZhaiXueChouSkill()
    {
        return new Skill(
            SkillIds.XueZhaiXueChou,
            "血债血偿",
            SkillCategory.CharacterExclusive,
            SkillRarity.Epic,
            new[] { SkillKind.Passive },
            "【被动技能】当你受到伤害后，对所有敌人造成等同于本次受到伤害的真实伤害（无视护甲、无视减伤、无视藤甲、无视鳞甲、无视闪避，不属于攻击伤害）。该伤害不触发反击、龙胆、观星、酒增伤或任何攻击命中效果，且不会再次触发血债血偿。",
            CharacterIds.XiaHouDun,
            TriggerTiming.OnDamageTaken,
            EffectPriority.Low,
            nameKey: "skill.xuezhaixuechou.name",
            descriptionKey: "skill.xuezhaixuechou.desc",
            assetCode: "SK30002");
    }

    /// <summary>
    /// Core System 的公开入口：XueZhaiXueChouNearDeathSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill XueZhaiXueChouNearDeathSkill()
    {
        return new Skill(
            SkillIds.XueZhaiXueChouNearDeath,
            "血债血偿",
            SkillCategory.CharacterExclusive,
            SkillRarity.Epic,
            new[] { SkillKind.Passive },
            "【被动技能】当自身第一次濒死（生命值≤0）时，不立即死亡，进入濒死状态。下回合对玩家造成等同于自身最大生命值40%的真实伤害（无视护甲、无视闪避、无视无懈可击），随后死亡。每场战斗仅触发一次。",
            null,
            TriggerTiming.OnDying,
            EffectPriority.High,
            nameKey: "skill.xuezhaixuechou_near_death.name",
            descriptionKey: "skill.xuezhaixuechou_near_death.desc");
    }

    /// <summary>
    /// Core System 的公开入口：HuaXingSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill HuaXingSkill()
    {
        return new Skill(
            SkillIds.HuaXing,
            "化形",
            SkillCategory.CharacterExclusive,
            SkillRarity.Legendary,
            new[] { SkillKind.Passive },
            "【被动技能】战斗开始时触发。生命值变为与玩家当前血量一致，同时复制玩家所有技能与装备（芯片加成除外），保留【化形】和【仙毫】，失去自身原有属性。复制仅发生一次。",
            CharacterIds.ZuoCi,
            TriggerTiming.OnGameStart,
            EffectPriority.High,
            nameKey: "skill.huaxing.name",
            descriptionKey: "skill.huaxing.desc",
            assetCode: "SK50007");
    }

    /// <summary>
    /// Core System 的公开入口：PoJunSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill PoJunSkill()
    {
        return new Skill(
            SkillIds.PoJun,
            "破军",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Passive },
            "【被动技能】每次杀类型攻击命中敌人时，若拥有至少0.5费，可在反应条打出【破军】；失去0.5费，使该杀伤害额外增加一倍。单独乘算，可多次触发；每次触发只增加一倍伤害，不会指数翻倍。",
            CharacterIds.XuSheng,
            TriggerTiming.OnDamage,
            EffectPriority.Mid,
            nameKey: "skill.pojun.name",
            descriptionKey: "skill.pojun.desc",
            assetCode: "SK40004");
    }

    /// <summary>
    /// 徐盛专属被动【疑城】。
    /// </summary>
    public static Skill YiChengSkill()
    {
        return new Skill(
            SkillIds.YiCheng,
            "疑城",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Passive },
            "【被动技能】每次在战斗中失去费用时，回复5点生命；每次失去生命时获得0.25费，每回合可以多次触发。",
            CharacterIds.XuSheng,
            TriggerTiming.OnResourceChanged,
            EffectPriority.Low,
            nameKey: "skill.yicheng.name",
            descriptionKey: "skill.yicheng.desc",
            assetCode: "SK40005");
    }

    /// <summary>
    /// Core System 的公开入口：ChangZuiSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill ChangZuiSkill()
    {
        return new Skill(
            SkillIds.ChangZui,
            "长醉",
            SkillCategory.General,
            SkillRarity.Rare,
            new[] { SkillKind.Passive },
            "【被动技能】使用酒后，酒层数不会因回合结束而消失，仅在发动杀系攻击后才消耗对应层数。",
            null,
            TriggerTiming.OnTurnEnd,
            EffectPriority.High,
            nameKey: "skill.changzui.name",
            descriptionKey: "skill.changzui.desc",
            assetCode: "SK10001");
    }

    /// <summary>
    /// Core System 的公开入口：JiHanSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill JiHanSkill()
    {
        return new Skill(
            SkillIds.JiHan,
            "极寒",
            SkillCategory.General,
            SkillRarity.Epic,
            new[] { SkillKind.Card, SkillKind.Active },
            "【卡牌技能】【主动技能】在出牌栏解锁【冰杀】（费用3，造成10点伤害并施加冰冻：下回合只能出费）。",
            null,
            null,
            null,
            nameKey: "skill.jihan.name",
            descriptionKey: "skill.jihan.desc",
            assetCode: "SK10002");
    }

    /// <summary>
    /// Core System 的公开入口：JiuChiSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill JiuChiSkill()
    {
        return new Skill(
            SkillIds.JiuChi,
            "酒池",
            SkillCategory.CharacterExclusive,
            SkillRarity.Legendary,
            new[] { SkillKind.Passive },
            "你每回合使用的前三张酒免费。",
            CharacterIds.DongZhuo,
            SkillSource.Character,
            TriggerTiming.OnTurnStart,
            EffectPriority.High,
            nameKey: "skill.jiuchi.name",
            descriptionKey: "skill.jiuchi.desc",
            assetCode: "SK10003");
    }

    /// <summary>
    /// Core System 的公开入口：RouLinSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill RouLinSkill()
    {
        return new Skill(
            SkillIds.RouLin,
            "肉林",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Passive },
            "每当你的攻击未命中时，目标失去0.5费。每回合最多触发一次。",
            CharacterIds.DongZhuo,
            SkillSource.Character,
            TriggerTiming.OnBeforeDamage,
            EffectPriority.Low,
            nameKey: "skill.roulin.name",
            descriptionKey: "skill.roulin.desc",
            assetCode: "SK60001");
    }

    /// <summary>
    /// Core System 的公开入口：BaoNueSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill BaoNueSkill()
    {
        return new Skill(
            SkillIds.BaoNue,
            "暴虐",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Passive },
            "其他群势力角色造成伤害时，获得1费。每回合最多触发一次。",
            null,
            SkillSource.Boss,
            TriggerTiming.OnDamageTaken,
            EffectPriority.Low,
            nameKey: "skill.baonue.name",
            descriptionKey: "skill.baonue.desc",
            assetCode: "SK60003");
    }

    /// <summary>
    /// Core System 的公开入口：RendeSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill RendeSkill()
    {
        return new Skill(
            SkillIds.Rende,
            "仁德",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Passive },
            "每回合开始获得2层仁德护盾。每层抵挡一次完整伤害，未消耗层数在回合结束时失效。",
            CharacterIds.LiuBei,
            SkillSource.Character,
            TriggerTiming.OnBattlePostPhase,
            EffectPriority.Low,
            nameKey: "skill.rende.name",
            descriptionKey: "skill.rende.desc",
            assetCode: "SK60006");
    }

    /// <summary>
    /// Core System 的公开入口：TaoyuanJiyiSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill TaoyuanJiyiSkill()
    {
        return new Skill(
            SkillIds.TaoyuanJiyi,
            "桃园结义",
            SkillCategory.CharacterExclusive,
            SkillRarity.Legendary,
            new[] { SkillKind.Passive },
            "当共享生命池首次降至150以下时，触发一次：恢复100点共享生命。",
            CharacterIds.LiuBei,
            SkillSource.Character,
            TriggerTiming.OnDamageTaken,
            EffectPriority.Low,
            nameKey: "skill.taoyuan_jiyi.name",
            descriptionKey: "skill.taoyuan_jiyi.desc",
            assetCode: "SK60007");
    }

    /// <summary>
    /// Core System 的公开入口：WushengSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill WushengSkill()
    {
        return new Skill(
            SkillIds.Wusheng,
            "武圣",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Passive },
            "关羽的杀系攻击基础伤害额外+5。",
            CharacterIds.GuanYu,
            SkillSource.Character,
            TriggerTiming.OnDamage,
            EffectPriority.Mid,
            nameKey: "skill.wusheng.name",
            descriptionKey: "skill.wusheng.desc",
            assetCode: "SK60008");
    }

    /// <summary>
    /// Core System 的公开入口：YijueSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill YijueSkill()
    {
        return new Skill(
            SkillIds.Yijue,
            "义绝",
            SkillCategory.CharacterExclusive,
            SkillRarity.Epic,
            new[] { SkillKind.Passive },
            "若玩家上回合受到过伤害，关羽本回合杀系伤害×2。",
            CharacterIds.GuanYu,
            SkillSource.Character,
            TriggerTiming.OnDamage,
            EffectPriority.Mid,
            nameKey: "skill.yijue.name",
            descriptionKey: "skill.yijue.desc",
            assetCode: "SK60009");
    }

    /// <summary>
    /// Core System 的公开入口：PaoxiaoSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill PaoxiaoSkill()
    {
        return new Skill(
            SkillIds.Paoxiao,
            "咆哮",
            SkillCategory.CharacterExclusive,
            SkillRarity.Epic,
            new[] { SkillKind.Passive },
            "每次杀系攻击命中并造成伤害后，张飞的杀系基础伤害永久+5（本战斗内累积）。",
            CharacterIds.ZhangFei,
            SkillSource.Boss,
            TriggerTiming.OnDamageTaken,
            EffectPriority.Low,
            nameKey: "skill.paoxiao.name",
            descriptionKey: "skill.paoxiao.desc",
            assetCode: "SK60010");
    }

    /// <summary>
    /// Core System 的公开入口：PaoxiaoPlayerSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill PaoxiaoPlayerSkill()
    {
        return new Skill(
            SkillIds.PaoxiaoPlayer,
            "咆哮",
            SkillCategory.CharacterExclusive,
            SkillRarity.Epic,
            new[] { SkillKind.Passive },
            "【被动技能】你的杀系伤害在战斗中逐渐增强。每使用一次杀，下一回合起该战斗杀基础伤害+5（当回合使用的杀，伤害在下回合生效；N次杀累计→+N×5）。战斗结束后重置本场累积加成；每赢得一场战斗，杀系伤害永久+5（本局保留）。",
            CharacterIds.ZhangFei,
            TriggerTiming.OnDamage,
            EffectPriority.Mid,
            nameKey: "skill.paoxiao_player.name",
            descriptionKey: "skill.paoxiao_player.desc",
            assetCode: "SK20008");
    }

    /// <summary>
    /// Core System 的公开入口：YingXiSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill YingXiSkill()
    {
        return new Skill(
            SkillIds.YingXi,
            "影袭",
            SkillCategory.CharacterExclusive,
            SkillRarity.Legendary,
            new[] { SkillKind.Active, SkillKind.Card },
            "将【影袭】（费用可变：0/1/2）加入出牌区。使用后进入影袭状态（3回合）：全程无敌、控制免疫，且无法被顺手牵羊；出牌区仅剩【影袭杀】和【潜伏】。【影袭杀】花费1费，继承必中杀属性，每次影袭期间只能使用一次；命中并造成伤害后下次影袭费用为0。三回合未攻击→恢复1费；攻击未造成伤害→下次2费。",
            CharacterIds.HuangYueYing,
            TriggerTiming.OnBeforeDamage,
            EffectPriority.Immediate,
            nameKey: "skill.yingxi.name",
            descriptionKey: "skill.yingxi.desc",
            assetCode: "SK20006");
    }

    /// <summary>
    /// Core System 的公开入口：RuYingSuiXingSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill RuYingSuiXingSkill()
    {
        return new Skill(
            SkillIds.RuYingSuiXing,
            "如影随行",
            SkillCategory.CharacterExclusive,
            SkillRarity.Common,
            new[] { SkillKind.Passive },
            "①无法获得额外最大生命值（当前HP可恢复）。②事件扣血无效。③场上任意单位受到实际伤害时，全场所有角色费用重置为1。④黄月英造成的伤害若被桃护盾/酒护盾/仁德护盾完全抵消，获得1费。",
            CharacterIds.HuangYueYing,
            TriggerTiming.OnDamageTaken,
            EffectPriority.Low,
            nameKey: "skill.ruyingsuixing.name",
            descriptionKey: "skill.ruyingsuixing.desc",
            assetCode: "SK20007");
    }

    /// <summary>
    /// Core System 的公开入口：ZhouTaiBuQuSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill ZhouTaiBuQuSkill()
    {
        return new Skill(
            SkillIds.ZhouTaiBuQu,
            "不屈",
            SkillCategory.CharacterExclusive,
            SkillRarity.Epic,
            new[] { SkillKind.Passive },
            "【被动技能】每当生命值降至0或以下时进行1D6判定：掷出5或6时获得1点费用并回复至1点生命。本场战斗第一次判定失败时，也会获得1点费用并回复至1点生命；之后判定失败则继续濒死/复活流程。",
            CharacterIds.ZhouTai,
            TriggerTiming.OnDying,
            EffectPriority.Highest,
            nameKey: "skill.zhoutai_buqu.name",
            descriptionKey: "skill.zhoutai_buqu.desc",
            assetCode: "SK20009");
    }

    /// <summary>
    /// Core System 的公开入口：ZhouTaiFenJiSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill ZhouTaiFenJiSkill()
    {
        return new Skill(
            SkillIds.ZhouTaiFenJi,
            "奋激",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Passive },
            "【被动技能】每当你的生命值降至0或以下并进入濒死结算时，永久获得2点攻击性锦囊伤害固定加值（本Run有效，跨战斗/章节保留，新Run重置）。",
            CharacterIds.ZhouTai,
            TriggerTiming.OnDying,
            EffectPriority.Immediate,
            nameKey: "skill.zhoutai_fenji.name",
            descriptionKey: "skill.zhoutai_fenji.desc",
            assetCode: "SK20011");
    }

    /// <summary>
    /// Core System 的公开入口：PangTongNirvanaSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill PangTongNirvanaSkill()
    {
        return new Skill(
            SkillIds.PangTongNirvana,
            "涅槃",
            SkillCategory.CharacterExclusive,
            SkillRarity.Epic,
            new[] { SkillKind.Passive },
            "【被动技能】本场战斗第一次生命值降至0或以下时，满血复活，双方费用重置为1；本回合及下回合无敌。每场战斗仅触发一次。",
            CharacterIds.PangTong,
            TriggerTiming.OnDying,
            EffectPriority.Immediate,
            nameKey: "skill.pangtong_nirvana.name",
            descriptionKey: "skill.pangtong_nirvana.desc",
            assetCode: "SK20010");
    }

    /// <summary>
    /// 庞统专属主动卡牌技能【铁索连环】。
    /// </summary>
    public static Skill PangTongIronChainSkill()
    {
        return new Skill(
            SkillIds.PangTongIronChain,
            "铁索连环",
            SkillCategory.CharacterExclusive,
            SkillRarity.Common,
            new[] { SkillKind.Active, SkillKind.Card },
            "【主动技能】【卡牌技能】将【铁索连环】加入出牌栏。使用后获得1层护盾；下回合起所有存活单位连锁，首次伤害会同步作用于其余所有单位。",
            CharacterIds.PangTong,
            TriggerTiming.OnBattlePhase,
            EffectPriority.High,
            nameKey: "skill.pangtong_iron_chain.name",
            descriptionKey: "skill.pangtong_iron_chain.desc",
            assetCode: "SK20011");
    }

    /// <summary>
    /// Core System 的公开入口：TianJiSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill TianJiSkill()
    {
        return new Skill(
            SkillIds.TianJi,
            "天机",
            SkillCategory.CharacterExclusive,
            SkillRarity.Epic,
            new[] { SkillKind.Passive },
            "【观星】升级强化。当你使用观星时，立即获得1费（观星本身仍正常扣费）。",
            CharacterIds.ZhuGeLiang,
            source: SkillSource.Event,
            nameKey: "skill.tianji.name",
            descriptionKey: "skill.tianji.desc",
            assetCode: "SK70001");
    }

    /// <summary>
    /// Core System 的公开入口：DongZhuoBengHuaiSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill DongZhuoBengHuaiSkill()
    {
        return new Skill(
            SkillIds.DongZhuoBengHuai,
            "崩坏",
            SkillCategory.CharacterExclusive,
            SkillRarity.Common,
            new[] { SkillKind.Passive },
            "从第2回合开始，每回合开始时：生命值高于最大生命值1/4则失去20点生命；低于或等于1/4则恢复最大生命值1/16（向下取整）。",
            CharacterIds.DongZhuo,
            TriggerTiming.OnTurnStart,
            EffectPriority.Low,
            nameKey: "skill.dongzhuo_benghuai.name",
            descriptionKey: "skill.dongzhuo_benghuai.desc",
            assetCode: "SK50008");
    }

    /// <summary>
    /// Core System 的公开入口：ZiBaoSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill ZiBaoSkill()
    {
        return new Skill(
            SkillIds.ZiBao,
            "自爆",
            SkillCategory.CharacterExclusive,
            SkillRarity.Common,
            new[] { SkillKind.Passive },
            "【被动技能】首次生命值降至0及以下时，生命值回复至1。下一回合对敌人造成自身最大生命值40%的伤害；仅可由无懈可击、桃盾或酒盾抵挡。每场战斗仅触发一次。",
            null,
            TriggerTiming.OnDying,
            EffectPriority.High,
            nameKey: "skill.zibao.name",
            descriptionKey: "skill.zibao.desc",
            assetCode: "SK10007");
    }

    /// <summary>
    /// Core System 的公开入口：LeiJiSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill LeiJiSkill()
    {
        return new Skill(
            SkillIds.LeiJi,
            "雷击",
            SkillCategory.CharacterExclusive,
            SkillRarity.Epic,
            new[] { SkillKind.Passive },
            "当敌人的攻击牌未能成功对你造成伤害时（被闪、护盾、无懈可击等抵消），立即对攻击者造成10点Thunder（雷属性）伤害。",
            CharacterIds.ZhangJiao,
            TriggerTiming.OnDamageTaken,
            EffectPriority.Low,
            nameKey: "skill.leiji.name",
            descriptionKey: "skill.leiji.desc",
            assetCode: "SK50009");
    }

    /// <summary>
    /// Core System 的公开入口：HuangTianSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill HuangTianSkill()
    {
        return new Skill(
            SkillIds.HuangTian,
            "黄天",
            SkillCategory.CharacterExclusive,
            SkillRarity.Epic,
            new[] { SkillKind.Passive },
            "当敌人受到Thunder（雷属性）伤害时，在该敌人身上生成一道【闪电】。每回合开始有10%概率落下，造成20点Thunder伤害；未触发时，【闪电】会在张角与随机敌人之间转移。该伤害享受攻击性锦囊伤害加成。",
            CharacterIds.ZhangJiao,
            TriggerTiming.OnDamageTaken,
            EffectPriority.Low,
            nameKey: "skill.huangtian.name",
            descriptionKey: "skill.huangtian.desc",
            assetCode: "SK50010");
    }

    /// <summary>
    /// Core System 的公开入口：QianXinSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill QianXinSkill()
    {
        return new Skill(
            SkillIds.QianXin,
            "虔信",
            SkillCategory.General,
            SkillRarity.Common,
            new[] { SkillKind.Passive },
            "【被动技能】当持有者受到Thunder（雷属性）伤害时，立即获得2层【亢奋】Buff并回复5点生命值。每层【亢奋】使攻击牌最终伤害×1.5（叠乘），每回合结束减少1层。无触发次数限制，每次Thunder伤害事件独立触发。",
            null,
            TriggerTiming.OnDamageTaken,
            EffectPriority.Low,
            nameKey: "skill.qianxin_devotion.name",
            descriptionKey: "skill.qianxin_devotion.desc",
            assetCode: "SK10004");
    }

    /// <summary>
    /// Core System 的公开入口：ChangYinSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill ChangYinSkill()
    {
        return new Skill(
            SkillIds.ChangYin,
            "畅饮",
            SkillCategory.General,
            SkillRarity.Common,
            new[] { SkillKind.Passive },
            "【被动技能】当持有者使用【酒】时，场上所有友方角色立即额外获得1层【酒】Buff（仅增伤效果，不含酒护盾）。包含持有者自身及所有友军。",
            null,
            TriggerTiming.OnBattlePhase,
            EffectPriority.Low,
            nameKey: "skill.chang_yin_jolly_drinking.name",
            descriptionKey: "skill.chang_yin_jolly_drinking.desc",
            assetCode: "SK10005",
            canAppearInRandomChoicePool: false);
    }

    /// <summary>
    /// Core System 的公开入口：SiDiSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill SiDiSkill()
    {
        return new Skill(
            SkillIds.SiDi,
            "司敌",
            SkillCategory.CharacterExclusive,
            SkillRarity.Epic,
            new[] { SkillKind.Passive },
            "【被动技能】仅比较你当前锁定的敌人；多敌人时其它敌人的牌不参与判定。当你与锁定敌人打出相同类别的牌时：【费】→你额外获得1费；双方均为攻击牌→锁定敌人的攻击无效；【桃】→你获得双倍治疗，对方桃失效；【酒】→你获得双倍酒增伤，对方酒失效；【闪】/【无懈可击】→你额外获得1费；【顺手牵羊】→对方顺手牵羊失效，你的仍然生效。",
            CharacterIds.CaoZhen,
            TriggerTiming.OnBattlePhase,
            EffectPriority.Highest,
            nameKey: "skill.si_di_enemy_read.name",
            descriptionKey: "skill.si_di_enemy_read.desc",
            assetCode: "SK30003");
    }

    /// <summary>
    /// 创建张辽的角色专属主动卡牌技能【突袭】。
    ///
    /// 技能定义只声明归属与展示信息；实际结算复用南蛮入侵和顺手牵羊的统一战斗入口。
    /// </summary>
    public static Skill TuxiSkill()
    {
        return new Skill(
            SkillIds.Tuxi,
            "突袭",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Active, SkillKind.Card },
            "【主动技能】【卡牌技能】开局获得2费锦囊牌【突袭】。按【南蛮入侵】规则结算；资源牌结算后，每个实际受到伤害的目标分别受到一次【顺手牵羊】。若此时目标没有费用且未偷到费用，使用者获得1费。",
            CharacterIds.ZhangLiao,
            TriggerTiming.OnBattlePhase,
            EffectPriority.Mid,
            nameKey: "skill.tuxi.name",
            descriptionKey: "skill.tuxi.desc",
            assetCode: "SK30004");
    }

    /// <summary>
    /// Core System 的公开入口：YingZiSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill YingZiSkill()
    {
        return new Skill(
            SkillIds.YingZi,
            "英姿",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Card },
            "将出牌栏中的【火杀】替换为专属锦囊牌【火攻】。",
            CharacterIds.ZhouYu,
            TriggerTiming.OnGameStart,
            EffectPriority.Lowest,
            nameKey: "skill.yingzi.name",
            descriptionKey: "skill.yingzi.desc",
            assetCode: "SK40009");
    }

    /// <summary>
    /// Core System 的公开入口：YeYanSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill YeYanSkill()
    {
        return new Skill(
            SkillIds.YeYan,
            "业炎",
            SkillCategory.CharacterExclusive,
            SkillRarity.Common,
            new[] { SkillKind.Passive },
            "当你成功对敌人造成火属性伤害时，使其获得2层【虚弱】。",
            CharacterIds.ZhouYu,
            TriggerTiming.OnDamageTaken,
            EffectPriority.Lowest,
            nameKey: "skill.yeyan.name",
            descriptionKey: "skill.yeyan.desc",
            assetCode: "SK40010");
    }

    /// <summary>
    /// Core System 的公开入口：MeihuoSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill MeihuoSkill()
    {
        return new Skill(
            SkillIds.Meihuo,
            "魅惑",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Active, SkillKind.Card },
            "【主动技能】【卡牌技能】向出招栏添加【魅惑】（1费，指向性锦囊）。选择一名敌人，自身获得一层魅惑护盾，抵挡下一次有效伤害；无论护盾是否被击破，目标下一回合都只能使用【费】。不可叠加。每场战斗最多使用3次。",
            CharacterIds.Diaochan,
            TriggerTiming.OnBattlePhase,
            EffectPriority.Low,
            nameKey: "skill.meihuo_charm.name",
            descriptionKey: "skill.meihuo_charm.desc",
            assetCode: "SK50011");
    }

    /// <summary>
    /// Core System 的公开入口：PlagueSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill PlagueSkill()
    {
        return new Skill(
            SkillIds.Plague,
            "瘟疫",
            SkillCategory.General,
            SkillRarity.Common,
            new[] { SkillKind.Passive },
            "【被动技能】每回合开始时，给所有敌方追加1层【瘟疫】Debuff。累积至10层时立即发作：造成10点真实毒素（Poison）伤害，清除本次触发的10层。受治疗时清除5层瘟疫叠加。",
            null,
            TriggerTiming.OnTurnStart,
            EffectPriority.Low,
            nameKey: "skill.plague_pestilence.name",
            descriptionKey: "skill.plague_pestilence.desc",
            assetCode: "SK10006");
    }

    /// <summary>
    /// Core System 的公开入口：JiAngSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill JiAngSkill()
    {
        return new Skill(
            SkillIds.JiAng,
            "激昂",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Passive },
            "当你的杀类型牌成功命中敌人时，立即对目标额外造成自身最大生命值20%的普通杀伤害，随后回复5点生命值。若杀类型牌未成功造成伤害，则立即失去当前生命值33%，随后获得1点费用。",
            CharacterIds.SunCe,
            TriggerTiming.OnDamageTaken,
            EffectPriority.Low,
            nameKey: "skill.jiang.name",
            descriptionKey: "skill.jiang.desc",
            assetCode: "SK40006");
    }

    /// <summary>
    /// Core System 的公开入口：HunZiSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill HunZiSkill()
    {
        return new Skill(
            SkillIds.HunZi,
            "魂姿",
            SkillCategory.CharacterExclusive,
            SkillRarity.Legendary,
            new[] { SkillKind.Passive },
            "受到伤害并实际减少生命值后，若当前生命值低于当前最大生命值的50%，且当前最大生命值大于20，则扣除当前最大生命值20%并额外扣除10点上限，随后回复至新的最大生命值。本效果可反复触发；当前最大生命值降至20或以下后不再触发。战斗结束后最大生命值恢复为战斗开始时的数值。",
            CharacterIds.SunCe,
            TriggerTiming.OnDamageTaken,
            EffectPriority.Low,
            nameKey: "skill.hunzi.name",
            descriptionKey: "skill.hunzi.desc",
            assetCode: "SK40007");
    }

    /// <summary>
    /// 孙尚香专属传奇被动技能：武库。
    ///
    /// 技能本身只是数据记录（Id/描述/本地化 Key），不包含任何战斗逻辑。真正的效果由
    /// <c>Scripts/SkillEffects/WuKuEffect.cs</c> 中的多个 <see cref="IBattleEffect"/>
    /// 实现，并在 <c>Scripts/BattleRules.cs</c> 的 <c>CreateDefaultManager</c> 中注册——
    /// 与项目里其它角色专属被动技能（例如黄月英的如影随行）完全一致的架构，
    /// 没有引入第二套技能系统。
    ///
    /// Timing/Priority 这里填写的是"最具代表性"的一个时机（开局统计装备），
    /// 实际上武库同时挂了 OnGameStart（最大生命值/载具初始费用）、
    /// OnDamage（攻击装备加伤）、OnBattlePhase（饰品费用加成）三种时机的效果。
    /// </summary>
    public static Skill WuKuSkill()
    {
        return new Skill(
            SkillIds.WuKu,
            "武库",
            SkillCategory.CharacterExclusive,
            SkillRarity.Legendary,
            new[] { SkillKind.Passive },
            "统计背包（无论是否已装备）中的全部装备：①防御型装备每件按品质提供+4/+8/+12/+16最大生命值；②攻击型装备每件按品质提供+2/+4/+6/+8点【杀】系及伤害锦囊的固定加伤；③每件饰品令【费】额外获得+0.2费；④每件载具令开局初始费用+0.5。以上效果均实时统计背包内容，不要求装备处于已装备状态。",
            CharacterIds.SunShangXiang,
            TriggerTiming.OnGameStart,
            EffectPriority.Mid,
            nameKey: "skill.wuku.name",
            descriptionKey: "skill.wuku.desc",
            assetCode: "SK40008");
    }

    /// <summary>
    /// Core System 的公开入口：IllusionSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill IllusionSkill()
    {
        return new Skill(
            SkillIds.Illusion,
            "幻象",
            SkillCategory.CharacterExclusive,
            SkillRarity.Legendary,
            new[] { SkillKind.Passive },
            "被攻击牌成功命中时，记录该攻击牌类型，之后对相同CardType攻击完全免疫。被不同CardType攻击成功命中后更新记录。仅在实际造成伤害时更新；装备、技能与环境伤害不参与记录或免疫。",
            null,
            SkillSource.Boss,
            TriggerTiming.OnBeforeDamage,
            EffectPriority.Highest,
            nameKey: "skill.illusion.name",
            descriptionKey: "skill.illusion.desc",
            assetCode: "SK60011");
    }

    /// <summary>
    /// Core System 的公开入口：SlimeSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill SlimeSkill()
    {
        return new Skill(
            SkillIds.Slime,
            "粘液",
            SkillCategory.CharacterExclusive,
            SkillRarity.Rare,
            new[] { SkillKind.Passive },
            "成功抵挡敌方杀系攻击后，给攻击方施加【粘液虚弱】：其下一张攻击牌伤害×0.5，一次性消耗，不可叠加。",
            null,
            SkillSource.Boss,
            TriggerTiming.OnDamageTaken,
            EffectPriority.Lowest,
            nameKey: "skill.slime.name",
            descriptionKey: "skill.slime.desc",
            assetCode: "SK60012");
    }

    // ── 第一章 Boss 专属技能 ─────────────────────────────────────

    /// <summary>
    /// Core System 的公开入口：BossHealerPassiveSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill BossHealerPassiveSkill()
    {
        return new Skill(
            SkillIds.BossHealerPassive,
            "神秘补剂",
            SkillCategory.CharacterExclusive,
            SkillRarity.Legendary,
            new[] { SkillKind.Passive },
            "【被动技能】当生命值（含临时生命值）达到120点时，进入强化状态：杀系攻击变为必中杀（不可闪），且攻击伤害+5。每场战斗仅触发一次。",
            null,
            SkillSource.Boss,
            TriggerTiming.OnHeal,
            EffectPriority.Mid,
            nameKey: "skill.boss_healer_passive.name",
            descriptionKey: "skill.boss_healer_passive.desc");
    }

    /// <summary>
    /// Core System 的公开入口：BossTraitorComboSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill BossTraitorComboSkill()
    {
        return new Skill(
            SkillIds.BossTraitorCombo,
            "连击",
            SkillCategory.CharacterExclusive,
            SkillRarity.Legendary,
            new[] { SkillKind.Passive },
            "【被动技能】连续2回合使用杀系牌 → 进入连击状态：攻击伤害+5，偏向火/雷杀。连续2回合不使用攻击牌 → 退出连击。",
            null,
            SkillSource.Boss,
            TriggerTiming.OnTurnEnd,
            EffectPriority.Mid,
            nameKey: "skill.boss_traitor_combo.name",
            descriptionKey: "skill.boss_traitor_combo.desc");
    }

    /// <summary>
    /// Core System 的公开入口：BuDaoSkill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Skill BuDaoSkill()
    {
        return new Skill(
            SkillIds.BuDao,
            "不屈",
            SkillCategory.CharacterExclusive,
            SkillRarity.Legendary,
            new[] { SkillKind.Passive },
            "【被动技能】第一次生命值降至0时，不立即死亡：恢复至1点生命，获得2费，下回合免疫所有伤害。每场战斗仅触发一次。",
            null,
            SkillSource.Boss,
            TriggerTiming.OnDying,
            EffectPriority.High,
            nameKey: "skill.budao.name",
            descriptionKey: "skill.budao.desc");
    }

}

/// <summary>
/// Core System 的公开类：SkillText。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class SkillText
{
    /// <summary>
    /// Core System 的公开入口：GetCategoryName。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetCategoryName(SkillCategory category)
    {
        return category switch
        {
            SkillCategory.General => "通用技能",
            SkillCategory.CharacterExclusive => "角色专属",
            _ => string.Empty
        };
    }

    /// <summary>
    /// Core System 的公开入口：GetRarityName。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetRarityName(SkillRarity rarity)
        => Localization.GetRarityName(rarity);

    /// <summary>
    /// Core System 的公开入口：GetKindName。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetKindName(SkillKind kind)
    {
        return kind switch
        {
            SkillKind.Passive => Localization.Get("skill.kind.passive_full"),
            SkillKind.Active => Localization.Get("skill.kind.active_full"),
            SkillKind.Card => Localization.Get("skill.kind.card_full"),
            SkillKind.Enchantment => Localization.Get("skill.kind.enchantment_full"),
            _ => string.Empty
        };
    }
}
