//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/JiGuLethalReactionRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证致死伤害仍会生成【击鼓】反应。
// 2. 验证反应完成前不会提前进入死亡状态。
// 3. 验证发动后继续复用统一OnDying死亡流程。
//
// 不负责：
// × 模拟反应条倒计时和鼠标点击。
// × 修改击鼓本身的恢复规则。
//
// 主要依赖：
// JiGuTriggerEffect
// DamageTakenEffect
// DyingEffect
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 祢衡【击鼓】致死伤害反应的 Headless 回归入口。
/// </summary>
public partial class JiGuLethalReactionRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行回归并通过进程退出码返回测试状态。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestLethalDamageCanActivateJiGuBeforeDying();
            GD.Print($"JIGU_LETHAL_REACTION_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"JIGU_LETHAL_REACTION_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestLethalDamageCanActivateJiGuBeforeDying()
    {
        GameManager.BeginNewRun();
        var player = new Player("祢衡", CharacterIds.MiHeng, BattleTeam.Player);
        player.ResetForNewBattle(30, 5, 0);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.JiGu)!);

        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "jigu_lethal_test_enemy",
            Name = "测试敌人",
            MaxHP = 40,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });
        var triggerManager = BattleTriggerEffects.CreateDefaultManager();
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        var context = new BattleContext(player, triggerManager) { Encounter = encounter };
        context.BeginRoundResult();

        var damage = new DamageEvent(enemy, player, CardType.Kill, 10);
        context.DamageEvent = damage;
        player.TakeDamage(10);
        triggerManager.RaiseTrigger(TriggerTiming.OnDamageTaken, context);

        Assert(player.Health <= 0, "测试前提失败：伤害没有把祢衡生命降至0以下");
        Assert(!player.IsDead && !context.GameOver, "击鼓反应出现前已经完成死亡结算");
        Assert(player.JiGuReactionPending, "致死伤害没有触发击鼓反应");
        Assert(context.Reactions.HasDeferredDyingReaction(player), "击鼓没有注册延迟濒死反应");

        var reaction = context.Reactions.Dequeue();
        Assert(reaction is IDeferredDyingReaction, "击鼓反应没有携带原濒死上下文");
        var activate = reaction!.Options.Find(option => option.Id == "activate");
        Assert(activate != null, "击鼓反应缺少发动选项");
        activate!.Resolve(context);
        Assert(player.JiGuActive, "生命值降至0以下时无法发动击鼓");
        Assert(player.JiGuTriggered, "发动击鼓后没有记录本场战斗已使用");

        context.CurrentDyingEventId = Guid.NewGuid().ToString("N");
        triggerManager.RaiseTrigger(TriggerTiming.OnDying, context);

        Assert(player.IsDead && context.GameOver, "发动击鼓后没有继续执行统一濒死与死亡流程");
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
