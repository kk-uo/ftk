//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/TimeHourglassRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证时间沙漏属于统一重铸/随机奖励池的传奇增益饰品。
// 2. 验证装备后在首回合战斗回合前对双方造成第55回合对应的伤害。
// 3. 验证战场崩坏从第55回合开始，致命掉血最低为0并进入濒死流程。
// 4. 验证未装备时不会触发，且不会修改当前回合数。
//
// 不负责：
// × 验证选择面板的视觉布局。
// × 模拟第53回合后的递增伤害。
//
// 主要依赖：
// TimeHourglassEffect
// BattlefieldCollapseEffect
// EquipmentChoiceProvider
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 时间沙漏数据与战斗触发的 Headless 回归入口。
/// </summary>
public partial class TimeHourglassRegression : Node
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
            TestDefinitionAndLegendaryChoicePool();
            TestEquippedBattleStartTrigger();
            TestUnequippedDoesNotTrigger();
            TestCollapseStartsAtTurn55();
            TestLethalCollapseClampsAtZeroAndResolvesDeath();
            TestCollapseUsesDyingRescuePipeline();
            TestSimultaneousLethalCollapseSettlesEveryUnit();
            GD.Print($"TIME_HOURGLASS_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"TIME_HOURGLASS_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestDefinitionAndLegendaryChoicePool()
    {
        GameManager.BeginNewRun();
        var definition = EquipmentDatabase.GetEquipment(EquipmentIds.TimeHourglass);
        Assert(definition != null, "装备数据库中缺少时间沙漏");
        Assert(definition!.Rarity == EquipmentRarity.Legendary, "时间沙漏不是传奇品质");
        Assert(definition.Types.Contains(EquipmentType.Buff), "时间沙漏缺少增益类标签");
        Assert(definition.Types.Contains(EquipmentType.Accessory), "时间沙漏不是饰品");
        Assert(definition.CanAppearInRandomPool, "时间沙漏未进入随机装备池");

        var choices = new EquipmentChoiceProvider
        {
            Count = 1,
            Randomize = false,
            Rarities = new[] { EquipmentRarity.Legendary },
            EquipmentPool = new[] { EquipmentIds.TimeHourglass }
        }.CreateChoices();
        Assert(choices.Count == 1 && choices[0].Id == EquipmentIds.TimeHourglass, "传奇装备选择池无法生成时间沙漏");

        var rewardPoolIds = EquipmentDatabase.GetAllEquipments()
            .Where(equipment => equipment.Rarity == EquipmentRarity.Legendary)
            .Where(equipment => RewardManager.CanAppearInRandomEquipmentReward(equipment, excludeOwned: false))
            .Select(equipment => equipment.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var reforgePoolIds = EquipmentDatabase.GetAllEquipments()
            .Where(equipment => equipment.Rarity == EquipmentRarity.Legendary)
            .Where(InventoryManager.IsEligibleForReforge)
            .Select(equipment => equipment.Id)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        Assert(rewardPoolIds.SequenceEqual(reforgePoolIds), "传奇随机奖励池没有与统一重铸池同步");
        Assert(rewardPoolIds.Length >= 2, "传奇随机奖励池候选不足两件");

        // 初始事件②【涅槃重生】正是通过该 Provider 生成传奇二选一；
        // 直接验证界面入口，避免底层候选存在但被额外 UI 过滤后只剩一项。
        var initialEventChoices = new EquipmentChoiceProvider
        {
            // 古蜀铸炉与其它传奇奖励界面都请求三选一；初始事件②只显示前两项。
            Count = 3,
            Randomize = false,
            Rarities = new[] { EquipmentRarity.Legendary }
        }.CreateChoices();
        Assert(initialEventChoices.Count == 3, "传奇装备选择界面候选不足三件");
        Assert(initialEventChoices.All(choice => rewardPoolIds.Contains(choice.Id)),
            "涅槃重生界面出现了统一重铸池之外的候选");
    }

    private void TestEquippedBattleStartTrigger()
    {
        GameManager.BeginNewRun();
        var equipment = InventoryManager.AddToInventory(
            EquipmentIds.TimeHourglass,
            EquipmentGainSource.SaveRestore);
        Assert(equipment != null, "无法创建时间沙漏测试装备");
        Assert(InventoryManager.EquipToSlot(equipment!.InstanceId, EquipmentSlot.Accessory1), "时间沙漏未能装备到饰品槽");

        var triggerManager = BattleTriggerEffects.CreateDefaultManager();
        var context = CreateContext(triggerManager);
        triggerManager.RaiseTrigger(TriggerTiming.OnBattlePrePhase, context);

        Assert(context.Player.Health == 39, "时间沙漏没有对玩家造成第55回合的1点伤害");
        Assert(context.Encounter!.Enemies[0].Health == 39, "时间沙漏没有对敌人造成第55回合的1点伤害");
        Assert(context.TurnCounter == 1, "时间沙漏错误修改了当前回合数");
        Assert(context.RoundResult.Text.Contains("第55回合", StringComparison.Ordinal), "时间沙漏触发后缺少战报");
    }

    private void TestUnequippedDoesNotTrigger()
    {
        GameManager.BeginNewRun();
        InventoryManager.AddToInventory(EquipmentIds.TimeHourglass, EquipmentGainSource.SaveRestore);
        var context = CreateContext(new TriggerManager());

        new TimeHourglassEffect().Execute(context);

        Assert(context.Player.Health == 40, "未装备时间沙漏仍然对玩家造成了伤害");
        Assert(context.Encounter!.Enemies[0].Health == 40, "未装备时间沙漏仍然对敌人造成了伤害");
    }

    private void TestCollapseStartsAtTurn55()
    {
        GameManager.BeginNewRun();
        var context = CreateContext(BattleTriggerEffects.CreateDefaultManager());
        context.TurnCounter = 54;
        new BattlefieldCollapseEffect().Execute(context);
        Assert(context.Player.Health == 40, "第54回合错误触发了战场崩坏");
        Assert(context.Encounter!.Enemies[0].Health == 40, "第54回合错误扣除了敌人生命");

        context.TurnCounter = 55;
        new BattlefieldCollapseEffect().Execute(context);
        Assert(context.Player.Health == 39, "第55回合没有开始战场崩坏");
        Assert(context.Encounter!.Enemies[0].Health == 39, "第55回合没有扣除敌人生命");
        Assert(BattlefieldCollapseEffect.CalculateDamage(56) == 2, "第56回合战场崩坏伤害没有递增为2");
    }

    private void TestLethalCollapseClampsAtZeroAndResolvesDeath()
    {
        GameManager.BeginNewRun();
        var context = CreateContext(
            BattleTriggerEffects.CreateDefaultManager(),
            playerHp: 1,
            playerMana: 0,
            enemyHp: 40);
        context.TurnCounter = 55;

        new BattlefieldCollapseEffect().Execute(context);

        Assert(context.Player.Health == 0, "致命战场崩坏把玩家生命扣成了负数");
        Assert(context.Player.IsDead, "玩家生命归零后没有完成OnDying/OnDeath结算");
        Assert(context.GameOver && context.Outcome == BattleOutcome.Defeat, "玩家死亡后没有进入正常战败流程");
        Assert(context.Encounter!.Enemies[0].Health == 39, "玩家先死亡导致敌人漏掉同批次全体掉血");
    }

    private void TestCollapseUsesDyingRescuePipeline()
    {
        GameManager.BeginNewRun();
        var context = CreateContext(
            BattleTriggerEffects.CreateDefaultManager(),
            playerHp: 1,
            playerMana: 2,
            enemyHp: 40);
        context.TurnCounter = 55;

        new BattlefieldCollapseEffect().Execute(context);

        Assert(context.Player.Health > 0, "战场崩坏致命时没有允许桃通过OnDying进行救援");
        Assert(!context.Player.IsDead, "桃救援成功后玩家仍被错误结算为死亡");
        Assert(Math.Abs(context.Player.CurrentMana) <= 0.001, "桃救援没有按统一濒死流程扣除2费");
    }

    private void TestSimultaneousLethalCollapseSettlesEveryUnit()
    {
        GameManager.BeginNewRun();
        var context = CreateContext(
            BattleTriggerEffects.CreateDefaultManager(),
            playerHp: 3,
            playerMana: 0,
            enemyHp: 3);
        context.TurnCounter = 59;

        new BattlefieldCollapseEffect().Execute(context);

        var enemy = context.Encounter!.Enemies[0];
        Assert(context.Player.Health == 0 && enemy.Health == 0, "同批次致命崩坏没有把双方生命限制在0");
        Assert(context.Player.IsDead && enemy.IsDead, "同批次归零后有单位遗漏濒死或死亡结算");
        Assert(context.MutualCollapseVictory, "双方同时被战场崩坏击倒时没有进入既有互伤结算");
    }

    private static BattleContext CreateContext(
        TriggerManager triggerManager,
        int playerHp = 40,
        double playerMana = 1,
        int enemyHp = 40)
    {
        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(40, playerHp, playerMana);
        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "time_hourglass_test_enemy",
            Name = "测试敌人",
            MaxHP = 40,
            Type = EnemyType.Normal,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });
        enemy.DebugSetHealth(enemyHp);
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        return new BattleContext(player, triggerManager)
        {
            Encounter = encounter,
            TurnCounter = 1
        };
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
