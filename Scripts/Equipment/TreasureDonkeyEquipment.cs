//////////////////////////////////////////////////////////
// 文件：Scripts/Equipment/TreasureDonkeyEquipment.cs
//
// 模块：Equipment System
//
// 职责：
// 1. 在正式战斗胜利的 OnBattleEnd 阶段读取玩家剩余费用。
// 2. 通过正式电量接口发放载宝驴的单场奖励。
// 3. 记录玩家日志、开发日志与永久图鉴统计。
//
// 不负责：
// × 改变战斗胜负判定。
// × 消耗或重置玩家费用。
// × 修改电量容量规则。
//
// 主要依赖：
// InventoryManager
// GameManager
// BattleLogService
// CodexService
//////////////////////////////////////////////////////////

using System;
using System.Globalization;
using System.Linq;

/// <summary>
/// 载宝驴的战斗胜利电量奖励效果。
///
/// OnBattleEnd 在当前项目中每回合都会触发，因此这里同时校验正式胜利结果，
/// 并以装备实例和战斗编号做幂等保护。
/// </summary>
public sealed class TreasureDonkeyBattleEndEffect : IBattleEffect
{
    public const int PerBattleEnergyCap = 20;

    public TriggerTiming Timing => TriggerTiming.OnBattleEnd;
    public EffectPriority Priority => EffectPriority.Lowest;

    /// <summary>
    /// 在胜利已经确定后，按费用整数部分发放电量。
    /// </summary>
    public void Execute(BattleContext context)
    {
        var equipment = InventoryManager.GetAllOwned().FirstOrDefault(item =>
            item.Definition.Id == EquipmentIds.TreasureDonkey && InventoryManager.IsActive(item));
        if (equipment == null || !IsFormalVictory(context))
        {
            return;
        }

        var battleId = context.LogService?.CurrentBattle?.BattleId ?? 0;
        var executionKey = $"treasure_donkey:{equipment.InstanceId:N}:{battleId}";
        if (context.Player.RuntimeStates.ContainsKey(executionKey))
        {
            return;
        }

        // 先锁定本场实例，避免同一个 OnBattleEnd 被界面或后续流程重复抬起时重复发奖。
        context.Player.RuntimeStates[executionKey] = true;

        var currentFee = context.Player.CurrentMana;
        var integerFee = Math.Max(0, (int)Math.Floor(currentFee));
        var calculatedGain = Math.Min(integerFee, PerBattleEnergyCap);
        var energyBefore = GameManager.Power;
        if (calculatedGain > 0)
        {
            GameManager.AddPower(calculatedGain);
        }

        var energyAfter = GameManager.Power;
        var actualGain = Math.Max(0, energyAfter - energyBefore);
        var cappedByEquipment = integerFee > PerBattleEnergyCap;
        var cappedByCapacity = actualGain < calculatedGain;

        RecordDeveloperLog(
            context,
            battleId,
            currentFee,
            integerFee,
            calculatedGain,
            energyBefore,
            energyAfter,
            actualGain,
            cappedByEquipment,
            cappedByCapacity);

        if (actualGain <= 0)
        {
            return;
        }

        var feeText = currentFee.ToString("0.0", CultureInfo.InvariantCulture);
        var resultKey = cappedByEquipment
            ? "battlelog.treasure_donkey.gain_capped"
            : cappedByCapacity
                ? "battlelog.treasure_donkey.gain_capacity_capped"
                : "battlelog.treasure_donkey.gain";
        var resultArgs = cappedByCapacity
            ? new[] { feeText, actualGain.ToString(), calculatedGain.ToString() }
            : new[] { feeText, actualGain.ToString() };

        context.RoundResult.AddLine(Localization.GetFmt(resultKey, resultArgs));
        context.LogService?.RecordStructured(
            BattleLogEventKind.Equipment,
            context.TurnCounter,
            "BattleEnd",
            resultKey,
            resultArgs,
            BattleLogCategory.Action,
            timing: Timing,
            priority: Priority,
            actor: context.Player.UnitId,
            source: EquipmentIds.TreasureDonkey,
            equipment: EquipmentIds.TreasureDonkey,
            energyBefore: energyBefore,
            energyAfter: energyAfter,
            result: $"ActualEnergyGain={actualGain}");
        CodexService.RecordEquipmentEnergyProvided(EquipmentIds.TreasureDonkey, actualGain);
    }

    private static bool IsFormalVictory(BattleContext context)
    {
        return context.GameOver
            && context.IsPlayerVictory
            && !TutorialManager.IsActive
            && !CodexService.DebugBattleActive;
    }

    private static void RecordDeveloperLog(
        BattleContext context,
        int battleId,
        double currentFee,
        int integerFee,
        int calculatedGain,
        int energyBefore,
        int energyAfter,
        int actualGain,
        bool cappedByEquipment,
        bool cappedByCapacity)
    {
        var executionResult = calculatedGain == 0
            ? "NoGain"
            : actualGain == 0
                ? "NoCapacity"
                : "Granted";
        var report = $"BattleId={battleId}; RoundFinal={context.TurnCounter}; TriggerTiming={TriggerTiming.OnBattleEnd}; "
            + $"EquipmentId={EquipmentIds.TreasureDonkey}; OwnerActorId={context.Player.UnitId}; "
            + $"FeeBeforeBattleEnd={currentFee:0.###}; FloorFee={integerFee}; PerBattleCap={PerBattleEnergyCap}; "
            + $"CalculatedEnergyGain={calculatedGain}; EnergyBefore={energyBefore}; EnergyMax={GameManager.MaxPower}; "
            + $"ActualEnergyGain={actualGain}; EnergyAfter={energyAfter}; WasCappedByEquipment={cappedByEquipment}; "
            + $"WasCappedByEnergyCapacity={cappedByCapacity}; ExecutionResult={executionResult}";

        context.TriggerLogs.Add($"[Equipment/载宝驴] {report}");
        context.LogService?.RecordStructured(
            BattleLogEventKind.Equipment,
            context.TurnCounter,
            "BattleEnd",
            "battlelog.treasure_donkey.debug",
            new[] { report },
            BattleLogCategory.Debug,
            debugOnly: true,
            timing: TriggerTiming.OnBattleEnd,
            priority: EffectPriority.Lowest,
            actor: context.Player.UnitId,
            source: EquipmentIds.TreasureDonkey,
            equipment: EquipmentIds.TreasureDonkey,
            energyBefore: energyBefore,
            energyAfter: energyAfter,
            result: executionResult,
            debugMessage: report);
    }
}
