//////////////////////////////////////////////////////////
// 文件：Scripts/SkillEffects/WuKuEffect.cs
//
// 模块：Skill Effect System
//
// 为什么存在：
// 孙尚香专属传奇被动【武库】统计玩家整个背包（不仅仅是已装备）里的攻击/防御/
// 饰品/载具装备数量，转化成最大生命值、伤害加成、费用加成。这份效果完全按照
// 项目既有的"Skill 只是数据、真正逻辑放在 IBattleEffect"架构实现（与黄月英
// 【如影随行】、孙策【魂姿】等角色专属被动完全一致），没有引入第二套技能系统，
// 也没有在 BattleManager/ShopManager 里写角色特殊判断。
//
// 职责：
// 1. WuKuMaxHpEffect：开局按背包防御装备品质统计最大生命值加成，实时同步。
// 2. WuKuVehicleManaEffect：开局按背包载具数量增加初始费用。
// 3. WuKuAttackDamageBonusEffect：杀系攻击牌与伤害锦囊命中时，按背包攻击装备
//    品质叠加固定加伤，复用既有的 DamageModifier/伤害加成接口。
// 4. WuKuFeeAccessoryBonusEffect：玩家出【费】时，按背包饰品数量额外获得费用，
//    支持小数费用（不取整）。
//
// 不负责：
// × 装备属性、掉落、商店定价（只读 InventoryManager/EquipmentDatabase 现有数据）。
// × 伤害计算公式本身（只通过 DamageEvent.AddModifier 追加一条固定加伤修正）。
// × 芯片转换为装备的逻辑（那部分在 GameManager.GrantWuKuChipEquipment，
//   因为芯片获取发生在战斗之外，不属于 IBattleEffect/Trigger 的职责范围）。
//
// 主要依赖：
// IBattleEffect / TriggerManager / InventoryManager / EquipmentDatabase / DamageModifier
//////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 武库效果的共用统计工具：按装备类型/品质汇总背包（含已装备）数量或加成。
///
/// 单独抽出来是因为四个 IBattleEffect 都需要同一套"扫描 InventoryManager.GetAllOwned()
/// 并按类型/品质分类"的逻辑，避免每个效果各写一遍。
/// </summary>
internal static class WuKuEffectUtility
{
    /// <summary>
    /// 统计背包（含已装备，读取 <see cref="InventoryManager.GetAllOwned"/>）中
    /// 带有 <paramref name="type"/> 标签的装备数量。
    /// </summary>
    public static int CountOwnedByType(EquipmentType type)
    {
        var count = 0;
        foreach (var item in InventoryManager.GetAllOwned())
        {
            if (item.Definition.Types.Contains(type))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 统计背包中带有 <paramref name="type"/> 标签的装备，按品质分别乘以对应数值后求和。
    ///
    /// 例如防御装备 (4/8/12/16) 或攻击装备 (2/4/6/8)，四个数值分别对应
    /// 普通/稀有/史诗/传说品质，顺序和 <see cref="EquipmentRarity"/> 枚举一致。
    /// </summary>
    public static int SumBonusByRarity(EquipmentType type, int commonValue, int rareValue, int epicValue, int legendaryValue)
    {
        var total = 0;
        foreach (var item in InventoryManager.GetAllOwned())
        {
            if (!item.Definition.Types.Contains(type))
            {
                continue;
            }

            total += item.Definition.Rarity switch
            {
                EquipmentRarity.Common => commonValue,
                EquipmentRarity.Rare => rareValue,
                EquipmentRarity.Epic => epicValue,
                EquipmentRarity.Legendary => legendaryValue,
                _ => 0
            };
        }

        return total;
    }
}

/// <summary>
/// 武库效果一：按背包防御装备（<see cref="EquipmentType.Defense"/>）品质提供最大生命值加成。
///
/// 品质对应加成：普通 +4，稀有 +8，史诗 +12，传说 +16。每次开局
/// （<see cref="TriggerTiming.OnGameStart"/>）都会用当前背包内容重新统计一次，
/// 直接叠加到这一局战斗的 <see cref="Player"/> 实例（<see cref="Player.AddMaxHealth"/>）上，
/// 完全不触碰跨局持久化的 <see cref="GameManager.MaxHP"/>——因为 Player 实例本身每场战斗
/// 都会重新创建（<c>ResetForNewBattle</c>），所以这里"重新统计并加一次"永远只反映
/// 当前这一局开始时的背包状态，不会因为反复进入战斗而越叠越高，也不需要额外的
/// "上次加了多少"跟踪字段——不写死、不永久累加，天然满足"实时统计"的要求。
/// </summary>
public sealed class WuKuMaxHpEffect : IBattleEffect
{
    public EffectPriority Priority => EffectPriority.Mid;
    public TriggerTiming Timing => TriggerTiming.OnGameStart;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (!context.Player.HasSkill(SkillIds.WuKu))
        {
            return;
        }

        var bonus = WuKuEffectUtility.SumBonusByRarity(EquipmentType.Defense, 4, 8, 12, 16);
        if (bonus <= 0)
        {
            return;
        }

        context.Player.AddMaxHealth(bonus);

        context.AddTriggerLog("[武库]");
        context.AddTriggerLog($"背包防御装备最大生命值加成：+{bonus}");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.WuKu, Timing, Priority, "max_hp");
    }
}

