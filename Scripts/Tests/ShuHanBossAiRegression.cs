//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/ShuHanBossAiRegression.cs
//
// 职责：验证蜀汉共生体 Boss 的卡组与 AI 不会退化为只使用普通杀。
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 刘备、关羽、张飞 Boss AI 的 Headless 回归入口。
/// </summary>
public partial class ShuHanBossAiRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestActionDecksAndAvailableAttacks();
            TestAttackWeightsOutweighDefense();
            TestSharedHealthPoolKeepsThreeDistinctTargets();
            GD.Print($"SHU_HAN_BOSS_AI_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"SHU_HAN_BOSS_AI_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestActionDecksAndAvailableAttacks()
    {
        var guanYu = EnemyDatabase.GetEnemy("guan_yu");
        var zhangFei = EnemyDatabase.GetEnemy("zhang_fei");
        var liuBei = EnemyDatabase.GetEnemy("liu_bei");
        Assert(guanYu != null && zhangFei != null && liuBei != null, "蜀汉共生体 Boss 定义缺失");

        Assert(guanYu!.StartingDeck.Cards.Contains(CardType.FireKill)
            && guanYu.StartingDeck.Cards.Contains(CardType.ThunderKill),
            "关羽卡组缺少火杀或雷杀");
        Assert(zhangFei!.StartingDeck.Cards.Contains(CardType.FireKill)
            && zhangFei.StartingDeck.Cards.Contains(CardType.ThunderKill),
            "张飞卡组缺少火杀或雷杀");
        Assert(liuBei!.StartingDeck.Cards.Contains(CardType.Peach)
            && liuBei.StartingDeck.Cards.Contains(CardType.Unassailable),
            "刘备卡组缺少治疗或反制支援行动");
        foreach (var boss in new[] { liuBei, guanYu, zhangFei })
        {
            Assert(!boss.StartingDeck.Cards.Contains(CardType.Steal)
                && !boss.StartingDeck.Cards.Contains(CardType.NanmanInvasion),
                $"{boss.Name}不应使用顺手牵羊或南蛮入侵");
        }
        Assert(!guanYu.StartingDeck.Cards.Contains(CardType.SureKill),
            "关羽不应使用必中杀");

        var ai = new EnemyAI();
        var guanYuInstance = new EnemyInstance(guanYu);
        var zhangFeiInstance = new EnemyInstance(zhangFei);
        var guanYuActions = ai.GetAvailableActionCards(guanYuInstance, guanYu).Select(card => card.Type).ToList();
        var zhangFeiActions = ai.GetAvailableActionCards(zhangFeiInstance, zhangFei).Select(card => card.Type).ToList();
        Assert(guanYuActions.Contains(CardType.FireKill)
            && guanYuActions.Contains(CardType.ThunderKill),
            "关羽在初始3费时无法使用新增威胁行动");
        Assert(zhangFeiActions.Contains(CardType.FireKill)
            && zhangFeiActions.Contains(CardType.ThunderKill),
            "张飞在初始3费时无法使用新增威胁行动");
        Assert(!guanYuActions.Contains(CardType.SureKill)
            && !guanYuActions.Contains(CardType.Steal)
            && !zhangFeiActions.Contains(CardType.Steal)
            && !zhangFeiActions.Contains(CardType.NanmanInvasion),
            "受限行动仍然出现在蜀汉 Boss 的可用行动中");
    }

    private void TestAttackWeightsOutweighDefense()
    {
        var player = new Player("玩家", "test_player", BattleTeam.Player);
        player.ResetForNewBattle(40, 40, 3);
        var ai = new EnemyAI();
        var guanYu = EnemyDatabase.GetEnemy("guan_yu")!;
        var zhangFei = EnemyDatabase.GetEnemy("zhang_fei")!;

        var guanEvaluation = ai.Evaluate(new EnemyInstance(guanYu), player, guanYu);
        Assert(guanEvaluation.FinalWeights.FireSlash > guanEvaluation.FinalWeights.Dodge
            && guanEvaluation.FinalWeights.ThunderSlash > guanEvaluation.FinalWeights.Dodge,
            "关羽的 AI 仍然把防御看得高于核心进攻行动");

        var zhangEvaluation = ai.Evaluate(new EnemyInstance(zhangFei), player, zhangFei);
        Assert(zhangEvaluation.FinalWeights.FireSlash > zhangEvaluation.FinalWeights.Dodge
            && zhangEvaluation.FinalWeights.ThunderSlash > zhangEvaluation.FinalWeights.Dodge,
            "张飞的 AI 仍然把防御看得高于核心进攻行动");
    }

    private void TestSharedHealthPoolKeepsThreeDistinctTargets()
    {
        var liuBeiDefinition = EnemyDatabase.GetEnemy("liu_bei")!;
        var guanYuDefinition = EnemyDatabase.GetEnemy("guan_yu")!;
        var zhangFeiDefinition = EnemyDatabase.GetEnemy("zhang_fei")!;
        var liuBei = new EnemyInstance(liuBeiDefinition);
        var guanYu = new EnemyInstance(guanYuDefinition);
        var zhangFei = new EnemyInstance(zhangFeiDefinition);
        var pool = new SharedHealthPool(liuBei.MaxHealth);

        liuBei.SetSharedPool(pool);
        guanYu.SetSharedPool(pool);
        zhangFei.SetSharedPool(pool);

        Assert(pool.Members.Count == 3, "刘关张共享血池必须保留三个成员对象");
        Assert(!ReferenceEquals(liuBei, guanYu)
            && !ReferenceEquals(guanYu, zhangFei)
            && !ReferenceEquals(liuBei, zhangFei),
            "刘备、关羽、张飞不应合并成同一个可选目标");

        guanYu.TakeDamage(25);
        Assert(liuBei.CurrentHP == pool.MaxHP - 25
            && guanYu.CurrentHP == pool.MaxHP - 25
            && zhangFei.CurrentHP == pool.MaxHP - 25,
            "攻击任一刘关张成员后，三人的共享血量必须同步");
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
