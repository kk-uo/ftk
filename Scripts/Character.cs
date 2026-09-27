//////////////////////////////////////////////////////////
// 文件：Scripts/Character.cs
//
// 模块：Character System
//
// 职责：
// 1. 承载角色定义、角色选择与角色展示相关代码。
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
using System.Linq;

/// <summary>
/// Character System 的公开枚举：Gender。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum Gender
{
    Male,
    Female
}

/// <summary>
/// Character System 的公开枚举：Faction。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum Faction
{
    Wei,
    Shu,
    Wu,
    Qun
}

/// <summary>
/// Character System 的公开类：CharacterData。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public class CharacterData : ILocalizedDefinition
{
    public string Id { get; set; } = string.Empty;
    private string _name = string.Empty;
    public string Name
    {
        get => Localization.GetOrFallback(NameKey, _name);
        set => _name = value;
    }
    public string NameKey { get; set; } = string.Empty;
    public string DescriptionKey => string.Empty;
    public Gender Gender;
    public Faction Faction;
    public int MaxHp;
    public List<string> SkillIds = new();
    public List<string> StartingEquipmentIds = new();
}

/// <summary>
/// Character System 的公开类：SkillInstance。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public class SkillInstance
{
    /// <summary>
    /// Character System 的公开入口：SkillInstance。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public SkillInstance(Skill data)
    {
        Data = data;
    }

    public Skill Data { get; }
}

/// <summary>
/// Character System 的公开类：CharacterInstance。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public class CharacterInstance
{
    /// <summary>
    /// Character System 的公开入口：CharacterInstance。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public CharacterInstance(CharacterData data)
    {
        Data = data;
        CurrentHp = data.MaxHp;
        Skills = new List<SkillInstance>();
    }

    public CharacterData Data { get; }
    public int CurrentHp { get; set; }
    public List<SkillInstance> Skills { get; }
}

