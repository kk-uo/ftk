//////////////////////////////////////////////////////////
// 文件：Scripts/Card.cs
//
// 模块：Card System
//
// 职责：
// 1. 承载卡牌定义、卡牌 UI 与卡牌规则相关代码。
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

/// <summary>
/// Card System 的公开类：BattleConstants。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class BattleConstants
{
    public const int InitialHealth = 40;
    public const int InitialMana = 1;
    public const int KillDamage = 10;
    public const int FireKillDamage = 15;
    public const int PeachHeal = 10;
    public const int WineDamageBonus = 1;
}

/// <summary>
/// Card System 的公开枚举：CardCategory。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
[Flags]
public enum CardCategory
{
    None = 0,
    Attack = 1 << 0,
    Defense = 1 << 1,
    Recovery = 1 << 2,
    Trick = 1 << 3,
    Resource = 1 << 4,
    Enhancement = 1 << 5
}

/// <summary>
/// 攻击招式携带的元素属性。None 表示该招式没有元素属性（即规则中的 null 属性）。
/// 属性是招式定义的一部分，与伤害来源、物理伤害、真实伤害等结算概念分离；
/// 同一招式可同时拥有多个属性，例如火雷杀 = Fire | Thunder。
/// </summary>
[Flags]
public enum AttackAttribute
{
    None = 0,
    Fire = 1 << 0,
    Thunder = 1 << 1,
    Ice = 1 << 2,
    Poison = 1 << 3
}

/// <summary>
/// 所有攻击招式属性的唯一数据入口。新增攻击牌时必须在此登记其元素属性，
/// 以保证卡面词条、图鉴与战斗规则读取到同一份定义。
/// </summary>
public static class AttackAttributeRules
{
    public static AttackAttribute GetAttributes(CardType type)
    {
        var attributes = GetIntrinsicAttributes(type);
        if (new Card(type).IsAttack)
        {
            attributes |= GameManager.PlayerAttackAttributeBonus;
            if (type == CardType.CelestialImpact && GameManager.IsMoonGemElementallyAwakened)
            {
                attributes |= AttackAttribute.Fire | AttackAttribute.Thunder | AttackAttribute.Ice | AttackAttribute.Poison;
            }
        }

        return attributes;
    }

    /// <summary>卡牌定义的固有元素属性，不含本局玩家侧祭坛增益。</summary>
    public static AttackAttribute GetIntrinsicAttributes(CardType type)
    {
        return type switch
        {
            CardType.FireKill or CardType.FireAttack => AttackAttribute.Fire,
            CardType.ThunderKill or CardType.LightningStrike => AttackAttribute.Thunder,
            CardType.FireThunderKill => AttackAttribute.Fire | AttackAttribute.Thunder,
            CardType.IceKill => AttackAttribute.Ice,
            CardType.PoisonKill => AttackAttribute.Poison,
            // 爆炸果实强化会把万箭齐发实际改为火属性伤害，卡面同步反映该局内变化。
            CardType.ArrowBarrage when GameManager.IsArrowBarrageFireUpgraded => AttackAttribute.Fire,
            _ => AttackAttribute.None
        };
    }

    /// <summary>把元素词条转换为统一伤害管线使用的伤害标记。</summary>
    public static DamageType ToDamageType(AttackAttribute attributes)
    {
        if (attributes == AttackAttribute.None)
        {
            return DamageType.Physical;
        }

        var type = (DamageType)0;
        if (attributes.HasFlag(AttackAttribute.Fire)) type |= DamageType.Fire;
        if (attributes.HasFlag(AttackAttribute.Thunder)) type |= DamageType.Thunder;
        if (attributes.HasFlag(AttackAttribute.Ice)) type |= DamageType.Ice;
        if (attributes.HasFlag(AttackAttribute.Poison)) type |= DamageType.Poison;
        return type;
    }

    public static string GetTraitText(AttackAttribute attributes)
    {
        var names = new System.Collections.Generic.List<string>();
        if (attributes.HasFlag(AttackAttribute.Fire)) names.Add(Localization.Get("card.attribute.fire"));
        if (attributes.HasFlag(AttackAttribute.Thunder)) names.Add(Localization.Get("card.attribute.thunder"));
        if (attributes.HasFlag(AttackAttribute.Ice)) names.Add(Localization.Get("card.attribute.ice"));
        if (attributes.HasFlag(AttackAttribute.Poison)) names.Add(Localization.Get("card.attribute.poison"));

        var value = names.Count == 0
            ? Localization.Get("card.attribute.none")
            : string.Join(" / ", names);
        return Localization.GetFmt("card.attribute.label", value);
    }
}

