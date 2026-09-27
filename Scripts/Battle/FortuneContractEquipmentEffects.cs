//////////////////////////////////////////////////////////
// 文件：Scripts/Battle/FortuneContractEquipmentEffects.cs
//
// 模块：Battle System
//
// 职责：
// 1. 承载初始事件㉑【财富契约】三件装备的战斗效果：
//    大亨之铠（受伤计数掉金币）/ 投机者之刃（杀加伤+杀命中计数得金币）/
//    收割者（斩杀线处决+斩杀得金币）。
// 2. 提供可复用的斩杀线判定 ExecutionRules，供以后其它斩杀类装备/技能复用。
//
// 不负责：
// × 修改死亡流程——收割者只抬高伤害数值，死亡仍走正常 OnDying/OnDeath/奖励结算。
// × 直接改金币变量——统一走 GameManager.AddGold。
//////////////////////////////////////////////////////////

/// <summary>
/// 斩杀线判定：目标当前生命值 ≤ 最大生命值 × 阈值 时可被处决。
/// 以后新增斩杀类装备/技能直接复用这里，不要各自写死 if(CurrentHp<=10%)。
/// </summary>
public static class ExecutionRules
{
    /// <summary>收割者的斩杀线（最大生命值百分比）。</summary>
    public const double ReaperExecutionThreshold = 0.10;

    /// <summary>
    /// 目标是否处于斩杀线内（存活且当前生命 ≤ 最大生命 × threshold）。
    /// </summary>
    public static bool IsExecutable(Player target, double threshold)
    {
        return target.Health > 0
            && target.Health <= System.Math.Ceiling(target.MaxHealth * threshold);
    }
}

// ── 大亨之铠：每场战斗第1/5/10次受到实际伤害时 +50金币 ─────────────────────
// "实际伤害"以 ActualDamageDealt > 0 判定：被闪避/格挡/护盾完全抵消/无敌（Cancelled
// 或实际伤害0）不计入。计数存在玩家 RuntimeStates，每场战斗自动重置。
/// <summary>
/// Battle System 的公开类：TycoonArmorGoldOnDamagedEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class TycoonArmorGoldOnDamagedEffect : IBattleEffect
{
    private const string CountKey = "tycoon_armor_damaged_count";

    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Battle System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.ActualDamageDealt <= 0) return;
        if (damage.Target != context.Player) return;
        if (!GameManager.HasEquipment(EquipmentIds.TycoonArmor)) return;

        var count = context.Player.RuntimeStates.TryGetValue(CountKey, out var v) ? (int)v : 0;
        count += 1;
        context.Player.RuntimeStates[CountKey] = count;

        if (count is 1 or 5 or 10)
        {
            GameManager.AddGold(50);
            context.RoundResult.AddLine($"大亨之铠：第{count}次受到伤害，掉落50金币！");
            context.AddTriggerLog($"[大亨之铠] 第{count}次受伤 → +50金币。");
        }
    }
}

// ── 投机者之刃（加伤）：所有杀类型牌伤害+2 ─────────────────────────────────
// 杀类型判定统一走 BattleRules.IsShaAttack（杀/火杀/雷杀/冰杀/必中杀/影袭杀，
// 以及以后新增的所有杀系 AttackType），不逐个枚举卡牌。
/// <summary>
/// Battle System 的公开类：SpeculatorBladeDamageEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class SpeculatorBladeDamageEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Battle System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled) return;
        // 蜀·兵谋同源生效时，杀类型牌与攻击性锦囊牌互为兼容标签——此处切到兼容感知版判定，
        // 演示"杀类型伤害加成"这一类效果如何在不改变原逻辑的前提下接入兼容标签。
        if (!BattleRules.IsShaAttackWithFactionCompat(damage.AttackType)) return;
        if (damage.Source != context.Player) return;
        if (!GameManager.HasEquipment(EquipmentIds.SpeculatorBlade)) return;

        damage.AddModifier(new DamageModifier(
            "投机者之刃",
            DamageModifierPriority.FlatBonus,
            DamageModifierOperation.Add,
            2));
        context.AddTriggerLog("[投机者之刃] 杀 +2。");
    }
}

