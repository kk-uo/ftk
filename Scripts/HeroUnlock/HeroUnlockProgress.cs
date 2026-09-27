//////////////////////////////////////////////////////////
// 文件：Scripts/HeroUnlock/HeroUnlockProgress.cs
//
// 模块：Hero Unlock System
//
// 为什么存在：
// 解锁进度（击败过哪些敌人、完成过哪些事件、各项成就累计到多少、哪些英雄
// 已经解锁）不应该让角色自己保存一个"是否解锁"的布尔值，而应该由一个统一的
// Progress 对象记录，角色选择界面/解锁弹窗/开发者模式全部只读取这一份数据。
//
// 职责：
// 1. 记录三种解锁条件各自需要的原始数据：击败过的敌人 Id 集合、完成过的
//    事件 Id 集合、各项成就的累计计数。
// 2. 提供 EvaluateCondition：给定任意一条 HeroUnlockCondition，统一算出
//    "是否已满足"+"当前进度/需要多少"，供 UI 和解锁判定共用同一份逻辑。
// 3. 维护"已解锁英雄"集合，并在某个角色第一次满足条件的那一刻记录一次
//    "待展示的解锁公告"，供 UI 弹出【新英雄已解锁】。
// 4. 提供开发者模式用的 UnlockAllForDebug/ResetAll。
//
// 不负责：
// × 判断游戏逻辑事件本身（例如"这次伤害是否击杀了敌人"由 Battle/Trigger
//   系统决定，本类只在收到"敌人死亡"这类通知后记录结果）。
// × 角色数据、角色技能（完全不引用 CharacterData/Skill 的任何字段）。
// × 存档到磁盘（当前项目还没有落地的存档系统，这里和 GameManager 的其它
//   运行时状态一样，只在本次运行的进程内存里维护；不随 GameManager.ResetRunData
//   清空，保证解锁进度不会因为开始新的一局而被重置）。
//
// 主要依赖：
// HeroUnlockCondition / HeroUnlockDatabase / HeroUnlockConditionType / AchievementType
//////////////////////////////////////////////////////////

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 英雄解锁进度：全局唯一的 Progress 存储。
///
/// 刻意做成完全独立于 <see cref="GameManager"/> 的静态类——解锁进度是跨越
/// "一局 Run"的长期进度，不应该被 <c>GameManager.ResetRunData</c>（开始新的一局）
/// 清空，只有明确调用 <see cref="ResetAll"/>（开发者模式的"重置英雄解锁"）
/// 才会清空。
/// </summary>
public static class HeroUnlockProgress
{
    // 敌人 Id → 累计击败次数（不是单纯的布尔值）：既满足"是否击败过"（次数 > 0），
    // 也满足开发者调试面板里"击败次数 +1/+10"这种数值展示需求，不需要维护两份数据。
    private static readonly Dictionary<string, int> EnemyKillCounts = new();
    private static readonly HashSet<string> EventsCompleted = new();
    private static readonly Dictionary<AchievementType, int> AchievementCounters = new();
    private static readonly HashSet<string> HeroesUnlocked = new();

    // 每一项是"应该一起展示的一批角色 Id"——单人解锁时是长度为 1 的列表；
    // 多个角色共用同一个 GroupId 且在同一次 CheckForNewUnlocks 里一起达成条件时，
    // 会被合并成一个列表，UI 只弹一次合并公告，不逐个弹窗。
    private static readonly Queue<IReadOnlyList<string>> PendingUnlockAnnouncements = new();

    // 连续使用【费】的当前连击数（不是历史最高值——历史最高值保存在
    // AchievementCounters[ConsecutiveFeeTurns] 里，由 RecordFeeTurn 维护）。
    private static int _currentFeeStreak;

    // ————————————————————————————————————————
    // 记录原始事件（由 Trigger 侧的新增 IBattleEffect / GameManager 既有资源变化
    // 入口调用，见 Scripts/HeroUnlock/README.md 的接入说明）
    // ————————————————————————————————————————

