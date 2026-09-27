//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/FireThunderArrowBarrageRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证火雷杀克制普通杀与火杀。
// 2. 验证火雷杀与雷杀互相抵消。
// 3. 验证火雷杀、必中杀对万箭齐发时双方独立造成一次伤害。
//
// 不负责：
// × 验证卡牌表现与飘字。
// × 验证费用支付与卡牌点击。
//
// 主要依赖：
// BattlePhaseResolutionEffect
// BattleRules
// ApplyDamageEffect
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 火雷杀克制关系与万箭齐发双向伤害的 Headless 回归入口。
/// </summary>
public partial class FireThunderArrowBarrageRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行所有克制与双向伤害用例，并通过进程退出码报告结果。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestFireThunderBeats(CardType.Kill);
            TestFireThunderBeats(CardType.FireKill);
            TestFireThunderCancelsThunder();
            TestArrowBarrageAgainstFireThunder(playerUsesBarrage: true);
            TestArrowBarrageAgainstFireThunder(playerUsesBarrage: false);
            TestArrowBarrageAgainstSureKill(playerUsesBarrage: true);
            TestArrowBarrageAgainstSureKill(playerUsesBarrage: false);
            TestShadowKillCountersArrowBarrage();
            TestStackedFireKillTargetsArrowBarrageOwnerOnly();
            GD.Print($"FIRE_THUNDER_ARROW_BARRAGE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"FIRE_THUNDER_ARROW_BARRAGE_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestFireThunderBeats(CardType opposingType)
    {
        var (player, enemy, context) = CreateContext();
        context.PlayerAction = BattleAction.FromCard(Card.FireThunderKill(), 1, enemy);
        context.SetActionForEnemy(enemy, BattleAction.FromCard(new Card(opposingType), 1, player));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(player.Health == player.MaxHealth,
            $"火雷杀对{BattleRules.GetCardName(opposingType)}时不应受到伤害");
        Assert(enemy.MaxHealth - enemy.Health == BattleRules.GetCardBaseDamage(CardType.FireThunderKill),
            $"火雷杀对{BattleRules.GetCardName(opposingType)}应造成30点伤害");
    }

    private void TestFireThunderCancelsThunder()
    {
        var (player, enemy, context) = CreateContext();
        context.PlayerAction = BattleAction.FromCard(Card.FireThunderKill(), 1, enemy);
        context.SetActionForEnemy(enemy, BattleAction.FromCard(Card.ThunderKill(), 1, player));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(player.Health == player.MaxHealth, "火雷杀与雷杀互消时玩家不应受伤");
        Assert(enemy.Health == enemy.MaxHealth, "火雷杀与雷杀互消时敌人不应受伤");
    }

    private void TestArrowBarrageAgainstFireThunder(bool playerUsesBarrage)
    {
        var (player, enemy, context) = CreateContext();
        SetOpposingActions(
            context,
            player,
            enemy,
            playerUsesBarrage ? Card.ArrowBarrage() : Card.FireThunderKill(),
            playerUsesBarrage ? Card.FireThunderKill() : Card.ArrowBarrage());

        new BattlePhaseResolutionEffect().Execute(context);

        var barrageUserDamage = playerUsesBarrage
            ? player.MaxHealth - player.Health
            : enemy.MaxHealth - enemy.Health;
        var fireThunderUserDamage = playerUsesBarrage
            ? enemy.MaxHealth - enemy.Health
            : player.MaxHealth - player.Health;
        Assert(barrageUserDamage == BattleRules.GetCardBaseDamage(CardType.FireThunderKill),
            $"万箭齐发使用者应受到30点火雷杀伤害，实际={barrageUserDamage}");
        Assert(fireThunderUserDamage == BattleConstants.KillDamage,
            $"火雷杀使用者应受到10点万箭齐发伤害，实际={fireThunderUserDamage}");
    }

    private void TestArrowBarrageAgainstSureKill(bool playerUsesBarrage)
    {
        var (player, enemy, context) = CreateContext();
        SetOpposingActions(
            context,
            player,
            enemy,
            playerUsesBarrage ? Card.ArrowBarrage() : Card.SureKill(),
            playerUsesBarrage ? Card.SureKill() : Card.ArrowBarrage());

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(player.MaxHealth - player.Health == BattleConstants.KillDamage,
            $"必中杀对万箭齐发时玩家应受到10点伤害，实际={player.MaxHealth - player.Health}");
        Assert(enemy.MaxHealth - enemy.Health == BattleConstants.KillDamage,
            $"必中杀对万箭齐发时敌人应受到10点伤害，实际={enemy.MaxHealth - enemy.Health}");
    }

    private void TestShadowKillCountersArrowBarrage()
    {
        var (player, enemy, context) = CreateContext();
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.YingXi)!);
        player.EnterShadowState();
        context.TriggerManager.Register(new YingXiInvincibilityEffect());
        SetOpposingActions(context, player, enemy, Card.ShadowKill(), Card.ArrowBarrage());

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(enemy.MaxHealth - enemy.Health == BattleConstants.KillDamage,
            "影袭杀对万箭齐发应反制并对万箭使用者造成10点伤害");
        Assert(player.Health == player.MaxHealth,
            "影袭状态下使用影袭杀反制万箭齐发后不应受到箭雨伤害");
        Assert(context.RoundResult.Text.Contains("影袭杀克制万箭齐发", StringComparison.Ordinal),
            "影袭杀反制万箭齐发时缺少正确的克制结果日志");
    }

    private void TestStackedFireKillTargetsArrowBarrageOwnerOnly()
    {
        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(100, 100, 10);
        var left = CreateEnemy("arrow_binding_enemy", "左侧敌人");
        var middle = CreateEnemy("arrow_binding_enemy", "中间敌人");
        var right = CreateEnemy("arrow_binding_enemy", "右侧敌人");
        var triggerManager = new TriggerManager();
        triggerManager.Register(new ApplyDamageEffect());
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(left);
        encounter.Enemies.Add(middle);
        encounter.Enemies.Add(right);
        var context = new BattleContext(player, triggerManager)
        {
            Encounter = encounter,
            TurnNumber = 1
        };
        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.FireKill(), 2, middle);
        context.PlayerLockedTarget = middle;
        context.SetActionForEnemy(left, BattleAction.FromCard(Card.Dodge(), 1, left));
        context.SetActionForEnemy(middle, BattleAction.FromCard(Card.ArrowBarrage(), 1, player));
        context.SetActionForEnemy(right, BattleAction.FromCard(Card.Dodge(), 1, right));

        new BattlePhaseResolutionEffect().Execute(context);

        Assert(left.Health == left.MaxHealth, "左侧敌人的闪错误替中间敌人格挡火杀");
        Assert(right.Health == right.MaxHealth, "右侧敌人的闪错误替中间敌人格挡火杀");
        Assert(
            middle.MaxHealth - middle.Health == BattleRules.GetCardBaseDamage(CardType.FireKill) * 2,
            $"火杀×2没有逐张命中万箭齐发使用者，实际伤害={middle.MaxHealth - middle.Health}");
        Assert(
            player.MaxHealth - player.Health == BattleConstants.KillDamage,
            $"玩家应只承受一次万箭齐发伤害，实际伤害={player.MaxHealth - player.Health}");
        Assert(context.GetTargetEnemyActionEntry()?.Enemy == middle, "玩家锁定目标没有解析到中间敌人实例");
        Assert(
            context.TriggerLogs.Exists(line => line.Contains(middle.BattleStateKey, StringComparison.Ordinal)
                && line.Contains("ResolvedTargetEntry", StringComparison.Ordinal)),
            "目标绑定调试日志没有记录中间敌人实例");
    }

    private static void SetOpposingActions(
        BattleContext context,
        Player player,
        EnemyInstance enemy,
        Card playerCard,
        Card enemyCard)
    {
        context.PlayerAction = BattleAction.FromCard(playerCard, 1, enemy);
        context.SetActionForEnemy(enemy, BattleAction.FromCard(enemyCard, 1, player));
    }

    private static (Player Player, EnemyInstance Enemy, BattleContext Context) CreateContext()
    {
        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(100, 100, 10);
        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "fire_thunder_arrow_test_enemy",
            Name = "结算测试敌人",
            MaxHP = 100,
            Type = EnemyType.Normal,
            StartingResource = 10,
            StartingDeck = new EnemyDeck()
        });
        enemy.DebugSetMana(10);

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
        return (player, enemy, context);
    }

    private static EnemyInstance CreateEnemy(string id, string name)
    {
        return new EnemyInstance(new EnemyDefinition
        {
            Id = id,
            Name = name,
            MaxHP = 100,
            Type = EnemyType.Normal,
            StartingResource = 10,
            StartingDeck = new EnemyDeck()
        });
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
