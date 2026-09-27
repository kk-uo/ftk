//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/HeavyHammerEquipment.cs
//
// 重锤：以八回合为一个独立周期记录装备者对敌方造成的实际伤害；
// 每个周期结束时，对目标造成该周期总伤害的一半，然后清空并开始下一个周期。
//////////////////////////////////////////////////////////

/// <summary>记录重锤当前八回合周期内的实际造成伤害。</summary>
public sealed class HeavyHammerRecordEffect : IBattleEffect
{
    internal const string DamageTotalKey = "heavy_hammer_cycle_damage_total";

    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Low;

    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.ActualDamageDealt <= 0)
        {
            return;
        }

        // 只记录“装备者对敌方”的实际伤害。伤害来源不限于普通杀，
        // 但重锤自身的周期爆发走直接结算，不会回流到这个触发器。
        if (damage.Source == context.Player
            && damage.Target is EnemyInstance
            && GameManager.HasEquipment(EquipmentIds.HeavyHammer))
        {
            AddDamage(context.Player, damage.ActualDamageDealt);
            context.AddTriggerLog($"[Equipment/HeavyHammer] 玩家本周期累计伤害 {GetDamage(context.Player)}。");
            return;
        }

        if (damage.Source is EnemyInstance enemySource
            && damage.Target == context.Player
            && enemySource.HasEquipment(EquipmentIds.HeavyHammer))
        {
            AddDamage(enemySource, damage.ActualDamageDealt);
            context.AddTriggerLog($"[Equipment/HeavyHammer] {enemySource.DisplayName}本周期累计伤害 {GetDamage(enemySource)}。");
        }
    }

    internal static int GetDamage(Player owner)
    {
        return owner.RuntimeStates.TryGetValue(DamageTotalKey, out var value) && value is int total
            ? total
            : 0;
    }

    internal static void AddDamage(Player owner, int damage)
    {
        owner.RuntimeStates[DamageTotalKey] = GetDamage(owner) + damage;
    }

    internal static void ResetDamage(Player owner)
    {
        owner.RuntimeStates[DamageTotalKey] = 0;
    }
}

/// <summary>每八回合结算一次重锤的半额伤害。</summary>
public sealed class HeavyHammerBurstEffect : IBattleEffect
{
    private const int CycleLength = 8;

    public TriggerTiming Timing => TriggerTiming.OnTurnEnd;
    public EffectPriority Priority => EffectPriority.Low;

    public void Execute(BattleContext context)
    {
        if (context.GameOver || context.TurnCounter <= 0 || context.TurnCounter % CycleLength != 0)
        {
            return;
        }

        TriggerPlayerBurst(context);
        TriggerEnemyBursts(context);
    }

    private static void TriggerPlayerBurst(BattleContext context)
    {
        if (!GameManager.HasEquipment(EquipmentIds.HeavyHammer))
        {
            return;
        }

        var total = HeavyHammerRecordEffect.GetDamage(context.Player);
        HeavyHammerRecordEffect.ResetDamage(context.Player);
        var burstDamage = total / 2;
        if (burstDamage <= 0)
        {
            return;
        }

        var target = context.GetFirstAliveEnemy();
        if (target == null)
        {
            return;
        }

        ApplyBurstDamage(context, context.Player, target, burstDamage, total);
    }

    private static void TriggerEnemyBursts(BattleContext context)
    {
        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            if (enemy.IsDead || !enemy.HasEquipment(EquipmentIds.HeavyHammer))
            {
                continue;
            }

            var total = HeavyHammerRecordEffect.GetDamage(enemy);
            HeavyHammerRecordEffect.ResetDamage(enemy);
            var burstDamage = total / 2;
            if (burstDamage <= 0)
            {
                continue;
            }

            ApplyBurstDamage(context, enemy, context.Player, burstDamage, total);
            if (context.GameOver)
            {
                return;
            }
        }
    }

    private static void ApplyBurstDamage(
        BattleContext context,
        Player source,
        Player target,
        int burstDamage,
        int cycleTotal)
    {
        var healthBefore = target.Health;
        target.TakeDamage(burstDamage);
        var actualDamage = System.Math.Max(0, healthBefore - target.Health);
        context.RecordDirectDamage(
            target,
            burstDamage,
            actualDamage,
            healthBefore,
            target.Health,
            new HealthChangeSource(HealthChangeSourceKind.Equipment, "重锤", EquipmentIds.HeavyHammer, source));
        context.RoundResult.AddLine($"重锤触发：{source.DisplayName}前8回合累计造成{cycleTotal}点伤害，对{target.DisplayName}造成{burstDamage}点伤害。");
        context.AddTriggerLog($"[Equipment/HeavyHammer] {source.DisplayName}周期爆发：{target.DisplayName} -{burstDamage}（累计{cycleTotal}÷2）。");

        if (target.Health <= 0 && !target.IsDead && !context.GameOver)
        {
            var savedDamage = context.DamageEvent;
            context.DamageEvent = new DamageEvent(
                source,
                target,
                CardType.Kill,
                burstDamage,
                origin: new HealthChangeSource(HealthChangeSourceKind.Equipment, "重锤", EquipmentIds.HeavyHammer, source));
            context.RaiseOnDying();
            context.DamageEvent = savedDamage;
        }
    }
}
