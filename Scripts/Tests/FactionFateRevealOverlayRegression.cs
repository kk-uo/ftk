//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/FactionFateRevealOverlayRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证 FactionFateRevealOverlay 在 fastMode 下能正常走完
//    出现→滚动→定格→等待点击→退场的完整流程，不会卡死。
// 2. 验证点击前 _GuiInput 不会提前结束等待（定格阶段结束前点击无效）。
// 3. 验证 FactionFateDatabase.AllFates 已公开可读（阵营命运效果抽取演出
//    的滚动候选池，不建第二套独立数据）。
//
// 不负责：
// × 验证真实战斗场景里 BattleManager.ContinueBattleStartAfterFateReveal
//   的实际接入效果（角标位置/动画视觉效果等）——这部分仍需人工在 Godot
//   编辑器内实际进入一场真实（非调试模式）战斗验证。
//
// 主要依赖：
// FactionFateRevealOverlay
// FactionFateDatabase
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// 阵营命运效果抽取演出的 Headless 回归入口。
/// </summary>
public partial class FactionFateRevealOverlayRegression : Node
{
    private int _assertionCount;

    public override void _Ready()
    {
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        try
        {
            await Run();
            GD.Print($"FACTION_FATE_REVEAL_OVERLAY_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"FACTION_FATE_REVEAL_OVERLAY_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task Run()
    {
        Localization.Initialize();

        // 不写死具体数字：阵营命运池会持续增补新条目，这里只验证 AllFates 是
        // 四个阵营候选池的并集，任何一方新增/减少命运都会自动同步，不需要回来改这个断言。
        var expectedTotal = FactionFateDatabase.AllQunFates.Count
            + FactionFateDatabase.AllWeiFates.Count
            + FactionFateDatabase.AllShuFates.Count
            + FactionFateDatabase.AllWuFates.Count;
        Assert(FactionFateDatabase.AllFates.Count == expectedTotal,
            $"AllFates应为四个阵营候选池之和={expectedTotal}，实际={FactionFateDatabase.AllFates.Count}");
        Assert(FactionFateDatabase.AllFates.Count > 0, "阵营命运池不应为空");

        var names = new List<string>();
        foreach (var fate in FactionFateDatabase.AllFates)
        {
            names.Add(Localization.Get(fate.NameKey));
        }

        // 生产环境使用独立 CanvasLayer 承载屏幕级演出；测试保持同样层级，避免只验证
        // 动画流程、却漏掉“父级尺寸为零导致面板跑到左上角”的布局回归。
        var presentationLayer = new CanvasLayer();
        AddChild(presentationLayer);
        var presentationRoot = new Control();
        presentationLayer.AddChild(presentationRoot);
        presentationRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var overlay = new FactionFateRevealOverlay();
        presentationRoot.AddChild(overlay);

        var playTask = overlay.PlayAsync(
            names,
            "测试命运",
            "测试命运的详细说明文本。",
            "群",
            new Color(0.68f, 0.62f, 0.20f),
            "群",
            fastMode: true);

        // fastMode 下出现/滚动/定格阶段总耗时很短，但仍需要真实的若干帧才能跑完
        // （WaitSeconds 内部通过 SceneTreeTimer 让出控制权）；轮询 IsAwaitingDismissClick
        // 而不是猜测固定帧数，等真正进入"等待点击"阶段再验证点击前不会误触发完成。
        for (var i = 0; i < 300 && !overlay.IsAwaitingDismissClick && !playTask.IsCompleted; i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        Assert(overlay.IsAwaitingDismissClick, "动画应该能在合理帧数内推进到等待点击阶段");
        Assert(!playTask.IsCompleted, "定格阶段结束前，PlayAsync不应该在没有点击的情况下自行结束");

        var viewportRect = GetViewport().GetVisibleRect();
        var panel = overlay.GetNode<Panel>("RevealPanel");
        var dim = overlay.GetNode<ColorRect>("ScreenDim");
        Assert(panel.GetGlobalRect().GetCenter().DistanceTo(viewportRect.GetCenter()) < 1.0f,
            $"老虎机面板应位于视口中心：面板中心={panel.GetGlobalRect().GetCenter()}，视口中心={viewportRect.GetCenter()}");
        Assert(dim.GetGlobalRect().Size.DistanceTo(viewportRect.Size) < 1.0f,
            $"老虎机遮罩应完整覆盖视口：遮罩={dim.GetGlobalRect().Size}，视口={viewportRect.Size}");

        // 模拟玩家点击任意位置：直接调用 overlay 的 _GuiInput（与真实鼠标点击触发的
        // 回调路径一致），验证点击后动画能顺利收尾并让 PlayAsync 正常返回，不会卡死。
        // 注意：这里特意不用 GetViewport().PushInput 模拟真实点击传播路径——已经过
        // 独立探测确认，headless 模式下 PushInput 根本不会触发任何 Control 的
        // _GuiInput（哪怕是全屏 Stop 且没有子节点的最简单情形），这是 headless 模式
        // 本身的限制，不代表真实窗口环境下的行为，用它来测会产生假失败。子节点
        // MouseFilter 是否正确吞掉/放行点击这件事，只能靠人工在编辑器里实际点击验证。
        var clickEvent = new InputEventMouseButton
        {
            ButtonIndex = MouseButton.Left,
            Pressed = true
        };
        overlay._GuiInput(clickEvent);

        var completed = await Task.WhenAny(playTask, Task.Delay(3000)) == playTask;
        Assert(completed, "点击后 PlayAsync 应该在合理时间内结束，而不是卡死");
        Assert(playTask.IsCompletedSuccessfully, "PlayAsync 应该正常完成而不是抛出异常");

        presentationLayer.QueueFree();
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
