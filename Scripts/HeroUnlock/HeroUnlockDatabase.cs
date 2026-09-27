//////////////////////////////////////////////////////////
// 文件：Scripts/HeroUnlock/HeroUnlockDatabase.cs
//
// 模块：Hero Unlock System
//
// 为什么存在：
// 每个英雄的解锁条件需要一个统一的注册表，角色选择界面按角色 Id 查询即可，
// 不需要为每个角色写 if/switch 判断解锁方式。
//
// 职责：
// 1. 按角色 Id（CharacterIds.X，和既有 CharacterData.Id 保持一致）注册/查询
//    HeroUnlockCondition。
// 2. 没有注册条件的角色视为"一直可选"（向后兼容默认值）——只有下面明确列出的
//    角色受这次改动影响，其余角色（包括默认出场角色赵云等）完全不受影响。
//
// 不负责：
// × 判断条件是否已经满足（由 HeroUnlockProgress 负责）。
// × 角色数据本身（不修改 CharacterData/CharacterDatabase 已有字段；本次任务
//   新增的刘备/关羽两个角色条目在 Character.cs 里维护，不在这个文件）。
//
// 主要依赖：
// HeroUnlockCondition / CharacterIds
//////////////////////////////////////////////////////////

using System.Collections.Generic;

/// <summary>
/// 英雄解锁条件数据库：角色 Id → HeroUnlockCondition。
///
/// 以后新增一个需要解锁的英雄，只需要调用 <see cref="Register"/> 注册一条条件，
/// 不需要修改 CharacterSelectController、HeroUnlockProgress 或本类的任何其它代码
/// ——这是 Hero Unlock System v1 的核心设计目标：新增英雄只配置，不新增代码。
/// </summary>
public static class HeroUnlockDatabase
{
    // 刘备/关羽/张飞共用同一个 GroupId：三人在同一次判定里一起达成条件时，
    // HeroUnlockProgress 只会入队一条合并公告，而不是分别弹三次。
    private const string ShuBrothersGroupId = "shu_brothers";
    private const string ShuBrothersAnnouncementTitle = "蜀汉三兄弟已解锁";

    private static readonly Dictionary<string, HeroUnlockCondition> Conditions = new();

