using Godot;
using System;

/// <summary>
/// 回归：整合式教程必须是隔离流程，不能生成章节变种、阵营命运、天命骰或正式地图节点。
/// </summary>
public partial class IntegratedTutorialIsolationRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            VerifyTutorialRunHasNoRandomChapterOrFactionFate();
            GD.Print($"INTEGRATED_TUTORIAL_ISOLATION_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"INTEGRATED_TUTORIAL_ISOLATION_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            IntegratedTutorialFlow.Reset();
            GameManager.BeginNewRun();
        }
    }

    private void VerifyTutorialRunHasNoRandomChapterOrFactionFate()
    {
        IntegratedTutorialFlow.Reset();
        IntegratedTutorialFlow.Start();
        GameManager.BeginNewRun();
        // 使用有阵营命运候选的魏国角色，确保断言能覆盖正常流程本应触发的随机项。
        GameManager.SelectCharacter(CharacterIds.CaoZhen);

        Assert(GameManager.CurrentChapterVariant == ChapterVariant.None, "教程错误生成了章节特殊变种");
        Assert(FactionFateManager.CurrentFateId == null, "教程错误生成了开局阵营命运");
        Assert(FactionFateManager.DiceHistory.Count == 0, "教程错误掷出了天命骰");
        Assert(GameManager.GetNode(IntegratedTutorialFlow.TutorialBattleNodeId) != null, "教程地图缺少教学战斗节点");
        Assert(GameManager.GetNode("initial_event") == null, "教程错误生成了正式章节初始事件节点");
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
