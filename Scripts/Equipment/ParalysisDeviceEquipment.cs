//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/ParalysisDeviceEquipment.cs
//
// 模块：Equipment System
//
// 职责：
// 1. 承载装备触发效果与装备战斗逻辑相关代码。
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

public sealed class ParalysisDeviceEffect : IBattleEffect
{
    private const int HitCount = 3;
    private const int DamagePerHitPerDevice = 5;

    public TriggerTiming Timing => TriggerTiming.OnBattlePrePhase;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.TurnCounter != 1 || context.GameOver)
        {
            return;
        }

        var deviceCount = GameManager.CountEquipment(EquipmentIds.ParalysisDevice);
        if (deviceCount <= 0 || context.Encounter == null)
        {
            return;
        }

        var damagePerHit = DamagePerHitPerDevice * deviceCount;
        var totalDamage = damagePerHit * HitCount;
        context.RoundResult.AddLine(deviceCount > 1
            ? $"瘫痪装置×{deviceCount}启动：对所有敌人各造成{HitCount}次{damagePerHit}点真实伤害（共{totalDamage}点）。"
            : $"瘫痪装置启动：对所有敌人各造成{HitCount}次{damagePerHit}点真实伤害（共{totalDamage}点）。");
        context.AddTriggerLog($"[Equipment/瘫痪装置] 开局对全体敌人造成{HitCount}次{damagePerHit}点真实伤害。");

        foreach (var enemy in context.Encounter.Enemies)
        {
            if (enemy.IsDead || context.GameOver)
            {
                continue;
            }

            for (var hitIndex = 1; hitIndex <= HitCount && !enemy.IsDead && !context.GameOver; hitIndex++)
            {
                var actualDamage = ResolveTrueDamageHit(context, enemy, damagePerHit);
                var resultText = actualDamage > 0
                    ? $"第{hitIndex}段造成{actualDamage}点真实伤害"
                    : $"第{hitIndex}段被格挡";
                context.RoundResult.AddLine($"瘫痪装置：{enemy.DisplayName}{resultText}。");
                context.AddTriggerLog($"[Equipment/瘫痪装置] {enemy.DisplayName}{resultText} → {enemy.Health}/{enemy.MaxHealth}");
            }
        }
    }

    /// <summary>
    /// True damage intentionally bypasses the ordinary damage-modifier pipeline,
    /// but every pulse still enters OnBeforeDamage. This lets one-use defenses
    /// such as Benevolent King block exactly one pulse and be exhausted before
    /// the following pulses arrive.
    /// </summary>
    private static int ResolveTrueDamageHit(BattleContext context, EnemyInstance enemy, int damage)
    {
        var previousDamage = context.DamageEvent;
        var hit = new DamageEvent(
            context.Player,
            enemy,
            CardType.LightningStrike,
            damage,
            overrideDamageType: DamageType.Mechanic,
            origin: new HealthChangeSource(HealthChangeSourceKind.Equipment, "瘫痪装置", EquipmentIds.ParalysisDevice, context.Player));
        context.DamageEvent = hit;
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);

        var healthBefore = enemy.Health;
        if (!hit.Cancelled)
        {
            enemy.TakeDamage(damage);
            hit.ActualDamageDealt = System.Math.Max(0, healthBefore - enemy.Health);
            context.RoundResult.AddDamage(enemy, hit.ActualDamageDealt);
            context.RecordDamageResolved(hit, healthBefore, enemy.Health);

            if (enemy.Health <= 0 && !enemy.IsDead && !context.GameOver)
            {
                context.RaiseOnDying();
            }
        }

        context.DamageEvent = previousDamage;
        return hit.ActualDamageDealt;
    }
}
