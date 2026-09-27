//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/HookChainPerActionRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证钩索按实际出牌数累计未命中。
// 2. 验证多目标伤害事件不会把一张牌重复计数。
// 3. 验证相同定义的敌人不会共享钩索状态。
//
// 不负责：
// × 验证装备掉落。
// × 验证战斗表现动画。
//
// 主要依赖：
// HookChainEffect
// HookChainResetEffect
// BattleContext
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 钩索逐行动计数与敌人实例隔离的 Headless 回归入口。
/// </summary>
public partial class HookChainPerActionRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行钩索多目标及重复敌人实例用例。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestOneCardWithMultipleMissEventsCountsOnce();
            TestDuplicateEnemiesKeepIndependentChains();
            GD.Print($"HOOK_CHAIN_PER_ACTION_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"HOOK_CHAIN_PER_ACTION_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestOneCardWithMultipleMissEventsCountsOnce()
    {
        var player = CreatePlayer();
        var hookUser = CreateHookEnemy("钩索使用者");
        var bystander = CreatePlainEnemy("旁观目标");
        var context = CreateContext(player, hookUser, bystander);
        var tracker = new HookChainEffect();
        var finalizer = new HookChainResetEffect(tracker);

        context.PlayerAction = BattleAction.FromCard(Card.Fee(), 1, player);
        context.SetActionForEnemy(hookUser, BattleAction.FromCard(Card.Kill(), 1, player));
        context.SetActionForEnemy(bystander, BattleAction.FromCard(Card.Fee(), 1, bystander));
        RecordBlockedDamage(tracker, context, hookUser, player);
        RecordBlockedDamage(tracker, context, hookUser, bystander);
        finalizer.Execute(context);

        Assert(hookUser.Health == hookUser.MaxHealth, "一张牌的两个未命中事件错误触发了钩索自伤");
        Assert(player.Health == player.MaxHealth, "一张牌的两个未命中事件错误触发了钩索目标伤害");
        Assert(tracker.HasPendingMissChain(hookUser.BattleStateKey), "一张未命中的杀没有留下1次连续计数");

        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Fee(), 1, player);
        context.ClearEnemyActions();
        context.SetActionForEnemy(hookUser, BattleAction.FromCard(Card.Kill(), 1, player));
        context.SetActionForEnemy(bystander, BattleAction.FromCard(Card.Fee(), 1, bystander));
        RecordBlockedDamage(tracker, context, hookUser, player);
        finalizer.Execute(context);

        Assert(hookUser.MaxHealth - hookUser.Health == 5, "连续第二张未命中的杀没有触发钩索自伤");
        Assert(player.MaxHealth - player.Health == 5, "连续第二张未命中的杀没有触发钩索目标伤害");
    }

    private void TestDuplicateEnemiesKeepIndependentChains()
    {
        var player = CreatePlayer();
        var first = CreateHookEnemy("流亡蛮族甲");
        var second = CreateHookEnemy("流亡蛮族乙");
        var context = CreateContext(player, first, second);
        var tracker = new HookChainEffect();
        var finalizer = new HookChainResetEffect(tracker);

        context.PlayerAction = BattleAction.FromCard(Card.Fee(), 1, player);
        context.SetActionForEnemy(first, BattleAction.FromCard(Card.Kill(), 1, player));
        context.SetActionForEnemy(second, BattleAction.FromCard(Card.Kill(), 1, player));
        RecordBlockedDamage(tracker, context, first, player);
        RecordBlockedDamage(tracker, context, second, player);
        finalizer.Execute(context);

        Assert(first.BattleStateKey != second.BattleStateKey, "重复敌人没有独立战斗实例键");
        Assert(first.Health == first.MaxHealth && second.Health == second.MaxHealth,
            "两个相同定义敌人的第一次未命中错误合并触发了钩索");
        Assert(player.Health == player.MaxHealth, "两个敌人的独立首次未命中错误伤害了玩家");
        Assert(tracker.HasPendingMissChain(first.BattleStateKey), "第一名敌人的钩索计数没有独立保存");
        Assert(tracker.HasPendingMissChain(second.BattleStateKey), "第二名敌人的钩索计数没有独立保存");
    }

    private static void RecordBlockedDamage(
        HookChainEffect tracker,
        BattleContext context,
        Player source,
        Player target)
    {
        context.DamageEvent = new DamageEvent(source, target, CardType.Kill, BattleConstants.KillDamage);
        context.DamageEvent.CancelAsFullyBlocked();
        tracker.Execute(context);
        context.DamageEvent = null;
    }

    private static BattleContext CreateContext(Player player, params EnemyInstance[] enemies)
    {
        var encounter = new BattleEncounter();
        foreach (var enemy in enemies)
        {
            encounter.Enemies.Add(enemy);
        }

        var context = new BattleContext(player, new TriggerManager())
        {
            Encounter = encounter,
            TurnNumber = 1
        };
        context.BeginRoundResult();
        return context;
    }

    private static Player CreatePlayer()
    {
        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(100, 100, 10);
        return player;
    }

    private static EnemyInstance CreateHookEnemy(string name)
    {
        var definition = CreateDefinition("duplicate_hook_enemy", name);
        definition.EquipmentIds.Add(EquipmentIds.HookChain);
        return new EnemyInstance(definition);
    }

    private static EnemyInstance CreatePlainEnemy(string name)
    {
        return new EnemyInstance(CreateDefinition("plain_enemy", name));
    }

    private static EnemyDefinition CreateDefinition(string id, string name)
    {
        return new EnemyDefinition
        {
            Id = id,
            Name = name,
            MaxHP = 100,
            Type = EnemyType.Normal,
            StartingResource = 10,
            StartingDeck = new EnemyDeck()
        };
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
