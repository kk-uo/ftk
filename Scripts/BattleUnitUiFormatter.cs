//////////////////////////////////////////////////////////
// 文件：Scripts/BattleUnitUiFormatter.cs
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
using System.Linq;

/// <summary>
/// Core System 的公开枚举：BattleUnitStatusKind。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum BattleUnitStatusKind
{
    Buff,
    Debuff,
    Special
}

/// <summary>
/// Core System 的公开类：BattleUnitStatusEntry。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BattleUnitStatusEntry
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string IconText { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string TypeText { get; init; } = string.Empty;
    public string SourceText { get; init; } = string.Empty;
    public string DurationText { get; init; } = string.Empty;
    public int StackCount { get; set; } = 1;
    public bool Stackable { get; init; }
    public BattleUnitStatusKind Kind { get; init; }
}

/// <summary>
/// Core System 的公开类：BattleUnitUiFormatter。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class BattleUnitUiFormatter
{
    private static string L(string key) => Localization.Get(key);

    /// <summary>
    /// Core System 的公开入口：GetFactionText。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetFactionText(BattleUnit unit)
    {
        if (unit is EnemyInstance enemy)
        {
            return EnemyInfoFormatter.GetFactionText(enemy.Definition);
        }

        if (unit is Player player)
        {
            return player.Character.Data.Faction switch
            {
                Faction.Shu => Localization.Get("faction.shu"),
                Faction.Wei => Localization.Get("faction.wei"),
                Faction.Wu => Localization.Get("faction.wu"),
                Faction.Qun => Localization.Get("faction.qun"),
                _ => Localization.Get("faction.unknown")
            };
        }

        return Localization.Get("faction.unknown");
    }

    /// <summary>
    /// Core System 的公开入口：GetDangerReason。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetDangerReason(BattleUnit unit)
    {
        return unit is EnemyInstance enemy
            ? EnemyInfoFormatter.GetDangerReason(enemy.Definition.LoreId)
            : string.Empty;
    }

    /// <summary>
    /// 读取战斗单位的技能展示数据。
    ///
    /// 该方法只负责把运行时技能转换为 UI 可展示的本地化文本，
    /// 不修改技能列表，也不触发任何技能效果。
    /// </summary>
    public static List<(string Name, string Rarity, string Type, string Description)> GetSkills(BattleUnit unit)
    {
        var result = new List<(string, string, string, string)>();
        if (unit is not Player player)
        {
            return result;
        }

        foreach (var skill in player.Skills)
        {
            var typeText = string.Join(" / ", skill.Kinds.Select(SkillText.GetKindName));
            result.Add((Localization.GetName(skill), SkillText.GetRarityName(skill.Rarity), typeText, Localization.GetDescription(skill)));
        }

        return result;
    }

    /// <summary>
    /// 读取战斗单位的装备展示数据。
    ///
    /// 该方法统一处理玩家装备与敌人装备的展示格式，
    /// 避免状态面板直接依赖不同单位的装备存储细节。
    /// </summary>
    public static List<(string Name, string Rarity, string Type, string Description)> GetEquipments(BattleUnit unit)
    {
        var result = new List<(string, string, string, string)>();
        if (unit is EnemyInstance enemy)
        {
            foreach (var equipment in enemy.Equipments)
            {
                result.Add((Localization.GetName(equipment), GetEquipmentRarityName(equipment.Rarity), GetEquipmentTypeText(equipment.Types), BuildEquipmentDescription(equipment)));
            }

            return result;
        }

        if (unit is Player)
        {
            foreach (var equipment in GameManager.Equipment)
            {
                result.Add((Localization.GetName(equipment), GetEquipmentRarityName(equipment.Rarity), GetEquipmentTypeText(equipment.Types), BuildEquipmentDescription(equipment)));
            }
        }

        return result;
    }

    /// <summary>
    /// Core System 的公开入口：GetStatuses。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static List<BattleUnitStatusEntry> GetStatuses(BattleUnit unit, BattleContext? context)
    {
        var entries = new List<BattleUnitStatusEntry>();
        if (unit is not Player player)
        {
            if (unit is EnemyInstance enemy)
            {
                if (RunBuffManager.CountStacks(RunBuffIds.Wet) > 0
                    && !enemy.HasEquipment(EquipmentIds.WaterproofModule))
                {
                    entries.Add(Create("wet", L("status.wet.name"), "湿", L("status.type.debuff"), L("runbuff.src"), L("status.dur.chapter"), L("status.wet.desc"), BattleUnitStatusKind.Debuff));
                }
            }
            return entries;
        }

        if (RunBuffManager.CountStacks(RunBuffIds.Wet) > 0 && !GameManager.HasEquipment(EquipmentIds.WaterproofModule))
        {
            entries.Add(Create("wet", L("status.wet.name"), "湿", L("status.type.debuff"), L("runbuff.src"), L("status.dur.chapter"), L("status.wet.desc"), BattleUnitStatusKind.Debuff));
        }

        if (player.IsFrozen)
        {
            entries.Add(Create("frozen", L("status.frozen.name"), "❄", L("status.type.control"), L("status.src.combat"), Localization.GetFmt("status.dur.remaining_turns_fmt", player.FrozenTurnsRemaining), L("status.frozen.desc"), BattleUnitStatusKind.Debuff));
        }

        if (player.IsStunned)
        {
            entries.Add(Create("stun", L("status.stun.name"), "晕", L("status.type.control"), L("status.src.combat"), Localization.GetFmt("status.dur.remaining_turns_fmt", player.StunTurnsRemaining), L("status.stun.desc"), BattleUnitStatusKind.Debuff));
        }

        if (player.HasFerocity)
        {
            entries.Add(Create("ferocity", L("status.ferocity.name"), "凶", L("status.type.buff"), L("status.src.combat"), L("status.dur.decreasing"), L("status.ferocity.desc"), BattleUnitStatusKind.Buff, true, player.FerocityLayers));
        }

        if (player.HasWeakness)
        {
            entries.Add(Create("weakness", L("status.weakness.name"), "弱", L("status.type.debuff"), L("status.src.combat"), Localization.GetFmt("status.dur.remaining_turns_fmt", player.WeaknessLayers), L("status.weakness.desc"), BattleUnitStatusKind.Debuff, true, player.WeaknessLayers));
        }

        if (player.JiGuActive)
        {
            entries.Add(Create("jigu", L("status.jigu.name"), "鼓", L("status.type.buff"), L("status.src.skill"), Localization.GetFmt("status.dur.remaining_turns_fmt", player.JiGuTurnsRemaining), L("status.jigu.desc"), BattleUnitStatusKind.Buff));
        }

        if (player.StealthModuleActive)
        {
            entries.Add(Create("stealth_module", L("status.stealth_module.name"), "隐", L("status.type.buff"), L("status.src.equip"), L("status.dur.ongoing"), L("status.stealth_module.desc"), BattleUnitStatusKind.Buff));
        }

        if (player.WarDrumActive)
        {
            entries.Add(Create("war_drum", L("status.wardrum.name"), "鼓", L("status.type.buff"), L("status.src.equip"), L("status.dur.active"), L("status.wardrum.desc"), BattleUnitStatusKind.Buff));
        }
        else if (player.WarDrumPending)
        {
            entries.Add(Create("war_drum_pending", L("status.wardrum_pending.name"), "鼓", L("status.type.special"), L("status.src.equip"), L("status.dur.next_round"), L("status.wardrum_pending.desc"), BattleUnitStatusKind.Special));
        }

        if (player.InShadowState)
        {
            entries.Add(Create("shadow_state", L("status.shadow.name"), "影", L("status.type.buff"), L("status.src.skill"), Localization.GetFmt("status.dur.remaining_turns_fmt", player.RemainingTurns), L("status.shadow.desc"), BattleUnitStatusKind.Buff));
        }

        if (player.LianyingPrepared)
        {
            entries.Add(Create("lianying_ready", L("status.lianying_ready.name"), "营", L("status.type.special"), L("status.src.skill"), L("status.dur.pending"), L("status.lianying_ready.desc"), BattleUnitStatusKind.Special));
        }

        if (player.LianyingFreeKillAvailable)
        {
            entries.Add(Create("lianying_free_kill", L("status.lianying_free.name"), "营", L("status.type.buff"), L("status.src.skill"), L("status.dur.this_round"), L("status.lianying_free.desc"), BattleUnitStatusKind.Buff));
        }

        if (player.GuanxingPhase == GuanxingPhase.Recording)
        {
            entries.Add(Create("guanxing_record", L("status.guanxing_rec.name"), "观", L("status.type.special"), L("status.src.skill"), L("status.dur.this_round"), L("status.guanxing_rec.desc"), BattleUnitStatusKind.Special));
        }
        else if (player.GuanxingPhase == GuanxingPhase.Repeating && player.GuanxingRecordedCardType.HasValue)
        {
            entries.Add(Create("guanxing_repeat", L("status.guanxing_rep.name"), "观", L("status.type.special"), L("status.src.skill"), Localization.GetFmt("status.dur.remaining_turns_fmt", player.GuanxingRepeatsRemaining), Localization.GetFmt("status.guanxing_rep.desc_fmt", BattleRules.GetCardName(player.GuanxingRecordedCardType.Value)), BattleUnitStatusKind.Special));
        }

        if (context != null)
        {
            if (player.Team == BattleTeam.Player)
            {
                if (context.PlayerDodgeDefenseActive)
                    entries.Add(Create("dodge", L("status.dodge.name"), "闪", L("status.type.buff"), L("status.src.defense"), L("status.dur.this_round"), L("status.dodge.desc"), BattleUnitStatusKind.Buff));
                if (context.PlayerCounterDefenseActive)
                    entries.Add(Create("counter", L("status.counter.name"), "懈", L("status.type.buff"), L("status.src.defense"), L("status.dur.this_round"), L("status.counter.desc"), BattleUnitStatusKind.Buff));
                if (context.PlayerPeachShieldLayers > 0)
                    entries.Add(Create("peach_shield", L("status.peach_shield.name"), "桃", L("status.type.buff"), L("status.src.card"), Localization.GetFmt("status.dur.stacks_fmt", context.PlayerPeachShieldLayers), L("status.peach_shield.desc"), BattleUnitStatusKind.Buff, true, context.PlayerPeachShieldLayers));
                if (context.PlayerWineShieldLayers > 0)
                    entries.Add(Create("wine_shield", L("status.wine_shield.name"), "酒", L("status.type.buff"), L("status.src.card"), Localization.GetFmt("status.dur.stacks_fmt", context.PlayerWineShieldLayers), L("status.wine_shield.desc"), BattleUnitStatusKind.Buff, true, context.PlayerWineShieldLayers));
                if (context.PlayerQingnangDodgeLayers > 0)
                    entries.Add(Create("qingnang_dodge", L("status.qingnang_dodge.name"), "囊", L("status.type.buff"), L("status.src.skill"), Localization.GetFmt("status.dur.stacks_fmt", context.PlayerQingnangDodgeLayers), L("status.qingnang_dodge.desc"), BattleUnitStatusKind.Buff, true, context.PlayerQingnangDodgeLayers));
            }
            else
            {
                var unitKey = BattleContext.GetUnitStateKey(player);
                if (context.EnemyDodgeDefenseActive.TryGetValue(unitKey, out var dodgeActive) && dodgeActive)
                    entries.Add(Create("dodge", L("status.dodge.name"), "闪", L("status.type.buff"), L("status.src.defense"), L("status.dur.this_round"), L("status.dodge.desc"), BattleUnitStatusKind.Buff));
                if (context.EnemyCounterDefenseActive.TryGetValue(unitKey, out var counterActive) && counterActive)
                    entries.Add(Create("counter", L("status.counter.name"), "懈", L("status.type.buff"), L("status.src.defense"), L("status.dur.this_round"), L("status.counter.desc"), BattleUnitStatusKind.Buff));
                if (context.EnemyPeachShieldLayers.TryGetValue(unitKey, out var peachLayers) && peachLayers > 0)
                    entries.Add(Create("peach_shield", L("status.peach_shield.name"), "桃", L("status.type.buff"), L("status.src.card"), Localization.GetFmt("status.dur.stacks_fmt", peachLayers), L("status.peach_shield.desc"), BattleUnitStatusKind.Buff, true, peachLayers));
                if (context.EnemyWineShieldLayers.TryGetValue(unitKey, out var wineLayers) && wineLayers > 0)
                    entries.Add(Create("wine_shield", L("status.wine_shield.name"), "酒", L("status.type.buff"), L("status.src.card"), Localization.GetFmt("status.dur.stacks_fmt", wineLayers), L("status.wine_shield.desc"), BattleUnitStatusKind.Buff, true, wineLayers));
                if (context.EnemyQingnangDodgeLayers.TryGetValue(unitKey, out var qingnangLayers) && qingnangLayers > 0)
                    entries.Add(Create("qingnang_dodge", L("status.qingnang_dodge.name"), "囊", L("status.type.buff"), L("status.src.skill"), Localization.GetFmt("status.dur.stacks_fmt", qingnangLayers), L("status.qingnang_dodge.desc"), BattleUnitStatusKind.Buff, true, qingnangLayers));
            }
        }

        if (player.RuntimeStates.TryGetValue("poison_count", out var poisonState) && poisonState is int poisonCount && poisonCount > 0)
        {
            entries.Add(Create("poison", L("status.poison.name"), "毒", L("status.type.debuff"), L("status.src.equip_status"), Localization.GetFmt("status.dur.stacks_fmt", poisonCount), L("status.poison.desc"), BattleUnitStatusKind.Debuff, true, poisonCount));
        }

        if (player.RuntimeStates.TryGetValue("plague_stacks", out var plagueState) && plagueState is int plagueStacks && plagueStacks > 0)
        {
            entries.Add(Create("plague", L("status.plague.name"), "疫", L("status.type.debuff"), L("status.src.skill_status"), Localization.GetFmt("status.dur.plague_fmt", plagueStacks), L("status.plague.desc"), BattleUnitStatusKind.Debuff, true, plagueStacks));
        }

        if (player.RuntimeStates.TryGetValue("meihuo_shield", out var meihuoVal) && meihuoVal is true)
        {
            entries.Add(Create("meihuo_shield", L("status.meihuo_shield.name"), "魅", L("status.type.buff"), L("status.src.skill_status"), L("status.dur.ongoing"), L("status.meihuo_shield.desc"), BattleUnitStatusKind.Buff));
        }

        var rendeStateKey = player.Team == BattleTeam.Player
            ? "rende_shield_player"
            : "rende_shield";
        if (player.RuntimeStates.TryGetValue(rendeStateKey, out var rendeValue)
            && rendeValue is int rendeLayers
            && rendeLayers > 0)
        {
            entries.Add(Create(
                "rende_shield",
                L("status.rende_shield.name"),
                "仁",
                L("status.type.buff"),
                L("status.src.skill_status"),
                Localization.GetFmt("status.dur.stacks_fmt", rendeLayers),
                L("status.rende_shield.desc"),
                BattleUnitStatusKind.Buff,
                true,
                rendeLayers));
        }

        if (player.RuntimeStates.TryGetValue("meihuo_charmed", out var charmedValue) && charmedValue is true)
        {
            entries.Add(Create("meihuo_charmed", L("status.meihuo_charmed.name"), "惑", L("status.type.debuff"), L("status.src.skill_status"), L("status.dur.ongoing"), L("status.meihuo_charmed.desc"), BattleUnitStatusKind.Debuff));
        }

        if (player.RuntimeStates.TryGetValue("hunzi_triggered", out var hunziTriggered) && hunziTriggered is true
            && player.RuntimeStates.TryGetValue("hunzi_original_max_hp", out var hunziOrigObj) && hunziOrigObj is int hunziOrigMax)
        {
            entries.Add(Create("hunzi", L("status.hunzi.name"), "魂", L("status.type.debuff"), L("status.src.skill"), L("status.dur.end_of_battle"), Localization.GetFmt("status.hunzi.desc_fmt", hunziOrigMax, player.MaxHealth), BattleUnitStatusKind.Debuff));
        }

        // 幻象触手：显示当前免疫的攻击牌类型
        if (player is EnemyInstance illusionEnemy && illusionEnemy.HasSkill(SkillIds.Illusion))
        {
            var illusionCardName = L("status.illusion.none");
            if (illusionEnemy.RuntimeStates.TryGetValue(HuanXiangKeys.IllusionCardType, out var illusionTypeObj)
                && illusionTypeObj is CardType illusionCardType)
            {
                illusionCardName = BattleRules.GetCardName(illusionCardType);
            }
            entries.Add(Create(
                "illusion",
                Localization.GetFmt("status.illusion.name_fmt", illusionCardName),
                "幻",
                L("status.type.special"),
                L("status.src.skill"),
                L("status.dur.permanent"),
                L("status.illusion.desc"),
                BattleUnitStatusKind.Special));
        }

        // 粘液虚弱（玩家状态）
        if (player.Team == BattleTeam.Player
            && player.RuntimeStates.TryGetValue(HuanXiangKeys.SlimeDebuff, out var slimeVal)
            && slimeVal is true)
        {
            entries.Add(Create(
                "slime_debuff",
                L("status.slime_debuff.name"),
                "粘",
                L("status.type.debuff"),
                L("status.src.skill_status"),
                L("status.dur.next_attack"),
                L("status.slime_debuff.desc"),
                BattleUnitStatusKind.Debuff));
        }

        // 局外Buff（RunBuff）只属于玩家本人，不应出现在敌方状态栏。
        if (player.Team == BattleTeam.Player)
        {
            foreach (var runBuff in BuildRunBuffEntries())
            {
                entries.Add(runBuff);
            }
        }

        return entries
            .OrderBy(entry => entry.Kind)
            .ThenBy(entry => entry.Name)
            .ToList();
    }

    private static List<BattleUnitStatusEntry> BuildRunBuffEntries()
    {
        var grouped = new Dictionary<string, BattleUnitStatusEntry>();
        foreach (var buff in RunBuffManager.ActiveBuffs)
        {
            if (!grouped.TryGetValue(buff.BuffId, out var entry))
            {
                entry = Create(
                    buff.BuffId,
                    Localization.GetName(buff.Definition),
                    GetRunBuffIcon(buff.BuffId, Localization.GetName(buff.Definition)),
                    GetRunBuffTypeText(buff.Definition.Type),
                    L("runbuff.src"),
                    buff.RemainingBattles < 0 ? L("runbuff.dur.ongoing") : Localization.GetFmt("runbuff.dur.battles_fmt", buff.RemainingBattles),
                    Localization.GetDescription(buff.Definition),
                    buff.Definition.Type == RunBuffType.Curse ? BattleUnitStatusKind.Debuff : BattleUnitStatusKind.Buff,
                    buff.Definition.Stackable,
                    1);
                grouped.Add(buff.BuffId, entry);
            }
            else if (entry.Stackable)
            {
                entry.StackCount += 1;
            }
        }

        return grouped.Values.ToList();
    }

    private static BattleUnitStatusEntry Create(
        string id,
        string name,
        string icon,
        string type,
        string source,
        string duration,
        string description,
        BattleUnitStatusKind kind,
        bool stackable = false,
        int stackCount = 1)
    {
        return new BattleUnitStatusEntry
        {
            Id = id,
            Name = name,
            IconText = icon,
            TypeText = type,
            SourceText = source,
            DurationText = duration,
            Description = description,
            Kind = kind,
            Stackable = stackable,
            StackCount = stackCount
        };
    }

    private static string GetRunBuffIcon(string buffId, string buffName)
    {
        return buffId switch
        {
            RunBuffIds.Curse => "咒",
            RunBuffIds.Darkness => "黑",
            RunBuffIds.BloodMoonDarkness => "月",
            RunBuffIds.Wet => "湿",
            RunBuffIds.MoonAttention => "月",
            RunBuffIds.ShaQiChenShen => "煞",
            RunBuffIds.QiXingTanBattle => "魂",
            _ => buffName.Length <= 1 ? buffName : buffName[..1]
        };
    }

    private static string GetRunBuffTypeText(RunBuffType type)
    {
        return type switch
        {
            RunBuffType.Blessing => L("runbuff.type.blessing"),
            RunBuffType.Curse => L("runbuff.type.curse"),
            RunBuffType.ChapterVariant => L("runbuff.type.chapter"),
            RunBuffType.RouteVariant => L("runbuff.type.route"),
            RunBuffType.Event => L("runbuff.type.event"),
            RunBuffType.BossEffect => L("runbuff.type.boss"),
            _ => L("runbuff.type.other")
        };
    }

    private static string BuildEquipmentDescription(EquipmentDefinition equipment)
    {
        return equipment.Effects.Count == 0
            ? Localization.GetDescription(equipment)
            : Localization.GetEquipmentEffectDescriptions(equipment, "\n");
    }

    private static string GetEquipmentRarityName(EquipmentRarity rarity)
    {
        return rarity switch
        {
            EquipmentRarity.Common => L("equip.rarity.common"),
            EquipmentRarity.Rare => L("equip.rarity.rare"),
            EquipmentRarity.Epic => L("equip.rarity.epic"),
            EquipmentRarity.Legendary => L("equip.rarity.legendary"),
            _ => L("equip.rarity.unknown")
        };
    }

    private static string GetEquipmentTypeText(IReadOnlyList<EquipmentType> types)
    {
        var names = new List<string>();
        foreach (var type in types)
        {
            names.Add(type switch
            {
                EquipmentType.Buff => L("equip.type.buff"),
                EquipmentType.Weapon => L("equip.type.weapon"),
                EquipmentType.Armor => L("equip.type.armor"),
                EquipmentType.Vehicle => L("equip.type.vehicle"),
                EquipmentType.Defense => L("equip.type.defense"),
                EquipmentType.Attack => L("equip.type.attack"),
                EquipmentType.Accessory => L("equip.type.accessory"),
                EquipmentType.Mount => L("equip.type.mount"),
                _ => type.ToString()
            });
        }

        return string.Join(" / ", names);
    }

    // ── Tooltip builders (shared by CharacterStatusCard and EnemyHover) ──────

    /// <summary>
    /// Core System 的公开入口：BuildStatusTooltip。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string BuildStatusTooltip(BattleUnitStatusEntry entry)
    {
        var stackText = entry.Stackable ? Localization.GetFmt("status.tooltip.stacks_fmt", entry.StackCount) : string.Empty;
        var durationText = string.IsNullOrWhiteSpace(entry.DurationText) ? string.Empty : Localization.GetFmt("status.tooltip.duration_fmt", entry.DurationText);
        var sourceText = string.IsNullOrWhiteSpace(entry.SourceText) ? string.Empty : Localization.GetFmt("status.tooltip.source_fmt", entry.SourceText);
        return Localization.GetFmt("status.tooltip.body_fmt", entry.Name, entry.TypeText, sourceText, entry.Description, stackText, durationText);
    }

    /// <summary>
    /// Core System 的公开入口：BuildOverflowTooltip。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string BuildOverflowTooltip(
        List<BattleUnitStatusEntry> statuses,
        List<(string Name, string Rarity, string Type, string Description)> equipments,
        int statShown, int eqShown, int overflowCount)
    {
        var lines = Localization.GetFmt("status.tooltip.overflow_fmt", overflowCount);
        foreach (var entry in statuses.GetRange(statShown, statuses.Count - statShown))
            lines += Localization.GetFmt("ui.label_value_fmt", $"{entry.IconText} {entry.Name}", entry.Description) + "\n";
        foreach (var eq in equipments.GetRange(eqShown, equipments.Count - eqShown))
            lines += Localization.GetFmt("ui.label_value_fmt", $"[{eq.Rarity}]{eq.Name}", eq.Description) + "\n";
        return lines.TrimEnd();
    }
}
