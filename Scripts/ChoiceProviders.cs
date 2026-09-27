//////////////////////////////////////////////////////////
// 文件：Scripts/ChoiceProviders.cs
//
// 模块：Choice System
//
// 职责：
// 1. 承载统一选择界面与候选项生成相关代码。
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

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Choice System 的公开类：SkillChoiceProvider。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class SkillChoiceProvider : IChoiceProvider
{
    public int Count { get; init; } = 3;
    public bool Randomize { get; init; } = true;
    public bool ExcludeOwnedSkills { get; init; } = true;
    public IReadOnlyCollection<SkillRarity>? Rarities { get; init; }
    public IReadOnlyCollection<string>? SkillPool { get; init; }
    public IReadOnlyCollection<string>? ExcludedSkillIds { get; init; }
    public bool IncludeBossSkills { get; init; }
    // 随机技能奖励默认包含角色专属技能。专属仅代表技能原本所属角色，
    // 不代表其它角色不能通过奖励习得；Boss 专属技能仍由 IncludeBossSkills 单独控制。
    public bool IncludeOtherCharacterExclusiveSkills { get; init; } = true;

    /// <summary>
    /// Choice System 的公开入口：CreateChoices。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public IReadOnlyList<ChoiceOption> CreateChoices()
    {
        var candidates = new List<Skill>();
        foreach (var skill in SkillDatabase.All())
        {
            if (!skill.CanAppearInRandomChoicePool) continue;
            if (SkillPool != null && !SkillPool.Contains(skill.Id)) continue;
            if (ExcludedSkillIds != null && ExcludedSkillIds.Contains(skill.Id)) continue;
            if (Rarities != null && !Rarities.Contains(skill.Rarity)) continue;
            if (!IncludeBossSkills && skill.Source == SkillSource.Boss) continue;
            if (!IncludeOtherCharacterExclusiveSkills
                && skill.Category == SkillCategory.CharacterExclusive
                && skill.CharacterId != GameManager.CurrentCharacterId)
            {
                continue;
            }
            if (ExcludeOwnedSkills
                && (GameManager.CurrentCharacterHasSkill(skill.Id) || GameManager.HasAcquiredSkill(skill.Id)))
            {
                continue;
            }

            candidates.Add(skill);
        }

        var selected = Select(candidates, Count, Randomize);
        return selected
            .Select(skill => new ChoiceOption(
                ChoiceKind.Skill,
                skill.Id,
                Localization.GetName(skill),
                Localization.GetDescription(skill),
                $"{FormatSkillRarity(skill.Rarity)} · {FormatSkillKinds(skill.Kinds)}",
                skill.IconText,
                skill))
            .ToList();
    }

    private static string FormatSkillRarity(SkillRarity rarity)
        => Localization.GetRarityName(rarity);

    private static string FormatSkillKinds(IReadOnlyList<SkillKind> kinds)
    {
        var names = new List<string>();
        foreach (var kind in kinds)
        {
            names.Add(Localization.GetSkillKindName(kind));
        }

        return string.Join(" / ", names);
    }

    internal static List<T> Select<T>(List<T> candidates, int count, bool randomize)
    {
        if (count <= 0 || candidates.Count == 0)
        {
            return new List<T>();
        }

        var result = new List<T>();
        if (!randomize)
        {
            result.AddRange(candidates.Take(count));
            return result;
        }

        while (result.Count < count && candidates.Count > 0)
        {
            // 奖励选择统一使用本局随机源，避免 Random.Shared 与存档/事件随机流脱节。
            var index = GameManager.EventRewardRandom.Next(candidates.Count);
            result.Add(candidates[index]);
            candidates.RemoveAt(index);
        }

        return result;
    }
}

