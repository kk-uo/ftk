//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/YiChengSkill.cs
//
// 徐盛专属【疑城】。
// 费用损失统一经 BattleRules.PayManaAndRaiseResourceChanged 上报；本效果只读取
// 实际损失量，因此不会把“费用不足而未扣除”的支付误判成一次触发。
//////////////////////////////////////////////////////////

using System;

/// <summary>
/// 兼容旧技能注册表的【疑城】条目。正式战斗由默认 TriggerManager 全局注册的效果
/// 根据持有技能的单位自检，避免重复注册。
/// </summary>
public sealed class YiChengEffect : ISkillEffect
{
    public string SkillId => SkillIds.YiCheng;

    public void Register(TriggerManager triggerManager, Player owner)
    {
        // 默认 TriggerManager 已注册 YiChengResourceHealEffect 与 YiChengDamageManaEffect。
    }
}

/// <summary>
/// 【疑城】之一：每次实际失去费用后固定回复5点生命。
/// </summary>
public sealed class YiChengResourceHealEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnResourceChanged;
    public EffectPriority Priority => EffectPriority.Low;

    public void Execute(BattleContext context)
    {
        var change = context.ResourceChangeEvent;
        if (change == null || change.Amount >= 0 || context.GameOver)
        {
            return;
        }

        var owner = change.Owner;
        if (!owner.HasSkill(SkillIds.YiCheng) || owner.IsDead || owner.Health >= owner.MaxHealth)
        {
            return;
        }

        const int requestedHeal = 5;
        var healing = BattleHealing.Apply(
            context,
            owner,
            requestedHeal,
            false,
            new HealthChangeSource(HealthChangeSourceKind.Skill, "疑城", SkillIds.YiCheng, owner));
        if (healing.HealedAmount <= 0 && !healing.WasConverted)
        {
            return;
        }

        context.RoundResult.AddLine(healing.WasConverted
            ? $"疑城：{owner.DisplayName}失去{BattleRules.FormatMana(-change.Amount)}费，回复效果被毒丹转换。"
            : $"疑城：{owner.DisplayName}失去{BattleRules.FormatMana(-change.Amount)}费，回复{healing.HealedAmount}生命。");
        context.AddTriggerLog("[疑城]");
        context.AddTriggerLog($"失去{BattleRules.FormatMana(-change.Amount)}费：请求回复{requestedHeal}生命。");
        context.ReportPlayerCharacterSkillTriggered(owner, SkillIds.YiCheng, Timing, Priority, "mana_lost_heal");
    }
}

/// <summary>
/// 【疑城】之二：每次实际失去生命后获得0.25费；同一回合可重复触发。
/// </summary>
public sealed class YiChengDamageManaEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Low;

    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.ActualDamageDealt <= 0 || context.GameOver)
        {
            return;
        }

        var owner = damage.Target;
        if (!owner.HasSkill(SkillIds.YiCheng) || owner.DyingState == DyingState.Dead)
        {
            return;
        }

        const double gainedMana = 0.25;
        owner.GainMana(gainedMana);
        context.RoundResult.AddResourceGain(owner, gainedMana);
        context.RoundResult.AddLine($"疑城：{owner.DisplayName}失去{damage.ActualDamageDealt}生命，获得{BattleRules.FormatMana(gainedMana)}费。");
        context.AddTriggerLog("[疑城]");
        context.AddTriggerLog($"失去{damage.ActualDamageDealt}生命：获得{BattleRules.FormatMana(gainedMana)}费。");
        context.ReportPlayerCharacterSkillTriggered(owner, SkillIds.YiCheng, Timing, Priority, "damage_taken_mana");
    }
}
