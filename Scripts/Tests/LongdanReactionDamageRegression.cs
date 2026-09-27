using Godot;
using System;
using System.Linq;

/// <summary>
/// 验证龙胆反应可在【闪】无指定目标时正确绑定敌方攻击，并按克制关系结算反击伤害。
/// </summary>
public partial class LongdanReactionDamageRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestFireKillCounterattacksKillWithoutExplicitTarget();
            TestKillCountersThunderAndRestoresDamageTakenThroughDodge();
            GD.Print($"LONGDAN_REACTION_DAMAGE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"LONGDAN_REACTION_DAMAGE_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestFireKillCounterattacksKillWithoutExplicitTarget()
    {
        var (player, enemy, context) = CreateContext(CardType.Kill);
        var reaction = new LongdanReaction(player.CurrentMana);
        var fireKill = reaction.Options.Single(option => option.CardType == CardType.FireKill);

        Assert(fireKill.Enabled, "费用充足时火杀龙胆选项被错误禁用");
        fireKill.Resolve(context);

        Assert(player.Health == player.MaxHealth, "闪挡住普通杀后，火杀龙胆反击不应令玩家受伤");
        Assert(enemy.MaxHealth - enemy.Health == BattleRules.GetCardBaseDamage(CardType.FireKill),
            "龙胆火杀对敌方杀没有造成火杀伤害");
        Assert(context.RoundResult.Text.Contains("火杀克制杀", StringComparison.Ordinal),
            "龙胆火杀反击缺少克制战报");
    }

    private void TestKillCountersThunderAndRestoresDamageTakenThroughDodge()
    {
        var (player, enemy, context) = CreateContext(CardType.ThunderKill);
        var playerHealthBeforeThunder = player.Health;
        BattlePhaseResolutionEffect.DealAttackDamage(context, enemy, player, CardType.ThunderKill);
        Assert(player.Health < playerHealthBeforeThunder, "测试前提失败：雷杀没有穿透闪造成伤害");

        var reaction = new LongdanReaction(player.CurrentMana);
        var kill = reaction.Options.Single(option => option.CardType == CardType.Kill);
        kill.Resolve(context);

        Assert(player.Health == playerHealthBeforeThunder, "龙胆普通杀克制雷杀后没有撤销本回合雷杀伤害");
        Assert(enemy.MaxHealth - enemy.Health == BattleRules.GetCardBaseDamage(CardType.Kill),
            "龙胆普通杀克制雷杀后没有对敌人造成伤害");
        Assert(context.RoundResult.Text.Contains("杀克制雷杀", StringComparison.Ordinal),
            "龙胆普通杀反击缺少克制战报");
    }

    private static (Player Player, EnemyInstance Enemy, BattleContext Context) CreateContext(CardType enemyCard)
    {
        var player = new Player("玩家", "longdan_reaction_player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 5);

        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "longdan_reaction_enemy",
            Name = "训练傀儡",
            MaxHP = 100,
            Type = EnemyType.Normal,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });

        var triggerManager = new TriggerManager();
        triggerManager.Register(new ApplyDamageEffect());
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        var context = new BattleContext(player, triggerManager) { Encounter = encounter };
        context.BeginRoundResult();
        // 特意不写 PlayerLockedTarget，也不为【闪】设 Target：复现玩家报告的实际路径。
        context.PlayerAction = BattleAction.FromCard(Card.Dodge());
        context.SetActionForEnemy(enemy, BattleAction.FromCard(new Card(enemyCard), 1, player));
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
