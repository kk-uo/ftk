//////////////////////////////////////////////////////////
// 文件：Scripts/AiRule.cs
//
// 模块：Core System
//
// 职责：
// 1. 承载核心数据结构、通用规则与跨模块协作相关代码。
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

/// <summary>
/// Core System 的公开枚举：AiConditionType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum AiConditionType
{
    PlayerResourceAtLeast,
    SelfResourceAtLeast,
    PlayerResourceBelow,
    SelfResourceBelow,
    PlayerHpBelow,
    SelfHpBelow,
    SelfLastActionKill,
    // 上一回合使用过任意杀系牌（普通杀/火杀/雷杀/必中杀）即满足。
    SelfLastActionAnySha,
    SelfLastActionWine,
    SelfLastActionGuanxing,
    SelfLastActionCelestialImpact,
    // 钳制机械外骨骼已触发（进入第二阶段）：装备者HP首次跌破50%后触发，之后此条件恒为真。
    SelfExoskeletonPhaseTwo,
    // 当前未处于观星阶段（GuanxingPhase.None）：用于在录制/重放阶段前进行资源积累。
    SelfGuanxingPhaseNone,
    // 自身当前酒层数（WinePower + PendingWinePower）>= Value。
    SelfWineLayersAtLeast,
    // 上一回合使用了顺手牵羊。
    SelfLastActionSteal,
    // 对手当前处于冰冻状态（下回合只能出费）。
    PlayerIsFrozen
}

/// <summary>
/// Core System 的公开枚举：EnemyActionWeightType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum EnemyActionWeightType
{
    Slash,
    FireSlash,
    ThunderSlash,
    FireThunderSlash,
    CelestialImpact,
    ArrowBarrage,
    NanmanInvasion,
    Dodge,
    Peach,
    Wuxie,
    Resource,
    ShunShou,
    SureSlash,
    Wine,
    Guanxing,
    IceSlash
}

/// <summary>
/// Core System 的公开类：AiCondition。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class AiCondition
{
    public AiConditionType Type;
    public double Value;
}

/// <summary>
/// Core System 的公开类：AiWeightModifier。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class AiWeightModifier
{
    public EnemyActionWeightType WeightType;
    public float Delta;
}

/// <summary>
/// Core System 的公开类：AiRule。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class AiRule
{
    public List<AiCondition> Conditions = new();
    public List<AiWeightModifier> Modifiers = new();
}
