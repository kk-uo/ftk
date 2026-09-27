//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/StackedAttackClashRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证攻击牌发生克制时，克制方按实际叠加张数逐次造成伤害。
// 2. 验证玩家和敌方使用叠加火杀时遵守同一结算规则。
//
// 不负责：
// × 验证卡牌点击与叠加计时窗口。
// × 验证伤害特效与飘字。
//
// 主要依赖：
// BattlePhaseResolutionEffect
// BattleAction
// ApplyDamageEffect
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 叠加攻击牌克制结算的 Headless 回归入口。
/// </summary>
public partial class StackedAttackClashRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行玩家与敌方两个方向的叠加火杀回归用例。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestPlayerTripleFireKillAgainstSingleKill();
            TestEnemyTripleFireKillAgainstSingleKill();
            GD.Print($"STACKED_ATTACK_CLASH_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"STACKED_ATTACK_CLASH_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestPlayerTripleFireKillAgainstSingleKill()
    {
        var player = CreatePlayer();
        var enemy = CreateEnemy();
        var context = CreateContext(player, enemy);
        var enemyHealthBefore = enemy.Health;

        context.PlayerAction = BattleAction.FromCard(Card.FireKill(), 3, enemy);
        context.SetActionForEnemy(enemy, BattleAction.FromCard(Card.Kill(), 1, player));
        new BattlePhaseResolutionEffect().Execute(context);

        var expectedDamage = BattleConstants.FireKillDamage * 3;
        Assert(enemyHealthBefore - enemy.Health == expectedDamage,
            $"玩家火杀×3对杀×1应造成三次火杀伤害，共{expectedDamage}，实际={enemyHealthBefore - enemy.Health}");
        Assert(player.Health == player.MaxHealth, "被克制的敌方杀不应对玩家造成伤害");
    }

    private void TestEnemyTripleFireKillAgainstSingleKill()
    {
        var player = CreatePlayer();
        var enemy = CreateEnemy();
        var context = CreateContext(player, enemy);
        var playerHealthBefore = player.Health;

        context.PlayerAction = BattleAction.FromCard(Card.Kill(), 1, enemy);
        context.SetActionForEnemy(enemy, BattleAction.FromCard(Card.FireKill(), 3, player));
        new BattlePhaseResolutionEffect().Execute(context);

        var expectedDamage = BattleConstants.FireKillDamage * 3;
        Assert(playerHealthBefore - player.Health == expectedDamage,
            $"敌方火杀×3对杀×1应造成三次火杀伤害，共{expectedDamage}，实际={playerHealthBefore - player.Health}");
        Assert(enemy.Health == enemy.MaxHealth, "被克制的玩家杀不应对敌方造成伤害");
    }

    private static Player CreatePlayer()
    {
        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(100, 100, 10);
        return player;
    }

    private static EnemyInstance CreateEnemy()
    {
        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "stacked_attack_test_enemy",
            Name = "叠加测试敌人",
            MaxHP = 100,
            Type = EnemyType.Normal,
            StartingResource = 10,
            StartingDeck = new EnemyDeck()
        });
        enemy.DebugSetMana(10);
        return enemy;
    }

    private static BattleContext CreateContext(Player player, EnemyInstance enemy)
    {
        var triggerManager = new TriggerManager();
        triggerManager.Register(new ApplyDamageEffect());

        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);

        var context = new BattleContext(player, triggerManager)
        {
            Encounter = encounter,
            TurnNumber = 1,
            PlayerLockedTarget = enemy
        };
        context.BeginRoundResult();
        return context;
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
