//////////////////////////////////////////////////////////
// 高温模块（史诗·饰品）
//
// 只增幅玩家打出的火属性攻击牌。装备、技能和环境伤害即使带有 Fire 属性，
// Origin.Kind 也不为 AttackAction，因而不会被错误放大。
//////////////////////////////////////////////////////////

public sealed class HighTemperatureModuleEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Mid;

    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null
            || damage.Cancelled
            || damage.Source != context.Player
            || damage.Origin.Kind != HealthChangeSourceKind.AttackAction
            || !damage.DamageType.HasFlag(DamageType.Fire)
            || !GameManager.HasEquipment(EquipmentIds.HighTemperatureModule))
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "高温模块（火属性攻击×1.5）",
            DamageModifierPriority.ArmorEquipmentMultiplier,
            DamageModifierOperation.MultiplyFloat,
            1.5));
        context.RoundResult.AddLine("高温模块触发：火属性攻击伤害×1.5。");
        context.AddTriggerLog("[高温模块] 火属性攻击伤害×1.5。");
    }
}