    /// <summary>记录一个敌人被击败一次（EnemyDefinition.Id），累计击败次数 +1。</summary>
    public static void RecordEnemyKilled(string enemyId)
    {
        if (string.IsNullOrEmpty(enemyId))
        {
            return;
        }

        EnemyKillCounts.TryGetValue(enemyId, out var count);
        EnemyKillCounts[enemyId] = count + 1;
        CheckForNewUnlocks();
    }

    /// <summary>记录一个事件被完成（EventData.Id）。重复记录同一个 Id 没有副作用。</summary>
    public static void RecordEventCompleted(string eventId)
    {
        if (string.IsNullOrEmpty(eventId))
        {
            return;
        }

        EventsCompleted.Add(eventId);
        CheckForNewUnlocks();
    }

    /// <summary>给指定成就累加进度（<paramref name="amount"/> 应为正数）。</summary>
    public static void AddAchievementProgress(AchievementType type, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        AchievementCounters.TryGetValue(type, out var current);
        AchievementCounters[type] = current + amount;
        CheckForNewUnlocks();
    }

    /// <summary>
    /// 记录这一回合是否使用了【费】，用于维护"历史最高连续使用【费】回合数"成就。
    /// 使用【费】：连续计数 +1，并更新历史最高值；使用其它任何卡牌：连续计数清零。
    /// </summary>
    public static void RecordFeeTurn(bool playedFee)
    {
        if (playedFee)
        {
            _currentFeeStreak++;
            AchievementCounters.TryGetValue(AchievementType.ConsecutiveFeeTurns, out var best);
            if (_currentFeeStreak > best)
            {
                AchievementCounters[AchievementType.ConsecutiveFeeTurns] = _currentFeeStreak;
            }
        }
        else
        {
            _currentFeeStreak = 0;
        }

        CheckForNewUnlocks();
    }

    // ————————————————————————————————————————
    // 查询
    // ————————————————————————————————————————

    /// <summary>
    /// 某个角色是否已解锁。没有在 <see cref="HeroUnlockDatabase"/> 注册解锁条件的角色
    /// 视为"一直可选"，保证这次改动不影响任何没有主动配置条件的角色。
    /// </summary>
    public static bool IsHeroUnlocked(string characterId)
    {
        var condition = HeroUnlockDatabase.Get(characterId);
        if (condition == null)
        {
            return true;
        }

        return HeroesUnlocked.Contains(characterId);
    }

    /// <summary>
    /// 统一计算任意一条解锁条件的"当前进度"。
    ///
    /// 返回值：是否已满足、当前进度、需要达到的进度——角色选择界面的悬停提示
    /// 和"是否解锁"的判定都调用这同一个方法，保证两处显示的数字不会对不上。
    /// </summary>
    public static (bool Satisfied, int Current, int Required) EvaluateCondition(HeroUnlockCondition condition)
    {
        int current;
        switch (condition.ConditionType)
        {
            case HeroUnlockConditionType.KillEnemy:
                // 支持用 '|' 分隔多个敌人 Id（共享血量池的多体 Boss，例如刘备/关羽/张飞
                // 共用的【蜀汉共生体】，三个 EnemyInstance 各自触发死亡记录）；
                // 取其中击败次数的最大值，任意一个记录到击败即视为"这个 Boss 被击败了"。
                current = condition.TargetId
                    .Split('|', System.StringSplitOptions.RemoveEmptyEntries)
                    .Select(id => EnemyKillCounts.TryGetValue(id, out var killCount) ? killCount : 0)
                    .DefaultIfEmpty(0)
                    .Max();
                break;

            case HeroUnlockConditionType.CompleteEvent:
                current = EventsCompleted.Contains(condition.TargetId) ? 1 : 0;
                break;

            case HeroUnlockConditionType.Achievement:
                current = System.Enum.TryParse<AchievementType>(condition.TargetId, out var achievementType)
                    && AchievementCounters.TryGetValue(achievementType, out var progress)
                        ? progress
                        : 0;
                break;

            default:
                current = 0;
                break;
        }

        var required = System.Math.Max(1, condition.RequiredAmount);
        return (current >= required, System.Math.Min(current, required), required);
    }

