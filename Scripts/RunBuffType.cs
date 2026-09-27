//////////////////////////////////////////////////////////
// 文件：Scripts/RunBuffType.cs
//
// 模块：Run Buff System
//
// 职责：
// 1. 承载跨战斗 Buff 定义、生命周期与结算相关代码。
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

public enum RunBuffType
{
    Blessing,
    Curse,
    ChapterVariant,
    RouteVariant,
    BossEffect,
    Event
}

/// <summary>
/// Run Buff System 的公开枚举：RunBuffTriggerTiming。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum RunBuffTriggerTiming
{
    OnBattleStart,
    OnBattleEnd,
    OnChapterStart,
    OnChapterEnd,
    OnDamageCalculate,
    OnRewardGenerate
}

/// <summary>
/// Run Buff System 的公开枚举：RunBuffEffectType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum RunBuffEffectType
{
    EnemyMaxHpPercentBonus,
    PlayerDamageTakenMultiplierBonus,
    PlayerDamageDealtMultiplierBonus,
    BlockPlayerHealing,
    EnemyDamageMultiplierBonus,
    PlayerFireDamageFlatBonus,
    PlayerThunderDamageFlatBonus,
    PlayerIceDamageFlatBonus,
    PlayerPoisonDamageFlatBonus,
    // 每场战斗开始时为玩家额外提供法力值。
    PlayerStartBattleManaBonus
}
