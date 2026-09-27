//////////////////////////////////////////////////////////
// 文件：Scripts/EquipmentDatabase.cs
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
/// Core System 的公开类：EquipmentDatabase。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class EquipmentDatabase
{
    public const int ArmorSlotCount = 1;
    public const int WeaponSlotCount = 1;
    public const int VehicleSlotCount = 1;
    public const int AccessorySlotCount = 3;

    private static readonly Dictionary<string, EquipmentDefinition> Definitions = new()
    {
        // ————————————————————————————————————————————————
        // 板甲：商店普通护甲，装备后立即获得15点生命值
        // ————————————————————————————————————————————————
        [EquipmentIds.BanJia] = new EquipmentDefinition(
            EquipmentIds.BanJia,
            "板甲",
            "厚重的钢铁护甲，可以有效保护穿戴者。\n装备后立即获得15点生命值。",
            new[] { EquipmentType.Buff, EquipmentType.Armor, EquipmentType.Defense },
            EquipmentRarity.Common,
            new[]
            {
                new EquipmentEffect("装备后立即获得15点生命值。", 150)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "armor", "defense", "on_acquire", "heal" },
            150,
            nameKey: "equipment.armor_ban_jia.name",
            descriptionKey: "equipment.armor_ban_jia.desc",
            assetCode: "EQ20001"),

        [EquipmentIds.SilverLion] = new EquipmentDefinition(
            EquipmentIds.SilverLion,
            "白银狮子",
            "获得20最大生命。本局第一次死亡时，生命重置为10，仅触发一次。",
            new[] { EquipmentType.Buff, EquipmentType.Armor, EquipmentType.Defense },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("获得20最大生命。", 200),
                new EquipmentEffect("本局第一次死亡时生命重置为10，仅触发一次。", 500)
            },
            EquipmentAcquisitionMethod.Reward,
            System.Array.Empty<string>(),
            new[] { "armor", "survival", "revive", "once" },
            500,
            nameKey: "equipment.silver_lion.name",
            descriptionKey: "equipment.silver_lion.desc",
            assetCode: "EQ20002"),

        [EquipmentIds.RustShield] = new EquipmentDefinition(
            EquipmentIds.RustShield,
            "锈盾",
            "获得10最大生命。",
            new[] { EquipmentType.Buff, EquipmentType.Armor, EquipmentType.Defense },
            EquipmentRarity.Common,
            new[]
            {
                new EquipmentEffect("获得10最大生命。", 200)
            },
            EquipmentAcquisitionMethod.Reward,
            System.Array.Empty<string>(),
            new[] { "armor", "hp" },
            200,
            nameKey: "equipment.rust_shield.name",
            descriptionKey: "equipment.rust_shield.desc",
            assetCode: "EQ20003"),

        [EquipmentIds.BenevolentKing] = new EquipmentDefinition(
            EquipmentIds.BenevolentKing,
            "仁王",
            "本局第一次受到伤害时免疫该次伤害，仅触发一次。",
            new[] { EquipmentType.Buff, EquipmentType.Armor, EquipmentType.Defense },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("本局第一次受到伤害时免疫该次伤害，仅触发一次。", 500)
            },
            EquipmentAcquisitionMethod.Reward,
            System.Array.Empty<string>(),
            new[] { "armor", "immunity", "once" },
            500,
            nameKey: "equipment.benevolent_king.name",
            descriptionKey: "equipment.benevolent_king.desc",
            assetCode: "EQ20004"),

        [EquipmentIds.RustBlueSteelSword] = new EquipmentDefinition(
            EquipmentIds.RustBlueSteelSword,
            "青钢剑-锈",
            "所有杀系攻击伤害+5。包含杀、火杀、雷杀、必中杀。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Common,
            new[]
            {
                new EquipmentEffect("所有杀系攻击伤害+5。", 200)
            },
            EquipmentAcquisitionMethod.Reward,
            System.Array.Empty<string>(),
            new[] { "weapon", "sha_series", "flat_bonus" },
            200,
            nameKey: "equipment.rust_blue_steel_sword.name",
            descriptionKey: "equipment.rust_blue_steel_sword.desc",
            assetCode: "EQ10001"),

        [EquipmentIds.BlueSteelSword] = new EquipmentDefinition(
            EquipmentIds.BlueSteelSword,
            "青钢剑",
            "所有杀系攻击伤害+8。包含杀、火杀、雷杀、必中杀。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("所有杀系攻击伤害+8。", 200)
            },
            EquipmentAcquisitionMethod.Reward,
            System.Array.Empty<string>(),
            new[] { "weapon", "sha_series", "flat_bonus" },
            300,
            nameKey: "equipment.blue_steel_sword.name",
            descriptionKey: "equipment.blue_steel_sword.desc",
            assetCode: "EQ10002"),

        [EquipmentIds.RustSword] = new EquipmentDefinition(
            EquipmentIds.RustSword,
            "锈矛",
            "普通杀伤害+7。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Common,
            new[]
            {
                new EquipmentEffect("普通杀伤害+7。", 200)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "weapon", "kill_only", "flat_bonus" },
            200,
            nameKey: "equipment.rust_sword.name",
            descriptionKey: "equipment.rust_sword.desc",
            assetCode: "EQ10003"),

        [EquipmentIds.RustSpear] = new EquipmentDefinition(
            EquipmentIds.RustSpear,
            "锈剑",
            "装备者的普通杀改为对所有敌人造成伤害。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Common,
            new[]
            {
                new EquipmentEffect("装备者的普通杀改为对所有敌人造成伤害。不影响火杀/雷杀/火雷杀。", 400)
            },
            EquipmentAcquisitionMethod.Unknown,
            System.Array.Empty<string>(),
            new[] { "weapon", "kill_only", "aoe" },
            400,
            nameKey: "equipment.rust_spear.name",
            descriptionKey: "equipment.rust_spear.desc",
            assetCode: "EQ10004"),

        [EquipmentIds.YellowTalisman] = new EquipmentDefinition(
            EquipmentIds.YellowTalisman,
            "黄道符",
            "战斗开始时初始费用固定为2。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("战斗开始时初始费用固定为2。", 400)
            },
            EquipmentAcquisitionMethod.Reward,
            System.Array.Empty<string>(),
            new[] { "accessory", "resource", "start_of_battle" },
            400,
            nameKey: "equipment.yellow_talisman.name",
            descriptionKey: "equipment.yellow_talisman.desc",
            assetCode: "EQ30001"),

        [EquipmentIds.WinePouch] = new EquipmentDefinition(
            EquipmentIds.WinePouch,
            "酒囊",
            "本局第一张酒费用变为0，仅第一张酒生效。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("本局第一张酒费用变为0，仅第一张酒生效。", 300)
            },
            EquipmentAcquisitionMethod.Reward,
            System.Array.Empty<string>(),
            new[] { "accessory", "wine", "cost", "once" },
            300,
            nameKey: "equipment.wine_pouch.name",
            descriptionKey: "equipment.wine_pouch.desc",
            assetCode: "EQ30002"),

        [EquipmentIds.TigerTally] = new EquipmentDefinition(
            EquipmentIds.TigerTally,
            "虎符",
            "第四个战斗回合结束后获得1费，仅触发一次。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Common,
            new[]
            {
                new EquipmentEffect("第四个战斗回合结束后获得1费，仅触发一次。", 400)
            },
            EquipmentAcquisitionMethod.Reward,
            System.Array.Empty<string>(),
            new[] { "accessory", "resource", "turn_based", "once" },
            400,
            nameKey: "equipment.tiger_tally.name",
            descriptionKey: "equipment.tiger_tally.desc",
            assetCode: "EQ30003"),

        [EquipmentIds.Dan] = new EquipmentDefinition(
            EquipmentIds.Dan,
            "丹",
            "这个丹好像并不是口服的，\n触碰它时会变为各种奇异的形态。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Legendary,
            new[]
            {
                // 触发接口预留：装备效果系统接入战斗后，需在"恢复生命"结算处挂接此触发。
                // 触发条件：装备者在战斗中发生任意生命回复（桃、华佗青囊、未来装备/技能/事件回复均算）。
                // 触发结果：对当前最左侧存活敌人造成 本次回复量 x 50% 的伤害；无存活敌人时不触发。
                new EquipmentEffect("装备者在战斗中恢复生命时，对当前最左侧存活敌人造成本次回复量×50%的伤害；若无存活敌人则不触发。", 600)
            },
            EquipmentAcquisitionMethod.Event,
            new[] { "仙丹术士事件-选项三" },
            new[] { "accessory", "legendary", "heal_trigger", "damage_reflect" },
            600,
            nameKey: "equipment.dan.name",
            descriptionKey: "equipment.dan.desc",
            assetCode: "EQ30004"),

        [EquipmentIds.MysteriousChip] = new EquipmentDefinition(
            EquipmentIds.MysteriousChip,
            "神秘芯片",
            "一枚来历不明的芯片。\n表面不断闪烁着无法识别的字符。\n它似乎正在等待什么。",
            new[] { EquipmentType.Accessory },
            EquipmentRarity.Legendary,
            System.Array.Empty<EquipmentEffect>(),
            EquipmentAcquisitionMethod.Event,
            new[] { "长坂坡事件-选项三(仅赵云)", "废弃实验室事件-选项二", "仙丹术士事件-选项四" },
            new[] { "accessory", "legendary", "story", "mystery", "unsellable" },
            0,
            nameKey: "equipment.mysterious_chip.name",
            descriptionKey: "equipment.mysterious_chip.desc",
            assetCode: "EQ30005",
            canAppearInRandomPool: false),

        // ————————————————————————————————————————————————
        // 新增装备：藤甲 / 鳞甲 / 白马
        // ————————————————————————————————————————————————

        [EquipmentIds.Tengjia] = new EquipmentDefinition(
            EquipmentIds.Tengjia,
            "藤甲",
            "获得10最大生命。\n受到物理伤害（普通杀、南蛮入侵、万箭齐发等）时，最终伤害减半（×0.5）。\n受到任意火属性伤害（火杀、火雷杀等）时，最终伤害翻倍（×2）。\n两个效果在所有加法完成后的最终乘区结算。",
            new[] { EquipmentType.Buff, EquipmentType.Armor, EquipmentType.Defense },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("获得10最大生命。", 200),
                new EquipmentEffect("受到物理伤害（普通杀/南蛮入侵/万箭齐发等）最终伤害减半；受到任意火属性伤害最终伤害翻倍（最终乘区）。", 600)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "armor", "epic", "damage_reduction", "fire_weakness" },
            600,
            nameKey: "equipment.armor_tengjia.name",
            descriptionKey: "equipment.armor_tengjia.desc",
            assetCode: "EQ20005"),

        [EquipmentIds.ScaleArmor] = new EquipmentDefinition(
            EquipmentIds.ScaleArmor,
            "鳞甲",
            "获得10最大生命。\n每次受到伤害时，单次最终伤害上限为15（在所有加法和乘法完成后封顶）。",
            new[] { EquipmentType.Buff, EquipmentType.Armor, EquipmentType.Defense },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("获得10最大生命。", 200),
                new EquipmentEffect("单次受到的最终伤害上限为15（最终结算封顶）。", 700)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "armor", "damage_cap" },
            700,
            nameKey: "equipment.armor_scale.name",
            descriptionKey: "equipment.armor_scale.desc",
            assetCode: "EQ20006"),

        [EquipmentIds.WhiteHorse] = new EquipmentDefinition(
            EquipmentIds.WhiteHorse,
            "白马",
            "每场战斗中第一次打出的普通杀费用变为0。\n仅影响普通杀，不影响火杀、雷杀、必中杀。\n每场战斗重置，切换场景不清除。",
            new[] { EquipmentType.Mount, EquipmentType.Buff },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("每场战斗中第一次打出的普通杀费用变为0，仅触发一次，每战斗重置。", 300)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "mount", "kill_cost_reduction", "per_battle" },
            300,
            nameKey: "equipment.mount_white_horse.name",
            descriptionKey: "equipment.mount_white_horse.desc",
            assetCode: "EQ40001"),

        [EquipmentIds.DiLu] = new EquipmentDefinition(
            EquipmentIds.DiLu,
            "的卢",
            "免疫本场战斗中受到的第一次伤害。\n若装备者为【刘备】：【仁德】恢复生命值由10提升至20。",
            new[] { EquipmentType.Vehicle, EquipmentType.Buff },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("免疫本场战斗中受到的第一次伤害。", 500),
                new EquipmentEffect("若装备者为【刘备】：【仁德】恢复生命值由10提升至20。", 300)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "vehicle", "rare", "damage_immunity", "once", "per_battle", "liubei" },
            500,
            nameKey: "equipment.vehicle_di_lu.name",
            descriptionKey: "equipment.vehicle_di_lu.desc",
            assetCode: "EQ40002"),

        [EquipmentIds.ZhuaHuangFeiDian] = new EquipmentDefinition(
            EquipmentIds.ZhuaHuangFeiDian,
            "爪黄飞电",
            "本场战斗中。\n第一张需要消耗费用的攻击牌不消耗费用。",
            new[] { EquipmentType.Vehicle, EquipmentType.Buff },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("本场战斗中，第一张需要消耗费用的攻击牌不消耗费用。", 420)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "vehicle", "rare", "attack_cost", "once", "per_battle" },
            420,
            nameKey: "equipment.vehicle_zhua_huang_fei_dian.name",
            descriptionKey: "equipment.vehicle_zhua_huang_fei_dian.desc",
            assetCode: "EQ40003"),

        [EquipmentIds.HanXueBaoMa] = new EquipmentDefinition(
            EquipmentIds.HanXueBaoMa,
            "汗血宝马",
            "战斗开始时，获得20点临时生命值。",
            new[] { EquipmentType.Vehicle, EquipmentType.Buff },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("战斗开始时，获得20点临时生命值。", 300)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "vehicle", "rare", "temp_hp", "battle_start" },
            300,
            nameKey: "equipment.vehicle_han_xue_bao_ma.name",
            descriptionKey: "equipment.vehicle_han_xue_bao_ma.desc",
            assetCode: "EQ40006"),

        // ————————————————————————————————————————————————
        // 新增装备：火纹银枪 / 钩索
        // ————————————————————————————————————————————————

        [EquipmentIds.FirePatternSilverSpear] = new EquipmentDefinition(
            EquipmentIds.FirePatternSilverSpear,
            "火纹银枪",
            "火杀费用变为1（原为2）。",
            new[] { EquipmentType.Weapon, EquipmentType.Buff },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("火杀费用变为1。", 300)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "weapon", "fire_kill", "cost_reduction", "epic" },
            300,
            nameKey: "equipment.weapon_fire_pattern_silver_spear.name",
            descriptionKey: "equipment.weapon_fire_pattern_silver_spear.desc",
            assetCode: "EQ10005"),

        [EquipmentIds.HookChain] = new EquipmentDefinition(
            EquipmentIds.HookChain,
            "钩索",
            "追捕，拖行。\n连续使用两张杀系牌后，若这两张牌均未命中目标，则立刻使双方各受到5点真实伤害。",
            new[] { EquipmentType.Weapon, EquipmentType.Buff },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("连续两张杀系牌均未命中时，立刻使双方各受到5点真实伤害，并重置计数。", 400)
            },
            EquipmentAcquisitionMethod.Reward,
            System.Array.Empty<string>(),
            new[] { "weapon", "hook_chain", "miss_tracking", "rare" },
            400,
            nameKey: "equipment.weapon_hook_chain.name",
            descriptionKey: "equipment.weapon_hook_chain.desc",
            assetCode: "EQ10006"),

        // ————————————————————————————————————————————————
        // 新增装备（第二章）：铁卫重甲 / 喷气式狼牙棒
        // ————————————————————————————————————————————————

        [EquipmentIds.IronHeavyArmor] = new EquipmentDefinition(
            EquipmentIds.IronHeavyArmor,
            "铁卫重甲",
            "光是看着就能感受到无比的重量了。\n获得20最大生命。\n每场战斗：首次受到超过20点的单次伤害时，该伤害归零（仅触发一次，每场战斗重置）。",
            new[] { EquipmentType.Buff, EquipmentType.Armor, EquipmentType.Defense },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("获得20最大生命。", 200),
                new EquipmentEffect("每场战斗首次受到超过20点单次伤害时归零，仅触发一次。", 600)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "armor", "damage_immunity", "once", "per_battle" },
            600,
            nameKey: "equipment.armor_iron_heavy.name",
            descriptionKey: "equipment.armor_iron_heavy.desc",
            assetCode: "EQ20007"),

        [EquipmentIds.JetMace] = new EquipmentDefinition(
            EquipmentIds.JetMace,
            "喷气式狼牙棒",
            "喷气式推进能瞬间爆发出巨大的力量。\n每场战斗第一次攻击造成三倍伤害（仅触发一次，每场战斗重置）。\n适用于全部攻击牌。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("每场战斗第一次攻击造成三倍伤害，仅触发一次。", 500)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "weapon", "triple_damage", "once", "per_battle" },
            500,
            nameKey: "equipment.weapon_jet_mace.name",
            descriptionKey: "equipment.weapon_jet_mace.desc",
            assetCode: "EQ10007"),

        [EquipmentIds.HeavyHammer] = new EquipmentDefinition(
            EquipmentIds.HeavyHammer,
            "重锤",
            "皇宫专用的巨型武器，雕刻着繁复的花纹，锤头纹着狮头。\n每过8回合后，对敌人造成前8回合内已造成伤害的一半。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("记录每8回合内对敌人造成的伤害总和。", 450),
                new EquipmentEffect("每8回合结束时，对敌方目标造成该8回合总伤害的一半。", 650)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "weapon", "rare", "imperial", "delayed_burst", "turn_8" },
            650,
            nameKey: "equipment.weapon_heavy_hammer.name",
            descriptionKey: "equipment.weapon_heavy_hammer.desc",
            assetCode: "EQ10008"),

        // ————————————————————————————————————————————————
        // 新增装备：战鼓（史诗，增益/饰品）
        // ————————————————————————————————————————————————

        [EquipmentIds.WarDrum] = new EquipmentDefinition(
            EquipmentIds.WarDrum,
            "战鼓",
            "敲动时居然可以发出一阵阵具像化的波纹。\n本场战斗第一次打出攻击牌后，从下一回合起所有攻击牌基础伤害+5。\n若本回合至少打出一张攻击牌，状态延续；否则立即移除。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Common,
            new[]
            {
                new EquipmentEffect("本场战斗第一次打出攻击牌后获得战鼓状态（下一回合生效）。", 300),
                new EquipmentEffect("战鼓状态：所有攻击牌基础伤害+5；若本回合未打出攻击牌则立即移除。", 350)
            },
            EquipmentAcquisitionMethod.Reward,
            System.Array.Empty<string>(),
            new[] { "accessory", "epic", "attack_bonus", "conditional" },
            350,
            nameKey: "equipment.equipment_war_drum.name",
            descriptionKey: "equipment.equipment_war_drum.desc",
            assetCode: "EQ30006"),

        // ————————————————————————————————————————————————
        // 新增装备：灵魂石（稀有，增益/饰品）
        // ————————————————————————————————————————————————

        [EquipmentIds.SoulStone] = new EquipmentDefinition(
            EquipmentIds.SoulStone,
            "灵魂石",
            "一块散发着神秘光芒的石头。\n出售时：除获得金币外，额外随机获得一枚芯片（攻击/防护/知识各33%）。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("出售时：额外随机获得一枚芯片（攻击/防护/知识各33%）。", 150)
            },
            EquipmentAcquisitionMethod.Reward,
            System.Array.Empty<string>(),
            new[] { "accessory", "on_sell", "chip" },
            150,
            nameKey: "equipment.soul_stone.name",
            descriptionKey: "equipment.soul_stone.desc",
            assetCode: "EQ30007"),

        [EquipmentIds.CurseBlade] = new EquipmentDefinition(
            EquipmentIds.CurseBlade,
            "诅咒之刃",
            "现在它缠上你了。\n当前诅咒层数×5，后续获得的诅咒也×5。\n每5层诅咒使你的伤害额外提升0.1倍。\n装备此刃进行战斗时，每场战斗获得2层诅咒。\n战斗结束后诅咒不会自动衰减。\n不可售卖。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Legendary,
            new[]
            {
                new EquipmentEffect("装备时将当前诅咒层数放大至5倍，并使后续获得的诅咒也×5。", 350),
                new EquipmentEffect("每5层诅咒使你的伤害额外提升0.1倍。", 420),
                new EquipmentEffect("装备此刃进行战斗时，每场战斗获得2层诅咒。", 500),
                new EquipmentEffect("战斗结束后诅咒不会自动衰减。", 600),
                new EquipmentEffect("不可售卖。", 900)
            },
            EquipmentAcquisitionMethod.Event,
            new[] { "会说话的剑事件-把它拔出来" },
            new[] { "weapon", "legendary", "curse", "damage_bonus", "unsellable", "chapter1_variant" },
            500,
            nameKey: "equipment.curse_blade.name",
            descriptionKey: "equipment.curse_blade.desc",
            assetCode: "EQ10009"),

        [EquipmentIds.BigBoneClub] = new EquipmentDefinition(
            EquipmentIds.BigBoneClub,
            "大骨棒",
            "这么大的骨头，究竟是什么生物的？\n你的杀类型牌伤害+9点，成功命中敌人会施加敌人一层【眩晕】。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("你的杀类型牌伤害+9点。", 450),
                new EquipmentEffect("杀类型牌成功命中敌人后，使目标获得1层【眩晕】。", 550)
            },
            EquipmentAcquisitionMethod.Event,
            new[] { "血池事件-打捞骨头" },
            new[] { "weapon", "rare", "chapter1_variant", "blood_moon", "slay_damage", "stun" },
            100,
            nameKey: "equipment.big_bone_club.name",
            descriptionKey: "equipment.big_bone_club.desc",
            assetCode: "EQ10010"),

        [EquipmentIds.ForgottenStone] = new EquipmentDefinition(
            EquipmentIds.ForgottenStone,
            "遗忘之石",
            "触碰它，仿佛你也同样被遗忘了。\n战斗开始后第1个战斗回合：免疫任何形式的伤害。\n第2、第3个战斗回合：受到的所有伤害减少50%（向下取整）。\n第4回合起恢复正常。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("第1回合免疫所有伤害；第2、3回合所有伤害减半（向下取整）。", 500)
            },
            EquipmentAcquisitionMethod.Event,
            new[] { "统治者雕像事件-摧毁雕像" },
            new[] { "accessory", "rare", "chapter2", "first_turn_immunity" },
            500,
            nameKey: "equipment.forgotten_stone.name",
            descriptionKey: "equipment.forgotten_stone.desc",
            assetCode: "EQ30008",
            // 仅可通过统治者雕像事件获得：商店不出售，也不出现在任何随机装备奖励池。
            canAppearInRandomPool: false),

        [EquipmentIds.StatueCore] = new EquipmentDefinition(
            EquipmentIds.StatueCore,
            "雕像核心",
            "在雕像上一直无休地旋转着。\n本场战斗前3次成功攻击时，对另外最多2个敌人造成50%的溅射伤害。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("本场战斗前3次成功攻击时，对另外最多2个敌人造成50%的溅射伤害。", 450)
            },
            EquipmentAcquisitionMethod.Event,
            new[] { "统治者雕像事件-窃走雕像核心" },
            new[] { "accessory", "rare", "chapter2", "splash", "aoe" },
            450,
            nameKey: "equipment.statue_core.name",
            descriptionKey: "equipment.statue_core.desc",
            assetCode: "EQ30009"),

        [EquipmentIds.SpringEssence] = new EquipmentDefinition(
            EquipmentIds.SpringEssence,
            "泉水精华",
            "凝结自永不干涸的泉眼。\n战斗中免疫所有负面Buff。售出时获得2粮草。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Legendary,
            new[]
            {
                new EquipmentEffect("战斗中不会受到负面Buff影响。", 900),
                new EquipmentEffect("售出时获得2粮草。", 900)
            },
            EquipmentAcquisitionMethod.Event,
            new[] { "统治者雕像事件" },
            new[] { "accessory", "legendary", "chapter2", "debuff_immune", "forage" },
            900,
            nameKey: "equipment.spring_essence.name",
            descriptionKey: "equipment.spring_essence.desc",
            assetCode: "EQ30010"),

        [EquipmentIds.WitchScalp] = new EquipmentDefinition(
            EquipmentIds.WitchScalp,
            "巫婆的头皮",
            "即使已经离开了身体，它依旧在耳边低声呢喃。\n售出时获得：攻击芯片×1、知识芯片×1、防御芯片×1。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("售出时获得攻击芯片×1、知识芯片×1、防御芯片×1，仅触发一次。", 900)
            },
            EquipmentAcquisitionMethod.Event,
            new[] { "巫婆事件-一拳干翻她" },
            new[] { "accessory", "epic", "chapter2", "on_sell", "chips" },
            900,
            nameKey: "equipment.witch_scalp.name",
            descriptionKey: "equipment.witch_scalp.desc",
            assetCode: "EQ30011"),

        [EquipmentIds.XianNiang] = new EquipmentDefinition(
            EquipmentIds.XianNiang,
            "仙酿",
            "酒费用-1。若酒费用已为0，则本回合使用过酒后，回合结束获得1费（每回合最多一次）。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Legendary,
            new[]
            {
                new EquipmentEffect("酒费用-1。", 300),
                new EquipmentEffect("若酒费用已为0，本回合使用过酒后，回合结束获得1费（每回合最多一次）。", 700)
            },
            EquipmentAcquisitionMethod.Unknown,
            System.Array.Empty<string>(),
            new[] { "accessory", "legendary", "wine", "resource" },
            700,
            nameKey: "equipment.xian_niang.name",
            descriptionKey: "equipment.xian_niang.desc",
            assetCode: "EQ30012"),

        [EquipmentIds.TyrantCrown] = new EquipmentDefinition(
            EquipmentIds.TyrantCrown,
            "暴虐皇冠",
            "只要装备者存活，其队友不会受到任何伤害。装备者受到的所有伤害×2。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Legendary,
            new[]
            {
                new EquipmentEffect("装备者存活时，队友免疫所有伤害。", 500),
                new EquipmentEffect("装备者受到的所有伤害×2。", 600)
            },
            EquipmentAcquisitionMethod.Unknown,
            System.Array.Empty<string>(),
            new[] { "accessory", "legendary", "boss", "guard", "vulnerable" },
            600,
            nameKey: "equipment.tyrant_crown.name",
            descriptionKey: "equipment.tyrant_crown.desc",
            assetCode: "EQ30013",
            canAppearInRandomPool: false),

        [EquipmentIds.MoonGem] = new EquipmentDefinition(
            EquipmentIds.MoonGem,
            "月亮宝石",
            "宝石内部仿佛封印着一颗正在燃烧的星辰。\n获得技能【天体撞击】，并将【天体撞击】加入出招栏。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Legendary,
            new[]
            {
                new EquipmentEffect("获得技能【天体撞击】。", 400),
                new EquipmentEffect("将【天体撞击】加入出招栏；若已拥有则不重复添加。", 410)
            },
            EquipmentAcquisitionMethod.Reward,
            new[] { "隐藏Boss【月亮】100%掉落" },
            new[] { "accessory", "legendary", "boss", "aoe", "celestial" },
            410,
            nameKey: "equipment.moon_gem.name",
            descriptionKey: "equipment.moon_gem.desc",
            assetCode: "EQ30014"),

        // ————————————————————————————————————————————————
        // 新增装备（集智体精英 / 第二章）
        // ————————————————————————————————————————————————

        [EquipmentIds.ThunderSpear] = new EquipmentDefinition(
            EquipmentIds.ThunderSpear,
            "雷矛",
            "通体流窜着电，如何握住才是关键。\n雷杀费用降为1（原为2）。",
            new[] { EquipmentType.Weapon, EquipmentType.Buff },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("雷杀费用降为1。", 400)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "weapon", "thunder_kill", "cost_reduction", "epic" },
            400,
            nameKey: "equipment.weapon_thunder_spear.name",
            descriptionKey: "equipment.weapon_thunder_spear.desc",
            assetCode: "EQ10011"),

        [EquipmentIds.AttackChip] = new EquipmentDefinition(
            EquipmentIds.AttackChip,
            "攻击芯片",
            "刻有攻击增幅矩阵的芯片。\n所有杀系攻击伤害+3。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("所有杀系攻击伤害+3。", 300)
            },
            EquipmentAcquisitionMethod.Reward,
            System.Array.Empty<string>(),
            new[] { "accessory", "sha_series", "flat_bonus", "rare" },
            300,
            nameKey: "equipment.chip_attack.name",
            descriptionKey: "equipment.chip_attack.desc",
            assetCode: "EQ30015",
            // 芯片通过专用奖励即时转化为计数，不是可进入背包的装备，不能占用随机装备候选位。
            canAppearInRandomPool: false),

        // ————————————————————————————————————————————————
        // 传奇装备：钳制机械外骨骼（卧龙集智体 Boss 携带 / 掉落）
        // ————————————————————————————————————————————————

        [EquipmentIds.ClampExoskeleton] = new EquipmentDefinition(
            EquipmentIds.ClampExoskeleton,
            "钳制机械外骨骼",
            "坚硬的外骨骼，可以在战斗中帮助控制敌人。\n获得50最大生命。\n当装备者生命值首次跌至最大生命值的一半及以下时触发：立刻对敌方使用一次顺手牵羊，获得酒BUFF×1，进入修复状态（下一回合开始时恢复至满生命值）。每场战斗仅触发一次。",
            new[] { EquipmentType.Buff, EquipmentType.Armor, EquipmentType.Defense },
            EquipmentRarity.Legendary,
            new[]
            {
                new EquipmentEffect("获得50最大生命。", 300),
                new EquipmentEffect("首次生命值跌至50%及以下时：顺手牵羊+酒BUFF×1+修复状态（下回合满血）。仅触发一次。", 800)
            },
            EquipmentAcquisitionMethod.Unknown,
            System.Array.Empty<string>(),
            new[] { "armor", "legendary", "hp", "steal", "wine", "repair", "once", "per_battle" },
            800,
            nameKey: "equipment.clamp_exoskeleton.name",
            descriptionKey: "equipment.clamp_exoskeleton.desc",
            assetCode: "EQ20008"),

        // ————————————————————————————————————————————————
        // 商店武器
        // ————————————————————————————————————————————————

        [EquipmentIds.ShortBow] = new EquipmentDefinition(
            EquipmentIds.ShortBow,
            "短弓",
            "轻巧的短弓，适合近距离骚扰。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Common,
            new[]
            {
                new EquipmentEffect("本场战斗前3次攻击命中时触发（每回合最多1次）：对另外最多2个存活敌人造成本次伤害50%的溅射伤害。3次后失效。", 400)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "weapon", "splash", "aoe", "limited_use" },
            400,
            nameKey: "equipment.weapon_short_bow.name",
            descriptionKey: "equipment.weapon_short_bow.desc",
            assetCode: "EQ10012"),

        [EquipmentIds.LongBow] = new EquipmentDefinition(
            EquipmentIds.LongBow,
            "长弓",
            "精准狙击，一击毙命。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Common,
            new[]
            {
                new EquipmentEffect("本场战斗第一张普通杀：无视敌方闪，且造成双倍伤害。仅影响第一张普通杀。", 400)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "weapon", "kill_only", "dodge_bypass", "double_damage", "once_per_battle" },
            400,
            nameKey: "equipment.weapon_long_bow.name",
            descriptionKey: "equipment.weapon_long_bow.desc",
            assetCode: "EQ10013"),

        [EquipmentIds.RustGuDingDao] = new EquipmentDefinition(
            EquipmentIds.RustGuDingDao,
            "锈古锭刀",
            "锈蚀的刀锋仍能抓住敌人露出的破绽。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("敌人没有费用时，你的普通杀无视其闪。徐盛装备时，普通杀伤害×1.25。", 350)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "weapon", "kill_only", "dodge_bypass", "mana_check", "rust" },
            350,
            nameKey: "equipment.weapon_rust_gu_ding_dao.name",
            descriptionKey: "equipment.weapon_rust_gu_ding_dao.desc",
            assetCode: "EQ10062"),

        [EquipmentIds.GuDingDao] = new EquipmentDefinition(
            EquipmentIds.GuDingDao,
            "古锭刀",
            "刀势专取气竭之敌。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("敌人没有费用时，你的普通杀变为必中杀。徐盛装备时，杀类型牌伤害×1.5。", 450)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "weapon", "kill_family", "direct_kill", "mana_check" },
            450,
            nameKey: "equipment.weapon_gu_ding_dao.name",
            descriptionKey: "equipment.weapon_gu_ding_dao.desc",
            assetCode: "EQ10014"),

        [EquipmentIds.TrueGuDingDao] = new EquipmentDefinition(
            EquipmentIds.TrueGuDingDao,
            "真·古锭刀",
            "刀身同时流动着火与雷，旧有招式已无法承载它。\n由徐盛在古蜀铸炉中重铸而成。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Legendary,
            new[]
            {
                new EquipmentEffect("杀、火杀、雷杀替换为火雷杀；敌人没有费用时火雷杀费用变为1。徐盛装备时，火雷杀伤害×1.75。", 550)
            },
            EquipmentAcquisitionMethod.Event,
            new[] { "古蜀铸炉事件-隐藏选项(仅徐盛)" },
            new[] { "weapon", "fire_thunder_kill", "card_replacement", "mana_check", "special_source" },
            550,
            nameKey: "equipment.weapon_true_gu_ding_dao.name",
            descriptionKey: "equipment.weapon_true_gu_ding_dao.desc",
            assetCode: "EQ10063",
            canAppearInRandomPool: false),

        // ————————————————————————————————————————————————
        // 商店饰品
        // ————————————————————————————————————————————————

        [EquipmentIds.Poison] = new EquipmentDefinition(
            EquipmentIds.Poison,
            "毒药",
            "涂到刃上，只要砍到，非死即残。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Common,
            new[]
            {
                new EquipmentEffect("你的杀（普通杀/火杀/雷杀/火雷杀）命中后：目标在下一回合开始时受到5点Poison（毒素）伤害。", 350)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "accessory", "poison", "dot", "sha_series" },
            350,
            nameKey: "equipment.accessory_poison.name",
            descriptionKey: "equipment.accessory_poison.desc",
            assetCode: "EQ30016"),

        [EquipmentIds.Gunpowder] = new EquipmentDefinition(
            EquipmentIds.Gunpowder,
            "火药",
            "能大幅增加元素的威力。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Common,
            new[]
            {
                new EquipmentEffect("火杀伤害+4，雷杀伤害+4，火雷杀伤害+8。", 300)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "accessory", "fire_sha", "thunder_sha", "fire_thunder_sha", "flat_bonus" },
            300,
            nameKey: "equipment.accessory_gunpowder.name",
            descriptionKey: "equipment.accessory_gunpowder.desc",
            assetCode: "EQ30017"),

        [EquipmentIds.SproutingBonsai] = new EquipmentDefinition(
            EquipmentIds.SproutingBonsai,
            "发芽盆栽",
            "能够见到这种有生机的东西，令人精神愉悦。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Common,
            new[]
            {
                new EquipmentEffect("你的桃回复量+5（10→15）。仅增加回复量，不影响桃护盾层数。", 250)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "accessory", "peach", "heal_bonus" },
            250,
            nameKey: "equipment.accessory_sprouting_bonsai.name",
            descriptionKey: "equipment.accessory_sprouting_bonsai.desc",
            assetCode: "EQ30018"),

        [EquipmentIds.PanXiao] = new EquipmentDefinition(
            EquipmentIds.PanXiao,
            "排箫",
            "掌权者最喜欢的乐器。\n悠扬的旋律会不断影响战场上的所有人。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("敌方最终受到伤害×1.1；我方最终受到伤害÷1.1。真实伤害除外。", 400)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "accessory", "rare", "damage_amp", "damage_reduce", "field_effect" },
            400,
            nameKey: "equipment.accessory_pan_xiao.name",
            descriptionKey: "equipment.accessory_pan_xiao.desc",
            assetCode: "EQ30019"),

        [EquipmentIds.ParalysisDevice] = new EquipmentDefinition(
            EquipmentIds.ParalysisDevice,
            "瘫痪装置",
            "小型战术干扰装置。\n战斗开局时，对所有敌人各造成3次5点真实伤害。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("战斗开局时，对所有敌人各造成3次5点真实伤害。", 420)
            },
            EquipmentAcquisitionMethod.Shop,
            new[] { "商店", "命运开端-初始事件⑮" },
            new[] { "accessory", "epic", "start_of_battle", "true_damage", "aoe" },
            420,
            nameKey: "equipment.accessory_paralysis_device.name",
            descriptionKey: "equipment.accessory_paralysis_device.desc",
            assetCode: "EQ30020"),

        [EquipmentIds.ElfDust] = new EquipmentDefinition(
            EquipmentIds.ElfDust,
            "精灵尘",
            "闪烁着微弱光点的粉尘。\n你的桃回复量翻倍。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("你的【桃】回复量翻倍。仅影响桃的回复量，不影响桃护盾、酒Buff或其它治疗来源。", 360)
            },
            EquipmentAcquisitionMethod.Shop,
            new[] { "商店", "囚禁的精灵事件" },
            new[] { "accessory", "epic", "peach", "heal_multiplier", "shop" },
            360,
            nameKey: "equipment.accessory_elf_dust.name",
            descriptionKey: "equipment.accessory_elf_dust.desc",
            assetCode: "EQ30021"),

        [EquipmentIds.WaterproofModule] = new EquipmentDefinition(
            EquipmentIds.WaterproofModule,
            "防水模块",
            "密封结构能够有效隔绝潮湿环境。\n最大生命值 +10，免疫【潮湿】。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Common,
            new[]
            {
                new EquipmentEffect("最大生命值 +10，免疫【潮湿】。", 200)
            },
            EquipmentAcquisitionMethod.Shop,
            new[] { "商店", "赤壁残骸事件" },
            new[] { "accessory", "common", "hp", "shop" },
            200,
            nameKey: "equipment.accessory_waterproof_module.name",
            descriptionKey: "equipment.accessory_waterproof_module.desc",
            assetCode: "EQ30022"),

        [EquipmentIds.MiJiang] = new EquipmentDefinition(
            EquipmentIds.MiJiang,
            "蜜浆",
            "一种粘稠而甘甜的液体，似乎拥有极强的生命力。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("每次与敌人战斗结束后，恢复至最大生命值。", 450)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "accessory", "rare", "heal", "battle_end", "full_restore" },
            450,
            nameKey: "equipment.accessory_mi_jiang.name",
            descriptionKey: "equipment.accessory_mi_jiang.desc",
            assetCode: "EQ30023"),

        // ————————————————————————————————————————————————
        // 新增装备：合金盾 / 赤兔 / 木牛流马 / 合金剑 / 合金矛
        // ————————————————————————————————————————————————

        [EquipmentIds.GangDun] = new EquipmentDefinition(
            EquipmentIds.GangDun,
            "合金盾",
            "锈盾升级而来。提供稳定的生命成长。装备者获得30点最大生命。",
            new[] { EquipmentType.Buff, EquipmentType.Armor, EquipmentType.Defense },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("获得30最大生命（战斗开始时生效，当前生命同步增加）。", 300)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "armor", "defense", "rare", "hp_bonus" },
            300,
            nameKey: "equipment.armor_gang_dun.name",
            descriptionKey: "equipment.armor_gang_dun.desc",
            assetCode: "EQ20009"),

        [EquipmentIds.GiantShield] = new EquipmentDefinition(
            EquipmentIds.GiantShield,
            "巨人盾",
            "厚重的巨型护盾将使用者的生命力转化为压倒性的攻势。\n\n获得45点最大生命值。你的主动攻击额外造成相当于自身当前生命值3%的伤害（向下取整）。",
            new[] { EquipmentType.Buff, EquipmentType.Armor },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("获得45点最大生命值（战斗开始时生效，当前生命同步增加）。", 600),
                new EquipmentEffect("主动攻击额外造成自身当前生命值3%的伤害（向下取整）。", 600)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "armor", "epic", "hp_bonus", "current_hp_damage", "shop" },
            600,
            nameKey: "equipment.armor_giant_shield.name",
            descriptionKey: "equipment.armor_giant_shield.desc",
            assetCode: "EQ20013"),

        [EquipmentIds.FoldingKnife] = new EquipmentDefinition(
            EquipmentIds.FoldingKnife,
            "折叠刀",
            "刀刃展开后分裂成两道短促而致命的斩击。\n\n你的普通杀改为造成2次5点伤害；每一段都独立结算普通杀的加伤、护盾与命中后效果。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("普通杀改为造成2次5点独立伤害。", 650),
                new EquipmentEffect("每一段均可触发普通杀的加伤与命中后效果。", 650)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "weapon", "epic", "kill", "multi_hit", "shop" },
            650,
            nameKey: "equipment.weapon_folding_knife.name",
            descriptionKey: "equipment.weapon_folding_knife.desc",
            assetCode: "EQ10023"),

        [EquipmentIds.ChiTu] = new EquipmentDefinition(
            EquipmentIds.ChiTu,
            "赤兔",
            "提供极强的首回合爆发能力。\n吕布装备后效果进一步强化。",
            new[] { EquipmentType.Vehicle, EquipmentType.Buff },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("战斗第一回合，自动视为额外打出一张普通杀。", 400),
                new EquipmentEffect("第一回合所有杀系伤害×2。", 450),
                new EquipmentEffect("若角色为吕布：【无双】从×2升级为×3。", 500)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "vehicle", "epic", "first_round", "kill_boost", "lvbu" },
            500,
            nameKey: "equipment.mount_chi_tu.name",
            descriptionKey: "equipment.mount_chi_tu.desc",
            assetCode: "EQ40004"),

        [EquipmentIds.MuNiuLiuMa] = new EquipmentDefinition(
            EquipmentIds.MuNiuLiuMa,
            "木牛流马",
            "持续提供费用成长。\n适合长期运营构筑。",
            new[] { EquipmentType.Vehicle, EquipmentType.Buff },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("每经过3个战斗回合，获得0.5费。", 350)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "vehicle", "epic", "mana_regen", "per_round" },
            350,
            nameKey: "equipment.mount_mu_niu_liu_ma.name",
            descriptionKey: "equipment.mount_mu_niu_liu_ma.desc",
            assetCode: "EQ40005"),

        [EquipmentIds.TreasureDonkey] = new EquipmentDefinition(
            EquipmentIds.TreasureDonkey,
            "载宝驴",
            "每场战斗结束时，根据你当前拥有费用的整数部分获得等量电量，单场最多获得20点电量。小数费用不计入。",
            new[] { EquipmentType.Vehicle, EquipmentType.Buff },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("战斗胜利时，根据剩余费用的整数部分获得电量，单场最多20点。", 100)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "vehicle", "epic", "battle_victory", "power_recovery" },
            100,
            nameKey: "equipment.treasure_donkey.name",
            descriptionKey: "equipment.treasure_donkey.desc",
            assetCode: "EQ40007"),

        [EquipmentIds.HejinJian] = new EquipmentDefinition(
            EquipmentIds.HejinJian,
            "合金剑",
            "锈剑升级而来。普通杀对所有敌人生效，且伤害+5。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("装备者的普通杀改为对所有敌人造成伤害，且普通杀伤害+5。", 500)
            },
            EquipmentAcquisitionMethod.Event,
            new[] { "古蜀铸炉事件-隐藏选项(仅徐盛)" },
            new[] { "weapon", "rare", "kill_only", "aoe", "flat_bonus", "upgrade" },
            500,
            nameKey: "equipment.weapon_hejin_jian.name",
            descriptionKey: "equipment.weapon_hejin_jian.desc",
            assetCode: "EQ10015"),

        [EquipmentIds.HejinMao] = new EquipmentDefinition(
            EquipmentIds.HejinMao,
            "合金矛",
            "锈矛升级而来。所有杀系攻击伤害+10。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("所有杀系攻击伤害+10。", 400)
            },
            EquipmentAcquisitionMethod.Unknown,
            System.Array.Empty<string>(),
            new[] { "weapon", "rare", "all_kill", "flat_bonus", "upgrade" },
            400,
            nameKey: "equipment.weapon_hejin_mao.name",
            descriptionKey: "equipment.weapon_hejin_mao.desc",
            assetCode: "EQ10016"),

        // ————————————————————————————————————————————————
        // 史诗装备：破碎情感组件（失心者掉落）
        // ————————————————————————————————————————————————

        [EquipmentIds.BrokenEmotionalComponent] = new EquipmentDefinition(
            EquipmentIds.BrokenEmotionalComponent,
            "破碎情感组件",
            "破碎的心，破碎的人。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("每个回合结束时，场上所有存活单位（玩家和全部敌人）各恢复10点生命值。", 600)
            },
            EquipmentAcquisitionMethod.Unknown,
            System.Array.Empty<string>(),
            new[] { "accessory", "epic", "heal", "all_units", "turn_end" },
            600,
            nameKey: "equipment.broken_emotional_component.name",
            descriptionKey: "equipment.broken_emotional_component.desc",
            assetCode: "EQ30024"),

        // ————————————————————————————————————————————————
        // 传奇装备：扩容芯片（卧龙集智体 Boss 掉落）
        // ————————————————————————————————————————————————

        [EquipmentIds.ExpansionChip] = new EquipmentDefinition(
            EquipmentIds.ExpansionChip,
            "扩容芯片",
            "突破原有硬件限制。为身体扩展更多接口。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Legendary,
            new[]
            {
                new EquipmentEffect("解锁一个额外的万能槽，可装备武器/护甲/饰品任意类型装备。卸下芯片前须先移除万能槽内的装备。", 1000)
            },
            EquipmentAcquisitionMethod.Unknown,
            System.Array.Empty<string>(),
            new[] { "accessory", "legendary", "slot_expansion", "universal" },
            1000,
            nameKey: "equipment.expansion_chip.name",
            descriptionKey: "equipment.expansion_chip.desc",
            assetCode: "EQ30025"),

        // ————————————————————————————————————————————————
        // 传奇武器：仙毫。它是无槽位的背包生效装备，因此不会占用武器、护甲或饰品栏。
        // ————————————————————————————————————————————————

        [EquipmentIds.XianHao] = new EquipmentDefinition(
            EquipmentIds.XianHao,
            "仙毫",
            "挥毫之间左右命运。每次受到攻击伤害时有概率将其免除；每次攻击命中都会提高该概率。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Legendary,
            new[]
            {
                new EquipmentEffect("不占用装备槽。受到攻击伤害时有25%概率免疫本次伤害；每次攻击命中后概率+10%，最高90%。", 1000)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "weapon", "legendary", "inventory_activated", "attack_immunity", "evasion" },
            1000,
            nameKey: "equipment.xian_hao.name",
            descriptionKey: "equipment.xian_hao.desc",
            assetCode: "EQ10064",
            activationType: EquipmentActivationType.Inventory),

        // 传奇芯片：仅由标准芯片三选一的1%替换规则产出；选择后立即结算随机技能，
        // 不会进入背包，也不会占用任何装备槽。
        [EquipmentIds.SkillChip] = new EquipmentDefinition(
            EquipmentIds.SkillChip,
            "技能芯片",
            "封存着未经分类的战斗经验。选择后将随机解析为一项任意品质技能。",
            new[] { EquipmentType.Buff },
            EquipmentRarity.Legendary,
            new[]
            {
                new EquipmentEffect("获得一项任意品质的随机技能。", 1000)
            },
            EquipmentAcquisitionMethod.Unknown,
            System.Array.Empty<string>(),
            new[] { "chip", "legendary", "random_skill", "no_slot" },
            1000,
            nameKey: "equipment.skill_chip.name",
            descriptionKey: "equipment.skill_chip.desc",
            assetCode: "EQ00006",
            canAppearInRandomPool: false,
            activationType: EquipmentActivationType.Inventory),

        // 冰蓝装甲：战斗开始获得 7 费，永久禁止通过出费获得费用（可叠加）
        [EquipmentIds.IceBlueArmor] = new EquipmentDefinition(
            EquipmentIds.IceBlueArmor,
            "冰蓝装甲",
            "寒气在装甲内部不断循环。仿佛冻结了时间。\n战斗开始获得7费；你无法再使用【费】回复费用。",
            new[] { EquipmentType.Buff, EquipmentType.Armor, EquipmentType.Defense },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("战斗开始时获得7费；装备者无法再通过【费】获得费用（仍可通过技能/装备/事件/Buff获得）。", 800)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "armor", "epic", "mana_on_start", "fee_disabled" },
            800,
            nameKey: "equipment.armor_ice_blue.name",
            descriptionKey: "equipment.armor_ice_blue.desc",
            assetCode: "EQ20010"),

        // 蛮族营寨事件奖励
        [EquipmentIds.BarbarianTooth] = new EquipmentDefinition(
            EquipmentIds.BarbarianTooth,
            "蛮族的牙齿",
            "战斗开始时获得3层【凶残】。凶残：攻击时杀系伤害×1.5，每回合结束-1层。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Common,
            new[]
            {
                new EquipmentEffect("战斗开始时获得3层【凶残】：攻击时杀系伤害×1.5，每回合结束-1层。", 400)
            },
            EquipmentAcquisitionMethod.Event,
            new[] { "蛮族营寨决斗胜利" },
            new[] { "accessory", "buff", "ferocity" },
            400,
            nameKey: "equipment.barbarian_tooth.name",
            descriptionKey: "equipment.barbarian_tooth.desc",
            assetCode: "EQ30027"),

        // 蜀汉共生体 Boss 专属武器（不可被玩家获取）
        [EquipmentIds.ZhangBaSheMao] = new EquipmentDefinition(
            EquipmentIds.ZhangBaSheMao,
            "丈八蛇矛",
            "张飞专属。杀系伤害+5；连续两回合使用杀系攻击后，回合结束获得0.5费。",
            new[] { EquipmentType.Weapon },
            EquipmentRarity.Legendary,
            new[]
            {
                new EquipmentEffect("杀系伤害+5；连续出杀两回合后获得0.5费。", 0)
            },
            EquipmentAcquisitionMethod.Unknown,
            System.Array.Empty<string>(),
            new[] { "weapon", "boss_only", "zhang_fei" },
            0,
            nameKey: "equipment.weapon_zhang_ba_she_mao.name",
            descriptionKey: "equipment.weapon_zhang_ba_she_mao.desc",
            assetCode: "EQ10017"),

        [EquipmentIds.QingLongYanYueDao] = new EquipmentDefinition(
            EquipmentIds.QingLongYanYueDao,
            "青龙偃月刀",
            "关羽专属。杀系伤害+5；被玩家闪避时返还1费。",
            new[] { EquipmentType.Weapon },
            EquipmentRarity.Legendary,
            new[]
            {
                new EquipmentEffect("杀系伤害+5；攻击被闪避时返还1费。", 0)
            },
            EquipmentAcquisitionMethod.Unknown,
            System.Array.Empty<string>(),
            new[] { "weapon", "boss_only", "guan_yu" },
            0,
            nameKey: "equipment.weapon_qing_long_yan_yue_dao.name",
            descriptionKey: "equipment.weapon_qing_long_yan_yue_dao.desc",
            assetCode: "EQ10018"),

        // ————————————————————————————————————————————————
        // 七星坛事件奖励：朱雀羽扇（搜刮选项）
        // ————————————————————————————————————————————————
        [EquipmentIds.ZhuQueYuShan] = new EquipmentDefinition(
            EquipmentIds.ZhuQueYuShan,
            "朱雀羽扇",
            "你的火杀造成双倍伤害。若你是周瑜，你的火攻也造成双倍伤害。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("你的火杀造成双倍伤害；周瑜的火攻也造成双倍伤害。", 400)
            },
            EquipmentAcquisitionMethod.Unknown,
            System.Array.Empty<string>(),
            new[] { "weapon", "fire_kill", "multiplier", "epic" },
            400,
            nameKey: "equipment.zhuque_yushan.name",
            descriptionKey: "equipment.zhuque_yushan.desc",
            assetCode: "EQ10019"),

        // ————————————————————————————————————————————————
        // 古蜀铸炉事件奖励：真·青钢剑（赵云隐藏选项）
        // ————————————————————————————————————————————————
        [EquipmentIds.TrueBlueSteelSword] = new EquipmentDefinition(
            EquipmentIds.TrueBlueSteelSword,
            "真·青钢剑",
            "所有杀系攻击伤害+15。包含杀、火杀、雷杀、必中杀。\n古蜀铸炉重铸，青钢之芒焕然一新。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Legendary,
            new[]
            {
                new EquipmentEffect("所有杀系攻击伤害+15。", 400)
            },
            EquipmentAcquisitionMethod.Event,
            new[] { "古蜀铸炉事件-隐藏选项(仅赵云)" },
            new[] { "weapon", "sha_series", "flat_bonus", "legendary" },
            500,
            nameKey: "equipment.true_blue_steel_sword.name",
            descriptionKey: "equipment.true_blue_steel_sword.desc",
            assetCode: "EQ10020"),

        // ————————————————————————————————————————————————
        // 传奇武器：诸葛连弩
        // ————————————————————————————————————————————————
        [EquipmentIds.ZhugeCrossbow] = new EquipmentDefinition(
            EquipmentIds.ZhugeCrossbow,
            "诸葛连弩",
            "结合了当前最高科技打造的自动连弩。\n扣下扳机的那一刻，便没有停下来的机会。\n\n你的普通【杀】费用变为0。\n本回合首次使用普通【杀】时，进入【连弩模式】（2秒）：期间可无限点击普通【杀】，每张正常结算伤害；2秒结束后立即结束本回合。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Legendary,
            new[]
            {
                new EquipmentEffect("普通【杀】费用变为0。", 500),
                new EquipmentEffect("首次使用普通【杀】时进入【连弩模式】（2秒）：可无限点击杀，2秒后立即结束本回合。", 500)
            },
            EquipmentAcquisitionMethod.Unknown,
            System.Array.Empty<string>(),
            new[] { "weapon", "legendary", "kill", "rapid_fire", "timed" },
            500,
            nameKey: "equipment.zhuge_crossbow.name",
            descriptionKey: "equipment.zhuge_crossbow.desc",
            assetCode: "EQ10021"),

        // ————————————————————————————————————————————————
        // 传奇武器：瘟疫权杖
        // ————————————————————————————————————————————————
        [EquipmentIds.PlagueStaff] = new EquipmentDefinition(
            EquipmentIds.PlagueStaff,
            "瘟疫权杖",
            "一柄不断滴落墨绿色毒液的权杖。\n据说每一次伤口都会成为瘟疫蔓延的源头。\n\n你的普通【杀】进化为【毒杀】：命中后立即造成10点Poison（毒素）伤害，并附加10层【瘟疫】。（瘟疫：累积10层时发作，造成10点真实毒素伤害；治疗清除5层）",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Legendary,
            new[]
            {
                new EquipmentEffect("普通【杀】进化为【毒杀】（继承普通杀所有属性）。", 500),
                new EquipmentEffect("毒杀命中：立即造成10点Poison（毒素）伤害+10层【瘟疫】。", 500),
                new EquipmentEffect("【瘟疫】10层发作造成的真实伤害不再次触发本效果（防止循环）。", 0)
            },
            EquipmentAcquisitionMethod.Unknown,
            System.Array.Empty<string>(),
            new[] { "weapon", "legendary", "poison", "plague", "kill_upgrade" },
            500,
            nameKey: "equipment.weapon_plague_staff.name",
            descriptionKey: "equipment.weapon_plague_staff.desc",
            assetCode: "EQ10022"),

        // ————————————————————————————————————————————————
        // 史诗护甲：导体
        // ————————————————————————————————————————————————
        [EquipmentIds.Conductor] = new EquipmentDefinition(
            EquipmentIds.Conductor,
            "导体",
            "特殊导体护甲，能够将大部分雷电导入地下。\n\n获得10点最大生命值。\n所受Thunder（雷属性）伤害-75%（仍视为受到雷属性攻击，不影响【黄天】等技能触发）。",
            new[] { EquipmentType.Buff, EquipmentType.Armor, EquipmentType.Defense },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("获得10点最大生命值。", 300),
                new EquipmentEffect("所受Thunder（雷属性）伤害-75%（不影响黄天等监听）。", 400)
            },
            EquipmentAcquisitionMethod.Unknown,
            System.Array.Empty<string>(),
            new[] { "armor", "epic", "thunder_immune", "lightning" },
            400,
            nameKey: "equipment.armor_conductor.name",
            descriptionKey: "equipment.armor_conductor.desc",
            assetCode: "EQ20011"),

        // ————————————————————————————————————————————————
        // 普通增益饰品：电击镣铐
        // ————————————————————————————————————————————————
        [EquipmentIds.ElectricShackle] = new EquipmentDefinition(
            EquipmentIds.ElectricShackle,
            "电击镣铐",
            "每回合开始时，先对自身造成1点Thunder（雷属性）自伤（装备自伤，不触发血债血偿等攻击类反馈），\n随后本回合承受的所有外部伤害降低10%。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Common,
            new[]
            {
                new EquipmentEffect("每回合开始自伤1点Thunder伤害（不触发攻击类反馈）。", 200),
                new EquipmentEffect("之后本回合所有外部来源伤害-10%。", 250)
            },
            EquipmentAcquisitionMethod.Reward,
            System.Array.Empty<string>(),
            new[] { "accessory", "common", "thunder", "damage_reduction", "self_damage" },
            300,
            nameKey: "equipment.accessory_electric_shackle.name",
            descriptionKey: "equipment.accessory_electric_shackle.desc",
            assetCode: "EQ30028"),

        [EquipmentIds.DiscountVoucher] = new EquipmentDefinition(
            EquipmentIds.DiscountVoucher,
            "打折券",
            "商店大促销！持有时，所有商店商品价格降低25%。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Common,
            new[]
            {
                new EquipmentEffect("【被动】持有时商店所有商品价格-25%（进入商店时生效）。", 0)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "accessory", "common", "shop", "discount" },
            0,
            nameKey: "equipment.accessory_discount_voucher.name",
            descriptionKey: "equipment.accessory_discount_voucher.desc",
            assetCode: "EQ30029"),

        // ── Boss掉落 ──────────────────────────────────────────────────────
        [EquipmentIds.MysteriousPotion] = new EquipmentDefinition(
            EquipmentIds.MysteriousPotion,
            "神秘补剂",
            "专门给强壮的怪物使用的针剂，能大幅提高战斗力。\n当自身当前生命值（含临时生命值）大于等于120时，普通杀变为必中杀。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("当前生命值（含临时生命值）≥120时，普通杀变为必中杀。", 0)
            },
            EquipmentAcquisitionMethod.Reward,
            new[] { "Boss掉落" },
            new[] { "accessory", "epic", "boss_drop", "kill_to_sure_kill" },
            0,
            nameKey: "equipment.boss_mysterious_potion.name",
            descriptionKey: "equipment.boss_mysterious_potion.desc",
            assetCode: "EQ60001"),

        // 黄金雕像：背包生效装备（EquipmentActivationType.Inventory）示例——
        // 无需装备到装备槽，只要存在于背包就会生效。金币加成本身在
        // GameManager.AddGold 里通过通用的 HasActiveEquipment 查询实现，
        // 不需要为这件装备单独写判断。
        [EquipmentIds.GoldenStatue] = new EquipmentDefinition(
            EquipmentIds.GoldenStatue,
            "黄金雕像",
            "金灿灿的。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("获得金币增加20%。", 0)
            },
            EquipmentAcquisitionMethod.Event,
            System.Array.Empty<string>(),
            new[] { "accessory", "inventory_activated", "gold" },
            0,
            nameKey: "equipment.golden_statue.name",
            descriptionKey: "equipment.golden_statue.desc",
            assetCode: "EQ30030",
            activationType: EquipmentActivationType.Inventory),

        // ————————————————————————————————————————————————
        // 初始事件㉑【财富契约】三选一装备
        // ————————————————————————————————————————————————
        [EquipmentIds.TycoonArmor] = new EquipmentDefinition(
            EquipmentIds.TycoonArmor,
            "大亨之铠",
            "每一道伤痕都是一笔进账。",
            new[] { EquipmentType.Buff, EquipmentType.Armor },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("最大生命值+10。", 100),
                new EquipmentEffect("每场战斗第1/5/10次受到实际伤害时，立即获得50金币（被闪避/格挡/护盾完全抵消/无敌不计入）。", 100)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "armor", "rare", "gold", "max_hp" },
            100,
            nameKey: "equipment.armor_tycoon.name",
            descriptionKey: "equipment.armor_tycoon.desc",
            assetCode: "EQ30031"),

        [EquipmentIds.SpeculatorBlade] = new EquipmentDefinition(
            EquipmentIds.SpeculatorBlade,
            "投机者之刃",
            "刀刃起落之间，皆是行情。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("所有杀类型牌（杀/火杀/雷杀/冰杀/必中杀/影袭杀等）伤害+2。", 100),
                new EquipmentEffect("每场战斗第1/5/10次使用杀类型牌时，获得30金币。", 100)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "weapon", "rare", "gold", "sha_bonus" },
            100,
            nameKey: "equipment.weapon_speculator_blade.name",
            descriptionKey: "equipment.weapon_speculator_blade.desc",
            assetCode: "EQ30032"),

        [EquipmentIds.Reaper] = new EquipmentDefinition(
            EquipmentIds.Reaper,
            "收割者",
            "残局，即是收成。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Rare,
            new[]
            {
                new EquipmentEffect("敌人生命值≤10%时，你的下一次伤害直接斩杀目标（Boss同样生效，正常触发死亡流程与奖励）。", 100),
                new EquipmentEffect("成功斩杀后立即获得25金币。", 100)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "weapon", "rare", "gold", "execution" },
            100,
            nameKey: "equipment.weapon_reaper.name",
            descriptionKey: "equipment.weapon_reaper.desc",
            assetCode: "EQ30033"),

        [EquipmentIds.VampiricFang] = new EquipmentDefinition(
            EquipmentIds.VampiricFang,
            "吸血之牙",
            "每回合首次主动攻击造成伤害后，回复等同于本次伤害25%的生命值（向下取整）。",
            new[] { EquipmentType.Buff, EquipmentType.Weapon },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("每回合首次主动攻击造成伤害后，回复该次伤害25%的生命值（向下取整，每回合最多一次）。", 100)
            },
            EquipmentAcquisitionMethod.Unknown,
            System.Array.Empty<string>(),
            new[] { "weapon", "epic", "lifesteal" },
            100,
            nameKey: "equipment.vampiric_fang.name",
            descriptionKey: "equipment.vampiric_fang.desc",
            assetCode: "EQ30034"),

        // ————————————————————————————————————————————————
        // 恶臭蘑菇：第二章下水道路线稀有事件专属，红/黄/绿三色随机分配（事件不可再获得）
        // ————————————————————————————————————————————————

        [EquipmentIds.StinkyMushroomRed] = new EquipmentDefinition(
            EquipmentIds.StinkyMushroomRed,
            "恶臭蘑菇·红",
            "猩红色菌丝不断释放迟缓神经的孢子。\n你变得迟缓，敌人也同样迟缓。\n你造成的最终伤害×0.75。你受到的最终伤害×0.75。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Rare,
            System.Array.Empty<EquipmentEffect>(),
            EquipmentAcquisitionMethod.Event,
            new[] { "恶臭蘑菇事件" },
            new[] { "accessory", "rare", "story", "event_exclusive" },
            600,
            nameKey: "equipment.equip_stinky_red.name",
            descriptionKey: "equipment.equip_stinky_red.desc",
            assetCode: "EQ30035",
            canAppearInRandomPool: false),

        [EquipmentIds.StinkyMushroomYellow] = new EquipmentDefinition(
            EquipmentIds.StinkyMushroomYellow,
            "恶臭蘑菇·黄",
            "黄色菌盖会疯狂吞噬周围所有能源。\n无论敌我，所有供能系统都会在战斗开始时陷入亏空。\n每场战斗开始时，玩家和所有敌人的初始费用直接设定为-1。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Rare,
            System.Array.Empty<EquipmentEffect>(),
            EquipmentAcquisitionMethod.Event,
            new[] { "恶臭蘑菇事件" },
            new[] { "accessory", "rare", "story", "event_exclusive" },
            600,
            nameKey: "equipment.equip_stinky_yellow.name",
            descriptionKey: "equipment.equip_stinky_yellow.desc",
            assetCode: "EQ30036",
            canAppearInRandomPool: false),

        [EquipmentIds.StinkyMushroomGreen] = new EquipmentDefinition(
            EquipmentIds.StinkyMushroomGreen,
            "恶臭蘑菇·绿",
            "绿色孢子不断侵蚀皮肤与装甲。\n所有伤口都会迅速扩大。\n所有角色受到的最终伤害×1.25。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Rare,
            System.Array.Empty<EquipmentEffect>(),
            EquipmentAcquisitionMethod.Event,
            new[] { "恶臭蘑菇事件" },
            new[] { "accessory", "rare", "story", "event_exclusive" },
            600,
            nameKey: "equipment.equip_stinky_green.name",
            descriptionKey: "equipment.equip_stinky_green.desc",
            assetCode: "EQ30037",
            canAppearInRandomPool: false),

        [EquipmentIds.TimeHourglass] = new EquipmentDefinition(
            EquipmentIds.TimeHourglass,
            "时间沙漏",
            "沙粒逆流，战场的终末被提前投射到了现在。\n战斗开始时，直接触发一次第55回合的掉血效果。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Legendary,
            new[]
            {
                new EquipmentEffect("战斗开始时，直接触发一次第55回合的战场崩坏效果。", 700)
            },
            EquipmentAcquisitionMethod.Reward,
            new[] { "传奇装备选择池" },
            new[] { "accessory", "legendary", "battle_start", "battlefield_collapse", "random_reward" },
            700,
            nameKey: "equipment.accessory_time_hourglass.name",
            descriptionKey: "equipment.accessory_time_hourglass.desc",
            assetCode: "EQ30038"),

        [EquipmentIds.NightVisionGoggles] = new EquipmentDefinition(
            EquipmentIds.NightVisionGoggles,
            "夜视镜",
            "锁定视野中最显眼的目标，其余的都只是噪点。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("单体伤害攻击牌（杀系）造成伤害×1.2。", 500),
                new EquipmentEffect("群体伤害攻击牌（万箭齐发/南蛮入侵/突袭/天体撞击）改为集中命中场上一名敌人，伤害×场上存活敌人数。", 500)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "accessory", "epic", "damage_multiplier", "aoe_to_single" },
            500,
            nameKey: "equipment.accessory_night_vision_goggles.name",
            descriptionKey: "equipment.accessory_night_vision_goggles.desc",
            assetCode: "EQ30039"),

        [EquipmentIds.StealthModule] = new EquipmentDefinition(
            EquipmentIds.StealthModule,
            "隐身模块",
            "折射光路，把佩戴者从战场上悄悄抹去——直到它自己扣下扳机。",
            new[] { EquipmentType.Buff, EquipmentType.Armor },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("战斗开始时获得无敌。", 500),
                new EquipmentEffect("第10回合开始时，或打出费/杀类型牌/锦囊牌时，立即失去无敌。", 500)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "armor", "epic", "invincible", "battle_start" },
            500,
            nameKey: "equipment.armor_stealth_module.name",
            descriptionKey: "equipment.armor_stealth_module.desc",
            assetCode: "EQ20012"),

        [EquipmentIds.Reforger] = new EquipmentDefinition(
            EquipmentIds.Reforger,
            "重铸器",
            "内部的齿轮仍在缓缓转动。\n出售它后，你下一次出售的装备将被替换为另一件同品质装备。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("出售后：下一次出售装备时，将其重铸为另一件同品质装备。", 600)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "accessory", "epic", "shop", "sale", "reforge" },
            600,
            nameKey: "equipment.accessory_reforger.name",
            descriptionKey: "equipment.accessory_reforger.desc",
            assetCode: "EQ30040"),

        [EquipmentIds.HighTemperatureModule] = new EquipmentDefinition(
            EquipmentIds.HighTemperatureModule,
            "高温模块",
            "核心温度被锁定在危险的红线之上。\n你的火属性攻击伤害×1.5。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Epic,
            new[]
            {
                new EquipmentEffect("你的火属性攻击伤害×1.5。装备、技能与环境火伤不受影响。", 600)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "accessory", "epic", "shop", "fire", "damage_multiplier" },
            600,
            nameKey: "equipment.accessory_high_temperature_module.name",
            descriptionKey: "equipment.accessory_high_temperature_module.desc",
            assetCode: "EQ30041"),

        [EquipmentIds.PoisonDan] = new EquipmentDefinition(
            EquipmentIds.PoisonDan,
            "毒丹",
            "似乎是一位穷凶极恶之人所调制。\n战斗内所有回血效果转为扣血效果；每次转换后获得转换值10%的最大生命值（最低1点）。",
            new[] { EquipmentType.Buff, EquipmentType.Accessory },
            EquipmentRarity.Legendary,
            new[]
            {
                new EquipmentEffect("战斗内所有回血效果转为扣血；每次转换后最大生命值+转换值的10%（最低1）。", 600)
            },
            EquipmentAcquisitionMethod.Shop,
            System.Array.Empty<string>(),
            new[] { "accessory", "legendary", "shop", "heal_conversion", "max_hp" },
            600,
            nameKey: "equipment.accessory_poison_dan.name",
            descriptionKey: "equipment.accessory_poison_dan.desc",
            assetCode: "EQ30042")
    };

    /// <summary>
    /// Core System 的公开入口：GetEquipment。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EquipmentDefinition? GetEquipment(string id)
    {
        return Definitions.TryGetValue(id, out var definition) ? definition : null;
    }

    /// <summary>
    /// Core System 的公开入口：GetAllEquipments。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static IReadOnlyList<EquipmentDefinition> GetAllEquipments()
    {
        return new List<EquipmentDefinition>(Definitions.Values);
    }
}
