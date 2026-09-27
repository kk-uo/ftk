//////////////////////////////////////////////////////////
// 文件：Scripts/RunBuffDefinition.cs
//
// 模块：Run Buff System
//
// 职责：
// 1. 承载跨战斗 Buff 定义、生命周期与结算相关代码。
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
/// Run Buff System 的公开类：RunBuffEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RunBuffEffect
{
    public RunBuffTriggerTiming Timing;
    public RunBuffEffectType Type;
    public double Value;
}

/// <summary>
/// Run Buff System 的公开类：RunBuffDefinition。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class RunBuffDefinition : ILocalizedDefinition
{
    public string BuffId = string.Empty;
    public string Id => BuffId;
    public string BuffName = string.Empty;
    public string NameKey { get; set; } = string.Empty;
    public string Description = string.Empty;
    public string DescriptionKey { get; set; } = string.Empty;
    public string DisplayName => Localization.GetOrFallback(NameKey, BuffName);
    public string DisplayDescription => Localization.GetOrFallback(DescriptionKey, Description);
    public int DefaultRemainingBattles = 1;
    public bool Stackable;
    public string Icon = string.Empty;
    public RunBuffType Type;
    public List<RunBuffEffect> Effects = new();
    // 章节开始时自动变形：目标章节编号（0 = 不变形）。
    public int TransformAtChapterStart = 0;
    // 变形后替换为的 RunBuff Id。
    public string TransformIntoBuff = string.Empty;
    // 变形发生时对玩家 MaxHP 的一次性增量（负数为减少）。
    public int TransformMaxHpDelta = 0;
}

/// <summary>
/// Run Buff System 的公开类：RunBuffIds。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class RunBuffIds
{
    public const string Darkness = "run_buff_darkness";
    public const string BloodMoonDarkness = "run_buff_blood_moon_darkness";
    public const string Curse = "run_buff_curse";
    public const string MoonAttention = "run_buff_moon_attention";
    public const string ShaQiChenShen = "run_buff_shaqichenshen";
    public const string QiXingTanBattle = "run_buff_qixingtan_battle";
    public const string AncestorBlessing = "run_buff_ancestor_blessing";
    public const string LostMind = "run_buff_lost_mind";
    public const string ExpiredEnhancer = "run_buff_expired_enhancer";
    public const string Withdrawal = "run_buff_withdrawal";
    public const string ExperimentData = "run_buff_experiment_data";
    public const string ElfFireDamage = "run_buff_elf_fire_damage";
    public const string ElfThunderDamage = "run_buff_elf_thunder_damage";
    public const string ElfIceDamage = "run_buff_elf_ice_damage";
    public const string ElfPoisonDamage = "run_buff_elf_poison_damage";
    public const string Wet = "run_buff_wet";
}

