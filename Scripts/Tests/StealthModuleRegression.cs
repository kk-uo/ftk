//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/StealthModuleRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证隐身模块战斗开始时授予无敌，且无敌能让本应造成的伤害归零。
// 2. 验证打出费/杀类型牌/锦囊牌会立即失去无敌，而其它牌（如闪）不会。
// 3. 验证第10回合开始时无敌强制失效，第9回合及之前不受影响。
// 4. 验证未装备隐身模块时战斗开始不会获得无敌。
//
// 不负责：
// × 验证商店随机权重/图鉴UI展示。
//
// 主要依赖：
// StealthModuleActivateEffect / StealthModuleTurnLimitEffect /
// StealthModuleCardBreakEffect / StealthModuleInvincibilityEffect
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 隐身模块装备效果的 Headless 回归入口。
/// </summary>
public partial class StealthModuleRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestDefinition();
            TestBattleStartGrantsInvincibilityOnlyWhenEquipped();
            TestInvincibilityBlocksDamage();
            TestFeeCardBreaksStealth();
            TestShaCardBreaksStealth();
            TestTrickCardBreaksStealth();
            TestNonBreakingCardKeepsStealth();
            TestTurnTenBreaksStealth();
            TestTurnBeforeTenDoesNotBreakStealth();

            GD.Print($"STEALTH_MODULE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"STEALTH_MODULE_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestDefinition()
    {
        var def = EquipmentDatabase.GetEquipment(EquipmentIds.StealthModule)
            ?? throw new InvalidOperationException("找不到隐身模块装备定义");
        Assert(def.Rarity == EquipmentRarity.Epic, "隐身模块应为史诗稀有度");
        Assert(def.AcquisitionMethod == EquipmentAcquisitionMethod.Shop, "隐身模块应只能通过商店获得");
        Assert(def.Effects.Count == 2, "隐身模块应该有两条效果说明");
    }

    private void TestBattleStartGrantsInvincibilityOnlyWhenEquipped()
    {
        ResetRun();
        var player = CreatePlayer();
        var context = CreateContext(player);

        new StealthModuleActivateEffect().Execute(context);
        Assert(!player.StealthModuleActive, "未装备隐身模块时不应该获得无敌");

        EquipArmor(EquipmentIds.StealthModule);
        player = CreatePlayer();
        context = CreateContext(player);
        new StealthModuleActivateEffect().Execute(context);
        Assert(player.StealthModuleActive, "装备隐身模块后战斗开始应该获得无敌");
    }

    private void TestInvincibilityBlocksDamage()
    {
        ResetRun();
        EquipArmor(EquipmentIds.StealthModule);
        var player = CreatePlayer();
        var context = CreateContext(player);
        new StealthModuleActivateEffect().Execute(context);

        var enemy = CreateEnemy();
        var damage = new DamageEvent(enemy, player, CardType.Kill, 10, true);
        context.DamageEvent = damage;
        new StealthModuleInvincibilityEffect().Execute(context);
        damage.ResolveModifiers();

        Assert(damage.Cancelled, "隐身模块无敌应该取消这次伤害");
        Assert(damage.WasFullyBlocked, "隐身模块无敌应该标记为完全格挡");
    }

    private void TestFeeCardBreaksStealth()
    {
        AssertCardBreaksStealth(CardType.Fee, true, "打出费应该打破隐身");
    }

    private void TestShaCardBreaksStealth()
    {
        AssertCardBreaksStealth(CardType.Kill, true, "打出杀应该打破隐身");
        AssertCardBreaksStealth(CardType.FireKill, true, "打出火杀应该打破隐身");
    }

    private void TestTrickCardBreaksStealth()
    {
        AssertCardBreaksStealth(CardType.NanmanInvasion, true, "打出南蛮入侵（锦囊）应该打破隐身");
        AssertCardBreaksStealth(CardType.Steal, true, "打出顺手牵羊（锦囊）应该打破隐身");
    }

    private void TestNonBreakingCardKeepsStealth()
    {
        AssertCardBreaksStealth(CardType.Dodge, false, "打出闪不应该打破隐身");
        AssertCardBreaksStealth(CardType.Peach, false, "打出桃不应该打破隐身");
    }

    private void AssertCardBreaksStealth(CardType type, bool shouldBreak, string message)
    {
        ResetRun();
        EquipArmor(EquipmentIds.StealthModule);
        var player = CreatePlayer();
        var context = CreateContext(player);
        new StealthModuleActivateEffect().Execute(context);
        Assert(player.StealthModuleActive, "测试前提失败：战斗开始未获得无敌");

        context.PlayerAction = BattleAction.FromCard(new Card(type), 1, null);
        new StealthModuleCardBreakEffect().Execute(context);

        Assert(player.StealthModuleActive != shouldBreak, message);
    }

    private void TestTurnTenBreaksStealth()
    {
        ResetRun();
        EquipArmor(EquipmentIds.StealthModule);
        var player = CreatePlayer();
        var context = CreateContext(player);
        new StealthModuleActivateEffect().Execute(context);

        context.TurnCounter = 10;
        new StealthModuleTurnLimitEffect().Execute(context);
        Assert(!player.StealthModuleActive, "第10回合开始时无敌应该失效");
    }

    private void TestTurnBeforeTenDoesNotBreakStealth()
    {
        ResetRun();
        EquipArmor(EquipmentIds.StealthModule);
        var player = CreatePlayer();
        var context = CreateContext(player);
        new StealthModuleActivateEffect().Execute(context);

        context.TurnCounter = 9;
        new StealthModuleTurnLimitEffect().Execute(context);
        Assert(player.StealthModuleActive, "第9回合开始时无敌不应该失效");
    }

    private static void EquipArmor(string equipmentId)
    {
        var item = InventoryManager.AddToInventory(equipmentId, EquipmentGainSource.SaveRestore)
            ?? throw new InvalidOperationException($"无法加入测试装备：{equipmentId}");
        if (!InventoryManager.EquipToSlot(item.InstanceId, EquipmentSlot.Armor))
        {
            throw new InvalidOperationException($"无法装备测试护甲：{equipmentId}");
        }
    }

    private static void ResetRun()
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.ZhaoYun);
        InventoryManager.Reset();
    }

    private static Player CreatePlayer()
    {
        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(100, 100, 10);
        return player;
    }

    private static EnemyInstance CreateEnemy()
    {
        return new EnemyInstance(new EnemyDefinition
        {
            Id = "stealth_module_regression_enemy",
            Name = "隐身模块测试敌人",
            MaxHP = 50,
            Type = EnemyType.Normal,
            StartingResource = 0,
            StartingDeck = new EnemyDeck()
        });
    }

    private static BattleContext CreateContext(Player player)
    {
        var encounter = new BattleEncounter();
        var context = new BattleContext(player, new TriggerManager())
        {
            Encounter = encounter,
            TurnCounter = 1
        };
        context.BeginRoundResult();
        return context;
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
