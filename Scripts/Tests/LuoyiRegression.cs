using Godot;
using System;

/// <summary>验证【裸衣】的穿透、护盾与攻击视为出费规则。</summary>
public partial class LuoyiRegression : Node
{
    private int _assertions;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestDodgeIsIgnoredAndAttackDoesNotGainFee();
            TestShieldStillBlocksAttack();
            TestEnemyAttackStillResolvesIndependently();
            TestIncomingDamageIsNormalWhenNoAttackWasPlayed();
            GD.Print($"LUOYI_TEST_PASS assertions={_assertions}");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"LUOYI_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestDodgeIsIgnoredAndAttackDoesNotGainFee()
    {
        var (player, enemy, context) = CreateContext(Card.Dodge());
        new BattlePhaseResolutionEffect().Execute(context);
        Assert(enemy.MaxHealth - enemy.Health == BattleConstants.KillDamage, "裸衣攻击应无视闪");
        Assert(Math.Abs(player.CurrentMana - 4d) < 0.001, "裸衣攻击应支付杀的费用，且不应获得费");
    }

    private void TestShieldStillBlocksAttack()
    {
        var (player, enemy, context) = CreateContext(Card.Wine());
        new BattlePhaseResolutionEffect().Execute(context);
        Assert(enemy.Health == enemy.MaxHealth, "裸衣攻击不应穿透酒盾");
        Assert(Math.Abs(player.CurrentMana - 4d) < 0.001, "护盾抵挡后裸衣攻击也不应获得费");
    }

    private void TestEnemyAttackStillResolvesIndependently()
    {
        var (player, enemy, context) = CreateContext(Card.ThunderKill());
        new BattlePhaseResolutionEffect().Execute(context);
        Assert(enemy.MaxHealth - enemy.Health == BattleConstants.KillDamage, "裸衣攻击仍应命中目标");
        Assert(player.MaxHealth - player.Health == BattleConstants.KillDamage * 2, "裸衣攻击不应抵消雷杀，且使用攻击牌时自身伤害应翻倍");
    }

    private void TestIncomingDamageIsNormalWhenNoAttackWasPlayed()
    {
        var (player, _, context) = CreateContext(Card.ThunderKill(), Card.Fee());
        new BattlePhaseResolutionEffect().Execute(context);
        Assert(player.MaxHealth - player.Health == BattleConstants.KillDamage,
            "裸衣未使用攻击牌时不应承受双倍伤害");
    }

    private static (Player Player, EnemyInstance Enemy, BattleContext Context) CreateContext(Card enemyCard, Card? playerCard = null)
    {
        var player = new Player("祢衡", CharacterIds.MiHeng, BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 5);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.Luoyi)!);
        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "luoyi_regression_enemy",
            Name = "测试敌人",
            MaxHP = 100,
            StartingResource = 5,
            StartingDeck = new EnemyDeck()
        });
        var triggerManager = new TriggerManager();
        triggerManager.Register(new DefenseBeforeDamageEffect());
        triggerManager.Register(new LuoyiVulnerableEffect());
        triggerManager.Register(new ApplyDamageEffect());
        var context = new BattleContext(player, triggerManager);
        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(playerCard ?? Card.Kill(), 1, enemy);
        context.PlayerLockedTarget = enemy;
        context.SetActionForEnemy(enemy, BattleAction.FromCard(enemyCard, 1, player));
        return (player, enemy, context);
    }

    private void Assert(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
