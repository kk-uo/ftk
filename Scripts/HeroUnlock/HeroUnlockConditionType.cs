//////////////////////////////////////////////////////////
// 文件：Scripts/HeroUnlock/HeroUnlockConditionType.cs
//
// 模块：Hero Unlock System
//
// 为什么存在：
// 以后所有英雄只允许通过三种正式方式解锁：击败指定敌人、完成指定事件、
// 达成指定成就——不再使用"通关第几章"这种模糊、无法在 UI 上展示具体进度的
// 解锁方式。这个枚举把"解锁方式"本身变成一个可以被 UnlockCondition 引用的
// 稳定分类，不需要为每种方式写专属代码路径。
//
// 主要依赖：
// 无
//////////////////////////////////////////////////////////

/// <summary>
/// 英雄解锁条件的类型。
///
/// 只描述"这是哪一种条件"，具体目标（打谁/完成哪个事件/达成什么成就）
/// 由 <see cref="HeroUnlockCondition.TargetId"/> 决定。
/// </summary>
public enum HeroUnlockConditionType
{
    /// <summary>击败指定敌人（TargetId = 敌人 Id，例如 "boss_healer"）。</summary>
    KillEnemy,

    /// <summary>完成指定事件（TargetId = 事件 Id，例如 "ruler_statue"）。</summary>
    CompleteEvent,

    /// <summary>达成指定成就（TargetId = <see cref="AchievementType"/> 的字符串名）。</summary>
    Achievement
}
