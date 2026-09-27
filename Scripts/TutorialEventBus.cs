//////////////////////////////////////////////////////////
// 文件：Scripts/TutorialEventBus.cs
//
// 模块：Tutorial System
//
// 职责：
// 1. 承载教程步骤、教程事件与教学流程相关代码。
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

using System;

/// <summary>
/// Decoupled event bus: battle subsystems publish committed tutorial actions here;
/// TutorialManager subscribes. No battle code needs to know about individual
/// tutorial steps; the bus is one-directional.
/// </summary>
public static class TutorialEventBus
{
    public static event Action<CardType>? CardPlayed;

    /// <summary>
    /// Tutorial System 的公开入口：NotifyCardPlayed。
    ///
    /// 仅在行动已通过合法性校验并进入真实结算时调用；普通点击或尚在堆叠确认
    /// 窗口中的选择不能触发本事件。
    /// </summary>
    public static void NotifyCardPlayed(CardType type) => CardPlayed?.Invoke(type);

    /// <summary>
    /// Tutorial System 的公开入口：RemoveAllListeners。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void RemoveAllListeners() => CardPlayed = null;
}
