//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/BattleReadBarLayoutRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证所有战斗读条固定在出牌区中央上方。
// 2. 验证不同战场尺寸不会改变读条的底部间距。
//
// 不负责：
// × 验证读条计时逻辑。
// × 验证卡牌结算。
//
// 主要依赖：
// BattleReadBarLayout
// Godot Control
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// 战斗读条固定布局的 Headless 回归入口。
/// </summary>
public partial class BattleReadBarLayoutRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 执行布局测试并通过进程退出码返回结果。
    /// </summary>
    public override async void _Ready()
    {
        try
        {
            await VerifyLayoutAsync(new Vector2(1920, 780));
            await VerifyLayoutAsync(new Vector2(2560, 1080));
            await VerifyLayoutAsync(new Vector2(1600, 900));
            GD.Print($"BATTLE_READ_BAR_LAYOUT_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"BATTLE_READ_BAR_LAYOUT_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task VerifyLayoutAsync(Vector2 parentSize)
    {
        var host = new Control
        {
            Size = parentSize
        };
        AddChild(host);

        var selectionBar = new PanelContainer();
        host.AddChild(selectionBar);
        BattleReadBarLayout.PlaceAboveActionArea(selectionBar, new Vector2(480, 72));

        var reactionBar = new PanelContainer();
        host.AddChild(reactionBar);
        BattleReadBarLayout.PlaceAboveActionArea(reactionBar, new Vector2(760, 172));

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        AssertPlacement(selectionBar, parentSize, new Vector2(480, 72), "确认读条");
        AssertPlacement(reactionBar, parentSize, new Vector2(760, 172), "反应读条");
        host.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void AssertPlacement(Control control, Vector2 parentSize, Vector2 expectedSize, string context)
    {
        Assert(NearlyEqual(control.AnchorLeft, 0.5f), $"{context}: 左锚点未居中");
        Assert(NearlyEqual(control.AnchorRight, 0.5f), $"{context}: 右锚点未居中");
        Assert(NearlyEqual(control.AnchorTop, 1.0f), $"{context}: 顶部未锚定出牌区上沿");
        Assert(NearlyEqual(control.AnchorBottom, 1.0f), $"{context}: 底部未锚定出牌区上沿");
        Assert(NearlyEqual(control.Position.X + expectedSize.X * 0.5f, parentSize.X * 0.5f), $"{context}: 水平位置未居中");
        Assert(NearlyEqual(control.Position.Y + expectedSize.Y, parentSize.Y - BattleReadBarLayout.BottomGap), $"{context}: 与出牌区间距错误");
        Assert(NearlyEqual(control.Size.X, expectedSize.X), $"{context}: 宽度错误");
        Assert(NearlyEqual(control.Size.Y, expectedSize.Y), $"{context}: 高度错误");
    }

    private void Assert(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static bool NearlyEqual(float left, float right)
    {
        return Mathf.Abs(left - right) <= 0.001f;
    }
}
