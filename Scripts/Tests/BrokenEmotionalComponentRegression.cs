using Godot;
using System;

/// <summary>
/// 回归：敌方持有【破碎情感组件】时，回合结束必须让玩家与所有存活敌人各恢复10点生命。
/// </summary>
public partial class BrokenEmotionalComponentRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            VerifyEnemyOwnedComponentHealsAllLivingUnits();
            VerifyWanshaDoesNotCancelOwnersOwnHealing();
            GD.Print($"BROKEN_EMOTIONAL_COMPONENT_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"BROKEN_EMOTIONAL_COMPONENT_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void VerifyEnemyOwnedComponentHealsAllLivingUnits()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();

        var player = CreatePlayer("玩家", 40, 20);
        var componentOwner = CreateEnemy("component_owner", 130, 82, hasComponent: true);
        var ally = CreateEnemy("component_ally", 60, 35, hasComponent: false);
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(componentOwner);
        encounter.Enemies.Add(ally);

        var context = new BattleContext(player, BattleTriggerEffects.CreateDefaultManager())
        {
            Encounter = encounter,
            TurnCounter = 13
        };
        context.BeginRoundResult();
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnTurnEnd, context);

        Assert(player.Health == 30, "敌方持有组件时，玩家没有恢复10点生命");
        Assert(componentOwner.Health == 92, "敌方组件持有者没有恢复10点生命");
        Assert(ally.Health == 45, "敌方同伴没有恢复10点生命");
        Assert(context.RoundResult.Text.Contains("破碎情感组件启动", StringComparison.Ordinal), "战报没有记录组件已启动");
    }

    private void VerifyWanshaDoesNotCancelOwnersOwnHealing()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();

        var player = CreatePlayer("玩家", 40, 20);
        var owner = CreateEnemy("wansha_component_owner", 130, 82, hasComponent: true, hasWansha: true);
        Assert(owner.HasSkill(SkillIds.Wansha), "测试敌人没有正确获得【完杀】");
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(owner);
        var context = new BattleContext(player, BattleTriggerEffects.CreateDefaultManager())
        {
            Encounter = encounter,
            TurnCounter = 13
        };
        context.BeginRoundResult();
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnTurnEnd, context);

        Assert(owner.Health == 92, "拥有【完杀】的敌人自身治疗被错误反噬");
        Assert(player.Health == 20, "敌方【完杀】没有对玩家的治疗造成等量伤害");
    }

    private static Player CreatePlayer(string name, int maxHealth, int health)
    {
        var player = new Player(name, "broken_component_player", BattleTeam.Player);
        player.ResetForNewBattle(maxHealth, health, 0);
        return player;
    }

    private static EnemyInstance CreateEnemy(string id, int maxHealth, int health, bool hasComponent, bool hasWansha = false)
    {
        var definition = new EnemyDefinition
        {
            Id = id,
            Name = id,
            MaxHP = maxHealth,
            Type = EnemyType.Elite,
            StartingDeck = new EnemyDeck(),
            EquipmentIds = hasComponent ? new() { EquipmentIds.BrokenEmotionalComponent } : new(),
            SkillIds = hasWansha ? new() { SkillIds.Wansha } : new()
        };
        var enemy = new EnemyInstance(definition);
        // EnemyInstance 构造时已完成战斗初始化并装载定义中的技能；不要再次 Reset，
        // 否则会清空【完杀】等运行时技能，导致测试与真实战斗不一致。
        enemy.CurrentHpValue = health;
        enemy.CurrentResource = 0;
        return enemy;
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
