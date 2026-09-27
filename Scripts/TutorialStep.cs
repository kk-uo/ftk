//////////////////////////////////////////////////////////
// 文件：Scripts/TutorialStep.cs
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

public enum TutorialAdvanceMode
{
    Button,
    Action,
    Manual
}

public enum HighlightTargetType
{
    None,
    CardOfType,
    Player,
    Enemy,
    UI
}

public sealed class TutorialHighlightTarget
{
    public HighlightTargetType Type { get; init; } = HighlightTargetType.None;
    public string TargetId { get; init; } = string.Empty;
}

public sealed class TutorialStep
{
    public string Id { get; init; } = string.Empty;
    public string TitleKey { get; init; } = string.Empty;
    public string DescriptionKey { get; init; } = string.Empty;
    public string ObjectiveKey { get; init; } = string.Empty;
    public string TipsKey { get; init; } = string.Empty;

    public TutorialHighlightTarget? Highlight { get; init; }
    public TutorialAdvanceMode AdvanceMode { get; init; } = TutorialAdvanceMode.Button;
    public string ActionTarget { get; init; } = string.Empty;
    public string NextStepId { get; init; } = string.Empty;

    public bool IsLastStep => string.IsNullOrEmpty(NextStepId);

    public string DisplayTitle => Localization.GetOrFallback(TitleKey, Id);
    public string DisplayDescription => Localization.GetOrFallback(DescriptionKey, string.Empty);
    public string DisplayObjective => Localization.GetOrFallback(ObjectiveKey, string.Empty);
    public string DisplayTips => Localization.GetOrFallback(TipsKey, string.Empty);
}
