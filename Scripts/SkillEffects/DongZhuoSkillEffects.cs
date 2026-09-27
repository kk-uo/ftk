//////////////////////////////////////////////////////////
// 文件：Scripts/SkillEffects/DongZhuoSkillEffects.cs
//
// 模块：Skill Effect System
//
// 职责：
// 1. 承载技能效果实现与 Trigger 接入相关代码。
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

// 崩坏（董卓专属·普通）：
// 从第2回合开始，每回合开始时：当前HP > MaxHP/4 → 失去20点生命；
// 否则 → 恢复 floor(MaxHP/16) 点生命（上限MaxHP）。每回合都重新判定，不是只触发一次；
// 玩家与敌方单位均生效（若持有此技能）。
//
// 使用 OnTurnStart 是为了让每回合的数值变化进入正常回合刷新与表现阶段。生命减少使用
// LoseHealth 而不是 TakeDamage，因为【崩坏】是自身生命损失，不应被第一回合免伤、护盾
// 或受伤后效果拦截。
/// <summary>
/// Skill Effect System 的公开类：DongZhuoBengHuaiEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class DongZhuoBengHuaiEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        Apply(context, context.Player);
        var enemies = context.Encounter?.Enemies;
        if (enemies == null) return;
        foreach (var enemy in enemies)
            Apply(context, enemy);
    }

    private static void Apply(BattleContext context, Player unit)
    {
        if (context.TurnCounter < 2
            || unit.IsDead
            || !unit.HasSkill(SkillIds.DongZhuoBengHuai))
        {
            return;
        }

        var threshold = unit.MaxHealth / 4;
        if (unit.Health > threshold)
        {
            var previousHealth = unit.Health;
            unit.LoseHealth(20);
            context.RecordDirectDamage(unit, 20, System.Math.Max(0, previousHealth - unit.Health), previousHealth, unit.Health,
                new HealthChangeSource(HealthChangeSourceKind.Skill, "崩坏", SkillIds.DongZhuoBengHuai, unit));
            context.RoundResult.AddLine($"{unit.DisplayName}【崩坏】生效：生命高于25%，失去20点生命。");
            context.AddTriggerLog("[崩坏]");
            context.AddTriggerLog($"{unit.DisplayName} HP {previousHealth} → {unit.Health}（高于{threshold}阈值）");
            context.ReportPlayerCharacterSkillTriggered(
                unit, SkillIds.DongZhuoBengHuai, TriggerTiming.OnTurnStart, EffectPriority.Low, "health_loss");

            if (unit.Health <= 0 && !unit.IsDead && !context.GameOver)
            {
                var savedDamage = context.DamageEvent;
                context.DamageEvent = new DamageEvent(unit, unit, CardType.Fee, 20,
                    origin: new HealthChangeSource(HealthChangeSourceKind.Skill, "崩坏", SkillIds.DongZhuoBengHuai, unit));
                context.RaiseOnDying();
                context.DamageEvent = savedDamage;
            }
        }
        else
        {
            var healAmount = unit.MaxHealth / 16;
            var healed = BattleHealing.Apply(
                context,
                unit,
                healAmount,
                false,
                new HealthChangeSource(HealthChangeSourceKind.Skill, "崩坏", SkillIds.DongZhuoBengHuai, unit)).HealedAmount;
            if (healed > 0)
            {
                context.RoundResult.AddLine($"{unit.DisplayName}【崩坏】反转：生命低于或等于25%，恢复{healed}点生命。");
                context.AddTriggerLog("[崩坏]");
                context.AddTriggerLog($"{unit.DisplayName} HP +{healed}（低于{threshold}阈值，回复{healAmount}）");
                context.ReportPlayerCharacterSkillTriggered(
                    unit, SkillIds.DongZhuoBengHuai, TriggerTiming.OnTurnStart, EffectPriority.Low, "heal");
            }
        }
    }
}
