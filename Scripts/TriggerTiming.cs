//////////////////////////////////////////////////////////
// 文件：Scripts/TriggerTiming.cs
//
// 模块：Trigger System
//
// 职责：
// 1. 承载触发时机、触发队列与优先级执行相关代码。
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

public enum TriggerTiming
{
    OnGameStart,
    OnTurnStart,
    OnBattlePrePhase,
    OnCardSelected,
    OnResourceChanged,
    OnBattleReveal,
    OnBattlePhase,
    OnBeforeDamage,
    OnDamage,
    OnDamageTaken,
    OnHeal,             // 生命值恢复后触发（丹等回复联动装备在此接入）
    OnBattleEnd,
    OnBattlePostPhase,
    OnDying,
    OnDeath,
    OnTurnEnd
}

/// <summary>
/// Trigger System 的公开枚举：EffectPriority。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum EffectPriority
{
    Immediate,
    Highest,
    High,
    Mid,
    Low,
    Lowest
}

/// <summary>
/// Trigger System 的公开枚举：BattlePhase。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum BattlePhase
{
    StartPhase,
    BattlePrePhase,
    BattlePhase,
    BattlePostPhase,
    EndPhase,
    GameOver
}

/// <summary>
/// Trigger System 的公开枚举：DyingState。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum DyingState
{
    Alive,
    Dying,
    Dead
}
