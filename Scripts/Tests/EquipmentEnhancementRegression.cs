//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/EquipmentEnhancementRegression.cs
//
// 覆盖合金盾/巨人盾生命与伤害、仙毫无槽位/免伤/成长上限与商店可售性。
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class EquipmentEnhancementRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestAlloyShieldGrantsThirtyMaxHp();
            TestGiantShieldGrantsHealthAndAttackDamage();
            TestFoldingKnifeResolvesThreeIndependentNormalKillHits();
            TestXianHaoIsSlotlessAndBuildsDodgeChance();
            TestHighTemperatureModuleAmplifiesOnlyFireAttackCards();
            TestZhuQueYuShanAmplifiesFireKillAndZhouYuFireAttack();
            TestReforgerConvertsNextSoldEquipmentAtSameRarity();
            GD.Print($"EQUIPMENT_ENHANCEMENT_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"EQUIPMENT_ENHANCEMENT_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            XianHaoDodgeEffect.RandomDoubleProviderForTests = null;
        }
    }

    private void TestAlloyShieldGrantsThirtyMaxHp()
    {
        var definition = EquipmentDatabase.GetEquipment(EquipmentIds.GangDun);
        Assert(definition != null && definition.Rarity == EquipmentRarity.Rare, "合金盾定义缺失或品质错误");
        Assert(definition!.Effects.Count == 1 && definition.Effects[0].Description.Contains("30", StringComparison.Ordinal),
            "合金盾装备效果没有更新为30生命");

        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "alloy_shield_hp_regression_enemy",
            Name = "合金盾测试敌人",
            MaxHP = 100,
            Type = EnemyType.Normal,
            StartingDeck = new EnemyDeck(),
            EquipmentIds = new List<string> { EquipmentIds.GangDun }
        });
        Assert(enemy.MaxHealth == 130 && enemy.Health == 130, "合金盾没有为装备者增加30最大生命和当前生命");
    }

    private void TestGiantShieldGrantsHealthAndAttackDamage()
    {
        var definition = EquipmentDatabase.GetEquipment(EquipmentIds.GiantShield);
        Assert(definition != null
            && definition.Rarity == EquipmentRarity.Epic
            && definition.AcquisitionMethod == EquipmentAcquisitionMethod.Shop,
            "巨人盾定义、品质或商店来源错误");
        Assert(definition!.Types.Contains(EquipmentType.Buff) && definition.Types.Contains(EquipmentType.Armor),
            "巨人盾装备类型应为增益类/护甲类");
        Assert(ShopManager.CanAppearInShop(definition), "巨人盾未进入商店池");

        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "giant_shield_hp_regression_enemy",
            Name = "巨人盾测试敌人",
            MaxHP = 100,
            Type = EnemyType.Normal,
            StartingDeck = new EnemyDeck(),
            EquipmentIds = new List<string> { EquipmentIds.GiantShield }
        });
        Assert(enemy.MaxHealth == 145 && enemy.Health == 145, "巨人盾没有为装备者增加45最大生命和当前生命");

        var target = new Player("测试目标", "giant_shield_target", BattleTeam.Player);
        target.ResetForNewBattle(300, 300, 0);
        var context = new BattleContext(target, BattleTriggerEffects.CreateDefaultManager());
        context.BeginRoundResult();
        var damage = new DamageEvent(enemy, target, CardType.Kill, 10, isDirectAttackDamage: true);
        context.DamageEvent = damage;
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
        damage.ResolveModifiers(enemy.WinePower);
        Assert(damage.Amount == 14, "巨人盾应按当前145生命的3%向下取整，为攻击增加4点伤害");

        enemy.TakeDamage(45);
        var reducedHealthDamage = new DamageEvent(enemy, target, CardType.Kill, 10, isDirectAttackDamage: true);
        context.DamageEvent = reducedHealthDamage;
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
        reducedHealthDamage.ResolveModifiers(enemy.WinePower);
        Assert(reducedHealthDamage.Amount == 13, "巨人盾加伤未随当前生命降低而更新");

        var skillDamage = new DamageEvent(enemy, target, CardType.LightningStrike, 20, isDirectAttackDamage: false);
        context.DamageEvent = skillDamage;
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
        skillDamage.ResolveModifiers(enemy.WinePower);
        Assert(skillDamage.Amount == 20, "巨人盾不应加成非主动攻击牌伤害");
    }

    private void TestFoldingKnifeResolvesThreeIndependentNormalKillHits()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var foldingKnife = InventoryManager.AddToInventory(EquipmentIds.FoldingKnife, EquipmentGainSource.SaveRestore);
        Assert(foldingKnife != null && InventoryManager.EquipToSlot(foldingKnife.InstanceId, EquipmentSlot.Weapon),
            "折叠刀未能装备到武器槽");
        InventoryManager.AddToInventory(EquipmentIds.AttackChip, EquipmentGainSource.SaveRestore);

        var player = new Player("折叠刀测试玩家", "folding_knife_player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 0);
        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "folding_knife_regression_enemy",
            Name = "折叠刀测试敌人",
            MaxHP = 100,
            Type = EnemyType.Normal,
            StartingDeck = new EnemyDeck()
        });
        var context = new BattleContext(player, BattleTriggerEffects.CreateDefaultManager());
        context.BeginRoundResult();

        BattlePhaseResolutionEffect.DealAttackDamage(context, player, enemy, CardType.Kill);
        // 每段：5基础伤害 + 攻击芯片的普通杀加伤3 = 8；共两段。
        Assert(enemy.Health == 84, $"折叠刀普通杀应结算2段各8伤害，敌人生命期望84，实际={enemy.Health}");
        Assert(context.RoundResult.Text.Contains("折叠刀第1段", StringComparison.Ordinal)
            && context.RoundResult.Text.Contains("折叠刀第2段", StringComparison.Ordinal)
            && !context.RoundResult.Text.Contains("折叠刀第3段", StringComparison.Ordinal),
            "折叠刀没有逐段写入战斗结算记录");

        var fireTarget = new EnemyInstance(new EnemyDefinition
        {
            Id = "folding_knife_fire_regression_enemy",
            Name = "折叠刀火杀测试敌人",
            MaxHP = 100,
            Type = EnemyType.Normal,
            StartingDeck = new EnemyDeck()
        });
        BattlePhaseResolutionEffect.DealAttackDamage(context, player, fireTarget, CardType.FireKill);
        Assert(fireTarget.Health == 82, "折叠刀不应改变火杀；火杀应只结算一次15基础+3攻击芯片伤害");

        InventoryManager.Reset();
        GameManager.BeginNewRun();
    }

    private void TestXianHaoIsSlotlessAndBuildsDodgeChance()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var item = InventoryManager.AddToInventory(EquipmentIds.XianHao, EquipmentGainSource.SaveRestore);
        Assert(item != null, "仙毫未进入背包");
        Assert(InventoryManager.GetSlotCategory(item!.Definition) == null, "仙毫错误占用装备槽分类");
        Assert(GameManager.HasActiveEquipment(EquipmentIds.XianHao), "仙毫作为无槽位背包装备没有生效");

        ShopManager.GenerateSlots(ShopType.BlackMarket);
        Assert(ShopManager.CanAppearInShop(EquipmentDatabase.GetEquipment(EquipmentIds.XianHao)!), "仙毫未被允许出现在可购买商店池");

        var player = new Player("玩家", "xian_hao_player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "xian_hao_regression_enemy",
            Name = "测试敌人",
            MaxHP = 100,
            Type = EnemyType.Normal,
            StartingDeck = new EnemyDeck()
        });
        var context = new BattleContext(player, new TriggerManager());
        context.BeginRoundResult();

        XianHaoDodgeEffect.RandomDoubleProviderForTests = () => 0;
        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10, isDirectAttackDamage: true);
        new XianHaoDodgeEffect().Execute(context);
        Assert(context.DamageEvent.Cancelled, "仙毫在25%判定成功时没有免疫攻击伤害");

        for (var hit = 0; hit < 7; hit++)
        {
            context.DamageEvent = new DamageEvent(player, enemy, CardType.Kill, 10, isDirectAttackDamage: true)
            {
                ActualDamageDealt = 10
            };
            new XianHaoHitChanceEffect().Execute(context);
        }
        Assert(XianHaoDodgeEffect.GetCurrentChancePercent(player) == 90, "仙毫命中成长未正确封顶在90%");

        XianHaoDodgeEffect.RandomDoubleProviderForTests = () => 0.899;
        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10, isDirectAttackDamage: true);
        new XianHaoDodgeEffect().Execute(context);
        Assert(context.DamageEvent.Cancelled, "仙毫90%概率下的成功判定没有免疫攻击伤害");

        XianHaoDodgeEffect.RandomDoubleProviderForTests = () => 0.90;
        context.DamageEvent = new DamageEvent(enemy, player, CardType.Kill, 10, isDirectAttackDamage: true);
        new XianHaoDodgeEffect().Execute(context);
        Assert(!context.DamageEvent.Cancelled, "仙毫概率判定边界错误：90%不应覆盖0.90");
    }

    private void TestHighTemperatureModuleAmplifiesOnlyFireAttackCards()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var module = InventoryManager.AddToInventory(EquipmentIds.HighTemperatureModule, EquipmentGainSource.SaveRestore);
        Assert(module != null && InventoryManager.EquipToSlot(module!.InstanceId, EquipmentSlot.Accessory1),
            "高温模块无法装备到饰品槽");

        var definition = EquipmentDatabase.GetEquipment(EquipmentIds.HighTemperatureModule);
        Assert(definition != null
            && definition.Rarity == EquipmentRarity.Epic
            && definition.AcquisitionMethod == EquipmentAcquisitionMethod.Shop
            && definition.Types.Contains(EquipmentType.Buff)
            && definition.Types.Contains(EquipmentType.Accessory),
            "高温模块定义、品质、来源或类型错误");
        Assert(ShopManager.CanAppearInShop(definition!), "高温模块没有进入商店池");

        var player = new Player("高温模块测试玩家", "high_temperature_module_player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 2);
        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "high_temperature_module_target",
            Name = "高温模块测试目标",
            MaxHP = 100,
            Type = EnemyType.Normal,
            StartingDeck = new EnemyDeck()
        });
        var context = new BattleContext(player, BattleTriggerEffects.CreateDefaultManager());
        context.BeginRoundResult();

        BattlePhaseResolutionEffect.DealAttackDamage(context, player, enemy, CardType.FireKill);
        Assert(enemy.Health == 78, "高温模块应将15点火杀伤害×1.5并向下取整为22点");

        var equipmentFireDamage = new DamageEvent(
            player,
            enemy,
            CardType.FireKill,
            15,
            origin: new HealthChangeSource(HealthChangeSourceKind.Equipment, "测试装备火伤", "test_equipment_fire", player));
        context.DamageEvent = equipmentFireDamage;
        context.TriggerManager.RaiseTrigger(TriggerTiming.OnDamage, context);
        equipmentFireDamage.ResolveModifiers(player.WinePower);
        Assert(equipmentFireDamage.Amount == 15, "高温模块不应放大装备造成的火属性伤害");
        context.DamageEvent = null;

        InventoryManager.Reset();
        GameManager.BeginNewRun();
    }

    private void TestReforgerConvertsNextSoldEquipmentAtSameRarity()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var reforger = InventoryManager.AddToInventory(EquipmentIds.Reforger, EquipmentGainSource.SaveRestore);
        var target = InventoryManager.AddToInventory(EquipmentIds.GangDun, EquipmentGainSource.SaveRestore);
        Assert(reforger != null && target != null, "重铸器测试装备未加入背包");

        var goldBeforeDeviceSale = GameManager.Gold;
        var deviceSale = InventoryManager.SellItem(reforger!.InstanceId);
        Assert(deviceSale == InventoryManager.GetSellPrice(EquipmentRarity.Epic)
            && GameManager.Gold == goldBeforeDeviceSale + deviceSale,
            "出售重铸器没有按正常售价结算金币");
        Assert(InventoryManager.IsReforgerSaleArmed, "出售重铸器后没有武装下一次出售重铸");

        var goldBeforeTargetSale = GameManager.Gold;
        var targetSale = InventoryManager.SellItem(target!.InstanceId);
        Assert(targetSale == 0 && InventoryManager.LastSaleUsedReforger,
            "重铸器没有接管下一次出售并执行同品质替换");
        Assert(InventoryManager.LastSaleReforgeOriginal?.Id == EquipmentIds.GangDun,
            "重铸器没有记录被重铸的原装备，背包界面无法显示结果");
        Assert(InventoryManager.LastSaleReforgeReplacement != null
               && InventoryManager.LastSaleReforgeReplacement.Id != EquipmentIds.GangDun,
            "重铸器没有记录替换装备，背包界面无法显示结果");
        Assert(GameManager.Gold == goldBeforeTargetSale, "重铸器替换不应额外获得出售金币");
        Assert(!InventoryManager.IsReforgerSaleArmed, "重铸器替换后没有消费机会");

        var replacement = InventoryManager.GetAllOwned().Single();
        Assert(replacement.Definition.Id != EquipmentIds.GangDun
            && replacement.Definition.Rarity == EquipmentRarity.Rare,
            "重铸器没有将出售目标替换为另一件同品质装备");

        InventoryManager.Reset();
        GameManager.BeginNewRun();
    }

    private void TestZhuQueYuShanAmplifiesFireKillAndZhouYuFireAttack()
    {
        GameManager.BeginNewRun();
        InventoryManager.Reset();
        var fan = InventoryManager.AddToInventory(EquipmentIds.ZhuQueYuShan, EquipmentGainSource.SaveRestore);
        Assert(fan != null && InventoryManager.EquipToSlot(fan!.InstanceId, EquipmentSlot.Weapon),
            "朱雀羽扇无法装备到武器槽");

        var definition = EquipmentDatabase.GetEquipment(EquipmentIds.ZhuQueYuShan);
        Assert(definition != null
            && definition.Rarity == EquipmentRarity.Epic
            && definition.AcquisitionMethod == EquipmentAcquisitionMethod.Unknown
            && definition.Types.Contains(EquipmentType.Buff)
            && definition.Types.Contains(EquipmentType.Weapon),
            "朱雀羽扇定义、品质、来源或类型错误");

        var player = new Player("朱雀羽扇测试玩家", "zhuque_yushan_player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 2);
        player.SetCharacter(CharacterDatabase.GetCharacter(CharacterIds.ZhouYu)!);
        var context = new BattleContext(player, BattleTriggerEffects.CreateDefaultManager());
        context.BeginRoundResult();

        var fireKillTarget = CreateZhuQueTestTarget("zhuque_fire_kill_target");
        BattlePhaseResolutionEffect.DealAttackDamage(context, player, fireKillTarget, CardType.FireKill);
        Assert(fireKillTarget.Health == 70, "朱雀羽扇应将15点火杀伤害加倍为30点");

        var zhouYuFireAttackTarget = CreateZhuQueTestTarget("zhuque_zhouyu_fire_attack_target");
        BattlePhaseResolutionEffect.DealAttackDamage(context, player, zhouYuFireAttackTarget, CardType.FireAttack);
        Assert(zhouYuFireAttackTarget.Health == 70, "周瑜携带朱雀羽扇时，火攻应将15点基础伤害加倍为30点");

        player.SetCharacter(CharacterDatabase.GetCharacter(CharacterIds.ZhaoYun)!);
        var nonZhouYuFireAttackTarget = CreateZhuQueTestTarget("zhuque_non_zhouyu_fire_attack_target");
        BattlePhaseResolutionEffect.DealAttackDamage(context, player, nonZhouYuFireAttackTarget, CardType.FireAttack);
        Assert(nonZhouYuFireAttackTarget.Health == 85, "非周瑜使用火攻不应触发朱雀羽扇的双倍伤害");

        InventoryManager.Reset();
        GameManager.BeginNewRun();
    }

    private static EnemyInstance CreateZhuQueTestTarget(string id) => new(new EnemyDefinition
    {
        Id = id,
        Name = "朱雀羽扇测试目标",
        MaxHP = 100,
        Type = EnemyType.Normal,
        StartingDeck = new EnemyDeck()
    });

    private void Assert(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
