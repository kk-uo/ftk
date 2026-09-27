//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/MeihuoShieldRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证魅惑通过统一锁定目标正确施加护盾。
// 2. 验证魅惑护盾显示在敌人状态 UI 数据中。
// 3. 验证攻击牌与非攻击牌伤害均统一触发护盾吸收。
// 4. 验证护盾无论是否被击破，魅惑都在下回合转化为控制。
// 5. 验证护盾只抵挡一次，0伤害与已取消伤害不会消耗护盾。
// 6. 验证魅惑每场战斗最多只能使用3次：第4次不再消耗费用/施加护盾，
//    且 Player.MeihuoUsesRemaining 归零后不会继续往下扣。
//
// 不负责：
// × 验证魅惑卡牌动画。
// × 验证所有敌人 AI 行为。
//
// 主要依赖：
// MeihuoApplyEffect
// MeihuoShieldEffect
// MeihuoActivateEffect
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 貂蝉【魅惑】护盾完整状态链的 Headless 回归入口。
/// </summary>
public partial class MeihuoShieldRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行魅惑护盾回归并通过进程退出码返回结果。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            GameManager.BeginNewRun();
            InventoryManager.Reset();
            TestDefaultTriggerPipelineAppliesShield();
            TestShieldAppliesBeforeSameRoundEnemyAttack();
            TestShieldAppliedThroughActionTarget();
            TestShieldAppliedThroughLockedTarget();
            TestShieldBlocksStandardAndEvolvedAttacks();
            TestShieldBlocksNonAttackDamageThroughTriggerPipeline();
            TestInvalidDamageDoesNotConsumeShield();
            TestConsumedShieldStillActivatesControl();
            TestSurvivingShieldActivatesControl();
            TestMeihuoUsageCappedAtThreePerBattle();
            GD.Print($"MEIHUO_SHIELD_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"MEIHUO_SHIELD_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestDefaultTriggerPipelineAppliesShield()
    {
        var manager = BattleTriggerEffects.CreateDefaultManager();
        var (player, enemy, context) = CreateContext(manager);
        context.PlayerAction = BattleAction.FromCard(Card.Meihuo(), 1, enemy);
        context.SetActionForEnemy(enemy, BattleAction.FromCard(Card.Fee()));

        manager.RaiseTrigger(TriggerTiming.OnBattlePhase, context);

        Assert(HasShield(player), "默认 TriggerManager 完整结算后没有给玩家提供魅惑护盾");
        Assert(IsCharmed(enemy), "默认 TriggerManager 完整结算后没有标记被魅惑目标");
        Assert(Math.Abs(player.CurrentMana - 2) <= 0.001, "魅惑完整结算没有正确消耗1费");
    }

    private void TestShieldAppliesBeforeSameRoundEnemyAttack()
    {
        var manager = BattleTriggerEffects.CreateDefaultManager();
        var (player, enemy, context) = CreateContext(manager);
        var healthBefore = player.Health;
        context.PlayerAction = BattleAction.FromCard(Card.Meihuo(), 1, enemy);
        context.SetActionForEnemy(enemy, BattleAction.FromCard(Card.Kill(), 1, player));

        manager.RaiseTrigger(TriggerTiming.OnBattlePhase, context);

        Assert(player.Health == healthBefore,
            "魅惑与敌方攻击同回合结算时，护盾没有在伤害前生效");
        Assert(!HasShield(player), "魅惑护盾抵挡同回合攻击后没有被消耗");
        Assert(IsCharmed(enemy), "魅惑护盾抵挡同回合攻击后错误清除了魅惑控制");
    }

    private void TestShieldAppliedThroughActionTarget()
    {
        var (player, enemy, context) = CreateContext();
        context.PlayerAction = BattleAction.FromCard(Card.Meihuo(), 1, enemy);

        new MeihuoApplyEffect().Execute(context);

        Assert(HasShield(player), "BattleAction.Target 路径没有给玩家施加魅惑护盾");
        Assert(
            BattleUnitUiFormatter.GetStatuses(player, context).Any(status => status.Id == "meihuo_shield"),
            "魅惑护盾没有进入玩家状态 UI 数据");
        Assert(
            BattleUnitUiFormatter.GetStatuses(enemy, context).Any(status => status.Id == "meihuo_charmed"),
            "被魅惑状态没有进入目标敌人状态 UI 数据");
        Assert(player.HasSkill(SkillIds.Meihuo), "测试前提失败：貂蝉未携带魅惑");
    }

    private void TestShieldAppliedThroughLockedTarget()
    {
        var (player, enemy, context) = CreateContext();
        context.PlayerAction = BattleAction.FromCard(Card.Meihuo());
        context.PlayerLockedTarget = enemy;

        new MeihuoApplyEffect().Execute(context);

        Assert(HasShield(player), "PlayerLockedTarget 路径没有给玩家施加魅惑护盾");
        Assert(IsCharmed(enemy), "PlayerLockedTarget 路径没有标记被魅惑目标");
    }

    private void TestShieldBlocksStandardAndEvolvedAttacks()
    {
        AssertShieldBlocks(CardType.Kill);
        AssertShieldBlocks(CardType.PoisonKill);
        AssertShieldBlocks(CardType.ArrowBarrage);
        AssertShieldBlocks(CardType.Tuxi);
        AssertShieldBlocks(CardType.LightningStrike);
        AssertShieldBlocks(CardType.Fee);
    }

    private void AssertShieldBlocks(CardType attackType)
    {
        var (player, enemy, context) = CreateContext();
        player.RuntimeStates[MeihuoApplyEffect.ShieldStateKey] = true;
        player.RuntimeStates[MeihuoApplyEffect.CharmedTargetStateKey] = enemy;
        enemy.RuntimeStates[MeihuoApplyEffect.CharmedStateKey] = true;
        var damage = new DamageEvent(enemy, player, attackType, 10, isDirectAttackDamage: true);
        context.DamageEvent = damage;

        new MeihuoShieldEffect().Execute(context);

        Assert(damage.Cancelled, $"{BattleRules.GetCardName(attackType)}没有被魅惑护盾吸收");
        Assert(damage.WasFullyBlocked, $"{BattleRules.GetCardName(attackType)}没有记录为完全格挡");
        Assert(!HasShield(player), $"{BattleRules.GetCardName(attackType)}命中后魅惑护盾没有消耗");
        Assert(IsCharmed(enemy), $"{BattleRules.GetCardName(attackType)}命中后错误清除了敌人魅惑标记");
    }

    private void TestShieldBlocksNonAttackDamageThroughTriggerPipeline()
    {
        var triggerManager = new TriggerManager();
        triggerManager.Register(new MeihuoShieldEffect());
        triggerManager.Register(new ApplyDamageEffect());
        var (player, enemy, context) = CreateContext(triggerManager);
        player.RuntimeStates[MeihuoApplyEffect.ShieldStateKey] = true;
        player.RuntimeStates[MeihuoApplyEffect.CharmedTargetStateKey] = enemy;
        enemy.RuntimeStates[MeihuoApplyEffect.CharmedStateKey] = true;
        var healthBefore = player.Health;

        ResolveDamage(context, enemy, player, CardType.LightningStrike, 10);
        Assert(player.Health == healthBefore, "魅惑护盾没有在正式伤害管线中抵挡玩家受到的非攻击牌雷击伤害");
        Assert(!HasShield(player), "魅惑护盾抵挡一次伤害后没有被消耗");

        ResolveDamage(context, enemy, player, CardType.LightningStrike, 10);
        Assert(player.Health == healthBefore - 10, "魅惑护盾被消耗后仍错误抵挡玩家受到的第二次伤害");
    }

    private void TestInvalidDamageDoesNotConsumeShield()
    {
        var (player, enemy, context) = CreateContext();
        player.RuntimeStates[MeihuoApplyEffect.ShieldStateKey] = true;
        player.RuntimeStates[MeihuoApplyEffect.CharmedTargetStateKey] = enemy;
        enemy.RuntimeStates[MeihuoApplyEffect.CharmedStateKey] = true;

        context.DamageEvent = new DamageEvent(enemy, player, CardType.LightningStrike, 0);
        new MeihuoShieldEffect().Execute(context);
        Assert(HasShield(player), "0伤害事件错误消耗了魅惑护盾");

        var cancelledDamage = new DamageEvent(enemy, player, CardType.LightningStrike, 10)
        {
            Cancelled = true
        };
        context.DamageEvent = cancelledDamage;
        new MeihuoShieldEffect().Execute(context);
        Assert(HasShield(player), "已被其它防御取消的伤害错误消耗了魅惑护盾");
    }

    private static void ResolveDamage(
        BattleContext context,
        Player source,
        Player target,
        CardType type,
        int amount)
    {
        context.DamageEvent = new DamageEvent(source, target, type, amount);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
        context.DamageEvent = null;
    }

    private void TestSurvivingShieldActivatesControl()
    {
        var (player, enemy, context) = CreateContext();
        player.RuntimeStates[MeihuoApplyEffect.ShieldStateKey] = true;
        player.RuntimeStates[MeihuoApplyEffect.CharmedTargetStateKey] = enemy;
        enemy.RuntimeStates[MeihuoApplyEffect.CharmedStateKey] = true;

        new MeihuoActivateEffect().Execute(context);

        Assert(!HasShield(player), "存活到下回合的魅惑护盾没有移除");
        Assert(!IsCharmed(enemy), "魅惑转化为控制后没有移除敌人魅惑标记");
        Assert(enemy.IsFrozen, "存活到下回合的魅惑护盾没有让目标只能使用【费】");
    }

    private void TestConsumedShieldStillActivatesControl()
    {
        var (player, enemy, context) = CreateContext();
        player.RuntimeStates[MeihuoApplyEffect.ShieldStateKey] = true;
        player.RuntimeStates[MeihuoApplyEffect.CharmedTargetStateKey] = enemy;
        enemy.RuntimeStates[MeihuoApplyEffect.CharmedStateKey] = true;
        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10, isDirectAttackDamage: true);

        new MeihuoShieldEffect().Execute(context);
        Assert(!HasShield(player), "魅惑护盾抵挡伤害后没有消耗");
        Assert(IsCharmed(enemy), "魅惑护盾消耗后错误取消了魅惑");

        new MeihuoActivateEffect().Execute(context);
        Assert(enemy.IsFrozen, "魅惑护盾被消耗后，魅惑没有在下回合生效");
        Assert(!IsCharmed(enemy), "魅惑控制结算后没有清理目标标记");
    }

    private void TestMeihuoUsageCappedAtThreePerBattle()
    {
        var (player, enemy, context) = CreateContext();
        Assert(player.MeihuoUsesRemaining == 3, "测试前提失败：魅惑本场战斗初始次数应为3");

        for (var i = 1; i <= 3; i++)
        {
            context.PlayerAction = BattleAction.FromCard(Card.Meihuo(), 1, enemy);
            new MeihuoApplyEffect().Execute(context);
            Assert(HasShield(player), $"第{i}次使用魅惑应该正常给玩家施加护盾");
            Assert(player.MeihuoUsesRemaining == 3 - i, $"第{i}次使用魅惑后剩余次数应为{3 - i}");
        }

        Assert(player.MeihuoUsesRemaining == 0, "使用3次后魅惑剩余次数应归零");

        // 剩余次数耗尽后，ConsumeMeihuoUse 不应该继续把计数扣成负数（供出招栏
        // 判断"是否还能使用"的 MeihuoUsesRemaining > 0 逻辑一直保持非负、语义清晰）。
        context.PlayerAction = BattleAction.FromCard(Card.Meihuo(), 1, enemy);
        new MeihuoApplyEffect().Execute(context);
        Assert(player.MeihuoUsesRemaining == 0, "剩余次数耗尽后不应该被扣减为负数");
    }

    private static (Player Player, EnemyInstance Enemy, BattleContext Context) CreateContext()
    {
        return CreateContext(new TriggerManager());
    }

    private static (Player Player, EnemyInstance Enemy, BattleContext Context) CreateContext(TriggerManager triggerManager)
    {
        var character = CharacterDatabase.GetCharacter(CharacterIds.DiaoChan);
        var player = new Player(character.Name, character.Id, BattleTeam.Player);
        player.ResetForNewBattle(character.MaxHp, character.MaxHp, 3);
        player.SetCharacter(character);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.Meihuo)!);

        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "meihuo_test_enemy",
            Name = "魅惑测试敌人",
            MaxHP = 40,
            Type = EnemyType.Normal,
            StartingResource = 2,
            StartingDeck = new EnemyDeck()
        });
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        var context = new BattleContext(player, triggerManager)
        {
            Encounter = encounter,
            TurnCounter = 1
        };
        context.BeginRoundResult();
        return (player, enemy, context);
    }

    private static bool HasShield(Player player)
    {
        return player.RuntimeStates.TryGetValue(MeihuoApplyEffect.ShieldStateKey, out var value)
            && value is true;
    }

    private static bool IsCharmed(EnemyInstance enemy)
    {
        return enemy.RuntimeStates.TryGetValue(MeihuoApplyEffect.CharmedStateKey, out var value)
            && value is true;
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
