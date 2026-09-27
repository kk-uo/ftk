//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/PangTongNirvanaRegression.cs
//
// 职责：验证庞统重制后的角色数据，以及【涅槃】优先于桃/酒救援。
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 庞统【涅槃】Headless 回归入口。
/// </summary>
public partial class PangTongNirvanaRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestDefinition();
            TestDyingRevivesBeforePeachOrWine();
            GD.Print($"PANGTONG_NIRVANA_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"PANGTONG_NIRVANA_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestDefinition()
    {
        var character = CharacterDatabase.GetCharacter(CharacterIds.PangTong);
        var nirvana = SkillDatabase.GetSkill(SkillIds.PangTongNirvana);
        var ironChain = SkillDatabase.GetSkill(SkillIds.PangTongIronChain);

        Assert(character.MaxHp == 30 && character.Faction == Faction.Shu, "庞统不是蜀阵营30生命值角色");
        Assert(character.SkillIds.Count == 2
            && character.SkillIds.Contains(SkillIds.PangTongNirvana)
            && character.SkillIds.Contains(SkillIds.PangTongIronChain),
            "庞统默认技能没有同时包含【涅槃】与【铁索连环】");
        Assert(nirvana != null && nirvana.Rarity == SkillRarity.Epic && Localization.GetName(nirvana) == "涅槃",
            "【涅槃】展示或稀有度错误");
        Assert(ironChain != null && ironChain.CharacterId == CharacterIds.PangTong && Localization.GetName(ironChain) == "铁索连环",
            "【铁索连环】没有正确归属庞统");
    }

    private void TestDyingRevivesBeforePeachOrWine()
    {
        var player = new Player("庞统", CharacterIds.PangTong, BattleTeam.Player);
        player.ResetForNewBattle(30, 30, 1);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.PangTongNirvana)!);
        player.DebugSetMana(8);
        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "pangtong_nirvana_test_enemy",
            Name = "测试敌人",
            MaxHP = 40,
            Type = EnemyType.Normal,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });
        enemy.DebugSetMana(6);
        var secondEnemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "pangtong_nirvana_test_enemy_2",
            Name = "第二测试敌人",
            MaxHP = 40,
            Type = EnemyType.Normal,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });
        secondEnemy.DebugSetMana(3);
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        encounter.Enemies.Add(secondEnemy);
        var manager = new TriggerManager();
        manager.Register(new PangTongNirvanaTriggerEffect());
        manager.Register(new PangTongNirvanaInvincibilityEffect());
        manager.Register(new PangTongNirvanaInvincibilityTurnEndEffect());
        manager.Register(new ApplyDamageEffect());
        manager.Register(new PangTongIronChainShareDamageEffect());
        manager.Register(new DyingEffect());
        var context = new BattleContext(player, manager) { Encounter = encounter };
        context.BeginRoundResult();

        player.DebugSetHealth(0);
        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10);
        context.RaiseOnDying();
        Assert(player.Health == player.MaxHealth, "涅槃濒死后没有满血复活");
        Assert(Math.Abs(player.CurrentMana - 1) < 0.001, "涅槃没有将玩家费用重置为1");
        Assert(Math.Abs(enemy.CurrentMana - 1) < 0.001 && Math.Abs(secondEnemy.CurrentMana - 1) < 0.001,
            "涅槃没有将所有敌方费用重置为1");
        Assert(player.DyingPeachReviveUses == 0 && !player.HasUsedWineRevive,
            "涅槃已成功救援后仍然错误消耗了桃或酒");

        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10);
        manager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
        Assert(context.DamageEvent.Cancelled, "涅槃触发当回合没有免疫后续伤害");
        player.RuntimeStates[PangTongIronChainActivateEffect.ActiveStateKey] = true;
        manager.RaiseTrigger(TriggerTiming.OnDamage, context);
        manager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);
        Assert(player.Health == player.MaxHealth && enemy.Health == 30 && secondEnemy.Health == 30,
            "涅槃无敌期间，铁索连环没有将本应命中的伤害同步给敌人，或庞统仍受到伤害");

        manager.RaiseTrigger(TriggerTiming.OnTurnEnd, context);
        Assert(player.RuntimeStates.TryGetValue(PangTongNirvanaTriggerEffect.InvincibleTurnsRemainingKey, out var remaining)
            && remaining is int turns && turns == 1, "触发回合结束后涅槃没有保留下一回合无敌");

        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10);
        manager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
        Assert(context.DamageEvent.Cancelled, "涅槃下一回合没有免疫伤害");

        manager.RaiseTrigger(TriggerTiming.OnTurnEnd, context);
        Assert(!player.RuntimeStates.ContainsKey(PangTongNirvanaTriggerEffect.InvincibleTurnsRemainingKey),
            "涅槃无敌在下一回合结束后没有失效");

        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10);
        manager.RaiseTrigger(TriggerTiming.OnBeforeDamage, context);
        Assert(!context.DamageEvent.Cancelled, "涅槃无敌结束后仍然错误免疫伤害");
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
