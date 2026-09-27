//////////////////////////////////////////////////////////
// 文件：Scripts/InitialEventPool.cs
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
/// Event System 的公开类：InitialEventPool。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class InitialEventPool
{
    public static readonly IReadOnlyList<InitialEventDefinition> AllEntries = new List<InitialEventDefinition>
    {
        // ① Tag A: 代价=战斗结束不回血；效果=第二章开始时普通/稀有技能三选一
        new InitialEventDefinition
        {
            Id = "ie_01",
            Tag = "A",
            Description = "进入第二章时，获得一次技能三选一机会（普通/稀有）。\n代价：每场战斗结束后无法自动恢复生命值。",
            DescriptionKey = "initial_event.ie_01.desc",
            EffectDetail = "代价：每场战斗结束后无法自动恢复生命值（米浆除外）。\n效果：进入第二章时，从随机普通/稀有技能中3选1。",
            EffectDetailKey = "initial_event.ie_01.detail",
            Apply = () =>
            {
                GameManager.SetInitialEventNoBattleEndRecovery();
                GameManager.InitialEventCh2SkillPick = true;
            }
        },
        // ② Tag A: 效果=前5件装备自动销毁→传奇二选一
        new InitialEventDefinition
        {
            Id = "ie_02",
            Tag = "A",
            Description = "本局前5件获得的装备将自动销毁。第5件销毁后，获得传奇装备二选一。",
            DescriptionKey = "initial_event.ie_02.desc",
            EffectDetail = "效果：获得任意装备时自动销毁（计数）。\n累计销毁5件后，立即触发传奇装备二选一奖励。",
            EffectDetailKey = "initial_event.ie_02.detail",
            Apply = () =>
            {
                GameManager.SetInitialEventAutoDestroyEquip();
            }
        },
        // ③ Tag A: 代价=所有传奇敌人+30%HP；效果=击败第一章Boss后随机获得当前Boss的一个技能
        new InitialEventDefinition
        {
            Id = "ie_03",
            Tag = "A",
            AdditionalTags = new List<string> { "E" },
            Description = "击败第一章Boss后，随机获得该Boss拥有的一个技能（若已拥有Boss的全部技能，则改为随机获得一个稀有技能）。\n代价：所有传奇敌人最大生命值+30%。",
            DescriptionKey = "initial_event.ie_03.desc",
            EffectDetail = "代价：所有传奇敌人的最大生命值永久提升30%。\n效果：击败第一章Boss时，随机获得当前Boss实际拥有的一个技能；若Boss的技能已经全部拥有，则改为随机获得一个稀有技能。",
            EffectDetailKey = "initial_event.ie_03.detail",
            Apply = () =>
            {
                GameManager.SetInitialEventLegendaryEnemyHpBonus();
                GameManager.InitialEventCh1BossSkillPick = true;
            }
        },
        // ④ Tag B: 芯片三选一有5%出现神秘芯片，扩容与技能芯片概率各提高至10%。
        new InitialEventDefinition
        {
            Id = "ie_04",
            Tag = "B",
            Description = "芯片三选一有5%概率出现神秘芯片；扩容芯片与技能芯片出现概率提高至10%。",
            DescriptionKey = "initial_event.ie_04.desc",
            EffectDetail = "效果：本局所有芯片三选一中，神秘芯片替换普通芯片的概率为5%；扩容芯片与技能芯片替换普通芯片的概率各为10%。",
            EffectDetailKey = "initial_event.ie_04.detail",
            Apply = () =>
            {
                GameManager.SetInitialEventChipChoiceEnhancement();
            }
        },
        // ⑤ Tag B: +15最大HP+100金
        new InitialEventDefinition
        {
            Id = "ie_05",
            Tag = "B",
            Description = "永久增加15点最大生命值，并获得100金币。",
            DescriptionKey = "initial_event.ie_05.desc",
            EffectDetail = "效果：立即永久+15最大生命值（当前HP同步提升），并获得100金币。",
            EffectDetailKey = "initial_event.ie_05.detail",
            Apply = () =>
            {
                GameManager.AddMaxHp(15);
                GameManager.AddGold(100);
            }
        },
        // ⑥ Tag B: +1随机稀有装备+50金
        new InitialEventDefinition
        {
            Id = "ie_06",
            Tag = "B",
            Description = "立即获得50金币，并从三件随机稀有装备中选择一件。",
            DescriptionKey = "initial_event.ie_06.desc",
            EffectDetail = "效果：立即获得50金币。\n进入稀有装备三选一界面，从随机生成的三件稀有装备中选择一件加入背包。",
            EffectDetailKey = "initial_event.ie_06.detail",
            Apply = () =>
            {
                GameManager.AddGold(50);
                GameManager.InitialEventIe06RarePending = true;
            }
        },
        // ⑦ 无标签：所有商店变为黑市，且不收取黑市溢价
        new InitialEventDefinition
        {
            Id = "ie_07",
            Tag = null,
            Description = "本局所有商店变为黑市，且其中商品不收取黑市溢价。",
            DescriptionKey = "initial_event.ie_07.desc",
            EffectDetail = "效果：本局所有商店使用黑市商品池（含传奇装备），但商品价格按普通商店价格结算。",
            EffectDetailKey = "initial_event.ie_07.detail",
            Apply = () =>
            {
                GameManager.SetInitialEventAllShopsBlackMarket();
            }
        },
        // ⑧ 无标签：每商店刷新次数+1，且所有刷新免费
        new InitialEventDefinition
        {
            Id = "ie_08",
            Tag = null,
            Description = "每次进入商店可额外刷新一次，且本局所有刷新免费。",
            DescriptionKey = "initial_event.ie_08.desc",
            EffectDetail = "效果：每次进入商店可刷新2次，且两次刷新均不消耗金币。",
            EffectDetailKey = "initial_event.ie_08.detail",
            Apply = () =>
            {
                GameManager.AddInitialEventExtraShopRefresh();
                GameManager.SetInitialEventShopRefreshFree();
            }
        },
        // ⑨ Tag D: -10最大HP；每场战斗胜利后+5最大HP（永久）
        new InitialEventDefinition
        {
            Id = "ie_09",
            Tag = "D",
            Description = "每场战斗胜利后，永久获得5点最大生命值。\n代价：立即失去10点最大生命值。",
            DescriptionKey = "initial_event.ie_09.desc",
            EffectDetail = "代价：立即失去10点最大生命值。\n效果：每场战斗胜利后，永久+5最大生命值（可叠加）。",
            EffectDetailKey = "initial_event.ie_09.detail",
            Apply = () =>
            {
                GameManager.AddMaxHp(-10);
                GameManager.SetInitialEventBattleEndMaxHpGain(5);
            }
        },
        // ⑩ Tag D: 替换火杀+雷杀→火雷杀（牌库），今后获得的不受影响
        new InitialEventDefinition
        {
            Id = "ie_10",
            Tag = "D",
            Description = "移除牌库中的火杀和雷杀，获得火雷杀。今后获得的火杀/雷杀不受影响。",
            DescriptionKey = "initial_event.ie_10.desc",
            EffectDetail = "效果：将起始牌库中的火杀和雷杀类型全部替换为火雷杀。\n之后通过任何途径获得的火杀/雷杀不受此影响。",
            EffectDetailKey = "initial_event.ie_10.detail",
            Apply = () =>
            {
                GameManager.RemovePlayerCardType(CardType.FireKill);
                GameManager.RemovePlayerCardType(CardType.ThunderKill);
                GameManager.AddPlayerCardType(CardType.FireThunderKill);
            }
        },
        // ⑪ Tag D/E: -全部初始金币；第一章Boss奖励→扩容芯片
        new InitialEventDefinition
        {
            Id = "ie_11",
            Tag = "D",
            AdditionalTags = new List<string> { "E" },
            Description = "击败第一章Boss后，奖励替换为扩容芯片（永久解锁万能装备槽）。\n代价：失去全部初始金币。",
            DescriptionKey = "initial_event.ie_11.desc",
            EffectDetail = "代价：立即失去全部初始金币。\n效果：击败第一章Boss时，奖励替换为扩容芯片（解锁第四装备槽）。",
            EffectDetailKey = "initial_event.ie_11.detail",
            Apply = () =>
            {
                GameManager.AddGold(-GameManager.Gold);
                GameManager.SetInitialEventCh1BossExpansionChip();
            }
        },
        // ⑫ null: 最大电量+25（永久，每章开始都会恢复到这个新上限）
        new InitialEventDefinition
        {
            Id = "ie_12",
            Tag = null,
            Description = "最大电量+25。",
            DescriptionKey = "initial_event.ie_12.desc",
            EffectDetail = "效果：本局永久提高最大电量25点（100→125）。每章开始时电量都会恢复至这个新的上限。",
            EffectDetailKey = "initial_event.ie_12.detail",
            Apply = () =>
            {
                GameManager.AddInitialEventMaxPowerBonus(25);
            }
        },
        // ⑬ Tag B: 黑暗RunBuff+知识芯片×4
        new InitialEventDefinition
        {
            Id = "ie_13",
            Tag = "B",
            Description = "获得4个知识芯片（锦囊伤害+20），本章所有敌人最大生命+30%。",
            DescriptionKey = "initial_event.ie_13.desc",
            EffectDetail = "效果：立即获得4个知识芯片（每个使锦囊类技能伤害+5）。\n本章内所有敌人最大生命值+30%（黑暗，仅限1场战斗）。",
            EffectDetailKey = "initial_event.ie_13.detail",
            Apply = () =>
            {
                RunBuffManager.Add(RunBuffIds.Darkness);
                GameManager.IncrementKnowledgeChipCount();
                GameManager.IncrementKnowledgeChipCount();
                GameManager.IncrementKnowledgeChipCount();
                GameManager.IncrementKnowledgeChipCount();
            }
        },
        // ⑭ null: 前3场战斗0累积伤害→史诗三选一；任何伤害→立即失效
        new InitialEventDefinition
        {
            Id = "ie_14",
            Tag = null,
            Description = "前3场战斗零累积伤害，则获得史诗装备三选一。受到任何伤害时立即失效。",
            DescriptionKey = "initial_event.ie_14.desc",
            EffectDetail = "效果：追踪前3场战斗是否受到伤害。\n全程0伤害 → 奖励史诗装备三选一。\n任意战斗受到伤害 → 立即失效，不再有奖励。",
            EffectDetailKey = "initial_event.ie_14.detail",
            Apply = () =>
            {
                GameManager.SetInitialEventNoDamageTrack();
            }
        },
        // ⑮ Tag F: 立即获得史诗装备【瘫痪装置】
        new InitialEventDefinition
        {
            Id = "ie_15",
            Tag = "F",
            Description = "立即获得史诗饰品【瘫痪装置】。",
            DescriptionKey = "initial_event.ie_15.desc",
            EffectDetail = "效果：立即将史诗饰品【瘫痪装置】加入背包。",
            EffectDetailKey = "initial_event.ie_15.detail",
            Apply = () =>
            {
                GameManager.AddEquipment(EquipmentIds.ParalysisDevice, EquipmentGainSource.InitialEvent);
            }
        },
        // ⑯ null: 第一战→精英替换；第一章event_2→随机第二章事件
        new InitialEventDefinition
        {
            Id = "ie_16",
            Tag = null,
            Description = "第一场战斗替换为随机精英遭遇；第一章第一个事件替换为随机第二章事件。",
            DescriptionKey = "initial_event.ie_16.desc",
            EffectDetail = "效果①：第一场战斗的敌人组合替换为精英遭遇（随机第一章精英战内容）。\n效果②：第一章第一个普通事件替换为随机第二章事件（允许城市/下水道，排除商店/Boss专属/限定事件）。",
            EffectDetailKey = "initial_event.ie_16.detail",
            Apply = () =>
            {
                GameManager.SetInitialEventFirstBattleElite();
                GameManager.SetInitialEventCh1Event2Ch2();
            }
        },
        // ⑰ null: +75金；每场战斗结束→满血回复
        new InitialEventDefinition
        {
            Id = "ie_17",
            Tag = null,
            Description = "立即获得75金币。每场战斗结束后，自动恢复至满血。",
            DescriptionKey = "initial_event.ie_17.desc",
            EffectDetail = "效果：立即获得75金币。\n每场战斗结束后，生命值完全恢复至上限。",
            EffectDetailKey = "initial_event.ie_17.detail",
            Apply = () =>
            {
                GameManager.AddGold(75);
                GameManager.SetInitialEventBattleEndFullHeal();
            }
        },
        // ⑱ Tag F: 装备升阶
        new InitialEventDefinition
        {
            Id = "ie_18",
            Tag = "F",
            Description = "正式进入1-1后，获得的第一件可提升品质装备将替换为同类型高一阶随机装备。",
            DescriptionKey = "initial_event.ie_18.desc",
            EffectDetail = "效果：正式进入1-1后，第一件可提升品质的装备替换为同主槽位类型、高一级品质的随机装备。\n普通→稀有，稀有→史诗，史诗→传奇；传奇或无候选时继续等待。",
            EffectDetailKey = "initial_event.ie_18.detail",
            Apply = () =>
            {
                GameManager.SetInitialEventUpgradeFirstEquip();
            }
        },
        // ⑲ Tag B: 血肉献祭
        new InitialEventDefinition
        {
            Id = "ie_19",
            Tag = "B",
            Description = "+5诅咒层数，永久增加35点最大生命值。",
            DescriptionKey = "initial_event.ie_19.desc",
            EffectDetail = "代价：立即获得5层诅咒（累计降低出牌效果）。\n效果：永久+35最大生命值。",
            EffectDetailKey = "initial_event.ie_19.detail",
            Apply = () =>
            {
                RewardManager.Execute(new RewardSequence
                {
                    new AddRunBuffRewardAction(RunBuffIds.Curse, 5),
                    new AddMaxHpRewardAction(35)
                });
            }
        },
        // ⑳ Tag B: 贪婪契约
        new InitialEventDefinition
        {
            Id = "ie_20",
            Tag = "B",
            Description = "+5诅咒层数，立即获得450金币。",
            DescriptionKey = "initial_event.ie_20.desc",
            EffectDetail = "代价：立即获得5层诅咒（累计降低出牌效果）。\n效果：立即获得450金币。",
            EffectDetailKey = "initial_event.ie_20.detail",
            Apply = () =>
            {
                RewardManager.Execute(new RewardSequence
                {
                    new AddRunBuffRewardAction(RunBuffIds.Curse, 5),
                    new AddGoldRewardAction(450)
                });
            }
        },
        // ㉑ null: 财富契约——初始金币变为-50，专属装备三选一（大亨之铠/投机者之刃/收割者）
        new InitialEventDefinition
        {
            Id = "ie_21",
            Tag = null,
            Description = "初始金币变为-50。立即从【大亨之铠】【投机者之刃】【收割者】中选择一件装备。",
            DescriptionKey = "initial_event.ie_21.desc",
            EffectDetail = "代价：初始金币直接设为-50（允许负数，需先赚钱还债）。\n效果：立即从三件专属稀有装备中三选一：\n【大亨之铠】+10最大生命；每场战斗第1/5/10次受伤掉落50金币。\n【投机者之刃】杀类型伤害+2；每场战斗第1/5/10次杀命中获得30金币。\n【收割者】敌人生命≤10%时下一次伤害直接斩杀；斩杀获得25金币。",
            EffectDetailKey = "initial_event.ie_21.detail",
            Apply = () =>
            {
                // 直接设为-50：先清到0再减50，统一走 AddGold 接口（已支持负数）。
                GameManager.AddGold(-GameManager.Gold - 50);
                GameManager.InitialEventFortuneContractPending = true;
            }
        },
        // ㉒ null: 从三张特殊锦囊中选择一张加入出牌栏
        new InitialEventDefinition
        {
            Id = "ie_22",
            Tag = null,
            Description = "从【万箭齐发】【南蛮入侵】【铁索连环】中选择一张加入出牌栏。",
            DescriptionKey = "initial_event.ie_22.desc",
            EffectDetail = "效果：立即从【万箭齐发】【南蛮入侵】【铁索连环】中三选一，所选牌永久加入本局出牌栏。",
            EffectDetailKey = "initial_event.ie_22.detail",
            Apply = () => GameManager.InitialEventAttackTrickChoicePending = true
        },
        // ㉓ Tag F: 立即获得【重铸器】
        new InitialEventDefinition
        {
            Id = "ie_23",
            Tag = "F",
            Description = "立即获得史诗饰品【重铸器】。",
            DescriptionKey = "initial_event.ie_23.desc",
            EffectDetail = "效果：立即将史诗饰品【重铸器】加入背包。卖出它后，你下一个卖出的装备会变为另一件同品质装备。",
            EffectDetailKey = "initial_event.ie_23.detail",
            Apply = () => GameManager.AddEquipment(EquipmentIds.Reforger, EquipmentGainSource.InitialEvent)
        },
        // ㉔ 无标签：初始费用+1
        new InitialEventDefinition
        {
            Id = "ie_24",
            Tag = null,
            Description = "本局每场战斗的初始费用+1。",
            DescriptionKey = "initial_event.ie_24.desc",
            EffectDetail = "效果：本局每场战斗开始时，初始费用永久+1。",
            EffectDetailKey = "initial_event.ie_24.detail",
            Apply = () => GameManager.AddInitialEventStartingMana(1)
        },
    };
}
