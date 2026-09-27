//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/KejiNoDamagePenaltyRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证吕蒙【克己】打出费用后不再让本回合受到的伤害翻倍（原有代价已移除）。
// 2. 验证【克己】费用携带上限已从10提升为15。
//
// 不负责：
// × 验证【克己·无限制协议】升级流程本身（禁书库事件已有覆盖）。
//
// 主要依赖：
// Player.GainMana / Scripts/Damage/DamageEffects.cs（伤害触发管线）
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 【克己】移除受伤翻倍代价的 Headless 回归入口。
/// </summary>
public partial class KejiNoDamagePenaltyRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestManaCapRaisedToFifteen();
            TestSpendingManaDoesNotDoubleIncomingDamage();

            GD.Print($"KEJI_NO_DAMAGE_PENALTY_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"KEJI_NO_DAMAGE_PENALTY_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestManaCapRaisedToFifteen()
    {
        var player = new Player("吕蒙", CharacterIds.LuMeng, BattleTeam.Player);
        player.ResetForNewBattle(100, 100, 10);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.Keji) ?? throw new InvalidOperationException("找不到克己技能定义"));

        player.GainMana(20);
        Assert(player.CurrentMana == 15, $"克己费用携带上限应为15，实际{player.CurrentMana}");
    }

    private void TestSpendingManaDoesNotDoubleIncomingDamage()
    {
        var manager = BattleTriggerEffects.CreateDefaultManager();
        var player = new Player("吕蒙", CharacterIds.LuMeng, BattleTeam.Player);
        player.ResetForNewBattle(100, 100, 10);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.Keji) ?? throw new InvalidOperationException("找不到克己技能定义"));

        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "keji_regression_enemy",
            Name = "克己测试敌人",
            MaxHP = 100,
            Type = EnemyType.Normal,
            StartingResource = 0,
            StartingDeck = new EnemyDeck()
        });

        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        var context = new BattleContext(player, manager) { Encounter = encounter };
        context.BeginRoundResult();

        // 模拟"本回合打出了费"：过去这里会调用 player.SetKejiPenaltyActive(true)（方法本身已删除）。
        // 现在没有任何入口能再让玩家进入"受到伤害翻倍"的状态，直接验证一次真实的敌方→玩家杀伤害
        // 结算不会被翻倍即可。
        var damage = new DamageEvent(enemy, player, CardType.Kill, 10, true);
        context.DamageEvent = damage;
        manager.RaiseTrigger(TriggerTiming.OnDamage, context);
        damage.ResolveModifiers();

        Assert(damage.Amount == 10, $"克己不应该再让打出费用后受到的伤害翻倍，实际{damage.Amount}");
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
