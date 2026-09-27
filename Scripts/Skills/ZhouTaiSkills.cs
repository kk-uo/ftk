//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/ZhouTaiSkills.cs
//
// 模块：Skill Effect System
//
// 职责：
// 1. 承载周泰专属被动技能【不屈】【奋激】的触发链实现。
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
//
// 设计说明：
// 与单位无关（不硬编码 context.Player 或 EnemyInstance）：任何持有【不屈】/【奋激】技能的
// 单位（玩家角色本人，或将来若有敌人也带这个技能）都能正确触发，判断始终落在
// context.DamageEvent.Target 本身身上，而不是固定检查某一方。
//////////////////////////////////////////////////////////

using System;

/// <summary>
/// Skill Effect System 的公开类：ZhouTaiBuQuTriggerEffect。
///
/// 每次生命值降至0或以下都会进行一次1D6判定；5/6成功时获得1费并回复至1点。
/// 本场战斗第一次判定失败也会保底获得1费并回复至1点；其后的失败才放行既有濒死/复活链。
///
/// 与 <see cref="ZhouTaiFenJiTriggerEffect"/> 共用 Task 2 引入的 context.CurrentDyingEventId
/// 去重机制：同一次 OnDying 事件只处理一次，避免管线内出现冗余 raise 时重复触发。
/// </summary>
public sealed class ZhouTaiBuQuTriggerEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDying;
    // 必须晚于【奋激】（Immediate）执行，但仍需抢在 DyingEffect（Low）之前把生命值治回1点，
    // 从而让 DyingEffect 的 target.Health > 0 守卫直接短路。
    public EffectPriority Priority => EffectPriority.Highest;

    private const string FirstFailureRescueUsedKey = "zhoutai_buqu_first_failure_rescue_used";
    private const string RollCountKey = "zhoutai_buqu_roll_count";
    private const string LastDyingEventIdKey = "zhoutai_buqu_last_dying_event_id";

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var target = context.DamageEvent?.Target;
        if (target == null || target.Health > 0 || context.GameOver)
        {
            return;
        }

        if (!target.HasSkill(SkillIds.ZhouTaiBuQu))
        {
            return;
        }

        // 同一次 OnDying 事件（同一个 context.CurrentDyingEventId）只处理一次。
        var dyingEventId = context.CurrentDyingEventId;
        if (dyingEventId != null
            && target.RuntimeStates.TryGetValue(LastDyingEventIdKey, out var lastId)
            && lastId is string lastIdStr
            && lastIdStr == dyingEventId)
        {
            return;
        }

        if (dyingEventId != null)
        {
            target.RuntimeStates[LastDyingEventIdKey] = dyingEventId;
        }

        // 每次致命伤均进行1D6判定，5/6成功。开发者面板可通过 ForcedZhouTaiDiceResult
        // 强制指定下一次结果，用于测试；消费后立即清空，不影响后续正常随机。
        var forced = DeveloperDebugPanel.ForcedZhouTaiDiceResult;
        int result;
        if (forced.HasValue)
        {
            result = Math.Clamp(forced.Value, 1, 6);
            DeveloperDebugPanel.ForcedZhouTaiDiceResult = null;
        }
        else
        {
            result = GameManager.EventRewardRandom.Next(1, 7);
        }

        var rollCount = target.RuntimeStates.TryGetValue(RollCountKey, out var countObj) && countObj is int count ? count : 0;
        rollCount += 1;
        target.RuntimeStates[RollCountKey] = rollCount;

        var rollSucceeded = result >= 5;
        var firstFailureRescueAvailable = !target.RuntimeStates.ContainsKey(FirstFailureRescueUsedKey);
        var rescuedByFirstFailure = !rollSucceeded && firstFailureRescueAvailable;
        if (rescuedByFirstFailure)
        {
            target.RuntimeStates[FirstFailureRescueUsedKey] = true;
        }

        if (rollSucceeded || rescuedByFirstFailure)
        {
            target.GainMana(1);
            target.DebugSetHealth(1);
            var reason = rollSucceeded
                ? "判定成功"
                : "本场首次判定失败，保底触发";
            context.RoundResult.AddLine($"{target.DisplayName}【不屈】第{rollCount}次判定：掷出{result}点，{reason}，获得1费，生命值回复至1点。");
            context.AddTriggerLog("[不屈]");
            context.AddTriggerLog($"Trigger: OnDying / Highest（1D6={result}，{reason}）");
            context.AddTriggerLog($"{target.DisplayName}：生命→1，费用+1。");
        }
        else
        {
            context.RoundResult.AddLine($"{target.DisplayName}【不屈】第{rollCount}次判定：掷出{result}点，判定失败，濒死流程继续。");
            context.AddTriggerLog("[不屈]");
            context.AddTriggerLog($"Trigger: OnDying / Highest（1D6={result}，失败）");
        }

        // 无论成功失败都记录待播放的骰子动画请求；BattleManager 会在本回合同步触发链结束后
        // 异步播放一次骰子动画表现，播放完毕后清空。
        context.PendingDiceRollRequest = (result, rollSucceeded);
        context.ReportPlayerCharacterSkillTriggered(
            target,
            SkillIds.ZhouTaiBuQu,
            Timing,
            Priority,
            rollSucceeded ? "success" : rescuedByFirstFailure ? "first_failure_rescue" : "failed");
    }
}