    static HeroUnlockDatabase()
    {
        // ① 击败指定 Boss 解锁（本版仅实现 KillEnemy/Achievement 两种方式；
        //   CompleteEvent 只预留类型定义，本次不注册任何事件类条件）。

        // 吕布：击败【背叛者】（EnemyDatabase "boss_traitor"）1次。
        Register(CharacterIds.LuBu, new HeroUnlockCondition(
            HeroUnlockConditionType.KillEnemy,
            targetId: "boss_traitor",
            requiredAmount: 1,
            description: "击败【背叛者】",
            descriptionKey: "hero.unlock.lubu"));

        // 貂蝉：累计击败【背叛者】3次——和吕布共用同一个 Boss，只是次数要求不同，
        // 两条各自独立注册、互不影响（不共用 GroupId，各自单独解锁公告）。
        Register(CharacterIds.DiaoChan, new HeroUnlockCondition(
            HeroUnlockConditionType.KillEnemy,
            targetId: "boss_traitor",
            requiredAmount: 3,
            description: "累计击败【背叛者】3次",
            descriptionKey: "hero.unlock.diaochan"));

        // 华佗：击败【医者】（EnemyDatabase "boss_healer"）1次。
        Register(CharacterIds.HuaTuo, new HeroUnlockCondition(
            HeroUnlockConditionType.KillEnemy,
            targetId: "boss_healer",
            requiredAmount: 1,
            description: "击败【医者】",
            descriptionKey: "hero.unlock.huatuo"));

        // 诸葛亮：击败【观星集智体】（EnemyDatabase "wulong_collective_intelligence"，
        // 卧龙集智体，使用观星技能，第二章 Boss）1次。
        Register(CharacterIds.ZhuGeLiang, new HeroUnlockCondition(
            HeroUnlockConditionType.KillEnemy,
            targetId: "wulong_collective_intelligence",
            requiredAmount: 1,
            description: "击败【观星集智体】",
            descriptionKey: "hero.unlock.zhugeliang"));

        // 祢衡：击败【失疯卖艺人】（EnemyDatabase "crazy_performer"）1次。
        Register(CharacterIds.MiHeng, new HeroUnlockCondition(
            HeroUnlockConditionType.KillEnemy,
            targetId: "crazy_performer",
            requiredAmount: 1,
            description: "击败【失疯卖艺人】",
            descriptionKey: "hero.unlock.miheng"));

        // 张角：击败【黄衣之主】（EnemyDatabase "huang_yi_zhi_zhu"）1次。
        Register(CharacterIds.ZhangJiao, new HeroUnlockCondition(
            HeroUnlockConditionType.KillEnemy,
            targetId: "huang_yi_zhi_zhu",
            requiredAmount: 1,
            description: "击败【黄衣之主】",
            descriptionKey: "hero.unlock.zhangjiao"));

        // 夏侯惇：击败【独眼巨人】（EnemyDatabase "cyclops"，第二章精英）1次。
        Register(CharacterIds.XiaHouDun, new HeroUnlockCondition(
            HeroUnlockConditionType.KillEnemy,
            targetId: "cyclops",
            requiredAmount: 1,
            description: "击败【独眼巨人】",
            descriptionKey: "hero.unlock.xiahoudun"));

        // 董卓：击败【暴虐昏君】（EnemyDatabase "boss_tyrant"，第三章 3-8 关的其中一条
        // 随机路线；同一关的另一条路线是下面的【蜀汉共生体】）1次。项目里目前没有
        // 单独的"董卓"敌人条目，经与用户确认后用这个 Boss 代表"击败董卓boss"——
        // 二者技能确实同源（酒池/肉林/崩坏），是同一个角色的战斗形态。
        Register(CharacterIds.DongZhuo, new HeroUnlockCondition(
            HeroUnlockConditionType.KillEnemy,
            targetId: "boss_tyrant",
            requiredAmount: 1,
            description: "击败【暴虐昏君】",
            descriptionKey: "hero.unlock.dongzhuo"));

        // 孟获：击败【流亡蛮族】（EnemyDatabase "exile_barbarian"，第一章精英）1次。
        Register(CharacterIds.MengHuo, new HeroUnlockCondition(
            HeroUnlockConditionType.KillEnemy,
            targetId: "exile_barbarian",
            requiredAmount: 1,
            description: "击败【流亡蛮族】",
            descriptionKey: "hero.unlock.menghuo"));

        // ② 达成指定累计条件解锁。

        // 孙尚香：累计获得100件装备（账号历史累计，出售/丢弃/重铸都不减少——
        // AchievementCounters 是只增不减的计数器，GameManager.AddEquipment 每次
        // 获得装备都会调用 HeroUnlockProgress.AddAchievementProgress，不随
        // ResetRunData 清空，天然满足"跨局累计、不因背包变化而回退"）。
        Register(CharacterIds.SunShangXiang, new HeroUnlockCondition(
            HeroUnlockConditionType.Achievement,
            targetId: nameof(AchievementType.EquipmentAcquired),
            requiredAmount: 100,
            description: "累计获得100件装备",
            descriptionKey: "hero.unlock.sunshangxiang"));

        // 刘备/关羽/张飞：三人共用同一个解锁条件——击败【蜀汉共生体】1次。
        // 这个 Boss 实际是三个共享血量池的 EnemyInstance（EnemyDatabase 里的
        // "liu_bei"/"guan_yu"/"zhang_fei"，UseSharedHealthPool=true），血量池归零时
        // 三个实例通常会在同一场战斗里各自触发死亡记录，所以 TargetId 用 '|' 列出
        // 三个敌人 Id，HeroUnlockProgress.EvaluateCondition 取其中击败次数的最大值，
        // 任意一个被记录到击败即视为整个 Boss 被击败。
        var shuBrothersCondition = new HeroUnlockCondition(
            HeroUnlockConditionType.KillEnemy,
            targetId: "liu_bei|guan_yu|zhang_fei",
            requiredAmount: 1,
            description: "击败【蜀汉共生体】",
            groupId: ShuBrothersGroupId,
            groupAnnouncementTitle: ShuBrothersAnnouncementTitle,
            descriptionKey: "hero.unlock.shu_brothers",
            groupAnnouncementTitleKey: "hero.unlock.shu_brothers.announcement");

        Register(CharacterIds.LiuBei, shuBrothersCondition);
        Register(CharacterIds.GuanYu, shuBrothersCondition);
        Register(CharacterIds.ZhangFei, shuBrothersCondition);
    }

    /// <summary>
    /// 注册（或覆盖）一个角色的解锁条件。
    /// </summary>
    public static void Register(string characterId, HeroUnlockCondition condition)
    {
        Conditions[characterId] = condition;
    }

    /// <summary>
    /// 查询一个角色的解锁条件；返回 null 表示该角色没有配置解锁条件，
    /// 调用方（HeroUnlockProgress.IsHeroUnlocked）应该视为"一直可选"。
    /// </summary>
    public static HeroUnlockCondition? Get(string characterId)
    {
        return Conditions.TryGetValue(characterId, out var condition) ? condition : null;
    }

    /// <summary>
    /// 返回所有"注册过解锁条件"的角色 Id（不包含没有配置条件、视为一直可选的角色）。
    ///
    /// 供 <see cref="HeroUnlockProgress"/> 遍历检测新解锁、以及开发者模式
    /// 【全部解锁英雄】使用。
    /// </summary>
    public static IReadOnlyCollection<string> GetAllConditionCharacterIds()
    {
        return Conditions.Keys;
    }
}
