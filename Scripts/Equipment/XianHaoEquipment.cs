//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/XianHaoEquipment.cs
//
// 模块：Equipment System
//
// 【仙毫】：无槽位传奇武器。
// 受到直接攻击伤害前，按当前概率免疫；装备者的直接攻击实际命中后提高概率。
//////////////////////////////////////////////////////////

using System;

public sealed class XianHaoDodgeEffect : IBattleEffect
{
    private const string DodgeChanceKey = "xian_hao_dodge_chance_percent";
    internal const int InitialDodgeChancePercent = 25;
    internal const int DodgeChanceIncreasePerHit = 10;
    internal const int MaximumDodgeChancePercent = 90;

    // 仅供无头回归固定随机结果；正式运行保持 null 并使用 Random.Shared。
    internal static Func<double>? RandomDoubleProviderForTests { get; set; }

    public TriggerTiming Timing => TriggerTiming.OnBeforeDamage;
    public EffectPriority Priority => EffectPriority.Highest;

    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null
            || damage.Cancelled
            || damage.Amount <= 0
            || damage.OnlyAllowCounterOrCardShields
            || !damage.IsDirectAttackDamage
            || !HasXianHao(context, damage.Target))
        {
            return;
        }

        var chancePercent = GetCurrentChancePercent(damage.Target);
        var roll = RandomDoubleProviderForTests?.Invoke() ?? Random.Shared.NextDouble();
        if (roll >= chancePercent / 100.0)
        {
            return;
        }

        damage.CancelAsFullyBlocked();
        context.RoundResult.AddLine($"仙毫触发：{damage.Target.DisplayName}挥毫改命，免疫本次攻击伤害（{chancePercent}%）。");
        context.AddTriggerLog("[Equipment/仙毫]");
        context.AddTriggerLog($"{damage.Target.DisplayName}攻击伤害判定成功：{chancePercent}% → 本次伤害免疫。");
    }

    internal static int GetCurrentChancePercent(Player owner)
    {
        if (owner.RuntimeStates.TryGetValue(DodgeChanceKey, out var stored)
            && stored is int chance)
        {
            return Math.Clamp(chance, InitialDodgeChancePercent, MaximumDodgeChancePercent);
        }

        return InitialDodgeChancePercent;
    }

    internal static void IncreaseChanceOnHit(BattleContext context, Player owner)
    {
        var before = GetCurrentChancePercent(owner);
        var after = Math.Min(MaximumDodgeChancePercent, before + DodgeChanceIncreasePerHit);
        owner.RuntimeStates[DodgeChanceKey] = after;

        if (after == before)
        {
            context.AddTriggerLog($"[Equipment/仙毫] {owner.DisplayName}命中攻击，免伤概率已达上限{MaximumDodgeChancePercent}%。");
            return;
        }

        context.RoundResult.AddLine($"仙毫：{owner.DisplayName}攻击命中，免伤概率{before}%→{after}%。");
        context.AddTriggerLog("[Equipment/仙毫]");
        context.AddTriggerLog($"{owner.DisplayName}攻击命中：免伤概率{before}%→{after}%。");
    }

    internal static bool HasXianHao(BattleContext context, Player owner)
    {
        return owner == context.Player
            ? GameManager.HasActiveEquipment(EquipmentIds.XianHao)
            : owner is EnemyInstance enemy && enemy.HasEquipment(EquipmentIds.XianHao);
    }
}

public sealed class XianHaoHitChanceEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Lowest;

    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null
            || damage.Cancelled
            || !damage.IsDirectAttackDamage
            || damage.ActualDamageDealt <= 0
            || !XianHaoDodgeEffect.HasXianHao(context, damage.Source))
        {
            return;
        }

        XianHaoDodgeEffect.IncreaseChanceOnHit(context, damage.Source);
    }
}