// ── 投机者之刃（得金币）：每场战斗第1/5/10次杀类型牌"使用"时 +30金币 ────────
// 计数的是"杀类型牌被使用的次数"，不是"命中/造成伤害的次数"：闪抵挡、未命中、
// 无懈/免疫导致0伤害、双杀互消/被克制（这些分支根本不会产生 DamageEvent），
// 都仍然算一次合法使用。因此不能挂在 OnDamageTaken/依赖 ActualDamageDealt，
// 而是挂在 OnBattlePhase，读取本回合玩家已提交（费用已扣、非UI预览）的
// context.PlayerAction——与 HeroUnlockKillCardTrackingEffect 使用同一时机、
// 同一个 BattleRules.IsShaAttack 判定。连弩一回合内堆叠出牌时 action.Count
// 就是这一次行动里实际使用的杀张数，逐张计数以支持"一回合内多次触发"。
/// <summary>
/// Battle System 的公开类：SpeculatorBladeGoldOnHitEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class SpeculatorBladeGoldOnHitEffect : IBattleEffect
{
    private const string CountKey = "speculator_blade_use_count";

    public TriggerTiming Timing => TriggerTiming.OnBattlePhase;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Battle System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var action = context.PlayerAction;
        if (action == null || !BattleRules.IsShaAttack(action.Type)) return;
        if (!GameManager.HasEquipment(EquipmentIds.SpeculatorBlade)) return;

        var count = context.Player.RuntimeStates.TryGetValue(CountKey, out var v) ? (int)v : 0;
        var uses = System.Math.Max(1, action.Count);
        for (var i = 0; i < uses; i++)
        {
            count += 1;

            if (count is 1 or 5 or 10)
            {
                GameManager.AddGold(30);
                context.RoundResult.AddLine($"投机者之刃：第{count}次杀使用，获得30金币！");
                context.AddTriggerLog($"[投机者之刃] 第{count}次杀使用 → +30金币。");
            }
        }
        context.Player.RuntimeStates[CountKey] = count;
    }
}

// ── 收割者（处决）：敌人生命≤斩杀线时，玩家下一次伤害直接斩杀 ────────────────
// 在最终结算区把伤害提高至目标的剩余生命值，确保减伤后仍可处决；不制造“当前生命+
// 最大生命”的伪超额伤害。死亡流程/OnDeath/掉落/奖励仍全部照常。
/// <summary>
/// Battle System 的公开类：ReaperExecutionEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ReaperExecutionEffect : IBattleEffect
{
    internal const string ExecutePendingKey = "reaper_execute_pending";

    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Battle System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled) return;
        if (damage.Source != context.Player || damage.Target is not EnemyInstance enemy) return;
        if (!GameManager.HasEquipment(EquipmentIds.Reaper)) return;
        if (!ExecutionRules.IsExecutable(enemy, ExecutionRules.ReaperExecutionThreshold)) return;

        damage.AddModifier(new DamageModifier(
            "收割者·斩杀",
            DamageModifierPriority.DamageCap,
            DamageModifierOperation.Minimum,
            enemy.Health));
        enemy.RuntimeStates[ExecutePendingKey] = true;
        context.RoundResult.AddLine($"收割者：{enemy.DisplayName}进入斩杀线，直接斩杀！");
        context.AddTriggerLog($"[收割者] {enemy.DisplayName} 生命≤{ExecutionRules.ReaperExecutionThreshold:P0}，处决。");
    }
}

// ── 收割者（得金币）：处决实际致死后 +25金币 ────────────────────────────────
/// <summary>
/// Battle System 的公开类：ReaperGoldOnExecuteEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ReaperGoldOnExecuteEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Battle System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Target is not EnemyInstance enemy) return;
        if (!enemy.RuntimeStates.TryGetValue(ReaperExecutionEffect.ExecutePendingKey, out var pending) || pending is not true) return;

        enemy.RuntimeStates.Remove(ReaperExecutionEffect.ExecutePendingKey);
        if (enemy.Health > 0) return;

        GameManager.AddGold(25);
        context.RoundResult.AddLine("收割者：斩杀成功，获得25金币！");
        context.AddTriggerLog("[收割者] 斩杀成功 → +25金币。");
    }
}