/// <summary>
/// Choice System 的公开类：EquipmentChoiceProvider。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class EquipmentChoiceProvider : IChoiceProvider
{
    public int Count { get; init; } = 3;
    public bool Randomize { get; init; } = true;
    public IReadOnlyCollection<EquipmentRarity>? Rarities { get; init; }
    public IReadOnlyCollection<EquipmentSlotCategory>? SlotCategories { get; init; }
    public IReadOnlyCollection<string>? EquipmentPool { get; init; }
    public IReadOnlyCollection<string>? ExcludedEquipmentIds { get; init; }
    public bool IncludeUnknownOrDebug { get; init; }
    public bool IncludeNonRandomPool { get; init; }

    /// <summary>
    /// Choice System 的公开入口：CreateChoices。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public IReadOnlyList<ChoiceOption> CreateChoices()
    {
        var candidates = new List<EquipmentDefinition>();
        foreach (var definition in EquipmentDatabase.GetAllEquipments())
        {
            if (EquipmentPool != null && !EquipmentPool.Contains(definition.Id)) continue;
            if (ExcludedEquipmentIds != null && ExcludedEquipmentIds.Contains(definition.Id)) continue;
            if (Rarities != null && !Rarities.Contains(definition.Rarity)) continue;
            if (!IncludeNonRandomPool && !RewardManager.CanAppearInRandomEquipmentReward(definition)) continue;
            // Unknown 是历史装备数据的正常来源，随机奖励应与重铸池一致地保留它；
            // 仅开发调试物品在常规选择界面中隐藏。
            if (!IncludeUnknownOrDebug
                && definition.AcquisitionMethod == EquipmentAcquisitionMethod.Debug)
            {
                continue;
            }

            var category = InventoryManager.GetSlotCategory(definition);
            if (SlotCategories != null && (category == null || !SlotCategories.Contains(category.Value))) continue;
            candidates.Add(definition);
        }

        var selected = SkillChoiceProvider.Select(candidates, Count, Randomize);
        return selected.Select(equipment => ToChoiceOption(equipment)).ToList();
    }

    /// <summary>
    /// Choice System 的公开入口：ToChoiceOption。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static ChoiceOption ToChoiceOption(EquipmentDefinition equipment, object? payload = null, ChoiceKind kind = ChoiceKind.Equipment)
        => new(
            kind,
            equipment.Id,
            Localization.GetName(equipment),
            Localization.GetDescription(equipment),
            $"{FormatRarity(equipment.Rarity)} · {FormatEquipmentTypes(equipment.Types)}",
            GetEquipmentIcon(equipment),
            payload ?? equipment);

    /// <summary>
    /// Choice System 的公开入口：FormatRarity。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string FormatRarity(EquipmentRarity rarity)
        => Localization.GetRarityName(rarity);

    private static string FormatEquipmentTypes(IReadOnlyList<EquipmentType> types)
    {
        var names = new List<string>();
        foreach (var type in types)
        {
            names.Add(Localization.GetEquipmentTypeName(type));
        }

        return string.Join(" / ", names);
    }

    private static string GetEquipmentIcon(EquipmentDefinition equipment)
    {
        var category = InventoryManager.GetSlotCategory(equipment);
        return category switch
        {
            EquipmentSlotCategory.Weapon => Localization.Get("equip.slot.icon.weapon"),
            EquipmentSlotCategory.Armor => Localization.Get("equip.slot.icon.armor"),
            EquipmentSlotCategory.Vehicle => Localization.Get("equip.slot.icon.vehicle"),
            EquipmentSlotCategory.Accessory => Localization.Get("equip.slot.icon.accessory"),
            _ => Localization.Get("equip.slot.icon.default")
        };
    }
}

