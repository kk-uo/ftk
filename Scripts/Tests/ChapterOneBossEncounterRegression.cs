using Godot;
using System;
using System.Linq;

/// <summary>
/// 验证第一章 Boss【背叛者】单独出场，且不影响城关守卫的普通关卡配置。
/// </summary>
public partial class ChapterOneBossEncounterRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            var bossStage = StageDatabase.GetStage("1-7")
                ?? throw new InvalidOperationException("找不到第一章 Boss 关");
            var traitorEncounter = bossStage.EncounterPool.SingleOrDefault(entry => entry.EnemyIds.Contains("boss_traitor"));

            Assert(traitorEncounter != null, "第一章 Boss 关缺少背叛者遭遇");
            Assert(traitorEncounter!.EnemyIds.SequenceEqual(new[] { "boss_traitor" }),
                "背叛者遭遇仍携带城关守卫或其它随从");

            // 第一章末位 Boss【失疯卖艺人】的定义：100 HP，且技能只能是【击鼓】【不屈】。
            // 这里锁定数据，避免后续调整 AI 或掉落时意外混入其它技能。
            var crazyPerformer = EnemyDatabase.GetEnemy("crazy_performer")
                ?? throw new InvalidOperationException("找不到第一章 Boss【失疯卖艺人】");
            Assert(crazyPerformer.MaxHP == 100, "失疯卖艺人的生命值应为100");
            Assert(crazyPerformer.SkillIds.SequenceEqual(new[] { SkillIds.JiGu, SkillIds.BuDao }),
                "失疯卖艺人的技能应仅为【击鼓】【不屈】");

            var firstStage = StageDatabase.GetStage("1-1")
                ?? throw new InvalidOperationException("找不到第一章第一关");
            Assert(firstStage.EncounterPool.Any(entry => entry.EnemyIds.Contains("gate_guard")),
                "移除背叛者随从时错误移除了普通关卡中的城关守卫");

            GD.Print($"CHAPTER_ONE_BOSS_ENCOUNTER_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"CHAPTER_ONE_BOSS_ENCOUNTER_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
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
