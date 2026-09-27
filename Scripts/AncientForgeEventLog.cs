//////////////////////////////////////////////////////////
// 文件：Scripts/AncientForgeEventLog.cs
//
// 模块：Event System
//
// 职责：
// 1. 承载地图事件、事件选项与事件奖励相关代码。
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

using Godot;

/// <summary>
/// Event System 的公开类：AncientForgeEventLog。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class AncientForgeEventLog
{
    /// <summary>
    /// Event System 的公开入口：Log。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void Log(string message)
    {
        GD.Print($"[AncientForgeEventLog] {message}");
    }
}
