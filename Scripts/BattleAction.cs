//////////////////////////////////////////////////////////
// 文件：Scripts/BattleAction.cs
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

public sealed class BattleAction
{
    private BattleAction(
        CardType type,
        CardType costType,
        string cardName,
        double cardCost,
        int count,
        CardTargetType targetType,
        BattleUnit? target)
    {
        Type = type;
        CostType = costType;
        CardName = cardName;
        Count = count;
        Cost = cardCost * count;
        TargetType = targetType;
        Target = target;
    }

    public CardType Type { get; }
    /// <summary>
    /// 费用结算所使用的原始牌型。
    ///
    /// 装备可能在揭示阶段把普通杀转换为其它杀，但不应因此改变玩家已经选择的牌的费用规则。
    /// </summary>
    public CardType CostType { get; }
    public string CardName { get; }
    public int Count { get; }
    public double Cost { get; }
    public CardTargetType TargetType { get; }
    public BattleUnit? Target { get; }
    public bool IsAttack => Type is CardType.Kill or CardType.FireKill or CardType.ThunderKill or CardType.FireThunderKill or CardType.SureKill or CardType.IceKill or CardType.ShadowKill or CardType.PoisonKill or CardType.FireAttack;
    public bool IsCelestialImpact => Type == CardType.CelestialImpact;
    public bool IsArrowBarrage => Type == CardType.ArrowBarrage;
    public bool IsNanmanInvasion => Type == CardType.NanmanInvasion;
    public bool IsTuxi => Type == CardType.Tuxi;
    public bool UsesNanmanSettlement => IsNanmanInvasion || IsTuxi || IsCelestialImpact;
    // 所有可触发龙胆的攻击性行动：杀系牌、万箭齐发、南蛮入侵。
    public bool CanTriggerDragonCourage => IsAttack || IsArrowBarrage || UsesNanmanSettlement;
    public bool IsDodge => Type == CardType.Dodge;
    public bool IsPeach => Type == CardType.Peach;
    public bool IsWine => Type == CardType.Wine;
    public bool IsSteal => Type == CardType.Steal;
    public bool IsUnassailable => Type == CardType.Unassailable;
    public bool IsFee => Type == CardType.Fee;
    public bool IsMeihuo => Type == CardType.Meihuo;
    public string DisplayName => CanStackType(Type) ? $"{CardName} ×{Count}" : CardName;

    /// <summary>
    /// Core System 的公开入口：FromCard。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static BattleAction FromCard(Card card, int count = 1, BattleUnit? target = null)
    {
        return new BattleAction(card.Type, card.Type, card.Name, card.Cost, count, card.TargetType, target);
    }

    /// <summary>
    /// 将行动转换为另一张牌，同时保留原始牌的费用类型、叠加数量和锁定目标。
    ///
    /// 用于古锭刀等“结算时视为另一张牌”的效果，避免表现转换意外改变扣费。
    /// </summary>
    public BattleAction TransformTo(Card card)
    {
        return new BattleAction(
            card.Type,
            CostType,
            card.Name,
            new Card(CostType).Cost,
            Count,
            card.TargetType,
            Target);
    }

    /// <summary>
    /// Core System 的公开入口：CanStackType。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool CanStackType(CardType type)
    {
        // 所有出牌区中的攻击性牌都可以在同一回合连续使用，费用按每一张独立累计。
        // 影袭杀虽然属于攻击牌，但它受【影袭】“每次影袭期仅可使用一次”的专属限制，
        // 不能因通用叠加规则绕过该限制。自爆、闪电等不属于可手动出牌的攻击牌，
        // 也不会因为没有 Attack 分类而进入这里。
        if (type != CardType.ShadowKill && new Card(type).IsAttack)
        {
            return true;
        }

        // 保留既有的回复牌叠加规则；其余非攻击性牌一次只能出一张。
        return type is CardType.Peach or CardType.Wine;
    }
}
