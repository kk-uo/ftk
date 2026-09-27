//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/ClampExoskeletonEquipment.cs
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

// 钳制机械外骨骼（传奇）：
// 触发条件：生命值首次跌至最大生命值的50%及以下（且仍存活）。
// 触发效果：对敌方使用顺手牵羊 + 自身获得酒BUFF×1 + 进入修复待机。
// 修复效果：下一个回合开始时恢复至满生命值。
// 每场战斗仅触发一次。
/// <summary>
/// Equipment System 的公开类：ClampExoskeletonTriggerEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ClampExoskeletonTriggerEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    // Low：在 ApplyDamageEffect（Mid）之后、DamageTakenEffect（Low）之前执行（注册顺序靠前）。
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Amount <= 0)
        {
            return;
        }

        CheckAndTrigger(context, damage.Target, damage.Amount);
    }

    private static void CheckAndTrigger(BattleContext context, Player target, int damageDealt)
    {
        if (target.Health <= 0)
        {
            return;
        }

        var threshold = target.MaxHealth / 2;
        var hpBefore = target.Health + damageDealt;
        if (hpBefore <= threshold || target.Health > threshold)
        {
            return;
        }

        if (target == context.Player)
        {
            if (!GameManager.HasEquipment(EquipmentIds.ClampExoskeleton) || !context.Player.ClampExoskeletonActive)
            {
                return;
            }

            context.Player.ConsumeClampExoskeleton();
            context.RoundResult.AddLine($"钳制机械外骨骼触发（{target.DisplayName}）：生命跌至 {target.Health}/{target.MaxHealth}（≤50%）。");
            ExecuteStealFromFirstAliveEnemy(context, context.Player);
            context.Player.QueueWinePower(1);
            context.RoundResult.AddLine($"{target.DisplayName}获得酒BUFF×1，进入修复状态（下回合恢复至满血）。");
            context.AddTriggerLog("[Equipment/ClampExoskeleton]");
            context.AddTriggerLog($"触发：{target.DisplayName} HP={target.Health}/{target.MaxHealth}，顺手牵羊+酒BUFF+修复待机。");
        }
        else if (target is EnemyInstance enemyTarget)
        {
            if (!enemyTarget.HasEquipment(EquipmentIds.ClampExoskeleton))
            {
                return;
            }

            if (!enemyTarget.RuntimeStates.TryGetValue("clamp_exo_active", out var activeVal) || activeVal is not true)
            {
                return;
            }

            enemyTarget.RuntimeStates["clamp_exo_active"] = false;
            enemyTarget.RuntimeStates["clamp_exo_repair_pending"] = true;
            context.RoundResult.AddLine($"钳制机械外骨骼触发（{target.DisplayName}）：生命跌至 {target.Health}/{target.MaxHealth}（≤50%）。");
            ExecuteStealFromPlayer(context, enemyTarget);
            enemyTarget.QueueWinePower(1);
            context.RoundResult.AddLine($"{target.DisplayName}获得酒BUFF×1，进入修复状态（下回合恢复至满血）。");
            context.AddTriggerLog("[Equipment/ClampExoskeleton]");
            context.AddTriggerLog($"触发：{target.DisplayName} HP={target.Health}/{target.MaxHealth}，顺手牵羊+酒BUFF+修复待机。");
        }
    }

    private static void ExecuteStealFromPlayer(BattleContext context, EnemyInstance thief)
    {
        if (context.Player.InShadowState)
        {
            context.RoundResult.AddLine($"顺手牵羊：{context.Player.DisplayName}处于影袭状态，无法被顺手牵羊。");
            context.AddTriggerLog("[影袭]");
            context.AddTriggerLog($"{context.Player.DisplayName}影袭状态：钳制机械外骨骼顺手牵羊无效。");
            return;
        }

        var stealAmount = context.Player.GetStealableMana();
        if (stealAmount <= 0)
        {
            context.RoundResult.AddLine($"顺手牵羊：{context.Player.DisplayName}无可偷取的费用。");
            return;
        }

        BattleRules.PayManaAndRaiseResourceChanged(context, context.Player, stealAmount, false);
        thief.GainProtectedStealMana(stealAmount);
        context.RoundResult.AddSteal(thief, stealAmount);
    }

    private static void ExecuteStealFromFirstAliveEnemy(BattleContext context, Player thief)
    {
        var target = context.GetFirstAliveEnemy();
        if (target == null)
        {
            context.RoundResult.AddLine("顺手牵羊：无存活敌人。");
            return;
        }

        if (target.InShadowState)
        {
            context.RoundResult.AddLine($"顺手牵羊：{target.DisplayName}处于影袭状态，无法被顺手牵羊。");
            context.AddTriggerLog("[影袭]");
            context.AddTriggerLog($"{target.DisplayName}影袭状态：钳制机械外骨骼顺手牵羊无效。");
            return;
        }

        var stealAmount = target.GetStealableMana();
        if (stealAmount <= 0)
        {
            context.RoundResult.AddLine($"顺手牵羊：{target.DisplayName}无可偷取的费用。");
            return;
        }

        BattleRules.PayManaAndRaiseResourceChanged(context, target, stealAmount, false);
        thief.GainProtectedStealMana(stealAmount);
        context.RoundResult.AddSteal(thief, stealAmount);
    }
}

// 钳制机械外骨骼修复效果：回合开始时检查修复待机标记，有则恢复至满生命值。
/// <summary>
/// Equipment System 的公开类：ClampExoskeletonRepairEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ClampExoskeletonRepairEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        // 玩家侧
        if (GameManager.HasEquipment(EquipmentIds.ClampExoskeleton) && context.Player.ClampExoskeletonRepairPending)
        {
            context.Player.ClearClampRepairPending();
            var heal = context.Player.MaxHealth - context.Player.Health;
            if (heal > 0)
            {
                var healed = BattleHealing.Apply(
                    context,
                    context.Player,
                    heal,
                    false,
                    new HealthChangeSource(HealthChangeSourceKind.Equipment, "钳制机械外骨骼", EquipmentIds.ClampExoskeleton, context.Player)).HealedAmount;
                context.RoundResult.AddLine($"钳制机械外骨骼修复：{context.Player.DisplayName}恢复至满生命值（+{healed}）。");
                context.AddTriggerLog("[Equipment/ClampExoskeleton]");
                context.AddTriggerLog($"修复：{context.Player.DisplayName} +{healed} HP，满血。");
            }
        }

        // 敌方侧
        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            if (enemy.IsDead || !enemy.HasEquipment(EquipmentIds.ClampExoskeleton))
            {
                continue;
            }

            if (!enemy.RuntimeStates.TryGetValue("clamp_exo_repair_pending", out var pendingVal) || pendingVal is not true)
            {
                continue;
            }

            enemy.RuntimeStates["clamp_exo_repair_pending"] = false;
            var heal = enemy.MaxHealth - enemy.Health;
            if (heal > 0)
            {
                var healed = BattleHealing.Apply(
                    context,
                    enemy,
                    heal,
                    false,
                    new HealthChangeSource(HealthChangeSourceKind.Equipment, "钳制机械外骨骼", EquipmentIds.ClampExoskeleton, enemy)).HealedAmount;
                context.RoundResult.AddLine($"钳制机械外骨骼修复：{enemy.DisplayName}恢复至满生命值（+{healed}）。");
                context.AddTriggerLog("[Equipment/ClampExoskeleton]");
                context.AddTriggerLog($"修复：{enemy.DisplayName} +{healed} HP，满血。");
            }
        }
    }
}
