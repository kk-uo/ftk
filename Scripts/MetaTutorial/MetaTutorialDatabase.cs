//////////////////////////////////////////////////////////
// 文件：Scripts/MetaTutorial/MetaTutorialDatabase.cs
//
// 模块：Tutorial System（战斗外教程）
//
// 职责：
// 1. 承载"战斗外教程"（地图/电量/阵营命运/初始事件/章节结构）的步骤数据，
//    镜像 Scripts/TutorialDatabase.cs 的形状（OrderedStepIds + Steps 字典 +
//    TotalSteps + GetStep），但内容完全独立，不与战斗教程共享任何步骤。
// 2. 供 TutorialManager.StartMetaTutorial()/AdvanceToNext() 等在
//    CurrentModuleId == TutorialModuleIds.Meta 时查询。
//
// 不负责：
// × 战斗教程内容（Scripts/TutorialDatabase.cs，本文件不修改也不复用它）。
// × UI 渲染（由 MetaTutorialController 负责）。
//////////////////////////////////////////////////////////

using System.Collections.Generic;

public static class MetaTutorialDatabase
{
    public static class StepIds
    {
        public const string Intro                 = "meta_intro";
        public const string MapStructure           = "meta_map_structure";
        public const string NodeTypes              = "meta_node_types";
        public const string AfterElite             = "meta_after_elite";
        public const string Energy                 = "meta_energy";
        public const string EnergyInsufficient     = "meta_energy_insufficient";
        public const string FactionDestiny         = "meta_faction_destiny";
        public const string InitialEvent           = "meta_initial_event";
        public const string VariantChapter         = "meta_variant_chapter";
        public const string RunGoal                = "meta_run_goal";
        public const string Complete               = "meta_complete";
    }

    public static readonly string[] OrderedStepIds =
    {
        StepIds.Intro,
        StepIds.MapStructure,
        StepIds.NodeTypes,
        StepIds.AfterElite,
        StepIds.Energy,
        StepIds.EnergyInsufficient,
        StepIds.FactionDestiny,
        StepIds.InitialEvent,
        StepIds.VariantChapter,
        StepIds.RunGoal,
        StepIds.Complete
    };

