//////////////////////////////////////////////////////////
// 黄天：闪电创建、10%落雷、张角↔敌人转移、攻击锦囊加伤和素材存在性回归。
//////////////////////////////////////////////////////////

using Godot;
using System;

public partial class HuangTianRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestThunderDamageCreatesLightningOnZhangJiaoEnemy();
            TestLightningStrikeUsesTrickDamageBonus();
            TestLightningCyclesBetweenZhangJiaoAndEnemy();
            TestPixelVfxAssetsExist();
            GD.Print($"HUANGTIAN_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"HUANGTIAN_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            LightningBuffTurnEffect.RandomRollForTesting = null;
        }
    }

    private void TestThunderDamageCreatesLightningOnZhangJiaoEnemy()
    {
        var (player, enemy, context) = CreateContext();
        var visualRequestCount = 0;
        context.HuangTianVisualRequested = _ => visualRequestCount++;
        context.DamageEvent = new DamageEvent(player, enemy, CardType.ThunderKill, 10,
            overrideDamageType: DamageType.Thunder);

        new HuangTianEffect().Execute(context);

        Assert(LightningBuffHelper.HasLightning(enemy), "敌人受到雷伤后【黄天】没有施加闪电");
        Assert(ReferenceEquals(LightningBuffHelper.GetOwner(enemy), player), "闪电没有记录张角为归属者");
        Assert(!LightningBuffHelper.HasLightning(player), "张角自己不应因敌人中雷立刻获得闪电");
        Assert(visualRequestCount == 1, "黄天施加闪电时没有提交小乌云表现请求");
    }

    private void TestLightningStrikeUsesTrickDamageBonus()
    {
        GameManager.BeginNewRun();
        GameManager.IncrementKnowledgeChipCount();
        var (_, enemy, context) = CreateContext();
        var player = context.Player;
        LightningBuffHelper.SetLightning(enemy, player);
        LightningBuffTurnEffect.RandomRollForTesting = () => 0.01;

        new LightningBuffTurnEffect().Execute(context);

        Assert(enemy.MaxHealth - enemy.Health == 25,
            "黄天落雷没有结算20基础伤害加知识芯片的5点攻击锦囊加伤");
        Assert(LightningBuffHelper.HasLightning(enemy),
            "敌人被黄天落雷击中后没有依据雷伤触发重新获得闪电");
    }

    private void TestLightningCyclesBetweenZhangJiaoAndEnemy()
    {
        var (player, enemy, context) = CreateContext();
        LightningBuffHelper.SetLightning(enemy, player);
        LightningBuffTurnEffect.RandomRollForTesting = () => 0.50;

        new LightningBuffTurnEffect().Execute(context);
        Assert(LightningBuffHelper.HasLightning(player), "闪电未触发时没有从敌人转移回张角");
        Assert(!LightningBuffHelper.HasLightning(enemy), "闪电转移回张角后敌人仍保留状态");

        new LightningBuffTurnEffect().Execute(context);
        Assert(LightningBuffHelper.HasLightning(enemy), "张角身上的未触发闪电没有转移至随机敌人");
        Assert(!LightningBuffHelper.HasLightning(player), "张角转移闪电后仍保留状态");
    }

    private void TestPixelVfxAssetsExist()
    {
        Assert(ResourceLoader.Exists("res://Assets/Effects/HuangTian/huangtian_storm_cloud.png"),
            "黄天小乌云像素素材缺失");
        Assert(ResourceLoader.Exists("res://Assets/Effects/HuangTian/huangtian_lightning_strike.png"),
            "黄天大落雷像素素材缺失");
    }

    private static (Player Player, EnemyInstance Enemy, BattleContext Context) CreateContext()
    {
        var player = new Player("张角", CharacterIds.ZhangJiao, BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        player.AddSkill(SkillDatabase.GetSkill(SkillIds.HuangTian)!);

        var enemy = new EnemyInstance(new EnemyDefinition
        {
            Id = "huangtian_test_enemy",
            Name = "测试敌人",
            MaxHP = 100,
            Type = EnemyType.Normal,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(enemy);

        var manager = new TriggerManager();
        manager.Register(new EquipmentDamageBonusEffect());
        manager.Register(new ApplyDamageEffect());
        manager.Register(new HuangTianEffect());
        var context = new BattleContext(player, manager) { Encounter = encounter };
        context.BeginRoundResult();
        return (player, enemy, context);
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
