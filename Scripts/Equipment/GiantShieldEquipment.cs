//////////////////////////////////////////////////////////
// 巨人盾（史诗·护甲）
//
// 战斗开始的 +45 最大生命值由 BattleManager / EnemyInstance 的统一装备生命
// 加成入口处理；本文件只负责伤害管线中的动态部分。使用当前生命值，而不是
// 最大生命值，确保每次受伤后下一次攻击的加伤会同步降低。
//////////////////////////////////////////////////////////

/// <summary>
/// 巨人盾：主动攻击牌的伤害额外增加装备者当前生命值的3%（向下取整）。
/// 技能、装备、反伤和持续伤害不是主动攻击牌，因此不触发。
/// </summary>
public sealed class GiantShieldDamageEffect : IBattleEffect
{
    private const int CurrentHealthPercent = 3;

    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.High;

    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null
            || damage.Cancelled
            || !damage.IsDirectAttackDamage
            || !BattleRules.IsAnyAttackCard(damage.AttackType)
            || !BattleRules.HasEquipment(damage.Source, EquipmentIds.GiantShield))
        {
            return;
        }

        // 整数除法即向下取整；生命很低时允许结果为0，不强制至少加1。
        var bonus = damage.Source.Health * CurrentHealthPercent / 100;
        if (bonus <= 0)
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "巨人盾（当前生命值3%）",
            DamageModifierPriority.FlatBonus,
            DamageModifierOperation.Add,
            bonus));
        context.RoundResult.AddLine($"巨人盾：{damage.Source.DisplayName}当前生命{damage.Source.Health}，攻击伤害+{bonus}。");
        context.AddTriggerLog("[Equipment/GiantShield]");
        context.AddTriggerLog($"{BattleRules.GetCardName(damage.AttackType)}：当前生命{damage.Source.Health}×3%→伤害+{bonus}。");
    }
}
