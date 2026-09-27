//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/CelestialImpactKillCounterRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证天体撞击完整复用南蛮入侵的响应关系。
// 2. 验证天体撞击保持1费，并按南蛮基础伤害造成10点伤害。
//
// 不负责：
// × 覆盖无懈可击/闪/必中杀/顺手牵羊等既有分支（未改动，行为不变）。
//
// 主要依赖：
// BattlePhaseResolutionEffect.ResolveCelestialImpact
// BattlePhaseResolutionEffect.ResolveNanmanInvasion
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 天体撞击 vs 杀系反制的 Headless 回归入口。
/// </summary>
public partial class CelestialImpactKillCounterRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Run();
            GD.Print($"CELESTIAL_IMPACT_KILL_COUNTER_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"CELESTIAL_IMPACT_KILL_COUNTER_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run()
    {
        Localization.Initialize();
        TestCardDefinitionMatchesNanman();
        TestOrdinaryKillCancelsCelestialImpact();
        TestIceKillCancelsCelestialImpact();
        TestDodgeCannotBlockCelestialImpact();
        TestSureKillCountersCelestialImpact();
        TestShadowKillCountersNanmanStyleAttacks();
        TestOffensiveCardsCanStack();
        TestStackedNanmanInvasionDealsEachHit();
        TestStackedCelestialImpactDealsEachHit();
        TestStackedArrowBarrageDealsEachHit();
    }

    private void TestOrdinaryKillCancelsCelestialImpact()
    {
        var attacker = new EnemyInstance(new EnemyDefinition
        {
            Id = "moon_boss_test",
            Name = "月亮",
            MaxHP = 120,
            Type = EnemyType.Boss,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });

        var target = new Player("玩家", "player", BattleTeam.Player);
        target.ResetForNewBattle(40, 40, 1);

        var triggerManager = new TriggerManager();
        triggerManager.Register(new DefenseBeforeDamageEffect());
        triggerManager.Register(new ApplyDamageEffect());
        var context = new BattleContext(target, triggerManager);
        context.BeginRoundResult();

        var responseAction = BattleAction.FromCard(Card.Kill());
        var attackerAction = BattleAction.FromCard(Card.CelestialImpact());

        var attackerHealthBefore = attacker.Health;
        var targetHealthBefore = target.Health;

        BattlePhaseResolutionEffect.ResolveCelestialImpact(context, attacker, attackerAction, target, responseAction);

        Assert(attacker.Health == attackerHealthBefore, "普通杀与天体撞击互消时，天体撞击使用者不应受伤");
        Assert(target.Health == targetHealthBefore, "普通杀与天体撞击互消时，普通杀使用者不应受伤");
    }

    private void TestCardDefinitionMatchesNanman()
    {
        var card = Card.CelestialImpact();
        Assert(Math.Abs(card.Cost - 1d) < 0.001, "天体撞击费用应为1");
        Assert(card.IsAttack && card.IsTrickCard, "天体撞击应与南蛮入侵一样属于攻击性锦囊");
        Assert(BattleRules.GetCardBaseDamage(CardType.CelestialImpact) == BattleConstants.KillDamage,
            "天体撞击基础伤害应与南蛮入侵一致为10");
    }

    private void TestIceKillCancelsCelestialImpact()
    {
        var attacker = new EnemyInstance(new EnemyDefinition
        {
            Id = "moon_boss_test2",
            Name = "月亮",
            MaxHP = 120,
            Type = EnemyType.Boss,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });

        var target = new Player("玩家", "player", BattleTeam.Player);
        target.ResetForNewBattle(40, 40, 1);

        var triggerManager = new TriggerManager();
        triggerManager.Register(new ApplyDamageEffect());
        var context = new BattleContext(target, triggerManager);
        context.BeginRoundResult();

        var responseAction = BattleAction.FromCard(Card.IceKill());
        var attackerAction = BattleAction.FromCard(Card.CelestialImpact());

        var attackerHealthBefore = attacker.Health;
        var targetHealthBefore = target.Health;

        BattlePhaseResolutionEffect.ResolveCelestialImpact(context, attacker, attackerAction, target, responseAction);

        Assert(attacker.Health == attackerHealthBefore, "冰杀与天体撞击互消时，天体撞击使用者不应受伤");
        Assert(target.Health == targetHealthBefore, "冰杀与天体撞击互消时，冰杀使用者不应受伤");
        Assert(!attacker.IsFrozen, "南蛮入侵互消规则不应额外施加冰冻");
    }

    private void TestDodgeCannotBlockCelestialImpact()
    {
        var (attacker, target, context) = CreateUnits();
        BattlePhaseResolutionEffect.ResolveCelestialImpact(
            context,
            attacker,
            BattleAction.FromCard(Card.CelestialImpact()),
            target,
            BattleAction.FromCard(Card.Dodge()));

        Assert(target.MaxHealth - target.Health == BattleConstants.KillDamage,
            "闪不能抵挡天体撞击，目标应受到10点伤害");
    }

    private void TestSureKillCountersCelestialImpact()
    {
        var (attacker, target, context) = CreateUnits();
        BattlePhaseResolutionEffect.ResolveCelestialImpact(
            context,
            attacker,
            BattleAction.FromCard(Card.CelestialImpact()),
            target,
            BattleAction.FromCard(Card.SureKill()));

        Assert(attacker.MaxHealth - attacker.Health == BattleConstants.KillDamage,
            "必中杀应按南蛮入侵规则反击天体撞击使用者");
        Assert(target.Health == target.MaxHealth, "必中杀反击时自身不应受到天体撞击伤害");
    }

    private void TestShadowKillCountersNanmanStyleAttacks()
    {
        foreach (var attackType in new[] { CardType.NanmanInvasion, CardType.Tuxi, CardType.CelestialImpact })
        {
            var (attacker, target, context) = CreateUnits();
            BattlePhaseResolutionEffect.ResolveNanmanInvasion(
                context,
                attacker,
                BattleAction.FromCard(new Card(attackType)),
                target,
                BattleAction.FromCard(Card.ShadowKill()));

            Assert(attacker.MaxHealth - attacker.Health == BattleConstants.KillDamage,
                $"影袭杀应反制{BattleRules.GetCardName(attackType)}并造成10点伤害");
            Assert(target.Health == target.MaxHealth,
                $"影袭杀反制{BattleRules.GetCardName(attackType)}时自身不应受到伤害");
            Assert(context.RoundResult.Text.Contains($"影袭杀克制{BattleRules.GetCardName(attackType)}", StringComparison.Ordinal),
                $"影袭杀反制{BattleRules.GetCardName(attackType)}时缺少克制结果日志");
        }
    }

    private void TestOffensiveCardsCanStack()
    {
        foreach (var type in new[]
                 {
                     CardType.Kill, CardType.FireKill, CardType.ThunderKill, CardType.FireThunderKill,
                     CardType.SureKill, CardType.IceKill, CardType.CelestialImpact, CardType.ArrowBarrage,
                     CardType.NanmanInvasion, CardType.Tuxi, CardType.PoisonKill, CardType.FireAttack
                 })
        {
            Assert(BattleAction.CanStackType(type), $"攻击性牌【{BattleRules.GetCardName(type)}】应支持同回合连续使用");
        }

        Assert(!BattleAction.CanStackType(CardType.ShadowKill), "影袭杀每次影袭期只能使用一次，不应被通用叠加规则绕过");
    }

    private void TestStackedCelestialImpactDealsEachHit()
    {
        var (attacker, target, context) = CreateUnits();
        BattlePhaseResolutionEffect.ResolveCelestialImpact(
            context,
            attacker,
            BattleAction.FromCard(Card.CelestialImpact(), 3),
            target,
            BattleAction.FromCard(Card.Fee()));

        Assert(target.MaxHealth - target.Health == BattleConstants.KillDamage * 3,
            "天体撞击×3面对费时应逐段造成3次伤害");
    }

    private void TestStackedNanmanInvasionDealsEachHit()
    {
        var (attacker, target, context) = CreateUnits();
        BattlePhaseResolutionEffect.ResolveNanmanInvasion(
            context,
            attacker,
            BattleAction.FromCard(Card.NanmanInvasion(), 3),
            target,
            BattleAction.FromCard(Card.Fee()));

        Assert(target.MaxHealth - target.Health == BattleConstants.KillDamage * 3,
            "南蛮入侵×3面对费时应逐段造成3次伤害，而不是只结算第一段");
    }

    private void TestStackedArrowBarrageDealsEachHit()
    {
        var (attacker, target, context) = CreateUnits();
        BattlePhaseResolutionEffect.ResolveArrowBarrage(
            context,
            attacker,
            BattleAction.FromCard(Card.ArrowBarrage(), 3),
            target,
            BattleAction.FromCard(Card.Fee()));

        Assert(target.MaxHealth - target.Health == BattleConstants.KillDamage * 3,
            "万箭齐发×3面对费时应逐段造成3次伤害");
    }

    private static (EnemyInstance Attacker, Player Target, BattleContext Context) CreateUnits()
    {
        var attacker = new EnemyInstance(new EnemyDefinition
        {
            Id = "moon_boss_shared_rule_test",
            Name = "月亮",
            MaxHP = 120,
            Type = EnemyType.Boss,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });
        var target = new Player("玩家", "player", BattleTeam.Player);
        target.ResetForNewBattle(40, 40, 1);
        var triggerManager = new TriggerManager();
        // 必须包含正式的通用防御管线，才能覆盖“敌方出闪后仍应被天体撞击命中”的真实路径。
        triggerManager.Register(new DefenseBeforeDamageEffect());
        triggerManager.Register(new ApplyDamageEffect());
        var context = new BattleContext(target, triggerManager);
        context.BeginRoundResult();
        return (attacker, target, context);
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
