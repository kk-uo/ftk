//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/NightVisionGogglesRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证夜视镜对单体伤害攻击牌（杀系）的×1.2伤害加成。
// 2. 验证夜视镜把群体伤害攻击牌集中到场上血量最高的一名敌人身上，
//    伤害×存活敌人数，其余敌人本次伤害归零。
// 3. 验证未装备夜视镜时不影响任何伤害结算。
//
// 不负责：
// × 验证商店随机权重/图鉴UI展示。
// × 验证响应判定（闪避/反震）本身的规则。
//
// 主要依赖：
// EquipmentDatabase / NightVisionGogglesSingleTargetBonusEffect / NightVisionGogglesGroupFocusEffect
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// 夜视镜装备效果的 Headless 回归入口。
/// </summary>
public partial class NightVisionGogglesRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestDefinition();
            TestSingleTargetBonusOnlyWhenEquipped();
            TestGroupFocusRedirectsToHighestHpEnemyAndScalesByCount();
            TestGroupFocusDoesNotAffectEnemySourcedDamage();
            GD.Print($"NIGHT_VISION_GOGGLES_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"NIGHT_VISION_GOGGLES_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestDefinition()
    {
        var def = EquipmentDatabase.GetEquipment(EquipmentIds.NightVisionGoggles)
            ?? throw new InvalidOperationException("找不到夜视镜装备定义");
        Assert(def.Rarity == EquipmentRarity.Epic, "夜视镜应为史诗稀有度");
        Assert(def.AcquisitionMethod == EquipmentAcquisitionMethod.Shop, "夜视镜应只能通过商店获得");
        Assert(def.Effects.Count == 2, "夜视镜应该有两条效果说明");
    }

    private void TestSingleTargetBonusOnlyWhenEquipped()
    {
        ResetRun(CharacterIds.ZhaoYun);
        var player = CreatePlayer();
        var enemy = CreateEnemy("nvg_single_no_equip");
        var context = CreateContext(player, enemy);
        var damage = new DamageEvent(player, enemy, CardType.Kill, 10, true);
        context.DamageEvent = damage;

        new NightVisionGogglesSingleTargetBonusEffect().Execute(context);
        damage.ResolveModifiers();
        Assert(damage.Amount == 10, "未装备夜视镜时不应该出现单体伤害加成");

        EquipAccessory(EquipmentIds.NightVisionGoggles);
        player = CreatePlayer();
        enemy = CreateEnemy("nvg_single_equipped");
        context = CreateContext(player, enemy);
        damage = new DamageEvent(player, enemy, CardType.Kill, 10, true);
        context.DamageEvent = damage;

        new NightVisionGogglesSingleTargetBonusEffect().Execute(context);
        damage.ResolveModifiers();
        Assert(damage.Amount == 12, $"装备夜视镜后普通杀应为10×1.2=12，实际{damage.Amount}");

        // 群体伤害攻击牌不应该被单体加成效果影响（各自独立生效）。
        var groupDamage = new DamageEvent(player, enemy, CardType.ArrowBarrage, 10, true);
        context.DamageEvent = groupDamage;
        new NightVisionGogglesSingleTargetBonusEffect().Execute(context);
        groupDamage.ResolveModifiers();
        Assert(groupDamage.Amount == 10, "单体伤害加成不应该影响群体伤害攻击牌");
    }

    private void TestGroupFocusRedirectsToHighestHpEnemyAndScalesByCount()
    {
        ResetRun(CharacterIds.ZhaoYun);
        EquipAccessory(EquipmentIds.NightVisionGoggles);

        var player = CreatePlayer();
        var weakEnemy = CreateEnemy("nvg_group_weak", maxHp: 30);
        var strongEnemy = CreateEnemy("nvg_group_strong", maxHp: 90);
        var deadEnemy = CreateEnemy("nvg_group_dead", maxHp: 20);
        deadEnemy.MarkDead();

        var encounter = new BattleEncounter();
        encounter.Enemies.Add(weakEnemy);
        encounter.Enemies.Add(strongEnemy);
        encounter.Enemies.Add(deadEnemy);
        var context = new BattleContext(player, new TriggerManager()) { Encounter = encounter };
        context.BeginRoundResult();

        var groupEffect = new NightVisionGogglesGroupFocusEffect();

        var hitWeak = new DamageEvent(player, weakEnemy, CardType.NanmanInvasion, 10, true);
        context.DamageEvent = hitWeak;
        groupEffect.Execute(context);
        hitWeak.ResolveModifiers();
        Assert(hitWeak.Amount == 0, $"非锁定目标的群体伤害应该归零，实际{hitWeak.Amount}");

        var hitStrong = new DamageEvent(player, strongEnemy, CardType.NanmanInvasion, 10, true);
        context.DamageEvent = hitStrong;
        groupEffect.Execute(context);
        hitStrong.ResolveModifiers();
        // 存活敌人数=2（不含已死亡的deadEnemy），血量最高者=strongEnemy，10×2=20。
        Assert(hitStrong.Amount == 20, $"锁定目标（血量最高的存活敌人）伤害应为10×2=20，实际{hitStrong.Amount}");
        Assert(ReferenceEquals(context.NightVisionAoeLockedTarget, strongEnemy), "锁定目标应该是场上血量最高的存活敌人");
        Assert(context.NightVisionAoeEnemyCount == 2, $"存活敌人数应为2（排除已死亡敌人），实际{context.NightVisionAoeEnemyCount}");

        // 同一回合内锁定目标应该保持稳定，即使再来一次群体攻击命中。
        var hitStrongAgain = new DamageEvent(player, strongEnemy, CardType.Tuxi, 5, true);
        context.DamageEvent = hitStrongAgain;
        groupEffect.Execute(context);
        hitStrongAgain.ResolveModifiers();
        Assert(hitStrongAgain.Amount == 10, $"同一回合内锁定目标应保持稳定，5×2=10，实际{hitStrongAgain.Amount}");

        // 回合结束重置后，锁定目标应该被清空，供下一回合重新计算。
        context.BeginRoundResult();
        Assert(context.NightVisionAoeLockedTarget == null, "回合开始时锁定目标应该被清空");
    }

    private void TestGroupFocusDoesNotAffectEnemySourcedDamage()
    {
        ResetRun(CharacterIds.ZhaoYun);
        EquipAccessory(EquipmentIds.NightVisionGoggles);

        var player = CreatePlayer();
        var enemy = CreateEnemy("nvg_group_enemy_source");
        var context = CreateContext(player, enemy);

        // 敌人对玩家使用群体伤害攻击牌时，夜视镜（玩家装备）不应该生效。
        var damage = new DamageEvent(enemy, player, CardType.NanmanInvasion, 10, true);
        context.DamageEvent = damage;
        new NightVisionGogglesGroupFocusEffect().Execute(context);
        damage.ResolveModifiers();
        Assert(damage.Amount == 10, "夜视镜不应该影响敌人对玩家造成的群体伤害");
    }

    private static void EquipAccessory(string equipmentId)
    {
        var item = InventoryManager.AddToInventory(equipmentId, EquipmentGainSource.SaveRestore)
            ?? throw new InvalidOperationException($"无法加入测试装备：{equipmentId}");
        if (!InventoryManager.EquipToSlot(item.InstanceId, EquipmentSlot.Accessory1))
        {
            throw new InvalidOperationException($"无法装备测试饰品：{equipmentId}");
        }
    }

    private static void ResetRun(string characterId)
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(characterId);
        InventoryManager.Reset();
    }

    private static Player CreatePlayer()
    {
        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(100, 100, 10);
        return player;
    }

    private static EnemyInstance CreateEnemy(string id, int maxHp = 50)
    {
        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = id,
            Name = "夜视镜测试敌人",
            MaxHP = maxHp,
            Type = EnemyType.Normal,
            StartingResource = 0,
            StartingDeck = new EnemyDeck()
        });
        return enemy;
    }

    private static BattleContext CreateContext(Player player, EnemyInstance enemy)
    {
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        var context = new BattleContext(player, new TriggerManager())
        {
            Encounter = encounter,
            PlayerLockedTarget = enemy
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
