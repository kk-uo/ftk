//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/GuDingDaoEquipmentRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证古锭刀三阶段的数据、触发条件和徐盛倍率。
// 2. 验证真·古锭刀的目标费用与动态行动组。
// 3. 验证巨大石碑可将锈古锭刀升级为古锭刀。
//
// 不负责：
// × 验证行动栏的像素布局。
// × 验证商店随机权重。
//
// 主要依赖：
// EquipmentDatabase
// BattleRules
// GuDingDaoEquipment
// InventoryManager
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 古锭刀系列装备的 Headless 回归入口。
/// </summary>
public partial class GuDingDaoEquipmentRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行装备定义、战斗触发、费用与石碑升级回归。
    /// </summary>
    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestDefinitions();
            TestRustDodgeBypass();
            TestEpicCardConversion();
            TestDodgeIntegration();
            TestXuShengDamageMultipliers();
            TestTrueBladeActionSetAndCost();
            TestSteleUpgrade();
            GD.Print($"GU_DING_DAO_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"GU_DING_DAO_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestDefinitions()
    {
        var rust = GetDefinition(EquipmentIds.RustGuDingDao);
        var epic = GetDefinition(EquipmentIds.GuDingDao);
        var trueBlade = GetDefinition(EquipmentIds.TrueGuDingDao);

        Assert(rust.Rarity == EquipmentRarity.Rare && rust.AcquisitionMethod == EquipmentAcquisitionMethod.Shop,
            "锈古锭刀应为商店稀有武器");
        Assert(epic.Rarity == EquipmentRarity.Epic && epic.AcquisitionMethod == EquipmentAcquisitionMethod.Shop,
            "古锭刀应为商店史诗武器");
        Assert(trueBlade.Rarity == EquipmentRarity.Legendary
            && trueBlade.AcquisitionMethod == EquipmentAcquisitionMethod.Event
            && !trueBlade.CanAppearInRandomPool,
            "真·古锭刀应为古蜀铸炉专属且不能进入随机池的传奇武器");
    }

    private void TestRustDodgeBypass()
    {
        EquipWeapon(CharacterIds.ZhaoYun, EquipmentIds.RustGuDingDao);
        var player = CreatePlayer();
        var enemy = CreateEnemy(0);
        var context = CreateContext(player, enemy);
        context.DamageEvent = new DamageEvent(player, enemy, CardType.Kill, BattleConstants.KillDamage, true);

        new RustGuDingDaoBeforeDamageEffect().Execute(context);
        Assert(context.IgnoreDefenderDodgeForThisAttack, "零费用敌人的闪应被锈古锭刀忽略");

        enemy.DebugSetMana(1);
        context.IgnoreDefenderDodgeForThisAttack = false;
        new RustGuDingDaoBeforeDamageEffect().Execute(context);
        Assert(!context.IgnoreDefenderDodgeForThisAttack, "敌人仍有费用时不应忽略闪");
    }

    private void TestEpicCardConversion()
    {
        EquipWeapon(CharacterIds.ZhaoYun, EquipmentIds.GuDingDao);
        var player = CreatePlayer();
        var enemy = CreateEnemy(0);
        var context = CreateContext(player, enemy);
        var original = BattleAction.FromCard(Card.Kill(), 2, enemy);
        context.PlayerAction = original;

        new GuDingDaoRevealEffect().Execute(context);
        Assert(context.PlayerAction?.Type == CardType.SureKill, "零费用目标应令普通杀转换为必中杀");
        Assert(context.PlayerAction?.CostType == CardType.Kill, "转换后必须保留普通杀费用来源");
        Assert(context.PlayerAction?.Count == 2 && ReferenceEquals(context.PlayerAction.Target, enemy),
            "转换后必须保留叠加数量与锁定目标");

        enemy.DebugSetMana(1);
        context.PlayerAction = BattleAction.FromCard(Card.Kill(), 1, enemy);
        new GuDingDaoRevealEffect().Execute(context);
        Assert(context.PlayerAction.Type == CardType.Kill, "敌人仍有费用时普通杀不应转换");
    }

    private void TestXuShengDamageMultipliers()
    {
        AssertDamageMultiplier(EquipmentIds.RustGuDingDao, CardType.Kill, 10, 12);
        AssertDamageMultiplier(EquipmentIds.GuDingDao, CardType.FireKill, 15, 22);
        AssertDamageMultiplier(EquipmentIds.TrueGuDingDao, CardType.FireThunderKill, 30, 52);
    }

    private void TestDodgeIntegration()
    {
        AssertDodgeOutcome(EquipmentIds.RustGuDingDao, 0, BattleConstants.KillDamage);
        AssertDodgeOutcome(EquipmentIds.RustGuDingDao, 1, 0);
        AssertDodgeOutcome(EquipmentIds.GuDingDao, 0, BattleConstants.KillDamage);
        AssertDodgeOutcome(EquipmentIds.GuDingDao, 1, 0);
    }

    private void AssertDodgeOutcome(string equipmentId, int enemyMana, int expectedDamage)
    {
        EquipWeapon(CharacterIds.ZhaoYun, equipmentId);
        var player = CreatePlayer();
        var enemy = CreateEnemy(enemyMana);
        var triggerManager = BattleTriggerEffects.CreateDefaultManager();
        var context = new BattleContext(player, triggerManager)
        {
            Encounter = new BattleEncounter(),
            PlayerLockedTarget = enemy
        };
        context.Encounter.Enemies.Add(enemy);
        context.BeginRoundResult();
        context.PlayerAction = BattleAction.FromCard(Card.Kill(), 1, enemy);
        context.SetActionForEnemy(enemy, BattleAction.FromCard(Card.Dodge(), 1, player));
        var healthBefore = enemy.Health;

        triggerManager.RaiseTrigger(TriggerTiming.OnBattleReveal, context);
        triggerManager.RaiseTrigger(TriggerTiming.OnBattlePhase, context);

        Assert(healthBefore - enemy.Health == expectedDamage,
            $"{equipmentId} 对 {enemyMana} 费闪的实际伤害错误：expected={expectedDamage}, actual={healthBefore - enemy.Health}");
    }

    private void TestTrueBladeActionSetAndCost()
    {
        EquipWeapon(CharacterIds.ZhaoYun, EquipmentIds.TrueGuDingDao);
        var player = CreatePlayer();
        var emptyEnemy = CreateEnemy(0);
        var fundedEnemy = CreateEnemy(1);

        Assert(BattleRules.UsesTrueGuDingDaoActionSet(player), "装备真·古锭刀后应切换为火雷杀行动组");
        Assert(Math.Abs(BattleRules.GetCardCost(player, CardType.FireThunderKill, emptyEnemy) - 1d) < 0.001,
            "零费用目标的火雷杀费用应为1");
        Assert(Math.Abs(BattleRules.GetCardCost(player, CardType.FireThunderKill, fundedEnemy) - Card.FireThunderKill().Cost) < 0.001,
            "有费用目标的火雷杀应保持基础费用");
        Assert(Math.Abs(BattleRules.GetActionCost(
            player,
            BattleAction.FromCard(Card.FireThunderKill(), 1, emptyEnemy)) - 1d) < 0.001,
            "正式行动扣费必须与目标费用预览一致");
    }

    private void TestSteleUpgrade()
    {
        ResetRun(CharacterIds.ZhaoYun);
        InventoryManager.AddToInventory(EquipmentIds.RustGuDingDao, EquipmentGainSource.SaveRestore);

        var upgraded = InventoryManager.UpgradeRustWeapons();
        Assert(!InventoryManager.GetAllOwned().Any(item => item.Definition.Id == EquipmentIds.RustGuDingDao),
            "磨刀后锈古锭刀应被移除");
        Assert(InventoryManager.GetAllOwned().Any(item => item.Definition.Id == EquipmentIds.GuDingDao),
            "磨刀后应获得古锭刀");
        Assert(upgraded.Contains(Localization.GetName(GetDefinition(EquipmentIds.GuDingDao))),
            "磨刀结果应返回升级后的装备名称");
    }

    private void AssertDamageMultiplier(
        string equipmentId,
        CardType attackType,
        int baseDamage,
        int expectedDamage)
    {
        EquipWeapon(CharacterIds.XuSheng, equipmentId);
        var player = CreatePlayer();
        var enemy = CreateEnemy(0);
        var context = CreateContext(player, enemy);
        var damage = new DamageEvent(player, enemy, attackType, baseDamage, true);
        context.DamageEvent = damage;

        new GuDingDaoDamageBonusEffect().Execute(context);
        damage.ResolveModifiers();
        Assert(damage.Amount == expectedDamage,
            $"{equipmentId} 对 {attackType} 的徐盛倍率错误：expected={expectedDamage}, actual={damage.Amount}");
    }

    private static void EquipWeapon(string characterId, string equipmentId)
    {
        ResetRun(characterId);
        var item = InventoryManager.AddToInventory(equipmentId, EquipmentGainSource.SaveRestore)
            ?? throw new InvalidOperationException($"无法加入测试装备：{equipmentId}");
        if (!InventoryManager.EquipToSlot(item.InstanceId, EquipmentSlot.Weapon))
        {
            throw new InvalidOperationException($"无法装备测试武器：{equipmentId}");
        }
    }

    private static void ResetRun(string characterId)
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(characterId);
        InventoryManager.Reset();
    }

    private static EquipmentDefinition GetDefinition(string id)
    {
        return EquipmentDatabase.GetEquipment(id)
            ?? throw new InvalidOperationException($"缺少装备定义：{id}");
    }

    private static Player CreatePlayer()
    {
        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(100, 100, 10);
        return player;
    }

    private static EnemyInstance CreateEnemy(int mana)
    {
        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = $"gu_ding_dao_enemy_{mana:0.#}",
            Name = "古锭刀测试敌人",
            MaxHP = 100,
            Type = EnemyType.Normal,
            StartingResource = mana,
            StartingDeck = new EnemyDeck()
        });
        enemy.DebugSetMana(mana);
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
