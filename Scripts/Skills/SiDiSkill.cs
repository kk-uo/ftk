//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/SiDiSkill.cs
//
// 模块：Skill System
//
// 职责：
// 1. 承载角色技能、Boss 技能与技能触发效果相关代码。
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

// 司敌（曹真专属）
// 亮牌后、正常战斗结算前，只比较玩家当前锁定敌人的行动；其它敌人不参与判定。
// 若玩家与该敌人出了同类牌，则触发以下效果：
//   费 vs 费         → 玩家额外获得1点费用。
//   攻击牌 vs 攻击牌 → 敌方攻击无效（视为出null，不扣费）。
//   桃 vs 桃         → 敌方桃失效，玩家桃治疗量翻倍。
//   酒 vs 酒         → 敌方酒失效，玩家酒增伤翻倍。
//   闪 vs 闪         → 玩家额外获得1点费用。
//   无懈 vs 无懈     → 玩家额外获得1点费用。
//   顺手牵羊 vs 顺手牵羊 → 敌方顺手牵羊失效，玩家正常生效。
//
// 桃/酒翻倍：通过 RuntimeState "sidi_double_peach" / "sidi_double_wine" 标记，
// 在 BattleResolver.ApplyResourceAndHealing / PrepareDefenseLayers 中消费。
/// <summary>
/// Skill System 的公开类：SiDiEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class SiDiEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBattlePhase;
    public EffectPriority Priority => EffectPriority.Highest;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (!context.Player.HasSkill(SkillIds.SiDi)) return;
        var playerAction = context.PlayerAction;
        if (playerAction == null || context.PlayerActionCancelled || context.GameOver) return;

        var entry = context.GetPlayerLockedEnemyActionEntry();
        if (entry == null || entry.Enemy.IsDead || entry.ActionCancelled)
        {
            return;
        }

        var enemyAction = entry.Action;

        // 费 vs 费 → 玩家额外+1费
        if (playerAction.IsFee && enemyAction.IsFee)
        {
            context.Player.GainMana();
            context.RoundResult.AddLine($"【司敌】：当前锁定的{entry.Enemy.DisplayName}同样出【费】，你额外获得1点费用。");
            context.AddTriggerLog($"[Skill/SiDi] 锁定目标={entry.Enemy.DisplayName}，费匹配 → 玩家+1费");
            Report(context, "fee");
            return;
        }

        // 攻击牌 vs 攻击牌 → 锁定敌人的攻击无效
        if (playerAction.IsAttack && enemyAction.IsAttack)
        {
            entry.ActionCancelled = true;
            context.RoundResult.AddLine($"【司敌】：当前锁定的{entry.Enemy.DisplayName}同样出攻击牌，其攻击无效。");
            context.AddTriggerLog($"[Skill/SiDi] 锁定目标={entry.Enemy.DisplayName}，攻击匹配 → 目标敌人攻击取消");
            Report(context, "attack");
            return;
        }

        // 桃 vs 桃 → 锁定敌人的桃失效，玩家桃翻倍
        if (BattleRules.ShouldApplyPeachEffect(context.Player, playerAction.Type) && enemyAction.IsPeach)
        {
            entry.ActionCancelled = true;
            context.Player.RuntimeStates["sidi_double_peach"] = true;
            context.RoundResult.AddLine($"【司敌】：当前锁定的{entry.Enemy.DisplayName}同样出【桃】，你获得双倍桃，对方桃失效。");
            context.AddTriggerLog($"[Skill/SiDi] 锁定目标={entry.Enemy.DisplayName}，桃匹配 → 双倍桃 + 目标敌人桃取消");
            Report(context, "peach");
            return;
        }

        // 酒 vs 酒 → 锁定敌人的酒失效，玩家酒翻倍
        if (BattleRules.ShouldApplyWineEffect(context.Player, playerAction.Type) && enemyAction.IsWine)
        {
            entry.ActionCancelled = true;
            context.Player.RuntimeStates["sidi_double_wine"] = true;
            context.RoundResult.AddLine($"【司敌】：当前锁定的{entry.Enemy.DisplayName}同样出【酒】，你获得双倍酒，对方酒失效。");
            context.AddTriggerLog($"[Skill/SiDi] 锁定目标={entry.Enemy.DisplayName}，酒匹配 → 双倍酒 + 目标敌人酒取消");
            Report(context, "wine");
            return;
        }

        // 闪 vs 闪 → 玩家额外+1费
        if (playerAction.IsDodge && enemyAction.IsDodge)
        {
            context.Player.GainMana();
            context.RoundResult.AddLine($"【司敌】：当前锁定的{entry.Enemy.DisplayName}同样出【闪】，你额外获得1点费用。");
            context.AddTriggerLog($"[Skill/SiDi] 锁定目标={entry.Enemy.DisplayName}，闪匹配 → 玩家+1费");
            Report(context, "dodge");
            return;
        }

        // 无懈可击 vs 无懈可击 → 玩家额外+1费
        if (playerAction.IsUnassailable && enemyAction.IsUnassailable)
        {
            context.Player.GainMana();
            context.RoundResult.AddLine($"【司敌】：当前锁定的{entry.Enemy.DisplayName}同样出【无懈可击】，你额外获得1点费用。");
            context.AddTriggerLog($"[Skill/SiDi] 锁定目标={entry.Enemy.DisplayName}，无懈匹配 → 玩家+1费");
            Report(context, "counter");
            return;
        }

        // 顺手牵羊 vs 顺手牵羊 → 锁定敌人的牌失效，玩家正常生效
        if (playerAction.IsSteal && enemyAction.IsSteal)
        {
            entry.ActionCancelled = true;
            context.RoundResult.AddLine($"【司敌】：当前锁定的{entry.Enemy.DisplayName}同样出【顺手牵羊】，对方顺手牵羊失效。");
            context.AddTriggerLog($"[Skill/SiDi] 锁定目标={entry.Enemy.DisplayName}，顺手牵羊匹配 → 目标敌人行动取消");
            Report(context, "steal");
        }
    }

    private void Report(BattleContext context, string variant)
    {
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.SiDi, Timing, Priority, variant);
    }
}
