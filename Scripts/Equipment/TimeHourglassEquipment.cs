//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/TimeHourglassEquipment.cs
//
// 模块：Equipment System
//
// 职责：
// 1. 在首回合战斗回合前检查玩家是否装备【时间沙漏】。
// 2. 复用【战场崩坏】规则，提前结算一次第55回合对应的伤害。
//
// 不负责：
// × 修改当前战斗回合数。
// × 复制或替代第55回合后的持续崩坏规则。
// × 处理装备获取与选择界面。
//
// 主要依赖：
// BattlefieldCollapseEffect
// GameManager
// TriggerManager
//////////////////////////////////////////////////////////

/// <summary>
/// 【时间沙漏】的首回合战斗回合前触发效果。
///
/// 仅提前执行一次第55回合对应的环境伤害；战斗仍从第1回合开始，
/// 原本第55回合后的递增伤害仍由全局规则正常处理。
/// </summary>
public sealed class TimeHourglassEffect : IBattleEffect
{
    // OnGameStart 发生在首回合战报初始化之前，效果和日志会被随后清空。
    // 在 OnBattlePrePhase 执行可确保伤害、浮字与战报进入首回合的正常结算链路。
    public TriggerTiming Timing => TriggerTiming.OnBattlePrePhase;
    public EffectPriority Priority => EffectPriority.High;

    /// <summary>
    /// 在装备生效时，复用战场崩坏的伤害计算与双方扣血入口。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (context.GameOver
            || context.TurnCounter != 1
            || !GameManager.HasEquipment(EquipmentIds.TimeHourglass))
        {
            return;
        }

        var damage = BattlefieldCollapseEffect.CalculateDamage(BattlefieldCollapseEffect.TriggerTurn);
        BattlefieldCollapseEffect.ApplyDamage(context, damage);

        context.RoundResult.AddLine($"时间沙漏：提前触发第{BattlefieldCollapseEffect.TriggerTurn}回合的战场崩坏，双方受到{damage}点真实伤害。");
        context.AddTriggerLog("[Equipment/时间沙漏]");
        context.AddTriggerLog($"首回合战斗回合前：提前触发第{BattlefieldCollapseEffect.TriggerTurn}回合战场崩坏，双方 -{damage}。");
    }
}
