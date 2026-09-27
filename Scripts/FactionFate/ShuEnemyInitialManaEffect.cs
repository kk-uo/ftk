//////////////////////////////////////////////////////////
// 文件：Scripts/FactionFate/ShuEnemyInitialManaEffect.cs
//
// 蜀命运三：所有敌人的初始费用归零。
//////////////////////////////////////////////////////////

/// <summary>
/// 在每场战斗的第一回合出牌前，将所有敌人的定义初始费用覆盖为0。
/// 装备、技能等后续“战斗开始获得费用”的效果仍按原规则结算，不被视作初始费用。
/// </summary>
public sealed class ShuEnemyInitialManaEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnBattlePrePhase;
    public EffectPriority Priority => EffectPriority.Highest;

    public void Execute(BattleContext context)
    {
        if (context.TurnNumber != 1 || !FactionFateManager.IsShuEnemyZeroStartingManaActive())
        {
            return;
        }

        var enemies = context.Encounter?.Enemies;
        if (enemies == null)
        {
            return;
        }

        foreach (var enemy in enemies)
        {
            enemy.CurrentResource = 0;
        }

        context.RoundResult.AddLine("蜀·破釜：所有敌人初始费用归零。");
        context.AddTriggerLog("[FactionFate/ShuEnemyInitialMana] 所有敌人初始费用归零。");
    }
}
