//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/StinkyYellowManaRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证恶臭蘑菇·黄将敌我初始费用设为-1。
// 2. 验证负费用时仍可使用0费牌填平亏空。
// 3. 验证负费用时不能使用需要支付费用的牌。
//
// 不负责：
// × 模拟完整战斗界面点击。
// × 验证恶臭蘑菇事件选项。
//
// 主要依赖：
// StinkyYellowInitialManaOverrideEffect
// BattleRules
// EnemyAI
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 恶臭蘑菇·黄费用亏空规则的 Headless 回归入口。
/// </summary>
public partial class StinkyYellowManaRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行费用可用性回归，并通过进程退出码返回结果。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestInitialManaOverrideAndZeroCostRecovery();
            GD.Print($"STINKY_YELLOW_MANA_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"STINKY_YELLOW_MANA_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestInitialManaOverrideAndZeroCostRecovery()
    {
        GameManager.BeginNewRun();
        var equipment = InventoryManager.AddToInventory(
            EquipmentIds.StinkyMushroomYellow,
            EquipmentGainSource.SaveRestore);
        Assert(equipment != null, "无法创建恶臭蘑菇·黄测试装备");
        Assert(InventoryManager.EquipToSlot(equipment!.InstanceId, EquipmentSlot.Accessory1),
            "恶臭蘑菇·黄未能装备到饰品槽");

        var player = new Player("玩家", "stinky_yellow_player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        var enemy = CreateEnemy();
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        var context = new BattleContext(player, new TriggerManager())
        {
            Encounter = encounter,
            TurnCounter = 1
        };

        new StinkyYellowInitialManaOverrideEffect().Execute(context);

        Assert(Math.Abs(player.CurrentMana + 1) < 0.001, "玩家初始费用没有被设为-1");
        Assert(Math.Abs(enemy.CurrentMana + 1) < 0.001, "敌人初始费用没有被设为-1");
        Assert(BattleRules.CanAffordActionCost(player, BattleRules.GetActionCost(player, BattleAction.FromCard(Card.Fee()))),
            "玩家负费用时无法使用0费的费牌");
        Assert(BattleRules.CanAffordActionCost(player, BattleRules.GetActionCost(player, BattleAction.FromCard(Card.Dodge()))),
            "玩家负费用时无法使用0费的闪");
        Assert(!BattleRules.CanAffordActionCost(player, BattleRules.GetActionCost(player, BattleAction.FromCard(Card.Kill()))),
            "玩家负费用时错误允许使用1费牌");

        var selected = new EnemyAI().SelectAction(enemy, player);
        Assert(selected.Type == CardType.Fee, "敌人负费用时没有选择唯一可恢复亏空的费牌");

        player.GainMana(1);
        Assert(Math.Abs(player.CurrentMana) < 0.001, "负1费使用费后没有回到0费");
        Assert(BattleRules.CanAffordActionCost(player, 0), "回到0费后0费牌仍不可用");
    }

    private static EnemyInstance CreateEnemy()
    {
        return new EnemyInstance(new EnemyDefinition
        {
            Id = "stinky_yellow_enemy",
            Name = "敌人",
            MaxHP = 40,
            StartingResource = 1,
            StartingDeck = new EnemyDeck
            {
                Cards = { CardType.Fee, CardType.Kill }
            }
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