    /// <summary>
    /// 取出一条"待展示"的解锁公告（先进先出）；没有则返回 null。
    /// 返回的列表可能包含多个角色 Id——这些角色共用同一个 <see cref="HeroUnlockCondition.GroupId"/>
    /// 且在同一次判定里一起达成了条件（例如蜀汉三兄弟），UI 应该把它们合并成一条公告展示，
    /// 而不是逐个弹窗。UI 应该在每次进入角色选择界面时检查一次，弹出后即视为已展示。
    /// </summary>
    public static IReadOnlyList<string>? DequeuePendingUnlockAnnouncement()
    {
        return PendingUnlockAnnouncements.Count > 0 ? PendingUnlockAnnouncements.Dequeue() : null;
    }

    // ————————————————————————————————————————
    // 开发者模式：查询（供 DeveloperDebugPanel 展示当前数值用）
    // ————————————————————————————————————————

    /// <summary>某个敌人当前的累计击败次数。</summary>
    public static int GetEnemyKillCount(string enemyId)
    {
        return EnemyKillCounts.TryGetValue(enemyId, out var count) ? count : 0;
    }

    /// <summary>某个事件是否已经被记录为"完成过"。</summary>
    public static bool IsEventCompleted(string eventId)
    {
        return EventsCompleted.Contains(eventId);
    }

    /// <summary>某项成就当前的累计进度。</summary>
    public static int GetAchievementProgress(AchievementType type)
    {
        return AchievementCounters.TryGetValue(type, out var value) ? value : 0;
    }

    // ————————————————————————————————————————
    // 开发者模式：修改（DeveloperDebugPanel 专用，全部只改 Progress 本身，
    // 不直接改 HeroUnlockDatabase/EnemyDatabase/EventDatabase 等只读数据库）
    // ————————————————————————————————————————

    /// <summary>开发者模式：直接把某个英雄设为已解锁/未解锁，不触发解锁公告弹窗。</summary>
    public static void SetHeroUnlockedForDebug(string characterId, bool unlocked)
    {
        if (unlocked)
        {
            HeroesUnlocked.Add(characterId);
        }
        else
        {
            HeroesUnlocked.Remove(characterId);
        }
    }

    /// <summary>开发者模式：把某个敌人的累计击败次数增加 <paramref name="amount"/>（可为负）。</summary>
    public static void AddEnemyKillCountForDebug(string enemyId, int amount)
    {
        EnemyKillCounts.TryGetValue(enemyId, out var current);
        EnemyKillCounts[enemyId] = System.Math.Max(0, current + amount);
        CheckForNewUnlocks();
    }

    /// <summary>开发者模式：把某个敌人的累计击败次数直接设为指定值。</summary>
    public static void SetEnemyKillCountForDebug(string enemyId, int value)
    {
        EnemyKillCounts[enemyId] = System.Math.Max(0, value);
        CheckForNewUnlocks();
    }

    /// <summary>开发者模式：直接把某个事件标记为完成/未完成。</summary>
    public static void SetEventCompletedForDebug(string eventId, bool completed)
    {
        if (completed)
        {
            EventsCompleted.Add(eventId);
        }
        else
        {
            EventsCompleted.Remove(eventId);
        }

        CheckForNewUnlocks();
    }

    /// <summary>开发者模式：把某项成就的进度增加 <paramref name="amount"/>（可为负）。</summary>
    public static void AddAchievementProgressForDebug(AchievementType type, int amount)
    {
        AchievementCounters.TryGetValue(type, out var current);
        AchievementCounters[type] = System.Math.Max(0, current + amount);
        CheckForNewUnlocks();
    }

