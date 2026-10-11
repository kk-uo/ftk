using Godot;
using System;
using System.Linq;

/// <summary>
/// Covers the Chapter 2-8 Zuo Ci/Gang Boss rework and its shared equipment rules.
/// </summary>
public partial class ChapterTwoBossReworkRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestDefinitions();
            TestSpearCosts();
            TestClampExoskeletonCycle();
            TestHuaXingUsesBaseHealthInsteadOfCurrentHealth();
            GD.Print($"CHAPTER_TWO_BOSS_REWORK_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"CHAPTER_TWO_BOSS_REWORK_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestDefinitions()
    {
        var zuoci = EnemyDatabase.GetEnemy("zuoci") ?? throw new InvalidOperationException("Missing Zuo Ci.");
        Assert(zuoci.MaxHP == 1 && zuoci.StartingResource == 2 && !zuoci.UseSharedHealthPool, "Zuo Ci base battle definition is incorrect.");
        Assert(zuoci.SkillIds.SequenceEqual(new[] { SkillIds.HuaXing }), "Zuo Ci must only start with Hua Xing.");
        Assert(zuoci.EquipmentIds.SequenceEqual(new[] { EquipmentIds.XianHao, EquipmentIds.ClampExoskeleton }), "Zuo Ci equipment does not match the rework.");
        Assert(zuoci.Reward.GoldOverride == 100 && zuoci.Reward.AttackChipDropCount == 1 && zuoci.Reward.KnowledgeChipDropProbability >= 1f, "Zuo Ci reward does not match the rework.");

        var gangBoss = EnemyDatabase.GetEnemy("gang_boss") ?? throw new InvalidOperationException("Missing Gang Boss.");
        Assert(gangBoss.MaxHP == 180 && gangBoss.StartingResource == 1 && !gangBoss.UseSharedHealthPool, "Gang Boss base battle definition is incorrect.");
        Assert(gangBoss.SkillIds.SequenceEqual(new[] { SkillIds.PoJun, SkillIds.ChangZui, SkillIds.YiCheng }), "Gang Boss skills do not match the rework.");
        Assert(gangBoss.EquipmentIds.Count(id => id == EquipmentIds.AttackChip) == 2 && gangBoss.EquipmentIds.Contains(EquipmentIds.GuDingDao) && gangBoss.EquipmentIds.Contains(EquipmentIds.WinePouch), "Gang Boss equipment does not match the rework.");
        Assert(gangBoss.StartingDeck.Cards.Contains(CardType.Steal) && gangBoss.StartingDeck.Cards.Contains(CardType.FireThunderKill), "Gang Boss deck is missing Steal or Fire-Thunder Kill.");
        Assert(gangBoss.Reward.GoldOverride == 100 && gangBoss.Reward.AttackChipDropCount == 1 && gangBoss.Reward.KnowledgeChipDropProbability >= 1f, "Gang Boss reward does not match the rework.");
    }

    private void TestSpearCosts()
    {
        var fireSpear = CreateEnemy("fire_spear_test", EquipmentIds.FirePatternSilverSpear);
        var thunderSpear = CreateEnemy("thunder_spear_test", EquipmentIds.ThunderSpear);
        var bothSpears = CreateEnemy("both_spears_test", EquipmentIds.FirePatternSilverSpear, EquipmentIds.ThunderSpear);

        Assert(BattleRules.GetCardCost(fireSpear, CardType.FireKill) == 1, "Fire-Pattern Silver Spear must set Fire Kill to 1 mana.");
        Assert(BattleRules.GetCardCost(fireSpear, CardType.FireThunderKill) == 2, "Fire-Pattern Silver Spear must reduce Fire-Thunder Kill by 1 mana.");
        Assert(BattleRules.GetCardCost(thunderSpear, CardType.ThunderKill) == 1, "Thunder Spear must set Thunder Kill to 1 mana.");
        Assert(BattleRules.GetCardCost(thunderSpear, CardType.FireThunderKill) == 2, "Thunder Spear must reduce Fire-Thunder Kill by 1 mana.");
        Assert(BattleRules.GetCardCost(bothSpears, CardType.FireThunderKill) == 1, "Independent Fire-Thunder Kill reductions must stack when both weapons coexist.");
    }

    private void TestClampExoskeletonCycle()
    {
        var target = CreateEnemy("clamp_test", EquipmentIds.ClampExoskeleton, maxHp: 20);
        var player = CreatePlayer(5);
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(target);
        var context = new BattleContext(player, BattleTriggerEffects.CreateDefaultManager()) { Encounter = encounter };
        context.BeginRoundResult();

        context.DamageEvent = new DamageEvent(player, target, CardType.Kill, 160, true);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
        Assert(target.RuntimeStates.TryGetValue("clamp_exo_invincible_this_round", out var prepared) && prepared is true,
            $"Clamp Exoskeleton pre-damage guard did not arm (HP={target.Health}/{target.MaxHealth}, damage={context.DamageEvent.Amount}).");
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
        Assert(target.Health == 1, $"Clamp Exoskeleton must intercept lethal damage at 1 HP (actual {target.Health}/{target.MaxHealth}).");
        Assert(target.RuntimeStates.TryGetValue("clamp_exo_repair_pending", out var pending) && pending is true, "Clamp Exoskeleton must schedule next-round repair.");
        Assert(target.RuntimeStates.TryGetValue("clamp_exo_invincible_this_round", out var invincible) && invincible is true, "Clamp Exoskeleton must grant current-round invincibility.");

        context.DamageEvent = new DamageEvent(player, target, CardType.Kill, 10, true);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
        Assert(context.DamageEvent.Cancelled, "Clamp Exoskeleton invincibility must cancel later damage in the same round.");

        context.DamageEvent = null;
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnTurnStart, context);
        Assert(target.Health == target.MaxHealth, "Clamp Exoskeleton must repair its wearer to full HP next round.");
        Assert(target.RuntimeStates.TryGetValue("clamp_exo_invincible_this_round", out invincible) && invincible is false, "Clamp Exoskeleton invincibility must expire next round.");
    }

    private void TestHuaXingUsesBaseHealthInsteadOfCurrentHealth()
    {
        GameManager.BeginNewRun();
        var player = CreatePlayer(2, maxHp: 100);
        player.DebugSetHealth(10);
        var zuoci = new EnemyInstance(EnemyDatabase.GetEnemy("zuoci") ?? throw new InvalidOperationException("Missing Zuo Ci."));
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(zuoci);
        var context = new BattleContext(player, new TriggerManager()) { Encounter = encounter };

        new HuaXingTransformEffect().Execute(context);
        Assert(zuoci.MaxHealth == 180 && zuoci.Health == 180, "Hua Xing must use 100 base HP plus its retained +80 Exoskeleton, not the player's current 10 HP.");
        Assert(zuoci.HasEquipment(EquipmentIds.XianHao) && zuoci.HasEquipment(EquipmentIds.ClampExoskeleton), "Hua Xing must retain Zuo Ci's own equipment.");
    }

    private static EnemyInstance CreateEnemy(string id, params string[] equipmentIds)
    {
        return CreateEnemy(id, equipmentIds, 40);
    }

    private static EnemyInstance CreateEnemy(string id, string equipmentId, int maxHp)
    {
        return CreateEnemy(id, new[] { equipmentId }, maxHp);
    }

    private static EnemyInstance CreateEnemy(string id, string[] equipmentIds, int maxHp)
    {
        return new EnemyInstance(new EnemyDefinition
        {
            Id = id,
            Name = id,
            MaxHP = maxHp,
            EquipmentIds = equipmentIds.ToList()
        });
    }

    private static Player CreatePlayer(double mana, int maxHp = 40)
    {
        var player = new Player("Test Player", "chapter_two_boss_test_player", BattleTeam.Player);
        player.ResetForNewBattle(maxHp, maxHp, mana);
        return player;
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
