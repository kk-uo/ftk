//////////////////////////////////////////////////////////
// 文件：Scripts/TutorialManager.cs
//
// 模块：Tutorial System
//
// 职责：
// 1. 承载教程步骤、教程事件与教学流程相关代码。
// 2. 为其它模块提供清晰、稳定的调用边界。
// 3. 保持本文件内的状态变化可追踪、可调试。
// 4. 同时驱动两个独立教程模块：战斗教程（TutorialDatabase）与
//    战斗外教程（MetaTutorialDatabase），由 CurrentModuleId 区分，
//    所有涉及"按 stepId 查库"的地方都要按 CurrentModuleId 分流。
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
/// Tutorial System 的公开类：TutorialManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class TutorialManager
{
    public static bool IsActive { get; private set; }
    public static bool IsCompleted { get; private set; }
    public static TutorialStep? CurrentStep { get; private set; }
    public static int CurrentStepIndex { get; private set; }

    /// <summary>
    /// 费用不足跳转到 NeedFee 插入步骤时，本来要教的那张卡牌的 CardType 名称
    /// （如 "FireKill"），供 TutorialOverlay 在 NeedFee 步骤里显示"先用费，
    /// 再用XX"这类具体提示；由 TutorialController 在跳转前后写入/清空。
    /// </summary>
    public static string? PendingFeeLessonTarget { get; set; }

    /// <summary>
    /// 当前激活的教程模块（<see cref="TutorialModuleIds.Combat"/> 或
    /// <see cref="TutorialModuleIds.Meta"/>），驱动 AdvanceToNext/JumpToStep/
    /// TotalSteps 等应该查询哪一个 Database。教程未激活时为 null。
    /// </summary>
    public static string? CurrentModuleId { get; private set; }

    public static int TotalSteps => CurrentModuleId == TutorialModuleIds.Meta
        ? MetaTutorialDatabase.TotalSteps
        : TutorialDatabase.TotalSteps;

    public static bool CanGoPrevious => _stepHistory.Count > 0;

    public static event Action<TutorialStep?>? StepChanged;
    public static event Action? TutorialCompleted;

    private static readonly Stack<string> _stepHistory = new();

    // ─── Public API ──────────────────────────────────────────────────────

    /// <summary>
    /// 向后兼容入口：等价于 <see cref="StartCombatTutorial"/>。
    ///
    /// TutorialOverlay.cs（战斗教程专属表现层，本次改造被冻结、不允许修改）的
    /// OnRetryPressed() 仍然调用的是这个方法名，因此这里保留一个同名方法作为
    /// 薄包装，行为与 StartCombatTutorial() 完全一致。新代码应直接调用
    /// StartCombatTutorial()。
    /// </summary>
    public static void StartTutorial() => StartCombatTutorial();

    /// <summary>
    /// 开始"战斗教程"（原 StartTutorial()，内容/行为不变），从
    /// <see cref="TutorialDatabase"/> 读取步骤链。
    /// </summary>
    public static void StartCombatTutorial()
    {
        if (IsActive || IsCompleted) return;

        _stepHistory.Clear();
        var firstId = TutorialDatabase.GetFirstStepId();
        if (firstId == null) return;

        CurrentModuleId = TutorialModuleIds.Combat;
        IsActive = true;
        TutorialEventBus.CardPlayed += OnCardPlayed;
        SetStep(TutorialDatabase.GetStep(firstId));
    }

    /// <summary>
    /// 开始"战斗外教程"，从 <see cref="MetaTutorialDatabase"/> 读取步骤链。
    /// 不订阅 TutorialEventBus.CardPlayed——战斗外教程没有任何 Action 推进模式
    /// 的步骤（全部是 Button/Manual），不需要监听真实出牌事件。
    /// </summary>
    public static void StartMetaTutorial()
    {
        if (IsActive || IsCompleted) return;

        _stepHistory.Clear();
        var firstId = MetaTutorialDatabase.GetFirstStepId();
        if (firstId == null) return;

        CurrentModuleId = TutorialModuleIds.Meta;
        IsActive = true;
        SetStep(MetaTutorialDatabase.GetStep(firstId));
    }

    /// <summary>
    /// Tutorial System 的公开入口：AdvanceToNext。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void AdvanceToNext()
    {
        if (!IsActive || CurrentStep == null) return;

        _stepHistory.Push(CurrentStep.Id);

        var nextId = CurrentStep.NextStepId;
        if (string.IsNullOrEmpty(nextId))
        {
            Complete();
            return;
        }

        var next = GetStepFromCurrentModule(nextId);
        if (next == null)
        {
            Complete();
            return;
        }

        SetStep(next);
    }

    /// <summary>
    /// Tutorial System 的公开入口：PreviousStep。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void PreviousStep()
    {
        if (!IsActive || _stepHistory.Count == 0) return;
        SetStep(GetStepFromCurrentModule(_stepHistory.Pop()));
    }

    /// <summary>
    /// Tutorial System 的公开入口：Restart。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void Restart()
    {
        if (!IsActive) return;
        _stepHistory.Clear();
        var firstId = CurrentModuleId == TutorialModuleIds.Meta
            ? MetaTutorialDatabase.GetFirstStepId()
            : TutorialDatabase.GetFirstStepId();
        if (firstId != null)
            SetStep(GetStepFromCurrentModule(firstId));
    }

    /// <summary>
    /// Tutorial System 的公开入口：JumpToStep。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void JumpToStep(string stepId)
    {
        if (!IsActive) return;
        var step = GetStepFromCurrentModule(stepId);
        if (step == null) return;
        _stepHistory.Clear();
        SetStep(step);
    }

    /// <summary>
    /// Tutorial System 的公开入口：SkipTutorial。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SkipTutorial()
    {
        TutorialEventBus.CardPlayed -= OnCardPlayed;
        MarkModuleCompletedInProgress();
        IsActive = false;
        IsCompleted = true;
        CurrentStep = null;
        _stepHistory.Clear();
        StepChanged?.Invoke(null);
    }

    /// <summary>
    /// Tutorial System 的公开入口：ResetForNewRun。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void ResetForNewRun()
    {
        TutorialEventBus.CardPlayed -= OnCardPlayed;
        IsActive = false;
        IsCompleted = false;
        CurrentStep = null;
        CurrentModuleId = null;
        _stepHistory.Clear();
    }

    // ─── Private ─────────────────────────────────────────────────────────

    private static TutorialStep? GetStepFromCurrentModule(string stepId) =>
        CurrentModuleId == TutorialModuleIds.Meta
            ? MetaTutorialDatabase.GetStep(stepId)
            : TutorialDatabase.GetStep(stepId);

    private static void SetStep(TutorialStep? step)
    {
        CurrentStep = step;
        CurrentStepIndex = step != null
            ? (CurrentModuleId == TutorialModuleIds.Meta
                ? MetaTutorialDatabase.GetStepIndex(step.Id)
                : TutorialDatabase.GetStepIndex(step.Id))
            : 0;
        StepChanged?.Invoke(step);
    }

    private static void OnCardPlayed(CardType type)
    {
        if (!IsActive || CurrentStep == null) return;
        if (CurrentStep.AdvanceMode != TutorialAdvanceMode.Action) return;
        if (type.ToString() != CurrentStep.ActionTarget) return;
        AdvanceToNext();
    }

    private static void Complete()
    {
        TutorialEventBus.CardPlayed -= OnCardPlayed;
        MarkModuleCompletedInProgress();
        IsActive = false;
        IsCompleted = true;
        CurrentStep = null;
        TutorialCompleted?.Invoke();
        StepChanged?.Invoke(null);
    }

    /// <summary>
    /// 教程真正结束时（完成或跳过），额外把这次结束持久化到
    /// <see cref="TutorialProgress"/>——只在这里追加持久化调用，不改变
    /// IsCompleted 本身的既有语义（仍然只表示"本次内存态刚完成"，供
    /// TutorialOverlay 等既有 UI 逻辑判断"是否刚完成"用）。
    /// </summary>
    private static void MarkModuleCompletedInProgress()
    {
        if (CurrentModuleId == TutorialModuleIds.Meta)
        {
            TutorialProgress.MarkMetaCompleted();
        }
        else if (CurrentModuleId == TutorialModuleIds.Combat)
        {
            TutorialProgress.MarkCombatCompleted();
        }
    }
}
