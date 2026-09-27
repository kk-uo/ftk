//////////////////////////////////////////////////////////
// 文件：Scripts/ChapterFourEnemyBehaviors.cs
//
// 模块：Enemy System
//
// 职责：
// 1. 承载第四章·深渊普通敌人（侦测者/巨口）的战斗触发效果，配合 EnemyAI.cs 里的
//    SelectScoutAction/SelectJuKouAction 自定义决策分支一起构成完整AI行为。
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

/// <summary>
/// Enemy System 的公开类：ScoutVigilanceClearOnDamageEffect。
///
/// 侦测者【警戒状态】：本场战斗第一次产生任意实际伤害（无论来源、含侦测者自己受到伤害）
/// 立即解除警戒。挂在全局 OnDamageTaken，只要有实际伤害发生就清除场上所有仍处于警戒状态
/// 的单位（目前只有侦测者会被打上这个标记），不区分伤害方向。
/// </summary>
public sealed class ScoutVigilanceClearOnDamageEffect : IBattleEffect
{
    private const string VigilantKey = "scout_vigilant";

    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Immediate;

    /// <summary>
    /// Enemy System 的公开入口：Execute。
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

        ClearIfVigilant(context.Player);
        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            ClearIfVigilant(enemy);
        }
    }

    private static void ClearIfVigilant(Player unit)
    {
        if (unit.RuntimeStates.ContainsKey(VigilantKey))
        {
            unit.RuntimeStates.Remove(VigilantKey);
        }
    }
}

/// <summary>
/// Enemy System 的公开类：ScoutVigilanceTurnStartEffect。
///
/// 侦测者【警戒状态】：若前3个战斗回合始终没有产生任何伤害，第4回合开始自动解除警戒
/// （侦测者从战斗第1回合就进入警戒，所以直接用全局回合数≥4判断即可，不需要额外的
/// 单位自身回合计数器）。
/// </summary>
public sealed class ScoutVigilanceTurnStartEffect : IBattleEffect
{
    private const string VigilantKey = "scout_vigilant";

    public TriggerTiming Timing => TriggerTiming.OnTurnStart;
    public EffectPriority Priority => EffectPriority.Mid;

    /// <summary>
    /// Enemy System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.TurnNumber < 4)
        {
            return;
        }

        ClearIfVigilant(context.Player);
        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            ClearIfVigilant(enemy);
        }
    }

    private static void ClearIfVigilant(Player unit)
    {
        if (unit.RuntimeStates.ContainsKey(VigilantKey))
        {
            unit.RuntimeStates.Remove(VigilantKey);
        }
    }
}

/// <summary>
/// Enemy System 的公开类：JuKouNoDamageTrackEffect。
///
/// 巨口：追踪"连续多少回合没有造成任何伤害"。回合结束时若本回合造成过实际伤害则清零，
/// 否则计数+1；EnemyAI.cs 的 SelectJuKouAction 读取这个计数决定是否大幅提权攻击。
/// </summary>
public sealed class JuKouNoDamageTrackEffect : IBattleEffect
{
    private const string DealtDamageThisTurnKey = "jukou_dealt_damage_this_turn";
    private const string NoDamageTurnsKey = "jukou_no_damage_turns";

    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Enemy System 的公开入口：Execute。
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

        if (damage.Source is EnemyInstance juKouSource && juKouSource.Definition.Id == "ju_kou")
        {
            juKouSource.RuntimeStates[DealtDamageThisTurnKey] = true;
        }
    }

    /// <summary>
    /// 回合结束时结算：由 <see cref="JuKouNoDamageTurnEndEffect"/> 调用。
    /// </summary>
    internal static void SettleTurnEnd(EnemyInstance juKou)
    {
        var dealtDamage = juKou.RuntimeStates.TryGetValue(DealtDamageThisTurnKey, out var v) && v is true;
        if (dealtDamage)
        {
            juKou.RuntimeStates[NoDamageTurnsKey] = 0;
        }
        else
        {
            var current = juKou.RuntimeStates.TryGetValue(NoDamageTurnsKey, out var c) && c is int n ? n : 0;
            juKou.RuntimeStates[NoDamageTurnsKey] = current + 1;
        }

        juKou.RuntimeStates.Remove(DealtDamageThisTurnKey);
    }

    internal static int GetNoDamageTurns(EnemyInstance juKou)
    {
        return juKou.RuntimeStates.TryGetValue(NoDamageTurnsKey, out var v) && v is int n ? n : 0;
    }
}

/// <summary>
/// Enemy System 的公开类：JuKouNoDamageTurnEndEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class JuKouNoDamageTurnEndEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnTurnEnd;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// Enemy System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            if (!enemy.IsDead && enemy.Definition.Id == "ju_kou")
            {
                JuKouNoDamageTrackEffect.SettleTurnEnd(enemy);
            }
        }
    }
}
