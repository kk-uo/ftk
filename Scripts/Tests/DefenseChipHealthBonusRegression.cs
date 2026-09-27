//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/DefenseChipHealthBonusRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证 GameManager.IncrementDefenseChipCount() 本身就会永久增加
//    10×阵营命运芯片倍率 的最大/当前生命值，不需要调用方额外补一次
//    AddMaxHp/AddCurrentHp。
// 2. 验证灵魂石出售随机到防御芯片时，生命值加成确实生效
//    （原bug：InventoryManager.ApplySoulStoneChipBonus 只增加了计数，
//    没有同步给生命值，导致玩家卖灵魂石拿到"防御芯片"却没有实际变化）。
// 3. 验证女巫头皮出售（同时给三种芯片各一个）时防御芯片那一份的生命值
//    加成也生效，覆盖同一类遗漏。
//
// 不负责：
// × 验证攻击/知识芯片对应的伤害加成计算。
// × 模拟完整背包UI点击流程。
//
// 主要依赖：
// GameManager
// InventoryManager
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 防御芯片生命值加成的 Headless 回归入口。
/// </summary>
public partial class DefenseChipHealthBonusRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Run();
            GD.Print($"DEFENSE_CHIP_HEALTH_BONUS_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"DEFENSE_CHIP_HEALTH_BONUS_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void Run()
    {
        Localization.Initialize();
        TestIncrementDefenseChipCountGrantsHealthBonus();
        TestSellingSoulStoneCanGrantHealthBonus();
        TestSellingWitchScalpGrantsHealthBonus();
    }

    private void TestIncrementDefenseChipCountGrantsHealthBonus()
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.ZhaoYun);
        InventoryManager.Reset();

        var maxBefore = GameManager.MaxHP;
        var currentBefore = GameManager.CurrentHP;

        GameManager.IncrementDefenseChipCount();

        Assert(GameManager.MaxHP == maxBefore + 10, "IncrementDefenseChipCount未正确增加最大生命值");
        Assert(GameManager.CurrentHP == currentBefore + 10, "IncrementDefenseChipCount未正确增加当前生命值");
        Assert(GameManager.DefenseChipCount == 1, "IncrementDefenseChipCount未正确增加计数");
    }

    // 灵魂石出售是随机三选一（攻击/防御/知识），headless下多次出售直到命中防御芯片，
    // 确认命中时确实产生了生命值加成——不依赖具体触发次数，只要求命中过一次即可。
    private void TestSellingSoulStoneCanGrantHealthBonus()
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.ZhaoYun);
        InventoryManager.Reset();

        var hitDefense = false;
        for (var i = 0; i < 200 && !hitDefense; i++)
        {
            var maxBefore = GameManager.MaxHP;
            var currentBefore = GameManager.CurrentHP;
            var defenseCountBefore = GameManager.DefenseChipCount;

            var owned = InventoryManager.AddToInventory(EquipmentIds.SoulStone, EquipmentGainSource.Developer);
            Assert(owned != null, "灵魂石测试装备未加入背包");
            InventoryManager.SellItem(owned!.InstanceId);

            if (GameManager.DefenseChipCount > defenseCountBefore)
            {
                hitDefense = true;
                Assert(GameManager.MaxHP == maxBefore + 10, "出售灵魂石命中防御芯片时未增加最大生命值（原bug）");
                Assert(GameManager.CurrentHP == currentBefore + 10, "出售灵魂石命中防御芯片时未增加当前生命值（原bug）");
            }
        }

        Assert(hitDefense, "200次出售灵魂石里一次防御芯片都没抽到，测试本身有问题");
    }

    private void TestSellingWitchScalpGrantsHealthBonus()
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.ZhaoYun);
        InventoryManager.Reset();

        var maxBefore = GameManager.MaxHP;
        var currentBefore = GameManager.CurrentHP;

        var owned = InventoryManager.AddToInventory(EquipmentIds.WitchScalp, EquipmentGainSource.Developer);
        Assert(owned != null, "女巫头皮测试装备未加入背包");
        InventoryManager.SellItem(owned!.InstanceId);

        Assert(GameManager.MaxHP == maxBefore + 10, "出售女巫头皮未增加最大生命值（原bug）");
        Assert(GameManager.CurrentHP == currentBefore + 10, "出售女巫头皮未增加当前生命值（原bug）");
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