/// <summary>
/// Character System 的公开类：CharacterDatabase。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class CharacterDatabase
{
    // 顺序按 Hero Unlock System 的解锁难度从易到难排列（见 Scripts/HeroUnlock/HeroUnlockDatabase.cs）：
    // ① 没有注册解锁条件、一直可选的角色排最前；
    // ② 第一章 Boss（背叛者/医者/失疯卖艺人）击败1次解锁的角色；
    // ③ 同样是第一章背叛者、但需要累计击败3次的貂蝉，比①③里"只需1次"的更难，排在后面；
    // ④ 第二章精英（独眼巨人）解锁的夏侯惇；
    // ⑤ 第二章 Boss（观星集智体/黄衣之主）解锁的角色；
    // ⑥ 需要账号历史累计的成就类条件（孙尚香：累计100件装备），跨局长期养成，难度更高；
    // ⑦ 第三章终局 Boss（3-8 关的两条路线：暴虐昏君 / 蜀汉共生体）解锁的角色，难度最高。
    // 这个顺序只影响列表展示，不影响任何解锁判定逻辑（判定始终只看 HeroUnlockProgress）。
    private static readonly IReadOnlyList<CharacterData> Characters = new[]
    {
        // ① 无解锁条件，一直可选。
        // 周泰为战斗续航角色；庞统使用【涅槃】与【铁索连环】构成高风险的全场伤害玩法。
        new CharacterData
        {
            Id = CharacterIds.ZhouTai,
            Name = "周泰",
            NameKey = "character.zhoutai.name",
            Gender = Gender.Male,
            Faction = Faction.Wu,
            MaxHp = 40,
            SkillIds = new List<string> { SkillIds.ZhouTaiBuQu, SkillIds.ZhouTaiFenJi }
        },
        new CharacterData
        {
            Id = CharacterIds.PangTong,
            Name = "庞统",
            NameKey = "character.pangtong.name",
            Gender = Gender.Male,
            Faction = Faction.Shu,
            MaxHp = 30,
            SkillIds = new List<string> { SkillIds.PangTongNirvana, SkillIds.PangTongIronChain }
        },
        new CharacterData
        {
            Id = CharacterIds.Zhaoyun,
            Name = "赵云",
            NameKey = "character.zhaoyun.name",
            Gender = Gender.Male,
            Faction = Faction.Shu,
            MaxHp = 40,
            SkillIds = new List<string> { SkillIds.Longdan }
        },
        new CharacterData
        {
            Id = CharacterIds.Machao,
            Name = "马超",
            NameKey = "character.machao.name",
            Gender = Gender.Male,
            Faction = Faction.Shu,
            MaxHp = 40,
            SkillIds = new List<string> { SkillIds.Bizhong }
        },
        new CharacterData
        {
            Id = CharacterIds.Luxun,
            Name = "陆逊",
            NameKey = "character.luxun.name",
            Gender = Gender.Male,
            Faction = Faction.Wu,
            MaxHp = 30,
            SkillIds = new List<string> { SkillIds.Lianying, SkillIds.Qianxun }
        },
        new CharacterData
        {
            Id = CharacterIds.ZhouYu,
            Name = "周瑜",
            NameKey = "character.zhouyu.name",
            Gender = Gender.Male,
            Faction = Faction.Wu,
            MaxHp = 30,
            SkillIds = new List<string> { SkillIds.YingZi, SkillIds.YeYan }
        },
        new CharacterData
        {
            Id = CharacterIds.Lvmeng,
            Name = "吕蒙",
            NameKey = "character.lvmeng.name",
            Gender = Gender.Male,
            Faction = Faction.Wu,
            MaxHp = 40,
            SkillIds = new List<string> { SkillIds.Keji }
        },
        new CharacterData
        {
            Id = CharacterIds.MengHuo,
            Name = "孟获",
            NameKey = "character.menghuo.name",
            Gender = Gender.Male,
            Faction = Faction.Shu,
            MaxHp = 40,
            SkillIds = new List<string> { SkillIds.Manzu }
        },
        new CharacterData
        {
            Id = CharacterIds.ZhenJi,
            Name = "甄姬",
            NameKey = "character.zhenji.name",
            Gender = Gender.Female,
            Faction = Faction.Wei,
            MaxHp = 30,
            SkillIds = new List<string> { SkillIds.Luoshen }
        },
        new CharacterData
        {
            Id = CharacterIds.XuSheng,
            Name = "徐盛",
            NameKey = "character.xusheng.name",
            Gender = Gender.Male,
            Faction = Faction.Wu,
            MaxHp = 40,
            SkillIds = new List<string> { SkillIds.PoJun, SkillIds.YiCheng }
        },
        new CharacterData
        {
            Id = CharacterIds.HuangYueYing,
            Name = "黄月英",
            NameKey = "character.huangyueying.name",
            Gender = Gender.Female,
            Faction = Faction.Shu,
            MaxHp = 20,
            SkillIds = new List<string> { SkillIds.YingXi, SkillIds.RuYingSuiXing }
        },
        new CharacterData
        {
            Id = CharacterIds.CaoZhen,
            Name = "曹真",
            NameKey = "character.caozhen.name",
            Gender = Gender.Male,
            Faction = Faction.Wei,
            MaxHp = 40,
            SkillIds = new List<string> { SkillIds.SiDi }
        },
        new CharacterData
        {
            Id = CharacterIds.ZhangLiao,
            Name = "张辽",
            NameKey = "character.zhangliao.name",
            Gender = Gender.Male,
            Faction = Faction.Wei,
            MaxHp = 40,
            SkillIds = new List<string> { SkillIds.Tuxi }
        },
        new CharacterData
        {
            Id = CharacterIds.SunCe,
            Name = "孙策",
            NameKey = "character.sunce.name",
            Gender = Gender.Male,
            Faction = Faction.Wu,
            MaxHp = 40,
            SkillIds = new List<string> { SkillIds.JiAng, SkillIds.HunZi }
        },

        // ② 第一章 Boss，击败1次解锁。
        new CharacterData
        {
            Id = CharacterIds.Lvbu,
            Name = "吕布",
            NameKey = "character.lvbu.name",
            Gender = Gender.Male,
            Faction = Faction.Qun,
            MaxHp = 40,
            SkillIds = new List<string> { SkillIds.Wushuang }
        },
        new CharacterData
        {
            Id = CharacterIds.Huatuo,
            Name = "华佗",
            NameKey = "character.huatuo.name",
            Gender = Gender.Male,
            Faction = Faction.Qun,
            MaxHp = 30,
            SkillIds = new List<string> { SkillIds.Qingnang }
        },
        new CharacterData
        {
            Id = CharacterIds.MiHeng,
            Name = "祢衡",
            NameKey = "character.miheng.name",
            Gender = Gender.Male,
            Faction = Faction.Wei,
            MaxHp = 40,
            SkillIds = new List<string> { SkillIds.Luoyi, SkillIds.JiGu }
        },

        // ③ 第一章同一个 Boss（背叛者），但要求累计击败3次，比只需1次更难。
        new CharacterData
        {
            Id = CharacterIds.Diaochan,
            Name = "貂蝉",
            NameKey = "character.diaochan.name",
            Gender = Gender.Female,
            Faction = Faction.Qun,
            MaxHp = 30,
            SkillIds = new List<string> { SkillIds.Biyue, SkillIds.Meihuo }
        },

        // ④ 第二章精英（独眼巨人）解锁。
        new CharacterData
        {
            Id = CharacterIds.XiaHouDun,
            Name = "夏侯惇",
            NameKey = "character.xiaohoudun.name",
            Gender = Gender.Male,
            Faction = Faction.Wei,
            MaxHp = 40,
            SkillIds = new List<string> { SkillIds.XueZhaiXueChou }
        },

        // ⑤ 第二章 Boss，击败1次解锁。
        new CharacterData
        {
            Id = CharacterIds.ZhuGeLiang,
            Name = "诸葛亮",
            NameKey = "character.zhugeliang.name",
            Gender = Gender.Male,
            Faction = Faction.Shu,
            MaxHp = 30,
            SkillIds = new List<string> { SkillIds.Guanxing }
        },
        new CharacterData
        {
            Id = CharacterIds.ZhangJiao,
            Name = "张角",
            NameKey = "character.zhangjiao.name",
            Gender = Gender.Male,
            Faction = Faction.Qun,
            MaxHp = 40,
            SkillIds = new List<string> { SkillIds.LeiJi, SkillIds.HuangTian },
            StartingEquipmentIds = new List<string> { EquipmentIds.Conductor }
        },

        // ⑥ 账号历史累计成就类条件（孙尚香：传奇被动【武库】，数值加成实现见
        //   Scripts/SkillEffects/WuKuEffect.cs），跨局长期养成，难度高于单次 Boss 击杀。
        new CharacterData
        {
            Id = CharacterIds.SunShangXiang,
            Name = "孙尚香",
            NameKey = "character.sunshangxiang.name",
            Gender = Gender.Female,
            Faction = Faction.Wu,
            MaxHp = 30,
            SkillIds = new List<string> { SkillIds.WuKu }
        },

        // ⑦ 第三章终局 Boss（3-8 关的两条随机路线之一）解锁，难度最高。
        new CharacterData
        {
            Id = CharacterIds.DongZhuo,
            Name = "董卓",
            NameKey = "character.dongzhuo.name",
            Gender = Gender.Male,
            Faction = Faction.Qun,
            MaxHp = 80,
            SkillIds = new List<string> { SkillIds.JiuChi, SkillIds.RouLin, SkillIds.DongZhuoBengHuai }
        },
        // 刘备/关羽：随 Hero Unlock System v1 一并新增（与张飞共用【蜀汉共生体】解锁条件，
        // 见 Scripts/HeroUnlock/HeroUnlockDatabase.cs）。仁德/桃园结义/武圣/义绝原本只有
        // 蜀汉共生体 Boss 侧的实现（Scripts/Skills/ShuHanBossSkills.cs，硬编码 EnemyInstance/
        // 共享生命池），玩家持有时完全空转；玩家可用的版本见 Scripts/Skills/ShuHanPlayerSkills.cs。
        new CharacterData
        {
            Id = CharacterIds.LiuBei,
            Name = "刘备",
            NameKey = "character.liubei.name",
            Gender = Gender.Male,
            Faction = Faction.Shu,
            MaxHp = 40,
            SkillIds = new List<string> { SkillIds.Rende, SkillIds.TaoyuanJiyi }
        },
        new CharacterData
        {
            Id = CharacterIds.GuanYu,
            Name = "关羽",
            NameKey = "character.guanyu.name",
            Gender = Gender.Male,
            Faction = Faction.Shu,
            MaxHp = 40,
            SkillIds = new List<string> { SkillIds.Wusheng, SkillIds.Yijue }
        },
        new CharacterData
        {
            Id = CharacterIds.ZhangFei,
            Name = "张飞",
            NameKey = "character.zhangfei.name",
            Gender = Gender.Male,
            Faction = Faction.Shu,
            MaxHp = 40,
            SkillIds = new List<string> { SkillIds.PaoxiaoPlayer }
        }
    };

    /// <summary>
    /// Character System 的公开入口：GetCharacter。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static CharacterData GetCharacter(string id)
    {
        return Characters.First(character => character.Id == id);
    }

    /// <summary>
    /// Character System 的公开入口：GetAllCharacters。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static IReadOnlyList<CharacterData> GetAllCharacters()
    {
        return Characters;
    }
}
