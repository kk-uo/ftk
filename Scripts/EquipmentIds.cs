//////////////////////////////////////////////////////////
// 文件：Scripts/EquipmentIds.cs
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

public static class EquipmentIds
{
    public const string SilverLion = "silver_lion";
    public const string RustShield = "rust_shield";
    public const string BenevolentKing = "benevolent_king";
    public const string RustBlueSteelSword = "rust_blue_steel_sword";
    public const string BlueSteelSword = "blue_steel_sword";
    public const string RustSword = "rust_sword";
    public const string RustSpear = "rust_spear";
    public const string YellowTalisman = "yellow_talisman";
    public const string WinePouch = "wine_pouch";
    public const string TigerTally = "tiger_tally";
    public const string Dan = "dan";
    public const string MysteriousChip = "mysterious_chip";

    // 新增装备（Task 5）
    public const string Tengjia = "armor_tengjia";
    public const string ScaleArmor = "armor_scale";
    public const string WhiteHorse = "mount_white_horse";
    public const string DiLu = "vehicle_di_lu";
    public const string ZhuaHuangFeiDian = "vehicle_zhua_huang_fei_dian";
    public const string HanXueBaoMa = "vehicle_han_xue_bao_ma";

    // 新增装备（精英掉落 / 商店）
    public const string FirePatternSilverSpear = "weapon_fire_pattern_silver_spear";
    public const string HookChain = "weapon_hook_chain";

    // 新增装备：战鼓（Boss掉落）
    public const string WarDrum = "equipment_war_drum";

    // 新增装备（第二章）
    public const string IronHeavyArmor = "armor_iron_heavy";
    public const string JetMace = "weapon_jet_mace";
    public const string HeavyHammer = "weapon_heavy_hammer";

    // 新增装备（路线系统）
    public const string SoulStone = "soul_stone";
    public const string ForgottenStone = "forgotten_stone";
    public const string StatueCore = "statue_core";
    public const string SpringEssence = "spring_essence";
    public const string WitchScalp = "witch_scalp";
    public const string XianNiang = "xian_niang";
    public const string TyrantCrown = "tyrant_crown";
    public const string MoonGem = "moon_gem";
    public const string CurseBlade = "curse_blade";
    public const string BigBoneClub = "big_bone_club";

    // 新增装备（集智体 / 第二章精英）
    public const string ThunderSpear = "weapon_thunder_spear";
    public const string AttackChip = "chip_attack";

    // 传奇装备（卧龙集智体 Boss 掉落）
    public const string ClampExoskeleton = "clamp_exoskeleton";

    // 新增装备（破碎情感组件 / 扩容芯片）
    public const string BrokenEmotionalComponent = "broken_emotional_component";
    public const string ExpansionChip = "expansion_chip";

    // 新增装备（武器 / 饰品 — 商店批次）
    public const string ShortBow = "weapon_short_bow";
    public const string LongBow = "weapon_long_bow";
    public const string Poison = "accessory_poison";
    public const string Gunpowder = "accessory_gunpowder";
    public const string RustGuDingDao = "weapon_rust_gu_ding_dao";
    public const string GuDingDao = "weapon_gu_ding_dao";
    public const string TrueGuDingDao = "weapon_true_gu_ding_dao";
    public const string SproutingBonsai = "accessory_sprouting_bonsai";

    // 左·慈专属装备
    public const string XianHao = "xian_hao";

    // 技能芯片：芯片三选一中以1%概率替换一个基础芯片，选择后立即随机获得一项技能。
    public const string SkillChip = "skill_chip";

    // 新增装备（商店饰品）
    public const string MiJiang = "accessory_mi_jiang";
    public const string PanXiao = "accessory_pan_xiao";

    // 新增装备（合金盾 / 赤兔 / 木牛流马）
    public const string GangDun = "armor_gang_dun";     // 合金盾（锈盾升级）
    public const string GiantShield = "armor_giant_shield"; // 巨人盾
    public const string FoldingKnife = "weapon_folding_knife"; // 折叠刀
    public const string ChiTu = "mount_chi_tu";
    public const string MuNiuLiuMa = "mount_mu_niu_liu_ma";
    public const string TreasureDonkey = "treasure_donkey";

    // 锈类装备升级目标（合金剑 / 合金矛）
    public const string HejinJian = "weapon_hejin_jian";   // 合金剑（锈剑升级）
    public const string HejinMao = "weapon_hejin_mao";     // 合金矛（锈矛升级）

