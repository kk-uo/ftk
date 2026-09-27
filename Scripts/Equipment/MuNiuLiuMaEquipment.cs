//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/MuNiuLiuMaEquipment.cs
//
// 模块：Equipment System
//
// 职责：
// 1. 承载装备触发效果与装备战斗逻辑相关代码。
// 2. 为其它模块提供清晰、稳定的调用边界。
// 3. 保持本文件内的状态变化可追踪、可调试。
//
// 不负责：
// × 处理无关模块的业务规则。
// × 绕过既有 Manager 或 Trigger 流程直接改写跨系统状态。
// × 在数据定义层混入表现层细节。
//
// 主要依赖：
// Godot / C# Runtime
// 项目内对应 Manager、Database 与 Trigger 系统
//////////////////////////////////////////////////////////

// 木牛流马（史诗·载具）：每经过3个战斗回合，玩家获得0.5费（OnTurnEnd，第3/6/9...回合触发）。
/// <summary>
/// Equipment System 的公开类：MuNiuLiuMaResourceEffect。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class MuNiuLiuMaResourceEffect : IBattleEffect
{
    private const int TriggerInterval = 3;
    private const double ManaGain = 0.5;

    public TriggerTiming Timing => TriggerTiming.OnTurnEnd;
    public EffectPriority Priority => EffectPriority.Low;

    /// <summary>
    /// Equipment System 的公开入口：Execute。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void Execute(BattleContext context)
    {
        if (!GameManager.HasEquipment(EquipmentIds.MuNiuLiuMa))
        {
            return;
        }

        if (context.TurnCounter % TriggerInterval != 0)
        {
            return;
        }

        context.Player.GainMana(ManaGain);
        context.RoundResult.AddLine($"木牛流马：第{context.TurnCounter}回合，获得{ManaGain:0.0}费。");
        context.AddTriggerLog("[Equipment/木牛流马]");
        context.AddTriggerLog($"每{TriggerInterval}回合触发：第{context.TurnCounter}回合结束，+{ManaGain:0.0}费。");
    }
}
