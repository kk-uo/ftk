//////////////////////////////////////////////////////////
// 文件：Scripts/Skills/ZhangJiaoSkills.cs
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

using System.Collections.Generic;

// 雷击（张角专属·史诗）：持有者受到攻击牌伤害被成功抵消时，对攻击者造成10点Thunder伤害。
// 触发条件：damage.Cancelled == true && BattleRules.IsAnyAttackCard(attackType)
// 持有者可以是玩家或敌人。
/// <summary>
/// Skill System 的公开类：LeiJiEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class LeiJiEffect : IBattleEffect
{
    private const int LeiJiDamage = 10;

    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.GameOver) return;
        var damage = context.DamageEvent;
        if (damage == null || !damage.Cancelled) return;
        if (!BattleRules.IsAnyAttackCard(damage.AttackType)) return;

        Player? holder = null;
        Player? attacker = damage.Source;

        if (context.Player.HasSkill(SkillIds.LeiJi) && damage.Target == context.Player)
        {
            holder = context.Player;
        }
        else if (damage.Target is EnemyInstance enemyTarget && enemyTarget.HasSkill(SkillIds.LeiJi))
        {
            holder = enemyTarget;
        }

        if (holder == null || attacker == null || attacker.IsDead || attacker == holder) return;

        context.RoundResult.AddLine($"{holder.DisplayName}【雷击】：{attacker.DisplayName}的攻击被抵消，对其造成{LeiJiDamage}点雷属性伤害。");
        context.AddTriggerLog("[雷击]");
        context.AddTriggerLog($"雷击：{attacker.DisplayName} 受到 {LeiJiDamage} 点 Thunder 伤害。");
        context.ReportPlayerCharacterSkillTriggered(
            holder, SkillIds.LeiJi, Timing, Priority, "counter");

        var savedDamage = context.DamageEvent;
        context.DamageEvent = new DamageEvent(holder, attacker, CardType.LightningStrike, LeiJiDamage, overrideDamageType: DamageType.Thunder,
            origin: new HealthChangeSource(HealthChangeSourceKind.Skill, "雷击", SkillIds.LeiJi, holder));
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
        context.DamageEvent = savedDamage;
    }
}

// 黄天（张角专属·史诗）：敌人受到Thunder伤害时，在该敌人身上生成【闪电】。
// 【闪电】未落下时在张角与随机敌人之间循环转移；只有张角自己的敌人会被初始施加。
/// <summary>
/// Skill System 的公开类：HuangTianEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class HuangTianEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.GameOver) return;
        var damage = context.DamageEvent;
        if (damage == null || damage.Cancelled) return;
        if (!damage.DamageType.HasFlag(DamageType.Thunder)) return;

        var target = damage.Target;
        if (target.IsDead) return;

        foreach (var owner in GetLivingHuangTianOwners(context))
        {
            // 只将闪电施加给该张角的敌人，避免张角被己方或自己承受的雷伤反复自我刷新。
            if (!IsEnemyOf(owner, target))
            {
                continue;
            }

            var alreadyHad = LightningBuffHelper.HasLightning(target);
            LightningBuffHelper.SetLightning(target, owner);
            context.HuangTianVisualRequested?.Invoke(
                new HuangTianVisualRequest(target, HuangTianVisualKind.LightningApplied));

            context.RoundResult.AddLine(alreadyHad
                ? $"{owner.DisplayName}【黄天】：{target.DisplayName}的【闪电】被刷新。"
                : $"{owner.DisplayName}【黄天】：{target.DisplayName}获得【闪电】。");
            context.AddTriggerLog("[黄天]");
            context.AddTriggerLog($"黄天：{target.DisplayName}获得/刷新【闪电】（来源：{owner.DisplayName}）。");
            context.ReportPlayerCharacterSkillTriggered(
                owner, SkillIds.HuangTian, Timing, Priority, "lightning_buff");
        }
    }

    internal static List<Player> GetLivingHuangTianOwners(BattleContext context)
    {
        var owners = new List<Player>();
        if (!context.Player.IsDead && context.Player.HasSkill(SkillIds.HuangTian))
        {
            owners.Add(context.Player);
        }

        if (context.Encounter != null)
        {
            foreach (var enemy in context.Encounter.Enemies)
            {
                if (!enemy.IsDead && enemy.HasSkill(SkillIds.HuangTian))
                {
                    owners.Add(enemy);
                }
            }
        }

        return owners;
    }

    internal static bool IsEnemyOf(Player owner, Player candidate)
    {
        return owner.Team == BattleTeam.Player
            ? candidate is EnemyInstance
            : candidate.Team == BattleTeam.Player;
    }
}

