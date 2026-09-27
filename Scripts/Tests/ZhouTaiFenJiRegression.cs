//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/ZhouTaiFenJiRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证周泰【奋激】在每一次独立的濒死事件（OnDying）都能重新触发，
//    每次永久获得2点攻击性锦囊伤害，而不是只在本场战斗第一次生效。
// 2. 钉住 BattleContext.RaiseOnDying() 的去重契约：只有通过它触发 OnDying
//    （每次都刷新 CurrentDyingEventId）才能保证连续多次濒死各自生效；
//    直接调用 TriggerManager.RaiseTrigger(TriggerTiming.OnDying, ...) 而不刷新
//    这个Guid，会被去重机制误判成"上一次已经处理过的濒死"而跳过——这正是
//    原bug的根源（毒素/瘟疫/自伤等大量调用点都曾经这样直接调用）。
//
// 不负责：
// × 模拟完整战斗界面。
//
// 主要依赖：
// ZhouTaiFenJiTriggerEffect
// BattleContext.RaiseOnDying
// GameManager.ZhouTaiFenjiBonus
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 周泰【奋激】每次独立濒死都应重新触发的 Headless 回归入口。
/// </summary>
public partial class ZhouTaiFenJiRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Run();
            TestBuQuRollAndFirstFailureRescue();
            TestDyingPeachCostDoublesEveryUse();
            GD.Print($"ZHOUTAI_FENJI_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"ZHOUTAI_FENJI_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run()
    {
        Localization.Initialize();
        GameManager.BeginNewRun();

        var player = new Player("周泰", CharacterIds.ZhouTai, BattleTeam.Player);
        player.ResetForNewBattle(80, 80, 1);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.ZhouTaiFenJi)!);

        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "zhoutai_fenji_test_enemy",
            Name = "测试敌人",
            MaxHP = 50,
            Type = EnemyType.Normal,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });

        var triggerManager = new TriggerManager();
        triggerManager.Register(new ZhouTaiFenJiTriggerEffect());
        var context = new BattleContext(player, triggerManager);
        context.BeginRoundResult();

        Assert(GameManager.ZhouTaiFenjiBonus == 0, "测试开始前奋激加值应为0");

        // 第一次濒死：正常通过 RaiseOnDying() 触发，必须生效。
        player.DebugSetHealth(0);
        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10);
        context.RaiseOnDying();
        Assert(GameManager.ZhouTaiFenjiBonus == 2, "第一次濒死后奋激未能生效（应+2）");

        // 第二次独立濒死（模拟不同伤害来源，比如毒素/瘟疫/自伤）：必须再次生效，
        // 而不是被去重机制误判成"上一次已经处理过"。这正是本次要修复的原bug场景。
        player.DebugSetHealth(0);
        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10);
        context.RaiseOnDying();
        Assert(GameManager.ZhouTaiFenjiBonus == 4, "第二次独立濒死事件后奋激应该再次触发（+2，累计4）");

        // 第三次独立濒死：故意不通过 RaiseOnDying() 刷新 CurrentDyingEventId，直接调用
        // TriggerManager.RaiseTrigger——这是修复前大量调用点（毒素/瘟疫/自伤装备等）的
        // 错误用法，去重机制会把它误判成"和上一次是同一次濒死"从而跳过，奋激不应该生效。
        // 这条断言钉住"为什么所有正式调用点都必须改用 RaiseOnDying()"这个契约。
        player.DebugSetHealth(0);
        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10);
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDying, context);
        Assert(GameManager.ZhouTaiFenjiBonus == 4, "未刷新CurrentDyingEventId直接raise时，去重机制应挡住重复触发（钉住必须使用RaiseOnDying()的契约）");

        // 第四次：换回 RaiseOnDying()，确认去重键刷新后立刻恢复正常触发，不会被上一次的
        // "错误用法"污染成永久失效。
        player.DebugSetHealth(0);
        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10);
        context.RaiseOnDying();
        Assert(GameManager.ZhouTaiFenjiBonus == 6, "刷新CurrentDyingEventId后奋激应该恢复正常触发（+2，累计6）");
    }

    private void TestBuQuRollAndFirstFailureRescue()
    {
        GameManager.BeginNewRun();
        var player = new Player("周泰", CharacterIds.ZhouTai, BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.ZhouTaiBuQu)!);
        var enemy = new Player("测试敌人", "zhoutai_buqu_test_enemy", BattleTeam.Enemy);
        enemy.ResetForNewBattle(40, 40, 1);

        var triggerManager = new TriggerManager();
        triggerManager.Register(new ZhouTaiBuQuTriggerEffect());
        var context = new BattleContext(player, triggerManager);
        context.BeginRoundResult();

        // 首次判定失败：不屈的失败保底应救回玩家，而不是旧规则的“首次无条件成功”。
        DeveloperDebugPanel.ForcedZhouTaiDiceResult = 1;
        player.DebugSetHealth(0);
        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10);
        context.RaiseOnDying();
        Assert(player.Health == 1 && player.CurrentMana == 2, "不屈首次判定失败没有保底回复至1并获得1费");
        Assert(context.PendingDiceRollRequest is { Result: 1, Success: false }, "不屈首次失败的骰子结果没有保留为失败");

        // 后续失败不再保底，交还给常规濒死链。
        DeveloperDebugPanel.ForcedZhouTaiDiceResult = 1;
        player.DebugSetHealth(0);
        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10);
        context.RaiseOnDying();
        Assert(player.Health == 0 && player.CurrentMana == 2, "不屈第二次失败错误触发了重复保底救援");

        // 任意一次5/6都正常成功。
        DeveloperDebugPanel.ForcedZhouTaiDiceResult = 5;
        player.DebugSetHealth(0);
        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10);
        context.RaiseOnDying();
        Assert(player.Health == 1 && player.CurrentMana == 3, "不屈掷出5后没有获得1费并回复至1");
    }

    private void TestDyingPeachCostDoublesEveryUse()
    {
        GameManager.BeginNewRun();
        var player = new Player("桃救援测试", "dying_peach_test", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 14);
        var enemy = new Player("测试敌人", "dying_peach_enemy", BattleTeam.Enemy);
        enemy.ResetForNewBattle(40, 40, 1);

        var triggerManager = new TriggerManager();
        triggerManager.Register(new DyingEffect());
        var context = new BattleContext(player, triggerManager);
        context.BeginRoundResult();

        player.DebugSetHealth(0);
        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10);
        context.RaiseOnDying();
        Assert(player.CurrentMana == 12 && player.DyingPeachReviveUses == 1,
            "第一次濒死桃救援未按基础2费结算或未记录次数");

        player.DebugSetHealth(0);
        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10);
        context.RaiseOnDying();
        Assert(player.CurrentMana == 8 && player.DyingPeachReviveUses == 2,
            "第二次濒死桃救援没有按双倍4费结算");

        // 每次实际使用后都继续翻倍：第三次应为基础费用的四倍（8费）。
        player.DebugSetHealth(0);
        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10);
        context.RaiseOnDying();
        Assert(player.CurrentMana == 0 && player.DyingPeachReviveUses == 3,
            "第三次濒死桃救援没有按四倍8费结算");
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
