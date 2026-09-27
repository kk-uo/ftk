//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/DongZhuoBengHuaiRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证董卓【崩坏】从第2回合开始、每回合开始时都重新判定（不是只触发一次）。
// 2. 验证生命损失不被第一回合免伤规则拦截。
// 3. 验证低血量反转与高血量扣血之间可以随生命值变化逐回合切换。
//
// 不负责：
// × 模拟完整战斗界面。
// × 验证技能大字的视觉效果。
//
// 主要依赖：
// DongZhuoBengHuaiEffect
// TriggerManager
// Player
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 董卓【崩坏】每回合触发规则的 Headless 回归入口。
/// </summary>
public partial class DongZhuoBengHuaiRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行【崩坏】回归并通过进程退出码返回测试状态。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestNoTriggerBeforeTurnTwo();
            TestRecurringHealthLossEachTurn();
            TestLowHealthReversalRecursEachTurn();
            TestOscillatesBetweenLossAndHeal();
            TestMissingSkillDoesNotTrigger();
            TestBengHuaiNpcDefinitions();
            GD.Print($"DONG_ZHUO_BENG_HUAI_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"DONG_ZHUO_BENG_HUAI_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestNoTriggerBeforeTurnTwo()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var forgottenStone = InventoryManager.AddToInventory(
            EquipmentIds.ForgottenStone,
            EquipmentGainSource.SaveRestore);
        Assert(forgottenStone != null, "无法创建遗忘之石测试装备");
        Assert(
            InventoryManager.EquipToSlot(forgottenStone!.InstanceId, EquipmentSlot.Accessory1),
            "遗忘之石未能装备到饰品槽");

        var player = CreateDongZhuo(80, 80);
        var triggerManager = CreateTriggerManager();
        var context = new BattleContext(player, triggerManager)
        {
            TurnCounter = 1
        };

        triggerManager.RaiseTrigger(TriggerTiming.OnGameStart, context);
        Assert(player.Health == 80, "【崩坏】不应在 OnGameStart 提前触发");

        triggerManager.RaiseTrigger(TriggerTiming.OnTurnStart, context);
        Assert(player.Health == 80, "【崩坏】第1回合不应该生效（从第2回合才开始）");
    }

    private void TestRecurringHealthLossEachTurn()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var player = CreateDongZhuo(80, 80);
        var triggerManager = CreateTriggerManager();
        var context = new BattleContext(player, triggerManager)
        {
            TurnCounter = 2
        };

        triggerManager.RaiseTrigger(TriggerTiming.OnTurnStart, context);
        Assert(player.Health == 60, "【崩坏】第2回合未失去20点生命");
        Assert(
            context.RoundResult.Text.Contains("失去20点生命", StringComparison.Ordinal),
            "【崩坏】缺少战报");

        context.TurnCounter = 3;
        triggerManager.RaiseTrigger(TriggerTiming.OnTurnStart, context);
        Assert(player.Health == 40, "【崩坏】第3回合应该再次失去20点生命（不是只触发一次）");

        context.TurnCounter = 4;
        triggerManager.RaiseTrigger(TriggerTiming.OnTurnStart, context);
        Assert(player.Health == 20, "【崩坏】第4回合应该继续每回合触发");
    }

    private void TestLowHealthReversalRecursEachTurn()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        // MaxHealth=80 → 阈值20，回复量固定为 80/16=5；起始血量1，回复两次后（1→6→11）
        // 仍然远低于阈值，用来验证恢复分支本身也是每回合重新判定，而不是像旧版一样只触发一次。
        var player = CreateDongZhuo(80, 1);
        var triggerManager = CreateTriggerManager();
        var context = new BattleContext(player, triggerManager)
        {
            TurnCounter = 2
        };

        triggerManager.RaiseTrigger(TriggerTiming.OnTurnStart, context);
        Assert(player.Health == 6, "【崩坏】低血量反转未恢复最大生命值1/16");

        context.TurnCounter = 3;
        triggerManager.RaiseTrigger(TriggerTiming.OnTurnStart, context);
        Assert(player.Health == 11, "【崩坏】低血量反转应该每回合都重新判定并继续恢复");
    }

    private void TestOscillatesBetweenLossAndHeal()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        // MaxHealth=80 → 阈值20；从21（高于阈值）开始，验证扣血把血量打到阈值以下后，
        // 下一回合能立刻切换成恢复分支，而不是停留在某个一次性状态。
        var player = CreateDongZhuo(80, 21);
        var triggerManager = CreateTriggerManager();
        var context = new BattleContext(player, triggerManager)
        {
            TurnCounter = 2
        };

        triggerManager.RaiseTrigger(TriggerTiming.OnTurnStart, context);
        Assert(player.Health == 1, "【崩坏】高于阈值时应该扣20点生命");

        context.TurnCounter = 3;
        triggerManager.RaiseTrigger(TriggerTiming.OnTurnStart, context);
        Assert(player.Health == 6, "【崩坏】跌破阈值后下一回合应该立刻切换为恢复分支");
    }

    private void TestMissingSkillDoesNotTrigger()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(80, 80, 1);
        var triggerManager = CreateTriggerManager();
        var context = new BattleContext(player, triggerManager)
        {
            TurnCounter = 2
        };

        triggerManager.RaiseTrigger(TriggerTiming.OnTurnStart, context);
        Assert(player.Health == 80, "未持有【崩坏】的单位错误失去生命");
    }

    private void TestBengHuaiNpcDefinitions()
    {
        var dongZhuoBoss = EnemyDatabase.GetEnemy("boss_tyrant");
        var abyssSymbiote = EnemyDatabase.GetEnemy("abyss_symbiote");
        Assert(dongZhuoBoss != null, "未找到董卓 Boss 定义");
        Assert(abyssSymbiote != null, "未找到深渊共生体定义");
        Assert(dongZhuoBoss!.SkillIds.Contains(SkillIds.DongZhuoBengHuai), "董卓 Boss 未使用正确的【崩坏】定义");
        Assert(abyssSymbiote!.SkillIds.Contains(SkillIds.DongZhuoBengHuai), "深渊共生体未沿用正确的【崩坏】定义");
        Assert(
            EnemyDatabase.GetAllEnemies().Count(enemy => enemy.SkillIds.Contains(SkillIds.DongZhuoBengHuai)) == 2,
            "当前敌人定义中【崩坏】携带者数量异常");
        Assert(
            EnemyDatabase.GetAllEnemies().All(enemy => !enemy.SkillIds.Contains("benghuai")),
            "旧版泛用【崩坏】仍被 NPC 引用");

        var tyrant = new EnemyInstance(dongZhuoBoss);
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(tyrant);
        var triggerManager = CreateTriggerManager();
        var player = new Player("测试玩家", "test_player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        var context = new BattleContext(player, triggerManager)
        {
            Encounter = encounter,
            TurnCounter = 2
        };
        triggerManager.RaiseTrigger(TriggerTiming.OnTurnStart, context);
        Assert(tyrant.Health == 980, "董卓 Boss 第2回合没有按【崩坏】失去20点生命");
    }

    private static Player CreateDongZhuo(int maxHealth, int health)
    {
        var player = new Player("董卓", CharacterIds.DongZhuo, BattleTeam.Player);
        player.ResetForNewBattle(maxHealth, health, 1);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.DongZhuoBengHuai)!);
        return player;
    }

    private static TriggerManager CreateTriggerManager()
    {
        var triggerManager = new TriggerManager();
        triggerManager.Register(new DongZhuoBengHuaiEffect());
        return triggerManager;
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
