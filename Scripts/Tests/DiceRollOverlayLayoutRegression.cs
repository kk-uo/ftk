//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/DiceRollOverlayLayoutRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证 DiceRollOverlay 不受调用方局部 Control 尺寸与位置影响。
// 2. 验证骰子面板始终位于视口中心，遮罩始终覆盖整个视口。
//
// 不负责：
// × 验证骰子业务结果与奖励结算。
// × 验证动画美术效果。
//
// 主要依赖：
// DiceRollOverlay
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// 天命骰屏幕级布局的 Headless 回归入口。
/// </summary>
public partial class DiceRollOverlayLayoutRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 启动异步布局回归测试。
    /// </summary>
    public override void _Ready()
    {
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        try
        {
            var localHost = new Control
            {
                Position = new Vector2(37, 53),
                Size = new Vector2(240, 180)
            };
            AddChild(localHost);

            // 故意挂到一个偏移且很小的局部控件下；CanvasLayer 必须让动画仍以视口为准。
            var overlay = new DiceRollOverlay();
            localHost.AddChild(overlay);
            var playTask = overlay.PlayAsync(
                6,
                true,
                "测试成功",
                "测试说明",
                "测试失败",
                "测试说明",
                fastMode: true);

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var viewportRect = GetViewport().GetVisibleRect();
            var screenRoot = overlay.GetNode<Control>("ScreenRoot");
            var panel = overlay.GetNode<Panel>("ScreenRoot/DicePanel");
            var dim = overlay.GetNode<ColorRect>("ScreenRoot/ScreenDim");

            Assert(screenRoot.GetGlobalRect().Size.DistanceTo(viewportRect.Size) < 1.0f,
                $"屏幕根节点应覆盖视口：root={screenRoot.GetGlobalRect().Size} viewport={viewportRect.Size}");
            Assert(dim.GetGlobalRect().Size.DistanceTo(viewportRect.Size) < 1.0f,
                $"遮罩应覆盖视口：dim={dim.GetGlobalRect().Size} viewport={viewportRect.Size}");
            Assert(panel.GetGlobalRect().GetCenter().DistanceTo(viewportRect.GetCenter()) < 1.0f,
                $"骰子面板应位于视口中心：panel={panel.GetGlobalRect().GetCenter()} viewport={viewportRect.GetCenter()}");

            await playTask;
            overlay.QueueFree();

            GD.Print($"DICE_ROLL_OVERLAY_LAYOUT_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"DICE_ROLL_OVERLAY_LAYOUT_TEST_FAIL {exception}");
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
