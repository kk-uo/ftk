//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/ParalysisDeviceRegression.cs
//
// 职责：验证瘫痪装置的品质、获取入口和分段真实伤害结算。
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 瘫痪装置强化后的 Headless 回归入口。
/// </summary>
public partial class ParalysisDeviceRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestDefinitionAndAcquisition();
            TestThreePulsesConsumeOneUseBlockPerHit();
            GD.Print($"PARALYSIS_DEVICE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"PARALYSIS_DEVICE_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestDefinitionAndAcquisition()
    {
        var definition = EquipmentDatabase.GetEquipment(EquipmentIds.ParalysisDevice);
        Assert(definition != null, "装备数据库中缺少瘫痪装置");
        Assert(definition!.Rarity == EquipmentRarity.Epic, "瘫痪装置没有升级为史诗品质");
        Assert(definition.Types.Contains(EquipmentType.Buff)
            && definition.Types.Contains(EquipmentType.Accessory), "瘫痪装置缺少增益类或饰品类标签");
        Assert(definition.AcquisitionMethod == EquipmentAcquisitionMethod.Shop,
            "瘫痪装置没有保留商店获取入口");
        Assert(definition.UnlockConditions.Contains("命运开端-初始事件⑮"),
            "瘫痪装置没有保留初始事件获取入口");
        Assert(definition.Effects.Single().Description.Contains("3次5点", StringComparison.Ordinal),
            "瘫痪装置效果描述没有更新为三段5点真实伤害");
    }

    private void TestThreePulsesConsumeOneUseBlockPerHit()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var item = InventoryManager.AddToInventory(EquipmentIds.ParalysisDevice, EquipmentGainSource.SaveRestore);
        Assert(item != null, "无法创建瘫痪装置测试装备");
        Assert(InventoryManager.EquipToSlot(item!.InstanceId, EquipmentSlot.Accessory1), "无法装备瘫痪装置");

        var protectedEnemy = CreateEnemy("benevolent_king_target", "仁王盾目标", true);
        var unprotectedEnemy = CreateEnemy("plain_target", "普通目标", false);
        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(protectedEnemy);
        encounter.Enemies.Add(unprotectedEnemy);
        var context = new BattleContext(player, BattleTriggerEffects.CreateDefaultManager())
        {
            Encounter = encounter,
            TurnCounter = 1
        };

        new ParalysisDeviceEffect().Execute(context);

        Assert(protectedEnemy.Health == 30,
            "仁王盾应仅格挡瘫痪装置第一段，后两段应各造成5点伤害");
        Assert(unprotectedEnemy.Health == 25,
            "瘫痪装置没有对每名敌人完整结算三段5点伤害");
        Assert(protectedEnemy.RuntimeStates.ContainsKey("benevolent_king_used"),
            "第一段伤害没有消耗仁王盾");
        Assert(context.RoundResult.Text.Contains("第1段被格挡", StringComparison.Ordinal)
            && context.RoundResult.Text.Contains("第2段造成5点真实伤害", StringComparison.Ordinal)
            && context.RoundResult.Text.Contains("第3段造成5点真实伤害", StringComparison.Ordinal),
            "瘫痪装置战报没有逐段记录格挡与伤害结果");
    }

    private static EnemyInstance CreateEnemy(string id, string name, bool hasBenevolentKing)
    {
        return new EnemyInstance(new EnemyDefinition
        {
            Id = id,
            Name = name,
            MaxHP = 40,
            Type = EnemyType.Normal,
            StartingResource = 1,
            StartingDeck = new EnemyDeck(),
            EquipmentIds = hasBenevolentKing
                ? new List<string> { EquipmentIds.BenevolentKing }
                : new List<string>()
        });
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
