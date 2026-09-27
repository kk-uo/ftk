//////////////////////////////////////////////////////////
// 文件：Scripts/EnemyTag.cs
//
// 模块：Enemy System
//
// 职责：
// 1. 承载敌人定义、敌人实例与敌方 AI相关代码。
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
/// Enemy System 的公开枚举：EnemyTag。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
[System.Flags]
public enum EnemyTag
{
    None = 0,
    Human = 1 << 0,
    Soldier = 1 << 1,
    Officer = 1 << 2,
    Warlord = 1 << 3,
    Shu = 1 << 4,
    Wei = 1 << 5,
    Wu = 1 << 6,
    Qun = 1 << 7,
    Boss = 1 << 8,
    Healer = 1 << 9,
    Traitor = 1 << 10,

    // 路线标签：标注该敌人所属的地图路线，用于路线筛选。
    RouteCity     = 1 << 11,
    RouteSewer    = 1 << 12,
    RouteFactory  = 1 << 13,
    RouteRuins    = 1 << 14,
    RouteImperial = 1 << 15
}
