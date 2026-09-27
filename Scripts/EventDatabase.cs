//////////////////////////////////////////////////////////
// 文件：Scripts/EventDatabase.cs
//
// 模块：Event System
//
// 职责：
// 1. 承载地图事件、事件选项与事件奖励相关代码。
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
/// Event System 的公开类：EventDatabase。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class EventDatabase
{
    private static readonly IReadOnlyList<EventData> AllEvents = new EventData[]
    {
        EventFactory.CreateChangbanpoEvent(),
        EventFactory.CreateXiandanShushiEvent(),
        EventFactory.CreateTavernEvent(),
        EventFactory.CreateGeneralShopEvent(),
        EventFactory.CreateGiantStoneSteleEvent(),
        EventFactory.CreateTreasurePavilionEvent(),
        EventFactory.CreateModificationShopEvent(),
        EventFactory.CreateAncientPhantomEvent(),
        EventFactory.CreateAbandonedLabEvent(),
        EventFactory.CreateShortCircuitMemoryEvent(),
        EventFactory.CreateTalkingSwordEvent(),
        EventFactory.CreateBloodPoolEvent(),
        EventFactory.CreateChibiRuinsEvent(),
        EventFactory.CreateGreedyVaultEvent(),
        EventFactory.CreateRulerStatueEvent(),
        EventFactory.CreateLifeSpringEvent(),
        EventFactory.CreateWitchEvent(),
        EventFactory.CreateVehicleShopEvent(),
        EventFactory.CreateManZuCampEvent(),
        EventFactory.CreateAncientForgeEvent(),
        EventFactory.CreateForbiddenLibraryEvent(),
        EventFactory.CreateQiXingTanEvent(),
        EventFactory.CreateHanShiZongCiEvent(),
        EventFactory.CreateImprisonedElfEvent(),
        EventFactory.CreateRatKingEventData(),
        EventFactory.CreateChasingPursuersEvent(),
        EventFactory.CreateStinkyMushroomEventData(),
        EventFactory.CreateExplosiveFruitEvent(),
        EventFactory.CreateAbyssCallEvent(),
        EventFactory.CreateElementalAltarEvent(),
        EventFactory.CreateTutorialRoadsideSupplyEvent()
    };

    /// <summary>
    /// Event System 的公开入口：GetAllEvents。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static IReadOnlyList<EventData> GetAllEvents()
    {
        return AllEvents;
    }

    /// <summary>
    /// Event System 的公开入口：GetEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData? GetEvent(string eventId)
    {
        foreach (var eventData in AllEvents)
        {
            if (eventData.Id == eventId)
            {
                return eventData;
            }
        }

        return null;
    }
}

