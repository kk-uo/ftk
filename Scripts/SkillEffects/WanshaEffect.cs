//////////////////////////////////////////////////////////
// 文件：Scripts/SkillEffects/WanshaEffect.cs
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

// 完杀（贾诩专属·史诗）：当任意角色通过治疗恢复生命值时，对其造成等同于本次治疗量的真实伤害。
// 真实伤害：无视护甲、无视减伤、无视闪避，不属于攻击伤害。
// 若实际恢复量为0，则不触发（例如满血状态使用桃）。
//
// WanshaEffect（ISkillEffect，注册存根，实际效果通过 BattleRules 直接注册）。
// WanshaHealCancelEffect（OnHeal, Low）：响应所有 OnHeal 事件，向被治疗单位施加等量真实伤害。
/// <summary>
/// Skill Effect System 的公开类：WanshaEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class WanshaEffect : ISkillEffect
{
    public string SkillId => SkillIds.Wansha;

    /// <summary>
    /// Skill Effect System 的公开入口：Register。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Register(TriggerManager triggerManager, Player owner)
    {
    }
}

/// <summary>
/// Skill Effect System 的公开类：WanshaHealCancelEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class WanshaHealCancelEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnHeal;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        // 完杀不再只检查玩家：只要玩家或任意一个存活敌人拥有完杀（例如精英"失心者"），
        // 这场战斗里的任意一次治疗都会触发等量真实伤害——与"任意角色通过治疗恢复生命值"
        // 的技能描述保持一致，不再是"只有玩家角色是贾诩才会生效"的单向判断。
        var hasWansha = context.Player.HasSkill(SkillIds.Wansha);
        if (!hasWansha && context.Encounter != null)
        {
            foreach (var enemy in context.Encounter.Enemies)
            {
                if (!enemy.IsDead && enemy.HasSkill(SkillIds.Wansha))
                {
                    hasWansha = true;
                    break;
                }
            }
        }

        if (!hasWansha || context.GameOver)
        {
            return;
        }

        var heal = context.HealEvent;
        if (heal == null || heal.Amount <= 0)
        {
            return;
        }

        var healed = heal.Healer;
        if (healed.IsDead)
        {
            return;
        }

        healed.TakeDamage(heal.Amount);
        context.RoundResult.AddLine($"完杀：{healed.DisplayName}恢复{heal.Amount}点生命，完杀造成{heal.Amount}点真实伤害。");
        context.AddTriggerLog("[完杀]");
        context.AddTriggerLog($"完杀：{healed.DisplayName}恢复{heal.Amount} → 受到{heal.Amount}点真实伤害。");
        context.ReportPlayerCharacterSkillTriggered(
            context.Player, SkillIds.Wansha, Timing, Priority, "heal_punish");

        if (healed.Health <= 0 && !healed.IsDead && !context.GameOver)
        {
            var savedHeal = context.HealEvent;
            context.HealEvent = null;
            context.DamageEvent = new DamageEvent(context.Player, healed, CardType.Kill, heal.Amount);
            context.RaiseOnDying();
            context.DamageEvent = null;
            context.HealEvent = savedHeal;
        }
    }
}