/// <summary>
/// 武库效果二：开局按背包载具（<see cref="EquipmentType.Vehicle"/>）数量增加初始费用，
/// 每件 +0.5，支持小数（不取整）。
///
/// 战斗开始时玩家费用已经由 BattleManager 重置为角色初始费用
/// （<see cref="TriggerTiming.OnGameStart"/> 在那之后触发），这里只做"在此基础上再加"，
/// 不需要修改初始费用的计算逻辑本身。
/// </summary>
public sealed class WuKuVehicleManaEffect : IBattleEffect
{
    public EffectPriority Priority => EffectPriority.Mid;
    public TriggerTiming Timing => TriggerTiming.OnGameStart;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (!context.Player.HasSkill(SkillIds.WuKu))
        {
            return;
        }

        var vehicleCount = WuKuEffectUtility.CountOwnedByType(EquipmentType.Vehicle);
        if (vehicleCount <= 0)
        {
            return;
        }

        var bonus = vehicleCount * 0.5;
        context.Player.GainMana(bonus);

        context.AddTriggerLog("[武库]");
        context.AddTriggerLog($"背包载具初始费用加成：+{bonus}");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.WuKu, Timing, Priority, "mana");
    }
}

/// <summary>
/// 武库效果三：杀系攻击牌（普通杀/火杀/雷杀/必中杀/冰杀）与所有造成伤害的锦囊命中时，
/// 按背包攻击装备（<see cref="EquipmentType.Weapon"/>）品质叠加固定加伤——
/// 普通 +2，稀有 +4，史诗 +6，传说 +8。
///
/// 复用既有的伤害加成接口（<see cref="DamageEvent.AddModifier"/> +
/// <see cref="DamageModifierPriority.FlatBonus"/>），和
/// <see cref="EquipmentDamageBonusEffect"/>（Damage/DamageEffects.cs）用的是同一套机制，
/// 只是作为一个独立的 IBattleEffect 注册，不修改 Damage 系统本身的任何文件。
/// </summary>
public sealed class WuKuAttackDamageBonusEffect : IBattleEffect
{
    public EffectPriority Priority => EffectPriority.High;
    public TriggerTiming Timing => TriggerTiming.OnDamage;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled)
        {
            return;
        }

        // 只针对玩家自己发起的攻击加伤，不影响敌方造成的伤害。
        if (damage.Source != context.Player)
        {
            return;
        }

        if (!context.Player.HasSkill(SkillIds.WuKu))
        {
            return;
        }

        // 杀系（含必中杀/冰杀）+ 所有造成伤害的锦囊（连弩/南蛮入侵/天降雷击等）。
        if (!BattleRules.IsAnyAttackCard(damage.AttackType))
        {
            return;
        }

        var bonus = WuKuEffectUtility.SumBonusByRarity(EquipmentType.Weapon, 2, 4, 6, 8);
        if (bonus <= 0)
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "武库-攻击装备加伤",
            DamageModifierPriority.FlatBonus,
            DamageModifierOperation.Add,
            bonus));

        context.AddTriggerLog("[武库]");
        context.AddTriggerLog($"背包攻击装备加伤：+{bonus}");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.WuKu, Timing, Priority, "damage_bonus");
    }
}

/// <summary>
/// 武库效果四：玩家使用【费】时，按背包饰品（<see cref="EquipmentType.Accessory"/>）
/// 数量额外获得费用，每件 +0.2，允许出现小数费用（不向上/向下取整）。
///
/// 【费】本身的基础费用获取仍然由 BattleResolver 处理（本效果不修改、也不需要知道
/// 那部分代码），这里只在同一个 <see cref="TriggerTiming.OnBattlePhase"/> 时机里
/// 检测"这一回合玩家出的是【费】"，再独立叠加饰品加成——两次 GainMana 调用互不影响，
/// 顺序也不影响最终结果。
/// </summary>
public sealed class WuKuFeeAccessoryBonusEffect : IBattleEffect
{
    public EffectPriority Priority => EffectPriority.Low;
    public TriggerTiming Timing => TriggerTiming.OnBattlePhase;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.PlayerAction is not { IsFee: true })
        {
            return;
        }

        if (!context.Player.HasSkill(SkillIds.WuKu))
        {
            return;
        }

        var accessoryCount = WuKuEffectUtility.CountOwnedByType(EquipmentType.Accessory);
        if (accessoryCount <= 0)
        {
            return;
        }

        var bonus = accessoryCount * 0.2;
        context.Player.GainMana(bonus);

        context.AddTriggerLog("[武库]");
        context.AddTriggerLog($"背包饰品费用加成：+{bonus}");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.WuKu, Timing, Priority, "fee_bonus");
    }
}
