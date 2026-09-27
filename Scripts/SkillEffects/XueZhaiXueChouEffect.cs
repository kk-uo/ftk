//////////////////////////////////////////////////////////
// 文件：Scripts/SkillEffects/XueZhaiXueChouEffect.cs
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

// 血债血偿·濒死版（独眼巨人专属）：第一次濒死拦截，下回合对玩家造成40%最大HP真实伤害，无视闪/无懈/护盾。
// XueZhaiNearDeathEffect（OnDying, High）：拦截第一次死亡，设置xuezhaixuechou_near_death_pending，HP→1。
// XueZhaiNearDeathSkillEffect（ISkillEffect存根）：用于SkillEffectRegistry注册。
//
// 血债血偿（夏侯惇专属·史诗）：持有者受到伤害后，对所有对立方单位造成等同于本次伤害的真实伤害。
// 真实伤害：无视护甲、无视减伤、无视藤甲、无视鳞甲、无视闪避，不属于攻击伤害。
// 不触发：反击、龙胆、观星、酒增伤、杀增伤、任何攻击命中效果。
// 该效果本身不会再次触发血债血偿（直接调用 TakeDamage，不经过伤害事件系统）。
//
// XueZhaiXueChouEffect（ISkillEffect，注册存根）。
// XueZhaiXueChouRetaliateEffect（OnDamageTaken, Low，注册于 DamageTakenEffect 之后）：
//   玩家持有时：玩家受伤 → 对全体存活敌人施加等量真实伤害并处理濒死链。
//   敌人持有时：敌人受伤 → 对玩家施加等量真实伤害并处理玩家濒死。
/// <summary>
/// Skill Effect System 的公开类：XueZhaiNearDeathSkillEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class XueZhaiNearDeathSkillEffect : ISkillEffect
{
    public string SkillId => SkillIds.XueZhaiXueChouNearDeath;

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
/// Skill Effect System 的公开类：XueZhaiNearDeathEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class XueZhaiNearDeathEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDying;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null) return;
        // 装备自伤（source == target）不触发血债血偿。
        if (damage.Source == damage.Target) return;

        var enemy = damage.Target as EnemyInstance;
        if (enemy == null || !enemy.HasSkill(SkillIds.XueZhaiXueChouNearDeath) || enemy.Health > 0) return;

        // 已触发过（第二次濒死），允许永久死亡。
        if (enemy.RuntimeStates.TryGetValue("xuezhaixuechou_near_death_pending", out var pending) && pending is true) return;

        // 拦截第一次死亡：设置濒死待发标志，HP恢复至1。
        enemy.RuntimeStates["xuezhaixuechou_near_death_pending"] = true;
        enemy.DebugSetHealth(1);
        context.RoundResult.AddLine($"【血债血偿】触发：{enemy.DisplayName}进入濒死状态，下回合释放濒死之力！");
        context.AddTriggerLog("[血债血偿]");
        context.AddTriggerLog($"{enemy.DisplayName} 血债血偿拦截死亡，HP恢复至1，下回合爆发。");
    }
}

