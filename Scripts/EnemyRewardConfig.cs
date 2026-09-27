using System;

/// <summary>
/// 敌人金币掉落的统一配置：按 EnemyType 分三档区间（普通/精英/Boss），
/// 所有敌人的金币掉落都从这里读取，不允许在 EnemyDatabase.cs 里再写死数值。
/// </summary>
public static class EnemyRewardConfig
{
    public const int NormalMinGold = 20;
    public const int NormalMaxGold = 25;
    public const int EliteMinGold = 75;
    public const int EliteMaxGold = 85;
    public const int BossMinGold = 100;
    public const int BossMaxGold = 120;
    public const int HuanXiangChuShouGold = 200;

    public static (int Min, int Max) GetGoldRange(EnemyType type) => type switch
    {
        EnemyType.Elite => (EliteMinGold, EliteMaxGold),
        EnemyType.Boss => (BossMinGold, BossMaxGold),
        _ => (NormalMinGold, NormalMaxGold)
    };

    /// <summary>
    /// 统一随机掉落逻辑：调用方传入战斗结算已有的种子随机实例，不在这里另建 Random，
    /// 保证金币掉落和装备概率掉落共用同一套随机流程。Next 的上界是排他的，
    /// 传 max+1 变成闭区间，保证结果绝不超出配置区间。
    /// </summary>
    public static int RollGold(EnemyType type, Random random)
    {
        var (min, max) = GetGoldRange(type);
        return random.Next(min, max + 1);
    }
}