/// <summary>
/// Card System 的公开枚举：CardSubType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum CardSubType
{
    Kill,
    FireKill,
    ThunderKill,
    FireThunderKill,   // 火雷杀：同时具有火属性与雷属性的杀系攻击牌
    DirectSha,
    IceKill,           // 冰杀：穿透闪，命中后施加冰冻
    CelestialImpact,
    ArrowBarrage,
    NanmanInvasion,
    Dodge,
    Peach,
    Wine,
    Steal,
    Unassailable,
    Fee,
    Guanxing,
    JiGuActivate,
    ShadowLurk,        // 潜伏：影袭状态专属，跳过本回合
    YingXiActivate,    // 影袭激活卡
    Meihuo,            // 魅惑：指向性锦囊，施加魅惑护盾控制
    Tuxi,              // 突袭：张辽角色专属锦囊牌，不进入任何普通随机卡池
    FireAttack,        // 火攻：周瑜角色专属锦囊牌，不进入任何普通随机卡池
    IronChain          // 铁索连环：庞统专属锦囊牌
}

/// <summary>
/// Card System 的公开枚举：AttackType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum AttackType
{
    None,
    Sha,
    FireSha,
    ThunderSha,
    FireThunderSha,    // 火雷杀：兼具火/雷属性，穿透闪但不穿透标准护盾，费用3，基础伤害30
    DirectSha,
    IceSha             // 冰杀：穿透闪，命中后施加冰冻Buff（1回合只能出费）
}

/// <summary>
/// Card System 的公开枚举：CardTargetType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum CardTargetType
{
    NonTargeted,
    SelfTarget,
    Targeted
}

/// <summary>
/// Card System 的公开枚举：CardType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum CardType
{
    Kill,
    FireKill,
    ThunderKill,
    FireThunderKill,   // 火雷杀：仅通过技能/装备/事件获得，不进入默认卡组与普通出牌区
    SureKill,
    IceKill,           // 冰杀：仅通过极寒技能解锁；穿透闪，命中后施加冰冻（1回合只能出费）
    CelestialImpact,
    ArrowBarrage,
    NanmanInvasion,
    Dodge,
    Peach,
    Wine,
    Steal,
    Unassailable,
    Fee,
    Guanxing,
    JiGuActivate,
    YingXiActivate,    // 影袭激活卡：主动进入影袭状态，费用可变（0/1/2）
    ShadowKill,        // 影袭杀：影袭状态专属，1费，DirectSha，每次影袭期仅可使用一次
    ShadowLurk,        // 潜伏：影袭状态专属，0费，跳过本回合，不中断影袭状态
    ZiBaoAttack,       // 自爆攻击：首次濒死后下回合释放，仅无懈、桃盾、酒盾可抵挡
    LightningStrike,   // 闪电：黄天Buff的雷属性机制伤害，非攻击牌，不可被闪避
    Meihuo,            // 魅惑：貂蝉专属，1费，指向性锦囊；护盾是否被击破不影响下回合控制
    PoisonKill,        // 毒杀：瘟疫权杖将普通杀进化而成，同费同伤，命中后立即附加10点Poison（毒素）伤害+10层【瘟疫】
    PoJunBoost,        // 破军：破军 Reaction 专属交互牌，消耗0.5费令已命中的杀伤害额外+1倍
    XueZhaiAttack,     // 血债血偿：独眼巨人专属，0费，濒死后下回合对玩家造成40%最大HP真实伤害，无视闪/无懈/护盾
    Tuxi,              // 突袭：追加在枚举末尾，避免改变旧存档中既有卡牌的数值映射
    FireAttack,        // 火攻：周瑜专属，追加在枚举末尾，避免改变旧存档中既有卡牌的数值映射
    IronChain          // 铁索连环：庞统专属，追加在枚举末尾，避免改变既有映射
}