/// <summary>
/// Skill Effect System 的公开类：XueZhaiXueChouEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class XueZhaiXueChouEffect : ISkillEffect
{
    public string SkillId => SkillIds.XueZhaiXueChou;

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
/// Skill Effect System 的公开类：XueZhaiXueChouRetaliateEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class XueZhaiXueChouRetaliateEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill Effect System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.GameOver) return;

        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled || damage.Amount <= 0) return;
        // 装备自伤（source == target）不触发血债血偿。
        if (damage.Source == damage.Target) return;

        var retaliateAmount = damage.Amount;

        // 玩家持有血债血偿：玩家受伤时，对全体存活敌人造成等量真实伤害。
        if (context.Player.HasSkill(SkillIds.XueZhaiXueChou)
            && damage.Target == context.Player
            && !context.Player.IsDead)
        {
            var enemies = context.Encounter?.Enemies;
            if (enemies == null) return;

            context.RoundResult.AddLine($"血债血偿：{context.Player.DisplayName}受到{retaliateAmount}点伤害，对所有敌人造成{retaliateAmount}点真实伤害。");
            context.AddTriggerLog("[血债血偿]");
            context.AddTriggerLog($"血债血偿：玩家受伤{retaliateAmount} → 全体敌人各受{retaliateAmount}点真实伤害。");
            context.ReportPlayerCharacterSkillTriggered(
                context.Player, SkillIds.XueZhaiXueChou, Timing, Priority, "retaliate");

            foreach (var enemy in enemies)
            {
                if (enemy.IsDead || context.GameOver) continue;

                var healthBefore = enemy.Health;
                enemy.TakeDamage(retaliateAmount);
                context.RecordDirectDamage(enemy, retaliateAmount, System.Math.Max(0, healthBefore - enemy.Health), healthBefore, enemy.Health,
                    new HealthChangeSource(HealthChangeSourceKind.Skill, "血债血偿", SkillIds.XueZhaiXueChou, context.Player));
                context.RoundResult.AddLine($"血债血偿：{enemy.DisplayName}受到{retaliateAmount}点真实伤害（生命剩余{System.Math.Max(0, enemy.Health)}/{enemy.MaxHealth}）。");
                context.AddTriggerLog($"血债血偿：{enemy.DisplayName} -{retaliateAmount} → {enemy.Health}/{enemy.MaxHealth}");

                if (enemy.Health <= 0 && !enemy.IsDead && !context.GameOver)
                {
                    var savedDamage = context.DamageEvent;
                    context.DamageEvent = new DamageEvent(context.Player, enemy, CardType.Kill, retaliateAmount,
                        origin: new HealthChangeSource(HealthChangeSourceKind.Skill, "血债血偿", SkillIds.XueZhaiXueChou, context.Player));
                    context.RaiseOnDying();
                    context.DamageEvent = savedDamage;
                }
            }

            return;
        }

        // 敌人持有血债血偿：敌人受伤时，对玩家造成等量真实伤害。
        if (damage.Target is EnemyInstance enemyOwner
            && enemyOwner.HasSkill(SkillIds.XueZhaiXueChou)
            && !context.Player.IsDead)
        {
            context.RoundResult.AddLine($"血债血偿：{enemyOwner.DisplayName}受到{retaliateAmount}点伤害，对玩家造成{retaliateAmount}点真实伤害。");
            context.AddTriggerLog("[血债血偿]");
            context.AddTriggerLog($"血债血偿：{enemyOwner.DisplayName}受伤{retaliateAmount} → 玩家受{retaliateAmount}点真实伤害。");

            var healthBefore = context.Player.Health;
            context.Player.TakeDamage(retaliateAmount);
            context.RecordDirectDamage(context.Player, retaliateAmount, System.Math.Max(0, healthBefore - context.Player.Health), healthBefore, context.Player.Health,
                new HealthChangeSource(HealthChangeSourceKind.Skill, "血债血偿", SkillIds.XueZhaiXueChou, enemyOwner));
            context.RoundResult.AddLine($"血债血偿：玩家受到{retaliateAmount}点真实伤害（生命剩余{System.Math.Max(0, context.Player.Health)}/{context.Player.MaxHealth}）。");
            context.AddTriggerLog($"血债血偿：玩家 -{retaliateAmount} → {context.Player.Health}/{context.Player.MaxHealth}");

            if (context.Player.Health <= 0 && !context.Player.IsDead && !context.GameOver)
            {
                var savedDamage = context.DamageEvent;
                context.DamageEvent = new DamageEvent(enemyOwner, context.Player, CardType.Kill, retaliateAmount,
                    origin: new HealthChangeSource(HealthChangeSourceKind.Skill, "血债血偿", SkillIds.XueZhaiXueChou, enemyOwner));
                context.RaiseOnDying();
                context.DamageEvent = savedDamage;
            }
        }
    }
}
