//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/CharacterAnimationState.cs
//
// 模块：Presentation System / Combat Visual Profile System
//
// 为什么存在：
// 角色动作（待机/攻击/防御/受击/死亡/胜利/技能）不应该写死在 Battle 或
// Presenter 里用字符串比较，用一个枚举描述"角色现在应该处于哪种动作状态"，
// 配合 CharacterVisualDatabase.ResolveAnimationId 统一解析成 AnimationDatabase
// 里的 AnimationId。
//
// 主要依赖：
// 无
//////////////////////////////////////////////////////////

/// <summary>
/// 角色动作状态。用于向 CharacterVisualDatabase 查询该状态对应的动画 Id，
/// 不代表状态机本身的切换逻辑（切换时机仍由 Battle/Presenter 决定）。
/// </summary>
public enum CharacterAnimationState
{
    Idle,
    Attack,
    Defense,
    Hit,
    /// <summary>预留，当前没有 Presenter 读取该状态对应的动画。</summary>
    Death,
    /// <summary>预留，当前没有 Presenter 读取该状态对应的动画。</summary>
    Victory,
    /// <summary>预留，当前没有 Presenter 读取该状态对应的动画。</summary>
    Skill
}