/// <summary>
/// Run Buff System 的公开类：RunBuffDatabase。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class RunBuffDatabase
{
    private static readonly Dictionary<string, RunBuffDefinition> Definitions = new()
    {
        [RunBuffIds.Darkness] = new RunBuffDefinition
        {
            BuffId = RunBuffIds.Darkness,
            BuffName = "黑暗",
            NameKey = "runbuff.darkness.name",
            Description = "所有敌人最大生命值+30%。",
            DescriptionKey = "runbuff.darkness.desc",
            DefaultRemainingBattles = 1,
            Stackable = false,
            Icon = string.Empty,
            Type = RunBuffType.Curse,
            Effects = new List<RunBuffEffect>
            {
                new()
                {
                    Timing = RunBuffTriggerTiming.OnBattleStart,
                    Type = RunBuffEffectType.EnemyMaxHpPercentBonus,
                    Value = 0.30
                }
            }
        },
        [RunBuffIds.Curse] = new RunBuffDefinition
        {
            BuffId = RunBuffIds.Curse,
            BuffName = "诅咒",
            NameKey = "runbuff.curse.name",
            Description = "每层使你受到的伤害倍率+0.1。达到44层时额外获得【煞气缠身】。每次战斗结束后，层数-1。",
            DescriptionKey = "runbuff.curse.desc",
            DefaultRemainingBattles = -1,
            Stackable = true,
            Icon = string.Empty,
            Type = RunBuffType.Curse,
            Effects = new List<RunBuffEffect>
            {
                new()
                {
                    Timing = RunBuffTriggerTiming.OnDamageCalculate,
                    Type = RunBuffEffectType.PlayerDamageTakenMultiplierBonus,
                    Value = 0.1
                }
            }
        },
        [RunBuffIds.BloodMoonDarkness] = new RunBuffDefinition
        {
            BuffId = RunBuffIds.BloodMoonDarkness,
            BuffName = "黑暗",
            NameKey = "runbuff.bloodmoondarkness.name",
            Description = "血红之月：整个章节内，所有敌人最大生命值+30%。",
            DescriptionKey = "runbuff.bloodmoondarkness.desc",
            DefaultRemainingBattles = -1,
            Stackable = false,
            Icon = string.Empty,
            Type = RunBuffType.ChapterVariant,
            Effects = new List<RunBuffEffect>
            {
                new()
                {
                    Timing = RunBuffTriggerTiming.OnBattleStart,
                    Type = RunBuffEffectType.EnemyMaxHpPercentBonus,
                    Value = 0.30
                }
            }
        },
        [RunBuffIds.Wet] = new RunBuffDefinition
        {
            BuffId = RunBuffIds.Wet, BuffName = "潮湿", NameKey = "runbuff.wet.name",
            Description = "受到的元素伤害×1.2。防水模块可免疫。", DescriptionKey = "runbuff.wet.desc",
            DefaultRemainingBattles = -1, Stackable = false, Type = RunBuffType.ChapterVariant
        },
        [RunBuffIds.MoonAttention] = new RunBuffDefinition
        {
            BuffId = RunBuffIds.MoonAttention,
            BuffName = "月亮的注意",
            NameKey = "runbuff.moonattention.name",
            Description = "你已引起【月亮】的注意。它将在未来的事件中回应你。",
            DescriptionKey = "runbuff.moonattention.desc",
            DefaultRemainingBattles = -1,
            Stackable = false,
            Icon = string.Empty,
            Type = RunBuffType.Event,
            Effects = new List<RunBuffEffect>()
        },

        // 煞气缠身：永久 Buff，生命值强制为 1、无法恢复，玩家造成伤害×1.5。
        [RunBuffIds.ShaQiChenShen] = new RunBuffDefinition
        {
            BuffId = RunBuffIds.ShaQiChenShen,
            BuffName = "煞气缠身",
            NameKey = "runbuff.shaqichenshen.name",
            Description = "生命值变为1；你一碰即死。你造成的所有伤害×1.5。",
            DescriptionKey = "runbuff.shaqichenshen.desc",
            DefaultRemainingBattles = -1,
            Stackable = false,
            Icon = string.Empty,
            Type = RunBuffType.Curse,
            Effects = new List<RunBuffEffect>
            {
                new()
                {
                    Timing = RunBuffTriggerTiming.OnDamageCalculate,
                    Type = RunBuffEffectType.BlockPlayerHealing,
                    Value = 1
                },
                new()
                {
                    Timing = RunBuffTriggerTiming.OnDamageCalculate,
                    Type = RunBuffEffectType.PlayerDamageDealtMultiplierBonus,
                    Value = 0.5
                }
            }
        },

        // 招魂强化：七星坛·作法招魂内部buff，1场战斗，敌人HP×3，伤害×2。
        [RunBuffIds.QiXingTanBattle] = new RunBuffDefinition
        {
            BuffId = RunBuffIds.QiXingTanBattle,
            BuffName = "招魂强化",
            NameKey = "runbuff.qixingtanbattle.name",
            Description = "（内部）敌人最大生命+200%，造成伤害×2。",
            DescriptionKey = "runbuff.qixingtanbattle.desc",
            DefaultRemainingBattles = 1,
            Stackable = false,
            Icon = string.Empty,
            Type = RunBuffType.BossEffect,
            Effects = new List<RunBuffEffect>
            {
                new()
                {
                    Timing = RunBuffTriggerTiming.OnBattleStart,
                    Type = RunBuffEffectType.EnemyMaxHpPercentBonus,
                    Value = 2.0
                },
                new()
                {
                    Timing = RunBuffTriggerTiming.OnDamageCalculate,
                    Type = RunBuffEffectType.EnemyDamageMultiplierBonus,
                    Value = 1.0
                }
            }
        },

        [RunBuffIds.AncestorBlessing] = new RunBuffDefinition
        {
            BuffId = RunBuffIds.AncestorBlessing,
            BuffName = "先祖的赐福",
            NameKey = "runbuff.ancestorblessing.name",
            Description = "本局前3次死亡时，立即恢复全部生命值，获得攻击芯片×1，并消耗1层。剩余层数即剩余复活次数。",
            DescriptionKey = "runbuff.ancestorblessing.desc",
            DefaultRemainingBattles = -1,
            Stackable = true,
            Icon = string.Empty,
            Type = RunBuffType.Event,
            Effects = new List<RunBuffEffect>()
        },

        [RunBuffIds.LostMind] = new RunBuffDefinition
        {
            BuffId = RunBuffIds.LostMind,
            BuffName = "失智",
            NameKey = "runbuff.lostmind.name",
            Description = "精神被撕裂后留下的异常状态。当前仅作为局内永久Buff显示。",
            DescriptionKey = "runbuff.lostmind.desc",
            DefaultRemainingBattles = -1,
            Stackable = false,
            Icon = string.Empty,
            Type = RunBuffType.Curse,
            Effects = new List<RunBuffEffect>()
        },

        // 过期强化剂：第一、二章有效，最大生命+30已在获得时结算；每场战斗开始时+2费。
        // 进入第三章时移除这30点最大生命，完成“仅第一、二章”的时限。
        [RunBuffIds.ExpiredEnhancer] = new RunBuffDefinition
        {
            BuffId = RunBuffIds.ExpiredEnhancer,
            BuffName = "过期强化剂",
            NameKey = "runbuff.expiredenhancer.name",
            Description = "第一、二章：最大生命+30，每场战斗开始时获得2点费用。进入第三章后效果结束。",
            DescriptionKey = "runbuff.expiredenhancer.desc",
            DefaultRemainingBattles = -1,
            Stackable = false,
            Icon = string.Empty,
            Type = RunBuffType.Blessing,
            Effects = new List<RunBuffEffect>
            {
                new()
                {
                    Timing = RunBuffTriggerTiming.OnBattleStart,
                    Type = RunBuffEffectType.PlayerStartBattleManaBonus,
                    Value = 2
                }
            },
            TransformAtChapterStart = 3,
            TransformIntoBuff = RunBuffIds.Withdrawal,
            TransformMaxHpDelta = -30
        },

        // 戒断反应：过期强化剂在第三章开始时转化而来，作为负面标记持续存在。
        [RunBuffIds.Withdrawal] = new RunBuffDefinition
        {
            BuffId = RunBuffIds.Withdrawal,
            BuffName = "戒断反应",
            NameKey = "runbuff.withdrawal.name",
            Description = "过期强化剂的时效已结束，第一、二章获得的30点最大生命已移除。",
            DescriptionKey = "runbuff.withdrawal.desc",
            DefaultRemainingBattles = -1,
            Stackable = false,
            Icon = string.Empty,
            Type = RunBuffType.Curse,
            Effects = new List<RunBuffEffect>()
        },

        // 密室数据：从废弃实验室收集，作为局内永久标记，可解锁禁书库暗门。
        [RunBuffIds.ExperimentData] = new RunBuffDefinition
        {
            BuffId = RunBuffIds.ExperimentData,
            BuffName = "密室数据",
            NameKey = "runbuff.experimentdata.name",
            Description = "来自废弃实验室的密室数据。",
            DescriptionKey = "runbuff.experimentdata.desc",
            DefaultRemainingBattles = -1,
            Stackable = false,
            Icon = string.Empty,
            Type = RunBuffType.Event,
            Effects = new List<RunBuffEffect>()
        },

        [RunBuffIds.ElfFireDamage] = new RunBuffDefinition
        {
            BuffId = RunBuffIds.ElfFireDamage,
            BuffName = "火焰强化",
            NameKey = "runbuff.elffiredamage.name",
            Description = "火焰伤害+10。",
            DescriptionKey = "runbuff.elffiredamage.desc",
            DefaultRemainingBattles = -1,
            Stackable = true,
            Icon = string.Empty,
            Type = RunBuffType.Blessing,
            Effects = new List<RunBuffEffect>
            {
                new()
                {
                    Timing = RunBuffTriggerTiming.OnDamageCalculate,
                    Type = RunBuffEffectType.PlayerFireDamageFlatBonus,
                    Value = 10
                }
            }
        },

        [RunBuffIds.ElfThunderDamage] = new RunBuffDefinition
        {
            BuffId = RunBuffIds.ElfThunderDamage,
            BuffName = "雷霆强化",
            NameKey = "runbuff.elfthunderdamage.name",
            Description = "雷属性伤害+10。",
            DescriptionKey = "runbuff.elfthunderdamage.desc",
            DefaultRemainingBattles = -1,
            Stackable = true,
            Icon = string.Empty,
            Type = RunBuffType.Blessing,
            Effects = new List<RunBuffEffect>
            {
                new()
                {
                    Timing = RunBuffTriggerTiming.OnDamageCalculate,
                    Type = RunBuffEffectType.PlayerThunderDamageFlatBonus,
                    Value = 10
                }
            }
        },

        [RunBuffIds.ElfIceDamage] = new RunBuffDefinition
        {
            BuffId = RunBuffIds.ElfIceDamage,
            BuffName = "寒冰强化",
            NameKey = "runbuff.elficedamage.name",
            Description = "冰属性伤害+10。",
            DescriptionKey = "runbuff.elficedamage.desc",
            DefaultRemainingBattles = -1,
            Stackable = true,
            Icon = string.Empty,
            Type = RunBuffType.Blessing,
            Effects = new List<RunBuffEffect>
            {
                new()
                {
                    Timing = RunBuffTriggerTiming.OnDamageCalculate,
                    Type = RunBuffEffectType.PlayerIceDamageFlatBonus,
                    Value = 10
                }
            }
        },

        [RunBuffIds.ElfPoisonDamage] = new RunBuffDefinition
        {
            BuffId = RunBuffIds.ElfPoisonDamage,
            BuffName = "毒素强化",
            NameKey = "runbuff.elfpoisondamage.name",
            Description = "毒素伤害+10。",
            DescriptionKey = "runbuff.elfpoisondamage.desc",
            DefaultRemainingBattles = -1,
            Stackable = true,
            Icon = string.Empty,
            Type = RunBuffType.Blessing,
            Effects = new List<RunBuffEffect>
            {
                new()
                {
                    Timing = RunBuffTriggerTiming.OnDamageCalculate,
                    Type = RunBuffEffectType.PlayerPoisonDamageFlatBonus,
                    Value = 10
                }
            }
        }
    };

    /// <summary>
    /// Run Buff System 的公开入口：Get。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static RunBuffDefinition? Get(string buffId)
    {
        return Definitions.GetValueOrDefault(buffId);
    }

    /// <summary>
    /// Run Buff System 的公开入口：GetAll。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static IReadOnlyCollection<RunBuffDefinition> GetAll()
    {
        return Definitions.Values;
    }
}
