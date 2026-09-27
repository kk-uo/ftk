//////////////////////////////////////////////////////////
// 文件：Scripts/ChoiceSystem.cs
//
// 模块：Choice System
//
// 职责：
// 1. 承载统一选择界面与候选项生成相关代码。
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
using System.Collections.Generic;

/// <summary>
/// Choice System 的公开枚举：ChoiceKind。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum ChoiceKind
{
    Skill,
    Equipment,
    Chip,
    Element,
    Buff,
    InventoryEquipment,
    Card,
    Generic
}

/// <summary>
/// Choice System 的公开类：ChoiceOption。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ChoiceOption
{
    /// <summary>
    /// Choice System 的公开入口：ChoiceOption。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public ChoiceOption(
        ChoiceKind kind,
        string id,
        string title,
        string description,
        string? subtitle = null,
        string? iconText = null,
        object? payload = null)
    {
        Kind = kind;
        Id = id;
        Title = title;
        Subtitle = subtitle ?? string.Empty;
        Description = description;
        IconText = iconText ?? string.Empty;
        Payload = payload;
    }

    public ChoiceKind Kind { get; }
    public string Id { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public string Description { get; }
    public string IconText { get; }
    public object? Payload { get; }
}

/// <summary>
/// Choice System 的公开类：ChoiceResult。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ChoiceResult
{
    /// <summary>
    /// Choice System 的公开入口：ChoiceResult。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public ChoiceResult(ChoiceOption? option, bool cancelled)
    {
        Option = option;
        Cancelled = cancelled;
    }

    public ChoiceOption? Option { get; }
    public bool Cancelled { get; }
    public ChoiceKind Kind => Option?.Kind ?? ChoiceKind.Generic;
    public string? Id => Option?.Id;
    public object? Payload => Option?.Payload;
}

/// <summary>
/// Choice System 的公开类：ChoiceRequest。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class ChoiceRequest
{
    public string Title { get; set; } = "请选择";
    public string Description { get; set; } = string.Empty;
    public string Footer { get; set; } = string.Empty;
    public string EmptyText { get; set; } = "没有可选择项。";
    public bool AllowCancel { get; set; }
    public string CancelText { get; set; } = "取消";
    public IReadOnlyList<ChoiceOption> Options { get; set; } = Array.Empty<ChoiceOption>();

    /// <summary>
    /// 阵营命运·改命 用：非null时，ChoicePanel 会在每个选项旁显示【刷新】按钮。
    /// 委托签名：(当前选项, 当前界面全部选项id列表) -> 若能合法重抽则返回新选项，否则返回null。
    /// 次数消耗/命运判定已经在 FactionFateManager.TryRerollChoiceOption 内部处理，
    /// ChoicePanel 侧不需要重复扣次数，也不需要知道具体是从哪个池子抽的。
    /// </summary>
    public Func<ChoiceOption, IReadOnlyList<string>, ChoiceOption?>? RerollOption { get; set; }
}

/// <summary>
/// Choice System 的公开接口：IChoiceProvider。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public interface IChoiceProvider
{
    IReadOnlyList<ChoiceOption> CreateChoices();
}

/// <summary>
/// Choice System 的公开枚举：ChipChoiceType。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum ChipChoiceType
{
    Attack,
    Defense,
    Knowledge,
    Expansion,
    Mysterious,
    Skill,
    Random
}

/// <summary>
/// Choice System 的公开枚举：InventoryChoiceScope。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public enum InventoryChoiceScope
{
    All,
    Equipped,
    Backpack
}
