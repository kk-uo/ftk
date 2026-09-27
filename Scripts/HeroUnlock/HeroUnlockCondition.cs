//////////////////////////////////////////////////////////
// 文件：Scripts/HeroUnlock/HeroUnlockCondition.cs
//
// 模块：Hero Unlock System
//
// 为什么存在：
// 一个英雄的解锁条件不应该写死在角色选择界面或某个 if 分支里。
// HeroUnlockCondition 把"用什么方式解锁、目标是谁、需要多少"收敛成一份数据，
// 新增一个英雄的解锁条件只需要注册一条这样的数据。
//
// 职责：
// 1. 描述一条解锁条件：类型（击败敌人/完成事件/达成成就）+ 目标 + 数量。
// 2. 提供统一的 Description，UI 直接显示，不需要各自拼接文案。
//
// 不负责：
// × 判断条件是否已经满足（由 HeroUnlockProgress 负责）。
// × 保存进度（由 HeroUnlockProgress 负责）。
// × 角色数据本身（不引用 CharacterData/CharacterDatabase）。
//
// 主要依赖：
// HeroUnlockConditionType / AchievementType
//////////////////////////////////////////////////////////

/// <summary>
/// 一条英雄解锁条件。
///
/// 通过 <see cref="HeroUnlockDatabase"/> 按角色 Id 注册/查询，不在角色数据
/// （CharacterData/CharacterDatabase）上新增任何字段。
/// </summary>
public sealed class HeroUnlockCondition
{
    /// <summary>
    /// 创建一条解锁条件。
    /// </summary>
    /// <param name="conditionType">解锁方式。</param>
    /// <param name="targetId">
    /// 目标 Id：KillEnemy 时是敌人 Id（EnemyDefinition.Id），CompleteEvent 时是事件 Id
    /// （EventData.Id），Achievement 时是 <see cref="AchievementType"/> 枚举值的字符串名
    /// （例如 "EquipmentAcquired"）。
    /// </param>
    /// <param name="requiredAmount">
    /// 需要达到的数量：KillEnemy/CompleteEvent 通常是 1（达成一次即可）；
    /// Achievement 是具体的达成阈值（例如 15 件装备、100 次杀）。
    /// </param>
    /// <param name="description">
    /// 展示文案（例如"击败【医者】"、"累计获得15件装备"），角色选择界面直接显示，
    /// 不需要根据 ConditionType 再拼一遍文字。
    /// </param>
    public HeroUnlockCondition(
        HeroUnlockConditionType conditionType,
        string targetId,
        int requiredAmount,
        string description,
        string? groupId = null,
        string? groupAnnouncementTitle = null,
        string? descriptionKey = null,
        string? groupAnnouncementTitleKey = null)
    {
        ConditionType = conditionType;
        TargetId = targetId;
        RequiredAmount = requiredAmount;
        Description = description;
        GroupId = groupId;
        GroupAnnouncementTitle = groupAnnouncementTitle;
        DescriptionKey = descriptionKey;
        GroupAnnouncementTitleKey = groupAnnouncementTitleKey;
    }

    /// <summary>解锁方式：击败敌人 / 完成事件 / 达成成就。</summary>
    public HeroUnlockConditionType ConditionType { get; }

    /// <summary>
    /// 目标 Id，含义随 <see cref="ConditionType"/> 变化，见构造函数说明。
    /// KillEnemy 支持用 <c>|</c> 分隔多个敌人 Id（例如共享血量池的多体 Boss，
    /// 一次战斗里可能有多个 EnemyInstance 各自触发死亡记录），
    /// <see cref="HeroUnlockProgress.EvaluateCondition"/> 会取这些 Id 里击败次数的最大值。
    /// </summary>
    public string TargetId { get; }

    /// <summary>需要达到的数量。</summary>
    public int RequiredAmount { get; }

    /// <summary>展示文案，角色选择界面直接使用。</summary>
    public string Description { get; }

    /// <summary>Optional localization key for <see cref="Description"/>.</summary>
    public string? DescriptionKey { get; }

    /// <summary>Localized condition text for player-facing UI.</summary>
    public string DisplayDescription => Localization.GetOrFallback(DescriptionKey, Description);

    /// <summary>
    /// 分组 Id（可选）：多个角色共用同一个解锁条件、且希望"同时解锁时只弹一次
    /// 合并公告"（例如刘备/关羽/张飞共用【蜀汉共生体】），把它们的 GroupId
    /// 设成同一个字符串即可；<see cref="HeroUnlockProgress"/> 会在它们于同一次
    /// 判定里一起达成条件时，合并成一条待展示公告，而不是分别弹三次。
    /// 留空（默认）表示这个角色的解锁公告始终单独展示。
    /// </summary>
    public string? GroupId { get; }

    /// <summary>
    /// 分组解锁公告的标题（仅在 <see cref="GroupId"/> 非空时使用），
    /// 例如"蜀汉三兄弟已解锁"。同一个 GroupId 下的角色应该配置相同的标题。
    /// </summary>
    public string? GroupAnnouncementTitle { get; }

    /// <summary>Optional localization key for <see cref="GroupAnnouncementTitle"/>.</summary>
    public string? GroupAnnouncementTitleKey { get; }

    /// <summary>Localized group announcement title for player-facing UI.</summary>
    public string DisplayGroupAnnouncementTitle => Localization.GetOrFallback(GroupAnnouncementTitleKey, GroupAnnouncementTitle ?? string.Empty);
}
