//////////////////////////////////////////////////////////
// 文件：Scripts/InventoryManager.cs
//
// 模块：Inventory System
//
// 职责：
// 1. 承载背包、装备槽位与装备实例管理相关代码。
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

/// <summary>
/// Inventory System 的公开枚举：EquipmentSlot。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum EquipmentSlot
{
    Weapon,
    Armor,
    Vehicle,
    Accessory1,
    Accessory2,
    Accessory3,
    Accessory4, // 吴·多宝架 命中时的第4个饰品槽；渲染层按命运是否命中过滤，是否显示见 InventoryController.SlotOrder 的消费点
    Accessory5, // 爆炸果实事件"碰一下"永久获得的"普通饰品槽"；仅允许普通品质饰品，与 Accessory4 相互独立
    Universal   // 万能槽，由扩容芯片解锁；可装备武器/护甲/饰品任意类型
}

/// <summary>
/// Inventory System 的公开枚举：EquipmentSlotCategory。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum EquipmentSlotCategory
{
    Weapon,
    Armor,
    Vehicle,
    Accessory
}

/// <summary>
/// Inventory System 的公开类：OwnedEquipment。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class OwnedEquipment
{
    /// <summary>
    /// Inventory System 的公开入口：OwnedEquipment。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public OwnedEquipment(EquipmentDefinition definition)
    {
        Definition = definition;
    }

    public Guid InstanceId { get; } = Guid.NewGuid();
    public EquipmentDefinition Definition { get; }
    public EquipmentSlot? EquippedSlot { get; set; }

    /// <summary>
    /// 吴·工坊重选：标记这件装备的来源。"FactionFateReselect" 表示它是重选流程自己发放的，
    /// 配合 <see cref="InventoryManager.AddToInventory"/> 的 skipFactionFateReselect 参数
    /// 防止重选结果本身又递归触发一次重选。其余情况留空，不需要额外赋值。
    /// </summary>
    public string? AcquiredFrom { get; set; }
}