/// <summary>
/// Choice System 的公开类：InventoryChoiceProvider。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class InventoryChoiceProvider : IChoiceProvider
{
    public InventoryChoiceScope Scope { get; init; } = InventoryChoiceScope.All;
    public IReadOnlyCollection<EquipmentSlotCategory>? SlotCategories { get; init; }
    public IReadOnlyCollection<string>? ExcludedEquipmentIds { get; init; }
    public int Count { get; init; } = int.MaxValue;
    public bool Randomize { get; init; }
    public bool RequireReforgeCandidate { get; init; }
    public bool RequireUpgradeCandidate { get; init; }

    /// <summary>
    /// Choice System 的公开入口：CreateChoices。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public IReadOnlyList<ChoiceOption> CreateChoices()
    {
        var result = new List<ChoiceOption>();
        foreach (var item in InventoryManager.GetAllOwned())
        {
            if (Scope == InventoryChoiceScope.Equipped && item.EquippedSlot == null) continue;
            if (Scope == InventoryChoiceScope.Backpack && item.EquippedSlot != null) continue;
            if (ExcludedEquipmentIds != null && ExcludedEquipmentIds.Contains(item.Definition.Id)) continue;

            var category = InventoryManager.GetSlotCategory(item.Definition);
            if (SlotCategories != null && (category == null || !SlotCategories.Contains(category.Value))) continue;
            if (RequireReforgeCandidate && InventoryManager.GetReforgeCandidates(item.Definition).Count == 0) continue;
            if (RequireUpgradeCandidate && RewardManager.GetEquipmentUpgradeCandidates(item.Definition).Count == 0) continue;

            var option = EquipmentChoiceProvider.ToChoiceOption(item.Definition, item, ChoiceKind.InventoryEquipment);
            var location = item.EquippedSlot == null ? Localization.Get("ui.inventory") : string.Format(Localization.Get("ui.equipped_fmt"), FormatSlot(item.EquippedSlot.Value));
            result.Add(new ChoiceOption(
                ChoiceKind.InventoryEquipment,
                item.InstanceId.ToString(),
                option.Title,
                option.Description,
                $"{option.Subtitle} · {location}",
                option.IconText,
                item));
        }

        return SkillChoiceProvider.Select(result, Count, Randomize);
    }

    private static string FormatSlot(EquipmentSlot slot)
        => slot switch
        {
            EquipmentSlot.Weapon => Localization.Get("equip.slot.weapon"),
            EquipmentSlot.Armor => Localization.Get("equip.slot.armor"),
            EquipmentSlot.Vehicle => Localization.Get("equip.slot.vehicle"),
            EquipmentSlot.Accessory1 => Localization.Get("equip.slot.accessory1"),
            EquipmentSlot.Accessory2 => Localization.Get("equip.slot.accessory2"),
            EquipmentSlot.Accessory3 => Localization.Get("equip.slot.accessory3"),
            EquipmentSlot.Accessory4 => Localization.Get("equip.slot.accessory4"),
            EquipmentSlot.Accessory5 => Localization.Get("equip.slot.accessory5"),
            EquipmentSlot.Universal => Localization.Get("equip.slot.universal"),
            _ => slot.ToString()
        };
}

