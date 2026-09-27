//////////////////////////////////////////////////////////
// 【铁索连环】回归：护盾、延迟生效、每回合仅首段伤害共享。
//////////////////////////////////////////////////////////

using Godot;
using System;

public partial class PangTongIronChainRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestIronChainLifecycle();
            TestUnassailableCancelsOnlyPendingChainActivation();
            GD.Print($"PANGTONG_IRON_CHAIN_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"PANGTONG_IRON_CHAIN_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestIronChainLifecycle()
    {
        var player = new Player("庞统", CharacterIds.PangTong, BattleTeam.Player);
        player.ResetForNewBattle(30, 30, 2);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.PangTongIronChain)!);
        var firstEnemy = CreateEnemy("iron_chain_first", "甲敌人");
        var secondEnemy = CreateEnemy("iron_chain_second", "乙敌人");
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(firstEnemy);
        encounter.Enemies.Add(secondEnemy);

        var manager = new TriggerManager();
        manager.Register(new PangTongIronChainActivateEffect());
        manager.Register(new PangTongIronChainActivateNextTurnEffect());
        manager.Register(new PangTongIronChainShieldEffect());
        manager.Register(new ApplyDamageEffect());
        manager.Register(new PangTongIronChainShareDamageEffect());
        var context = new BattleContext(player, manager) { Encounter = encounter, TurnNumber = 1 };
        context.BeginRoundResult();

        Assert(Card.IronChain().Cost == 1 && Card.IronChain().IsTrickCard,
            "铁索连环不是1费锦囊牌");
        Assert(BattleRules.CanUnassailableCounter(CardType.IronChain), "铁索连环不能被无懈可击反制");

        context.PlayerAction = BattleAction.FromCard(Card.IronChain());
        manager.RaiseTrigger(TriggerTiming.OnBattlePhase, context);
        Assert(player.RuntimeStates.ContainsKey(PangTongIronChainActivateEffect.ShieldStateKey),
            "使用铁索连环后没有获得护盾");
        Assert(!player.RuntimeStates.ContainsKey(PangTongIronChainActivateEffect.ActiveStateKey),
            "铁索连环不应在使用当回合生效");

        context.DamageEvent = new DamageEvent(firstEnemy, player, CardType.Kill, 10);
        manager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
        Assert(context.DamageEvent.Cancelled && player.Health == 30,
            "铁索连环护盾没有抵挡第一次伤害");
        Assert(!player.RuntimeStates.ContainsKey(PangTongIronChainActivateEffect.ShieldStateKey),
            "铁索连环护盾抵挡后没有消耗");

        context.TurnNumber = 2;
        manager.RaiseTrigger(TriggerTiming.OnTurnStart, context);
        Assert(player.RuntimeStates.ContainsKey(PangTongIronChainActivateEffect.ActiveStateKey),
            "铁索连环没有在下一回合开始时生效");

        context.BeginRoundResult();
        context.DamageEvent = new DamageEvent(player, firstEnemy, CardType.Kill, 10, isDirectAttackDamage: true);
        manager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
        Assert(firstEnemy.Health == 30 && player.Health == 20 && secondEnemy.Health == 30,
            "铁索连环没有将首段实际伤害同步给其余所有存活单位");

        context.DamageEvent = new DamageEvent(player, secondEnemy, CardType.Kill, 10, isDirectAttackDamage: true);
        manager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
        Assert(secondEnemy.Health == 20 && player.Health == 20 && firstEnemy.Health == 30,
            "铁索连环在同一回合错误重复结算后续伤害");
    }

    private void TestUnassailableCancelsOnlyPendingChainActivation()
    {
        var player = new Player("庞统", CharacterIds.PangTong, BattleTeam.Player);
        player.ResetForNewBattle(30, 30, 2);
        var enemy = CreateEnemy("iron_chain_counter", "反制敌人");
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        var manager = new TriggerManager();
        manager.Register(new PangTongIronChainActivateEffect());
        var context = new BattleContext(player, manager) { Encounter = encounter, TurnNumber = 3 };
        context.PlayerAction = BattleAction.FromCard(Card.IronChain());
        context.SetActionForEnemy(enemy, BattleAction.FromCard(Card.Unassailable()));
        player.RuntimeStates[PangTongIronChainActivateEffect.ActiveStateKey] = true;
        player.RuntimeStates[PangTongIronChainActivateEffect.ShieldStateKey] = true;
        player.RuntimeStates[PangTongIronChainActivateEffect.PendingActivationTurnKey] = 4;

        manager.RaiseTrigger(TriggerTiming.OnBattlePhase, context);
        Assert(context.PlayerActionCancelled
            && player.RuntimeStates.ContainsKey(PangTongIronChainActivateEffect.ActiveStateKey)
            && player.RuntimeStates.ContainsKey(PangTongIronChainActivateEffect.ShieldStateKey)
            && !player.RuntimeStates.ContainsKey(PangTongIronChainActivateEffect.PendingActivationTurnKey),
            "无懈可击没有只取消本次待激活连锁，或错误清除了已有连锁/护盾");
    }

    private static EnemyInstance CreateEnemy(string id, string name)
    {
        return new EnemyInstance(new EnemyDefinition
        {
            Id = id,
            Name = name,
            MaxHP = 40,
            Type = EnemyType.Normal,
            StartingResource = 1,
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
