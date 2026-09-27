//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/CurseBladeEquipment.cs
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

public sealed class CurseBladeBattleStartEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBattlePrePhase;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.TurnNumber != 1 || !GameManager.HasEquipment(EquipmentIds.CurseBlade))
        {
            return;
        }

        RunBuffManager.AddStacks(RunBuffIds.Curse, 2);
        // 若本次（受×5影响后的）叠层跨过44层，Run Buff 会追加【煞气缠身】。
        // 它发生在已建立的战斗内，因此同步把当前战斗生命也强制设为1。
        if (RunBuffManager.CountStacks(RunBuffIds.ShaQiChenShen) > 0)
        {
            context.Player.SetCurrentHealth(1);
        }
        context.RoundResult.AddLine("诅咒之刃生效：本场战斗开始时获得2层诅咒。");
        context.AddTriggerLog("[诅咒之刃]");
        context.AddTriggerLog("战斗开始时获得2层诅咒（受诅咒之刃倍率影响）。");
    }
}

/// <summary>
/// Equipment System 的公开类：CurseBladeDamageBonusEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class CurseBladeDamageBonusEffect : IBattleEffect
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
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Source != context.Player)
        {
            return;
        }

        if (!GameManager.HasEquipment(EquipmentIds.CurseBlade))
        {
            return;
        }

        var curseStacks = RunBuffManager.CountStacks(RunBuffIds.Curse);
        var multiplierSteps = curseStacks / 5;
        if (multiplierSteps <= 0)
        {
            return;
        }

        var multiplier = 1d + multiplierSteps * 0.1d;
        damage.AddModifier(new DamageModifier(
            "诅咒之刃",
            DamageModifierPriority.SpecialMultiplier,
            DamageModifierOperation.MultiplyFloat,
            multiplier));
        context.RoundResult.AddLine($"诅咒之刃生效：{curseStacks}层诅咒使你的伤害×{multiplier:0.##}。");
        context.AddTriggerLog("[诅咒之刃]");
        context.AddTriggerLog($"当前诅咒 {curseStacks} 层，伤害倍率 ×{multiplier:0.##}。");
    }
}
