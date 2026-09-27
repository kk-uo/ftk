//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/PlagueStaffEquipment.cs
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

// 瘟疫权杖（传奇武器）
// 玩家：普通【杀】进化为【毒杀】，命中后立即造成10点Poison真实伤害，并附加10层【瘟疫】。
// 敌人：持有权杖时使用【杀】同样触发相同效果，对玩家施加毒素伤害与瘟疫叠加。
//
// 无限循环防护：
//   - 10点Poison伤害通过 raw TakeDamage 施加（不经过DamageEvent链），
//     故不触发任何监听DamageEvent的效果，天然防止叠加-发作-叠加的无限循环。
//   - 【瘟疫】10层发作也使用 raw TakeDamage，不会再次触发本效果。
/// <summary>
/// Equipment System 的公开类：PlagueStaffPoisonEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class PlagueStaffPoisonEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Amount <= 0) return;

        // 玩家持权杖打毒杀，或敌人持权杖打普通杀
        if (damage.Source == context.Player)
        {
            if (damage.AttackType != CardType.PoisonKill) return;
            if (!GameManager.HasEquipment(EquipmentIds.PlagueStaff)) return;
        }
        else if (damage.Source is EnemyInstance sourceEnemy)
        {
            if (damage.AttackType != CardType.Kill) return;
            if (!sourceEnemy.HasEquipment(EquipmentIds.PlagueStaff)) return;
        }
        else return;

        const int poisonDmg = 10;

        if (damage.Target is EnemyInstance targetEnemy)
        {
            if (targetEnemy.IsDead || context.GameOver) return;
            var healthBefore = targetEnemy.Health;
            targetEnemy.TakeDamage(poisonDmg);
            context.RecordDirectDamage(targetEnemy, poisonDmg, System.Math.Max(0, healthBefore - targetEnemy.Health), healthBefore, targetEnemy.Health,
                new HealthChangeSource(HealthChangeSourceKind.Equipment, "瘟疫权杖", EquipmentIds.PlagueStaff, damage.Source));
            context.RoundResult.AddLine($"瘟疫权杖：{targetEnemy.DisplayName}受到{poisonDmg}点Poison（毒素）附加伤害。");
            context.AddTriggerLog($"[Equipment/PlagueStaff] 毒杀命中 → {targetEnemy.DisplayName} -{poisonDmg}（毒素真实）");
            targetEnemy.RuntimeStates.TryGetValue("plague_stacks", out var v);
            var prev = v is int n ? n : 0;
            var next = prev + poisonDmg;
            targetEnemy.RuntimeStates["plague_stacks"] = next;
            context.RoundResult.AddLine($"瘟疫权杖：{targetEnemy.DisplayName}附加{poisonDmg}层【瘟疫】（共{next}层）。");
            context.AddTriggerLog($"[Equipment/PlagueStaff] plague_stacks: {prev}→{next}");
            if (targetEnemy.Health <= 0 && !targetEnemy.IsDead && !context.GameOver)
            {
                var savedDamage = context.DamageEvent;
                context.DamageEvent = new DamageEvent(damage.Source, targetEnemy, CardType.PoisonKill, poisonDmg, overrideDamageType: DamageType.Poison,
                    origin: new HealthChangeSource(HealthChangeSourceKind.Equipment, "瘟疫权杖", EquipmentIds.PlagueStaff, damage.Source));
                context.RaiseOnDying();
                context.DamageEvent = savedDamage;
            }
        }
        else if (damage.Target == context.Player)
        {
            if (context.GameOver) return;
            var player = context.Player;
            var before = player.Health;
            player.TakeDamage(poisonDmg);
            context.RecordDirectDamage(player, poisonDmg, System.Math.Max(0, before - player.Health), before, player.Health,
                new HealthChangeSource(HealthChangeSourceKind.Equipment, "瘟疫权杖", EquipmentIds.PlagueStaff, damage.Source));
            context.RequestPlayerDamageBorder(System.Math.Max(0, before - player.Health), player.MaxHealth);
            context.RoundResult.AddLine($"瘟疫权杖：{player.DisplayName}受到{poisonDmg}点Poison（毒素）附加伤害。");
            context.AddTriggerLog($"[Equipment/PlagueStaff] 毒杀命中 → {player.DisplayName} -{poisonDmg}（毒素真实）");
            if (player.IsCombatDebuffImmune)
            {
                context.AddTriggerLog("[泉水精华] 玩家免疫瘟疫层数。");
                return;
            }

            player.RuntimeStates.TryGetValue("plague_stacks", out var v2);
            var prev2 = v2 is int n2 ? n2 : 0;
            var next2 = prev2 + poisonDmg;
            player.RuntimeStates["plague_stacks"] = next2;
            context.RoundResult.AddLine($"瘟疫权杖：{player.DisplayName}附加{poisonDmg}层【瘟疫】（共{next2}层）。");
            context.AddTriggerLog($"[Equipment/PlagueStaff] plague_stacks: {prev2}→{next2}");
            if (player.Health <= 0 && !player.IsDead && !context.GameOver)
            {
                var savedDamage = context.DamageEvent;
                context.DamageEvent = new DamageEvent(damage.Source, player, CardType.PoisonKill, poisonDmg, overrideDamageType: DamageType.Poison,
                    origin: new HealthChangeSource(HealthChangeSourceKind.Equipment, "瘟疫权杖", EquipmentIds.PlagueStaff, damage.Source));
                context.RaiseOnDying();
                context.DamageEvent = savedDamage;
            }
        }
    }
}
