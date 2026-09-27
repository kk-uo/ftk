//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/CardMatchupCoverageRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证 CardMatchupData 覆盖了 CardType 枚举的全部取值，图鉴内不会出现
//    "有牌但没有克制关系说明"的空白。
// 2. 抽样验证几条关键克制关系与 BattleRules.BeatsAttack / 实际战斗结算代码
//    保持一致，防止图鉴文案与真实规则日后逐渐脱节。
//
// 不负责：
// × 验证图鉴UI渲染。
//
// 主要依赖：
// CardMatchupData / BattleRules.BeatsAttack
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;

/// <summary>
/// 卡牌克制关系数据完整性的 Headless 回归入口。
/// </summary>
public partial class CardMatchupCoverageRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestAllCardTypesHaveMatchupEntries();
            TestCircleConsistencyWithBattleRules();
            TestIceKillAndPoisonKillKeyFacts();
            TestShadowKillMirrorsSureKillCircle();

            GD.Print($"CARD_MATCHUP_COVERAGE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"CARD_MATCHUP_COVERAGE_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestAllCardTypesHaveMatchupEntries()
    {
        foreach (var type in Enum.GetValues<CardType>())
        {
            var lines = CardMatchupData.GetMatchups(type);
            Assert(lines != null && lines.Length > 0, $"{type} 在图鉴克制关系表里没有任何说明");
        }
    }

    // 三元循环克制（火杀>杀>雷杀>火杀）是绝对规则，图鉴文案必须和 BeatsAttack 一致。
    private void TestCircleConsistencyWithBattleRules()
    {
        Assert(BattleRules.BeatsAttack(CardType.FireKill, CardType.Kill), "火杀应克制杀（BattleRules真实规则）");
        Assert(BattleRules.BeatsAttack(CardType.Kill, CardType.ThunderKill), "杀应克制雷杀（BattleRules真实规则）");
        Assert(BattleRules.BeatsAttack(CardType.ThunderKill, CardType.FireKill), "雷杀应克制火杀（BattleRules真实规则）");

        var killLines = CardMatchupData.GetMatchups(CardType.Kill)!;
        Assert(killLines.Any(line => line.Contains("火杀") && line.Contains("本方受伤")), "杀的图鉴文案应体现被火杀克制");
        Assert(killLines.Any(line => line.Contains("雷杀") && line.Contains("对方受伤")), "杀的图鉴文案应体现克制雷杀");
    }

    // 冰杀/毒杀是较晚加入战斗系统的杀系分支，专门验证它们的克制关系已经补全到图鉴，
    // 且与 BattleRules.BeatsAttack 的真实判定一致（这两点是本次任务新增的重点）。
    private void TestIceKillAndPoisonKillKeyFacts()
    {
        Assert(BattleRules.BeatsAttack(CardType.IceKill, CardType.Kill), "冰杀应克制杀（BattleRules真实规则）");
        Assert(BattleRules.BeatsAttack(CardType.IceKill, CardType.ThunderKill), "冰杀应克制雷杀（BattleRules真实规则）");
        Assert(BattleRules.BeatsAttack(CardType.FireKill, CardType.IceKill), "火杀应克制冰杀（BattleRules真实规则）");
        Assert(!BattleRules.BeatsAttack(CardType.SureKill, CardType.IceKill), "必中杀不应克制冰杀（冰杀不在必中杀克制范围内）");

        var iceLines = CardMatchupData.GetMatchups(CardType.IceKill)!;
        Assert(iceLines.Any(line => line.Contains("必中杀") && line.Contains("双方无伤")), "冰杀图鉴文案应体现与必中杀互不克制");
        Assert(iceLines.Any(line => line.Contains("穿透闪")), "冰杀图鉴文案应体现穿透闪");

        Assert(BattleRules.BeatsAttack(CardType.FireKill, CardType.PoisonKill), "火杀应克制毒杀（BattleRules真实规则）");
        Assert(BattleRules.BeatsAttack(CardType.PoisonKill, CardType.ThunderKill), "毒杀应克制雷杀（BattleRules真实规则）");
        Assert(!BattleRules.BeatsAttack(CardType.Kill, CardType.PoisonKill) && !BattleRules.BeatsAttack(CardType.PoisonKill, CardType.Kill),
            "普通杀与毒杀应互不克制（BattleRules真实规则）");

        var poisonLines = CardMatchupData.GetMatchups(CardType.PoisonKill)!;
        Assert(poisonLines.Any(line => line.Contains("杀：双方无伤")), "毒杀图鉴文案应体现与普通杀互不克制");
    }

    // 影袭杀（黄月英专属）与必中杀共享 AttackType.DirectSha；万箭齐发响应也必须
    // 进入必中类反击分支。影袭状态本身负责取消影袭杀使用者受到的箭雨伤害。
    private void TestShadowKillMirrorsSureKillCircle()
    {
        Assert(BattleRules.BeatsAttack(CardType.ShadowKill, CardType.Kill) == BattleRules.BeatsAttack(CardType.SureKill, CardType.Kill),
            "影袭杀对杀的克制判定应与必中杀一致");
        Assert(BattleRules.BeatsAttack(CardType.ShadowKill, CardType.FireThunderKill) == BattleRules.BeatsAttack(CardType.SureKill, CardType.FireThunderKill),
            "影袭杀对火雷杀的克制判定应与必中杀一致");

        var shadowLines = CardMatchupData.GetMatchups(CardType.ShadowKill)!;
        Assert(shadowLines.Any(line => line.Contains("万箭齐发") && line.Contains("反制")),
            "影袭杀图鉴文案应体现它能反制万箭齐发");
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