/// <summary>
/// Card System 的公开类：Card。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed partial class Card : ILocalizedDefinition
{
    /// <summary>
    /// Card System 的公开入口：Card。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public Card(CardType type)
    {
        Type = type;
    }

    public CardType Type { get; }

    string ILocalizedDefinition.Id => Type.ToString().ToLowerInvariant();
    string ILocalizedDefinition.NameKey => BattleRules.GetCardNameKey(Type);
    string ILocalizedDefinition.DescriptionKey => string.Empty;

    public string Name => BattleRules.GetCardName(Type);

    /// <summary>攻击招式的元素属性；None 即无属性。</summary>
    public AttackAttribute Attributes => AttackAttributeRules.GetAttributes(Type);

    public double Cost => Type switch
    {
        CardType.Kill => 1,
        CardType.FireKill => 2,
        CardType.ThunderKill => 2,
        CardType.FireThunderKill => 3,
        CardType.SureKill => 1,
        CardType.IceKill => 3,
        CardType.CelestialImpact => 1,
        CardType.ArrowBarrage => 2,
        CardType.NanmanInvasion => 3,
        CardType.Tuxi => 2,
        CardType.Dodge => 0,
        CardType.Peach => 2,
        CardType.Wine => 1,
        CardType.Steal => 1,
        CardType.Unassailable => 0.5,
        CardType.Fee => 0,
        CardType.Guanxing => 0,
        CardType.JiGuActivate => 0,
        CardType.YingXiActivate => 1,  // 默认1费，实际由BattleRules.GetCardCost动态返回
        CardType.ShadowKill => 1,
        CardType.ShadowLurk => 0,
        CardType.Meihuo => 1,
        CardType.PoisonKill => 1,
        CardType.FireAttack => 1,
        CardType.IronChain => 1,
        _ => 0
    };

    public CardCategory Categories => Type switch
    {
        CardType.Kill => CardCategory.Attack,
        CardType.FireKill => CardCategory.Attack,
        CardType.ThunderKill => CardCategory.Attack,
        CardType.FireThunderKill => CardCategory.Attack,
        CardType.SureKill => CardCategory.Attack,
        CardType.IceKill => CardCategory.Attack,
        CardType.CelestialImpact => CardCategory.Attack | CardCategory.Trick,
        CardType.ArrowBarrage => CardCategory.Attack | CardCategory.Trick,
        CardType.NanmanInvasion => CardCategory.Attack | CardCategory.Trick,
        CardType.Tuxi => CardCategory.Attack | CardCategory.Trick,
        CardType.Dodge => CardCategory.Defense,
        CardType.Peach => CardCategory.Recovery,
        CardType.Wine => CardCategory.Recovery | CardCategory.Enhancement,
        CardType.Steal => CardCategory.Trick,
        CardType.Unassailable => CardCategory.Trick,
        CardType.Fee => CardCategory.Resource,
        CardType.Guanxing => CardCategory.Trick,
        CardType.JiGuActivate => CardCategory.None,
        CardType.YingXiActivate => CardCategory.None,
        CardType.ShadowKill => CardCategory.Attack,
        CardType.ShadowLurk => CardCategory.None,
        CardType.Meihuo => CardCategory.Trick,
        CardType.PoisonKill => CardCategory.Attack,
        CardType.FireAttack => CardCategory.Attack | CardCategory.Trick,
        CardType.IronChain => CardCategory.Trick,
        _ => CardCategory.None
    };

    public CardSubType SubType => Type switch
    {
        CardType.Kill => CardSubType.Kill,
        CardType.FireKill => CardSubType.FireKill,
        CardType.ThunderKill => CardSubType.ThunderKill,
        CardType.FireThunderKill => CardSubType.FireThunderKill,
        CardType.SureKill => CardSubType.DirectSha,
        CardType.IceKill => CardSubType.IceKill,
        CardType.CelestialImpact => CardSubType.CelestialImpact,
        CardType.ArrowBarrage => CardSubType.ArrowBarrage,
        CardType.NanmanInvasion => CardSubType.NanmanInvasion,
        CardType.Tuxi => CardSubType.Tuxi,
        CardType.Dodge => CardSubType.Dodge,
        CardType.Peach => CardSubType.Peach,
        CardType.Wine => CardSubType.Wine,
        CardType.Steal => CardSubType.Steal,
        CardType.Unassailable => CardSubType.Unassailable,
        CardType.Fee => CardSubType.Fee,
        CardType.Guanxing => CardSubType.Guanxing,
        CardType.JiGuActivate => CardSubType.JiGuActivate,
        CardType.YingXiActivate => CardSubType.YingXiActivate,
        CardType.ShadowKill => CardSubType.DirectSha,
        CardType.ShadowLurk => CardSubType.ShadowLurk,
        CardType.ZiBaoAttack => CardSubType.Kill,
        CardType.LightningStrike => CardSubType.Fee,
        CardType.Meihuo => CardSubType.Meihuo,
        CardType.PoisonKill => CardSubType.Kill,
        CardType.PoJunBoost => CardSubType.Fee,
        CardType.XueZhaiAttack => CardSubType.Fee,
        CardType.FireAttack => CardSubType.FireAttack,
        CardType.IronChain => CardSubType.IronChain,
        _ => throw new ArgumentOutOfRangeException()
    };

    public AttackType AttackType => Type switch
    {
        CardType.Kill => AttackType.Sha,
        CardType.FireKill => AttackType.FireSha,
        CardType.ThunderKill => AttackType.ThunderSha,
        CardType.FireThunderKill => AttackType.FireThunderSha,
        CardType.SureKill => AttackType.DirectSha,
        CardType.IceKill => AttackType.IceSha,
        CardType.ShadowKill => AttackType.DirectSha,
        CardType.PoisonKill => AttackType.Sha,
        _ => AttackType.None
    };

    public CardTargetType TargetType => Type switch
    {
        CardType.Fee => CardTargetType.NonTargeted,
        CardType.Dodge => CardTargetType.NonTargeted,
        CardType.Unassailable => CardTargetType.NonTargeted,
        CardType.ArrowBarrage => CardTargetType.NonTargeted,
        CardType.NanmanInvasion => CardTargetType.NonTargeted,
        CardType.Tuxi => CardTargetType.NonTargeted,
        CardType.CelestialImpact => CardTargetType.NonTargeted,
        CardType.Peach => CardTargetType.SelfTarget,
        CardType.Wine => CardTargetType.SelfTarget,
        CardType.Kill => CardTargetType.Targeted,
        CardType.FireKill => CardTargetType.Targeted,
        CardType.ThunderKill => CardTargetType.Targeted,
        CardType.FireThunderKill => CardTargetType.Targeted,
        CardType.SureKill => CardTargetType.Targeted,
        CardType.IceKill => CardTargetType.Targeted,
        CardType.Steal => CardTargetType.Targeted,
        CardType.Meihuo => CardTargetType.Targeted,
        CardType.Guanxing => CardTargetType.NonTargeted,
        CardType.YingXiActivate => CardTargetType.NonTargeted,
        CardType.ShadowKill => CardTargetType.Targeted,
        CardType.ShadowLurk => CardTargetType.NonTargeted,
        CardType.PoisonKill => CardTargetType.Targeted,
        CardType.FireAttack => CardTargetType.Targeted,
        CardType.IronChain => CardTargetType.NonTargeted,
        _ => CardTargetType.NonTargeted
    };

    // 卡面只展示：名称 / 费用 / 类型 / 核心效果，固定四行。克制关系/响应关系/特殊交互说明由规则表维护，不在卡面展示。
    public string TypeLabel => Type switch
    {
        CardType.Kill => Localization.Get("card.type.attack"),
        CardType.FireKill => Localization.Get("card.type.attack"),
        CardType.ThunderKill => Localization.Get("card.type.attack"),
        CardType.FireThunderKill => Localization.Get("card.type.attack"),
        CardType.SureKill => Localization.Get("card.type.attack"),
        CardType.IceKill => Localization.Get("card.type.attack"),
        CardType.CelestialImpact => Localization.Get("card.type.attack"),
        CardType.ArrowBarrage => Localization.Get("card.type.ranged_attack"),
        CardType.NanmanInvasion => Localization.Get("card.type.ranged_attack"),
        CardType.Tuxi => Localization.Get("card.type.trick"),
        CardType.Dodge => Localization.Get("card.type.defense"),
        CardType.Peach => Localization.Get("card.type.recovery"),
        CardType.Wine => Localization.Get("card.type.enhancement"),
        CardType.Steal => Localization.Get("card.type.resource"),
        CardType.Unassailable => Localization.Get("card.type.trick"),
        CardType.Fee => Localization.Get("card.type.resource"),
        CardType.Guanxing => Localization.Get("card.type.stargazing"),
        CardType.JiGuActivate => Localization.Get("card.type.skill_activate"),
        CardType.YingXiActivate => Localization.Get("card.type.skill_activate"),
        CardType.ShadowKill => Localization.Get("card.type.attack"),
        CardType.ShadowLurk => Localization.Get("card.type.skill_card"),
        CardType.Meihuo => Localization.Get("card.type.trick"),
        CardType.PoisonKill => Localization.Get("card.type.attack"),
        CardType.ZiBaoAttack => Localization.Get("card.type.special_attack"),
        CardType.LightningStrike => Localization.Get("card.type.special_damage"),
        CardType.PoJunBoost => Localization.Get("card.type.response"),
        CardType.XueZhaiAttack => Localization.Get("card.type.special_damage"),
        CardType.FireAttack => Localization.Get("card.type.trick"),
        CardType.IronChain => Localization.Get("card.type.trick"),
        _ => string.Empty
    };

    public string Description => Type switch
    {
        CardType.Kill => string.Format(Localization.Get("card.kill.desc"), BattleConstants.KillDamage),
        CardType.FireKill => string.Format(Localization.Get("card.firekill.desc"), BattleConstants.FireKillDamage),
        CardType.ThunderKill => string.Format(Localization.Get("card.thunderkill.desc"), BattleConstants.KillDamage),
        CardType.FireThunderKill => string.Format(Localization.Get("card.firethunderkill.desc"), BattleConstants.KillDamage * 3),
        CardType.SureKill => string.Format(Localization.Get("card.surekill.desc"), BattleConstants.KillDamage),
        CardType.IceKill => string.Format(Localization.Get("card.icekill.desc"), BattleConstants.KillDamage),
        CardType.CelestialImpact => Localization.Get("card.celestialimpact.desc"),
        CardType.ArrowBarrage => string.Format(Localization.Get("card.arrowbarrage.desc"), BattleConstants.KillDamage),
        CardType.NanmanInvasion => string.Format(Localization.Get("card.nanmaninvasion.desc"), BattleConstants.KillDamage),
        CardType.Tuxi => string.Format(Localization.Get("card.tuxi.desc"), BattleConstants.KillDamage),
        CardType.Dodge => Localization.Get("card.dodge.desc"),
        CardType.Peach => Localization.Get("card.peach.desc"),
        CardType.Wine => Localization.Get("card.wine.desc"),
        CardType.Steal => Localization.Get("card.steal.desc"),
        CardType.Unassailable => Localization.Get("card.unassailable.desc"),
        CardType.Fee => Localization.Get("card.fee.desc"),
        CardType.Guanxing => Localization.Get("card.guanxing.desc"),
        CardType.JiGuActivate => Localization.Get("card.jiguactivate.desc"),
        CardType.YingXiActivate => Localization.Get("card.yingxiactivate.desc"),
        CardType.ShadowKill => string.Format(Localization.Get("card.shadowkill.desc"), BattleConstants.KillDamage),
        CardType.ShadowLurk => Localization.Get("card.shadowlurk.desc"),
        CardType.ZiBaoAttack => Localization.Get("card.zibaoattack.desc"),
        CardType.LightningStrike => Localization.Get("card.lightningstrike.desc"),
        CardType.Meihuo => Localization.Get("card.meihuo.desc"),
        CardType.PoisonKill => string.Format(Localization.Get("card.poisonkill.desc"), BattleConstants.KillDamage),
        CardType.PoJunBoost => Localization.Get("card.pojunboost.desc"),
        CardType.XueZhaiAttack => Localization.Get("card.xuezhaiattack.desc"),
        CardType.FireAttack => Localization.Get("card.fireattack.desc"),
        CardType.IronChain => Localization.Get("card.ironchain.desc"),
        _ => string.Empty
    };

    public bool IsAttack => Categories.HasFlag(CardCategory.Attack);

    /// <summary>
    /// 是否属于需要展示属性词条的攻击行动。部分由系统触发的伤害招式没有普通攻击分类，
    /// 也在这里纳入，避免未来将其做成可展示卡牌时遗漏属性信息。
    /// </summary>
    public bool IsAttackAction => IsAttack
        || Type is CardType.ZiBaoAttack or CardType.LightningStrike or CardType.PoJunBoost or CardType.XueZhaiAttack;

    /// <summary>卡面攻击词条；非攻击行动为空，攻击行动始终显示“属性：…”。</summary>
    public string AttributeTraitText => IsAttackAction ? AttackAttributeRules.GetTraitText(Attributes) : string.Empty;

    /// <summary>卡面类型行。攻击行动在类型后追加元素属性词条。</summary>
    public string DisplayTypeLabel => string.IsNullOrEmpty(AttributeTraitText)
        ? TypeLabel
        : $"{TypeLabel} · {AttributeTraitText}";

    public bool IsTrickCard => Categories.HasFlag(CardCategory.Trick);

    /// <summary>
    /// Card System 的公开入口：Kill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card Kill() => new(CardType.Kill);

    /// <summary>
    /// Card System 的公开入口：FireKill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card FireKill() => new(CardType.FireKill);

    /// <summary>
    /// Card System 的公开入口：ThunderKill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card ThunderKill() => new(CardType.ThunderKill);

    // 火雷杀：仅通过技能/装备/事件特殊途径获得，不进入默认卡组与普通出牌区。
    /// <summary>
    /// Card System 的公开入口：FireThunderKill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card FireThunderKill() => new(CardType.FireThunderKill);

    /// <summary>
    /// Card System 的公开入口：SureKill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card SureKill() => new(CardType.SureKill);

    /// <summary>
    /// Card System 的公开入口：IceKill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card IceKill() => new(CardType.IceKill);

    /// <summary>
    /// Card System 的公开入口：CelestialImpact。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card CelestialImpact() => new(CardType.CelestialImpact);

    /// <summary>
    /// Card System 的公开入口：ArrowBarrage。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card ArrowBarrage() => new(CardType.ArrowBarrage);

    /// <summary>
    /// Card System 的公开入口：NanmanInvasion。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card NanmanInvasion() => new(CardType.NanmanInvasion);

    /// <summary>
    /// 创建张辽专属锦囊牌【突袭】。
    /// </summary>
    public static Card Tuxi() => new(CardType.Tuxi);

    /// <summary>
    /// Card System 的公开入口：FireAttack。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card FireAttack() => new(CardType.FireAttack);

    /// <summary>
    /// 庞统专属锦囊牌【铁索连环】。
    /// </summary>
    public static Card IronChain() => new(CardType.IronChain);

    /// <summary>
    /// Card System 的公开入口：Dodge。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card Dodge() => new(CardType.Dodge);

    /// <summary>
    /// Card System 的公开入口：Peach。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card Peach() => new(CardType.Peach);

    /// <summary>
    /// Card System 的公开入口：Wine。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card Wine() => new(CardType.Wine);

    /// <summary>
    /// Card System 的公开入口：Steal。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card Steal() => new(CardType.Steal);

    /// <summary>
    /// Card System 的公开入口：Unassailable。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card Unassailable() => new(CardType.Unassailable);

    /// <summary>
    /// Card System 的公开入口：Fee。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card Fee() => new(CardType.Fee);

    /// <summary>
    /// Card System 的公开入口：Guanxing。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card Guanxing() => new(CardType.Guanxing);

    /// <summary>
    /// Card System 的公开入口：JiGuActivate。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card JiGuActivate() => new(CardType.JiGuActivate);

    /// <summary>
    /// Card System 的公开入口：YingXiActivate。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card YingXiActivate() => new(CardType.YingXiActivate);

    /// <summary>
    /// Card System 的公开入口：ShadowKill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card ShadowKill() => new(CardType.ShadowKill);

    /// <summary>
    /// Card System 的公开入口：ShadowLurk。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card ShadowLurk() => new(CardType.ShadowLurk);

    /// <summary>
    /// Card System 的公开入口：ZiBaoAttack。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card ZiBaoAttack() => new(CardType.ZiBaoAttack);

    /// <summary>
    /// Card System 的公开入口：Meihuo。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card Meihuo() => new(CardType.Meihuo);

    /// <summary>
    /// Card System 的公开入口：PoisonKill。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card PoisonKill() => new(CardType.PoisonKill);

    /// <summary>
    /// Card System 的公开入口：XueZhaiAttack。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Card XueZhaiAttack() => new(CardType.XueZhaiAttack);
}
