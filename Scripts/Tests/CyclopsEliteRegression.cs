//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/CyclopsEliteRegression.cs
//
// 职责：验证第二章 2-5 精英【独眼巨人】的定义、遭遇入口与反伤技能。
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 独眼巨人精英定义的 Headless 回归入口。
/// </summary>
public partial class CyclopsEliteRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestDefinitionAndStagePlacement();
            TestBloodDebtRetaliation();
            GD.Print($"CYCLOPS_ELITE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"CYCLOPS_ELITE_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestDefinitionAndStagePlacement()
    {
        var cyclops = EnemyDatabase.GetEnemy("cyclops");
        Assert(cyclops != null, "敌人数据库中缺少独眼巨人");
        Assert(cyclops!.MaxHP == 80 && cyclops.Type == EnemyType.Elite,
            "独眼巨人的基础生命或精英类型错误");
        Assert(cyclops.SkillIds.SequenceEqual(new[] { SkillIds.XueZhaiXueChou }),
            "独眼巨人必须装备标准的受伤反击【血债血偿】");
        Assert(cyclops.EquipmentIds.SequenceEqual(new[] { EquipmentIds.ScaleArmor }),
            "独眼巨人必须携带【鳞甲】");
        Assert(cyclops.Tags.Contains(EnemyTag.Wei) && cyclops.Tags.Contains(EnemyTag.RouteCity),
            "独眼巨人缺少魏阵营或城市路线标签");
        Assert(cyclops.StartingResource == 1 && !cyclops.UseSharedHealthPool,
            "独眼巨人的初始费用或独立生命池配置错误");
        Assert(cyclops.Reward.GoldOverride == null,
            "独眼巨人不应覆盖既有精英金币奖励规则");
        Assert(cyclops.Reward.EquipmentDrops.Count == 1
            && cyclops.Reward.EquipmentDrops[0].EquipmentId == EquipmentIds.ScaleArmor
            && Math.Abs(cyclops.Reward.EquipmentDrops[0].Probability - 0.5f) < 0.0001f,
            "独眼巨人应有 50% 概率掉落【鳞甲】");
        Assert(cyclops.StartingDeck.Cards.SequenceEqual(new[]
        {
            CardType.Kill, CardType.FireKill, CardType.ThunderKill, CardType.Dodge,
            CardType.Peach, CardType.Wine, CardType.Steal, CardType.Unassailable, CardType.Fee
        }), "独眼巨人没有使用默认初始卡组");
        Assert(cyclops.ActionWeights.Slash > cyclops.ActionWeights.Dodge
            && cyclops.ActionWeights.FireSlash > cyclops.ActionWeights.Dodge
            && cyclops.ActionWeights.ThunderSlash > cyclops.ActionWeights.Dodge,
            "独眼巨人的 AI 没有保持强攻击、低防御倾向");

        var stage = StageDatabase.GetStage("2-5");
        Assert(stage != null && stage.EncounterPool.Any(entry => entry.EnemyIds.SequenceEqual(new[] { "cyclops" })),
            "城市路线 2-5 的精英池未包含独眼巨人");
    }

    private void TestBloodDebtRetaliation()
    {
        var cyclops = new EnemyInstance(EnemyDatabase.GetEnemy("cyclops")!);
        var player = new Player("玩家", "player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 1);
        var encounter = new BattleEncounter();
        encounter.Enemies.Add(cyclops);
        var context = new BattleContext(player, new TriggerManager())
        {
            Encounter = encounter,
            DamageEvent = new DamageEvent(player, cyclops, CardType.Kill, 10)
        };

        new XueZhaiXueChouRetaliateEffect().Execute(context);

        Assert(player.Health == 30,
            "独眼巨人受到10点伤害后没有以【血债血偿】反弹10点真实伤害");
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
