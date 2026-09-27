//////////////////////////////////////////////////////////
// 文件：Scripts/FactionFate/FactionFateIds.cs
//
// 模块：Faction Fate System
//
// 职责：
// 1. 承载阵营命运的稳定 ID 常量，供数据定义/状态判定/调试面板统一引用。
// 2. 为其它模块提供清晰、稳定的调用边界。
//
// 不负责：
// × 处理无关模块的业务规则。
// × 绕过既有 Manager 或 Trigger 流程直接改写跨系统状态。
//////////////////////////////////////////////////////////

/// <summary>
/// Faction Fate System 的公开类：FactionFateIds。
///
/// 阵营命运 ID 使用稳定字符串而不是枚举——和项目里
/// EquipmentIds/SkillIds 等既有 Id 常量类风格保持一致，方便存档/调试直接对比字符串。
/// </summary>
public static class FactionFateIds
{
    public const string Reroll = "qun_fate_reroll";
    public const string Reforge = "qun_fate_reforge";
    public const string Dice = "qun_fate_dice";
    public const string DoubleInitialChoice = "qun_fate_double_initial_choice";
    public const string QunChipReconfiguration = "qun_fate_chip_reconfiguration";
    public const string QunFirstTwoEquipmentUpgrade = "qun_fate_first_two_equipment_upgrade";

    // 魏阵营命运（Phase 1 新增）。
    public const string WeiGoldenReserve = "wei_fate_golden_reserve";
    public const string WeiBackupBattery = "wei_fate_backup_battery";
    public const string WeiBattleSettlement = "wei_fate_battle_settlement";
    public const string WeiOfficialPrivilege = "wei_fate_official_privilege";
    public const string WeiDoubleCampaign = "wei_fate_double_campaign";
    public const string WeiDoubleChipEffect = "wei_fate_double_chip_effect";

    // 蜀阵营命运（Phase 2 新增）。
    public const string ShuUnifiedTactics = "shu_fate_unified_tactics";
    public const string ShuInitiative = "shu_fate_initiative";
    public const string ShuTrickResource = "shu_fate_trick_resource";
    public const string ShuPeachWineUnity = "shu_fate_peach_wine_unity";
    public const string ShuFirstTrickFree = "shu_fate_first_trick_free";
    public const string ShuMuNiuLiuMaBossReward = "shu_fate_mu_niu_liu_ma_boss_reward";

    // 旧存档兼容：不再进入任何新 Run 的蜀命运候选池。
    public const string ShuChainStrategy = "shu_fate_chain_strategy";
    public const string ShuStrategyAmplification = "shu_fate_strategy_amplification";

    // 吴阵营命运（Phase 3 新增）。
    public const string WuBossRewardReplace = "wu_fate_boss_reward_replace";
    public const string WuAccessorySlot = "wu_fate_accessory_slot";
    public const string WuFirstPurchaseFree = "wu_fate_first_purchase_free";
    public const string WuEquipmentReselect = "wu_fate_equipment_reselect";
    public const string WuBattleDividend = "wu_fate_battle_dividend";
}