    // 冰蓝装甲：战斗开始获得 7 费，但永久禁止通过出费获得费用
    public const string IceBlueArmor = "armor_ice_blue";

    // 蛮族营寨事件奖励
    public const string BarbarianTooth = "barbarian_tooth";

    // 蜀汉共生体 Boss 专属武器
    public const string ZhangBaSheMao = "weapon_zhang_ba_she_mao";
    public const string QingLongYanYueDao = "weapon_qing_long_yan_yue_dao";

    // 古蜀铸炉事件奖励（赵云专属隐藏选项）
    public const string TrueBlueSteelSword = "true_blue_steel_sword";

    // 七星坛事件奖励
    public const string ZhuQueYuShan = "zhuque_yushan";

    // 商店普通护甲
    public const string BanJia = "armor_ban_jia";

    // 传奇武器：诸葛连弩
    public const string ZhugeCrossbow = "zhuge_crossbow";

    // 导体护甲：免疫Thunder伤害，获得10点生命
    public const string Conductor = "armor_conductor";

    // 电击镣铐：每回合开始自伤1点Thunder，之后本回合受到伤害-10%
    public const string ElectricShackle = "accessory_electric_shackle";

    // 传奇武器：瘟疫权杖
    public const string PlagueStaff = "weapon_plague_staff";

    // 打折券：商店商品价格-25%（被动，持有即生效）
    public const string DiscountVoucher = "accessory_discount_voucher";

    // 瘫痪装置：开局对所有敌人造成真实伤害
    public const string ParalysisDevice = "accessory_paralysis_device";

    // 精灵尘：桃回复量翻倍
    public const string ElfDust = "accessory_elf_dust";

    // 防水模块：最大生命值 +10
    public const string WaterproofModule = "accessory_waterproof_module";

    // 神秘补剂：当前生命（含临时生命）≥120时，普通杀视为必中杀。
    public const string MysteriousPotion = "boss_mysterious_potion";

    // 黄金雕像：背包生效（EquipmentActivationType.Inventory，无需装备），金币获得+20%
    public const string GoldenStatue = "accessory_golden_statue";

    // 大亨之铠：初始事件㉑【财富契约】三选一。+10最大生命；每场战斗第1/5/10次受到实际伤害时+50金币
    public const string TycoonArmor = "armor_tycoon";

    // 投机者之刃：初始事件㉑【财富契约】三选一。杀类型伤害+2；每场战斗第1/5/10次杀命中时+30金币
    public const string SpeculatorBlade = "weapon_speculator_blade";

    // 收割者：初始事件㉑【财富契约】三选一。敌人生命≤斩杀线（10%）时下一次伤害直接斩杀，成功斩杀+25金币
    public const string Reaper = "weapon_reaper";

    // 吸血之牙：每回合首次主动攻击造成伤害后，回复本次伤害25%生命值（向下取整）
    public const string VampiricFang = "weapon_vampiric_fang";

    // 恶臭蘑菇（第二章下水道路线稀有事件专属，红/黄/绿三色随机分配）
    public const string StinkyMushroomRed = "equip_stinky_red";
    public const string StinkyMushroomYellow = "equip_stinky_yellow";
    public const string StinkyMushroomGreen = "equip_stinky_green";

    // 时间沙漏：战斗开始时提前触发一次第55回合的战场崩坏伤害
    public const string TimeHourglass = "accessory_time_hourglass";

    // 夜视镜：单体伤害攻击牌伤害×1.2；群体伤害攻击牌改为集中命中场上一名敌人，伤害×场上敌人数
    public const string NightVisionGoggles = "accessory_night_vision_goggles";

    // 隐身模块：战斗开始时获得无敌；第10回合开始时，或打出费/杀类型牌/锦囊牌时，失去无敌
    public const string StealthModule = "armor_stealth_module";

    // 重铸器：出售它后，下一次出售装备改为同品质随机重铸
    public const string Reforger = "accessory_reforger";

    // 高温模块：火属性攻击伤害×1.5
    public const string HighTemperatureModule = "accessory_high_temperature_module";

    // 毒丹：战斗内的生命回复改为生命损失，并按转换量提高最大生命值。
    public const string PoisonDan = "accessory_poison_dan";
}
