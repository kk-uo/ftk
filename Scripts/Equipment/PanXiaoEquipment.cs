//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/PanXiaoEquipment.cs
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

// 排箫（稀有饰品）：持有方视角 ——
//   我方受到伤害 ÷1.1；敌方受到伤害 ×1.1。
// 双方均可装备；效果相对于持有方叠加。真实伤害（直接调用 TakeDamage）不经 DamageEvent，自动豁免。
/// <summary>
/// Equipment System 的公开类：PanXiaoEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class PanXiaoEffect : IBattleEffect
{
    private const double EnemyDamageTakenMultiplier = 1.1;
    private const double AllyDamageTakenMultiplier = 1.0 / 1.1;

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
        if (damage == null || damage.Cancelled)
        {
            return;
        }

        // 玩家持有排箫
        if (GameManager.HasEquipment(EquipmentIds.PanXiao))
        {
            if (damage.Target == context.Player)
            {
                damage.AddModifier(new DamageModifier(
                    "排箫（我方减伤）",
                    DamageModifierPriority.VulnerableOrReduction,
                    DamageModifierOperation.MultiplyFloat,
                    AllyDamageTakenMultiplier));
                context.RoundResult.AddLine("排箫：我方受到伤害÷1.1。");
                context.AddTriggerLog("[排箫] 玩家受伤 ÷1.1");
            }
            else
            {
                damage.AddModifier(new DamageModifier(
                    "排箫（敌方增伤）",
                    DamageModifierPriority.VulnerableOrReduction,
                    DamageModifierOperation.MultiplyFloat,
                    EnemyDamageTakenMultiplier));
                context.RoundResult.AddLine("排箫：敌方受到伤害×1.1。");
                context.AddTriggerLog("[排箫] 敌方受伤 ×1.1");
            }
        }

        // 持有排箫的敌人受到伤害 → 其自身（我方）减伤。
        if (damage.Target is EnemyInstance targetEnemy && targetEnemy.HasEquipment(EquipmentIds.PanXiao))
        {
            damage.AddModifier(new DamageModifier(
                "排箫（我方减伤）",
                DamageModifierPriority.VulnerableOrReduction,
                DamageModifierOperation.MultiplyFloat,
                AllyDamageTakenMultiplier));
            context.RoundResult.AddLine($"排箫（{targetEnemy.DisplayName}）：受到伤害÷1.1。");
            context.AddTriggerLog($"[排箫] {targetEnemy.DisplayName}受伤 ÷1.1");
        }

        // 持有排箫的敌人攻击玩家 → 玩家（敌方）受到伤害增加。
        if (damage.Source is EnemyInstance sourceEnemy
            && sourceEnemy.HasEquipment(EquipmentIds.PanXiao)
            && damage.Target == context.Player)
        {
            damage.AddModifier(new DamageModifier(
                "排箫（敌方增伤）",
                DamageModifierPriority.VulnerableOrReduction,
                DamageModifierOperation.MultiplyFloat,
                EnemyDamageTakenMultiplier));
            context.RoundResult.AddLine($"排箫（{sourceEnemy.DisplayName}）：我方受到伤害×1.1。");
            context.AddTriggerLog($"[排箫] {sourceEnemy.DisplayName}攻击 → 玩家受伤 ×1.1");
        }
    }
}
