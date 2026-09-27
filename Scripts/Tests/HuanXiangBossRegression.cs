using Godot;
using System;
using System.Linq;

/// <summary>
/// Covers the Chapter 4-8 Phantom Tentacle definition and its Illusion/Slime combat rules.
/// </summary>
public partial class HuanXiangBossRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestDefinitionAndStage();
            TestIllusionAndSlimeResponses();
            TestRuntimeIllusionCycle();
            GD.Print($"HUAN_XIANG_BOSS_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"HUAN_XIANG_BOSS_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestDefinitionAndStage()
    {
        var boss = EnemyDatabase.GetEnemy("huan_xiang_chu_shou");
        Assert(boss != null, "Phantom Tentacle is not registered in the enemy database.");
        Assert(boss!.MaxHP == 2000 && boss.Type == EnemyType.Boss, "Phantom Tentacle base stats are incorrect.");
        Assert(boss.RewardTier == RewardTier.Legendary, "Phantom Tentacle reward tier is not Legendary.");
        Assert(boss.Reward.GoldOverride == EnemyRewardConfig.HuanXiangChuShouGold, "Phantom Tentacle does not grant its fixed 200 Gold reward.");
        Assert(!boss.UseSharedHealthPool, "Phantom Tentacle must not share an HP pool.");
        Assert(boss.SkillIds.SequenceEqual(new[] { SkillIds.Illusion, SkillIds.Slime }), "Phantom Tentacle skill list does not match the specification.");
        Assert(boss.EquipmentIds.Count(id => id == EquipmentIds.AttackChip) == 5, "Phantom Tentacle must carry five Attack Chips.");
        var instance = new EnemyInstance(boss);
        var equipmentName = Localization.GetName(EquipmentDatabase.GetEquipment(EquipmentIds.AttackChip)!);
        var equipmentSummary = EnemyInfoFormatter.BuildEquipmentSummary(instance);
        Assert(equipmentSummary.StartsWith($"{equipmentName}*5", StringComparison.Ordinal),
            "Repeated enemy equipment must be displayed as a single Attack Chip*5 entry.");
        Assert(!equipmentSummary.Contains("\n", StringComparison.Ordinal),
            "Repeated enemy equipment must not be displayed as multiple duplicated lines.");
        Assert(boss.StartingDeck.Cards.SequenceEqual(new[]
        {
            CardType.Kill, CardType.FireKill, CardType.ThunderKill, CardType.Dodge,
            CardType.Peach, CardType.Wine, CardType.Steal, CardType.Unassailable, CardType.Fee
        }), "Phantom Tentacle does not use the default action deck.");

        var stage = StageDatabase.GetStage("4-8");
        Assert(stage != null && stage.EncounterPool.Count == 1
            && stage.EncounterPool[0].EnemyIds.SequenceEqual(new[] { boss.Id }), "Stage 4-8 does not contain Phantom Tentacle as its Boss.");
    }

    private void TestIllusionAndSlimeResponses()
    {
        var definition = EnemyDatabase.GetEnemy("huan_xiang_chu_shou")!;
        var boss = new EnemyInstance(definition);
        var player = new Player("Test Player", "phantom_tentacle_test_player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 2);
        var context = new BattleContext(player, new TriggerManager());

        boss.RuntimeStates[HuanXiangKeys.IllusionCardType] = CardType.Kill;
        context.DamageEvent = new DamageEvent(player, boss, CardType.Kill, 10)
        {
            ActualDamageDealt = 0
        };
        new IllusionImmunityEffect(boss).Execute(context);
        Assert(context.DamageEvent.Cancelled && context.DamageEvent.WasFullyBlocked,
            "Illusion did not fully block the recorded attack type.");

        new SlimeBlockDetectEffect(boss).Execute(context);
        Assert(player.RuntimeStates.TryGetValue(HuanXiangKeys.SlimeDebuff, out var slime) && slime is true,
            "An Illusion-immunized attack did not trigger Slime Weakness.");

        new IllusionAiModeEffect(boss).Execute(context);
        Assert(boss.RuntimeStates.TryGetValue(HuanXiangKeys.AiMode, out var mode) && mode as string == HuanXiangKeys.AiModeAttack,
            "Being attacked did not change Phantom Tentacle into attack mode.");

        context.DamageEvent = new DamageEvent(boss, player, CardType.Kill, 10)
        {
            ActualDamageDealt = 10
        };
        new IllusionAiModeEffect(boss).Execute(context);
        Assert(boss.RuntimeStates.TryGetValue(HuanXiangKeys.AiMode, out mode) && mode as string == HuanXiangKeys.AiModeDefend,
            "A successful Phantom Tentacle attack did not change it back to defense mode.");
    }

    private void TestRuntimeIllusionCycle()
    {
        var boss = new EnemyInstance(EnemyDatabase.GetEnemy("huan_xiang_chu_shou")!);
        var player = new Player("测试玩家", "illusion_runtime_test_player", BattleTeam.Player);
        player.ResetForNewBattle(100, 100, 5);
        var context = new BattleContext(player, BattleTriggerEffects.CreateDefaultManager());
        context.BeginRoundResult();

        // 第一次普通杀成功命中：记录【杀】；第二次普通杀必须完全免疫。
        BattlePhaseResolutionEffect.DealAttackDamage(context, player, boss, CardType.Kill);
        Assert(boss.Health == 1990, "首次普通杀没有实际命中幻象目标");
        Assert(GetRecordedType(boss) == CardType.Kill, "首次普通杀命中后没有记录CardType");
        BattlePhaseResolutionEffect.DealAttackDamage(context, player, boss, CardType.Kill);
        Assert(boss.Health == 1990, "已记录普通杀后，相同CardType攻击没有被免疫");

        // 火杀是不同攻击牌：应命中并把记录切换为【火杀】；再出火杀则免疫。
        BattlePhaseResolutionEffect.DealAttackDamage(context, player, boss, CardType.FireKill);
        Assert(boss.Health == 1975, "不同CardType的火杀应该穿过普通杀免疫");
        Assert(GetRecordedType(boss) == CardType.FireKill, "火杀成功命中后没有替换幻象记录");
        BattlePhaseResolutionEffect.DealAttackDamage(context, player, boss, CardType.FireKill);
        Assert(boss.Health == 1975, "已记录火杀后，相同CardType火杀没有被免疫");

        // 再用普通杀成功命中，应解除火杀免疫并把记录切回普通杀。
        BattlePhaseResolutionEffect.DealAttackDamage(context, player, boss, CardType.Kill);
        Assert(boss.Health == 1965 && GetRecordedType(boss) == CardType.Kill,
            "普通杀没有解除火杀免疫并更新为普通杀记录");

        // 伤害来源为装备/技能时：即使 CardType 与记录相同，也不得被免疫或改写记录。
        ResolveNonAttackCardDamage(context, player, boss, CardType.Kill, HealthChangeSourceKind.Equipment, "测试装备伤害");
        Assert(boss.Health == 1955 && GetRecordedType(boss) == CardType.Kill,
            "装备伤害错误触发了幻象免疫或覆盖记录");
        ResolveNonAttackCardDamage(context, player, boss, CardType.FireKill, HealthChangeSourceKind.Skill, "测试技能伤害");
        Assert(boss.Health == 1945 && GetRecordedType(boss) == CardType.Kill,
            "技能伤害错误参与了幻象记录或免疫");
    }

    private static CardType? GetRecordedType(EnemyInstance enemy)
    {
        return enemy.RuntimeStates.TryGetValue(HuanXiangKeys.IllusionCardType, out var type)
            && type is CardType cardType
            ? cardType
            : null;
    }

    private static void ResolveNonAttackCardDamage(
        BattleContext context,
        Player source,
        Player target,
        CardType displayType,
        HealthChangeSourceKind sourceKind,
        string sourceName)
    {
        context.DamageEvent = new DamageEvent(
            source,
            target,
            displayType,
            10,
            origin: new HealthChangeSource(sourceKind, sourceName, sourceName, source));
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
        context.DamageEvent = null;
    }

    private void Assert(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
