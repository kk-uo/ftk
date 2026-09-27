//////////////////////////////////////////////////////////
// 文件：Scripts/Damage/WeaknessEffect.cs
//
// 模块：Damage System
//
// 职责：
// 1. 承载伤害事件、伤害修正与命中后效果相关代码。
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

// 虚弱：战斗内Debuff（不是RunBuff/永久Buff/装备效果）。
//   层数含义 = 剩余持续回合数，不代表强度：只要层数>0，造成的最终伤害统一×0.75，
//   不会因为层数越多而进一步降低（1层/2层/99层效果完全一样）。
//   持续时间：每回合结束-1层（见 Player.DecrementWeakness，由
//   BattleLifecycleEffects.cs 的 TurnEndCleanupEffect 统一调用，玩家/敌人对称）。
//   叠加规则：重新获得虚弱时用 Player.AddWeaknessLayers 累加剩余回合数，不重复
//   乘算倍率（虚弱×1 再获得虚弱×2 → 虚弱×3，效果仍然只是×0.75，只是持续更久）。
//
// 结算位置：单独一个 IBattleEffect，挂在 OnDamage/Mid（与凶残 FerocityEffect 完全
// 同一套写法/同一优先级），只在这一处判断 Source.HasWeakness 并追加一个
// DamageModifier；不会在杀/火杀/雷杀/火攻/万箭齐发等任何具体卡牌里各自判断
// "if HasBuff(weakness)"。
//
// 作用范围：不限制 AttackType（不像凶残只对杀系生效）——只要是走真实 DamageEvent
// 管线、且 Source 是虚弱角色本人的伤害都会命中这个判定，天然覆盖杀系/攻击性
// 锦囊/技能直接伤害（如闪电、血债血偿）等 spec 要求的全部范围，不需要逐类型硬编码。
//
// 已知局限（本次任务明确排除、未覆盖）：项目里存在约30余处绕过 DamageEvent/
// OnDamage 管线、直接调用 Player.TakeDamage 的毒素/反伤/自伤/环境伤害路径
// （详见早前"遗忘之石 & damage pipeline"调查结论）。虚弱不会影响这些路径的伤害，
// 因为它们从未创建过 DamageEvent、也就没有触发 OnDamage。要覆盖这部分需要
// 改造 Player.TakeDamage 本身，属于"重构整个Damage Pipeline"范畴，本次任务
// 明确要求不做，因此在此如实记录为已知限制。
public sealed class WeaknessDamageEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamage;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Damage System 的公开入口：Execute。
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

        var source = damage.Source;
        if (source == null || !source.HasWeakness)
        {
            return;
        }

        damage.AddModifier(new DamageModifier(
            "虚弱",
            DamageModifierPriority.SpecialMultiplier,
            DamageModifierOperation.MultiplyFloat,
            0.75));
        context.RoundResult.AddLine($"虚弱（剩余{source.WeaknessLayers}回合）：{source.DisplayName}造成的最终伤害×0.75。");
        context.AddTriggerLog("[虚弱]");
        context.AddTriggerLog($"{source.DisplayName}：造成的最终伤害×0.75（剩余{source.WeaknessLayers}回合）。");
    }
}
