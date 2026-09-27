//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/ChituEquipment.cs
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

// 赤兔（史诗·载具）：
// 1. 第一回合所有由玩家打出的杀系伤害×2（ChituFirstRoundDoubleKillEffect，OnDamage，Low）。
// 2. 第一回合正式出招时（OnBattlePhase，Low，晚于 BattlePhaseResolutionEffect 的 Mid），
//    自动视为额外打出一张普通杀，完全按正常判定表结算（ChituFirstRoundExtraKillEffect）：
//    不提前于出招阶段发动、不跳过敌方响应、不绕过闪/无懈可击/技能。
//    该额外杀由 BattlePhaseResolutionEffect.DealAttackDamage 发起，会自动触发 OnBeforeDamage 上的
//    统一防御链（桃盾/酒盾/闪/无懈可击/必中穿透等），因此天然满足"完全按正常杀处理"。
//    额外杀的 Source 就是 context.Player，因此也会被 ChituFirstRoundDoubleKillEffect
//    正常识别并计入×2，无需额外处理。
//    AI 装备赤兔时同样遵循：仅在第一回合正式出招时对玩家追加一张普通杀，不提前发动。
// 吕布专属：若当前角色为吕布且持有赤兔，【无双】倍率从×2升为×3（在 WushuangDamageEffect 中实现，不变）。
/// <summary>
/// Equipment System 的公开类：ChituFirstRoundDoubleKillEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ChituFirstRoundDoubleKillEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.TurnCounter != 1)
        {
            return;
        }

        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled)
        {
            return;
        }

        if (damage.Source != context.Player)
        {
            return;
        }

        if (!GameManager.HasEquipment(EquipmentIds.ChiTu))
        {
            return;
        }

        if (!BattleRules.IsShaAttack(damage.AttackType))
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "赤兔（首回合双倍）",
            DamageModifierPriority.VulnerableOrReduction,
            DamageModifierOperation.MultiplyFloat,
            2.0));
        context.RoundResult.AddLine("赤兔：第一回合杀系伤害×2。");
        context.AddTriggerLog("[Equipment/赤兔]");
        context.AddTriggerLog("首回合杀系伤害×2。");
    }
}

/// <summary>
/// Equipment System 的公开类：ChituFirstRoundExtraKillEffect。
///
/// 第一回合正式出招阶段（OnBattlePhase），在 BattlePhaseResolutionEffect（Mid）已经
/// 完整结算完玩家/敌方本回合真实行动之后（Low，晚于 Mid），为持有赤兔的一方额外追加
/// 一张普通杀，并让它按正常判定表与"对方本回合的行动"结算：
/// - 若对方本回合的行动不是攻击牌：额外杀视为普通单体攻击，直接 DealAttackDamage
///   （内部会自动触发闪/无懈可击/桃盾/酒盾等统一防御链，不绕过任何防御）。
/// - 若对方本回合的行动也是攻击牌：额外杀与其单独按杀系克制表判定——
///   额外杀获胜才造成一次伤害；平局或被克制则不再造成伤害（对方本回合攻击的伤害
///   已经由 BattlePhaseResolutionEffect 的主结算流程处理过一次，这里不重复计算，
///   避免同一张对方攻击牌的伤害被计算两次）。
/// 玩家侧与"若干装备了赤兔的敌方单位"均适用同一套逻辑（AI 装备赤兔时行为一致）。
/// </summary>
public sealed class ChituFirstRoundExtraKillEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBattlePhase;
    public EffectPriority Priority => EffectPriority.Low;

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

        if (context.PlayerAction != null
            && !context.PlayerActionCancelled
            && GameManager.HasEquipment(EquipmentIds.ChiTu))
        {
            var target = context.PlayerAction.Target as EnemyInstance ?? context.GetFirstAliveEnemy();
            if (target != null && !target.IsDead)
            {
                ResolveExtraKill(context, context.Player, target, context.GetActionForEnemy(target));
            }
        }

        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            if (context.GameOver || enemy.IsDead || !enemy.HasEquipment(EquipmentIds.ChiTu))
            {
                continue;
            }

            var enemyEntry = context.GetEnemyActionEntry(enemy);
            if (enemyEntry == null || enemyEntry.ActionCancelled)
            {
                continue;
            }

            ResolveExtraKill(context, enemy, context.Player, context.PlayerAction);
        }
    }

    private static void ResolveExtraKill(BattleContext context, Player attacker, Player target, BattleAction? opposingAction)
    {
        if (context.GameOver || target.IsDead)
        {
            return;
        }

        context.RoundResult.AddLine($"赤兔：{attacker.DisplayName}第一回合正式出招，自动追加一张普通杀。");
        context.AddTriggerLog("[Equipment/赤兔]");
        context.AddTriggerLog($"{attacker.DisplayName}：第一回合追加普通杀，参与正常判定。");

        if (opposingAction == null || !opposingAction.IsAttack)
        {
            BattlePhaseResolutionEffect.DealAttackDamage(context, attacker, target, CardType.Kill);
            return;
        }

        if (BattleRules.BeatsAttack(CardType.Kill, opposingAction.Type))
        {
            BattlePhaseResolutionEffect.DealAttackDamage(context, attacker, target, CardType.Kill);
            return;
        }

        // 平局或被对方克制：额外杀不造成伤害。对方本回合攻击的伤害已经在主结算
        // 流程（BattlePhaseResolutionEffect，Mid）中处理过一次，这里不重复结算。
        context.RoundResult.AddRelation($"赤兔追加的普通杀与{BattleRules.GetCardName(opposingAction.Type)}结算完毕");
    }
}
