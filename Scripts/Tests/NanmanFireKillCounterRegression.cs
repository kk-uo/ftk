//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/NanmanFireKillCounterRegression.cs
//
// 职责：验证【火杀】与【南蛮入侵】双向相遇时均互相抵消，双方不受伤。
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 火杀与南蛮入侵双向互消的 Headless 回归入口。
/// </summary>
public partial class NanmanFireKillCounterRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestPlayerNanmanAgainstEnemyFireKill();
            TestPlayerFireKillAgainstEnemyNanman();
            GD.Print($"NANMAN_FIRE_KILL_COUNTER_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"NANMAN_FIRE_KILL_COUNTER_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestPlayerNanmanAgainstEnemyFireKill()
    {
        var (player, enemy, context) = CreateContext();
        context.PlayerAction = BattleAction.FromCard(Card.NanmanInvasion());
        context.SetActionForEnemy(enemy, BattleAction.FromCard(Card.FireKill(), 1, player));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(player.Health == player.MaxHealth, "玩家南蛮入侵对敌方火杀时，玩家不应受伤");
        Assert(enemy.Health == enemy.MaxHealth, "玩家南蛮入侵对敌方火杀时，敌方不应受伤");
        Assert(context.RoundResult.Text.Contains("双方均未受到伤害", StringComparison.Ordinal),
            "南蛮入侵对火杀的互消结果缺少战报说明");
    }

    private void TestPlayerFireKillAgainstEnemyNanman()
    {
        var (player, enemy, context) = CreateContext();
        context.PlayerAction = BattleAction.FromCard(Card.FireKill(), 1, enemy);
        context.SetActionForEnemy(enemy, BattleAction.FromCard(Card.NanmanInvasion(), 1, player));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(player.Health == player.MaxHealth, "玩家火杀对敌方南蛮入侵时，玩家不应受伤");
        Assert(enemy.Health == enemy.MaxHealth, "玩家火杀对敌方南蛮入侵时，敌方不应受伤");
    }

    private static (Player Player, EnemyInstance Enemy, BattleContext Context) CreateContext()
    {
        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 10);

        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "nanman_firekill_counter_enemy",
            Name = "测试敌人",
            MaxHP = 40,
            Type = EnemyType.Normal,
            StartingResource = 10,
            StartingDeck = new EnemyDeck()
        });

        var triggerManager = new TriggerManager();
        triggerManager.Register(new ApplyDamageEffect());
        var context = new BattleContext(player, triggerManager);
        context.BeginRoundResult();
        return (player, enemy, context);
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
