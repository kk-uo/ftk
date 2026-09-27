//////////////////////////////////////////////////////////
// 文件：Scripts/TutorialDatabase.cs
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

using System.Collections.Generic;

/// <summary>
/// Tutorial System 的公开类：TutorialDatabase。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class TutorialDatabase
{
    /// <summary>
    /// Tutorial System 的公开类：StepIds。
    ///
    /// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
    /// </summary>
    public static class StepIds
    {
        // 首步：不再一次性罗列7个UI区域（原 ui_intro/char_detail 两步重复讲了
        // 两遍"查看双方信息"），改为单独一步讲清"双方同时出牌"这个核心机制
        // 本身——这是整场战斗最反直觉、之前从未被独立教过的规则。
        public const string SimultaneousPlay = "simultaneous_play";
        public const string CardFee          = "card_fee";
        public const string CardDodge        = "card_dodge";
        public const string CardKill         = "card_kill";
        public const string CardFireKill     = "card_firekill";
        public const string CardThunderKill  = "card_thunderkill";
        // 杀系克制关系复习：三个连续小回合演示完整环形克制（原 card_counters
        // 纯文字说明改造为真实演示），不再提"必中杀"。
        public const string CounterRoundA    = "counter_round_a";
        public const string CounterRoundB    = "counter_round_b";
        public const string CounterRoundC    = "counter_round_c";
        public const string CounterSummary   = "counter_summary";
        public const string CardPeach        = "card_peach";
        public const string CardWine         = "card_wine";
        public const string CardWineKill     = "card_wine_kill";
        public const string CardUnassailable = "card_unassailable";
        public const string FinalBattle      = "final_battle";

        /// <summary>
        /// 费用不足时的共享插入步骤：不计入 <see cref="OrderedStepIds"/>，
        /// 由 TutorialController 在需要时动态跳转过去，教完后再跳回原步骤。
        /// </summary>
        public const string NeedFee = "need_fee";
    }

    public static readonly string[] OrderedStepIds =
    {
        StepIds.SimultaneousPlay,
        StepIds.CardFee,
        StepIds.CardDodge,
        StepIds.CardKill,
        StepIds.CardFireKill,
        StepIds.CardThunderKill,
        StepIds.CounterRoundA,
        StepIds.CounterRoundB,
        StepIds.CounterRoundC,
        StepIds.CounterSummary,
        StepIds.CardPeach,
        StepIds.CardWine,
        StepIds.CardWineKill,
        StepIds.CardUnassailable,
        StepIds.FinalBattle
    };

    private static readonly Dictionary<string, TutorialStep> Steps = new()
    {
        [StepIds.SimultaneousPlay] = new TutorialStep
        {
            Id = StepIds.SimultaneousPlay,
            TitleKey = "tutorial.step.simultaneous_play.title",
            DescriptionKey = "tutorial.step.simultaneous_play.desc",
            ObjectiveKey = "tutorial.step.simultaneous_play.obj",
            Highlight = new TutorialHighlightTarget { Type = HighlightTargetType.Player },
            AdvanceMode = TutorialAdvanceMode.Button,
            NextStepId = StepIds.CardFee
        },
        [StepIds.CardFee] = new TutorialStep
        {
            Id = StepIds.CardFee,
            TitleKey = "tutorial.step.card_fee.title",
            DescriptionKey = "tutorial.step.card_fee.desc",
            ObjectiveKey = "tutorial.step.card_fee.obj",
            Highlight = new TutorialHighlightTarget
            {
                Type = HighlightTargetType.CardOfType,
                TargetId = nameof(CardType.Fee)
            },
            AdvanceMode = TutorialAdvanceMode.Action,
            ActionTarget = nameof(CardType.Fee),
            NextStepId = StepIds.CardDodge
        },
        [StepIds.CardDodge] = new TutorialStep
        {
            Id = StepIds.CardDodge,
            TitleKey = "tutorial.step.card_dodge.title",
            DescriptionKey = "tutorial.step.card_dodge.desc",
            ObjectiveKey = "tutorial.step.card_dodge.obj",
            Highlight = new TutorialHighlightTarget
            {
                Type = HighlightTargetType.CardOfType,
                TargetId = nameof(CardType.Dodge)
            },
            AdvanceMode = TutorialAdvanceMode.Action,
            ActionTarget = nameof(CardType.Dodge),
            NextStepId = StepIds.CardKill
        },
        [StepIds.CardKill] = new TutorialStep
        {
            Id = StepIds.CardKill,
            TitleKey = "tutorial.step.card_kill.title",
            DescriptionKey = "tutorial.step.card_kill.desc",
            ObjectiveKey = "tutorial.step.card_kill.obj",
            Highlight = new TutorialHighlightTarget
            {
                Type = HighlightTargetType.CardOfType,
                TargetId = nameof(CardType.Kill)
            },
            AdvanceMode = TutorialAdvanceMode.Action,
            ActionTarget = nameof(CardType.Kill),
            NextStepId = StepIds.CardFireKill
        },
        [StepIds.CardFireKill] = new TutorialStep
        {
            Id = StepIds.CardFireKill,
            TitleKey = "tutorial.step.card_firekill.title",
            DescriptionKey = "tutorial.step.card_firekill.desc",
            ObjectiveKey = "tutorial.step.card_firekill.obj",
            Highlight = new TutorialHighlightTarget
            {
                Type = HighlightTargetType.CardOfType,
                TargetId = nameof(CardType.FireKill)
            },
            AdvanceMode = TutorialAdvanceMode.Action,
            ActionTarget = nameof(CardType.FireKill),
            NextStepId = StepIds.CardThunderKill
        },
        [StepIds.CardThunderKill] = new TutorialStep
        {
            Id = StepIds.CardThunderKill,
            TitleKey = "tutorial.step.card_thunderkill.title",
            DescriptionKey = "tutorial.step.card_thunderkill.desc",
            ObjectiveKey = "tutorial.step.card_thunderkill.obj",
            Highlight = new TutorialHighlightTarget
            {
                Type = HighlightTargetType.CardOfType,
                TargetId = nameof(CardType.ThunderKill)
            },
            AdvanceMode = TutorialAdvanceMode.Action,
            ActionTarget = nameof(CardType.ThunderKill),
            NextStepId = StepIds.CounterRoundA
        },
        // 杀系克制关系复习：三个连续小回合真实演示完整环形克制（火杀克制杀→
        // 杀克制雷杀→雷杀克制火杀），每一步都要求玩家真的打出对应的牌，而不是
        // 只读一段文字说明；克制/被克制的结果由真实伤害结算给出，不额外弹字幕。
        [StepIds.CounterRoundA] = new TutorialStep
        {
            Id = StepIds.CounterRoundA,
            TitleKey = "tutorial.step.counter_round_a.title",
            DescriptionKey = "tutorial.step.counter_round_a.desc",
            ObjectiveKey = "tutorial.step.counter_round_a.obj",
            Highlight = new TutorialHighlightTarget
            {
                Type = HighlightTargetType.CardOfType,
                TargetId = nameof(CardType.FireKill)
            },
            AdvanceMode = TutorialAdvanceMode.Action,
            ActionTarget = nameof(CardType.FireKill),
            NextStepId = StepIds.CounterRoundB
        },
        [StepIds.CounterRoundB] = new TutorialStep
        {
            Id = StepIds.CounterRoundB,
            TitleKey = "tutorial.step.counter_round_b.title",
            DescriptionKey = "tutorial.step.counter_round_b.desc",
            ObjectiveKey = "tutorial.step.counter_round_b.obj",
            Highlight = new TutorialHighlightTarget
            {
                Type = HighlightTargetType.CardOfType,
                TargetId = nameof(CardType.Kill)
            },
            AdvanceMode = TutorialAdvanceMode.Action,
            ActionTarget = nameof(CardType.Kill),
            NextStepId = StepIds.CounterRoundC
        },
        [StepIds.CounterRoundC] = new TutorialStep
        {
            Id = StepIds.CounterRoundC,
            TitleKey = "tutorial.step.counter_round_c.title",
            DescriptionKey = "tutorial.step.counter_round_c.desc",
            ObjectiveKey = "tutorial.step.counter_round_c.obj",
            Highlight = new TutorialHighlightTarget
            {
                Type = HighlightTargetType.CardOfType,
                TargetId = nameof(CardType.ThunderKill)
            },
            AdvanceMode = TutorialAdvanceMode.Action,
            ActionTarget = nameof(CardType.ThunderKill),
            NextStepId = StepIds.CounterSummary
        },
        // 纯讲解收尾：三组回合演示完之后，用一句话总结整个环形关系，点击继续
        // 直接进入下一课。不提"必中杀"——这张牌全程从未被正式教过。
        [StepIds.CounterSummary] = new TutorialStep
        {
            Id = StepIds.CounterSummary,
            TitleKey = "tutorial.step.counter_summary.title",
            DescriptionKey = "tutorial.step.counter_summary.desc",
            ObjectiveKey = "tutorial.step.counter_summary.obj",
            AdvanceMode = TutorialAdvanceMode.Button,
            NextStepId = StepIds.CardPeach
        },
        [StepIds.CardPeach] = new TutorialStep
        {
            Id = StepIds.CardPeach,
            TitleKey = "tutorial.step.card_peach.title",
            DescriptionKey = "tutorial.step.card_peach.desc",
            ObjectiveKey = "tutorial.step.card_peach.obj",
            TipsKey = "tutorial.step.card_peach.tips",
            Highlight = new TutorialHighlightTarget
            {
                Type = HighlightTargetType.CardOfType,
                TargetId = nameof(CardType.Peach)
            },
            AdvanceMode = TutorialAdvanceMode.Action,
            ActionTarget = nameof(CardType.Peach),
            NextStepId = StepIds.CardWine
        },
        [StepIds.CardWine] = new TutorialStep
        {
            Id = StepIds.CardWine,
            TitleKey = "tutorial.step.card_wine.title",
            DescriptionKey = "tutorial.step.card_wine.desc",
            ObjectiveKey = "tutorial.step.card_wine.obj",
            Highlight = new TutorialHighlightTarget
            {
                Type = HighlightTargetType.CardOfType,
                TargetId = nameof(CardType.Wine)
            },
            AdvanceMode = TutorialAdvanceMode.Action,
            ActionTarget = nameof(CardType.Wine),
            NextStepId = StepIds.CardWineKill
        },
        [StepIds.CardWineKill] = new TutorialStep
        {
            Id = StepIds.CardWineKill,
            TitleKey = "tutorial.step.card_wine_kill.title",
            DescriptionKey = "tutorial.step.card_wine_kill.desc",
            ObjectiveKey = "tutorial.step.card_wine_kill.obj",
            Highlight = new TutorialHighlightTarget
            {
                Type = HighlightTargetType.CardOfType,
                TargetId = nameof(CardType.Kill)
            },
            AdvanceMode = TutorialAdvanceMode.Action,
            ActionTarget = nameof(CardType.Kill),
            NextStepId = StepIds.CardUnassailable
        },
        [StepIds.CardUnassailable] = new TutorialStep
        {
            Id = StepIds.CardUnassailable,
            TitleKey = "tutorial.step.card_unassailable.title",
            DescriptionKey = "tutorial.step.card_unassailable.desc",
            ObjectiveKey = "tutorial.step.card_unassailable.obj",
            Highlight = new TutorialHighlightTarget
            {
                Type = HighlightTargetType.CardOfType,
                TargetId = nameof(CardType.Unassailable)
            },
            AdvanceMode = TutorialAdvanceMode.Action,
            ActionTarget = nameof(CardType.Unassailable),
            NextStepId = StepIds.FinalBattle
        },
        [StepIds.FinalBattle] = new TutorialStep
        {
            Id = StepIds.FinalBattle,
            TitleKey = "tutorial.step.final_battle.title",
            // 修复此前遗留的死文案bug：localization里早就写好了完整的总结文案和
            // 提示文案（tutorial.step.final_battle.desc/.tips），但这两个Key从未
            // 被接到这一步上，玩家进入综合练习时实际什么提示都看不到。
            DescriptionKey = "tutorial.step.final_battle.desc",
            ObjectiveKey = "tutorial.step.final_battle.obj",
            TipsKey = "tutorial.step.final_battle.tips",
            AdvanceMode = TutorialAdvanceMode.Manual,
            NextStepId = string.Empty
        },
        [StepIds.NeedFee] = new TutorialStep
        {
            Id = StepIds.NeedFee,
            TitleKey = "tutorial.step.need_fee.title",
            DescriptionKey = "tutorial.step.need_fee.desc",
            ObjectiveKey = "tutorial.step.need_fee.obj",
            Highlight = new TutorialHighlightTarget
            {
                Type = HighlightTargetType.CardOfType,
                TargetId = nameof(CardType.Fee)
            },
            AdvanceMode = TutorialAdvanceMode.Manual,
            NextStepId = string.Empty
        }
    };

    /// <summary>
    /// Tutorial System 的公开入口：GetStep。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static TutorialStep? GetStep(string stepId) =>
        Steps.TryGetValue(stepId, out var step) ? step : null;

    public static int TotalSteps => OrderedStepIds.Length;

    /// <summary>
    /// Tutorial System 的公开入口：GetStepIndex。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetStepIndex(string stepId)
    {
        for (var i = 0; i < OrderedStepIds.Length; i++)
            if (OrderedStepIds[i] == stepId) return i;
        return -1;
    }

    /// <summary>
    /// Tutorial System 的公开入口：GetFirstStepId。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string? GetFirstStepId() =>
        OrderedStepIds.Length > 0 ? OrderedStepIds[0] : null;
}
