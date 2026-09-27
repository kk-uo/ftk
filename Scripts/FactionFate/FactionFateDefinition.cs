//////////////////////////////////////////////////////////
// 文件：Scripts/FactionFate/FactionFateDefinition.cs
//
// 模块：Faction Fate System
//
// 职责：
// 1. 承载魏、蜀、吴、群四个阵营的命运静态数据定义。
// 2. 为其它模块提供清晰、稳定的调用边界。
//
// 不负责：
// × 承载任何随局内进度变化的状态（状态见 FactionFateState/FactionFateManager）。
//////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Faction Fate System 的公开类：FactionFateDefinition。
///
/// 只描述"是什么"（Id/文案 Key/所属阵营），不承载随局内进度变化的状态；
/// 和项目里 EquipmentDefinition/SkillDefinition 的“静态数据类”角色一致。
/// </summary>
public sealed class FactionFateDefinition
{
    public FactionFateDefinition(string id, string nameKey, string descriptionKey, Faction faction)
    {
        Id = id;
        NameKey = nameKey;
        DescriptionKey = descriptionKey;
        Faction = faction;
    }

    public string Id { get; }
    public string NameKey { get; }
    public string DescriptionKey { get; }
    public Faction Faction { get; }
}

/// <summary>
/// Faction Fate System 的公开类：FactionFateDatabase。
///
/// 按阵营维护候选池，并提供用于展示与按Id查询的总集合。
/// </summary>
public static class FactionFateDatabase
{
    public static readonly IReadOnlyList<FactionFateDefinition> AllQunFates = new List<FactionFateDefinition>
    {
        new(FactionFateIds.Reroll, "factionfate.qun_fate_reroll.name", "factionfate.qun_fate_reroll.desc", Faction.Qun),
        new(FactionFateIds.Reforge, "factionfate.qun_fate_reforge.name", "factionfate.qun_fate_reforge.desc", Faction.Qun),
        new(FactionFateIds.Dice, "factionfate.qun_fate_dice.name", "factionfate.qun_fate_dice.desc", Faction.Qun),
        new(FactionFateIds.DoubleInitialChoice, "factionfate.qun_fate_double_initial_choice.name", "factionfate.qun_fate_double_initial_choice.desc", Faction.Qun),
        new(FactionFateIds.QunChipReconfiguration, "factionfate.qun_fate_chip_reconfiguration.name", "factionfate.qun_fate_chip_reconfiguration.desc", Faction.Qun),
        new(FactionFateIds.QunFirstTwoEquipmentUpgrade, "factionfate.qun_fate_first_two_equipment_upgrade.name", "factionfate.qun_fate_first_two_equipment_upgrade.desc", Faction.Qun),
    };

    // 魏阵营命运六选一。
    public static readonly IReadOnlyList<FactionFateDefinition> AllWeiFates = new List<FactionFateDefinition>
    {
        new(FactionFateIds.WeiGoldenReserve, "factionfate.wei_fate_golden_reserve.name", "factionfate.wei_fate_golden_reserve.desc", Faction.Wei),
        new(FactionFateIds.WeiBackupBattery, "factionfate.wei_fate_backup_battery.name", "factionfate.wei_fate_backup_battery.desc", Faction.Wei),
        new(FactionFateIds.WeiBattleSettlement, "factionfate.wei_fate_battle_settlement.name", "factionfate.wei_fate_battle_settlement.desc", Faction.Wei),
        new(FactionFateIds.WeiOfficialPrivilege, "factionfate.wei_fate_official_privilege.name", "factionfate.wei_fate_official_privilege.desc", Faction.Wei),
        new(FactionFateIds.WeiDoubleCampaign, "factionfate.wei_fate_double_campaign.name", "factionfate.wei_fate_double_campaign.desc", Faction.Wei),
        new(FactionFateIds.WeiDoubleChipEffect, "factionfate.wei_fate_double_chip_effect.name", "factionfate.wei_fate_double_chip_effect.desc", Faction.Wei),
    };

    // 蜀阵营命运固定为以下六项；未列出的旧命运不再进入新 Run 抽取池。
    public static readonly IReadOnlyList<FactionFateDefinition> AllShuFates = new List<FactionFateDefinition>
    {
        new(FactionFateIds.ShuUnifiedTactics, "factionfate.shu_fate_unified_tactics.name", "factionfate.shu_fate_unified_tactics.desc", Faction.Shu),
        new(FactionFateIds.ShuInitiative, "factionfate.shu_fate_initiative.name", "factionfate.shu_fate_initiative.desc", Faction.Shu),
        new(FactionFateIds.ShuTrickResource, "factionfate.shu_fate_trick_resource.name", "factionfate.shu_fate_trick_resource.desc", Faction.Shu),
        new(FactionFateIds.ShuPeachWineUnity, "factionfate.shu_fate_peach_wine_unity.name", "factionfate.shu_fate_peach_wine_unity.desc", Faction.Shu),
        new(FactionFateIds.ShuFirstTrickFree, "factionfate.shu_fate_first_trick_free.name", "factionfate.shu_fate_first_trick_free.desc", Faction.Shu),
        new(FactionFateIds.ShuMuNiuLiuMaBossReward, "factionfate.shu_fate_mu_niu_liu_ma_boss_reward.name", "factionfate.shu_fate_mu_niu_liu_ma_boss_reward.desc", Faction.Shu),
    };

    // 吴阵营命运五选一（Phase 3 新增）。
    public static readonly IReadOnlyList<FactionFateDefinition> AllWuFates = new List<FactionFateDefinition>
    {
        new(FactionFateIds.WuBossRewardReplace, "factionfate.wu_fate_boss_reward_replace.name", "factionfate.wu_fate_boss_reward_replace.desc", Faction.Wu),
        new(FactionFateIds.WuAccessorySlot, "factionfate.wu_fate_accessory_slot.name", "factionfate.wu_fate_accessory_slot.desc", Faction.Wu),
        new(FactionFateIds.WuFirstPurchaseFree, "factionfate.wu_fate_first_purchase_free.name", "factionfate.wu_fate_first_purchase_free.desc", Faction.Wu),
        new(FactionFateIds.WuEquipmentReselect, "factionfate.wu_fate_equipment_reselect.name", "factionfate.wu_fate_equipment_reselect.desc", Faction.Wu),
        new(FactionFateIds.WuBattleDividend, "factionfate.wu_fate_battle_dividend.name", "factionfate.wu_fate_battle_dividend.desc", Faction.Wu),
    };

    public static readonly IReadOnlyList<FactionFateDefinition> AllFates =
        AllQunFates.Concat(AllWeiFates).Concat(AllShuFates).Concat(AllWuFates).ToList();

    public static FactionFateDefinition? Get(string? id) => AllFates.FirstOrDefault(f => f.Id == id);
}
