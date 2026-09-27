//////////////////////////////////////////////////////////
// 文件：Scripts/EnemyDatabase.cs
//
// 模块：Enemy System
//
// 职责：
// 1. 承载敌人定义、敌人实例与敌方 AI相关代码。
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
/// Enemy System 的公开类：EnemyDatabase。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class EnemyDatabase
{
    private static readonly IReadOnlyList<EnemyDefinition> Enemies = new[]
    {
        CreateScavenger(),
        CreateGateGuard(),
        CreateFeatherGuard(),
        CreateAbandonedServant(),
        CreateBossHealer(),
        CreateBossTraitor(),
        CreateCorpseCollector(),
        CreateHunter(),
        CreateExileBarbarian(),
        CreateCrazyPerformer(),
        CreateSteelGuard(),
        CreateModifiedThug(),
        CreateWineDrinker(),
        CreateCollectiveIntelligence(),
        CreateShinxinZhe(),
        CreateCyclops(),
        CreateGiantRollingStone(),
        CreateGiantRollingLog(),
        CreateWulongCollectiveIntelligence(),
        CreateZuoCi(),
        CreateGangBoss(),
        CreateMiHuanXiaoShou(),
        CreateRenwanGuard(),
        CreateIceGuard(),
        CreateHeavyArmorGuard(),
        CreateGunner(),
        CreateMoonBoss(),
        CreateBossTyrant(),
        CreateLiuBei(),
        CreateGuanYu(),
        CreateZhangFei(),
        CreateRoyalDeathGuard(),
        CreateGiantPusSac(),
        CreateGiantMechCockroach(),
        CreateGiantMechRat(),
        CreateHuangYiZhiZhu(),
        CreateYellowTurbanDevotee(),
        CreateRatKing(),
        CreateHuanXiangChuShou(),
        CreateTrainingDummy(),
        // 第四章·深渊：普通敌人。
        CreateScout(),
        CreateJuKou(),
        CreateAbyssSymbiote()
    };

    /// <summary>
    /// Enemy System 的公开入口：GetEnemy。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EnemyDefinition? GetEnemy(string id)
    {
        foreach (var enemy in Enemies)
        {
            if (enemy.Id == id)
            {
                return enemy;
            }
        }

        return null;
    }

    /// <summary>
    /// Enemy System 的公开入口：GetAllEnemies。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static IReadOnlyList<EnemyDefinition> GetAllEnemies()
    {
        return Enemies;
    }

    /// <summary>
    /// Enemy System 的公开入口：GetEnemiesForStage。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static IReadOnlyList<EnemyDefinition> GetEnemiesForStage(int stageNumber)
    {
        var enemies = new List<EnemyDefinition>();
        foreach (var enemy in Enemies)
        {
            if (stageNumber >= enemy.StageRange.MinStage && stageNumber <= enemy.StageRange.MaxStage)
            {
                enemies.Add(enemy);
            }
        }

        return enemies;
    }

    private static EnemyDefinition CreateScavenger()
    {
        return new EnemyDefinition
        {
            Id = "scavenger",
            Name = "拾荒者",
            NameKey = "enemy.scavenger.name",
            MaxHP = 30,
            Type = EnemyType.Normal,
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Common,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 1,
                MaxStage = 8
            },
            StartingResource = 0,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Steal
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 15,
                FireSlash = 50,
                ThunderSlash = 10,
                Dodge = 5,
                Wuxie = 5,
                Resource = 15,
                ShunShou = 40
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun },
            AiRules = new List<AiRule>
            {
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.PlayerResourceAtLeast, Value = 2 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.ShunShou, Delta = 100 }
                    }
                },
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.PlayerResourceAtLeast, Value = 4 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.ShunShou, Delta = 200 }
                    }
                }
            }
        };
    }

    private static EnemyDefinition CreateGiantRollingStone()
    {
        return new EnemyDefinition
        {
            Id = "giant_rolling_stone",
            Name = "巨型滚石",
            NameKey = "enemy.giant_rolling_stone.name",
            MaxHP = 120,
            Type = EnemyType.Elite,
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Epic,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 0,
                MaxStage = 0
            },
            StartingResource = 1,
            // 藏宝阁的威胁由第5回合四连杀脚本明确表达。这里不能再叠加未展示的
            // 喷气重锤与攻击芯片，否则单个敌人的首轮爆发会从40膨胀到150，
            // 战报只显示“装备发动”，玩家无法从事件说明或出招 UI 预判死亡。
            EquipmentIds = new List<string>(),
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Peach
                }
            },
            AiProfile = new EnemyAiProfile
            {
                ScriptedActions = new List<ScriptedEnemyActionRule>
                {
                    new() { MinTurn = 1, MaxTurn = 4, CardType = CardType.Fee, Count = 1 },
                    new() { MinTurn = 5, MaxTurn = 5, CardType = CardType.Kill, Count = 4 }
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 100,
                Resource = 20,
                Dodge = 5,
                Wuxie = 5
            },
            Tags = new List<EnemyTag> { EnemyTag.Qun, EnemyTag.RouteRuins },
            LoreId = "giant_rolling_stone"
        };
    }

    private static EnemyDefinition CreateGiantRollingLog()
    {
        return new EnemyDefinition
        {
            Id = "giant_rolling_log",
            Name = "巨型滚木",
            NameKey = "enemy.giant_rolling_log.name",
            MaxHP = 120,
            Type = EnemyType.Elite,
            SkillIds = new List<string> { SkillIds.Bizhong },
            Reward = new EnemyReward
            {
                EquipmentRewards = new List<string> { EquipmentIds.AttackChip }
            },
            RewardTier = RewardTier.Epic,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 0,
                MaxStage = 0
            },
            StartingResource = 0,
            // 与巨型滚石保持同一可读规则：第5回合四连必中杀本身就是爆发，
            // 不再叠加隐藏装备倍率。奖励中的攻击芯片不受影响。
            EquipmentIds = new List<string>(),
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.SureKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Peach
                }
            },
            AiProfile = new EnemyAiProfile
            {
                ScriptedActions = new List<ScriptedEnemyActionRule>
                {
                    new() { MinTurn = 1, MaxTurn = 4, CardType = CardType.Fee, Count = 1 },
                    new() { MinTurn = 5, MaxTurn = 5, CardType = CardType.SureKill, Count = 4 }
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                SureSlash = 100,
                Slash = 20,
                Resource = 20,
                Dodge = 5,
                Wuxie = 5
            },
            Tags = new List<EnemyTag> { EnemyTag.Qun, EnemyTag.RouteRuins },
            LoreId = "giant_rolling_log"
        };
    }

    private static EnemyDefinition CreateGateGuard()
    {
        return new EnemyDefinition
        {
            Id = "gate_guard",
            Name = "城关守卫",
            NameKey = "enemy.gate_guard.name",
            MaxHP = 50,
            Type = EnemyType.Normal,
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Common,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 1,
                MaxStage = 10
            },
            StartingResource = 0,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 15,
                FireSlash = 5,
                ThunderSlash = 45,
                Dodge = 60,
                Wuxie = 25,
                Resource = 20
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun, EnemyTag.Soldier },
            AiRules = new List<AiRule>
            {
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 2 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Dodge, Delta = 50 }
                    }
                },
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 3 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 30 }
                    }
                },
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.PlayerResourceBelow, Value = 1 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = 60 }
                    }
                }
            }
        };
    }

    private static EnemyDefinition CreateFeatherGuard()
    {
        return new EnemyDefinition
        {
            Id = "feather_guard",
            Name = "羽卫",
            NameKey = "enemy.feather_guard.name",
            MaxHP = 20,
            Type = EnemyType.Normal,
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Common,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 1,
                MaxStage = 8
            },
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.ArrowBarrage
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 15,
                FireSlash = 0,
                ThunderSlash = 0,
                ArrowBarrage = 120,
                NanmanInvasion = 0,
                Dodge = 20,
                Wuxie = 15,
                Resource = 45
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun },
            AiRules = new List<AiRule>
            {
                // 费用≥2：压倒性偏向万箭齐发（AOE核心技）
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 2 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.ArrowBarrage, Delta = 150 },
                        new() { WeightType = EnemyActionWeightType.Resource, Delta = -30 }
                    }
                },
                // 低血量时提高闪避以求生
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfHpBelow, Value = 11 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Dodge, Delta = 50 }
                    }
                }
            }
        };
    }

    private static EnemyDefinition CreateAbandonedServant()
    {
        return new EnemyDefinition
        {
            Id = "abandoned_servant",
            Name = "废弃机仆",
            NameKey = "enemy.abandoned_servant.name",
            MaxHP = 40,
            Type = EnemyType.Normal,
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Common,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 1,
                MaxStage = 3
            },
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 50,
                FireSlash = 60,
                ThunderSlash = 50,
                Dodge = 8,
                Wuxie = 5,
                Resource = 10
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun },
            AiRules = new List<AiRule>
            {
                // 费用≥2：大幅提高火杀/雷杀权重（2费优先火杀，伤害最大化）
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 2 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.FireSlash, Delta = 110 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 70 },
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.Resource, Delta = -8 }
                    }
                },
                // 费用≥3：进一步激活攻击欲望
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 3 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = 40 },
                        new() { WeightType = EnemyActionWeightType.FireSlash, Delta = 40 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 40 }
                    }
                },
                // 上回合使用攻击牌且当前有费：延续连续进攻节奏
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 1 },
                        new() { Type = AiConditionType.SelfLastActionAnySha }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = 60 },
                        new() { WeightType = EnemyActionWeightType.Resource, Delta = -8 }
                    }
                }
            }
        };
    }

    // ————————————————————————————————————————————————————
    // 精英敌人：收尸人 / 追捕者
    // ————————————————————————————————————————————————————

    private static EnemyDefinition CreateCorpseCollector()
    {
        return new EnemyDefinition
        {
            Id = "corpse_collector",
            Name = "收尸人",
            NameKey = "enemy.corpse_collector.name",
            LoreId = "corpse_collector",
            MaxHP = 80,
            Type = EnemyType.Elite,
            // 收尸人继承马超的【必中】：其专属必中杀会在敌人信息面板中
            // 作为技能展示，同时保留已有的 SureKill 卡组与 AI 权重。
            SkillIds = new List<string> { SkillIds.Bizhong },
            Reward = new EnemyReward
            {
                EquipmentDrops = new List<EnemyEquipmentDrop>
                {
                    new() { EquipmentId = EquipmentIds.RustBlueSteelSword, Probability = 0.5f }
                }
            },
            RewardTier = RewardTier.Rare,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 5,
                MaxStage = 5
            },
            StartingResource = 1,
            // 携带青钢剑-锈；战斗中所有杀系攻击伤害 +5。
            EquipmentIds = new List<string> { EquipmentIds.RustBlueSteelSword },
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.SureKill,
                    CardType.Kill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                SureSlash = 90,
                Slash = 20,
                Dodge = 30,
                Wuxie = 20,
                Resource = 20
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun },
            AiRules = new List<AiRule>
            {
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 1 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.SureSlash, Delta = 80 }
                    }
                },
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.PlayerHpBelow, Value = 20 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.SureSlash, Delta = 120 }
                    }
                }
            }
        };
    }

    private static EnemyDefinition CreateHunter()
    {
        return new EnemyDefinition
        {
            Id = "hunter",
            Name = "追捕者",
            NameKey = "enemy.hunter.name",
            MaxHP = 80,
            Type = EnemyType.Elite,
            SkillIds = new List<string> { SkillIds.Lianying },
            Reward = new EnemyReward
            {
                EquipmentDrops = new List<EnemyEquipmentDrop>
                {
                    new() { EquipmentId = EquipmentIds.HookChain, Probability = 0.5f }
                }
            },
            RewardTier = RewardTier.Rare,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 5,
                MaxStage = 5
            },
            StartingResource = 1,
            // 携带钩索；连续2次普通杀被闪后对目标造成5点直接伤害。
            EquipmentIds = new List<string> { EquipmentIds.HookChain },
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 80,
                FireSlash = 20,
                Dodge = 8,
                Wuxie = 8,
                Resource = 5
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun },
            AiRules = new List<AiRule>
            {
                // 费用≥1：极高攻击权重，主动将费用打空以触发连营
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 1 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = 100 },
                        new() { WeightType = EnemyActionWeightType.Resource, Delta = -5 }
                    }
                },
                // 费用=0：提高Kill权重（与LianyingFreeKillAvailable配合，实现85%攻击/15%闪避比例）
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceBelow, Value = 1 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = 120 }
                    }
                },
                // 上回合出杀且当前有费：延续连续进攻节奏
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfLastActionAnySha },
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 1 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = 60 }
                    }
                },
                // 玩家低血量：加大进攻力度
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.PlayerHpBelow, Value = 25 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = 50 }
                    }
                }
            }
        };
    }

    private static EnemyDefinition CreateExileBarbarian()
    {
        return new EnemyDefinition
        {
            Id = "exile_barbarian",
            Name = "流亡蛮族",
            NameKey = "enemy.exile_barbarian.name",
            MaxHP = 100,
            Type = EnemyType.Elite,
            SkillIds = new List<string> { SkillIds.Manzu },
            Reward = new EnemyReward
            {
                KnowledgeChipDropProbability = 0.5f
            },
            RewardTier = RewardTier.Rare,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 5,
                MaxStage = 5
            },
            StartingResource = 2,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.NanmanInvasion
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 8,
                FireSlash = 8,
                ThunderSlash = 8,
                NanmanInvasion = 100,
                Dodge = 15,
                Wuxie = 8,
                Resource = 15
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun },
            AiRules = new List<AiRule>
            {
                // 费用<2（无法使用南蛮入侵）：强制蓄费，压制杀系出牌
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceBelow, Value = 2 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Resource, Delta = 80 },
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = -25 },
                        new() { WeightType = EnemyActionWeightType.FireSlash, Delta = -20 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = -20 }
                    }
                },
                // 费用≥2：压倒性偏向南蛮入侵（蛮族技能使实际费用=2，AOE优先）
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 2 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.NanmanInvasion, Delta = 220 },
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = -5 },
                        new() { WeightType = EnemyActionWeightType.FireSlash, Delta = -5 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = -5 },
                        new() { WeightType = EnemyActionWeightType.Resource, Delta = -20 }
                    }
                },
                // 费用≥3：进一步强化南蛮入侵
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 3 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.NanmanInvasion, Delta = 100 }
                    }
                },
                // 玩家低血量：加大进攻力度
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.PlayerHpBelow, Value = 50 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.NanmanInvasion, Delta = 60 },
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = 15 }
                    }
                }
            }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 失疯卖艺人  （第一章 Boss，群，传说）
    // MaxHP=100（基础），不再携带鳞甲 → 有效 HP=100。
    // 技能：击鼓 + 不屈（HP≤0首次 → 复活1HP+2费+下回合无敌）。
    // 装备：战鼓（首次攻击后下回合起攻击+5，维持需每回合出攻击）。
    // 奖励：100金 + 30%概率掉落战鼓。
    // AI：正常阶段更倾向蓄费；低生命值时转为强攻击倾向。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateCrazyPerformer()
    {
        return new EnemyDefinition
        {
            Id = "crazy_performer",
            Name = "失疯卖艺人",
            NameKey = "enemy.crazy_performer.name",
            MaxHP = 100,
            Type = EnemyType.Boss,
            SkillIds = new List<string>
            {
                SkillIds.JiGu,
                SkillIds.BuDao
            },
            EquipmentIds = new List<string> { EquipmentIds.WarDrum },
            Reward = new EnemyReward
            {
                // 专属装备：战鼓，100%必定掉落（不再走概率掉落，避免同一件装备被判定两次）。
                EquipmentRewards = new List<string> { EquipmentIds.WarDrum },
                DefenseChipDropCount = 1
            },
            RewardTier = RewardTier.Legendary,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 7,
                MaxStage = 7
            },
            StartingResource = 2,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Peach
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 20,
                FireSlash = 15,
                ThunderSlash = 25,
                Dodge = 15,
                Wuxie = 10,
                Peach = 10,
                Resource = 45   // 正常阶段：优先蓄费等待时机
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun, EnemyTag.Boss },
            AiRules = new List<AiRule>
            {
                // 费用 >= 2：稍微减少蓄费，提权雷杀
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 2 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 25 },
                        new() { WeightType = EnemyActionWeightType.Resource,     Delta = -20 }
                    }
                },
                // HP < 40：单纯的 AI 攻击倾向，不对应额外技能、状态或伤害加成。
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfHpBelow, Value = 40 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 60 },
                        new() { WeightType = EnemyActionWeightType.Slash,        Delta = 20 },
                        new() { WeightType = EnemyActionWeightType.FireSlash,    Delta = 20 }
                    }
                }
            }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 医者  （第一章 Boss，群，史诗）
    // MaxHP=50（基础），装备藤甲后实例化时自动加 +10 → 有效 HP=60。
    // 技能：青囊（桃费用-1）+ 神秘补剂强化（生命≥120后使用必中杀，伤害+5）。
    // 装备：神秘补剂（当前生命≥120时普通杀变为必中杀）+ 藤甲（+10HP，物理减半/火属性翻倍）+ 发芽盆栽（桃+5回复→15）。
    // 奖励：100金 + 50%概率掉落藤甲。
    // AI：HP<80（有效HP≤60，此条件始终成立）时全力回血；强化后改用必中杀。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateBossHealer()
    {
        return new EnemyDefinition
        {
            Id = "boss_healer",
            Name = "医者",
            NameKey = "enemy.boss_healer.name",
            MaxHP = 50,     // 基础HP；藤甲在 EnemyInstance 初始化时额外 +10 → 有效60
            Type = EnemyType.Boss,
            SkillIds = new List<string> { SkillIds.Qingnang, SkillIds.BossHealerPassive },
            EquipmentIds = new List<string>
            {
                EquipmentIds.MysteriousPotion,
                EquipmentIds.Tengjia,
                EquipmentIds.SproutingBonsai
            },
            Reward = new EnemyReward
            {
                // 专属装备：神秘补剂，100%必定掉落。
                EquipmentRewards = new List<string> { EquipmentIds.MysteriousPotion },
                EquipmentDrops = new List<EnemyEquipmentDrop>
                {
                    new() { EquipmentId = EquipmentIds.Tengjia, Probability = 0.5f }
                },
                DefenseChipDropCount = 1
            },
            RewardTier = RewardTier.Legendary,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 7,
                MaxStage = 7
            },
            StartingResource = 2,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Peach,
                    CardType.SureKill
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 15,
                FireSlash = 15,
                ThunderSlash = 15,
                Dodge = 25,
                Peach = 100,    // 高基础桃权重，配合青囊费用降为1，发芽盆栽每桃回复15
                Wuxie = 20,
                Resource = 10,
                SureSlash = 5   // 强化前低权重，强化后由EnemyAI硬编码提至100+
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun, EnemyTag.Boss, EnemyTag.Healer },
            AiRules = new List<AiRule>
            {
                // HP < 80（有效HP上限）：大幅提权桃
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfHpBelow, Value = 80 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Peach, Delta = 100 }
                    }
                },
                // HP < 50（濒危）：进一步提权桃，减少攻击倾向
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfHpBelow, Value = 50 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Peach, Delta = 150 },
                        new() { WeightType = EnemyActionWeightType.Slash,       Delta = -10 },
                        new() { WeightType = EnemyActionWeightType.FireSlash,   Delta = -10 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = -10 }
                    }
                },
                // 费用 >= 1：可以出桃时优先回血
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 1 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Peach, Delta = 50 }
                    }
                }
            }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 背叛者  （第一章 Boss，群，传说）
    // MaxHP=70，技能：无双（杀系伤害×2）+ 闭月（偶数回合+1费）+ 连击（被动：2连攻+5伤害偏火雷）。
    // 装备：虎符（第4回合结束后+1费，仅一次）。
    // 奖励：100金 + 50%概率掉落虎符。
    // AI：命中后攻击提权；费用≥2显著偏向火/雷；连击激活后由EnemyAI加权。
    // 削弱：初始费用从1改为0（不再有初始蓄费，进一步压低第1回合的爆发空间）。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateBossTraitor()
    {
        return new EnemyDefinition
        {
            Id = "boss_traitor",
            Name = "背叛者",
            NameKey = "enemy.boss_traitor.name",
            MaxHP = 70,
            Type = EnemyType.Boss,
            SkillIds = new List<string> { SkillIds.Wushuang, SkillIds.Biyue, SkillIds.BossTraitorCombo },
            EquipmentIds = new List<string> { EquipmentIds.TigerTally },
            Reward = new EnemyReward
            {
                // 专属装备：虎符，100%必定掉落（不再走概率掉落，避免同一件装备被判定两次）。
                EquipmentRewards = new List<string> { EquipmentIds.TigerTally },
                DefenseChipDropCount = 1
            },
            RewardTier = RewardTier.Legendary,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 7,
                MaxStage = 7
            },
            StartingResource = 0,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee
                }
            },
            // 基础攻击权重偏低，第1回合倾向蓄费；连击状态由EnemyAI.cs硬编码叠加火/雷权重。
            ActionWeights = new EnemyActionWeights
            {
                Slash = 30,
                FireSlash = 30,
                ThunderSlash = 30,
                Dodge = 20,
                Wuxie = 10,
                Resource = 35   // 初始费用1，第1回合倾向蓄费
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun, EnemyTag.Boss, EnemyTag.Traitor },
            LoreId = "boss_traitor",
            AiRules = new List<AiRule>
            {
                // 上一回合使用过攻击牌（命中后）→ 提高所有杀权重（乘势追击）
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfLastActionAnySha }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Slash,        Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.FireSlash,    Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.Resource,     Delta = -15 }
                    }
                },
                // 费用 >= 2 → 偏向火杀/雷杀
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 2 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.FireSlash,    Delta = 60 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 60 },
                        new() { WeightType = EnemyActionWeightType.Resource,     Delta = -20 }
                    }
                },
                // 费用 >= 3 → 停止蓄费，全力进攻
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 3 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Resource,     Delta = -25 },
                        new() { WeightType = EnemyActionWeightType.Slash,        Delta = 20 },
                        new() { WeightType = EnemyActionWeightType.FireSlash,    Delta = 20 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 20 }
                    }
                }
            }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 钢铁卫士  （第二章普通怪，群）
    // MaxHP=80（基础），携带铁卫重甲 +20 → 有效 HP=100。
    // 装备：铁卫重甲（首次受到>20点伤害时归零）。
    // 初始费用1，重甲弩兵——倾向蓄费，费用≥2后压倒性偏向万箭齐发。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateSteelGuard()
    {
        return new EnemyDefinition
        {
            Id = "steel_guard",
            // RouteCity：仅出现在城市路线（ChapterRoute.Default）。
            Name = "钢铁卫士",
            NameKey = "enemy.steel_guard.name",
            MaxHP = 80,     // 基础HP；铁卫重甲在 EnemyInstance 初始化时额外 +20 → 有效100
            Type = EnemyType.Normal,
            EquipmentIds = new List<string> { EquipmentIds.IronHeavyArmor },
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Common,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 8,
                MaxStage = 10
            },
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.ArrowBarrage
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 15,
                FireSlash = 10,
                ThunderSlash = 10,
                ArrowBarrage = 30,
                Dodge = 15,
                Wuxie = 10,
                Resource = 50
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun, EnemyTag.RouteCity },
            AiRules = new List<AiRule>
            {
                // 费用≥2：万箭齐发权重压倒性提升，杀系权重大幅降低
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 2 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.ArrowBarrage, Delta = 200 },
                        new() { WeightType = EnemyActionWeightType.Slash,        Delta = -10 },
                        new() { WeightType = EnemyActionWeightType.FireSlash,    Delta = -5 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = -5 },
                        new() { WeightType = EnemyActionWeightType.Resource,     Delta = -30 }
                    }
                },
                // 低血量时提高闪避
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfHpBelow, Value = 40 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Dodge, Delta = 30 }
                    }
                }
            }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 酒徒  （第二章普通怪，群，城市2-1/2-3）
    // MaxHP=50，装备酒囊（本局第一张酒免费）。初始费用2。
    // 自饮自爆型——始终优先让自己处于酒效果状态，一旦有酒层数立刻转为用杀/火杀/雷杀
    // 消耗增伤，而不是继续屯酒；费用充足时优先火杀/雷杀打出更高伤害。
    // 低血量时降低"喝酒强攻"的倾向，转为用桃保命。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateWineDrinker()
    {
        return new EnemyDefinition
        {
            Id = "wine_drinker",
            Name = "酒徒",
            NameKey = "enemy.wine_drinker.name",
            MaxHP = 50,
            Type = EnemyType.Normal,
            SkillIds = new List<string> { SkillIds.ChangYin },
            EquipmentIds = new List<string> { EquipmentIds.WinePouch },
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Common,
            UseSharedHealthPool = false,
            StageRange = new StageRange { MinStage = 8, MaxStage = 10 },
            StartingResource = 2,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Peach,
                    CardType.Wine
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 15,
                FireSlash = 15,
                ThunderSlash = 15,
                Dodge = 12,
                Peach = 8,
                Wuxie = 6,
                Resource = 12,
                Wine = 100
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun, EnemyTag.RouteCity },
            AiRules = new List<AiRule>
            {
                // 酒效果已生效：不再继续屯酒，转为用杀系消耗增伤（火杀/雷杀优先于普通杀）。
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfWineLayersAtLeast, Value = 1 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Wine, Delta = -80 },
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = 25 },
                        new() { WeightType = EnemyActionWeightType.FireSlash, Delta = 35 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 35 }
                    }
                },
                // 费用充足（≥2）：优先火杀/雷杀打出更高伤害。
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 2 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.FireSlash, Delta = 25 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 25 },
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = -5 }
                    }
                },
                // 低血量：降低喝酒强攻倾向，优先用桃保命。
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfHpBelow, Value = 40 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Wine, Delta = -60 },
                        new() { WeightType = EnemyActionWeightType.Peach, Delta = 70 },
                        new() { WeightType = EnemyActionWeightType.Dodge, Delta = 15 }
                    }
                }
            }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 改造打手  （第二章普通怪，群）
    // MaxHP=60，携带喷气式狼牙棒（首次攻击×3）。
    // 初始费用2，爆发型近战——费用≥3时大概率喝酒，酒后强制火杀>雷杀>普通杀，利用狼牙棒打出超高首击。
    // 禁止连续喝酒；费用<2时完全禁酒（保证酒后至少能发动一次攻击）。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateModifiedThug()
    {
        return new EnemyDefinition
        {
            Id = "modified_thug",
            Name = "改造打手",
            NameKey = "enemy.modified_thug.name",
            MaxHP = 60,
            Type = EnemyType.Normal,
            EquipmentIds = new List<string> { EquipmentIds.JetMace },
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Common,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 8,
                MaxStage = 10
            },
            StartingResource = 2,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Wine
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 20,
                FireSlash = 30,
                ThunderSlash = 25,
                Wine = 10,
                Dodge = 15,
                Wuxie = 8,
                Resource = 10
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun, EnemyTag.RouteCity },
            AiRules = new List<AiRule>
            {
                // 酒后：强制攻击，优先火杀>雷杀>普通杀，禁止再次喝酒与收费
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfLastActionWine }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.FireSlash,    Delta = 200 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 160 },
                        new() { WeightType = EnemyActionWeightType.Slash,        Delta = 100 },
                        new() { WeightType = EnemyActionWeightType.Wine,         Delta = -1000 },
                        new() { WeightType = EnemyActionWeightType.Resource,     Delta = -1000 }
                    }
                },
                // 费用<2：完全禁止喝酒（酒后无法发动有效攻击）
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceBelow, Value = 2 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Wine, Delta = -1000 }
                    }
                },
                // 费用≥2：允许喝酒（至少能在酒后发动普通杀）
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 2 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Wine, Delta = 40 }
                    }
                },
                // 费用≥3：大幅提高喝酒意愿（酒后可打出火杀/雷杀，理想爆发回合）
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 3 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Wine, Delta = 70 }
                    }
                }
            }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 集智体  （第二章精英，城市路线，群）
    // HP=140，携带攻击芯片×1 + 雷矛；初始费用2。
    // 观星型精英：第一回合大概率使用观星记录，之后利用雷矛连续发动低费雷杀爆发。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateCollectiveIntelligence()
    {
        return new EnemyDefinition
        {
            Id = "collective_intelligence",
            Name = "集智体",
            NameKey = "enemy.collective_intelligence.name",
            MaxHP = 140,
            Type = EnemyType.Elite,
            SkillIds = new List<string> { SkillIds.Guanxing },
            EquipmentIds = new List<string> { EquipmentIds.AttackChip, EquipmentIds.ThunderSpear },
            Reward = new EnemyReward
            {
                EquipmentDrops = new List<EnemyEquipmentDrop>
                {
                    new() { EquipmentId = EquipmentIds.AttackChip, Probability = 0.5f }
                }
            },
            RewardTier = RewardTier.Rare,
            UseSharedHealthPool = false,
            StageRange = new StageRange { MinStage = 12, MaxStage = 12 },
            StartingResource = 2,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Guanxing
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 15,
                ThunderSlash = 25,
                Dodge = 12,
                Wuxie = 8,
                Resource = 10,
                Guanxing = 65
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun, EnemyTag.RouteCity },
            AiRules = new List<AiRule>
            {
                // 非观星状态（None）：录制/重复阶段由 BuildEnemyActions 强制选择攻击牌（不经过此权重），
                // 因此这里只需要把 None 阶段的杀系权重压到很低，避免它在正式进入观星循环之前就把攻击牌打光。
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfGuanxingPhaseNone }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Slash,       Delta = -13 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = -23 }
                    }
                },
                // 观星前蓄费（第一阶段）：资源不足3时优先积攒，确保录制时可连续施放雷杀。
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfGuanxingPhaseNone },
                        new() { Type = AiConditionType.SelfResourceBelow, Value = 3 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Resource, Delta = 80 },
                        new() { WeightType = EnemyActionWeightType.Guanxing, Delta = -50 }
                    }
                },
                // 费用充足时提高观星权重，尽快进入观星循环。
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfGuanxingPhaseNone },
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 3 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Guanxing, Delta = 20 }
                    }
                },
                // 低血量时转向防御。
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfHpBelow, Value = 40 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Dodge,    Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.Guanxing, Delta = -30 }
                    }
                }
            }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 失心者（第二章精英，魏，城市路线 2-5）
    // 情感组件受损、失去理智的人形单位。HP=130，血量低时优先回血，平时以普通杀为主。
    // 技能【完杀】+ 装备【破损情感组件】（该装备每回合结束为全场单位回复10生命，
    // 与完杀形成"全场回血同时全场受到等量真实伤害"的联动）。
    // 掉落破损情感组件（50%概率）。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateShinxinZhe()
    {
        return new EnemyDefinition
        {
            Id = "shixin_zhe",
            Name = "失心者",
            NameKey = "enemy.shixin_zhe.name",
            MaxHP = 130,
            Type = EnemyType.Elite,
            SkillIds = new List<string> { SkillIds.Wansha },
            EquipmentIds = new List<string> { EquipmentIds.BrokenEmotionalComponent },
            Reward = new EnemyReward
            {
                EquipmentDrops = new List<EnemyEquipmentDrop>
                {
                    new() { EquipmentId = EquipmentIds.BrokenEmotionalComponent, Probability = 0.5f }
                }
            },
            RewardTier = RewardTier.Rare,
            UseSharedHealthPool = false,
            StageRange = new StageRange { MinStage = 12, MaxStage = 12 },
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.Kill,
                    CardType.Peach,
                    CardType.Peach,
                    CardType.Dodge,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 30,
                Dodge = 15,
                Peach = 25,
                Resource = 10
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Wei, EnemyTag.RouteCity },
            AiRules = new List<AiRule>
            {
                // 血量低于30时大幅提升桃权重，优先回血。
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfHpBelow, Value = 30 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Peach,  Delta = 80 },
                        new() { WeightType = EnemyActionWeightType.Slash,  Delta = -15 }
                    }
                }
            }
        };
    }

    // 独眼巨人（第二章精英，魏，城市路线 2-5）
    // 基础HP=80；鳞甲在 EnemyInstance 初始化时额外 +10 → 有效HP=90。
    // 技能：血债血偿（每次受到伤害后，对玩家反弹等量真实伤害）。
    // AI定位：极高攻击性，几乎不防御，优先连续攻击。血量越低攻击欲望越强。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateCyclops()
    {
        return new EnemyDefinition
        {
            Id = "cyclops",
            Name = "独眼巨人",
            NameKey = "enemy.cyclops.name",
            MaxHP = 80,
            Type = EnemyType.Elite,
            SkillIds = new List<string> { SkillIds.XueZhaiXueChou },
            EquipmentIds = new List<string> { EquipmentIds.ScaleArmor },
            Reward = new EnemyReward
            {
                EquipmentDrops = new List<EnemyEquipmentDrop>
                {
                    new() { EquipmentId = EquipmentIds.ScaleArmor, Probability = 0.5f }
                }
            },
            RewardTier = RewardTier.Rare,
            UseSharedHealthPool = false,
            StageRange = new StageRange { MinStage = 12, MaxStage = 12 },
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Peach,
                    CardType.Wine,
                    CardType.Unassailable,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 60,
                FireSlash = 48,
                ThunderSlash = 40,
                Dodge = 2,
                Wuxie = 1,
                Resource = 8
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Wei, EnemyTag.RouteCity },
            AiRules = new List<AiRule>
            {
                // 上回合使用了杀：连续发动攻击，进一步压低防御意愿
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfLastActionAnySha }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Slash,     Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.FireSlash, Delta = 25 },
                        new() { WeightType = EnemyActionWeightType.Dodge,     Delta = -4 },
                        new() { WeightType = EnemyActionWeightType.Resource,  Delta = -5 }
                    }
                },
                // 低血量时更强的攻击性（血债血偿使伤亡逆转为威胁）。
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfHpBelow, Value = 70 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Slash,     Delta = 40 },
                        new() { WeightType = EnemyActionWeightType.FireSlash, Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.Dodge,     Delta = -4 }
                    }
                }
            }
        };
    }

    // 左·慈（第二章 Boss，城市路线 2-8，群，传奇）
    // HP=1（基础值，化形触发后立即变为玩家当前血量）；携带仙毫。
    // 战斗开始时发动【化形】：复制玩家血量/技能/装备（芯片除外），保留化形与仙毫。
    private static EnemyDefinition CreateZuoCi()
    {
        return new EnemyDefinition
        {
            Id = "zuoci",
            Name = "左·慈",
            NameKey = "enemy.zuoci.name",
            MaxHP = 1,
            Type = EnemyType.Boss,
            SkillIds = new List<string> { SkillIds.HuaXing },
            EquipmentIds = new List<string> { EquipmentIds.XianHao },
            Reward = new EnemyReward
            {
                DefenseChipDropCount = 1,
                KnowledgeChipDropProbability = 1.0f
            },
            RewardTier = RewardTier.Legendary,
            UseSharedHealthPool = false,
            StageRange = new StageRange { MinStage = 15, MaxStage = 15 },
            StartingResource = 2,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.FireThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Peach
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 20,
                FireSlash = 25,
                ThunderSlash = 25,
                FireThunderSlash = 35,
                Dodge = 20,
                Peach = 20,
                Wuxie = 15,
                Resource = 15
            },
            Tags = new List<EnemyTag> { EnemyTag.Qun, EnemyTag.Boss, EnemyTag.RouteCity },
            AiRules = new List<AiRule>
            {
                // 有费时优先出高伤害牌
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 2 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.FireThunderSlash, Delta = 40 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash,     Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.FireSlash,        Delta = 20 },
                        new() { WeightType = EnemyActionWeightType.Resource,         Delta = -10 }
                    }
                },
                // 玩家血量偏低时全力猛攻
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.PlayerHpBelow, Value = 15 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.FireThunderSlash, Delta = 60 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash,     Delta = 50 },
                        new() { WeightType = EnemyActionWeightType.FireSlash,        Delta = 40 },
                        new() { WeightType = EnemyActionWeightType.Slash,            Delta = 30 }
                    }
                },
                // 上回合出杀后持续施压
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfLastActionAnySha },
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 1 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.FireThunderSlash, Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash,     Delta = 25 },
                        new() { WeightType = EnemyActionWeightType.FireSlash,        Delta = 20 },
                        new() { WeightType = EnemyActionWeightType.Slash,            Delta = 15 }
                    }
                }
            }
        };
    }

    // 帮派头目（第二章 Boss，城市路线 2-8，吴）
    // HP=180；携带古锭刀+酒囊；技能：破军+长醉。
    // 核心策略：存酒（长醉）→ 顺手牵羊 → 火雷杀+破军爆发。
    private static EnemyDefinition CreateGangBoss()
    {
        return new EnemyDefinition
        {
            Id = "gang_boss",
            Name = "帮派头目",
            NameKey = "enemy.gang_boss.name",
            MaxHP = 180,
            Type = EnemyType.Boss,
            SkillIds = new List<string> { SkillIds.PoJun, SkillIds.ChangZui },
            EquipmentIds = new List<string> { EquipmentIds.GuDingDao, EquipmentIds.WinePouch },
            Reward = new EnemyReward
            {
                DefenseChipDropCount = 1,
                KnowledgeChipDropProbability = 1.0f
            },
            RewardTier = RewardTier.Legendary,
            UseSharedHealthPool = false,
            StageRange = new StageRange { MinStage = 15, MaxStage = 15 },
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.FireThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Peach,
                    CardType.Steal,
                    CardType.Wine
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 15,
                FireSlash = 20,
                ThunderSlash = 25,
                FireThunderSlash = 35,
                Wine = 45,
                ShunShou = 40,
                Dodge = 12,
                Peach = 12,
                Wuxie = 10,
                Resource = 8
            },
            Tags = new List<EnemyTag> { EnemyTag.Wu, EnemyTag.Boss, EnemyTag.RouteCity },
            AiRules = new List<AiRule>
            {
                // 有酒层时：大幅提高攻击权重，降低继续积酒权重
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfWineLayersAtLeast, Value = 1 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.FireThunderSlash, Delta = 60 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash,     Delta = 40 },
                        new() { WeightType = EnemyActionWeightType.FireSlash,        Delta = 25 },
                        new() { WeightType = EnemyActionWeightType.Slash,            Delta = 15 },
                        new() { WeightType = EnemyActionWeightType.Wine,             Delta = -30 }
                    }
                },
                // 上回合顺手牵羊后：高概率立即用火雷杀爆发（约80%权重倾向）
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfLastActionSteal }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.FireThunderSlash, Delta = 120 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash,     Delta = 60 },
                        new() { WeightType = EnemyActionWeightType.Wine,             Delta = -50 },
                        new() { WeightType = EnemyActionWeightType.ShunShou,         Delta = -30 }
                    }
                },
                // 自身血量 < 90（约50%）：加大攻势，降低防御
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfHpBelow, Value = 90 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.FireThunderSlash, Delta = 40 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash,     Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.FireSlash,        Delta = 20 },
                        new() { WeightType = EnemyActionWeightType.Dodge,            Delta = -15 },
                        new() { WeightType = EnemyActionWeightType.Peach,            Delta = -10 }
                    }
                },
                // 玩家血量 < 20：全力猛攻
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.PlayerHpBelow, Value = 20 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.FireThunderSlash, Delta = 80 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash,     Delta = 50 },
                        new() { WeightType = EnemyActionWeightType.FireSlash,        Delta = 35 },
                        new() { WeightType = EnemyActionWeightType.Slash,            Delta = 25 }
                    }
                }
            }
        };
    }

    // 卧龙集智体  （第二章 Boss，城市路线 2-8，群）
    // HP=100（基础）+50（钳制机械外骨骼）=150 有效生命；携带攻击芯片×2 + 钳制机械外骨骼。
    // 奖励：生命芯片（当前实现复用防御芯片，提供最大/当前生命+10）+ 知识芯片。
    // 第一阶段：观星布局 + 存费至3费 → 火雷杀爆发。
    // 第二阶段（钳制机械外骨骼触发后）：顺手牵羊 + 酒BUFF + 满血修复 → 火雷杀连续爆发。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateWulongCollectiveIntelligence()
    {
        return new EnemyDefinition
        {
            Id = "wulong_collective_intelligence",
            Name = "卧龙集智体",
            NameKey = "enemy.wulong_collective_intelligence.name",
            MaxHP = 100,
            Type = EnemyType.Boss,
            SkillIds = new List<string> { SkillIds.Guanxing },
            EquipmentIds = new List<string>
            {
                EquipmentIds.AttackChip,
                EquipmentIds.AttackChip,
                EquipmentIds.ClampExoskeleton
            },
            Reward = new EnemyReward
            {
                DefenseChipDropCount = 1,
                KnowledgeChipDropProbability = 1.0f
            },
            RewardTier = RewardTier.Legendary,
            UseSharedHealthPool = false,
            StageRange = new StageRange { MinStage = 15, MaxStage = 15 },
            StartingResource = 2,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.FireThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Guanxing
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 10,
                FireSlash = 15,
                ThunderSlash = 20,
                FireThunderSlash = 30,
                Guanxing = 120,
                Resource = 15,
                Dodge = 10,
                Wuxie = 8
            },
            Tags = new List<EnemyTag> { EnemyTag.Qun, EnemyTag.Boss, EnemyTag.RouteCity },
            AiRules = new List<AiRule>
            {
                // 观星前蓄费（第一阶段）：资源不足3时优先积攒，等待火雷杀爆发时机。
                // 录制阶段由 BuildEnemyActions 中的 SelectGuanxingRecordCard 直接决定牌型，此规则不参与录制。
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfGuanxingPhaseNone },
                        new() { Type = AiConditionType.SelfResourceBelow, Value = 3 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Resource,          Delta = 200 },
                        new() { WeightType = EnemyActionWeightType.FireThunderSlash,  Delta = -50 },
                        new() { WeightType = EnemyActionWeightType.Guanxing,          Delta = -1000 }
                    }
                },
                // 第二阶段（钳制机械外骨骼已触发）：攻击权重大幅提高，尽快用火雷杀爆发。
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfExoskeletonPhaseTwo }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.FireThunderSlash,  Delta = 220 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash,      Delta = 80 },
                        new() { WeightType = EnemyActionWeightType.FireSlash,         Delta = 50 },
                        new() { WeightType = EnemyActionWeightType.Slash,             Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.Guanxing,          Delta = -1000 },
                        new() { WeightType = EnemyActionWeightType.Resource,          Delta = -80 }
                    }
                }
            }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 迷幻箫手  （第三章皇宫路线普通怪，群）
    // 携带排箫：敌方受伤×1.1，我方受伤÷1.1。
    // 出现场景：皇宫路线 3-1（StageRange 16）/ 3-3（StageRange 18）。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateMiHuanXiaoShou()
    {
        return new EnemyDefinition
        {
            Id = "mihuan_xiao_shou",
            Name = "迷幻箫手",
            NameKey = "enemy.mihuan_xiao_shou.name",
            MaxHP = 80,
            Type = EnemyType.Normal,
            EquipmentIds = new List<string> { EquipmentIds.PanXiao },
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Common,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 16,
                MaxStage = 18
            },
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 40,
                FireSlash = 30,
                ThunderSlash = 20,
                Dodge = 10,
                Wuxie = 8,
                Resource = 30
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun, EnemyTag.RouteImperial },
            AiRules = new List<AiRule>
            {
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 2 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.FireSlash,    Delta = 60 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 50 },
                        new() { WeightType = EnemyActionWeightType.Slash,        Delta = 20 },
                        new() { WeightType = EnemyActionWeightType.Resource,     Delta = -20 }
                    }
                }
            },
            LoreId = "mihuan_xiao_shou"
        };
    }

    private static EnemyDefinition CreateIceGuard()
    {
        return new EnemyDefinition
        {
            Id = "ice_guard",
            Name = "寒冰护卫",
            NameKey = "enemy.ice_guard.name",
            MaxHP = 200,
            Type = EnemyType.Normal,
            SkillIds = new List<string> { SkillIds.JiHan, SkillIds.Biyue },
            EquipmentIds = new List<string>
            {
                EquipmentIds.IceBlueArmor,
                EquipmentIds.IceBlueArmor
            },
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Rare,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 3,
                MaxStage = 5
            },
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.IceKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Peach
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                IceSlash = 100,
                Slash = 25,
                FireSlash = 18,
                ThunderSlash = 18,
                Dodge = 7,
                Wuxie = 8,
                Peach = 10,
                Resource = 3
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Wei, EnemyTag.Soldier, EnemyTag.RouteImperial },
            AiRules = new List<AiRule>
            {
                // 目标冻结后：大幅提升攻击权重，积极趁冻爆发，压缩防御与出费空间
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.PlayerIsFrozen }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Slash,        Delta = 70 },
                        new() { WeightType = EnemyActionWeightType.FireSlash,    Delta = 55 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 55 },
                        new() { WeightType = EnemyActionWeightType.IceSlash,     Delta = 40 },
                        new() { WeightType = EnemyActionWeightType.Dodge,        Delta = -7 },
                        new() { WeightType = EnemyActionWeightType.Resource,     Delta = -3 }
                    }
                },
                // 上回合使用了杀系牌：连续发动攻击，进一步抑制防御
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfLastActionAnySha }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.IceSlash,     Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.Slash,        Delta = 25 },
                        new() { WeightType = EnemyActionWeightType.FireSlash,    Delta = 20 },
                        new() { WeightType = EnemyActionWeightType.Dodge,        Delta = -7 },
                        new() { WeightType = EnemyActionWeightType.Resource,     Delta = -3 }
                    }
                },
                // 费用充足时优先冰杀
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 3 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.IceSlash, Delta = 50 }
                    }
                }
            }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 仁王卫（第三章皇宫路线普通怪，群）
    // 携带仁王盾：本局第一次受到伤害时免疫该次伤害。
    // 出现场景：皇宫路线 3-1 / 3-3。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateRenwanGuard()
    {
        return new EnemyDefinition
        {
            Id = "renwan_guard",
            Name = "仁王卫",
            NameKey = "enemy.renwan_guard.name",
            MaxHP = 140,
            Type = EnemyType.Normal,
            EquipmentIds = new List<string>
            {
                EquipmentIds.BenevolentKing,
                EquipmentIds.AttackChip,
                EquipmentIds.AttackChip
            },
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Common,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 16,
                MaxStage = 20
            },
            StartingResource = 2,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Peach
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                // 高攻击权重使其即使受到 AI 的重复出牌抑制，仍会持续施压。
                Slash = 65,
                FireSlash = 45,
                Dodge = 4,
                Wuxie = 3,
                Peach = 6,
                Resource = 7
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun, EnemyTag.RouteImperial }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 重甲铁卫（第三章皇宫路线普通怪，群）
    // 携带重锤：前8回合累计主动攻击伤害，第8回合结束时爆发。
    // 出现场景：皇宫路线 3-3（StageRange 18）。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateHeavyArmorGuard()
    {
        return new EnemyDefinition
        {
            Id = "heavy_armor_guard",
            Name = "重甲铁卫",
            NameKey = "enemy.heavy_armor_guard.name",
            MaxHP = 150,
            Type = EnemyType.Normal,
            EquipmentIds = new List<string> { EquipmentIds.HeavyHammer },
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Common,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 16,
                MaxStage = 18
            },
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 25,
                FireSlash = 20,
                ThunderSlash = 20,
                Dodge = 10,
                Wuxie = 8,
                Resource = 20
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun, EnemyTag.RouteImperial },
            LoreId = "heavy_armor_guard"
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 枪手（第三章皇宫路线普通怪，群）
    // 双枪手时轮流：A优先费/B优先杀，下回合交换。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateGunner()
    {
        return new EnemyDefinition
        {
            Id = "gunner",
            Name = "枪手",
            NameKey = "enemy.gunner.name",
            MaxHP = 60,
            Type = EnemyType.Normal,
            EquipmentIds = new List<string> { EquipmentIds.HejinMao },
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Common,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 16,
                MaxStage = 18
            },
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 35,
                FireSlash = 15,
                ThunderSlash = 15,
                Dodge = 8,
                Wuxie = 6,
                Resource = 25
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun, EnemyTag.RouteImperial },
            LoreId = "gunner"
        };
    }

    private static EnemyDefinition CreateMoonBoss()
    {
        return new EnemyDefinition
        {
            Id = "moon_boss",
            Name = "月亮",
            NameKey = "enemy.moon_boss.name",
            MaxHP = 120,
            Type = EnemyType.Boss,
            SkillIds = new List<string>
            {
                SkillIds.MoonGaze,
                SkillIds.CelestialImpact
            },
            Reward = new EnemyReward
            {
                EquipmentRewards = new List<string> { EquipmentIds.MoonGem }
            },
            RewardTier = RewardTier.Legendary,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 0,
                MaxStage = 0
            },
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.CelestialImpact,
                    CardType.Dodge,
                    CardType.Peach,
                    CardType.Wine,
                    CardType.Unassailable,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                CelestialImpact = 70,
                Dodge = 20,
                Peach = 20,
                Wine = 15,
                Wuxie = 10,
                Resource = 20
            },
            AiRules = new List<AiRule>
            {
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfLastActionCelestialImpact }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Resource, Delta = 120 }
                    }
                }
            },
            EquipmentIds = new List<string> { EquipmentIds.MoonGem },
            Tags = new List<EnemyTag> { EnemyTag.Qun, EnemyTag.Boss },
            LoreId = "moon_boss"
        };
    }

    private static EnemyDefinition CreateBossTyrant()
    {
        return new EnemyDefinition
        {
            Id = "boss_tyrant",
            Name = "暴虐昏君",
            NameKey = "enemy.boss_tyrant.name",
            MaxHP = 1000,
            Type = EnemyType.Boss,
            // 【崩坏】是董卓专属技能。暴虐昏君即董卓的 Boss 形态，使用同一份技能定义，
            // 不再保留旧版“高于500HP扣10、否则回10”的泛用伪技能。
            SkillIds = new List<string> { SkillIds.JiuChi, SkillIds.RouLin, SkillIds.DongZhuoBengHuai, SkillIds.BaoNue },
            EquipmentIds = new List<string> { EquipmentIds.XianNiang, EquipmentIds.TyrantCrown },
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Legendary,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 24,
                MaxStage = 24
            },
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.NanmanInvasion,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Peach,
                    CardType.Wine,
                    CardType.Steal
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 18,
                FireSlash = 12,
                ThunderSlash = 12,
                NanmanInvasion = 10,
                Dodge = 8,
                Peach = 6,
                Wuxie = 6,
                Resource = 30,
                ShunShou = 24,
                Wine = 80
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun, EnemyTag.Boss, EnemyTag.Warlord, EnemyTag.RouteImperial },
            LoreId = "boss_tyrant",
            AiRules = new List<AiRule>
            {
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfHpBelow, Value = 501 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Wine, Delta = 100 },
                        new() { WeightType = EnemyActionWeightType.FireSlash, Delta = 90 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 90 },
                        new() { WeightType = EnemyActionWeightType.NanmanInvasion, Delta = 120 },
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = 60 },
                        new() { WeightType = EnemyActionWeightType.Resource, Delta = -20 },
                        new() { WeightType = EnemyActionWeightType.ShunShou, Delta = -10 }
                    }
                },
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfLastActionWine }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Resource, Delta = 40 },
                        new() { WeightType = EnemyActionWeightType.ShunShou, Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = 10 }
                    }
                },
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 2 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.FireSlash, Delta = 40 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 40 }
                    }
                },
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 3 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Resource, Delta = -20 },
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.FireSlash, Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.NanmanInvasion, Delta = 30 }
                    }
                }
            }
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 第三章皇宫路线最终Boss：蜀汉共生体（刘备 / 关羽 / 张飞，共享300HP生命池）
    // ──────────────────────────────────────────────────────────────────────────

    private static EnemyDefinition CreateLiuBei()
    {
        return new EnemyDefinition
        {
            Id = "liu_bei",
            Name = "刘备",
            NameKey = "enemy.liu_bei.name",
            MaxHP = 300,
            Type = EnemyType.Boss,
            UseSharedHealthPool = true,
            SkillIds = new List<string> { SkillIds.Rende, SkillIds.TaoyuanJiyi },
            // 蜀汉共生体（刘备/关羽/张飞共享血条）：金币只由张飞一人结算，避免同一场战斗被
            // 三个敌人各自的统一金币掉落重复计算三份。
            Reward = new EnemyReward { GrantsGold = false },
            RewardTier = RewardTier.Common,
            StageRange = new StageRange { MinStage = 20, MaxStage = 20 },
            StartingResource = 3,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Peach,
                    CardType.Wine,
                    CardType.Unassailable,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 35,
                FireSlash = 20,
                ThunderSlash = 20,
                Dodge = 12,
                Peach = 40,
                Wine = 18,
                Wuxie = 12,
                Resource = 18
            },
            Tags = new List<EnemyTag> { EnemyTag.RouteImperial },
            AiRules = new List<AiRule>
            {
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfHpBelow, Value = 150 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Peach, Delta = 60 }
                    }
                }
            }
        };
    }

    private static EnemyDefinition CreateGuanYu()
    {
        return new EnemyDefinition
        {
            Id = "guan_yu",
            Name = "关羽",
            NameKey = "enemy.guan_yu.name",
            MaxHP = 300,
            Type = EnemyType.Boss,
            UseSharedHealthPool = true,
            SkillIds = new List<string> { SkillIds.Wusheng, SkillIds.Yijue },
            EquipmentIds = new List<string> { EquipmentIds.QingLongYanYueDao },
            // 蜀汉共生体：同上，金币只由张飞一人结算。
            Reward = new EnemyReward { GrantsGold = false },
            RewardTier = RewardTier.Common,
            StageRange = new StageRange { MinStage = 20, MaxStage = 20 },
            StartingResource = 3,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Wine,
                    CardType.Unassailable,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 42,
                FireSlash = 50,
                ThunderSlash = 50,
                Dodge = 6,
                Wine = 14,
                Wuxie = 8,
                Resource = 12
            },
            Tags = new List<EnemyTag> { EnemyTag.RouteImperial },
            AiRules = new List<AiRule>
            {
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 4 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.FireSlash, Delta = 35 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 35 },
                        new() { WeightType = EnemyActionWeightType.Resource, Delta = -8 }
                    }
                }
            }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 皇家死侍（第三章皇宫路线精英怪，蜀）
    // 技能：影袭；装备：仁王 + 木牛流马；出现场景：精英关 3-5。
    // 专属AI：始终优先发动影袭，进入影袭时保留影袭杀所需的1费，3回合内等概率随机一回合出影袭杀。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateRoyalDeathGuard()
    {
        return new EnemyDefinition
        {
            Id = "royal_death_guard",
            Name = "皇家死侍",
            NameKey = "enemy.royal_death_guard.name",
            MaxHP = 70,
            Type = EnemyType.Elite,
            SkillIds = new List<string> { SkillIds.YingXi },
            EquipmentIds = new List<string>
            {
                EquipmentIds.BenevolentKing,
                EquipmentIds.MuNiuLiuMa
            },
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Rare,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 0,
                MaxStage = 0
            },
            // 1费用于发动影袭，另1费作为影袭杀的保留费用；运行时仍会在每次进入影袭时兜底补足。
            StartingResource = 2,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.Dodge,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights(),
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Shu, EnemyTag.RouteImperial }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 巨型脓包（第二章下水道普通敌人，群）
    // 技能：自爆；无装备；起始费用0。
    // 自爆：首次濒死时HP→1，下回合造成最大生命40%伤害（仅无懈、桃/酒可挡），之后继续战斗。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateGiantPusSac()
    {
        return new EnemyDefinition
        {
            Id = "giant_pus_sac",
            Name = "巨型脓包",
            NameKey = "enemy.giant_pus_sac.name",
            LoreId = "giant_pus_sac",
            MaxHP = 60,
            Type = EnemyType.Normal,
            SkillIds = new List<string> { SkillIds.ZiBao },
            EquipmentIds = new List<string>(),
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Common,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 8,
                MaxStage = 12
            },
            StartingResource = 0,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.Dodge,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 80,
                FireSlash = 35,
                Dodge = 8,
                Resource = 25
            },
            Tags = new List<EnemyTag> { EnemyTag.Qun, EnemyTag.RouteSewer }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 巨型机械蟑螂（第二章下水道 2-1/2-3，群，稀有）
    // HP=120；无技能；装备：毒药×2；奖励75金。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateGiantMechCockroach()
    {
        return new EnemyDefinition
        {
            Id = "giant_mech_cockroach",
            Name = "巨型机械蟑螂",
            NameKey = "enemy.giant_mech_cockroach.name",
            LoreId = "giant_mech_cockroach",
            MaxHP = 120,
            Type = EnemyType.Normal,
            SkillIds = new List<string>(),
            EquipmentIds = new List<string> { EquipmentIds.Poison, EquipmentIds.Poison },
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Rare,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 8,
                MaxStage = 10
            },
            StartingResource = 0,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Peach
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 40,
                Dodge = 25,
                Peach = 15,
                Wuxie = 10,
                Resource = 10
            },
            Tags = new List<EnemyTag> { EnemyTag.Qun, EnemyTag.RouteSewer }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 机械巨鼠（第二章下水道普通敌人，群，稀有）
    // HP=100；技能：瘟疫；奖励75金。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateGiantMechRat()
    {
        return new EnemyDefinition
        {
            Id = "giant_mech_rat",
            Name = "机械巨鼠",
            NameKey = "enemy.giant_mech_rat.name",
            LoreId = "giant_mech_rat",
            MaxHP = 100,
            Type = EnemyType.Normal,
            SkillIds = new List<string> { SkillIds.Plague },
            EquipmentIds = new List<string>(),
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Rare,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 8,
                MaxStage = 10
            },
            StartingResource = 0,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Peach
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 40,
                Dodge = 25,
                Peach = 15,
                Wuxie = 10,
                Resource = 10
            },
            Tags = new List<EnemyTag> { EnemyTag.Qun, EnemyTag.RouteSewer }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 黄衣之主（第二章下水道 Boss 2-8，群，传奇）
    // HP=170（+10导体）=180；技能：雷击+黄天；装备：导体+雷矛。
    // AI优先雷杀；奖励100金+攻击芯片×1+知识芯片×1（100%）。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateHuangYiZhiZhu()
    {
        return new EnemyDefinition
        {
            Id = "huang_yi_zhi_zhu",
            Name = "黄衣之主",
            NameKey = "enemy.huang_yi_zhi_zhu.name",
            LoreId = "huang_yi_zhi_zhu",
            MaxHP = 170,
            Type = EnemyType.Boss,
            SkillIds = new List<string> { SkillIds.LeiJi, SkillIds.HuangTian },
            EquipmentIds = new List<string> { EquipmentIds.Conductor, EquipmentIds.ThunderSpear },
            Reward = new EnemyReward
            {
                DefenseChipDropCount = 1,
                KnowledgeChipDropProbability = 1.0f
            },
            RewardTier = RewardTier.Legendary,
            UseSharedHealthPool = false,
            StageRange = new StageRange { MinStage = 15, MaxStage = 15 },
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Peach
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 15,
                ThunderSlash = 65,
                Dodge = 20,
                Peach = 15,
                Wuxie = 10,
                Resource = 15
            },
            Tags = new List<EnemyTag> { EnemyTag.Qun, EnemyTag.Boss, EnemyTag.RouteSewer },
            AiRules = new List<AiRule>
            {
                // 有费时强烈偏向雷杀
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 1 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 50 },
                        new() { WeightType = EnemyActionWeightType.Slash,        Delta = -10 }
                    }
                },
                // 玩家血量低时全力攻击
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.PlayerHpBelow, Value = 20 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 40 },
                        new() { WeightType = EnemyActionWeightType.Slash,        Delta = 20 }
                    }
                }
            }
        };
    }

    private static EnemyDefinition CreateZhangFei()
    {
        return new EnemyDefinition
        {
            Id = "zhang_fei",
            Name = "张飞",
            NameKey = "enemy.zhang_fei.name",
            MaxHP = 300,
            Type = EnemyType.Boss,
            UseSharedHealthPool = true,
            SkillIds = new List<string> { SkillIds.Paoxiao },
            EquipmentIds = new List<string> { EquipmentIds.ZhangBaSheMao },
            // 蜀汉共生体：三人中唯一保留 GrantsGold=true 的成员，按 Boss 档统一区间随机。
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Legendary,
            StageRange = new StageRange { MinStage = 20, MaxStage = 20 },
            StartingResource = 3,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Wine,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 58,
                FireSlash = 55,
                ThunderSlash = 55,
                Dodge = 3,
                Wine = 20,
                Resource = 10
            },
            Tags = new List<EnemyTag> { EnemyTag.RouteImperial },
            AiRules = new List<AiRule>
            {
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 2 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.FireSlash, Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.Resource, Delta = -6 }
                    }
                }
            }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 黄巾教徒（第二章下水道精英 2-5，群，史诗）
    // HP=100；技能：虔信；装备：黄道符+电击镣铐；初始费用2。
    // 每回合电击镣铐自伤1雷→虔信触发→+2亢奋+回5血，本回合减伤10%，攻击随亢奋叠乘×1.5^n。
    // 奖励：100金 + 随机掉落{黄道符, 电击镣铐}各选一。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateYellowTurbanDevotee()
    {
        return new EnemyDefinition
        {
            Id = "yellow_turban_devotee",
            Name = "黄巾教徒",
            NameKey = "enemy.yellow_turban_devotee.name",
            LoreId = "yellow_turban_devotee",
            MaxHP = 100,
            Type = EnemyType.Elite,
            SkillIds = new List<string> { SkillIds.QianXin },
            EquipmentIds = new List<string> { EquipmentIds.YellowTalisman, EquipmentIds.ElectricShackle },
            Reward = new EnemyReward
            {
                RandomEquipmentPool = new List<string>
                {
                    EquipmentIds.YellowTalisman,
                    EquipmentIds.ElectricShackle
                }
            },
            RewardTier = RewardTier.Epic,
            UseSharedHealthPool = false,
            StageRange = new StageRange { MinStage = 12, MaxStage = 12 },
            StartingResource = 2,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee,
                    CardType.Peach
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 45,
                Dodge = 20,
                Peach = 10,
                Wuxie = 10,
                Resource = 15
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun, EnemyTag.RouteSewer },
            AiRules = new List<AiRule>
            {
                // 亢奋叠满时强力进攻
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 2 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Slash,    Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.Resource, Delta = -10 }
                    }
                },
                // 低血量时优先防御
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfHpBelow, Value = 30 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Dodge,  Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.Peach,  Delta = 20 },
                        new() { WeightType = EnemyActionWeightType.Slash,  Delta = -20 }
                    }
                }
            }
        };
    }

    // ────────────────────────────────────────────────────────────────────────────
    // 鼠王（第二章精英关2-5，群，传奇）
    // HP=300；技能：瘟疫；装备：瘟疫权杖+暴虐皇冠。
    // 初始费用1；奖励400金；不共享HP池。
    // AI：默认AI，优先选择【杀】（进化为毒杀）。
    // ────────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateRatKing()
    {
        return new EnemyDefinition
        {
            Id = "rat_king",
            Name = "鼠王",
            NameKey = "enemy.rat_king.name",
            LoreId = "rat_king",
            MaxHP = 300,
            Type = EnemyType.Boss,
            SkillIds = new List<string> { SkillIds.Plague },
            EquipmentIds = new List<string> { EquipmentIds.PlagueStaff, EquipmentIds.TyrantCrown },
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Legendary,
            UseSharedHealthPool = false,
            StageRange = new StageRange
            {
                MinStage = 11,
                MaxStage = 11
            },
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.Kill,
                    CardType.Kill,
                    CardType.Dodge,
                    CardType.Fee,
                    CardType.Peach
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 60,
                Dodge = 20,
                Peach = 10,
                Resource = 10
            },
            Tags = new List<EnemyTag> { EnemyTag.Qun, EnemyTag.Boss },
            AiRules = new List<AiRule>
            {
                // 有费用时优先出杀
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfResourceAtLeast, Value = 1 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = 40 },
                        new() { WeightType = EnemyActionWeightType.Resource, Delta = -10 }
                    }
                },
                // 低血量时补桃自保
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfHpBelow, Value = 80 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Peach, Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.Dodge, Delta = 15 }
                    }
                }
            }
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 第四章 Boss：幻象触手（2000HP，群，传奇）
    // 技能：幻象（传奇）+ 粘液（稀有）
    // AI：防御模式（闪/无懈/桃）↔ 攻击模式（杀/雷杀），根据伤害结果自动切换
    // ──────────────────────────────────────────────────────────────────────────
    private static EnemyDefinition CreateHuanXiangChuShou()
    {
        return new EnemyDefinition
        {
            Id = "huan_xiang_chu_shou",
            Name = "幻象触手",
            NameKey = "enemy.huan_xiang_chu_shou.name",
            MaxHP = 2000,
            Type = EnemyType.Boss,
            SkillIds = new List<string> { SkillIds.Illusion, SkillIds.Slime },
            EquipmentIds = new List<string>
            {
                EquipmentIds.AttackChip,
                EquipmentIds.AttackChip,
                EquipmentIds.AttackChip,
                EquipmentIds.AttackChip,
                EquipmentIds.AttackChip
            },
            Reward = new EnemyReward { GoldOverride = EnemyRewardConfig.HuanXiangChuShouGold },
            RewardTier = RewardTier.Legendary,
            UseSharedHealthPool = false,
            StageRange = new StageRange { MinStage = 32, MaxStage = 32 },
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Peach,
                    CardType.Wine,
                    CardType.Steal,
                    CardType.Unassailable,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 15,
                ThunderSlash = 15,
                Dodge = 60,
                Wuxie = 50,
                Peach = 25,
                Resource = 20
            },
            Tags = new List<EnemyTag> { EnemyTag.Qun, EnemyTag.Boss },
            LoreId = "huan_xiang_chu_shou"
        };
    }

    // 训练傀儡：仅供新手教程使用的专属敌人。StageRange 设为 0-0，
    // 与 giant_rolling_stone 等特殊敌人一致，不会出现在正常关卡生成中。
    // 出牌顺序在教学环节由 BattleManager.Tutorial.cs 的
    // SetTutorialEnemyAction 逐回合强制指定；综合练习环节则交还给下方
    // ActionWeights 做费/杀/闪三选一的随机决策。
    private static EnemyDefinition CreateTrainingDummy()
    {
        return new EnemyDefinition
        {
            Id = "training_dummy",
            Name = "训练傀儡",
            NameKey = "tutorial.enemy_name",
            // 教学环节累计会造成多次伤害；100 HP 可保证训练傀儡不会在卡牌讲解
            // 尚未结束前提前死亡，最终综合练习也使用相同的生命上限。
            MaxHP = 100,
            Type = EnemyType.Normal,
            // 教学训练傀儡：不是真实战斗，不发放金币。
            Reward = new EnemyReward { GrantsGold = false },
            RewardTier = RewardTier.Common,
            UseSharedHealthPool = false,
            StageRange = new StageRange { MinStage = 0, MaxStage = 0 },
            StartingResource = 0,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Fee,
                    CardType.Dodge,
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Peach,
                    CardType.Wine,
                    CardType.Steal,
                    CardType.Unassailable
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 100,
                Dodge = 100,
                Resource = 100
            },
            LoreId = string.Empty
        };
    }

    // ————————————————————————————————————————————————————————————
    // 第四章·深渊：普通敌人
    // ————————————————————————————————————————————————————————————

    // 侦测者：警戒单位。战斗开始进入警戒状态（前3回合只出费），本场战斗第一次产生
    // 任意伤害（无论来源、含自身受到伤害）立即解除；若前3回合始终无伤害，第4回合自动解除。
    // 警戒状态机由 EnemyAI.cs 的 SelectScoutAction/scout_vigilant 相关逻辑驱动，这里只声明
    // 基础数值；解除警戒后的默认出牌权重也在这里配置。
    private static EnemyDefinition CreateScout()
    {
        return new EnemyDefinition
        {
            Id = "scout",
            Name = "侦测者",
            NameKey = "enemy.scout.name",
            MaxHP = 140,
            Type = EnemyType.Normal,
            SkillIds = new List<string> { SkillIds.Qianxun },
            EquipmentIds = new List<string> { EquipmentIds.ForgottenStone },
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Common,
            UseSharedHealthPool = false,
            StageRange = new StageRange { MinStage = 25, MaxStage = 30 },
            StartingResource = 3,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 20,
                FireSlash = 10,
                ThunderSlash = 25,
                Dodge = 40,
                Wuxie = 20,
                Resource = 25
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun }
        };
    }

    // 巨口：疯狂撕咬、持续吸血。必中杀最高优先级；连续2回合未造成伤害则下回合大幅提权攻击；
    // HP<50%时进一步提权、降低防守；吸血之牙本回合未触发时优先主动攻击。后两条依赖自定义
    // RuntimeStates 计数器，由 EnemyAI.cs 的 SelectJuKouAction 在这套基础权重之上叠加。
    private static EnemyDefinition CreateJuKou()
    {
        return new EnemyDefinition
        {
            Id = "ju_kou",
            Name = "巨口",
            NameKey = "enemy.ju_kou.name",
            MaxHP = 200,
            Type = EnemyType.Normal,
            EquipmentIds = new List<string>
            {
                EquipmentIds.VampiricFang,
                EquipmentIds.AttackChip,
                EquipmentIds.AttackChip,
                EquipmentIds.AttackChip,
                EquipmentIds.AttackChip,
                EquipmentIds.AttackChip
            },
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Common,
            UseSharedHealthPool = false,
            StageRange = new StageRange { MinStage = 25, MaxStage = 30 },
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.SureKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 40,
                FireSlash = 20,
                ThunderSlash = 20,
                SureSlash = 60,
                Dodge = 15,
                Wuxie = 10,
                Resource = 20
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun },
            AiRules = new List<AiRule>
            {
                // HP<50%（100/200）：进一步提高攻击牌权重，降低防守倾向。
                new()
                {
                    Conditions = new List<AiCondition>
                    {
                        new() { Type = AiConditionType.SelfHpBelow, Value = 100 }
                    },
                    Modifiers = new List<AiWeightModifier>
                    {
                        new() { WeightType = EnemyActionWeightType.Slash, Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.FireSlash, Delta = 20 },
                        new() { WeightType = EnemyActionWeightType.ThunderSlash, Delta = 20 },
                        new() { WeightType = EnemyActionWeightType.SureSlash, Delta = 30 },
                        new() { WeightType = EnemyActionWeightType.Dodge, Delta = -10 }
                    }
                }
            }
        };
    }

    // 深渊共生体：高生命值、高压制力的前排坦克。使用默认AI逻辑，不加自定义 AiRules。
    // 关卡里会一次性放出3个（同一个 EnemyId 出现3次），配合 UseSharedHealthPool=true
    // 自动共享同一条血条（参照"蜀汉共生体"三人共享血量的既有机制）。
    private static EnemyDefinition CreateAbyssSymbiote()
    {
        return new EnemyDefinition
        {
            Id = "abyss_symbiote",
            Name = "深渊共生体",
            NameKey = "enemy.abyss_symbiote.name",
            MaxHP = 450,
            Type = EnemyType.Normal,
            // 深渊共生体沿用董卓专属【崩坏】的同一套回合规则，不能再使用已删除的旧版泛用定义。
            SkillIds = new List<string> { SkillIds.DongZhuoBengHuai, SkillIds.JiAng },
            EquipmentIds = new List<string> { EquipmentIds.GuDingDao },
            Reward = new EnemyReward(),
            RewardTier = RewardTier.Common,
            UseSharedHealthPool = true,
            StageRange = new StageRange { MinStage = 25, MaxStage = 30 },
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = new List<CardType>
                {
                    CardType.Kill,
                    CardType.FireKill,
                    CardType.ThunderKill,
                    CardType.Dodge,
                    CardType.Unassailable,
                    CardType.Fee
                }
            },
            ActionWeights = new EnemyActionWeights
            {
                Slash = 40,
                FireSlash = 20,
                ThunderSlash = 20,
                Dodge = 30,
                Wuxie = 15,
                Resource = 25
            },
            Tags = new List<EnemyTag> { EnemyTag.Human, EnemyTag.Qun }
        };
    }
}