/// <summary>
/// Choice System 的公开类：ChipChoiceProvider。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ChipChoiceProvider : IChoiceProvider
{
    public const double ExpansionReplacementChanceDefault = 0.01;
    public const double SkillChipReplacementChanceDefault = 0.01;
    private const int StandardChipChoiceCount = 3;

    public int Count { get; init; } = 3;
    public IReadOnlyList<ChipChoiceType> ChipTypes { get; init; } = new[] { ChipChoiceType.Random };
    // 供需要排除特定芯片类型的选择流程使用；标准三选一直接传入三种固定类型。
    public IReadOnlyCollection<ChipChoiceType>? ExcludedChipTypes { get; init; }
    // 标准芯片三选一有1%概率将其中随机一项替换为扩容芯片。
    // 此委托仅用于无界面回归测试；正常游戏始终使用 Random.Shared。
    public double ExpansionReplacementChance { get; init; } = ExpansionReplacementChanceDefault;
    // 标准芯片三选一另有1%概率将一个基础芯片替换为【技能芯片】。
    public double SkillChipReplacementChance { get; init; } = SkillChipReplacementChanceDefault;
    // 群·芯片重构会在本局开始时固定一种基础芯片目标。保留三个位置是为了让扩容/技能
    // 芯片仍能按各自概率替换其中一个位置；未被替换的位置会显示为同一种芯片。
    public ChipChoiceType? ForcedBaseChipType { get; init; }
    // 初始事件④会启用【神秘芯片】的候选替换；默认100%仅用于兼容固定候选调用方，
    // 正式初始事件会明确传入5%。
    public bool IncludeMysteriousChip { get; init; }
    public double MysteriousChipReplacementChance { get; init; } = 1.0;
    public Func<double>? RandomDoubleProvider { get; init; }

    /// <summary>
    /// Choice System 的公开入口：CreateChoices。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public IReadOnlyList<ChoiceOption> CreateChoices()
    {
        var choices = new List<ChoiceOption>();

        if (ForcedBaseChipType is ChipChoiceType forced
            && forced is ChipChoiceType.Attack or ChipChoiceType.Defense or ChipChoiceType.Skill)
        {
            for (var index = 0; index < Count; index++)
            {
                choices.Add(ToOption(forced));
            }

            ApplyMysteriousChipReplacement(choices);
            ApplyExpansionReplacementIfRolled(choices);
            ApplySkillChipReplacementIfRolled(choices);
            return choices;
        }

        // 明确指定类型代表固定候选池：先严格按调用方给出的顺序生成且不重复，
        // 再由统一的1%扩容芯片规则随机替换其中一项。
        if (ChipTypes.Count > 0 && !ChipTypes.Contains(ChipChoiceType.Random))
        {
            foreach (var type in ChipTypes)
            {
                if (choices.Count >= Count)
                {
                    break;
                }

                if (ExcludedChipTypes != null && ExcludedChipTypes.Contains(type))
                {
                    continue;
                }

                if (choices.Any(option => option.Payload is ChipChoiceType existing && existing == type))
                {
                    continue;
                }

                choices.Add(ToOption(type));
            }

            ApplyMysteriousChipReplacement(choices);
            ApplyExpansionReplacementIfRolled(choices);
            ApplySkillChipReplacementIfRolled(choices);
            return choices;
        }

        // 排除项最多只有3种芯片类型（攻击/防御/知识），给一个远大于候选数的重试上限，
        // 避免排除项覆盖了全部类型时死循环。
        var guard = 0;
        while (choices.Count < Count && guard < 200)
        {
            guard++;
            var type = ResolveType(ChipTypes.Count == 0 ? ChipChoiceType.Random : ChipTypes[Random.Shared.Next(ChipTypes.Count)]);
            if (ExcludedChipTypes != null && ExcludedChipTypes.Contains(type)) continue;
            choices.Add(ToOption(type));
        }

        ApplyMysteriousChipReplacement(choices);
        ApplyExpansionReplacementIfRolled(choices);
        ApplySkillChipReplacementIfRolled(choices);
        return choices;
    }

    private void ApplyMysteriousChipReplacement(List<ChoiceOption> choices)
    {
        if (!IncludeMysteriousChip
            || Count != StandardChipChoiceCount
            || choices.Count != StandardChipChoiceCount
            || MysteriousChipReplacementChance <= 0
            || (ExcludedChipTypes?.Contains(ChipChoiceType.Mysterious) ?? false)
            || choices.Any(option => option.Payload is ChipChoiceType.Mysterious))
        {
            return;
        }

        var roll = RandomDoubleProvider?.Invoke() ?? Random.Shared.NextDouble();
        if (roll >= MysteriousChipReplacementChance)
        {
            return;
        }

        choices[Random.Shared.Next(choices.Count)] = ToOption(ChipChoiceType.Mysterious);
    }

    private void ApplyExpansionReplacementIfRolled(List<ChoiceOption> choices)
    {
        if (Count != StandardChipChoiceCount
            || choices.Count != StandardChipChoiceCount
            || ExpansionReplacementChance <= 0
            || (ExcludedChipTypes?.Contains(ChipChoiceType.Expansion) ?? false)
            || choices.Any(option => option.Payload is ChipChoiceType.Expansion))
        {
            return;
        }

        var roll = RandomDoubleProvider?.Invoke() ?? Random.Shared.NextDouble();
        if (roll >= ExpansionReplacementChance)
        {
            return;
        }

        var replacementIndexes = choices
            .Select((option, index) => (option, index))
            .Where(pair => pair.option.Payload is not ChipChoiceType.Mysterious)
            .Select(pair => pair.index)
            .ToList();
        if (replacementIndexes.Count == 0) return;
        var replacementIndex = replacementIndexes[Random.Shared.Next(replacementIndexes.Count)];
        choices[replacementIndex] = ToOption(ChipChoiceType.Expansion);
    }

    private void ApplySkillChipReplacementIfRolled(List<ChoiceOption> choices)
    {
        if (Count != StandardChipChoiceCount
            || choices.Count != StandardChipChoiceCount
            || SkillChipReplacementChance <= 0
            || (ExcludedChipTypes?.Contains(ChipChoiceType.Skill) ?? false)
            || choices.Any(option => option.Payload is ChipChoiceType.Skill))
        {
            return;
        }

        var roll = RandomDoubleProvider?.Invoke() ?? Random.Shared.NextDouble();
        if (roll >= SkillChipReplacementChance)
        {
            return;
        }

        // 扩容/神秘芯片是自身流程的特殊候选；技能芯片只替换基础芯片，保证特殊奖励不被覆盖。
        var replacementIndexes = choices
            .Select((option, index) => (option, index))
            .Where(pair => pair.option.Payload is ChipChoiceType.Attack
                or ChipChoiceType.Defense
                or ChipChoiceType.Knowledge)
            .Select(pair => pair.index)
            .ToList();
        if (replacementIndexes.Count == 0) return;
        var replacementIndex = replacementIndexes[Random.Shared.Next(replacementIndexes.Count)];
        choices[replacementIndex] = ToOption(ChipChoiceType.Skill);
    }

    private static ChipChoiceType ResolveType(ChipChoiceType type)
        => type == ChipChoiceType.Random
            ? (ChipChoiceType)Random.Shared.Next(0, 3)
            : type;

    private static ChoiceOption ToOption(ChipChoiceType type)
    {
        var (id, titleKey, descKey, iconKey) = type switch
        {
            ChipChoiceType.Attack => ("attack", "chip.attack.name", "chip.attack.desc", "chip.attack.icon"),
            ChipChoiceType.Defense => ("defense", "chip.defense.name", "chip.defense.desc", "chip.defense.icon"),
            ChipChoiceType.Knowledge => ("knowledge", "chip.knowledge.name", "chip.knowledge.desc", "chip.knowledge.icon"),
            ChipChoiceType.Expansion => ("expansion", "chip.expansion.name", "chip.expansion.desc", "chip.expansion.icon"),
            ChipChoiceType.Mysterious => ("mysterious", "chip.mysterious.name", "chip.mysterious.desc", "chip.mysterious.icon"),
            ChipChoiceType.Skill => ("skill", "chip.skill.name", "chip.skill.desc", "chip.skill.icon"),
            _ => ("random", "chip.random.name", "chip.random.desc", "?")
        };

        var icon = iconKey == "?" ? "?" : Localization.Get(iconKey);
        return new ChoiceOption(ChoiceKind.Chip, id, Localization.Get(titleKey), Localization.Get(descKey), Localization.Get("chip.subtitle"), icon, type);
    }
}

