//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/NeedFeeLessonHintRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证费用不足跳转到共享 NeedFee 步骤时，TutorialManager.PendingFeeLessonTarget
//    能正确记录本来要教的目标卡牌（如火杀）。
// 2. 验证 TutorialOverlay 在 NeedFee 步骤下会拼出"先用费，再用XX"的具体提示，
//    而不是永远显示不带目标卡牌名的通用提示。
// 3. 验证非 NeedFee 步骤、或没有待处理目标卡牌时，仍然回退到步骤自身配置的
//    通用文案，不受这次改动影响。
//
// 不负责：
// × 验证完整教程战斗流程（费用真实凑够后跳回原步骤等）——那部分由既有的
//   TutorialController 费用重定向逻辑负责，本次改动没有触碰。
//
// 主要依赖：
// TutorialManager.PendingFeeLessonTarget / TutorialOverlay.GetObjectiveText（反射）
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Reflection;

/// <summary>
/// NeedFee 步骤"先用费再用XX"具体提示的 Headless 回归入口。
/// </summary>
public partial class NeedFeeLessonHintRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        try
        {
            Localization.Initialize();
            TestPendingFeeLessonTargetRoundTrip();
            TestObjectiveTextNamesTargetCardOnNeedFeeStep();
            TestObjectiveTextFallsBackWithoutPendingTarget();
            TestObjectiveTextUnaffectedOnOtherSteps();

            GD.Print($"NEED_FEE_LESSON_HINT_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"NEED_FEE_LESSON_HINT_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private void TestPendingFeeLessonTargetRoundTrip()
    {
        TutorialManager.PendingFeeLessonTarget = null;
        Assert(TutorialManager.PendingFeeLessonTarget == null, "测试前提失败：初始值应为空");

        TutorialManager.PendingFeeLessonTarget = nameof(CardType.FireKill);
        Assert(TutorialManager.PendingFeeLessonTarget == "FireKill", "PendingFeeLessonTarget 未正确写入火杀");

        TutorialManager.PendingFeeLessonTarget = null;
        Assert(TutorialManager.PendingFeeLessonTarget == null, "PendingFeeLessonTarget 未能正确清空");
    }

    private void TestObjectiveTextNamesTargetCardOnNeedFeeStep()
    {
        TutorialManager.PendingFeeLessonTarget = nameof(CardType.FireKill);
        var needFeeStep = TutorialDatabase.GetStep(TutorialDatabase.StepIds.NeedFee)
            ?? throw new InvalidOperationException("找不到 need_fee 教程步骤定义");

        var text = InvokeGetObjectiveText(needFeeStep);
        var expected = Localization.GetFmt("tutorial.step.need_fee.obj_target_fmt", BattleRules.GetCardName(CardType.FireKill));

        Assert(text == expected, $"NeedFee步骤应显示带火杀名字的具体提示，实际=\"{text}\"，期望=\"{expected}\"");
        Assert(text.Contains(BattleRules.GetCardName(CardType.FireKill)), "具体提示里应该包含火杀的显示名");

        TutorialManager.PendingFeeLessonTarget = null;
    }

    private void TestObjectiveTextFallsBackWithoutPendingTarget()
    {
        TutorialManager.PendingFeeLessonTarget = null;
        var needFeeStep = TutorialDatabase.GetStep(TutorialDatabase.StepIds.NeedFee)
            ?? throw new InvalidOperationException("找不到 need_fee 教程步骤定义");

        var text = InvokeGetObjectiveText(needFeeStep);
        Assert(text == needFeeStep.DisplayObjective, "没有待处理目标卡牌时应该回退到通用文案");
    }

    private void TestObjectiveTextUnaffectedOnOtherSteps()
    {
        TutorialManager.PendingFeeLessonTarget = nameof(CardType.FireKill);
        var killStep = TutorialDatabase.GetStep(TutorialDatabase.StepIds.CardKill)
            ?? throw new InvalidOperationException("找不到 card_kill 教程步骤定义");

        var text = InvokeGetObjectiveText(killStep);
        Assert(text == killStep.DisplayObjective, "非NeedFee步骤不应该被这次改动影响");

        TutorialManager.PendingFeeLessonTarget = null;
    }

    private static string InvokeGetObjectiveText(TutorialStep step)
    {
        var method = typeof(TutorialOverlay).GetMethod(
            "GetObjectiveText",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("找不到 TutorialOverlay.GetObjectiveText，方法可能被重命名或删除");
        return (string)method.Invoke(null, new object[] { step })!;
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
