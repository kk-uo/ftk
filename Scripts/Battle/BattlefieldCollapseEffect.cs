//////////////////////////////////////////////////////////
// 文件：Scripts/Battle/BattlefieldCollapseEffect.cs
//
// 模块：Battle Rules（全局战斗规则）
//
// 为什么存在：
// 【战场崩坏】是一条不属于任何角色/装备的全局战斗规则：第55回合结束开始，
// 双方所有单位每回合结束都会受到递增的环境伤害（本回合数-54）。这类"和具体
// 角色/装备无关、影响所有战斗"的规则应该作为独立的 IBattleEffect 通过
// BattleRules.cs 统一注册，不应该散落进 BattleManager 或某个角色技能里；
// 以后新增其它全局规则也应该照这个样子加一个新文件 + 一行 Register，
// 不需要改这个文件本身。
//
// 职责：
// 1. 在 TriggerTiming.OnTurnEnd、第55回合起，对 context.Player 和
//    context.Encounter 里所有存活敌人各自造成一次真实伤害。
// 2. 环境伤害通过 Player.LoseHealth 直接扣减 HP，不经过 DamageEvent/
//    OnDamage/OnDamageTaken/OnBeforeDamage 管线——天然无视护盾/闪避/防御，
//    也不会触发任何"受到攻击"、"使用牌"或反击类技能（那些技能全部挂在
//    OnDamage/OnDamageTaken/OnBattlePhase 等不同的 Timing 上，本效果
//    从不主动 RaiseTrigger 那几个 Timing，结构上不可能被它们感知到）。
// 3. HP最低扣至0；所有单位完成本次掉血后，再统一进入 OnDying → OnDeath，
//    保证救援技能有机会生效，也避免结算顺序导致部分单位漏掉本次全体掉血。
// 4. 暴露 TriggerTurn/CalculateDamage 两个公开静态成员，供 BattleManager
//    表现层判断"是不是刚进入第55回合"、"这一回合应该显示多少伤害数字"，
//    伤害数值只在这里算一次，避免规则和表现各算一遍导致数字不一致。
//
// 不负责：
// × 播放特效、显示飘字、屏幕中央提示（表现层职责，由 BattleManager 在
//   OnTurnEnd 触发之后根据同一份 TurnCounter/CalculateDamage 独立判断、
//   独立渲染，参照既有 ShowDeltaPopups 的模式——本效果只改数据，不碰任何
//   UI 节点，也不引用 Presentation 命名空间下的任何类型）。
// × 实现角色专属救援或死亡效果（本效果只负责接入统一 OnDying/OnDeath 管线）。
//
// 主要依赖：
// IBattleEffect / BattleContext / Player.TakeDamage
//////////////////////////////////////////////////////////

/// <summary>
/// 全局战斗规则【战场崩坏】：第55回合结束起，双方所有单位每回合结束
/// 受到递增的环境伤害（本回合数 - 54），无视护盾/闪避/防御，不触发
/// 任何攻击/出牌相关技能或反击，仅作为环境伤害结算。
/// </summary>
public sealed class BattlefieldCollapseEffect : IBattleEffect
{
    /// <summary>真实伤害从这一回合开始生效（含）。</summary>
    public const int TriggerTurn = 55;

    public EffectPriority Priority => EffectPriority.Lowest;
    public TriggerTiming Timing => TriggerTiming.OnTurnEnd;

    /// <summary>
    /// 按当前回合数计算这一回合应该造成的真实伤害；未到 <see cref="TriggerTurn"/>
    /// 时返回 0。BattleManager 表现层用同一个方法计算要显示的数字，保证规则
    /// 和表现读到的是同一个值，不会出现"日志说3点、飘字显示4点"这种不一致。
    /// </summary>
    public static int CalculateDamage(int turnNumber)
    {
        return turnNumber < TriggerTurn ? 0 : turnNumber - (TriggerTurn - 1);
    }

    /// <summary>
    /// 对双方存活单位应用指定数值的战场崩坏伤害，并对归零单位执行濒死结算。
    ///
    /// 该入口让时间沙漏等效果复用全局规则的实际扣血语义，而不需要复制一份
    /// “玩家与所有敌人分别扣血”的实现。调用方仍负责决定伤害数值和记录来源日志。
    /// </summary>
    public static void ApplyDamage(BattleContext context, int damage)
    {
        if (damage <= 0)
        {
            return;
        }

        var dyingTargets = new System.Collections.Generic.List<(Player Target, int Damage)>();

        if (!context.Player.IsDead)
        {
            var before = context.Player.Health;
            var applied = ApplyClampedHealthLoss(context.Player, damage);
            context.RecordDirectDamage(context.Player, damage, applied, before, context.Player.Health,
                new HealthChangeSource(HealthChangeSourceKind.Environment, "战场崩坏", "battlefield_collapse"));
            context.RequestPlayerDamageBorder(applied, context.Player.MaxHealth);
            if (before > 0 && context.Player.Health == 0)
            {
                dyingTargets.Add((context.Player, applied));
            }
        }

        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            ResolveDyingTargets(context, dyingTargets);
            return;
        }

        foreach (var enemy in enemies)
        {
            if (!enemy.IsDead && enemy.Health > 0)
            {
                var before = enemy.Health;
                var applied = ApplyClampedHealthLoss(enemy, damage);
                context.RecordDirectDamage(enemy, damage, applied, before, enemy.Health,
                    new HealthChangeSource(HealthChangeSourceKind.Environment, "战场崩坏", "battlefield_collapse"));
                if (enemy.Health == 0)
                {
                    dyingTargets.Add((enemy, applied));
                }
            }
        }

        // 先让所有单位承受同一批次的全体掉血，再处理死亡。否则第一个单位死亡后
        // GameOver 会截断后续对象，本应同时受到的环境伤害会因列表顺序而丢失。
        ResolveDyingTargets(context, dyingTargets);
    }

    private static int ApplyClampedHealthLoss(Player target, int requestedDamage)
    {
        var applied = System.Math.Min(System.Math.Max(0, target.Health), requestedDamage);
        if (applied <= 0)
        {
            return 0;
        }

        target.LoseHealth(applied);
        return applied;
    }

    private static void ResolveDyingTargets(
        BattleContext context,
        System.Collections.Generic.IReadOnlyList<(Player Target, int Damage)> dyingTargets)
    {
        var previousDamage = context.DamageEvent;
        foreach (var (target, damage) in dyingTargets)
        {
            if (target.IsDead || target.Health > 0)
            {
                continue;
            }

            context.DamageEvent = new DamageEvent(
                target,
                target,
                CardType.Fee,
                damage,
                isDirectAttackDamage: false,
                overrideDamageType: DamageType.Mechanic,
                origin: new HealthChangeSource(HealthChangeSourceKind.Environment, "战场崩坏", "battlefield_collapse"));
            context.DamageEvent.ActualDamageDealt = damage;
            context.RaiseOnDying();
        }

        context.DamageEvent = previousDamage;
    }

    /// <summary>
    /// Battle Rules 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = CalculateDamage(context.TurnCounter);
        if (damage <= 0)
        {
            return;
        }

        ApplyDamage(context, damage);

        context.AddTriggerLog("[战场崩坏]");
        context.AddTriggerLog("Trigger: OnTurnEnd");
        context.AddTriggerLog($"第{context.TurnCounter}回合：双方受到{damage}点真实伤害（环境伤害，无视护盾/闪避/防御）。");
    }
}
