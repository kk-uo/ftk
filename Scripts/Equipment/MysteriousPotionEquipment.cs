//////////////////////////////////////////////////////////
// 神秘补剂：当前生命值（含临时生命值）达到 120 时，
// 装备者的普通杀视为必中杀。
//////////////////////////////////////////////////////////

/// <summary>
/// 在揭示阶段把满足生命阈值的普通杀转换为必中杀。
/// <para>
/// <see cref="Player.Health"/> 是战斗中的实际当前生命，因此包含允许超出最大生命的临时生命值。
/// </para>
/// </summary>
public sealed class MysteriousPotionRevealEffect : IBattleEffect
{
    private const int HealthThreshold = 120;

    public TriggerTiming Timing => TriggerTiming.OnBattleReveal;
    public EffectPriority Priority => EffectPriority.Highest;

    public void Execute(BattleContext context)
    {
        TransformPlayerAction(context);

        foreach (var entry in context.EnemyActions)
        {
            TransformEnemyAction(context, entry);
        }
    }

    private static void TransformPlayerAction(BattleContext context)
    {
        var action = context.PlayerAction;
        if (action == null
            || action.Type != CardType.Kill
            || context.Player.Health < HealthThreshold
            || !BattleRules.HasEquipment(context.Player, EquipmentIds.MysteriousPotion))
        {
            return;
        }

        context.PlayerAction = action.TransformTo(Card.SureKill());
        WriteTransformLog(context, context.Player);
    }

    private static void TransformEnemyAction(BattleContext context, EnemyActionEntry entry)
    {
        if (entry.Action.Type != CardType.Kill
            || entry.Enemy.Health < HealthThreshold
            || !BattleRules.HasEquipment(entry.Enemy, EquipmentIds.MysteriousPotion))
        {
            return;
        }

        entry.Action = entry.Action.TransformTo(Card.SureKill());
        WriteTransformLog(context, entry.Enemy);
    }

    private static void WriteTransformLog(BattleContext context, Player source)
    {
        context.RoundResult.AddLine($"神秘补剂：{source.DisplayName}当前生命≥{HealthThreshold}，普通杀变为必中杀。");
        context.AddTriggerLog($"[Equipment/MysteriousPotion] {source.DisplayName}当前生命={source.Health}，普通杀转换为必中杀。");
    }
}
