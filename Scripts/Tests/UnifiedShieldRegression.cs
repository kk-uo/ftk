//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/UnifiedShieldRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证酒、桃、魅惑与仁德统一按层抵挡完整伤害。
// 2. 验证酒桃多层护盾逐层撤销对应卡牌效果。
// 3. 验证仁德固定两层且不会跨回合保留。
// 4. 验证多敌人结算时护盾优先抵挡最高单次伤害。
//
// 不负责：
// × 验证护盾图标和动画。
// × 验证卡牌点击输入。
//
// 主要依赖：
// DefenseBeforeDamageEffect
// RendePlayerShieldAbsorbEffect
// BattlePhaseResolutionEffect
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 统一护盾语义的 Headless 回归入口。
/// </summary>
public partial class UnifiedShieldRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行统一护盾回归并通过进程退出码返回结果。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestPeachLayersCancelMatchingHealing();
            TestWineLayersCancelMatchingPower();
            TestZeroDamageDoesNotConsumeCardShield();
            TestRendeHasTwoCurrentTurnLayers();
            TestHighestSingleDamageConsumesShieldFirst();
            TestHighestDamagePriorityDuringAttackClashes();
            TestZiBaoRestrictedDefense();
            GD.Print($"UNIFIED_SHIELD_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"UNIFIED_SHIELD_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestPeachLayersCancelMatchingHealing()
    {
        var (player, enemies, context) = CreateContext(2);
        context.PlayerPeachShieldLayers = 2;
        context.PlayerPeachHealGranted = 2;

        ResolveDamage(context, enemies[0], player, CardType.Kill, 10);
        ResolveDamage(context, enemies[1], player, CardType.FireKill, 15);

        Assert(player.Health == player.MaxHealth, "两层桃护盾没有分别抵挡两次伤害");
        Assert(context.PlayerPeachShieldLayers == 0, "桃护盾层数没有逐次减一");
        Assert(context.PlayerPeachHealGranted == 0, "桃护盾被击破后没有逐层撤销对应回复");
    }

    private void TestWineLayersCancelMatchingPower()
    {
        var (player, enemies, context) = CreateContext(2);
        context.PlayerWineShieldLayers = 2;
        player.QueueWinePower(2);

        ResolveDamage(context, enemies[0], player, CardType.Kill, 10);
        ResolveDamage(context, enemies[1], player, CardType.FireKill, 15);

        Assert(player.Health == player.MaxHealth, "两层酒护盾没有分别抵挡两次伤害");
        Assert(context.PlayerWineShieldLayers == 0, "酒护盾层数没有逐次减一");
        Assert(player.PendingWinePower == 0, "酒护盾被击破后没有逐层撤销对应增伤");
    }

    private void TestZeroDamageDoesNotConsumeCardShield()
    {
        var (player, enemies, context) = CreateContext(1);
        context.PlayerPeachShieldLayers = 1;
        context.PlayerWineShieldLayers = 1;
        player.QueueWinePower(1);

        ResolveDamage(context, enemies[0], player, CardType.Kill, 0);

        Assert(context.PlayerPeachShieldLayers == 1, "0伤害错误消耗桃护盾");
        Assert(context.PlayerWineShieldLayers == 1, "0伤害错误消耗酒护盾");
        Assert(player.PendingWinePower == 1, "0伤害错误撤销酒增伤");
    }

    private void TestRendeHasTwoCurrentTurnLayers()
    {
        var triggerManager = new TriggerManager();
        triggerManager.Register(new DefenseBeforeDamageEffect());
        triggerManager.Register(new RendePlayerShieldAbsorbEffect());
        triggerManager.Register(new ApplyDamageEffect());
        var (player, enemies, context) = CreateContext(1, triggerManager);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.Rende)!);

        new RendePlayerShieldReplenishEffect().Execute(context);
        Assert(GetRendeLayers(player) == 2, "仁德每回合没有固定刷新为两层护盾");
        Assert(
            BattleUnitUiFormatter.GetStatuses(player, context)
                .Any(status => status.Id == "rende_shield" && status.StackCount == 2),
            "仁德两层护盾没有进入统一状态 UI 数据");

        ResolveDamage(context, enemies[0], player, CardType.FireKill, 15);
        ResolveDamage(context, enemies[0], player, CardType.Kill, 10);
        Assert(player.Health == player.MaxHealth, "仁德两层没有各自抵挡一次完整伤害");
        Assert(GetRendeLayers(player) == 0, "仁德护盾没有逐层消耗");

        ResolveDamage(context, enemies[0], player, CardType.Kill, 10);
        Assert(player.Health == player.MaxHealth - 10, "仁德层数耗尽后仍错误抵挡伤害");

        player.RuntimeStates["rende_shield_player"] = 2;
        new TurnEndCleanupEffect().Execute(context);
        Assert(!player.RuntimeStates.ContainsKey("rende_shield_player"), "仁德护盾错误跨回合保留");
    }

    private void TestHighestSingleDamageConsumesShieldFirst()
    {
        var (player, enemies, context) = CreateContext(2);
        context.PlayerAction = BattleAction.FromCard(Card.Peach());
        context.SetActionForEnemy(enemies[0], BattleAction.FromCard(Card.Kill(), 1, player));
        context.SetActionForEnemy(enemies[1], BattleAction.FromCard(Card.FireKill(), 1, player));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(
            player.MaxHealth - player.Health == BattleRules.GetCardBaseDamage(CardType.Kill),
            "桃护盾没有优先抵挡火杀，或错误抵挡了较低的普通杀");
        Assert(context.PlayerPeachShieldLayers == 0, "优先抵挡后桃护盾没有正确消耗");
        Assert(context.PlayerPeachHealGranted == 0, "桃护盾抵挡最高伤害后仍错误保留回复");
    }

    private void TestHighestDamagePriorityDuringAttackClashes()
    {
        var (player, enemies, context) = CreateContext(2);
        context.PlayerWineShieldLayers = 1;
        player.QueueWinePower(1);
        context.PlayerAction = BattleAction.FromCard(Card.Kill(), 1, enemies[0]);
        context.PlayerLockedTarget = enemies[0];
        context.SetActionForEnemy(enemies[0], BattleAction.FromCard(Card.FireKill(), 1, player));
        context.SetActionForEnemy(enemies[1], BattleAction.FromCard(Card.FireThunderKill(), 1, player));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(
            player.MaxHealth - player.Health == BattleRules.GetCardBaseDamage(CardType.FireKill),
            "攻击碰撞提前结算，导致护盾没有优先抵挡更高伤害的火雷杀");
        Assert(enemies[0].Health == enemies[0].MaxHealth, "玩家普通杀错误伤害了克制它的火杀目标");
        Assert(enemies[1].Health == enemies[1].MaxHealth, "玩家普通杀错误扩散到未锁定敌人");
        Assert(context.PlayerWineShieldLayers == 0, "攻击碰撞中的最高伤害没有消耗酒护盾");
    }

    private void TestZiBaoRestrictedDefense()
    {
        var (playerWithDodge, dodgeEnemies, dodgeContext) = CreateContext(1);
        dodgeContext.PlayerDodgeDefenseActive = true;
        dodgeContext.PlayerDodgeLayers = 1;
        ResolveZiBaoDamage(dodgeContext, dodgeEnemies[0], playerWithDodge, 24);
        Assert(playerWithDodge.Health == playerWithDodge.MaxHealth - 24,
            "自爆伤害错误地被闪抵挡");

        var (playerWithPeach, peachEnemies, peachContext) = CreateContext(1);
        peachContext.PlayerPeachShieldLayers = 1;
        ResolveZiBaoDamage(peachContext, peachEnemies[0], playerWithPeach, 24);
        Assert(playerWithPeach.Health == playerWithPeach.MaxHealth && peachContext.PlayerPeachShieldLayers == 0,
            "自爆伤害没有被桃盾抵挡");

        var (playerWithNegate, negateEnemies, negateContext) = CreateContext(1);
        negateContext.PlayerCounterDefenseActive = true;
        ResolveZiBaoDamage(negateContext, negateEnemies[0], playerWithNegate, 24);
        Assert(playerWithNegate.Health == playerWithNegate.MaxHealth,
            "自爆伤害没有被无懈可击抵挡");
    }

    private static (Player Player, EnemyInstance[] Enemies, BattleContext Context) CreateContext(
        int enemyCount,
        TriggerManager? triggerManager = null)
    {
        triggerManager ??= CreateShieldTriggerManager();
        var player = new Player("护盾测试玩家", "shield_test_player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 10);
        var encounter = new BattleEncounter();
        var enemies = new EnemyInstance[enemyCount];
        for (var i = 0; i < enemyCount; i++)
        {
            enemies[i] = new EnemyInstance(new EnemyDefinition
            {
                Id = $"shield_test_enemy_{i}",
                Name = $"护盾测试敌人{i}",
                MaxHP = 40,
                Type = EnemyType.Normal,
                StartingResource = 2,
                StartingDeck = new EnemyDeck()
            });
            encounter.Enemies.Add(enemies[i]);
        }

        var context = new BattleContext(player, triggerManager)
        {
            Encounter = encounter,
            TurnNumber = 1
        };
        context.BeginRoundResult();
        return (player, enemies, context);
    }

    private static TriggerManager CreateShieldTriggerManager()
    {
        var triggerManager = new TriggerManager();
        triggerManager.Register(new DefenseBeforeDamageEffect());
        triggerManager.Register(new ApplyDamageEffect());
        return triggerManager;
    }

    private static void ResolveDamage(
        BattleContext context,
        Player source,
        Player target,
        CardType cardType,
        int amount)
    {
        context.DamageEvent = new DamageEvent(source, target, cardType, amount, isDirectAttackDamage: true);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
        context.DamageEvent = null;
    }

    private static void ResolveZiBaoDamage(BattleContext context, Player source, Player target, int amount)
    {
        context.DamageEvent = new DamageEvent(
            source,
            target,
            CardType.ZiBaoAttack,
            amount,
            isDirectAttackDamage: true,
            onlyAllowCounterOrCardShields: true);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
        context.DamageEvent = null;
    }

    private static int GetRendeLayers(Player player)
    {
        return player.RuntimeStates.TryGetValue("rende_shield_player", out var value) && value is int layers
            ? layers
            : 0;
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