    /// <summary>开发者模式：把某项成就的进度直接设为指定值。</summary>
    public static void SetAchievementProgressForDebug(AchievementType type, int value)
    {
        AchievementCounters[type] = System.Math.Max(0, value);
        CheckForNewUnlocks();
    }

    // ————————————————————————————————————————
    // 开发者模式：批量操作
    // ————————————————————————————————————————

    /// <summary>
    /// 开发者模式【全部解锁英雄】：让所有注册过解锁条件的角色立即解锁。
    /// 不修改任何 Progress 计数器本身（击败/完成/成就的原始记录不变），
    /// 也不触发解锁公告弹窗——纯粹是调试用的快捷方式，不影响正常存档语义。
    /// </summary>
    public static void UnlockAllForDebug()
    {
        foreach (var characterId in HeroUnlockDatabase.GetAllConditionCharacterIds())
        {
            HeroesUnlocked.Add(characterId);
        }
    }

    /// <summary>
    /// 开发者模式【重置英雄解锁】：清空全部 Progress（击败记录/事件记录/成就计数/
    /// 已解锁英雄/待展示公告），方便重新测试解锁流程。
    /// </summary>
    public static void ResetAll()
    {
        EnemyKillCounts.Clear();
        EventsCompleted.Clear();
        AchievementCounters.Clear();
        HeroesUnlocked.Clear();
        PendingUnlockAnnouncements.Clear();
        _currentFeeStreak = 0;
    }

    /// <summary>开发者模式【重置事件】：只清空事件完成记录，不影响击败记录/成就/已解锁英雄。</summary>
    public static void ResetEventsForDebug()
    {
        EventsCompleted.Clear();
        CheckForNewUnlocks();
    }

    /// <summary>开发者模式【重置成就】：只清空成就计数，不影响击败记录/事件/已解锁英雄。</summary>
    public static void ResetAchievementsForDebug()
    {
        AchievementCounters.Clear();
        _currentFeeStreak = 0;
        CheckForNewUnlocks();
    }

    /// <summary>开发者模式【重置英雄】：只清空已解锁英雄集合，不影响击败/事件/成就记录。</summary>
    public static void ResetHeroesForDebug()
    {
        HeroesUnlocked.Clear();
        PendingUnlockAnnouncements.Clear();
    }

    // ————————————————————————————————————————
    // 内部：检测是否有新英雄刚好达成解锁条件
    // ————————————————————————————————————————

    private static void CheckForNewUnlocks()
    {
        // 第一遍：只收集"这一次判定里新达成条件"的角色，不立即入队公告——
        // 需要先按 GroupId 分组，才能知道哪些角色应该合并成一条公告。
        var newlyUnlocked = new List<(string CharacterId, HeroUnlockCondition Condition)>();

        foreach (var characterId in HeroUnlockDatabase.GetAllConditionCharacterIds())
        {
            if (HeroesUnlocked.Contains(characterId))
            {
                continue;
            }

            var condition = HeroUnlockDatabase.Get(characterId);
            if (condition == null)
            {
                continue;
            }

            var (satisfied, _, _) = EvaluateCondition(condition);
            if (!satisfied)
            {
                continue;
            }

            HeroesUnlocked.Add(characterId);
            newlyUnlocked.Add((characterId, condition));
        }

        if (newlyUnlocked.Count == 0)
        {
            return;
        }

        // 有 GroupId 的角色：同一个 GroupId 合并成一条公告（保持注册顺序）；
        // 没有 GroupId 的角色：各自单独一条公告。
        var grouped = newlyUnlocked
            .GroupBy(entry => entry.Condition.GroupId)
            .ToList();

        foreach (var group in grouped)
        {
            if (group.Key == null)
            {
                foreach (var entry in group)
                {
                    PendingUnlockAnnouncements.Enqueue(new[] { entry.CharacterId });
                }
            }
            else
            {
                PendingUnlockAnnouncements.Enqueue(group.Select(entry => entry.CharacterId).ToList());
            }
        }
    }
}