internal static class EventFactory
{
    /// <summary>
    /// Event System 的公开入口：CreateChangbanpoEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateChangbanpoEvent()
    {
        return new EventData
        {
            Id = "changbanpo",
            AssetCode = "EV111001",
            Name = "长坂坡",
            Description = "长坂坡现在已经成为了一个巨大的荒冢。\n白骨遍地，四处散落着各种不值钱的废物。",
            NameKey = "event.changbanpo.name",
            DescriptionKey = "event.changbanpo.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            EventTags = new List<string> { "History", "ZhaoYun", "Equipment", "Gold" },
            // 赵云路线必须在第一章遇到长坂坡；仍会遵循章节、一次性等通用条件。
            GuaranteedForCharacterId = CharacterIds.ZhaoYun,
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            // 仅第一章出现；RunOnce 同时保证每局只触发一次。
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 1
                }
            },
            Weight = 30,
            Options = new List<EventOption>
            {
                // ① 翻找武器：分别随机获得一件普通武器与一件普通护甲。
                new()
                {
                    Name = "翻找武器",
                    NameKey = "event.changbanpo.option1.name",
                    Description = "在荒冢中翻找可用残骸，获得一件普通品质武器和一件普通品质护甲。",
                    DescriptionKey = "event.changbanpo.option1.desc",
                    RewardSequence =
                    {
                        new RandomEquipmentByCategoryAndRarityRewardAction(
                            EquipmentSlotCategory.Weapon,
                            EquipmentRarity.Common),
                        new RandomEquipmentByCategoryAndRarityRewardAction(
                            EquipmentSlotCategory.Armor,
                            EquipmentRarity.Common)
                    }
                },
                // ② 拼凑尸体：100 金币
                new()
                {
                    Name = "拼凑尸体",
                    NameKey = "event.changbanpo.option2.name",
                    Description = "搜刮战场遗留的财物，获得100金币。",
                    DescriptionKey = "event.changbanpo.option2.desc",
                    RewardSequence =
                    {
                        new AddGoldRewardAction(100)
                    },
                    ResultText = "你从尸骸与遗物中搜集到了100金币。",
                    ResultTextKey = "event.changbanpo.option2.result"
                },
                // ③ 吸收记忆：防御芯片 ×1
                new()
                {
                    Name = "吸收记忆",
                    NameKey = "event.changbanpo.option3.name",
                    Description = "从残留的战场意志中提炼信息，获得一个防御芯片。",
                    DescriptionKey = "event.changbanpo.option3.desc",
                    RewardSequence =
                    {
                        new AddChipRewardAction(RewardChipType.Defense)
                    }
                },
                // ④（赵云专属）搜寻古井：青钢剑 + 神秘芯片
                new()
                {
                    Name = "搜寻古井",
                    NameKey = "event.changbanpo.option4.name",
                    Description = "探索废墟深处的一口古井，或许有特别的发现。",
                    DescriptionKey = "event.changbanpo.option4.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.CurrentCharacter,
                            StringValue = CharacterIds.ZhaoYun
                        }
                    },
                    RewardSequence =
                    {
                        new AddEquipmentRewardAction(EquipmentIds.BlueSteelSword),
                        new AddEquipmentRewardAction(EquipmentIds.MysteriousChip)
                    },
                    ResultText = "你在一口被遗忘的古井中发现了赵云遗失的兵器。\n同时还找到了一枚来历不明的神秘芯片。",
                    ResultTextKey = "event.changbanpo.option4.result"
                },
                // ⑤（蜀国专属）悼念：知识芯片 ×1
                new()
                {
                    Name = "悼念",
                    NameKey = "event.changbanpo.option5.name",
                    Description = "为曾在此战死的蜀国将士默哀，获得一个知识芯片。",
                    DescriptionKey = "event.changbanpo.option5.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.CurrentCharacterFaction,
                            StringValue = "Shu"
                        }
                    },
                    RewardSequence =
                    {
                        new AddChipRewardAction(RewardChipType.Knowledge)
                    },
                    ResultText = "灵魂共鸣——知识芯片+1。",
                    ResultTextKey = "event.changbanpo.option5.result"
                },
                // ⑥（魏国专属）悼念：攻击芯片 ×1
                new()
                {
                    Name = "悼念",
                    NameKey = "event.changbanpo.option6.name",
                    Description = "为曾在此战死的魏国将士默哀，获得一个攻击芯片。",
                    DescriptionKey = "event.changbanpo.option6.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.CurrentCharacterFaction,
                            StringValue = "Wei"
                        }
                    },
                    RewardSequence =
                    {
                        new AddChipRewardAction(RewardChipType.Attack)
                    },
                    ResultText = "残留的武器芯片依然可用——攻击芯片+1。",
                    ResultTextKey = "event.changbanpo.option6.result"
                }
            }
        };
    }

    /// <summary>
    /// Event System 的公开入口：CreateXiandanShushiEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateXiandanShushiEvent()
    {
        return new EventData
        {
            Id = "xiandan_shushi",
            AssetCode = "EV111002",
            Name = "仙丹术士",
            Description = "一位瘦骨嶙峋的老者手里攥着巨大的葫芦。\n他望着你，仿佛正在等待你的选择。",
            NameKey = "event.xiandan_shushi.name",
            DescriptionKey = "event.xiandan_shushi.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            EventTags = new List<string> { "Chapter1", "Life", "Equipment", "Growth" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                // StageRequirement 只描述章节内阶段，不能代替章节归属；显式限制后，
                // 固定流程和自由探索都会通过 EventManager.CanUseEvent 共享同一判断。
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 1
                }
            },
            Weight = 30,
            Options = new List<EventOption>
            {
                new()
                {
                    Name = "求药",
                    NameKey = "event.xiandan_shushi.option1.name",
                    Description = "消耗100金币，永久获得10点最大生命值（金币不足100时无法选择）。",
                    DescriptionKey = "event.xiandan_shushi.option1.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Gold, Amount = 100 }
                    },
                    Rewards = new List<EventReward>
                    {
                        // 先提升上限再回复，保证“当前生命值同步+10”在任意当前生命值下都成立。
                        new() { Type = EventRewardType.MaxHp, Amount = 10 },
                        new() { Type = EventRewardType.Heal, Amount = 10 }
                    }
                },
                new()
                {
                    Name = "问路",
                    NameKey = "event.xiandan_shushi.option2.name",
                    Description = "下一场战斗胜利后，随机获得一件装备。",
                    DescriptionKey = "event.xiandan_shushi.option2.desc",
                    ResultText = "你将在下一场胜利后获得一件随机装备。",
                    ResultTextKey = "event.xiandan_shushi.option2.result",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.DeferredEquipmentAfterNextBattle, StringValue = "Common,Rare" }
                    }
                },
                new()
                {
                    Name = "解惑",
                    NameKey = "event.xiandan_shushi.option3.name",
                    Description = "击败第一章Boss后，获得装备【丹】。",
                    DescriptionKey = "event.xiandan_shushi.option3.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.DeferredEquipmentAfterChapterBoss, StringValue = EquipmentIds.Dan, Amount = 1 }
                    }
                },
                new()
                {
                    Name = "寻道",
                    NameKey = "event.xiandan_shushi.option4.name",
                    Description = "消耗150金币，获得装备【神秘芯片】（金币不足150时无法选择）。",
                    DescriptionKey = "event.xiandan_shushi.option4.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Gold, Amount = 150 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Equipment, StringValue = EquipmentIds.MysteriousChip }
                    },
                    ResultText = "老者将手中的葫芦交到你手上。\n\n获得【神秘芯片】。",
                    ResultTextKey = "event.xiandan_shushi.option4.result"
                }
            }
        };
    }

    /// <summary>
    /// Event System 的公开入口：CreateTavernEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateTavernEvent()
    {
        return new EventData
        {
            Id = "tavern",
            AssetCode = "EV211001",
            Name = "酒馆",
            Description = "在繁华的市井里，一座人来人往的小酒馆。鱼龙混杂之地。",
            NameKey = "event.tavern.name",
            DescriptionKey = "event.tavern.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new() { Type = EventConditionType.CurrentChapter, Comparison = EventComparison.GreaterOrEqual, IntValue = 2 }
            },
            Weight = 30,
            Options = new List<EventOption>
            {
                new()
                {
                    Name = "休息",
                    NameKey = "event.tavern.option1.name",
                    Description = "粮草 +1。",
                    DescriptionKey = "event.tavern.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Forage, Amount = 1 }
                    }
                },
                new()
                {
                    // 设计标注：条件 金币>=75。当前事件系统暂无选项级前置条件强制校验字段，此处仅作数据记录。
                    Name = "点杯酒",
                    NameKey = "event.tavern.option2.name",
                    Description = "条件：金币≥75。消耗75金币，本局永久生效：每场战斗濒死时可以免费使用酒进行救援（原本需要花费1费）。",
                    DescriptionKey = "event.tavern.option2.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Gold, Amount = 75 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.FreeWineRevive }
                    },
                    ResultText = "你一饮而尽，只觉得浑身滚烫。\n\n本局往后每场战斗濒死时，都可以免费饮酒续命。",
                    ResultTextKey = "event.tavern.option2.result"
                },
                new()
                {
                    Name = "黑市交易",
                    NameKey = "event.tavern.option3.name",
                    Description = "进入黑市：装备品质额外获得10%出现传奇装备的概率，但所有商品价格上涨50%。",
                    DescriptionKey = "event.tavern.option3.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.OpenBlackMarketShop }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Event System 的公开入口：CreateWitchEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateWitchEvent()
    {
        return new EventData
        {
            Id = "witch",
            AssetCode = "EV221001",
            Name = "巫婆",
            Description = "昏暗的下水道深处，一位披着黑袍的老妇人正守着一口沸腾的大锅。\n她缓缓抬起头，露出诡异的笑容。\n\n「年轻人，你是来寻找未来……还是寻找财富？」",
            NameKey = "event.witch.name",
            DescriptionKey = "event.witch.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 2
                },
                new()
                {
                    Type = EventConditionType.CurrentChapterRoute,
                    Comparison = EventComparison.Equal,
                    StringValue = ChapterRoute.Sewer.ToString()
                }
            },
            Weight = 24,
            Options = new List<EventOption>
            {
                new()
                {
                    Name = "触摸水晶球，预知未来",
                    NameKey = "event.witch.option1.name",
                    Description = "花费200金币，随机立即触发一个第三章非商店事件。",
                    DescriptionKey = "event.witch.option1.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Gold, Amount = 200 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.TriggerRandomChapterNonShopEvent, Amount = 3 }
                    }
                },
                new()
                {
                    Name = "看看她在卖点什么",
                    NameKey = "event.witch.option2.name",
                    Description = "进入【巫婆的商店】。",
                    DescriptionKey = "event.witch.option2.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.OpenWitchShop }
                    }
                },
                new()
                {
                    Name = "一拳干翻她",
                    NameKey = "event.witch.option3.name",
                    Description = "立即获得【巫婆的头皮】。",
                    DescriptionKey = "event.witch.option3.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Equipment, StringValue = EquipmentIds.WitchScalp }
                    },
                    ResultText = "你没有给她继续说话的机会。\n你一拳干翻了巫婆，顺手扯下了她的头皮。\n获得：【巫婆的头皮】",
                    ResultTextKey = "event.witch.option3.result"
                }
            }
        };
    }

    // ————————————————————————————————————————————————————————————
    // 巨大石碑：第二章 Epic 单次事件，四选项（力量/生命/技能芯片/全部锈类装备进阶）。
    // ————————————————————————————————————————————————————————————
    /// <summary>
    /// Event System 的公开入口：CreateGiantStoneSteleEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateGiantStoneSteleEvent()
    {
        return new EventData
        {
            Id = "giant_stone_stele",
            AssetCode = "EV213001",
            Name = "巨大石碑",
            Description = "里面夹杂着钢铁，骸骨。\n繁荣的象征。",
            NameKey = "event.giant_stone_stele.name",
            DescriptionKey = "event.giant_stone_stele.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Epic,
            EventTags = new List<string> { "Stele", "Power", "Blood", "Chapter2" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            // 仅第二章出现；RunOnce 保证每局只触发一次。
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 2
                }
            },
            Weight = 20,
            Options = new List<EventOption>
            {
                // 选项一：100金币 → 攻击锦囊伤害 +10
                new()
                {
                    Name = "供奉金钱，获得力量",
                    NameKey = "event.giant_stone_stele.option1.name",
                    Description = "花费100金币，所有攻击性锦囊牌伤害永久+10。（需 金币≥100）",
                    DescriptionKey = "event.giant_stone_stele.option1.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Gold, Amount = 100 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.AttackTrickDamageBonus, Amount = 10 }
                    },
                    ResultText = "石碑吐出了一丝灵气。\n像电流一样传遍了你的全身。\n你的攻击性锦囊牌变得更加危险了。",
                    ResultTextKey = "event.giant_stone_stele.option1.result"
                },
                // 选项二：100金币 → MaxHP+25
                new()
                {
                    Name = "供奉金钱，获得生命",
                    NameKey = "event.giant_stone_stele.option2.name",
                    Description = "花费100金币，获得25点生命上限。（需 金币≥100）",
                    DescriptionKey = "event.giant_stone_stele.option2.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Gold, Amount = 100 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.MaxHp, Amount = 25 }
                    },
                    ResultText = "石碑吐出了一丝灵气。\n像电流一样传遍了你的全身。\n你的生命力得到了强化。",
                    ResultTextKey = "event.giant_stone_stele.option2.result"
                },
                // 选项三：失去50点最大生命，获得技能芯片。
                new()
                {
                    Name = "献上鲜血",
                    NameKey = "event.giant_stone_stele.option3.name",
                    Description = "失去50点生命上限，获得一个【技能芯片】。（需 最大生命≥51）",
                    DescriptionKey = "event.giant_stone_stele.option3.desc",
                    Conditions = new List<EventCondition>
                    {
                        new() { Type = EventConditionType.MaxHP, Comparison = EventComparison.GreaterOrEqual, IntValue = 51 }
                    },
                    RewardSequence =
                    {
                        new LoseMaxHpRewardAction(50),
                        new AddEquipmentRewardAction(EquipmentIds.SkillChip)
                    }
                },
                // 选项四：磨刀（需持有任意锈类装备）
                new()
                {
                    Name = "拿石碑磨刀",
                    NameKey = "event.giant_stone_stele.option4.name",
                    Description = "使用锈类装备在石碑上磨砺。所有持有的锈类装备都会变为进阶版：锈盾/锈剑/锈矛/锈青钢剑/锈古锭刀。",
                    DescriptionKey = "event.giant_stone_stele.option4.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            // “持有”同时包含背包与已装备物品；升级逻辑也会处理两者。
                            Type = EventConditionType.OwnsAnyEquipmentInPool,
                            StringValue = $"{EquipmentIds.RustShield},{EquipmentIds.RustSword},{EquipmentIds.RustSpear},{EquipmentIds.RustBlueSteelSword},{EquipmentIds.RustGuDingDao}"
                        }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.FutureRustWeaponUpgrade }
                    },
                    ResultText = "石碑吞没了所有锈类装备，锈迹逐渐脱落，进阶后的装备回到了你的手中。",
                    ResultTextKey = "event.giant_stone_stele.option4.result"
                }
            }
        };
    }

    // ————————————————————————————————————————————————————————————
    // 改造铺：第一至第三章 Common 每章一次事件，提供芯片选择、零件金币与武器重铸。
    // ————————————————————————————————————————————————————————————
    /// <summary>
    /// Event System 的公开入口：CreateModificationShopEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateModificationShopEvent()
    {
        return new EventData
        {
            Id = "modification_shop",
            AssetCode = "EV111007",
            Name = "改造铺",
            Description = "里面塞满了各种肢体。\n你几乎无从下脚。",
            NameKey = "event.modification_shop.name",
            DescriptionKey = "event.modification_shop.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.OncePerChapter,
            IsRepeatable = true,
            Rarity = EventRarity.Common,
            EventTags = new List<string> { "Chip", "Upgrade", "AllChapters" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.GreaterOrEqual,
                    IntValue = 1
                },
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.LessOrEqual,
                    IntValue = 3
                }
            },
            Weight = 25,
            Options = new List<EventOption>
            {
                // 选项一：支付75金币后进入统一芯片三选一。
                new()
                {
                    Name = "接受改造",
                    NameKey = "event.modification_shop.option1.name",
                    Description = "花费75金币，获得一次芯片三选一机会。",
                    DescriptionKey = "event.modification_shop.option1.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Gold, Amount = 75 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.ChooseOneChipOnce }
                    }
                },
                // 选项二：从零件中获得100至125金币。
                new()
                {
                    Name = "没钱了——顺几个零件走",
                    NameKey = "event.modification_shop.option2.name",
                    Description = "获得100至125金币。",
                    DescriptionKey = "event.modification_shop.option2.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.RandomGold, Amount = 100, MaxAmount = 125 }
                    }
                },
                // 选项三：支付75金币后，从当前拥有的武器中随机展示至多三件进行重铸。
                new()
                {
                    Name = "改造武器",
                    NameKey = "event.modification_shop.option3.name",
                    Description = "花费75金币，随机展示背包与已装备武器中的三件，选择一件进行重铸。",
                    DescriptionKey = "event.modification_shop.option3.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Gold, Amount = 75 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.ModificationShopWeaponReforge }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Event System 的公开入口：CreateTreasurePavilionEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateTreasurePavilionEvent()
    {
        return new EventData
        {
            Id = "treasure_pavilion",
            AssetCode = "EV212001",
            Name = "藏宝阁",
            Description = "一座隐藏在废墟深处的藏宝阁。\n厚重的钢铁大门已经被撞开。\n里面似乎存放着大量珍贵装备。\n但守卫宝藏的巨物仍然在活动。",
            NameKey = "event.treasure_pavilion.name",
            DescriptionKey = "event.treasure_pavilion.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Rare,
            EventTags = new List<string> { "Treasure", "EpicEquipment", "SpecialBattle", "Chapter2" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 2
                }
            },
            Weight = 20,
            Options = new List<EventOption>
            {
                new()
                {
                    Name = "进入",
                    NameKey = "event.treasure_pavilion.option1.name",
                    Description = "进入特殊战斗。胜利后从3件随机史诗装备中选择1件。",
                    DescriptionKey = "event.treasure_pavilion.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.StartTreasurePavilionBattle }
                    }
                },
                new()
                {
                    Name = "退出",
                    NameKey = "event.treasure_pavilion.option2.name",
                    Description = "获得50金币，离开藏宝阁。",
                    DescriptionKey = "event.treasure_pavilion.option2.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Gold, Amount = 50 }
                    },
                    ResultText = "你没有惊动藏宝阁深处的巨物。\n你带走了门口散落的50金币。",
                    ResultTextKey = "event.treasure_pavilion.option2.result"
                },
                new()
                {
                    Name = "灵魂献祭",
                    NameKey = "event.treasure_pavilion.option3.name",
                    Description = "拥有灵魂石时可见。不消耗灵魂石，直接获得史诗装备三选一与200金币。",
                    DescriptionKey = "event.treasure_pavilion.option3.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.OwnsEquipment,
                            StringValue = EquipmentIds.SoulStone
                        }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.ChooseOneEpicEquipment, Amount = 200 }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Event System 的公开入口：CreateGeneralShopEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateGeneralShopEvent()
    {
        return new EventData
        {
            Id = "shop_general",
            AssetCode = "EV212002",
            Name = "商店",
            Description = "就是一家普通的商店。",
            NameKey = "event.shop_general.name",
            DescriptionKey = "event.shop_general.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            // 这是随机事件节点中的单局一次商店事件，只进入第二、三章事件池；
            // 各章地图上的固定商店节点仍由 MapNodeType.Shop 独立处理。
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.GreaterOrEqual,
                    IntValue = 2
                },
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.LessOrEqual,
                    IntValue = 3
                }
            },
            Weight = 30,
            Options = new List<EventOption>
            {
                new()
                {
                    Name = "进入商店",
                    NameKey = "event.shop_general.option1.name",
                    Description = "获得75金币，然后进入商店。",
                    DescriptionKey = "event.shop_general.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Gold, Amount = 75 },
                        new() { Type = EventRewardType.OpenShopUI }
                    },
                    ResultText = "你获得了75金币，随后进入商店。",
                    ResultTextKey = "event.shop_general.option1.result"
                },
                new()
                {
                    Name = "打劫",
                    NameKey = "event.shop_general.option2.name",
                    Description = "失去10点电量，获得225金币。（需 电量≥10）",
                    DescriptionKey = "event.shop_general.option2.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Power, Amount = 10 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Gold, Amount = 225 }
                    },
                    ResultText = "你闯入商店，横扫了收银台。\n失去10点电量，获得225金币。",
                    ResultTextKey = "event.shop_general.option2.result"
                }
            }
        };
    }

    /// <summary>
    /// Event System 的公开入口：CreateAncientPhantomEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateAncientPhantomEvent()
    {
        return new EventData
        {
            Id = "ancient_phantom",
            AssetCode = "EV122001",
            Name = "旧日虚影",
            Description = "一道模糊的轮廓出现在你面前。\n它没有面孔，没有声音，却能让你感受到某种遥远的意志。\n\n它似乎在引导你前往某个地方。",
            NameKey = "event.ancient_phantom.name",
            DescriptionKey = "event.ancient_phantom.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Rare,
            EventTags = new List<string> { "Route", "Mysterious" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 1
                }
            },
            Weight = 15,
            Options = new List<EventOption>
            {
                new()
                {
                    Name = "跟随指引",
                    NameKey = "event.ancient_phantom.option1.name",
                    Description = "任凭虚影引导，踏入未知的深处。\n最大生命+10，当前生命+10，下一章进入下水道路线，并获得【黑暗】。",
                    DescriptionKey = "event.ancient_phantom.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.MaxHp, Amount = 10 },
                        new() { Type = EventRewardType.Heal, Amount = 10 },
                        new() { Type = EventRewardType.SetChapterRoute, Amount = (int)ChapterRoute.Sewer },
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.Darkness, Amount = 1 }
                    },
                    ResultText = "虚影消散了。\n你感到身体充满了力量，心中浮现出一条陌生的路。\n黑暗缠绕在你的四周，下一场战斗中的敌人将更加强大。",
                    ResultTextKey = "event.ancient_phantom.option1.result"
                },
                new()
                {
                    Name = "坚定意志",
                    NameKey = "event.ancient_phantom.option2.name",
                    Description = "不为所动，静观其变。\n获得【灵魂石】。",
                    DescriptionKey = "event.ancient_phantom.option2.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Equipment, StringValue = EquipmentIds.SoulStone }
                    },
                    ResultText = "虚影凝固成一块石头，落入你的掌心。\n它散发着微弱的光，好像在等待某个时机。",
                    ResultTextKey = "event.ancient_phantom.option2.result"
                },
                new()
                {
                    Name = "试图利用（生命≤30）",
                    NameKey = "event.ancient_phantom.option3.name",
                    Description = "趁着身体虚弱，主动与虚影共鸣，汲取其力量。\n最大生命+10，当前生命+10，获得【灵魂石】。",
                    DescriptionKey = "event.ancient_phantom.option3.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.CurrentHP,
                            Comparison = EventComparison.LessOrEqual,
                            IntValue = 30
                        }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.MaxHp, Amount = 10 },
                        new() { Type = EventRewardType.Heal, Amount = 10 },
                        new() { Type = EventRewardType.Equipment, StringValue = EquipmentIds.SoulStone }
                    },
                    ResultText = "痛苦与力量交织在一起。\n虚影与你融为一体，留下了一块温热的石头。",
                    ResultTextKey = "event.ancient_phantom.option3.result"
                }
            }
        };
    }

    /// <summary>
    /// Event System 的公开入口：CreateRulerStatueEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateRulerStatueEvent()
    {
        return new EventData
        {
            Id = "ruler_statue",
            AssetCode = "EV211002",
            Name = "统治者雕像",
            Description = "这个城市的统治者是【{chapter2_boss_name}】。\n雕像记录着这位统治者为城市做出的贡献。\n又或者是他留下的剥削与压迫。\n时间早已让石碑模糊不清。\n但雕像内部似乎仍在运转。",
            NameKey = "event.ruler_statue.name",
            DescriptionKey = "event.ruler_statue.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            EventTags = new List<string> { "Chapter2", "Equipment", "Statue" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 2
                }
            },
            Weight = 30,
            Options = new List<EventOption>
            {
                new()
                {
                    Name = "摧毁雕像",
                    NameKey = "event.ruler_statue.option1.name",
                    Description = "你挥动武器砸碎了雕像。大量恶心的虫子从雕像底部涌出。当一切归于平静后，你在残骸中找到了一块奇怪的石头。",
                    DescriptionKey = "event.ruler_statue.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Equipment, StringValue = EquipmentIds.ForgottenStone }
                    },
                    ResultText = "雕像彻底崩塌。\n你在残骸中找到了一块奇怪的石头。\n获得【遗忘之石】。",
                    ResultTextKey = "event.ruler_statue.option1.result"
                },
                new()
                {
                    Name = "窃走雕像核心",
                    NameKey = "event.ruler_statue.option2.name",
                    Description = "你拆开雕像外壳，取出了其中不断旋转的核心。核心似乎一直在低语。它告诉你。它不属于这里。",
                    DescriptionKey = "event.ruler_statue.option2.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Equipment, StringValue = EquipmentIds.StatueCore }
                    },
                    ResultText = "你取出了雕像内部不断旋转的核心。\n它在你手中仍然微微震动。\n获得【雕像核心】。",
                    ResultTextKey = "event.ruler_statue.option2.result"
                },
                new()
                {
                    Name = "提取净化液",
                    Description = "你打开雕像底座的循环装置，取得其中仍保持澄澈的净化液。",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Equipment, StringValue = EquipmentIds.SpringEssence }
                    },
                    ResultText = "循环装置的最后一滴净化液凝结成晶体。\n获得【泉水精华】。"
                }
            }
        };
    }

    /// <summary>
    /// Event System 的公开入口：CreateChasingPursuersEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateChasingPursuersEvent()
    {
        return new EventData
        {
            Id = "chasing_pursuers",
            AssetCode = "EV333001",
            Name = "追兵",
            Description = "因为你摧毁了城市雕像，搜捕小队终于找到了你的踪迹。\n他们要求你交出偷走的遗物，否则将立即发动攻击。",
            NameKey = "event.chasing_pursuers.name",
            DescriptionKey = "event.chasing_pursuers.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Rare,
            EventTags = new List<string> { "Chapter3", "ImperialRoute", "Pursuit" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 3
                },
                new()
                {
                    Type = EventConditionType.CurrentChapterRoute,
                    StringValue = nameof(ChapterRoute.Imperial)
                },
                new()
                {
                    Type = EventConditionType.HasSeenEvent,
                    StringValue = "ruler_statue"
                }
            },
            Weight = 20,
            Options = new List<EventOption>
            {
                new()
                {
                    Name = "杀出去",
                    NameKey = "event.chasing_pursuers.option1.name",
                    Description = "正面迎击追捕者×3。胜利后保留【遗忘之石】/【雕像核心】（若拥有），并获得100金币。",
                    DescriptionKey = "event.chasing_pursuers.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.StartChasingPursuersBattle }
                    }
                },
                new()
                {
                    Name = "交出赃物",
                    NameKey = "event.chasing_pursuers.option2.name",
                    Description = "交出【遗忘之石】/【雕像核心】（若拥有），换取安全离开，并恢复15点生命值。",
                    DescriptionKey = "event.chasing_pursuers.option2.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.RemoveSpecificEquipment, StringValue = EquipmentIds.ForgottenStone },
                        new() { Type = EventRewardType.RemoveSpecificEquipment, StringValue = EquipmentIds.StatueCore },
                        new() { Type = EventRewardType.Heal, Amount = 15 }
                    },
                    ResultText = "你交出了偷走的遗物。\n搜捕小队满意地离开了。\n恢复15点生命值。",
                    ResultTextKey = "event.chasing_pursuers.option2.result"
                },
                new()
                {
                    Name = "声东击西",
                    NameKey = "event.chasing_pursuers.option3.name",
                    Description = "制造混乱，趁机脱身。\n50%成功：保留全部遗物，获得50金币。\n50%失败：失去【遗忘之石】/【雕像核心】（若拥有），损失15点生命值。",
                    DescriptionKey = "event.chasing_pursuers.option3.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.ChasingPursuersDiversion }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Event System 的公开入口：CreateLifeSpringEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateLifeSpringEvent()
    {
        const string temperPool = $"{EquipmentIds.RustSword},{EquipmentIds.RustSpear},{EquipmentIds.RustShield},{EquipmentIds.RustBlueSteelSword},{EquipmentIds.RustGuDingDao},{EquipmentIds.HejinJian},{EquipmentIds.HejinMao},{EquipmentIds.GangDun},{EquipmentIds.BlueSteelSword},{EquipmentIds.GuDingDao}";

        return new EventData
        {
            Id = "life_spring",
            AssetCode = "EV211003",
            Name = "生命之泉",
            Description = "一汪清澈的泉水从地底涌出。\n据说它拥有改变命运的力量。\n有人获得了新生。\n也有人因此堕入深渊。",
            NameKey = "event.life_spring.name",
            DescriptionKey = "event.life_spring.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            EventTags = new List<string> { "Chapter2", "CityRoute", "SewerRoute", "Life", "Curse" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 2
                }
            },
            Weight = 30,
            Options = new List<EventOption>
            {
                new()
                {
                    Name = "饮用生命之泉",
                    NameKey = "event.life_spring.option1.name",
                    Description = "获得20%最大生命值上限，当前生命同步增加相同数值。",
                    DescriptionKey = "event.life_spring.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.MaxHpPercentWithCurrentSync, Amount = 20 }
                    },
                    ResultText = "你饮下泉水，冰凉的力量迅速流遍全身。",
                    ResultTextKey = "event.life_spring.option1.result"
                },
                new()
                {
                    Name = "用泉水淬炼刀刃",
                    NameKey = "event.life_spring.option2.name",
                    Description = "若你拥有指定装备之一，所有杀系伤害永久+5；持有锈古锭刀时，将其升级为古锭刀。",
                    DescriptionKey = "event.life_spring.option2.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.OwnsAnyEquipmentInPool,
                            StringValue = temperPool
                        }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.RunKillDamageBonus, Amount = 5 },
                        new()
                        {
                            Type = EventRewardType.TransformEquipment,
                            StringValue = EquipmentIds.RustGuDingDao,
                            SecondaryStringValue = EquipmentIds.GuDingDao
                        }
                    },
                    ResultText = "泉水在锋刃与甲片上留下了新的纹路。",
                    ResultTextKey = "event.life_spring.option2.result"
                },
                new()
                {
                    Name = "拿取泉水之源",
                    NameKey = "event.life_spring.option3.name",
                    Description = "获得诅咒×2，并立即获得1粮草。",
                    DescriptionKey = "event.life_spring.option3.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.Curse },
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.Curse },
                        new() { Type = EventRewardType.Forage, Amount = 1 }
                    },
                    ResultText = "你带走了泉眼深处残留的能量，一股阴冷气息同时缠上了你。",
                    ResultTextKey = "event.life_spring.option3.result"
                }
            }
        };
    }

    // ————————————————————————————————————————————————————————————
    // 意外短路：第一章 Common RunOnce 事件，五选项（换牌×3 + 获取顺手牵羊 + 芯片三选一）
    // ————————————————————————————————————————————————————————————
    /// <summary>
    /// Event System 的公开入口：CreateShortCircuitMemoryEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateShortCircuitMemoryEvent()
    {
        return new EventData
        {
            Id = "short_circuit_memory",
            AssetCode = "EV111003",
            Name = "意外短路",
            Description = "你意外短路了，但是短路让你回想起了一些过去的战斗经验。",
            NameKey = "event.short_circuit_memory.name",
            DescriptionKey = "event.short_circuit_memory.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            EventTags = new List<string> { "Memory", "Card", "Chapter1" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 1
                }
            },
            Weight = 25,
            Options = new List<EventOption>
            {
                // 选项1：雷杀 → 南蛮入侵（原子替换，需持有雷杀才显示）
                new()
                {
                    Name = "失去雷杀，获得【南蛮入侵】",
                    NameKey = "event.short_circuit_memory.option1.name",
                    Description = "移除【雷杀】，替换为【南蛮入侵】。（需持有雷杀）",
                    DescriptionKey = "event.short_circuit_memory.option1.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.HasPlayerCardType,
                            StringValue = nameof(CardType.ThunderKill)
                        }
                    },
                    Rewards = new List<EventReward>
                    {
                        new()
                        {
                            Type = EventRewardType.ReplaceCardType,
                            StringValue = nameof(CardType.ThunderKill),
                            SecondaryStringValue = nameof(CardType.NanmanInvasion)
                        }
                    },
                    ResultText = "电流在你的神经里重新排布。\n雷击记忆消散，入侵指令写入了你的核心。",
                    ResultTextKey = "event.short_circuit_memory.option1.result"
                },
                // 选项2：火杀 → 万箭齐发（原子替换，需持有火杀才显示）
                new()
                {
                    Name = "失去火杀，获得【万箭齐发】",
                    NameKey = "event.short_circuit_memory.option2.name",
                    Description = "移除【火杀】，替换为【万箭齐发】。（需持有火杀）",
                    DescriptionKey = "event.short_circuit_memory.option2.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.HasPlayerCardType,
                            StringValue = nameof(CardType.FireKill)
                        }
                    },
                    Rewards = new List<EventReward>
                    {
                        new()
                        {
                            Type = EventRewardType.ReplaceCardType,
                            StringValue = nameof(CardType.FireKill),
                            SecondaryStringValue = nameof(CardType.ArrowBarrage)
                        }
                    },
                    ResultText = "焦灼的电流灼烧着旧日战场的轮廓。\n烈焰记忆消散，箭雨指令写入了你的核心。",
                    ResultTextKey = "event.short_circuit_memory.option2.result"
                },
                // 选项3：火杀 + 200金币 → 火攻（原子替换，需持有火杀且金币足够）
                new()
                {
                    Name = "失去火杀，花费200金币获得【火攻】",
                    NameKey = "event.short_circuit_memory.option3.name",
                    Description = "移除【火杀】，花费200金币替换为【火攻】。（需持有火杀且金币≥200）",
                    DescriptionKey = "event.short_circuit_memory.option3.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.HasPlayerCardType,
                            StringValue = nameof(CardType.FireKill)
                        }
                    },
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Gold, Amount = 200 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new()
                        {
                            Type = EventRewardType.ReplaceCardType,
                            StringValue = nameof(CardType.FireKill),
                            SecondaryStringValue = nameof(CardType.FireAttack)
                        }
                    },
                    ResultText = "灼热的战斗记忆被重新编译。\n旧的火杀指令熄灭，火攻协议写入了你的核心。",
                    ResultTextKey = "event.short_circuit_memory.option3.result"
                },
                // 选项4：失去50金币，获得顺手牵羊。
                new()
                {
                    Name = "失去50金币，获得【顺手牵羊】",
                    NameKey = "event.short_circuit_memory.option4.name",
                    Description = "失去50金币，获得【顺手牵羊】。",
                    DescriptionKey = "event.short_circuit_memory.option4.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Gold, Amount = 50 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new()
                        {
                            Type = EventRewardType.AddPlayerCardType,
                            StringValue = nameof(CardType.Steal)
                        }
                    },
                    ResultText = "你用一段昂贵的旧代码换回了掠夺协议。\n【顺手牵羊】已加入出牌栏。",
                    ResultTextKey = "event.short_circuit_memory.option4.result"
                },
                // 选项5：芯片三选一（完全复用既有芯片三选一系统，见 talking_sword 同款 ChooseOneChipOnce）
                new()
                {
                    Name = "获得一次芯片三选一机会",
                    NameKey = "event.short_circuit_memory.option5.name",
                    Description = "从攻击芯片、防御芯片、知识芯片中三选一。",
                    DescriptionKey = "event.short_circuit_memory.option5.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.ChooseOneChipOnce }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Event System 的公开入口：CreateTalkingSwordEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateTalkingSwordEvent()
    {
        return new EventData
        {
            Id = "talking_sword",
            AssetCode = "EV111004",
            Name = "会说话的剑",
            Description = "你在路边发现了一把插在石头里的黑色长剑。\n\n当你靠近时，它忽然开口：\n\n「终于有人来了。」\n\n「快把我拔出来，我已经在这里待了太久。」\n\n「等等……你不会想把我卖掉吧？」",
            NameKey = "event.talking_sword.name",
            DescriptionKey = "event.talking_sword.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            Weight = 50,
            GuaranteedForChapterVariant = ChapterVariant.CurseNight.ToString(),
            EventTags = new List<string> { "Chapter1", "CurseNight", "Weapon", "Curse" },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 1
                },
                new()
                {
                    Type = EventConditionType.CurrentChapterVariant,
                    StringValue = ChapterVariant.CurseNight.ToString()
                }
            },
            Options = new List<EventOption>
            {
                new()
                {
                    Name = "把它拔出来",
                    NameKey = "event.talking_sword.option1.name",
                    Description = "获得3层诅咒，获得【诅咒之刃】。",
                    DescriptionKey = "event.talking_sword.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.Curse },
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.Curse },
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.Curse },
                        new() { Type = EventRewardType.Equipment, StringValue = EquipmentIds.CurseBlade }
                    },
                    ResultText = "你拔出了那把剑。\n它低声笑了起来。\n你承受了3层诅咒，并获得了【诅咒之刃】。",
                    ResultTextKey = "event.talking_sword.option1.result"
                },
                new()
                {
                    Name = "无视它",
                    NameKey = "event.talking_sword.option2.name",
                    Description = "获得150金币。",
                    DescriptionKey = "event.talking_sword.option2.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Gold, Amount = 150 }
                    },
                    ResultText = "你熟练地撬下剑柄上的红宝石。\n身后传来愤怒的咒骂声。\n获得150金币。",
                    ResultTextKey = "event.talking_sword.option2.result"
                },
                new()
                {
                    Name = "恐吓它",
                    NameKey = "event.talking_sword.option3.name",
                    Description = "失去10点当前生命值，获得芯片三选一机会。",
                    DescriptionKey = "event.talking_sword.option3.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.CurrentHP, Amount = 10 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.ChooseOneChipOnce }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Event System 的公开入口：CreateBloodPoolEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateBloodPoolEvent()
    {
        return new EventData
        {
            Id = "blood_pool",
            AssetCode = "EV111005",
            Name = "血池",
            Description = "远处的池塘已经被鲜血染红。\n大量气泡不断从池底翻涌而出。\n空气中弥漫着铁锈般的腥味。\n你隐约感觉池底似乎有什么东西正在注视着你。",
            NameKey = "event.blood_pool.name",
            DescriptionKey = "event.blood_pool.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            Weight = 50,
            GuaranteedForChapterVariant = ChapterVariant.BloodMoon.ToString(),
            EventTags = new List<string> { "Chapter1", "BloodMoon", "Blood", "Curse", "Chip" },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 1
                },
                new()
                {
                    Type = EventConditionType.CurrentChapterVariant,
                    StringValue = ChapterVariant.BloodMoon.ToString()
                }
            },
            Options = new List<EventOption>
            {
                new()
                {
                    Name = "放血",
                    NameKey = "event.blood_pool.option1.name",
                    Description = "失去10点当前生命值，获得5点最大生命值，然后继续留在血池前。",
                    DescriptionKey = "event.blood_pool.option1.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.CurrentHP, Amount = 10 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.MaxHp, Amount = 5 },
                        new() { Type = EventRewardType.TriggerEvent, StringValue = "blood_pool" }
                    }
                },
                new()
                {
                    Name = "饮用血池",
                    NameKey = "event.blood_pool.option2.name",
                    Description = "获得【月亮的注意】。",
                    DescriptionKey = "event.blood_pool.option2.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.MoonAttention }
                    },
                    ResultText = "你俯下身喝下池中的鲜血。\n那股温热的液体仿佛活物般流入你的身体。\n你引起了【月亮】的注意。",
                    ResultTextKey = "event.blood_pool.option2.result"
                },
                new()
                {
                    Name = "打捞骨头",
                    NameKey = "event.blood_pool.option3.name",
                    Description = "获得【大骨棒】。",
                    DescriptionKey = "event.blood_pool.option3.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Equipment, StringValue = EquipmentIds.BigBoneClub }
                    },
                    ResultText = "你从血池中拖出一根巨大的骸骨。\n获得了【大骨棒】。",
                    ResultTextKey = "event.blood_pool.option3.result"
                }
            }
        };
    }

    // ————————————————————————————————————————————————————————————
    // 赤壁残骸：第一章 Common RunOnce，四选项（打捞/寻找船夫/进入沼泽/进入华容道）
    // ————————————————————————————————————————————————————————————
    /// <summary>
    /// Event System 的公开入口：CreateChibiRuinsEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateChibiRuinsEvent()
    {
        return new EventData
        {
            Id = "chibi_ruins",
            AssetCode = "EV111006",
            Name = "赤壁残骸",
            Description = "赤壁现在已经变为了一片腐臭的沼泽，远处还能看见当年战争留下的巨型船骸。",
            NameKey = "event.chibi_ruins.name",
            DescriptionKey = "event.chibi_ruins.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            EventTags = new List<string> { "Equipment", "Route", "Chapter1" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 1
                }
            },
            Weight = 25,
            Options = new List<EventOption>
            {
                // 选项一：打捞残骸（随机普通装备×2）
                new()
                {
                    Name = "打捞残骸",
                    NameKey = "event.chibi_ruins.option1.name",
                    Description = "你尝试登上那些已经倾斜的巨船。获得两件随机普通装备。",
                    DescriptionKey = "event.chibi_ruins.option1.desc",
                    RewardSequence =
                    {
                        new RandomEquipmentByRarityRewardAction(
                            EquipmentRarity.Common,
                            "你从浑浊的江水中打捞起了一件残存的普通装备：\n【{name}】"),
                        new RandomEquipmentByRarityRewardAction(
                            EquipmentRarity.Common,
                            "你又从倾斜的船舱中找到一件普通装备：\n【{name}】")
                    }
                },
                // 选项二：寻找船夫（吴阵营免费，其它阵营-50金 → 回复10生命 + 防水模块）
                // 吴国角色熟悉水路，船夫不收取费用；其它阵营正常支付50金币。
                new()
                {
                    Name = "寻找船夫",
                    NameKey = "event.chibi_ruins.option2.name",
                    Description = "恢复10点生命值，获得【防水模块】。吴阵营免费，其它阵营花费50金币。",
                    DescriptionKey = "event.chibi_ruins.option2.desc",
                    RewardSequence =
                    {
                        new LoseGoldUnlessFactionRewardAction(50, Faction.Wu),
                        new HealRewardAction(10),
                        new AddEquipmentRewardAction(EquipmentIds.WaterproofModule)
                    },
                    ResultText = "你找到了一名在芦苇丛中躲避的船夫。\n他帮你避开了最深的泥潭，并交给你一枚密封模块。\n\n生命 +10，获得【防水模块】。",
                    ResultTextKey = "event.chibi_ruins.option2.result"
                },
                // 选项三：进入沼泽（立即进入旧日虚影事件）
                new()
                {
                    Name = "进入沼泽",
                    NameKey = "event.chibi_ruins.option3.name",
                    Description = "你沿着沼泽深处传来的呼唤继续前进。进入【旧日虚影】事件。",
                    DescriptionKey = "event.chibi_ruins.option3.desc",
                    RewardSequence =
                    {
                        new JumpEventRewardAction("ancient_phantom")
                    }
                },
                // 选项四：华容道（仅魏阵营可见 → 直接获得芯片三选一机会）
                // 不是独立事件节点：不跳转到任何其它 EventData，只是本事件内部的一个特殊分支，
                // 选完直接打开既有的芯片三选一界面，选择完成后照常关闭事件、返回当前章节地图，
                // 不额外消耗事件次数、不额外消耗电量、不开启新的地图/事件流程。
                new()
                {
                    Name = "进入华容道",
                    NameKey = "event.chibi_ruins.option4.name",
                    Description = "沿着华容古道继续前进，获得一次芯片三选一机会。（需阵营：魏）",
                    DescriptionKey = "event.chibi_ruins.option4.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.CurrentCharacterFaction,
                            StringValue = "Wei"
                        }
                    },
                    RewardSequence =
                    {
                        new OpenChipChoiceRewardAction()
                    }
                }
            }
        };
    }

    // ————————————————————————————————————————————————————————————
    // 贪婪密窟：第一章普通事件，RunOnce，三选一（金币/随机稀有装备/献祭生命换黄金雕像）
    // ————————————————————————————————————————————————————————————
    /// <summary>
    /// Event System 的公开入口：CreateGreedyVaultEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    internal static EventData CreateGreedyVaultEvent()
    {
        return new EventData
        {
            Id = "greedy_vault",
            AssetCode = "EV111008",
            Name = "贪婪密窟",
            Description = "你发现了一座被巨石封死的古老金库。\n\n空气中弥漫着浓郁的血腥味，金砖与金币堆积成山，中央矗立着一尊散发诡异光芒的黄金雕像，仿佛正在等待新的祭品。",
            NameKey = "event.greedy_vault.name",
            DescriptionKey = "event.greedy_vault.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            EventTags = new List<string> { "Gold", "Equipment", "Chapter1" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 1
                }
            },
            Weight = 20,
            Options = new List<EventOption>
            {
                // 选项一：选择金币（获得200金币）
                new()
                {
                    Name = "选择金币",
                    NameKey = "event.greedy_vault.option1.name",
                    Description = "获得200金币。",
                    DescriptionKey = "event.greedy_vault.option1.desc",
                    RewardSequence =
                    {
                        new AddGoldRewardAction(200)
                    }
                },
                // 选项二：寻找财宝（随机一件稀有装备）
                new()
                {
                    Name = "寻找财宝",
                    NameKey = "event.greedy_vault.option2.name",
                    Description = "获得一件随机稀有装备。",
                    DescriptionKey = "event.greedy_vault.option2.desc",
                    RewardSequence =
                    {
                        new RandomEquipmentByRarityRewardAction(
                            EquipmentRarity.Rare,
                            "你在密窟深处翻出了一件稀有装备：\n【{name}】")
                    }
                },
                // 选项三：奉上血液（永久-5最大生命 → 获得【黄金雕像】）
                new()
                {
                    Name = "奉上血液",
                    NameKey = "event.greedy_vault.option3.name",
                    Description = "失去5点最大生命值，获得【黄金雕像】。（需 最大生命≥6）",
                    DescriptionKey = "event.greedy_vault.option3.desc",
                    // 最大生命不足6时禁止选择，以防止将上限降至0或以下。
                    Conditions = new List<EventCondition>
                    {
                        new() { Type = EventConditionType.MaxHP, Comparison = EventComparison.GreaterOrEqual, IntValue = 6 }
                    },
                    RewardSequence =
                    {
                        new LoseMaxHpRewardAction(5),
                        new AddEquipmentRewardAction(EquipmentIds.GoldenStatue)
                    },
                    ResultText = "你将手臂划破，任由鲜血滴落在雕像的底座上。\n黄金雕像的光芒骤然一亮，随即被你收入囊中。\n\n最大生命 -5，获得【黄金雕像】。",
                    ResultTextKey = "event.greedy_vault.option3.result"
                }
            }
        };
    }

    // ————————————————————————————————————————————————————————————
    // 载具店：第二、三章普通单次事件；内含载具商店与角色专属购买选项。
    // ————————————————————————————————————————————————————————————
    /// <summary>
    /// Event System 的公开入口：CreateVehicleShopEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateVehicleShopEvent()
    {
        return new EventData
        {
            Id = "vehicle_shop_event",
            AssetCode = "EV211004",
            Name = "载具店",
            Description = "一间隐藏在城市角落里的特殊商店。\n这里不售卖武器。\n也不售卖护甲。\n老板似乎只对各种稀奇古怪的载具感兴趣。",
            NameKey = "event.vehicle_shop_event.name",
            DescriptionKey = "event.vehicle_shop_event.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.GreaterOrEqual,
                    IntValue = 2
                }
            },
            Weight = 20,
            Options = new List<EventOption>
            {
                // 选项一：进入载具商店
                new()
                {
                    Name = "进入载具商店",
                    NameKey = "event.vehicle_shop_event.option1.name",
                    Description = "进入载具商店，浏览在售载具。",
                    DescriptionKey = "event.vehicle_shop_event.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.OpenVehicleShop }
                    }
                },
                // 选项二：吕布专属购买赤兔
                new()
                {
                    Name = "购买赤兔",
                    NameKey = "event.vehicle_shop_event.option2.name",
                    Description = "消耗150金币，获得【赤兔】。（吕布专属）",
                    DescriptionKey = "event.vehicle_shop_event.option2.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.CurrentCharacter,
                            StringValue = CharacterIds.LuBu
                        },
                        new()
                        {
                            Type = EventConditionType.Gold,
                            Comparison = EventComparison.GreaterOrEqual,
                            IntValue = 150
                        }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Gold, Amount = -150 },
                        new() { Type = EventRewardType.Equipment, StringValue = EquipmentIds.ChiTu }
                    },
                    ResultText = "你花费了150金币。\n【赤兔】加入了你的背包。",
                    ResultTextKey = "event.vehicle_shop_event.option2.result"
                },
                // 选项三：诸葛亮专属购买木牛流马
                new()
                {
                    Name = "购买木牛流马",
                    NameKey = "event.vehicle_shop_event.option3.name",
                    Description = "消耗150金币，获得【木牛流马】。（诸葛亮专属）",
                    DescriptionKey = "event.vehicle_shop_event.option3.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.CurrentCharacter,
                            StringValue = CharacterIds.ZhuGeLiang
                        },
                        new()
                        {
                            Type = EventConditionType.Gold,
                            Comparison = EventComparison.GreaterOrEqual,
                            IntValue = 150
                        }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Gold, Amount = -150 },
                        new() { Type = EventRewardType.Equipment, StringValue = EquipmentIds.MuNiuLiuMa }
                    },
                    ResultText = "你花费了150金币。\n【木牛流马】加入了你的背包。",
                    ResultTextKey = "event.vehicle_shop_event.option3.result"
                }
            }
        };
    }

    /// <summary>
    /// Event System 的公开入口：CreateManZuCampEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateManZuCampEvent()
    {
        return new EventData
        {
            Id = "manzu_camp",
            AssetCode = "EV212003",
            Name = "蛮族营寨",
            Description = "你在山林间发现了一处南蛮部落的营地。\n篝火未息，肉香四溢。\n蛮族士兵警觉地打量着你。",
            NameKey = "event.manzu_camp.name",
            DescriptionKey = "event.manzu_camp.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Rare,
            GuaranteedForCharacterId = CharacterIds.MengHuo,
            Weight = 20,
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 2
                }
            },
            Options = new List<EventOption>
            {
                // 选项1：学习战斗技术（仅当玩家尚无南蛮入侵时显示）
                new()
                {
                    Name = "学习战斗技术",
                    NameKey = "event.manzu_camp.option1.name",
                    Description = "花费50金与蛮族交换技艺，将南蛮入侵加入牌组。",
                    DescriptionKey = "event.manzu_camp.option1.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.HasPlayerCardType,
                            StringValue = "NanmanInvasion",
                            Negate = true
                        }
                    },
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Gold, Amount = 50 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.AddPlayerCardType, StringValue = "NanmanInvasion" }
                    },
                    ResultText = "你向蛮族学习了南蛮入侵的战法，牌组中多了一张南蛮入侵。",
                    ResultTextKey = "event.manzu_camp.option1.result"
                },
                // 选项2：进行决斗
                new()
                {
                    Name = "进行决斗",
                    NameKey = "event.manzu_camp.option2.name",
                    Description = "向蛮族的勇士发起决斗（流亡蛮族×2），胜利后获得【蛮族的牙齿】。",
                    DescriptionKey = "event.manzu_camp.option2.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.StartManZuCampBattle }
                    }
                },
                // 选项3：大快朵颐
                new()
                {
                    Name = "大快朵颐",
                    NameKey = "event.manzu_camp.option3.name",
                    Description = "花费100金共享营地大餐，回复至满血，最大生命+10，当前生命+10。",
                    DescriptionKey = "event.manzu_camp.option3.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Gold, Amount = 100 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.HealFull },
                        new() { Type = EventRewardType.MaxHp, Amount = 10 },
                        new() { Type = EventRewardType.Heal, Amount = 10 }
                    },
                    ResultText = "你与蛮族共享了丰盛的食物。回复至满血，最大生命+10，当前生命+10。",
                    ResultTextKey = "event.manzu_camp.option3.result"
                },
                // 选项4：偷窃（50%成功获得200金，50%失败进入决斗）
                new()
                {
                    Name = "偷窃",
                    NameKey = "event.manzu_camp.option4.name",
                    Description = "趁蛮族不注意，偷窃物资。成功（50%）：获得200金；失败（50%）：被发现，进入决斗。"
                    + "\n失败的决斗胜利同样获得【蛮族的牙齿】。",
                    DescriptionKey = "event.manzu_camp.option4.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.ManZuCampTheft }
                    }
                },
                // 选项5：招募援军（需要南蛮入侵牌型）
                new()
                {
                    Name = "招募援军",
                    NameKey = "event.manzu_camp.option5.name",
                    Description = "花费100金说服蛮族同行，南蛮入侵伤害本局永久 ×1.5。",
                    DescriptionKey = "event.manzu_camp.option5.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.HasPlayerCardType,
                            StringValue = "NanmanInvasion"
                        }
                    },
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Gold, Amount = 100 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.NanmanDamageMultiply, Amount = 50 }
                    }
                },
                // 选项6：统领旧部（孟获专属，需要蛮族技能）
                new()
                {
                    Name = "统领旧部",
                    NameKey = "event.manzu_camp.option6.name",
                    Description = "花费200金统领南蛮旧部，【蛮族】技能进化为【蛮族之王】。"
                    + "\n南蛮入侵伤害×2，每次命中敌人回复5生命和0.5费。（孟获专属）",
                    DescriptionKey = "event.manzu_camp.option6.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.CurrentCharacter,
                            StringValue = CharacterIds.MengHuo
                        }
                    },
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Gold, Amount = 200 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.EvolveManzuToManzuWang }
                    }
                }
            }
        };
    }

    // ————————————————————————————————————————————————————————————
    // 古蜀铸炉：第三章皇宫路线 史诗事件，RunOnce
    // ————————————————————————————————————————————————————————————
    /// <summary>
    /// Event System 的公开入口：CreateAncientForgeEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateAncientForgeEvent()
    {
        return new EventData
        {
            Id = "ancient_forge",
            AssetCode = "EV313001",
            Name = "古蜀铸炉",
            Description = "你来到一座仍在燃烧的古老铸炉。\n炉中青焰千年不灭。\n炉壁刻着一行字：「剑可重铸，魂不可回。」",
            NameKey = "event.ancient_forge.name",
            DescriptionKey = "event.ancient_forge.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Epic,
            EventTags = new List<string> { "Chapter3", "ImperialRoute", "Equipment", "Legendary", "Curse" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 3
                },
                new()
                {
                    Type = EventConditionType.CurrentChapterRoute,
                    StringValue = nameof(ChapterRoute.Imperial)
                }
            },
            Weight = 25,
            Options = new List<EventOption>
            {
                // 选项1：献祭装备 → 销毁装备+清空金币 → 2诅咒 → 传奇三选一
                new()
                {
                    Name = "献祭装备",
                    NameKey = "event.ancient_forge.option1.name",
                    Description = "将所有装备与金币投入古炉，换取传奇之器。获得2层诅咒。",
                    DescriptionKey = "event.ancient_forge.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.DestroyAllEquipmentAndGold },
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.Curse },
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.Curse },
                        new() { Type = EventRewardType.ChooseOneLegendaryEquipment }
                    }
                },
                // 选项2：献祭灵魂 → 当前生命固定为1、最大生命-30% → 传奇三选一
                new()
                {
                    Name = "献祭灵魂",
                    NameKey = "event.ancient_forge.option2.name",
                    Description = "将生命精华注入古炉，当前生命降至1点，最大生命降低30%，换取传奇之器。",
                    DescriptionKey = "event.ancient_forge.option2.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.SetCurrentHpToOneAndLoseMaxHpPercent, Amount = 30 },
                        new() { Type = EventRewardType.ChooseOneLegendaryEquipment }
                    }
                },
                // 选项3：寻找装备 → 稀有/史诗三选一，无代价
                new()
                {
                    Name = "寻找装备",
                    NameKey = "event.ancient_forge.option3.name",
                    Description = "提供稀有、史诗装备三选一。",
                    DescriptionKey = "event.ancient_forge.option3.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.ChooseOneRareOrEpicEquipment }
                    }
                },
                // 选项4：回炉重铸 → 选择一件装备摧毁 → 高一品质随机装备 + 5层诅咒
                // 持有任意装备时才显示
                new()
                {
                    Name = "回炉重铸",
                    NameKey = "event.ancient_forge.option4.name",
                    Description = "将一件装备投入古炉，炼出更高品质的神器。获得5层诅咒。",
                    DescriptionKey = "event.ancient_forge.option4.desc",
                    Conditions = new List<EventCondition>
                    {
                        new() { Type = EventConditionType.HasAnyOwnedEquipment }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.Curse },
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.Curse },
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.Curse },
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.Curse },
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.Curse },
                        new() { Type = EventRewardType.SacrificeOneEquipmentForUpgrade }
                    }
                },
                // 隐藏选项：赵云持有青钢剑（已装备或背包中均可触发）
                new()
                {
                    Name = "投入青钢剑",
                    NameKey = "event.ancient_forge.option5.name",
                    Description = "仅赵云持有青钢剑与神秘芯片时显示。投入青钢剑并失去神秘芯片，获得【真·青钢剑】。",
                    DescriptionKey = "event.ancient_forge.option5.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.CurrentCharacter,
                            StringValue = CharacterIds.ZhaoYun
                        },
                        new()
                        {
                            Type = EventConditionType.OwnsEquipment,
                            StringValue = EquipmentIds.BlueSteelSword
                        },
                        new()
                        {
                            Type = EventConditionType.OwnsEquipment,
                            StringValue = EquipmentIds.MysteriousChip
                        }
                    },
                    Rewards = new List<EventReward>
                    {
                        new()
                        {
                            Type = EventRewardType.TransformEquipment,
                            StringValue = EquipmentIds.BlueSteelSword,
                            SecondaryStringValue = EquipmentIds.TrueBlueSteelSword
                        },
                        new() { Type = EventRewardType.RemoveSpecificEquipment, StringValue = EquipmentIds.MysteriousChip },
                    },
                    ResultText = "青钢剑在烈焰中升华。\n真·青钢剑在你手中微微震颤，散发着凛冽的寒芒。\n获得【真·青钢剑】。",
                    ResultTextKey = "event.ancient_forge.option5.result"
                },
                // 隐藏选项：徐盛持有古锭刀与神秘芯片（已装备或背包中均可触发）
                new()
                {
                    Name = "投入古锭刀",
                    NameKey = "event.ancient_forge.option6.name",
                    Description = "仅徐盛持有古锭刀与神秘芯片时显示。投入古锭刀并失去神秘芯片，获得【真·古锭刀】。",
                    DescriptionKey = "event.ancient_forge.option6.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.CurrentCharacter,
                            StringValue = CharacterIds.XuSheng
                        },
                        new()
                        {
                            Type = EventConditionType.OwnsEquipment,
                            StringValue = EquipmentIds.GuDingDao
                        },
                        new()
                        {
                            Type = EventConditionType.OwnsEquipment,
                            StringValue = EquipmentIds.MysteriousChip
                        }
                    },
                    Rewards = new List<EventReward>
                    {
                        new()
                        {
                            Type = EventRewardType.TransformEquipment,
                            StringValue = EquipmentIds.GuDingDao,
                            SecondaryStringValue = EquipmentIds.TrueGuDingDao
                        },
                        new() { Type = EventRewardType.RemoveSpecificEquipment, StringValue = EquipmentIds.MysteriousChip },
                    },
                    ResultText = "古锭刀在青焰中迸发出火雷之光。\n神秘芯片化为刀身的核心。\n获得【真·古锭刀】。",
                    ResultTextKey = "event.ancient_forge.option6.result"
                }
            }
        };
    }

    // ————————————————————————————————————————————————————————————
    // 废弃实验室：第一章 Rare RunOnce 事件，4选项（注射/拆解/收集/回收电池）
    // EV122002：ch1 Rare，与旧日虚影同类
    // ————————————————————————————————————————————————————————————
    /// <summary>
    /// Event System 的公开入口：CreateAbandonedLabEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateAbandonedLabEvent()
    {
        return new EventData
        {
            Id = "abandoned_lab",
            AssetCode = "EV122002",
            Name = "废弃实验室",
            Description = "你发现了一间废弃实验室。一支仍在发光的针剂插在培养仓旁。\n系统提示：\"实验型战争强化剂\"",
            NameKey = "event.abandoned_lab.name",
            DescriptionKey = "event.abandoned_lab.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Rare,
            EventTags = new List<string> { "Chapter1", "Rare", "Lab", "Equipment" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 1
                }
            },
            Weight = 20,
            Options = new List<EventOption>
            {
                // 选项1：注射 → 第一、二章 MaxHP+30 + 战斗开始+2费；第三章移除这段强化。
                new()
                {
                    Name = "注射药剂",
                    NameKey = "event.abandoned_lab.option1.name",
                    Description = "获得【过期强化剂】：第一、二章最大生命值+30，每场战斗开始时+2费。",
                    DescriptionKey = "event.abandoned_lab.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.MaxHp, Amount = 30 },
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.ExpiredEnhancer }
                    },
                    ResultText = "药剂注入体内，感觉前所未有的清醒。\n第一、二章最大生命值+30，每场战斗开始时+2费。",
                    ResultTextKey = "event.abandoned_lab.option1.result"
                },
                // 选项2：拆解设备 → 获得神秘芯片
                new()
                {
                    Name = "拆解设备",
                    NameKey = "event.abandoned_lab.option2.name",
                    Description = "拆解实验室核心装置，取出一枚结构精密的神秘芯片。",
                    DescriptionKey = "event.abandoned_lab.option2.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Equipment, StringValue = EquipmentIds.MysteriousChip }
                    },
                    ResultText = "你小心翼翼地取下芯片，其内部电路在黑暗中隐隐发光。\n\n获得【神秘芯片】。",
                    ResultTextKey = "event.abandoned_lab.option2.result"
                },
                // 选项3：收集数据 → 50金币 + 隐藏的密室数据标记（禁书库暗门条件不在此处说明）。
                new()
                {
                    Name = "收集数据",
                    NameKey = "event.abandoned_lab.option3.name",
                    Description = "将实验终端中残存的数据复制下来，同时找到了一些研究经费。获得50金币与密室数据。",
                    DescriptionKey = "event.abandoned_lab.option3.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Gold, Amount = 50 },
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.ExperimentData }
                    },
                    ResultText = "数据已备份完毕，同时从抽屉里找到了一沓旧钞票。\n获得50金币与密室数据。",
                    ResultTextKey = "event.abandoned_lab.option3.result"
                },
                // 选项4：回收电池 → 最大电量永久+15（本局永久，不随章节重置，进入新章节按新上限回满）
                new()
                {
                    Name = "回收电池",
                    NameKey = "event.abandoned_lab.option4.name",
                    Description = "回收培养仓残留的动力电池组，最大电量永久+15。",
                    DescriptionKey = "event.abandoned_lab.option4.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.MaxPowerBonus, Amount = 15 }
                    },
                    ResultText = "你拆下电池组接入探索装置的接口，电量表的上限刻度向后延伸了一截。\n\n最大电量永久+15。",
                    ResultTextKey = "event.abandoned_lab.option4.result"
                }
            }
        };
    }

    // ————————————————————————————————————————————————————————————
    // 禁书库：第三章皇宫路线 Common RunOnce 事件，5选项（含吕蒙专属 + 密室数据暗门）。
    // ————————————————————————————————————————————————————————————
    /// <summary>
    /// Event System 的公开入口：CreateForbiddenLibraryEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateForbiddenLibraryEvent()
    {
        return new EventData
        {
            Id = "forbidden_library",
            AssetCode = "EV311001",
            Name = "禁书库",
            Description = "存放着各种记忆芯片。",
            NameKey = "event.forbidden_library.name",
            DescriptionKey = "event.forbidden_library.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            EventTags = new List<string> { "Chapter3", "ImperialRoute", "Chip", "Skill" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 3
                },
                new()
                {
                    Type = EventConditionType.CurrentChapterRoute,
                    StringValue = nameof(ChapterRoute.Imperial)
                }
            },
            Weight = 30,
            GuaranteedForCharacterId = CharacterIds.LuMeng,
            GuaranteedWhenHasRunBuffId = RunBuffIds.ExperimentData,
            Options = new List<EventOption>
            {
                // 选项1：回收记忆 → 失去所有芯片 → 随机稀有技能
                new()
                {
                    Name = "回收记忆",
                    NameKey = "event.forbidden_library.option1.name",
                    Description = "回收全部记忆芯片，从数据库中随机习得一项稀有技能。",
                    DescriptionKey = "event.forbidden_library.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.RemoveAllChips },
                        new() { Type = EventRewardType.RandomRareSkill }
                    }
                },
                // 选项2：购买访问权限 → 花费500金币 → 随机稀有技能
                new()
                {
                    Name = "购买访问权限",
                    NameKey = "event.forbidden_library.option2.name",
                    Description = "花费500金币购买临时访问权限，从数据库中随机习得一项稀有技能。",
                    DescriptionKey = "event.forbidden_library.option2.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Gold, Amount = 500 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.RandomRareSkill }
                    }
                },
                // 选项3：阅读禁书 → 连续两次芯片三选一
                new()
                {
                    Name = "阅读禁书",
                    NameKey = "event.forbidden_library.option3.name",
                    Description = "深入禁区档案，连续进行两次芯片三选一。",
                    DescriptionKey = "event.forbidden_library.option3.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.ChooseOneChipTwice }
                    }
                },
                // 隐藏选项：吕蒙 + 克己 → 升级为克己·无限制协议
                new()
                {
                    Name = "接入最高权限数据库",
                    NameKey = "event.forbidden_library.option4.name",
                    Description = "吕蒙凝视着核心终端。某段深埋的权限代码开始共鸣——克己协议的封印正在解除。",
                    DescriptionKey = "event.forbidden_library.option4.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.CurrentCharacter,
                            StringValue = CharacterIds.LuMeng
                        },
                        new()
                        {
                            Type = EventConditionType.HasSkill,
                            StringValue = SkillIds.Keji
                        },
                        new()
                        {
                            Type = EventConditionType.HasSkill,
                            StringValue = SkillIds.KejiUnlimited,
                            Negate = true
                        }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.EvolveKejiToKejiUnlimited }
                    },
                    ResultText = "权限验证通过。\n克己协议升级完成。\n资源限制已解除——费用上限20，每1费+10%伤害，免疫顺手牵羊。",
                    ResultTextKey = "event.forbidden_library.option4.result"
                },
                // 隐藏选项：持有密室数据 → 传奇武器三选一
                new()
                {
                    Name = "铁门后的秘密",
                    NameKey = "event.forbidden_library.option5.name",
                    Description = "你发现了禁书库一扇神秘的门，里面摆放着一件传奇武器。提供传奇武器三选一。",
                    DescriptionKey = "event.forbidden_library.option5.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.HasRunBuff,
                            StringValue = RunBuffIds.ExperimentData
                        }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.ChooseOneLegendaryWeaponEquipment }
                    },
                    ResultText = string.Empty,
                    ResultTextKey = "event.forbidden_library.option5.result"
                }
            }
        };
    }

    // ————————————————————————————————————————————————————————————
    // 七星坛：第三章皇宫路线 Common RunOnce 事件，3选项+1隐藏选项（诸葛亮专属）
    // ————————————————————————————————————————————————————————————
    /// <summary>
    /// Event System 的公开入口：CreateQiXingTanEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateQiXingTanEvent()
    {
        return new EventData
        {
            Id = "qixingtan",
            AssetCode = "EV311002",
            Name = "七星坛",
            Description = "残破的祭坛仍矗立在宫墙深处。\n七根石柱围绕中央法阵。",
            NameKey = "event.qixingtan.name",
            DescriptionKey = "event.qixingtan.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            GuaranteedForCharacterId = CharacterIds.ZhuGeLiang,
            EventTags = new List<string> { "Chapter3", "ImperialRoute", "Ritual", "Buff" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 3
                }
            },
            Weight = 25,
            Options = new List<EventOption>
            {
                // 选项1：进行占卜 → 获得【煞气缠身】（HP→1，禁止所有治疗，伤害×1.5，永久）
                new()
                {
                    Name = "进行占卜",
                    NameKey = "event.qixingtan.option1.name",
                    Description = "踏入法阵，感受天地煞气。获得【煞气缠身】：生命值恒定为1，无法通过任何方式恢复生命，但你造成的所有伤害×1.5。（永久，直到Run结束）",
                    DescriptionKey = "event.qixingtan.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.ShaQiChenShen }
                    },
                    ResultText = "煞气如潮水般涌入体内。你感到无比虚弱，却又充满了力量。\n【煞气缠身】已附身——生命恒为1，但你的每一击都携带着毁灭之力。",
                    ResultTextKey = "event.qixingtan.option1.result"
                },
                // 选项2：作法招魂 → 进入特殊战斗（第一章Boss×3HP，伤害×2）
                new()
                {
                    Name = "作法招魂",
                    NameKey = "event.qixingtan.option2.name",
                    Description = "点燃七星灯，召唤第一章Boss的强化灵魂对决。敌人生命值×3，造成伤害×2。\n胜利：获得【灵魂石】×2。失败：按正常战斗失败结算。",
                    DescriptionKey = "event.qixingtan.option2.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.StartQiXingTanBattle }
                    }
                },
                // 选项3：搜刮 → 获得【朱雀羽扇】
                new()
                {
                    Name = "搜刮",
                    NameKey = "event.qixingtan.option3.name",
                    Description = "在祭坛四周的石柱旁寻找遗物，发现了一把锦扇。获得【朱雀羽扇】。",
                    DescriptionKey = "event.qixingtan.option3.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Equipment, StringValue = EquipmentIds.ZhuQueYuShan }
                    },
                    ResultText = "羽扇轻轻发热——朱雀之灵封印其中。\n获得【朱雀羽扇】：你的火杀造成双倍伤害；若你是周瑜，火攻也造成双倍伤害。",
                    ResultTextKey = "event.qixingtan.option3.result"
                },
                // 隐藏选项：诸葛亮专属，持有【观星】时 → 重启七星阵，将观星升级为【天机】
                new()
                {
                    Name = "重启七星阵",
                    NameKey = "event.qixingtan.option4.name",
                    Description = "诸葛亮凝视着七根石柱，脑中忽然浮现出一道残缺的阵法。以【观星】为引，重启七星阵——将【观星】升级为【天机】。",
                    DescriptionKey = "event.qixingtan.option4.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.CurrentCharacter,
                            StringValue = CharacterIds.ZhuGeLiang
                        },
                        new()
                        {
                            Type = EventConditionType.HasSkill,
                            StringValue = SkillIds.Guanxing
                        },
                        new()
                        {
                            Type = EventConditionType.HasSkill,
                            StringValue = SkillIds.TianJi,
                            Negate = true
                        }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.EvolveGuanxingToTianJi }
                    },
                    ResultText = "七星阵轰然启动，光柱冲天。观星之术在法阵共鸣中升华。\n【天机】已觉醒——使用观星时立即获得1费（仍正常扣费）。",
                    ResultTextKey = "event.qixingtan.option4.result"
                }
            }
        };
    }

    // ————————————————————————————————————————————————————————————
    // 囚禁的精灵：第三章皇宫路线普通事件，RunOnce。
    // ————————————————————————————————————————————————————————————
    /// <summary>
    /// Event System 的公开入口：CreateImprisonedElfEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateImprisonedElfEvent()
    {
        return new EventData
        {
            Id = "imprisoned_elf",
            AssetCode = "EV331001",
            Name = "囚禁的精灵",
            Description = "昏暗的监牢深处。\n\n一只精灵被沉重的锁链束缚。\n\n它虚弱地望向你。\n\n眼神中充满恐惧与求生的希望。",
            NameKey = "event.imprisoned_elf.name",
            DescriptionKey = "event.imprisoned_elf.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            EventTags = new List<string> { "Chapter3", "ImperialRoute", "Element", "Reforge", "Peach" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 3
                },
                new()
                {
                    Type = EventConditionType.CurrentChapterRoute,
                    StringValue = nameof(ChapterRoute.Imperial)
                }
            },
            Weight = 30,
            Options = new List<EventOption>
            {
                new()
                {
                    Name = "释放它",
                    NameKey = "event.imprisoned_elf.option1.name",
                    Description = "精灵感谢你的帮助。选择一种元素获得永久强化。",
                    DescriptionKey = "event.imprisoned_elf.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.ChooseElfElementBlessing }
                    }
                },
                new()
                {
                    Name = "折磨它",
                    NameKey = "event.imprisoned_elf.option2.name",
                    Description = "获得【失智】Buff，获得【精灵尘】。",
                    DescriptionKey = "event.imprisoned_elf.option2.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.LostMind },
                        new() { Type = EventRewardType.Equipment, StringValue = EquipmentIds.ElfDust }
                    },
                    ResultText = "精灵的哀鸣在监牢里回荡。\n\n获得局外Buff：【失智】。\n获得装备：【精灵尘】。",
                    ResultTextKey = "event.imprisoned_elf.option2.result"
                },
                new()
                {
                    Name = "逼迫它",
                    NameKey = "event.imprisoned_elf.option3.name",
                    Description = "打开背包，选择一件装备，将其重铸为同品质的另一件装备（剧情/角色专属装备除外）。",
                    DescriptionKey = "event.imprisoned_elf.option3.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.ElfEquipmentReforge }
                    }
                },
                new()
                {
                    Name = "带走卖掉",
                    NameKey = "event.imprisoned_elf.option4.name",
                    Description = "获得500金币。",
                    DescriptionKey = "event.imprisoned_elf.option4.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Gold, Amount = 500 }
                    },
                    ResultText = "你将精灵交给了出价最高的人。\n\n获得500金币。",
                    ResultTextKey = "event.imprisoned_elf.option4.result"
                },
                new()
                {
                    Name = "治疗它",
                    NameKey = "event.imprisoned_elf.option5.name",
                    Description = "获得【精灵尘】。本局桃基础回复量永久+5。",
                    DescriptionKey = "event.imprisoned_elf.option5.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.CurrentCharacterOrPeachBaseHealAtLeast,
                            StringValue = CharacterIds.HuaTuo,
                            IntValue = 15
                        }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Equipment, StringValue = EquipmentIds.ElfDust },
                        new() { Type = EventRewardType.PeachBaseHealBonus, Amount = 5 }
                    },
                    ResultText = "精灵的气息逐渐平稳。\n它将一点闪烁的尘光交给你。\n\n获得装备：【精灵尘】。\n桃基础回复量永久+5。",
                    ResultTextKey = "event.imprisoned_elf.option5.result"
                }
            }
        };
    }

    // ————————————————————————————————————————————————————————————
    // 汉室宗祠：第三章皇宫路线稀有事件，拥有神秘芯片时本局必定出现一次。
    // ————————————————————————————————————————————————————————————
    /// <summary>
    /// Event System 的公开入口：CreateHanShiZongCiEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateHanShiZongCiEvent()
    {
        return new EventData
        {
            Id = "han_shi_zong_ci",
            AssetCode = "EV332001",
            Name = "汉室宗祠",
            Description = "无数牌位整齐排列。\n\n空气中弥漫着淡淡的香火。\n\n墙壁上刻着四个大字：\n\n「兴复汉室」",
            NameKey = "event.han_shi_zong_ci.name",
            DescriptionKey = "event.han_shi_zong_ci.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Rare,
            GuaranteedWhenOwnsEquipmentId = EquipmentIds.MysteriousChip,
            EventTags = new List<string> { "Chapter3", "ImperialRoute", "Accessory", "Curse", "SoulPossession" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 3
                },
                new()
                {
                    Type = EventConditionType.CurrentChapterRoute,
                    StringValue = nameof(ChapterRoute.Imperial)
                }
            },
            Weight = 18,
            Options = new List<EventOption>
            {
                new()
                {
                    Name = "进行祭拜",
                    NameKey = "event.han_shi_zong_ci.option1.name",
                    Description = "燃上香烛，向先贤行礼。获得随机史诗饰品×1。",
                    DescriptionKey = "event.han_shi_zong_ci.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.RandomEpicAccessory }
                    }
                },
                new()
                {
                    Name = "推倒牌位",
                    NameKey = "event.han_shi_zong_ci.option2.name",
                    Description = "将牌位扫落一地，搜刮供品与金银。获得300金币，获得3层诅咒。",
                    DescriptionKey = "event.han_shi_zong_ci.option2.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Gold, Amount = 300 },
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.Curse },
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.Curse },
                        new() { Type = EventRewardType.RunBuff, StringValue = RunBuffIds.Curse }
                    }
                },
                new()
                {
                    Name = "偷吃贡品",
                    NameKey = "event.han_shi_zong_ci.option3.name",
                    Description = "立即获得15点最大生命值。本局酒的增伤倍率永久增加0.5倍。",
                    DescriptionKey = "event.han_shi_zong_ci.option3.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.MaxHp, Amount = 15 },
                        new() { Type = EventRewardType.WineDamageMultiplierBonus }
                    }
                },
                new()
                {
                    Name = "夺舍灵魂",
                    NameKey = "event.han_shi_zong_ci.option4.name",
                    Description = "移除所有未受灵魂标记保护的技能，摧毁所有装备，随机获得传奇、史诗、稀有、普通技能各1个。",
                    DescriptionKey = "event.han_shi_zong_ci.option4.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.SoulPossession }
                    }
                },
                new()
                {
                    Name = "插入神秘芯片",
                    NameKey = "event.han_shi_zong_ci.option5.name",
                    Description = "失去神秘芯片，唤醒宗祠深处的祖灵回响，获得【先祖的赐福】。",
                    DescriptionKey = "event.han_shi_zong_ci.option5.desc",
                    Conditions = new List<EventCondition>
                    {
                        new()
                        {
                            Type = EventConditionType.OwnsEquipment,
                            StringValue = EquipmentIds.MysteriousChip
                        }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.RemoveSpecificEquipment, StringValue = EquipmentIds.MysteriousChip },
                        new() { Type = EventRewardType.AncestorBlessing }
                    },
                    ResultText = "神秘芯片嵌入宗祠中央的裂缝。\n牌位后的墙壁亮起细密纹路，香火凝成金色人影。\n\n你听见无数声音低声诵念：\n「汉祚未绝。」\n\n获得【先祖的赐福 ×3】。",
                    ResultTextKey = "event.han_shi_zong_ci.option5.result"
                }
            }
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 鼠王：第二章下水道稀有事件，RunOnce
    // ──────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Event System 的公开入口：CreateRatKingEventData。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateRatKingEventData()
    {
        return new EventData
        {
            Id = "rat_king_event",
            AssetCode = "EV222001",
            Name = "鼠王",
            Description = "下水道深处。\n一群老鼠围绕着一张腐朽的餐桌。\n其中一只体型巨大的老鼠正端坐在中央。\n它手持一根古老的权杖。\n\n看见你的到来，它露出笑容，向你招了招手。\n\n「一起用餐吧。」",
            NameKey = "event.rat_king_event.name",
            DescriptionKey = "event.rat_king_event.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Rare,
            EventTags = new List<string> { "Chapter2", "SewerRoute", "Combat", "RatKing" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 2
                },
                new()
                {
                    Type = EventConditionType.CurrentChapterRoute,
                    StringValue = ChapterRoute.Sewer.ToString()
                }
            },
            Weight = 40,
            Options = new List<EventOption>
            {
                // ① 一起用餐：失去10点最大生命，获得250金币
                new()
                {
                    Name = "一起用餐",
                    NameKey = "event.rat_king_event.option1.name",
                    Description = "接受邀请，与鼠王共进晚餐。\n（永久失去10点最大生命值，获得250金币）",
                    DescriptionKey = "event.rat_king_event.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.MaxHp, Amount = -10 },
                        new() { Type = EventRewardType.Gold, Amount = 250 }
                    },
                    ResultText = "餐桌上的食物比想象中更美味。\n你们相谈甚欢。\n临别时，鼠王慷慨地向你赠送了金币。\n获得：250金币。\n最大生命值-10。",
                    ResultTextKey = "event.rat_king_event.option1.result"
                },
                // ② 献上礼物：失去200金币，随机销毁稀有+装备，获得更高品质装备
                new()
                {
                    Name = "献上礼物",
                    NameKey = "event.rat_king_event.option2.name",
                    Description = "向鼠王献上一份礼物。\n（失去200金币，随机摧毁一件稀有及以上装备，获得高一品质的随机装备）",
                    DescriptionKey = "event.rat_king_event.option2.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Gold, Amount = 200 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.RatKingOfferGift }
                    },
                    ResultText = string.Empty
                },
                // ③ 攻击鼠王：进入特殊战斗
                new()
                {
                    Name = "攻击鼠王",
                    NameKey = "event.rat_king_event.option3.name",
                    Description = "直接发起攻击。\n（进入特殊战斗：鼠王 + 机械巨鼠×2）\n\n【特殊机制】战斗中累计使用3张桃，鼠王停止战斗，赠送【瘟疫权杖】。",
                    DescriptionKey = "event.rat_king_event.option3.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.StartRatKingEventBattle }
                    },
                    ResultText = "你握紧武器，向鼠王发起冲锋！",
                    ResultTextKey = "event.rat_king_event.option3.result"
                }
            }
        };
    }

    /// <summary>
    /// Event System 的公开入口：CreateStinkyMushroomEventData。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateStinkyMushroomEventData()
    {
        return new EventData
        {
            Id = "event_stinky_mushroom",
            AssetCode = "EV222002",
            Name = "恶臭蘑菇",
            Description = "你发现了一大片绿色的蘑菇。\n黏稠的菌丝缠绕着整个下水道，不断散发着令人作呕的恶臭瘴气。\n菌盖深处闪烁着红、黄、绿三种不同颜色的微光。",
            NameKey = "event.event_stinky_mushroom.name",
            DescriptionKey = "event.event_stinky_mushroom.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Rare,
            EventTags = new List<string> { "Chapter2", "SewerRoute", "Equipment", "StinkyMushroom" },
            StageRequirement = new EventStageRequirement
            {
                MinStage = 1,
                MaxStage = GameManager.TotalLevels,
                StageTag = EventStageTag.Any
            },
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 2
                },
                new()
                {
                    Type = EventConditionType.CurrentChapterRoute,
                    StringValue = ChapterRoute.Sewer.ToString()
                }
            },
            Weight = 40,
            Options = new List<EventOption>
            {
                // ① 强行采摘：失去20点电量。
                new()
                {
                    Name = "强行采摘",
                    NameKey = "event.event_stinky_mushroom.option1.name",
                    Description = "失去20点电量。",
                    DescriptionKey = "event.event_stinky_mushroom.option1.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.Power, Amount = 20 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.StinkyMushroomEquipmentForOption, Amount = 0 }
                    }
                },
                // ② 以血培菌：永久失去10点最大生命值。
                new()
                {
                    Name = "以血培菌",
                    NameKey = "event.event_stinky_mushroom.option2.name",
                    Description = "永久失去10点最大生命值。",
                    DescriptionKey = "event.event_stinky_mushroom.option2.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.MaxHealth, Amount = 10 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.StinkyMushroomEquipmentForOption, Amount = 1 }
                    }
                },
                // ③ 芯片喂养：随机失去1点芯片储备。
                new()
                {
                    Name = "芯片喂养",
                    NameKey = "event.event_stinky_mushroom.option3.name",
                    Description = "随机失去1点芯片储备。",
                    DescriptionKey = "event.event_stinky_mushroom.option3.desc",
                    Costs = new List<EventCost>
                    {
                        new() { Type = EventCostType.RandomChip, Amount = 0 }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.StinkyMushroomEquipmentForOption, Amount = 2 }
                    }
                }
            }
        };
    }

    // ————————————————————————————————————————
    // 爆炸果实（稀有，第二章·城市/下水道均可出现，整局仅一次，无结果页事件）
    // ————————————————————————————————————————
    // 所有后果直接写在选项描述中，选择后立即返回流程，不显示额外结果页。选项一不使用
    // EventCostType.CurrentHP（那会因为"生命不足"被禁用而无法致命），
    // 而是用专属的 ExplosiveFruitTouchDamage 奖励类型，让它始终可选，扣血致死时按项目
    // 既有的"战败消耗粮草复活/粮草耗尽游戏结束"规则处理（GameManager.ApplyExplosiveFruitTouchDamage）。
    /// <summary>
    /// Event System 的公开入口：CreateExplosiveFruitEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EventData CreateExplosiveFruitEvent()
    {
        return new EventData
        {
            Id = "explosive_fruit",
            AssetCode = "EV212004",
            Name = "爆炸果实",
            Description = "路边有一颗红色的巨大果实，好像一碰就会迸发出巨大的能量。",
            NameKey = "event.explosive_fruit.name",
            DescriptionKey = "event.explosive_fruit.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Rare,
            // 描述已完整展示在选项中，结算后不额外进入结果文本页。
            SuppressResultPresentation = true,
            Weight = 20,
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 2
                }
            },
            Options = new List<EventOption>
            {
                // 选项一：碰一下（-50当前生命；致死时消耗粮草并回满；获得普通饰品槽）
                new()
                {
                    Name = "碰一下",
                    NameKey = "event.explosive_fruit.option1.name",
                    Description = "你被炸了一个大洞！扣除50点当前生命值；若死亡则消耗1点粮草并回满生命值。获得一个普通饰品槽位（只能放置普通饰品）。",
                    DescriptionKey = "event.explosive_fruit.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.ExplosiveFruitTouchDamage, Amount = 50 },
                        new() { Type = EventRewardType.ExplosiveFruitAccessorySlot }
                    }
                },
                // 选项二：远处射箭激发果实（仅在拥有万箭齐发时显示；永久强化万箭齐发为火属性）
                new()
                {
                    Name = "远处射箭激发果实",
                    NameKey = "event.explosive_fruit.option2.name",
                    Description = "你的万箭齐发现在可以享受火属性加成。",
                    DescriptionKey = "event.explosive_fruit.option2.desc",
                    Conditions = new List<EventCondition>
                    {
                        new() { Type = EventConditionType.HasPlayerCardType, StringValue = "ArrowBarrage" }
                    },
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.ExplosiveFruitArrowBarrageFireUpgrade }
                    }
                },
                // 选项三：小心绕行（最大电量+15，当前电量-10）
                new()
                {
                    Name = "小心绕行",
                    NameKey = "event.explosive_fruit.option3.name",
                    Description = "获得15点电量上限，失去10点电量。",
                    DescriptionKey = "event.explosive_fruit.option3.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.MaxPowerBonus, Amount = 15 },
                        new() { Type = EventRewardType.PowerGain, Amount = -10 }
                    }
                },
                // 选项四：进行分析（+1知识芯片；项目当前无芯片容量上限，因此始终可选）
                new()
                {
                    Name = "进行分析",
                    NameKey = "event.explosive_fruit.option4.name",
                    Description = "获得一个知识芯片。",
                    DescriptionKey = "event.explosive_fruit.option4.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.KnowledgeChipInstall }
                    }
                },
                // 选项五：掌握反应（获得火攻）
                new()
                {
                    Name = "掌握反应",
                    NameKey = "event.explosive_fruit.option5.name",
                    Description = "获得火攻。",
                    DescriptionKey = "event.explosive_fruit.option5.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.AddPlayerCardType, StringValue = nameof(CardType.FireAttack) }
                    }
                }
            }
        };
    }

    // ————————————————————————————————————————
    // 第四章・深渊呼唤与元素祭坛
    // ————————————————————————————————————————
    public static EventData CreateAbyssCallEvent()
    {
        return new EventData
        {
            Id = "abyss_call",
            AssetCode = "EV412001",
            Name = "深渊呼唤",
            Description = "深渊深处传来低沉的呼唤，像是某种意识正在等待回应。",
            NameKey = "event.abyss_call.name",
            DescriptionKey = "event.abyss_call.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            Weight = 20,
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 4
                }
            },
            Options = new List<EventOption>
            {
                new()
                {
                    Name = "回应呼唤",
                    NameKey = "event.abyss_call.option1.name",
                    Description = "最大生命值降低30%，获得一个技能芯片。",
                    DescriptionKey = "event.abyss_call.option1.desc",
                    RewardSequence =
                    {
                        new LoseMaxHpPercentRewardAction(30),
                        new AddEquipmentRewardAction(EquipmentIds.SkillChip)
                    }
                },
                new()
                {
                    Name = "狂热加入",
                    NameKey = "event.abyss_call.option2.name",
                    Description = "失去所有芯片，获得一次技能二选一机会。",
                    DescriptionKey = "event.abyss_call.option2.desc",
                    RewardSequence =
                    {
                        new RemoveAllChipsRewardAction(),
                        new OpenSkillChoiceRewardAction()
                    }
                },
                new()
                {
                    Name = "抛弃所有",
                    NameKey = "event.abyss_call.option3.name",
                    Description = "抛弃所有装备，获得一次技能二选一机会。",
                    DescriptionKey = "event.abyss_call.option3.desc",
                    RewardSequence =
                    {
                        new DestroyAllEquipmentRewardAction(),
                        new OpenSkillChoiceRewardAction()
                    }
                },
                new()
                {
                    Name = "坚定意志",
                    NameKey = "event.abyss_call.option4.name",
                    Description = "失去10点当前生命值。",
                    DescriptionKey = "event.abyss_call.option4.desc",
                    RewardSequence = { new LoseCurrentHpRewardAction(10) }
                }
            }
        };
    }

    public static EventData CreateElementalAltarEvent()
    {
        return new EventData
        {
            Id = "elemental_altar",
            AssetCode = "EV414001",
            Name = "元素祭坛",
            Description = "断裂的祭坛仍在吞吐元素辉光，仿佛能为你的攻击写下新的属性。",
            NameKey = "event.elemental_altar.name",
            DescriptionKey = "event.elemental_altar.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.RunOnce,
            IsRepeatable = false,
            Rarity = EventRarity.Common,
            Weight = 20,
            Conditions = new List<EventCondition>
            {
                new()
                {
                    Type = EventConditionType.CurrentChapter,
                    Comparison = EventComparison.Equal,
                    IntValue = 4
                }
            },
            Options = new List<EventOption>
            {
                new()
                {
                    Name = "召唤冰元素",
                    NameKey = "event.elemental_altar.option1.name",
                    Description = "往出牌栏添加【冰杀】。",
                    DescriptionKey = "event.elemental_altar.option1.desc",
                    RewardSequence = { new AddPlayerCardRewardAction(CardType.IceKill) }
                },
                new()
                {
                    Name = "召唤毒素",
                    NameKey = "event.elemental_altar.option2.name",
                    Description = "往出牌栏添加【毒杀】。",
                    DescriptionKey = "event.elemental_altar.option2.desc",
                    RewardSequence = { new AddPlayerCardRewardAction(CardType.PoisonKill) }
                },
                new()
                {
                    Name = "召唤火焰",
                    NameKey = "event.elemental_altar.option3.name",
                    Description = "本局所有攻击性牌获得火属性词条，并享受火属性伤害加成。",
                    DescriptionKey = "event.elemental_altar.option3.desc",
                    RewardSequence = { new AddPlayerAttackAttributesRewardAction(AttackAttribute.Fire) }
                },
                new()
                {
                    Name = "召唤雷电",
                    NameKey = "event.elemental_altar.option4.name",
                    Description = "本局所有攻击性牌获得雷属性词条，并享受雷属性伤害加成。",
                    DescriptionKey = "event.elemental_altar.option4.desc",
                    RewardSequence = { new AddPlayerAttackAttributesRewardAction(AttackAttribute.Thunder) }
                },
                new()
                {
                    Name = "放上月亮宝石",
                    NameKey = "event.elemental_altar.option5.name",
                    Description = "月亮宝石进化：天体撞击获得火、雷、毒、冰全部元素词条。",
                    DescriptionKey = "event.elemental_altar.option5.desc",
                    Conditions = new List<EventCondition>
                    {
                        new() { Type = EventConditionType.OwnsEquipment, StringValue = EquipmentIds.MoonGem }
                    },
                    RewardSequence = { new AwakenMoonGemElementsRewardAction() }
                }
            }
        };
    }

    // ————————————————————————————————————————
    // 【路边补给】（整合式教程专用事件）
    // ————————————————————————————————————————
    // 只挂在 GameManager.BuildTutorialMap 建的教学事件节点上（该节点的
    // MapNode.FixedEventId 直接写死这个事件Id）。Category=Common（不能用
    // Special——CanUseEvent 对 Event 类型节点会直接拒绝 Special 分类，
    // 连 FixedEventId 命中也会被挡下、退化成随机池），真正防泄漏靠
    // Conditions 里的 TutorialIntegratedActive：只有整合式教程处于激活状态时
    // CanUseEvent 才会通过，教程结束/跳过后这个事件对正式第一章的随机事件池
    // 永远不可见。
    public static EventData CreateTutorialRoadsideSupplyEvent()
    {
        return new EventData
        {
            Id = "event_tutorial_roadside_supply",
            AssetCode = "EV11T001",
            Name = "路边补给",
            Description = "你发现了一处尚未被搜刮的补给点。",
            NameKey = "event.event_tutorial_roadside_supply.name",
            DescriptionKey = "event.event_tutorial_roadside_supply.desc",
            Category = EventCategory.Common,
            RepeatType = EventRepeatType.Repeatable,
            Rarity = EventRarity.Common,
            Conditions = new List<EventCondition>
            {
                new() { Type = EventConditionType.TutorialIntegratedActive }
            },
            Options = new List<EventOption>
            {
                new()
                {
                    Name = "拿走金币",
                    NameKey = "event.event_tutorial_roadside_supply.option1.name",
                    Description = "获得30金币。",
                    DescriptionKey = "event.event_tutorial_roadside_supply.option1.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Gold, Amount = 30 }
                    },
                    ResultText = "你拿走了补给点里的金币。",
                    ResultTextKey = "event.event_tutorial_roadside_supply.option1.result"
                },
                new()
                {
                    Name = "使用补给包扎",
                    NameKey = "event.event_tutorial_roadside_supply.option2.name",
                    Description = "恢复10点生命值。",
                    DescriptionKey = "event.event_tutorial_roadside_supply.option2.desc",
                    Rewards = new List<EventReward>
                    {
                        new() { Type = EventRewardType.Heal, Amount = 10 }
                    },
                    ResultText = "你用补给包扎了伤口，恢复了一些生命值。",
                    ResultTextKey = "event.event_tutorial_roadside_supply.option2.result"
                }
            }
        };
    }
}