    private static readonly Dictionary<string, TutorialStep> Steps = new()
    {
        [StepIds.Intro] = new TutorialStep
        {
            Id = StepIds.Intro,
            TitleKey = "tutorial.meta.step.meta_intro.title",
            DescriptionKey = "tutorial.meta.step.meta_intro.desc",
            Highlight = new TutorialHighlightTarget { Type = HighlightTargetType.UI, TargetId = "chapter_name" },
            AdvanceMode = TutorialAdvanceMode.Button,
            NextStepId = StepIds.MapStructure
        },
        [StepIds.MapStructure] = new TutorialStep
        {
            Id = StepIds.MapStructure,
            TitleKey = "tutorial.meta.step.meta_map_structure.title",
            DescriptionKey = "tutorial.meta.step.meta_map_structure.desc",
            Highlight = new TutorialHighlightTarget { Type = HighlightTargetType.UI, TargetId = "node_chain" },
            AdvanceMode = TutorialAdvanceMode.Button,
            NextStepId = StepIds.NodeTypes
        },
        [StepIds.NodeTypes] = new TutorialStep
        {
            Id = StepIds.NodeTypes,
            TitleKey = "tutorial.meta.step.meta_node_types.title",
            DescriptionKey = "tutorial.meta.step.meta_node_types.desc",
            ObjectiveKey = "tutorial.meta.step.meta_node_types.obj",
            Highlight = new TutorialHighlightTarget { Type = HighlightTargetType.UI, TargetId = "node_battle_1" },
            AdvanceMode = TutorialAdvanceMode.Button,
            NextStepId = StepIds.AfterElite
        },
        // 关键演示步骤：不显示"下一步"按钮，玩家必须点击模拟的分支节点之一才能推进——
        // 用 Manual 模式 + MetaTutorialController 在点击回调里直接调用 AdvanceToNext()。
        [StepIds.AfterElite] = new TutorialStep
        {
            Id = StepIds.AfterElite,
            TitleKey = "tutorial.meta.step.meta_after_elite.title",
            DescriptionKey = "tutorial.meta.step.meta_after_elite.desc",
            ObjectiveKey = "tutorial.meta.step.meta_after_elite.obj",
            Highlight = new TutorialHighlightTarget { Type = HighlightTargetType.UI, TargetId = "post_elite_branches" },
            AdvanceMode = TutorialAdvanceMode.Manual,
            NextStepId = StepIds.Energy
        },
        [StepIds.Energy] = new TutorialStep
        {
            Id = StepIds.Energy,
            TitleKey = "tutorial.meta.step.meta_energy.title",
            DescriptionKey = "tutorial.meta.step.meta_energy.desc",
            ObjectiveKey = "tutorial.meta.step.meta_energy.obj",
            Highlight = new TutorialHighlightTarget { Type = HighlightTargetType.UI, TargetId = "power_bar" },
            AdvanceMode = TutorialAdvanceMode.Manual,
            NextStepId = StepIds.EnergyInsufficient
        },
        [StepIds.EnergyInsufficient] = new TutorialStep
        {
            Id = StepIds.EnergyInsufficient,
            TitleKey = "tutorial.meta.step.meta_energy_insufficient.title",
            DescriptionKey = "tutorial.meta.step.meta_energy_insufficient.desc",
            ObjectiveKey = "tutorial.meta.step.meta_energy_insufficient.obj",
            Highlight = new TutorialHighlightTarget { Type = HighlightTargetType.UI, TargetId = "power_insufficient_node" },
            AdvanceMode = TutorialAdvanceMode.Manual,
            NextStepId = StepIds.FactionDestiny
        },
        // 关键演示步骤：点击任意阵营卡片才能推进（Manual 模式）。
        [StepIds.FactionDestiny] = new TutorialStep
        {
            Id = StepIds.FactionDestiny,
            TitleKey = "tutorial.meta.step.meta_faction_destiny.title",
            DescriptionKey = "tutorial.meta.step.meta_faction_destiny.desc",
            TipsKey = "tutorial.meta.step.meta_faction_destiny.tips",
            Highlight = new TutorialHighlightTarget { Type = HighlightTargetType.UI, TargetId = "faction_fate_preview" },
            AdvanceMode = TutorialAdvanceMode.Manual,
            NextStepId = StepIds.InitialEvent
        },
        // 关键演示步骤：点击任意一个模拟事件选项才能推进（Manual 模式）。
        [StepIds.InitialEvent] = new TutorialStep
        {
            Id = StepIds.InitialEvent,
            TitleKey = "tutorial.meta.step.meta_initial_event.title",
            DescriptionKey = "tutorial.meta.step.meta_initial_event.desc",
            Highlight = new TutorialHighlightTarget { Type = HighlightTargetType.UI, TargetId = "initial_event_preview" },
            AdvanceMode = TutorialAdvanceMode.Manual,
            NextStepId = StepIds.VariantChapter
        },
        [StepIds.VariantChapter] = new TutorialStep
        {
            Id = StepIds.VariantChapter,
            TitleKey = "tutorial.meta.step.meta_variant_chapter.title",
            DescriptionKey = "tutorial.meta.step.meta_variant_chapter.desc",
            Highlight = new TutorialHighlightTarget { Type = HighlightTargetType.UI, TargetId = "chapter_cards" },
            AdvanceMode = TutorialAdvanceMode.Button,
            NextStepId = StepIds.RunGoal
        },
        [StepIds.RunGoal] = new TutorialStep
        {
            Id = StepIds.RunGoal,
            TitleKey = "tutorial.meta.step.meta_run_goal.title",
            DescriptionKey = "tutorial.meta.step.meta_run_goal.desc",
            TipsKey = "tutorial.meta.step.meta_run_goal.tips",
            Highlight = new TutorialHighlightTarget { Type = HighlightTargetType.UI, TargetId = "full_map" },
            AdvanceMode = TutorialAdvanceMode.Button,
            NextStepId = StepIds.Complete
        },
        [StepIds.Complete] = new TutorialStep
        {
            Id = StepIds.Complete,
            TitleKey = "tutorial.meta.step.meta_complete.title",
            DescriptionKey = "tutorial.meta.step.meta_complete.desc",
            AdvanceMode = TutorialAdvanceMode.Button,
            NextStepId = string.Empty
        }
    };

    public static TutorialStep? GetStep(string stepId) =>
        Steps.TryGetValue(stepId, out var step) ? step : null;

    public static int TotalSteps => OrderedStepIds.Length;

    public static int GetStepIndex(string stepId)
    {
        for (var i = 0; i < OrderedStepIds.Length; i++)
            if (OrderedStepIds[i] == stepId) return i;
        return -1;
    }

    public static string? GetFirstStepId() =>
        OrderedStepIds.Length > 0 ? OrderedStepIds[0] : null;
}
