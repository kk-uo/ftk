//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/TreasureDonkeyRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证载宝驴的费用取整、单场上限与电量容量规则。
// 2. 验证正式胜利条件、装备状态和同场幂等保护。
// 3. 验证日志、图鉴统计及商店基础资格。
//
// 不负责：
// × 验证商店随机权重的统计分布。
// × 验证装备触发提示的最终视觉样式。
//
// 主要依赖：
// TreasureDonkeyBattleEndEffect
// BattleLogService
// CodexService
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 载宝驴装备的 Headless 回归入口。
/// </summary>
public partial class TreasureDonkeyRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行回归并通过进程退出码返回结果。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestDefinitionAndShopEligibility();
            TestFeeFloorAndPerBattleCap();
            TestEnergyCapacityAndNegativePower();
            TestVictoryAndEquipmentGuards();
            TestSameBattleIdempotenceAndNextBattleReset();
            TestLogsAndCodexStatistics();
            GD.Print($"TREASURE_DONKEY_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"TREASURE_DONKEY_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestDefinitionAndShopEligibility()
    {
        var definition = EquipmentDatabase.GetEquipment(EquipmentIds.TreasureDonkey);
        Assert(definition != null, "装备数据库中缺少载宝驴");
        Assert(definition!.Id == "treasure_donkey", "载宝驴稳定ID错误");
        Assert(definition.Rarity == EquipmentRarity.Epic, "载宝驴不是史诗品质");
        Assert(definition.Types.Contains(EquipmentType.Vehicle), "载宝驴不是载具类");
        Assert(definition.Types.Contains(EquipmentType.Buff), "载宝驴缺少增益类标签");
        Assert(definition.AcquisitionMethod == EquipmentAcquisitionMethod.Shop, "载宝驴没有使用商店来源");
        Assert(definition.CanAppearInRandomPool, "载宝驴未进入随机池");
        Assert(ShopManager.CanAppearInShop(definition), "载宝驴被史诗商店品质规则排除");
    }

    private void TestFeeFloorAndPerBattleCap()
    {
        var cases = new (double Fee, int Expected)[]
        {
            (0, 0), (0.5, 0), (1, 1), (1.5, 1), (2.5, 2), (7.5, 7),
            (19.5, 19), (20, 20), (20.5, 20), (30, 20)
        };

        foreach (var testCase in cases)
        {
            var context = CreateContext(testCase.Fee, startingPower: 0, equipped: true, victory: true);
            var feeBefore = context.Player.CurrentMana;
            new TreasureDonkeyBattleEndEffect().Execute(context);
            Assert(GameManager.Power == testCase.Expected,
                $"费用{testCase.Fee:0.0}应获得{testCase.Expected}电量，实际{GameManager.Power}");
            Assert(Math.Abs(context.Player.CurrentMana - feeBefore) < 0.001, "载宝驴错误消耗了剩余费用");
        }
    }

    private void TestEnergyCapacityAndNegativePower()
    {
        var capacityContext = CreateContext(10, startingPower: GameManager.InitialMaxPower - 4, equipped: true, victory: true);
        new TreasureDonkeyBattleEndEffect().Execute(capacityContext);
        Assert(GameManager.Power == GameManager.MaxPower, "载宝驴没有遵守现有最大电量容量");
        var capacityEntry = capacityContext.LogService!.CurrentBattle!.Entries
            .Last(entry => entry.Kind == BattleLogEventKind.Equipment && !entry.DebugOnly);
        Assert(capacityEntry.EnergyBefore == GameManager.InitialMaxPower - 4 && capacityEntry.EnergyAfter == GameManager.MaxPower,
            "容量截断日志没有记录实际电量前后值");

        var negativeContext = CreateContext(8, startingPower: -10, equipped: true, victory: true);
        new TreasureDonkeyBattleEndEffect().Execute(negativeContext);
        Assert(GameManager.Power == -2, "载宝驴没有通过正式电量接口恢复负电量");
    }

    private void TestVictoryAndEquipmentGuards()
    {
        var defeatContext = CreateContext(10, startingPower: 0, equipped: true, victory: false);
        new TreasureDonkeyBattleEndEffect().Execute(defeatContext);
        Assert(GameManager.Power == 0, "战败时载宝驴错误发放电量");

        var unequippedContext = CreateContext(10, startingPower: 0, equipped: false, victory: true);
        new TreasureDonkeyBattleEndEffect().Execute(unequippedContext);
        Assert(GameManager.Power == 0, "未装备载宝驴仍然发放电量");

        var debugContext = CreateContext(10, startingPower: 0, equipped: true, victory: true);
        CodexService.DebugBattleActive = true;
        new TreasureDonkeyBattleEndEffect().Execute(debugContext);
        CodexService.DebugBattleActive = false;
        Assert(GameManager.Power == 0, "调试战斗中载宝驴错误污染正式电量");
    }

    private void TestSameBattleIdempotenceAndNextBattleReset()
    {
        var context = CreateContext(20, startingPower: 0, equipped: true, victory: true);
        var triggerManager = BattleTriggerEffects.CreateDefaultManager();
        triggerManager.RaiseTrigger(TriggerTiming.OnBattleEnd, context);
        triggerManager.RaiseTrigger(TriggerTiming.OnBattleEnd, context);
        Assert(GameManager.Power == 20, "同一战斗重复执行OnBattleEnd导致载宝驴重复发奖");

        context.LogService!.BeginBattle("treasure_donkey_second_battle");
        triggerManager.RaiseTrigger(TriggerTiming.OnBattleEnd, context);
        Assert(GameManager.Power == 40, "载宝驴的20点上限错误变成了整个Run累计上限");
    }

    private void TestLogsAndCodexStatistics()
    {
        CodexService.ResetForTest();
        var context = CreateContext(24.5, startingPower: 0, equipped: true, victory: true, resetCodex: false);
        new TreasureDonkeyBattleEndEffect().Execute(context);

        var entries = context.LogService!.CurrentBattle!.Entries;
        var playerEntry = entries.Single(entry => entry.Kind == BattleLogEventKind.Equipment && !entry.DebugOnly);
        var developerEntry = entries.Single(entry => entry.Kind == BattleLogEventKind.Equipment && entry.DebugOnly);
        Assert(playerEntry.GetText().Contains("获得20点电量（已达上限）", StringComparison.Ordinal),
            "载宝驴单场封顶的玩家日志错误");
        Assert(developerEntry.DebugMessage.Contains("FloorFee=24", StringComparison.Ordinal)
            && developerEntry.DebugMessage.Contains("ActualEnergyGain=20", StringComparison.Ordinal),
            "载宝驴Developer Report缺少结构化结算字段");

        var codex = CodexService.GetEquipment(EquipmentIds.TreasureDonkey);
        Assert(codex != null && codex.TriggeredCount == 1, "载宝驴图鉴有效触发次数错误");
        Assert(codex!.TotalEnergyProvided == 20, "载宝驴图鉴累计电量错误");
        Assert(codex.MaxEnergyProvidedInBattle == 20, "载宝驴图鉴单场最高电量错误");

        var noGainContext = CreateContext(0.5, startingPower: 0, equipped: true, victory: true, resetCodex: false);
        new TreasureDonkeyBattleEndEffect().Execute(noGainContext);
        Assert(noGainContext.LogService!.CurrentBattle!.Entries.All(entry => entry.DebugOnly),
            "0收益时载宝驴错误生成了玩家可见日志");
        Assert(CodexService.GetEquipment(EquipmentIds.TreasureDonkey)!.TriggeredCount == 1,
            "0收益被错误计入图鉴有效触发次数");
    }

    private static BattleContext CreateContext(
        double fee,
        int startingPower,
        bool equipped,
        bool victory,
        bool resetCodex = true)
    {
        GameManager.BeginNewRun();
        if (resetCodex)
        {
            CodexService.ResetForTest();
        }
        CodexService.DebugBattleActive = false;
        GameManager.AddPower(startingPower - GameManager.Power);

        var owned = InventoryManager.AddToInventory(EquipmentIds.TreasureDonkey, EquipmentGainSource.SaveRestore);
        if (equipped && owned != null)
        {
            InventoryManager.EquipToSlot(owned.InstanceId, EquipmentSlot.Vehicle);
        }

        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, fee);
        var logService = new BattleLogService();
        logService.BeginBattle("treasure_donkey_regression");
        return new BattleContext(player, new TriggerManager())
        {
            TurnCounter = 8,
            GameOver = true,
            Outcome = victory ? BattleOutcome.Victory : BattleOutcome.Defeat,
            GameOverText = victory
                ? Localization.Get("battle.gameover.victory")
                : Localization.Get("battle.gameover.defeat"),
            LogService = logService
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
