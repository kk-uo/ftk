//////////////////////////////////////////////////////////
// 文件：Scripts/TutorialModuleIds.cs
//
// 模块：Tutorial System
//
// 职责：
// 1. 区分"战斗教程"与"战斗外教程"两个独立可进入的教程模块。
// 2. 供 TutorialManager.CurrentModuleId 使用，决定当前该从
//    TutorialDatabase 还是 MetaTutorialDatabase 里查询步骤。
//////////////////////////////////////////////////////////

public static class TutorialModuleIds
{
    public const string Combat = "tutorial_combat";
    public const string Meta = "tutorial_meta";
    public const string Integrated = "tutorial_integrated";
}
