//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/ZhouYuSkills.cs
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

// 周瑜专属技能：
//   【英姿】：不需要独立的 IBattleEffect——将【火杀】替换为【火攻】完全由
//     BattleManager.Selection.cs 的行动栏构建逻辑
//     这一行驱动（每回合根据"是否拥有该技能"重新计算可出牌区，天然不会因为
//     战斗重开/读档/角色重建UI而重复获得，因为从来没有真正"写入"过一份持久
//     卡组数据）。SkillEffectRegistry 里对应的 Register 留空即可，与 KejiEffect
//     的写法完全一致。
//   【业炎】：见下方 YeYanEffect，唯一的真实触发点。

/// <summary>
/// Skill System 的公开类：YeYanEffect。
///
/// 当周瑜对敌方造成任意真实火属性伤害（DamageType 含 Fire、ActualDamageDealt>0）
/// 时，令受伤目标获得2层【虚弱】。挂在 OnDamageTaken/Lowest（与 CodexDamageTrackingEffect
/// 相同优先级层，此时 ActualDamageDealt 已经是最终结算值），DamagePreviewService
/// 从不触发 OnDamageTaken，天然不会被预估/悬停污染。
/// </summary>
public sealed class YeYanEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.ActualDamageDealt <= 0)
        {
            return;
        }

        if ((damage.DamageType & DamageType.Fire) == 0)
        {
            return;
        }

        if (!ReferenceEquals(damage.Source, context.Player) || !context.Player.HasSkill(SkillIds.YeYan))
        {
            return;
        }

        if (damage.Target is not EnemyInstance target || target.IsDead)
        {
            return;
        }

        if (target.IsCombatDebuffImmune)
        {
            context.AddTriggerLog($"[泉水精华] {target.DisplayName}免疫虚弱。");
            return;
        }

        target.AddWeaknessLayers(2);
        context.RoundResult.AddLine($"业炎：{target.DisplayName}获得【虚弱】×2（剩余{target.WeaknessLayers}回合）。");
        context.AddTriggerLog("[业炎]");
        context.AddTriggerLog($"{target.DisplayName}虚弱+2层，剩余{target.WeaknessLayers}回合。");

        // 一次范围攻击可能让多个目标各自触发这里、各自获得虚弱，但技能大字
        // 本回合只应该播放一次——用 BeginRoundResult 每回合重置的标记去重，
        // 而不是依赖 DamageEvent 的 resolution_chain_id（多目标伤害各自独立
        // RaiseTrigger，chain id 并不相同，无法用来去重）。
        if (!context.YeYanTriggeredThisRound)
        {
            context.YeYanTriggeredThisRound = true;
            context.ReportPlayerCharacterSkillTriggered(
                context.Player, SkillIds.YeYan, Timing, Priority, "weaken");
        }
    }
}
