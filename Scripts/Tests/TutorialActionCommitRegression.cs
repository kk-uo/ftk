//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/TutorialActionCommitRegression.cs
//
// 职责：验证教程仅在行动确认并进入真实结算后推进，不能在卡牌点击、
// 堆叠确认窗口尚未结束时提前切换步骤并锁死输入。
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;
using System.Reflection;

/// <summary>
/// 教程行动提交时机的 Headless 回归入口。
/// </summary>
public partial class TutorialActionCommitRegression : Node
{
    private int _assertionCount;
    private TutorialController? _controller;

    public override async void _Ready()
    {
        BattleManager? battle = null;
        try
        {
            Localization.Initialize();
            TutorialManager.ResetForNewRun();
            TutorialManager.StartCombatTutorial();

            var battleScene = GD.Load<PackedScene>("res://Scenes/Battle.tscn");
            battle = battleScene.Instantiate<BattleManager>();
            battle.ConfigureDebugMode(CharacterIds.ZhaoYun);
            AddChild(battle);
            await NextFrame();
            await NextFrame();

            _controller = new TutorialController(battle);
            var stealSlot = GetActionSlots(battle)
                .FirstOrDefault(slot => slot.Card?.Type == CardType.Steal);
            Assert(stealSlot == null,
                "顺手牵羊已移出初始牌组，教程不应再临时提供该教学卡牌");

            TutorialManager.JumpToStep(TutorialDatabase.StepIds.CounterRoundC);
            _controller.Tick();
            await NextFrame();

            var thunderSlot = GetActionSlots(battle)
                .FirstOrDefault(slot => slot.Card?.Type == CardType.ThunderKill);
            Assert(thunderSlot != null, "行动栏中没有可用的雷杀");

            thunderSlot!.Execute();
            Assert(
                TutorialManager.CurrentStep?.Id == TutorialDatabase.StepIds.CounterRoundC,
                "点击仍在确认窗口的雷杀时，教程不应提前进入总结步骤");

            await ToSignal(GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
            Assert(
                TutorialManager.CurrentStep?.Id == TutorialDatabase.StepIds.CounterSummary,
                "雷杀正式确认并开始结算后，教程应进入总结步骤");

            GD.Print($"TUTORIAL_ACTION_COMMIT_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"TUTORIAL_ACTION_COMMIT_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
        finally
        {
            _controller?.Shutdown();
            TutorialManager.ResetForNewRun();
            battle?.QueueFree();
        }
    }

    private async System.Threading.Tasks.Task NextFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static ActionSlot[] GetActionSlots(BattleManager battle)
    {
        var field = battle.GetType().GetField(
            "_actionSlots",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(battle.GetType().Name, "_actionSlots");
        return (ActionSlot[])field.GetValue(battle)!;
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