// 【闪电】Buff回合开始处理：每回合开始，对所有携带闪电的角色分别判定。
// 10%概率：对目标造成20点Thunder伤害；
// 90%概率：闪电转移（敌方→张角，张角→随机敌方）。
// 新生成的闪电不在本轮快照内，不重复判定。
/// <summary>
/// Skill System 的公开类：LightningBuffTurnEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class LightningBuffTurnEffect : IBattleEffect
{
    private const int LightningDamage = 20;
    private const double TriggerChance = 0.10;

    // 仅供同程序集 Headless 回归固定概率；正式游戏始终使用 Random.Shared。
    internal static System.Func<double>? RandomRollForTesting { get; set; }

    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Skill System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var enemies = context.Encounter?.Enemies;

        // 快照：本轮开始时持有闪电的角色。
        var holders = new List<Player>();
        if (LightningBuffHelper.HasLightning(context.Player)) holders.Add(context.Player);
        if (enemies != null)
        {
            foreach (var enemy in enemies)
            {
                if (!enemy.IsDead && LightningBuffHelper.HasLightning(enemy))
                    holders.Add(enemy);
            }
        }

        if (holders.Count == 0) return;

        foreach (var holder in holders)
        {
            if (context.GameOver) break;
            if (holder.IsDead)
            {
                LightningBuffHelper.SetLightning(holder, false);
                continue;
            }

            var owner = LightningBuffHelper.GetOwner(holder);
            if (owner == null || owner.IsDead)
            {
                LightningBuffHelper.SetLightning(holder, false);
                continue;
            }

            // 先清除当前闪电，避免本轮被再次纳入快照外处理。
            LightningBuffHelper.SetLightning(holder, false);

            if (GetRandomRoll() < TriggerChance)
            {
                // 10% 落下：20点雷属性技能伤害，完整经过伤害修正、日志、濒死与黄天触发链。
                context.RoundResult.AddLine($"【闪电】落下！对{holder.DisplayName}造成{LightningDamage}点雷属性伤害。");
                context.AddTriggerLog("[闪电触发]");
                context.AddTriggerLog($"闪电：{holder.DisplayName} 受到 {LightningDamage} 点 Thunder 伤害。");
                context.HuangTianVisualRequested?.Invoke(
                    new HuangTianVisualRequest(holder, HuangTianVisualKind.LightningStruck));

                var savedDamage = context.DamageEvent;
                context.DamageEvent = new DamageEvent(
                    owner,
                    holder,
                    CardType.LightningStrike,
                    LightningDamage,
                    overrideDamageType: DamageType.Thunder,
                    origin: new HealthChangeSource(HealthChangeSourceKind.Skill, "黄天·闪电", SkillIds.HuangTian, owner));
                context.TriggerManager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
                context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
                context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
                context.DamageEvent = savedDamage;
            }
            else
            {
                // 90% 转移
                if (ReferenceEquals(holder, owner))
                {
                    // 张角 → 随机存活敌人。
                    var candidates = GetLivingEnemiesOf(owner, context);
                    if (candidates.Count > 0)
                    {
                        var target = candidates[System.Random.Shared.Next(candidates.Count)];
                        TransferLightning(context, holder, target, owner);
                    }
                }
                else
                {
                    // 敌人 → 张角。
                    TransferLightning(context, holder, owner, owner);
                }
            }
        }
    }

    private static List<Player> GetLivingEnemiesOf(Player owner, BattleContext context)
    {
        var candidates = new List<Player>();
        if (owner.Team == BattleTeam.Player)
        {
            if (context.Encounter != null)
            {
                foreach (var enemy in context.Encounter.Enemies)
                {
                    if (!enemy.IsDead) candidates.Add(enemy);
                }
            }
        }
        else if (!context.Player.IsDead)
        {
            candidates.Add(context.Player);
        }

        return candidates;
    }

    private static void TransferLightning(BattleContext context, Player from, Player target, Player owner)
    {
        LightningBuffHelper.SetLightning(target, owner);
        context.HuangTianVisualRequested?.Invoke(
            new HuangTianVisualRequest(target, HuangTianVisualKind.LightningTransferred));
        context.RoundResult.AddLine($"【闪电】本回合未落下，从{from.DisplayName}转移至{target.DisplayName}。");
        context.AddTriggerLog($"[闪电转移] {from.DisplayName} → {target.DisplayName}");
    }

    private static double GetRandomRoll() =>
        RandomRollForTesting?.Invoke() ?? System.Random.Shared.NextDouble();
}

// 静态工具：管理角色的【闪电】Buff状态。
/// <summary>
/// Skill System 的公开类：LightningBuffHelper。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class LightningBuffHelper
{
    private const string Key = "lightning_buff";

    /// <summary>
    /// Skill System 的公开入口：HasLightning。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static bool HasLightning(Player player) =>
        player.RuntimeStates.TryGetValue(Key, out var v) && v is true;

    /// <summary>
    /// Skill System 的公开入口：SetLightning。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    private const string OwnerKey = "lightning_buff_owner";

    public static Player? GetOwner(Player player) =>
        player.RuntimeStates.TryGetValue(OwnerKey, out var value) ? value as Player : null;

    public static void SetLightning(Player player, Player owner)
    {
        player.RuntimeStates[Key] = true;
        player.RuntimeStates[OwnerKey] = owner;
    }

    public static void SetLightning(Player player, bool value)
    {
        if (value)
        {
            // 保留旧入口给现有调用方；没有归属的状态不会参与黄天回合结算。
            player.RuntimeStates[Key] = true;
            return;
        }

        player.RuntimeStates.Remove(Key);
        player.RuntimeStates.Remove(OwnerKey);
    }
}
