using Godot;
using System;

/// <summary>
/// Headless regression coverage for the 10%-rain chapter variant.
/// Verifies Wet applies only to elemental damage and that Waterproof Module prevents it.
/// </summary>
public partial class WetChapterVariantRegression : Node
{
    private int _assertions;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            ResetRun();
            GameManager.SetDebugChapterVariant(ChapterVariant.Rainstorm);
            Assert(GameManager.CurrentChapterVariant == ChapterVariant.Rainstorm, "暴雨章节变种没有设置成功");
            Assert(RunBuffManager.CountStacks(RunBuffIds.Wet) == 1, "暴雨没有添加潮湿章节Buff");

            TestElementalDamageAmplification();
            TestPhysicalDamageUnaffected();
            TestWaterproofImmunity();

            GD.Print($"WET_CHAPTER_VARIANT_TEST_PASS assertions={_assertions}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"WET_CHAPTER_VARIANT_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestElementalDamageAmplification()
    {
        var context = CreateContext(out var player, out var enemy);
        var damage = new DamageEvent(player, enemy, CardType.FireKill, 10, true);
        context.DamageEvent = damage;
        new WetDamageEffect().Execute(context);
        damage.ResolveModifiers();
        Assert(damage.Amount == 12, $"潮湿下火属性伤害应为10×1.2=12，实际{damage.Amount}");
        Assert(BattleUnitUiFormatter.GetStatuses(enemy, context).Exists(status => status.Id == "wet"), "敌方未显示潮湿状态");
    }

    private void TestPhysicalDamageUnaffected()
    {
        var context = CreateContext(out var player, out var enemy);
        var damage = new DamageEvent(player, enemy, CardType.Kill, 10, true);
        context.DamageEvent = damage;
        new WetDamageEffect().Execute(context);
        damage.ResolveModifiers();
        Assert(damage.Amount == 10, $"潮湿不应影响物理伤害，实际{damage.Amount}");
    }

    private void TestWaterproofImmunity()
    {
        var item = InventoryManager.AddToInventory(EquipmentIds.WaterproofModule, EquipmentGainSource.SaveRestore)
            ?? throw new InvalidOperationException("无法加入防水模块测试装备");
        Assert(InventoryManager.EquipToSlot(item.InstanceId, EquipmentSlot.Accessory1), "无法装备防水模块");

        var context = CreateContext(out var player, out _);
        var damage = new DamageEvent(new Player("敌方", "wet_test_enemy", BattleTeam.Enemy), player, CardType.ThunderKill, 10, true);
        context.DamageEvent = damage;
        new WetDamageEffect().Execute(context);
        damage.ResolveModifiers();
        Assert(damage.Amount == 10, $"防水模块应免疫潮湿加成，实际{damage.Amount}");
        Assert(!BattleUnitUiFormatter.GetStatuses(player, context).Exists(status => status.Id == "wet"), "防水模块持有者不应显示潮湿状态");
    }

    private static void ResetRun()
    {
        GameManager.BeginNewRun();
        GameManager.SelectCharacter(CharacterIds.ZhaoYun);
        InventoryManager.Reset();
        GameManager.SetDebugChapterVariant(ChapterVariant.None);
    }

    private static BattleContext CreateContext(out Player player, out EnemyInstance enemy)
    {
        player = new Player("玩家", "wet_test_player", BattleTeam.Player);
        player.ResetForNewBattle(100, 100, 10);
        enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "wet_test_enemy",
            Name = "潮湿测试敌人",
            MaxHP = 100,
            Type = EnemyType.Normal,
            StartingResource = 0,
            StartingDeck = new EnemyDeck()
        });
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);
        var context = new BattleContext(player, new TriggerManager()) { Encounter = encounter, PlayerLockedTarget = enemy };
        context.BeginRoundResult();
        return context;
    }

    private void Assert(bool condition, string message)
    {
        _assertions++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
