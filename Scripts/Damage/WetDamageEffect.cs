public sealed class WetDamageEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.High;

    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || RunBuffManager.CountStacks(RunBuffIds.Wet) == 0) return;
        var elemental = damage.DamageType & (DamageType.Fire | DamageType.Thunder | DamageType.Ice | DamageType.Poison);
        if (elemental == DamageType.None) return;
        if (damage.Target == context.Player && GameManager.HasEquipment(EquipmentIds.WaterproofModule)) return;
        if (damage.Target is EnemyInstance enemy && enemy.HasEquipment(EquipmentIds.WaterproofModule)) return;
        damage.AddModifier(new DamageModifier("潮湿", DamageModifierPriority.SpecialMultiplier, DamageModifierOperation.MultiplyFloat, 1.2));
        context.RoundResult.AddLine($"潮湿：{damage.Target.DisplayName}受到的元素伤害×1.2。");
        context.AddTriggerLog($"[ChapterVariant/Wet] {damage.Target.DisplayName} element damage ×1.2");
    }
}