/// <summary>
/// Skill Effect System 的公开类：ZhouTaiFenJiTriggerEffect。
///
/// 每当持有【奋激】的单位进入一次 OnDying 结算（不论后续是否被【不屈】或其它效果救回），
/// 立即为 GameManager 的攻击性锦囊伤害固定加值永久+2（本Run有效）。必须先于【不屈】执行，
/// 因为其触发条件只看"是否发生了这次 OnDying"，与后续救援结果无关。
/// </summary>
public sealed class ZhouTaiFenJiTriggerEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDying;
    // Immediate 是 EffectPriority 里排序最靠前（最先执行）的档位，用来确保【奋激】的记录
    // 一定发生在【不屈】（Highest）判断"是否救回"之前，语义上与"是否救回"完全解耦。
    public EffectPriority Priority => EffectPriority.Immediate;

    private const string LastDyingEventIdKey = "zhoutai_fenji_last_dying_event_id";
    private const int BonusPerTrigger = 2;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var target = context.DamageEvent?.Target;
        if (target == null || target.Health > 0 || !target.HasSkill(SkillIds.ZhouTaiFenJi))
        {
            return;
        }

        var dyingEventId = context.CurrentDyingEventId;
        if (dyingEventId != null
            && target.RuntimeStates.TryGetValue(LastDyingEventIdKey, out var lastId)
            && lastId is string lastIdStr
            && lastIdStr == dyingEventId)
        {
            return;
        }

        if (dyingEventId != null)
        {
            target.RuntimeStates[LastDyingEventIdKey] = dyingEventId;
        }

        GameManager.AddZhouTaiFenjiBonus(BonusPerTrigger);
        context.RoundResult.AddLine($"{target.DisplayName}【奋激】触发：攻击性锦囊伤害永久+{BonusPerTrigger}，当前共+{GameManager.ZhouTaiFenjiBonus}。");
        context.AddTriggerLog("[奋激]");
        context.AddTriggerLog("Trigger: OnDying / Immediate");
        context.AddTriggerLog($"攻击性锦囊伤害永久加值 → {GameManager.ZhouTaiFenjiBonus}");
        context.ReportPlayerCharacterSkillTriggered(
            target, SkillIds.ZhouTaiFenJi, Timing, Priority, "damage_bonus");
    }
}

/// <summary>
/// Skill Effect System 的公开类：ZhouTaiBuQuEffect。
///
/// ISkillEffect 注册存根：实际战斗逻辑由全局注册的 IBattleEffect 承载，这里不需要
/// 再挂任何 per-owner 的触发逻辑（与如影随行/影袭的既有约定一致）。
/// </summary>
public sealed class ZhouTaiBuQuEffect : ISkillEffect
{
    public string SkillId => SkillIds.ZhouTaiBuQu;

    /// <summary>
    /// Skill Effect System 的公开入口：Register。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Register(TriggerManager triggerManager, Player owner) { }
}
