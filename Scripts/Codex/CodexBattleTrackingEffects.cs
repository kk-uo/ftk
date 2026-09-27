//////////////////////////////////////////////////////////
// 文件：Scripts/Codex/CodexBattleTrackingEffects.cs
//
// 模块：Codex System（跨Run永久图鉴）
//
// 为什么存在：
// 图鉴需要知道"每次伤害结算的最终结果"（命中/格挡/伤害数值/是否击杀），但不应该
// 侵入核心伤害规则文件（Scripts/Damage/DamageEffects.cs）——按照与 BattleLogService
// 相同的原则：真实游戏系统各自在正确时机产生事件，Codex 独立监听，不通过解析
// 其它系统的文本/状态反推。
//
// 这里注册在 TriggerTiming.OnDamageTaken、Priority.Lowest：
// 保证在 ApplyDamageEffect（Mid，写入 ActualDamageDealt）和 DamageTakenEffect
// （Low，可能触发嵌套的 OnDying→OnDeath 链，把目标标记为真正死亡）都执行完之后
// 才读取最终结果，避免读到"伤害已扣但死亡状态还没结算"的中间态。
//
// 不负责：
// × 判断伤害是否成立（沿用 damage.Cancelled/ActualDamageDealt，不重复计算）。
// × 卡牌是否"使用一次"（见 BattlePhaseResolutionEffect 里的 CodexService.RecordCardUsed）。
//
// 主要依赖：
// CodexService / DamageEvent / BattleRules.IsAnyAttackCard
//////////////////////////////////////////////////////////

/// <summary>
/// Codex System 的公开类：CodexDamageTrackingEffect。
///
/// 已知简化（详见实现报告）：技能触发的"借用某个CardType作为伤害标签"的自伤/连锁伤害
/// （例如魂姿失手自伤借用CardType.Kill）会被一并计入该卡牌类型的命中/伤害统计，
/// 因为 DamageEvent 本身不区分"这是玩家真实打出的这张牌"还是"某个技能借用这个类型
/// 记账"。要完全区分需要给 DamageEvent 增加"是否代表一次真实卡牌行动"的标记，
/// 工作量已经超出本次实现范围，此处按已知限制处理。
/// </summary>
public sealed class CodexDamageTrackingEffect : IBattleEffect
{
    public TriggerTiming Timing => TriggerTiming.OnDamageTaken;
    public EffectPriority Priority => EffectPriority.Lowest;

    public void Execute(BattleContext context)
    {
        var damage = context.DamageEvent;
        if (damage == null)
        {
            return;
        }

        var sourceIsPlayer = ReferenceEquals(damage.Source, context.Player);
        var targetIsPlayer = ReferenceEquals(damage.Target, context.Player);
        var characterId = GameManager.CurrentCharacterId;

        if (sourceIsPlayer && BattleRules.IsAnyAttackCard(damage.AttackType))
        {
            if (damage.Cancelled)
            {
                CodexService.RecordCardBlocked(damage.AttackType);
            }
            else if (damage.ActualDamageDealt > 0)
            {
                CodexService.RecordCardHit(damage.AttackType, damage.ActualDamageDealt, damage.Target.IsDead);
            }
        }

        if (damage.Cancelled || damage.ActualDamageDealt <= 0)
        {
            return;
        }

        if (sourceIsPlayer && damage.Target is EnemyInstance hitEnemy)
        {
            CodexService.RecordEnemyDamageDealt(hitEnemy.Definition.Id, damage.ActualDamageDealt);
            if (!string.IsNullOrEmpty(characterId))
            {
                CodexService.RecordCharacterDamageDealt(characterId, damage.ActualDamageDealt);
            }
        }
        else if (targetIsPlayer)
        {
            if (damage.Source is EnemyInstance sourceEnemy)
            {
                CodexService.RecordEnemyDamageTaken(sourceEnemy.Definition.Id, damage.ActualDamageDealt);
            }
            if (!string.IsNullOrEmpty(characterId))
            {
                CodexService.RecordCharacterDamageTaken(characterId, damage.ActualDamageDealt);
            }
        }
    }
}
