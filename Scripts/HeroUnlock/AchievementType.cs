//////////////////////////////////////////////////////////
// 文件：Scripts/HeroUnlock/AchievementType.cs
//
// 模块：Hero Unlock System
//
// 为什么存在：
// "达成指定成就"这种解锁方式需要一个统一、可枚举的成就分类，
// 每一类成就对应 HeroUnlockProgress 里的一个累计计数器。
//
// 新增一种成就类型时（例如"击杀敌人总数"、"翻牌次数"），需要：
// 1. 在这里新增一个枚举值。
// 2. 在 HeroUnlockProgress 里新增对应的累计入口（Add 方法）。
// 3. 在合适的 Trigger 时机（通过 BattleRules.cs 注册一个新的 IBattleEffect，
//    或在 GameManager 已有的资源变化入口里追加一行）调用该累计入口。
// 这一步确实需要新增代码——"以后新增英雄不需要新增代码"指的是新增角色本身
// 只需要注册 HeroUnlockCondition，不是说成就统计逻辑本身可以纯配置产生
// （它天然需要知道"什么时候+1"，这必须是代码）。
//
// 主要依赖：
// 无
//////////////////////////////////////////////////////////

/// <summary>
/// 成就类型：每一项对应 <see cref="HeroUnlockProgress"/> 里的一个累计计数器。
///
/// 这次先落地四个示例成就（对应任务里给出的例子），验证"达成成就解锁英雄"
/// 这条链路；其余成就类型可以按同样的模式继续扩展，不需要改动
/// <see cref="HeroUnlockCondition"/>/<see cref="HeroUnlockDatabase"/> 的结构。
/// </summary>
public enum AchievementType
{
    /// <summary>累计获得装备数量（含所有品质、所有来源）。</summary>
    EquipmentAcquired,

    /// <summary>累计使用"杀"系攻击卡牌次数（普通杀/火杀/雷杀/必中杀/冰杀等）。</summary>
    KillCardsPlayed,

    /// <summary>累计恢复生命值总量。</summary>
    HealingDone,

    /// <summary>累计获得金币总量。</summary>
    GoldEarned,

    /// <summary>历史最高连续使用【费】的回合数。</summary>
    ConsecutiveFeeTurns
}
