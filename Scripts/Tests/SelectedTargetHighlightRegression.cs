//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/SelectedTargetHighlightRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证目标黄色高亮不会自行消失。
// 2. 验证切换目标时旧目标熄灭、新目标持续点亮。
// 3. 验证普通敌人与Boss共用同一组选中状态规则。
//
// 不负责：
// × 模拟完整战斗结算。
// × 验证Tooltip内容。
//
// 主要依赖：
// SelectionPresenter
// EnemyStatusUI
// BossStatusUI
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>
/// 战斗目标持续高亮的 Headless 回归入口。
/// </summary>
public partial class SelectedTargetHighlightRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 构建状态UI并验证选中、保持、切换和清理四个阶段。
    /// </summary>
    public override async void _Ready()
    {
        try
        {
            var first = CreateEnemy("highlight_first");
            var second = CreateEnemy("highlight_second");
            var firstUi = new EnemyStatusUI();
            var secondUi = new EnemyStatusUI();
            var bossUi = new BossStatusUI();
            AddChild(firstUi);
            AddChild(secondUi);
            AddChild(bossUi);
            firstUi.Refresh(first, null);
            secondUi.Refresh(second, null);
            bossUi.Refresh(second, null);

            var presenter = new SelectionPresenter();
            presenter.HighlightEnemy(first, firstUi);
            await WaitForTween();
            Assert(GetHighlightAlpha(firstUi) > 0.99f, "普通敌人选中高亮没有点亮");

            await WaitForTween();
            Assert(GetHighlightAlpha(firstUi) > 0.99f, "普通敌人黄色高亮会随时间自行消失");

            presenter.HighlightEnemy(second, secondUi);
            await WaitForTween();
            Assert(GetHighlightAlpha(firstUi) < 0.01f, "切换普通敌人后旧目标高亮没有清除");
            Assert(GetHighlightAlpha(secondUi) > 0.99f, "切换普通敌人后新目标没有持续高亮");

            presenter.HighlightBoss(second, bossUi);
            await WaitForTween();
            Assert(GetHighlightAlpha(secondUi) < 0.01f, "切换Boss后普通敌人高亮没有清除");
            Assert(GetHighlightAlpha(bossUi) > 0.99f, "Boss选中高亮没有持续点亮");

            presenter.ClearAll();
            await WaitForTween();
            Assert(GetHighlightAlpha(bossUi) < 0.01f, "无合法目标时Boss高亮没有清除");

            GD.Print($"SELECTED_TARGET_HIGHLIGHT_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"SELECTED_TARGET_HIGHLIGHT_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task WaitForTween()
    {
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
    }

    private static EnemyInstance CreateEnemy(string id)
    {
        return new EnemyInstance(new EnemyDefinition
        {
            Id = id,
            Name = id,
            MaxHP = 20,
            Type = EnemyType.Normal,
            StartingResource = 1,
            StartingDeck = new EnemyDeck()
        });
    }

    private static float GetHighlightAlpha(object statusUi)
    {
        var field = statusUi.GetType().GetField(
            "_highlightAlpha",
            BindingFlags.Instance | BindingFlags.NonPublic);
        return field?.GetValue(statusUi) is float value ? value : -1f;
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