/// <summary>
/// Choice System 的公开类：ElementChoiceProvider。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ElementChoiceProvider : IChoiceProvider
{
    /// <summary>
    /// Choice System 的公开入口：CreateChoices。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public IReadOnlyList<ChoiceOption> CreateChoices()
    {
        var subtitle = Localization.Get("element.subtitle");
        return new[]
        {
            new ChoiceOption(ChoiceKind.Element, "fire", Localization.Get("element.fire.name"), Localization.Get("element.fire.desc"), subtitle, Localization.Get("element.fire.icon"), RunBuffIds.ElfFireDamage),
            new ChoiceOption(ChoiceKind.Element, "thunder", Localization.Get("element.thunder.name"), Localization.Get("element.thunder.desc"), subtitle, Localization.Get("element.thunder.icon"), RunBuffIds.ElfThunderDamage),
            new ChoiceOption(ChoiceKind.Element, "ice", Localization.Get("element.ice.name"), Localization.Get("element.ice.desc"), subtitle, Localization.Get("element.ice.icon"), RunBuffIds.ElfIceDamage),
            new ChoiceOption(ChoiceKind.Element, "poison", Localization.Get("element.poison.name"), Localization.Get("element.poison.desc"), subtitle, Localization.Get("element.poison.icon"), RunBuffIds.ElfPoisonDamage)
        };
    }
}

