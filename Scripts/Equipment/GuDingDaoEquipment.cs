//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/GuDingDaoEquipment.cs
//
// 模块：Equipment System
//
// 职责：
// 1. 实现锈古锭刀、古锭刀与真·古锭刀的战斗效果。
// 2. 在统一 Trigger 与 DamageModifier 管线中完成牌型转换和伤害倍率。
// 3. 保证古锭刀的牌型转换不改变原始牌的费用来源。
//
// 不负责：
// × 修改玩家永久牌池。
// × 直接结算伤害。
// × 控制行动栏 UI 布局。
//
// 主要依赖：
// TriggerManager
// BattleContext
// DamageModifierPipeline
//////////////////////////////////////////////////////////

/// <summary>
/// 锈古锭刀的闪避穿透效果。
///
/// 只影响玩家对零费用敌人使用的普通杀；防御层仍由统一伤害系统消费。
/// </summary>
public sealed class RustGuDingDaoBeforeDamageEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.Highest;

    /// <summary>
    /// 在防御结算前标记本次普通杀忽略敌方闪。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null
            || damage.Cancelled
            || damage.AttackType != CardType.Kill
            || damage.Source != context.Player
            || !GameManager.HasEquipment(EquipmentIds.RustGuDingDao)
            || damage.Target is not EnemyInstance targetEnemy
            || targetEnemy.CurrentMana > 0)
        {
            return;
        }

        context.IgnoreDefenderDodgeForThisAttack = true;
        context.RoundResult.AddLine($"锈古锭刀：{targetEnemy.DisplayName}费用为0，闪失效。");
        context.AddTriggerLog("[Equipment/RustGuDingDao] 目标费用为0，普通杀忽略闪。");
    }
}

/// <summary>
/// 古锭刀的揭示阶段牌型转换。
///
/// 转换后的行动保留普通杀作为费用类型，因此只改变攻防规则，不重复扣费或改变叠加数量。
/// </summary>
public sealed class GuDingDaoRevealEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBattleReveal;
    public EffectPriority Priority => EffectPriority.Highest;

    /// <summary>
    /// 将对零费用目标使用的普通杀转换为必中杀。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var action = context.PlayerAction;
        if (action == null
            || action.Type != CardType.Kill
            || action.Target is not EnemyInstance targetEnemy
            || targetEnemy.CurrentMana > 0
            || !GameManager.HasEquipment(EquipmentIds.GuDingDao))
        {
            return;
        }

        context.PlayerAction = action.TransformTo(Card.SureKill());
        context.RoundResult.AddLine($"古锭刀：{targetEnemy.DisplayName}费用为0，普通杀变为必中杀。");
        context.AddTriggerLog("[Equipment/GuDingDao] 普通杀转换为必中杀，保留原费用与叠加数量。");
    }
}

/// <summary>
/// 古锭刀系列对徐盛提供的角色专属伤害倍率。
/// </summary>
public sealed class GuDingDaoDamageBonusEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// 根据已装备的古锭刀阶段和当前杀类型加入对应浮点伤害倍率。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null
            || damage.Cancelled
            || damage.Source != context.Player
            || GameManager.CurrentCharacterId != CharacterIds.XuSheng)
        {
            return;
        }

        var multiplier = GetMultiplier(damage.AttackType);
        if (multiplier <= 1d)
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "徐盛·古锭刀",
            DamageModifierPriority.SpecialMultiplier,
            DamageModifierOperation.MultiplyFloat,
            multiplier));
        context.AddTriggerLog($"[Equipment/GuDingDao/XuSheng] {BattleRules.GetCardName(damage.AttackType)}伤害×{multiplier:0.##}。");
    }

    private static double GetMultiplier(CardType attackType)
    {
        if (GameManager.HasEquipment(EquipmentIds.TrueGuDingDao)
            && attackType == CardType.FireThunderKill)
        {
            return 1.75d;
        }

        if (GameManager.HasEquipment(EquipmentIds.GuDingDao)
            && new Card(attackType).AttackType != AttackType.None)
        {
            return 1.5d;
        }

        if (GameManager.HasEquipment(EquipmentIds.RustGuDingDao)
            && attackType == CardType.Kill)
        {
            return 1.25d;
        }

        return 1d;
    }
}