// 背包与装备槽数据管理。装备只有放入装备槽（Equip）才会生效，背包内的装备不生效。
// 战斗系统（BattleRules/BattleManager）继续通过 GameManager.HasEquipment / CountEquipment 等既有接口读取数据，
// GameManager 内部已改为只统计这里的“已装备”条目，因此战斗系统代码本身无需任何改动。
/// <summary>
/// Inventory System 的公开类：InventoryManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class InventoryManager
{
    private static readonly List<OwnedEquipment> Owned = new();
    // 【重铸器】出售后只保留到下一次有效出售的单次机会；这是 Run 内背包状态，
    // 因而和装备槽、商店 UI 解耦，并在 Reset() 时统一清除。
    private static bool _reforgerSaleArmed;

    public static bool IsReforgerSaleArmed => _reforgerSaleArmed;
    public static bool LastSaleUsedReforger { get; private set; }
    // 重铸器属于“出售后自动替换”的路径，SellItem 的整型返回值无法携带替换结果。
    // 保留本次成功重铸的前后装备，供背包 UI 在刷新后展示准确结果；下一次出售开始时会清空。
    public static EquipmentDefinition? LastSaleReforgeOriginal { get; private set; }
    public static EquipmentDefinition? LastSaleReforgeReplacement { get; private set; }

    /// <summary>
    /// Inventory System 的公开入口：GetAllOwned。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static IReadOnlyList<OwnedEquipment> GetAllOwned()
    {
        return Owned;
    }

    /// <summary>
    /// 判断一件已拥有的装备当前是否"生效"：
    /// <see cref="EquipmentActivationType.Inventory"/>（背包生效）的装备只要拥有就生效；
    /// 其余（默认 <see cref="EquipmentActivationType.EquippedOnly"/>）必须装备到装备槽
    /// （<see cref="OwnedEquipment.EquippedSlot"/> 非空）才生效。
    ///
    /// 统一入口：任何需要判断"这件装备算不算生效"的效果（黄金雕像、以后新增的背包生效
    /// 装备）都应该调用这个方法，不要各自重复"是不是背包生效装备"的判断逻辑。
    /// </summary>
    public static bool IsActive(OwnedEquipment item)
    {
        return item.Definition.ActivationType == EquipmentActivationType.Inventory || item.EquippedSlot.HasValue;
    }

    /// <summary>
    /// Inventory System 的公开入口：GetBackpackItems。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static List<OwnedEquipment> GetBackpackItems()
    {
        var result = new List<OwnedEquipment>();
        foreach (var item in Owned)
        {
            if (item.EquippedSlot == null)
            {
                result.Add(item);
            }
        }

        return result;
    }

    /// <summary>
    /// Inventory System 的公开入口：GetSlotItem。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static OwnedEquipment? GetSlotItem(EquipmentSlot slot)
    {
        foreach (var item in Owned)
        {
            if (item.EquippedSlot == slot)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// Inventory System 的公开入口：FindOwned。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static OwnedEquipment? FindOwned(Guid instanceId)
    {
        foreach (var item in Owned)
        {
            if (item.InstanceId == instanceId)
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// Inventory System 的公开入口：AddToInventory。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static OwnedEquipment? AddToInventory(
        string equipmentId,
        EquipmentGainSource source = EquipmentGainSource.GameplayReward,
        bool skipFactionFateReselect = false)
    {
        var definition = EquipmentDatabase.GetEquipment(equipmentId);
        if (definition == null)
        {
            return null;
        }

        // 芯片类道具：立即自动应用效果，不进入背包。
        if (equipmentId == EquipmentIds.AttackChip)
        {
            GameManager.IncrementAttackChipCount();
            return null;
        }

        if (equipmentId == EquipmentIds.ExpansionChip)
        {
            GameManager.IncrementExpansionChipCount();
            return null;
        }

        if (equipmentId == EquipmentIds.SkillChip)
        {
            GameManager.GrantRandomSkillFromSkillChip();
            return null;
        }

        var item = new OwnedEquipment(definition)
        {
            AcquiredFrom = source.ToString()
        };
        Owned.Add(item);

        // 图鉴：只有真正进入玩家拥有状态才算"获得"——商店刷新看到、事件预览、图鉴查看、
        // 掉落生成但未领取都不会走到这里。调试面板（Developer）和读档恢复（SaveRestore）
        // 显式排除，避免污染正式统计。
        if (source != EquipmentGainSource.Developer && source != EquipmentGainSource.SaveRestore)
        {
            CodexService.RecordEquipmentObtained(equipmentId);
        }

        // 初始事件②：自动销毁前5件装备
        if (GameManager.InitialEventAutoDestroyEquip)
        {
            Owned.Remove(item);
            GameManager.IncrementInitialEventAutoDestroyCount();
            return null;
        }

        // 吴·工坊重选：新获得的装备若满足条件，自动销毁并弹出3选1同稀有度替换。
        // skipFactionFateReselect=true（重选流程自己调用 AddToInventory 时传入）或
        // item.AcquiredFrom == "FactionFateReselect"（防御性双保险）都会跳过，避免递归触发。
        if (!skipFactionFateReselect
            && source != EquipmentGainSource.FactionFateReplacement
            && FactionFateManager.CurrentFateId == FactionFateIds.WuEquipmentReselect
            && item.AcquiredFrom != "FactionFateReselect")
        {
            FactionFateManager.TryTriggerEquipmentReselect(item);
        }

        return item;
    }

    // 拖到对应槽位即视为装备；若槽位已有装备，旧装备自动返回背包，不弹确认框。
    // 特例：若槽位旧装备是扩容芯片且万能槽有内容，则拒绝操作（须先卸下万能槽装备）。
    /// <summary>
    /// Inventory System 的公开入口：EquipToSlot。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool EquipToSlot(Guid instanceId, EquipmentSlot slot)
    {
        var item = FindOwned(instanceId);
        if (item == null || !CanEquipToSlot(item.Definition, slot))
        {
            return false;
        }

        var existing = GetSlotItem(slot);
        if (existing != null && existing != item)
        {
            return SwapEquipment(instanceId, existing.InstanceId);
        }

        var previousSlot = item.EquippedSlot;
        if (!KeepsUniversalSlotValid(item, slot, null, null))
        {
            return false;
        }

        item.EquippedSlot = slot;
        if (previousSlot == null)
        {
            CodexService.RecordEquipmentEquipped(item.Definition.Id);
        }
        if (previousSlot == null && item.Definition.Id == EquipmentIds.CurseBlade)
        {
            GameManager.OnCurseBladeEquipped();
        }
        return true;
    }

    /// <summary>
    /// 判断两个装备实例能否直接交换所在位置，但不修改任何状态。
    ///
    /// 位置由 <see cref="OwnedEquipment.EquippedSlot"/> 表示，null 代表背包。
    /// 因此同一套规则同时覆盖“背包与装备槽”和“两个装备槽”之间的交换。
    /// </summary>
    public static bool CanSwapEquipment(Guid firstInstanceId, Guid secondInstanceId)
    {
        var first = FindOwned(firstInstanceId);
        var second = FindOwned(secondInstanceId);
        if (first == null || second == null || first == second)
        {
            return false;
        }

        var firstSlot = first.EquippedSlot;
        var secondSlot = second.EquippedSlot;
        if (firstSlot == secondSlot)
        {
            return false;
        }

        if (secondSlot.HasValue && !CanEquipToSlot(first.Definition, secondSlot.Value))
        {
            return false;
        }

        if (firstSlot.HasValue && !CanEquipToSlot(second.Definition, firstSlot.Value))
        {
            return false;
        }

        return KeepsUniversalSlotValid(first, secondSlot, second, firstSlot);
    }

    /// <summary>
    /// 原子交换两个已拥有装备的位置。
    ///
    /// 所有类型、品质和特殊槽位检查均在写入前完成；验证失败时不会修改任一实例。
    /// 交换只改变现有实例的槽位引用，不删除、复制或重新生成装备，因此不受背包容量影响。
    /// </summary>
    public static bool SwapEquipment(Guid firstInstanceId, Guid secondInstanceId)
    {
        if (!CanSwapEquipment(firstInstanceId, secondInstanceId))
        {
            return false;
        }

        var first = FindOwned(firstInstanceId)!;
        var second = FindOwned(secondInstanceId)!;
        var firstSlot = first.EquippedSlot;
        var secondSlot = second.EquippedSlot;

        first.EquippedSlot = secondSlot;
        second.EquippedSlot = firstSlot;

        NotifyCurseBladeEquipped(first, firstSlot);
        NotifyCurseBladeEquipped(second, secondSlot);
        MainFlow.TryShowFirstTimeHint(FirstTimeHintManager.HintIds.FirstEquipmentSwap);
        return true;
    }

    private static void NotifyCurseBladeEquipped(OwnedEquipment item, EquipmentSlot? previousSlot)
    {
        if (previousSlot == null
            && item.EquippedSlot.HasValue
            && item.Definition.Id == EquipmentIds.CurseBlade)
        {
            GameManager.OnCurseBladeEquipped();
        }
    }

    // 扩容芯片是万能槽的结构依赖。交换必须基于“提交后的最终状态”判断，
    // 不能先卸下再检查，否则满背包或交换失败时会留下半完成状态。
    private static bool KeepsUniversalSlotValid(
        OwnedEquipment first,
        EquipmentSlot? firstNewSlot,
        OwnedEquipment? second,
        EquipmentSlot? secondNewSlot)
    {
        if (GetSlotItem(EquipmentSlot.Universal) == null || GameManager.ExpansionChipCount > 0)
        {
            return true;
        }

        var equippedExpansionChips = CountEquipped(EquipmentIds.ExpansionChip);
        if (first.Definition.Id == EquipmentIds.ExpansionChip)
        {
            equippedExpansionChips += (firstNewSlot.HasValue ? 1 : 0) - (first.EquippedSlot.HasValue ? 1 : 0);
        }

        if (second?.Definition.Id == EquipmentIds.ExpansionChip)
        {
            equippedExpansionChips += (secondNewSlot.HasValue ? 1 : 0) - (second.EquippedSlot.HasValue ? 1 : 0);
        }

        return equippedExpansionChips > 0;
    }

    // 已装备装备可直接拖回背包。
    // 特例：扩容芯片在万能槽有内容时不允许卸下（须先卸下万能槽装备）。
    /// <summary>
    /// Inventory System 的公开入口：UnequipToBackpack。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool UnequipToBackpack(Guid instanceId)
    {
        var item = FindOwned(instanceId);
        if (item == null)
        {
            return false;
        }

        if (item.Definition.Id == EquipmentIds.ExpansionChip && item.EquippedSlot != null)
        {
            if (GetSlotItem(EquipmentSlot.Universal) != null)
            {
                return false;
            }
        }

        item.EquippedSlot = null;
        return true;
    }

    // 已装备装备可直接拖到出售区出售，出售前自动卸下；出售后装备彻底删除。
    // 返回 -1 表示被阻止（扩容芯片万能槽有内容时不允许出售）；返回 0 表示未找到；返回 >0 表示出售价格。
    /// <summary>
    /// Inventory System 的公开入口：SellItem。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int SellItem(Guid instanceId)
    {
        LastSaleUsedReforger = false;
        LastSaleReforgeOriginal = null;
        LastSaleReforgeReplacement = null;
        var item = FindOwned(instanceId);
        if (item == null)
        {
            return 0;
        }

        var unsellable = false;
        foreach (var tag in item.Definition.Tags)
        {
            if (tag == "unsellable")
            {
                unsellable = true;
                break;
            }
        }

        if (unsellable)
        {
            return -2;
        }

        if (item.Definition.Id == EquipmentIds.ExpansionChip && item.EquippedSlot != null)
        {
            if (GetSlotItem(EquipmentSlot.Universal) != null)
            {
                return -1;
            }
        }

        // 重铸器出售后的下一件装备不会换成金币，而是直接替换为同品质的另一件装备。
        // 重铸器自身的出售只负责武装机会，不能立刻消费这次机会。
        if (_reforgerSaleArmed
            && item.Definition.Id != EquipmentIds.Reforger
            && TryReforgeSoldItem(item))
        {
            _reforgerSaleArmed = false;
            LastSaleUsedReforger = true;
            return 0;
        }

        var definitionId = item.Definition.Id;
        item.EquippedSlot = null;
        Owned.Remove(item);
        var price = GetSellPrice(item.Definition.Rarity);
        if (price > 0)
        {
            GameManager.AddGold(price);
        }
        CodexService.RecordEquipmentSold(definitionId);

        if (definitionId == EquipmentIds.SoulStone)
        {
            ApplySoulStoneChipBonus();
        }

        if (definitionId == EquipmentIds.SpringEssence)
        {
            GameManager.AddForage(2);
        }

        if (definitionId == EquipmentIds.WitchScalp)
        {
            GameManager.IncrementAttackChipCount();
            GameManager.IncrementKnowledgeChipCount();
            GameManager.IncrementDefenseChipCount();
        }

        if (definitionId == EquipmentIds.Reforger)
        {
            _reforgerSaleArmed = true;
        }

        return price;
    }

    /// <summary>
    /// 【重铸器】的出售替换：仅接受统一重铸池认可的装备，并保持原槽位（若替代品类型兼容）。
    /// 它不是一次“获得装备”奖励，因此不触发品质跃迁、阵营工坊重选或出售附带奖励。
    /// </summary>
    private static bool TryReforgeSoldItem(OwnedEquipment item)
    {
        var candidates = GetReforgeCandidates(item.Definition);
        if (candidates.Count == 0)
        {
            return false;
        }

        var replacement = candidates[System.Random.Shared.Next(candidates.Count)];
        var previousSlot = item.EquippedSlot;
        item.EquippedSlot = null;
        Owned.Remove(item);

        var newItem = new OwnedEquipment(replacement)
        {
            AcquiredFrom = EquipmentGainSource.ReforgeReplacement.ToString()
        };
        if (previousSlot.HasValue && CanEquipToSlot(replacement, previousSlot.Value))
        {
            newItem.EquippedSlot = previousSlot.Value;
        }

        Owned.Add(newItem);
        LastSaleReforgeOriginal = item.Definition;
        LastSaleReforgeReplacement = replacement;
        return true;
    }

    // 将背包/槽中所有锈类装备升级为对应合金版本，保留原位置。返回升级后的装备名称列表。
    /// <summary>
    /// Inventory System 的公开入口：UpgradeRustWeapons。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static List<string> UpgradeRustWeapons()
    {
        var pairs = new System.Collections.Generic.Dictionary<string, string>
        {
            [EquipmentIds.RustShield] = EquipmentIds.GangDun,
            [EquipmentIds.RustSpear] = EquipmentIds.HejinJian,
            [EquipmentIds.RustSword] = EquipmentIds.HejinMao,
            [EquipmentIds.RustBlueSteelSword] = EquipmentIds.BlueSteelSword,
            [EquipmentIds.RustGuDingDao] = EquipmentIds.GuDingDao,
        };

        var toUpgrade = new List<(OwnedEquipment item, string targetId)>();
        foreach (var item in Owned)
        {
            if (pairs.TryGetValue(item.Definition.Id, out var targetId))
            {
                toUpgrade.Add((item, targetId));
            }
        }

        var names = new List<string>();
        foreach (var (item, targetId) in toUpgrade)
        {
            var targetDef = EquipmentDatabase.GetEquipment(targetId);
            if (targetDef == null)
            {
                continue;
            }

            var slot = item.EquippedSlot;
            Owned.Remove(item);

            var newItem = new OwnedEquipment(targetDef);
            if (slot.HasValue)
            {
                newItem.EquippedSlot = slot.Value;
            }

            Owned.Add(newItem);
            names.Add(targetDef.Name);
        }

        return names;
    }

    /// <summary>
    /// Inventory System 的公开入口：ReforgeFirstMatchingEquipment。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static (EquipmentDefinition? Original, EquipmentDefinition? Replacement, string Message) ReforgeFirstMatchingEquipment(string equipmentId)
    {
        foreach (var item in Owned)
        {
            if (item.Definition.Id == equipmentId)
            {
                return ReforgeEquipment(item);
            }
        }

        return (null, null, Localization.Get("equipment.reforge.not_found"));
    }

    /// <summary>
    /// Inventory System 的公开入口：ReforgeEquipment。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static (EquipmentDefinition? Original, EquipmentDefinition? Replacement, string Message) ReforgeEquipment(Guid instanceId)
    {
        var target = FindOwned(instanceId);
        return target == null
            ? (null, null, Localization.Get("equipment.reforge.not_found"))
            : ReforgeEquipment(target);
    }

    private static (EquipmentDefinition? Original, EquipmentDefinition? Replacement, string Message) ReforgeEquipment(OwnedEquipment target)
    {
        var original = target.Definition;
        if (!IsEligibleForReforge(original))
        {
            return (original, null, Localization.Get("equipment.reforge.invalid"));
        }

        var candidates = GetReforgeCandidates(original);

        if (candidates.Count == 0)
        {
            return (original, null, Localization.Get("equipment.reforge.no_candidate"));
        }

        var replacement = candidates[System.Random.Shared.Next(candidates.Count)];
        var slot = target.EquippedSlot;
        Owned.Remove(target);

        // 重铸结果的装备槽类型可能和原装备不同（例如武器重铸成了饰品），此时原来的槽位
        // 装不下新装备——CanEquipToSlot 会正确拒绝，新装备转入背包，不强行占用原槽位。
        var newItem = new OwnedEquipment(replacement);
        if (slot.HasValue && CanEquipToSlot(replacement, slot.Value))
        {
            newItem.EquippedSlot = slot.Value;
        }

        Owned.Add(newItem);
        return (
            original,
            replacement,
            Localization.GetFmt(
                "equipment.reforge.success_fmt",
                Localization.GetName(original),
                Localization.GetName(replacement)));
    }

    /// <summary>
    /// 重铸候选的唯一准入规则。所有品质都使用同一规则：仅剧情/角色专属装备不可进入。
    /// 芯片是直接结算为资源、不会生成背包实例的特殊物品，不能作为替换目标。
    /// </summary>
    public static bool IsEligibleForReforge(EquipmentDefinition definition)
    {
        // 【暴虐皇冠】是暴虐昏君的专属 Boss 装备。它可以由明确的 Boss 奖励授予，
        // 但绝不能通过随机奖励或任意重铸入口获得；这些入口都复用本准入规则。
        if (definition.Id == EquipmentIds.TyrantCrown)
        {
            return false;
        }

        if (definition.Id is EquipmentIds.AttackChip or EquipmentIds.ExpansionChip)
        {
            return false;
        }

        if (GetSlotCategory(definition) == null)
        {
            return false;
        }

        foreach (var tag in definition.Tags)
        {
            if (tag is "story" or "character_exclusive" or "zuoci")
            {
                return false;
            }
        }

        foreach (var condition in definition.UnlockConditions)
        {
            if (condition.Contains("专属", System.StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 返回同品质的全部可重铸候选。重铸不保留槽位类型；除剧情/角色专属外，
    /// 已拥有装备也保留在池中，避免背包较满时候选意外收缩为单一装备。
    /// </summary>
    public static IReadOnlyList<EquipmentDefinition> GetReforgeCandidates(EquipmentDefinition original)
    {
        return GetReforgeCandidatesAtRarity(original, original.Rarity);
    }

    /// <summary>
    /// 返回高一品质的全部可重铸候选，供各类“品质跃迁”重铸入口共用。
    /// </summary>
    public static IReadOnlyList<EquipmentDefinition> GetUpgradeReforgeCandidates(
        EquipmentDefinition original,
        bool allowExclusiveOriginal = false)
    {
        var nextRarity = original.Rarity switch
        {
            EquipmentRarity.Common => EquipmentRarity.Rare,
            EquipmentRarity.Rare => EquipmentRarity.Epic,
            EquipmentRarity.Epic => EquipmentRarity.Legendary,
            _ => (EquipmentRarity?)null
        };

        return nextRarity.HasValue
            ? GetReforgeCandidatesAtRarity(original, nextRarity.Value, allowExclusiveOriginal)
            : Array.Empty<EquipmentDefinition>();
    }

    private static IReadOnlyList<EquipmentDefinition> GetReforgeCandidatesAtRarity(
        EquipmentDefinition original,
        EquipmentRarity targetRarity,
        bool allowExclusiveOriginal = false)
    {
        var candidates = new List<EquipmentDefinition>();
        // 剧情/事件专属装备仍不能成为任何随机重铸的“结果”。但群·淬炼开局
        // 明确替换本局前两件获得的装备，因此允许它们作为这条命运的原装备。
        // 该开关只放宽 original，候选 definition 继续走统一准入规则，专属物不会泄漏进随机池。
        if (!allowExclusiveOriginal && !IsEligibleForReforge(original))
        {
            return candidates;
        }

        foreach (var definition in EquipmentDatabase.GetAllEquipments())
        {
            if (definition.Id == original.Id) continue;
            if (definition.Rarity != targetRarity) continue;
            if (!IsEligibleForReforge(definition)) continue;
            candidates.Add(definition);
        }

        return candidates;
    }

    // 阵营命运·倒手重铸 用：放弃出售金币，直接把原装备换成 replacement。
    // 顺序要求：不能出现"删了旧的却没加上新的"的中间状态——这里先记录槽位信息、
    // 从 Owned 里移除旧装备，再调用 GameManager.AddEquipment 加入新装备，两步之间不会被外部打断
    // （C# 单线程同步执行，不存在中间态被读取的风险）。
    /// <summary>
    /// Inventory System 的公开入口：ReforgeWithoutSelling。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool ReforgeWithoutSelling(Guid instanceId, EquipmentDefinition replacement)
    {
        var owned = FindOwned(instanceId);
        if (owned == null)
        {
            return false;
        }

        var previousSlot = owned.EquippedSlot;
        owned.EquippedSlot = null;
        Owned.Remove(owned);
        var added = GameManager.AddEquipment(replacement.Id, EquipmentGainSource.ReforgeReplacement);
        if (added == null)
        {
            owned.EquippedSlot = previousSlot;
            Owned.Add(owned);
            return false;
        }

        if (previousSlot.HasValue && CanEquipToSlot(replacement, previousSlot.Value))
        {
            added.EquippedSlot = previousSlot.Value;
        }

        return true;
    }

    private static void ApplySoulStoneChipBonus()
    {
        switch (System.Random.Shared.Next(3))
        {
            case 0: GameManager.IncrementAttackChipCount(); break;
            case 1: GameManager.IncrementDefenseChipCount(); break;
            default: GameManager.IncrementKnowledgeChipCount(); break;
        }
    }

    /// <summary>
    /// Inventory System 的公开入口：GetSellPrice。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetSellPrice(EquipmentRarity rarity)
    {
        return rarity switch
        {
            EquipmentRarity.Common => 25,
            EquipmentRarity.Rare => 50,
            EquipmentRarity.Epic => 100,
            EquipmentRarity.Legendary => 200,
            _ => 0
        };
    }

    /// <summary>
    /// Inventory System 的公开入口：CanEquipToSlot。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool CanEquipToSlot(EquipmentDefinition definition, EquipmentSlot slot)
    {
        if (slot == EquipmentSlot.Universal)
        {
            // 万能槽：需要扩容芯片已装备，接受武器/护甲/饰品，扩容芯片本身不可放入。
            return IsUniversalSlotUnlocked()
                && GetSlotCategory(definition) != null
                && definition.Id != EquipmentIds.ExpansionChip;
        }

        if (slot == EquipmentSlot.Accessory5)
        {
            // 爆炸果实"普通饰品槽"：仅允许普通品质的饰品类装备，稀有/史诗/传奇饰品一律拒绝。
            return GetSlotCategory(definition) == EquipmentSlotCategory.Accessory
                && definition.Rarity == EquipmentRarity.Common;
        }

        var category = GetSlotCategory(definition);
        return category != null && category == GetCategoryOfSlot(slot);
    }

    /// <summary>
    /// Inventory System 的公开入口：IsUniversalSlotUnlocked。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool IsUniversalSlotUnlocked()
    {
        return CountEquipped(EquipmentIds.ExpansionChip) > 0 || GameManager.ExpansionChipCount > 0;
    }

    // 芯片与无槽位装备不进入任何装备槽。
    /// <summary>
    /// Inventory System 的公开入口：GetSlotCategory。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static EquipmentSlotCategory? GetSlotCategory(EquipmentDefinition definition)
    {
        if (definition.Id is EquipmentIds.MysteriousChip or EquipmentIds.XianHao or EquipmentIds.SkillChip)
        {
            return null;
        }

        foreach (var type in definition.Types)
        {
            switch (type)
            {
                case EquipmentType.Weapon:
                    return EquipmentSlotCategory.Weapon;
                case EquipmentType.Armor:
                    return EquipmentSlotCategory.Armor;
                case EquipmentType.Vehicle:
                case EquipmentType.Mount:
                    return EquipmentSlotCategory.Vehicle;
                case EquipmentType.Accessory:
                    return EquipmentSlotCategory.Accessory;
            }
        }

        return null;
    }

    private static EquipmentSlotCategory GetCategoryOfSlot(EquipmentSlot slot)
    {
        return slot switch
        {
            EquipmentSlot.Weapon => EquipmentSlotCategory.Weapon,
            EquipmentSlot.Armor => EquipmentSlotCategory.Armor,
            EquipmentSlot.Vehicle => EquipmentSlotCategory.Vehicle,
            _ => EquipmentSlotCategory.Accessory
        };
    }

    // 仅统计已装备（在槽位中）的数量；背包内未装备的同名装备不计入。
    /// <summary>
    /// Inventory System 的公开入口：CountEquipped。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int CountEquipped(string equipmentId)
    {
        var count = 0;
        foreach (var item in Owned)
        {
            if (item.EquippedSlot != null && item.Definition.Id == equipmentId)
            {
                count += 1;
            }
        }

        return count;
    }

    /// <summary>
    /// Inventory System 的公开入口：GetEquippedDefinitions。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static List<EquipmentDefinition> GetEquippedDefinitions()
    {
        var result = new List<EquipmentDefinition>();
        foreach (var item in Owned)
        {
            if (item.EquippedSlot != null)
            {
                result.Add(item.Definition);
            }
        }

        return result;
    }

    // 销毁单件装备（不返还金币）。供事件系统使用（如隐藏选项移除指定装备）。
    /// <summary>
    /// Inventory System 的公开入口：DestroyEquipment。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool DestroyEquipment(Guid instanceId)
    {
        var item = FindOwned(instanceId);
        if (item == null) return false;
        item.EquippedSlot = null;
        Owned.Remove(item);
        return true;
    }

    // 销毁全部拥有装备（含已装备槽）并返回装备名称列表（供事件系统记录）。不返还金币。
    /// <summary>
    /// Inventory System 的公开入口：DestroyAllEquipment。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static List<string> DestroyAllEquipment()
    {
        var names = new List<string>();
        foreach (var item in Owned)
            names.Add(item.Definition.Name);
        Owned.Clear();
        return names;
    }

    // 仅清除背包（未装备）物品，已装入槽位的装备不受影响。供调试面板使用，不影响战斗中的装备槽数值。
    /// <summary>
    /// Inventory System 的公开入口：ClearBackpack。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void ClearBackpack()
    {
        Owned.RemoveAll(item => item.EquippedSlot == null);
    }

    /// <summary>
    /// Inventory System 的公开入口：Reset。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void Reset()
    {
        Owned.Clear();
        _reforgerSaleArmed = false;
        LastSaleUsedReforger = false;
        LastSaleReforgeOriginal = null;
        LastSaleReforgeReplacement = null;
    }
}
