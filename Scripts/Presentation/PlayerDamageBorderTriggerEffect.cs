//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/PlayerDamageBorderTriggerEffect.cs
//
// 模块：Presentation System
//
// 职责：
// 1. 在标准 DamageEvent 完成实际扣血后，请求玩家受伤屏幕边框。
// 2. 将“是否实际受伤”的判定绑定到 ActualDamageDealt，而不是 HP Label 或 UI 文本。
//
// 不负责：
// × 计算伤害。
// × 修改玩家生命值。
// × 创建或操作 Godot 节点。
//////////////////////////////////////////////////////////

/// <summary>
/// 玩家受伤边框的 Trigger 桥接效果。
///
/// 该效果只在 ApplyDamageEffect 已写入 <see cref="DamageEvent.ActualDamageDealt"/>
/// 后运行，并通过 BattleContext 的表现层回调发出请求。
/// </summary>
public sealed class PlayerDamageBorderTriggerEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// 根据最终实际扣血结果请求播放受伤边框。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null
            || damage.Cancelled
            || damage.Target != context.Player
            || damage.ActualDamageDealt <= 0)
        {
            return;
        }

        context.RequestPlayerDamageBorder(damage.ActualDamageDealt, context.Player.MaxHealth);
    }
}
