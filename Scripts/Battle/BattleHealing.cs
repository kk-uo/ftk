//////////////////////////////////////////////////////////
// 文件：Scripts/Battle/BattleHealing.cs
//
// 战斗内统一生命恢复入口。
// 所有卡牌、技能、装备的回血都应通过此处结算，以保证【毒丹】不会遗漏。
//////////////////////////////////////////////////////////

using System;

public readonly struct BattleHealingResult
{
    public BattleHealingResult(int healedAmount, int convertedDamage, int maxHealthGain)
    {
        HealedAmount = healedAmount;
        ConvertedDamage = convertedDamage;
        MaxHealthGain = maxHealthGain;
    }

    public int HealedAmount { get; }
    public int ConvertedDamage { get; }
    public int MaxHealthGain { get; }
    public bool WasConverted => ConvertedDamage > 0 || MaxHealthGain > 0;
}

/// <summary>
/// 统一战斗治疗结算：正常治疗会记录并触发 OnHeal；【毒丹】会将治疗量转为生命损失，
/// 不触发 OnHeal，随后按转换值的 10%（最低 1）提高最大生命值。
/// </summary>
public static class BattleHealing
{
    public static BattleHealingResult Apply(
        BattleContext context,
        Player target,
        int requestedAmount,
        bool allowOverheal,
        HealthChangeSource origin,
        bool raiseOnHeal = true)
    {
        if (requestedAmount <= 0 || context.GameOver)
        {
            return new BattleHealingResult(0, 0, 0);
        }

        if (!HasPoisonDan(context, target))
        {
            var healthBefore = target.Health;
            var healed = target.Heal(requestedAmount, allowOverheal);
            if (healed <= 0)
            {
                return new BattleHealingResult(0, 0, 0);
            }

            context.RecordHealResolved(target, target, healed, healthBefore, target.Health, origin);
            context.RoundResult.AddHeal(target, healed);
            if (raiseOnHeal)
            {
                context.HealEvent = new HealEvent(target, healed);
                context.TriggerManager.RaiseTrigger(TriggerTiming.OnHeal, context);
                context.HealEvent = null;
            }
            return new BattleHealingResult(healed, 0, 0);
        }

        // “扣血”是生命损失，不是攻击伤害：不被护盾、闪避或减伤影响。
        var before = target.Health;
        target.LoseHealth(requestedAmount);
        var actualLoss = Math.Max(0, before - target.Health);
        var maxHealthGain = Math.Max(1, (int)Math.Floor(requestedAmount * 0.1));
        target.AddMaxHealthWithoutHealing(maxHealthGain);

        var poisonDanSource = new HealthChangeSource(
            HealthChangeSourceKind.Equipment,
            "毒丹",
            EquipmentIds.PoisonDan,
            target);
        context.RecordDirectDamage(target, requestedAmount, actualLoss, before, target.Health, poisonDanSource);
        context.RoundResult.AddDamage(target, actualLoss);
        context.RoundResult.AddLine($"毒丹：{target.DisplayName}的{origin.Name}回复{requestedAmount}点转为失去{actualLoss}点生命，最大生命+{maxHealthGain}。");
        context.AddTriggerLog("[Equipment/毒丹]");
        context.AddTriggerLog($"{origin.Name} 回复{requestedAmount} → 生命损失{actualLoss}，最大生命+{maxHealthGain}。");

        // 生命损失仍可进入濒死/死亡系统，但在已经处理濒死救援时不递归触发。
        if (before > 0 && target.Health <= 0 && target.DyingState != DyingState.Dying && !target.IsDead && !context.GameOver)
        {
            var previousDamage = context.DamageEvent;
            context.DamageEvent = new DamageEvent(target, target, CardType.Fee, requestedAmount, origin: poisonDanSource);
            context.RaiseOnDying();
            context.DamageEvent = previousDamage;
        }

        return new BattleHealingResult(0, actualLoss, maxHealthGain);
    }

    private static bool HasPoisonDan(BattleContext context, Player target)
    {
        if (ReferenceEquals(target, context.Player))
        {
            return GameManager.HasEquipment(EquipmentIds.PoisonDan);
        }

        return target is EnemyInstance enemy && enemy.HasEquipment(EquipmentIds.PoisonDan);
    }
}
