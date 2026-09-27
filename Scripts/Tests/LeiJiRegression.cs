//////////////////////////////////////////////////////////
// 雷击：所有攻击牌被成功防御抵消后均应触发的回归。
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 验证【雷击】能接收万箭齐发等专属结算路径发出的取消伤害事件。
/// </summary>
public partial class LeiJiRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestDodgeBlocksArrowBarrage();
            TestCounterDefenseBlocksAllNanmanStyleAttackCards();
            GD.Print($"LEIJI_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"LEIJI_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    /// <summary>
    /// 复现报告路径：张角以【闪】成功防住敌方【万箭齐发】。
    /// </summary>
    private void TestDodgeBlocksArrowBarrage()
    {
        var (player, enemy, context) = CreateContext();
        context.PlayerAction = BattleAction.FromCard(Card.Dodge());
        context.SetActionForEnemy(enemy, BattleAction.FromCard(Card.ArrowBarrage(), 1, player));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(player.Health == player.MaxHealth, "闪防住万箭齐发后张角仍然受伤");
        Assert(enemy.MaxHealth - enemy.Health == 10,
            "闪防住万箭齐发后【雷击】没有对攻击者造成10点雷属性伤害");
        Assert(context.RoundResult.Text.Contains("【雷击】", StringComparison.Ordinal),
            "闪防住万箭齐发后战报没有记录【雷击】");
    }

    /// <summary>
    /// 南蛮入侵、突袭、天体撞击也走专属对撞结算；无懈抵消时同样必须通知雷击。
    /// </summary>
    private void TestCounterDefenseBlocksAllNanmanStyleAttackCards()
    {
        foreach (var attackType in new[] { CardType.NanmanInvasion, CardType.Tuxi, CardType.CelestialImpact })
        {
            var (player, enemy, context) = CreateContext();
            var attackAction = BattleAction.FromCard(new Card(attackType), 1, player);
            var defenseAction = BattleAction.FromCard(Card.Unassailable());

            BattlePhaseResolutionEffect.ResolveNanmanInvasion(context, enemy, attackAction, player, defenseAction);

            Assert(player.Health == player.MaxHealth,
                $"无懈抵消{BattleRules.GetCardName(attackType)}后张角仍然受伤");
            Assert(enemy.MaxHealth - enemy.Health == 10,
                $"无懈抵消{BattleRules.GetCardName(attackType)}后【雷击】没有触发");
        }
    }

    private static (Player Player, EnemyInstance Enemy, BattleContext Context) CreateContext()
    {
        var player = new Player("张角", CharacterIds.ZhangJiao, BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 5);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.LeiJi)!);

        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "leiji_regression_enemy",
            Name = "攻击测试敌人",
            MaxHP = 100,
            Type = EnemyType.Normal,
            StartingResource = 5,
            StartingDeck = new EnemyDeck()
        });

        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        var triggerManager = new TriggerManager();
        triggerManager.Register(new ApplyDamageEffect());
        triggerManager.Register(new LeiJiEffect());
        var context = new BattleContext(player, triggerManager)
        {
            Encounter = encounter,
            PlayerLockedTarget = enemy,
            TurnNumber = 1
        };
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