/// <summary>
/// Choice System 的公开类：BuffChoiceProvider。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class BuffChoiceProvider : IChoiceProvider
{
    public IReadOnlyList<string> RunBuffPool { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Choice System 的公开入口：CreateChoices。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public IReadOnlyList<ChoiceOption> CreateChoices()
    {
        var result = new List<ChoiceOption>();
        foreach (var buffId in RunBuffPool)
        {
            var definition = RunBuffDatabase.Get(buffId);
            if (definition == null) continue;
            result.Add(new ChoiceOption(
                ChoiceKind.Buff,
                definition.BuffId,
                Localization.GetName(definition),
                Localization.GetDescription(definition),
                "RunBuff",
                string.IsNullOrEmpty(definition.Icon) ? "Buff" : definition.Icon,
                definition));
        }

        return result;
    }
}

/// <summary>
/// Choice System 的公开类：CardChoiceProvider。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class CardChoiceProvider : IChoiceProvider
{
    public int Count { get; init; } = 3;
    public bool Randomize { get; init; }
    public bool UseCurrentPlayerDeck { get; init; }
    public bool IncludeCharacterExclusive { get; init; }
    public IReadOnlyCollection<CardType>? CardPool { get; init; }
    public IReadOnlyCollection<CardType>? ExcludedCardTypes { get; init; }

    /// <summary>
    /// Choice System 的公开入口：CreateChoices。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public IReadOnlyList<ChoiceOption> CreateChoices()
    {
        var candidates = new List<CardType>();
        var source = CardPool ?? Enum.GetValues<CardType>();
        foreach (var cardType in source)
        {
            // 角色专属牌由角色技能注入行动栏，不能通过随机卡池复制给其它角色。
            if (!IncludeCharacterExclusive && cardType is CardType.Tuxi or CardType.FireAttack) continue;
            if (ExcludedCardTypes != null && ExcludedCardTypes.Contains(cardType)) continue;
            if (UseCurrentPlayerDeck && !GameManager.HasPlayerCardType(cardType)) continue;
            candidates.Add(cardType);
        }

        var selected = SkillChoiceProvider.Select(candidates, Count, Randomize);
        return selected.Select(type =>
        {
            var card = new Card(type);
            var cardName = Localization.GetName(card);
            return new ChoiceOption(
                ChoiceKind.Card,
                type.ToString(),
                cardName,
                card.Description,
                string.Format(Localization.Get("card.cost_fmt"), card.Cost),
                cardName.Length <= 2 ? cardName : cardName[..2],
                type);
        }).ToList();
    }
}
