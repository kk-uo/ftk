//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/DebugBattleGameOverRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证调试战斗死亡后显示返回主菜单按钮。
// 2. 验证按钮使用调试模式对应的本地化文案。
// 3. 验证返回主菜单会清理本Run的行动牌移除状态。
//
// 不负责：
// × 验证死亡结算规则。
// × 验证按钮美术样式。
//
// 主要依赖：
// Battle.tscn
// BattleManager
// GameManager
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>
/// 调试战斗死亡返回流程的 Headless 回归入口。
/// </summary>
public partial class DebugBattleGameOverRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 启动调试战斗并验证死亡按钮及 Run 清理流程。
    /// </summary>
    public override async void _Ready()
    {
        try
        {
            Localization.Initialize();
            GameManager.BeginNewRun();
            GameManager.RemovePlayerCardType(CardType.FireKill);

            var battleScene = GD.Load<PackedScene>("res://Scenes/Battle.tscn");
            var battle = battleScene.Instantiate<BattleManager>();
            battle.ConfigureDebugMode(CharacterIds.DiaoChan);
            var returnedToMenu = false;
            battle.ReturnToMainMenuRequested += () => returnedToMenu = true;
            AddChild(battle);
            await NextFrame();
            await NextFrame();

            Invoke(battle, "DebugKillPlayer");
            await NextFrame();

            var overlay = battle.GetNode<CenterContainer>("GameOverOverlay");
            var content = overlay.GetNode<VBoxContainer>("GameOverContent");
            var button = content.GetChildren().OfType<Button>().Single();
            Assert(overlay.Visible, "调试战斗死亡后结算层没有显示");
            Assert(button.Visible, "调试战斗死亡后返回主菜单按钮仍被隐藏");
            Assert(
                button.Text == Localization.Get("battle.return_menu"),
                "调试战斗死亡按钮没有显示返回主菜单文案");

            button.EmitSignal(Button.SignalName.Pressed);
            await NextFrame();
            await NextFrame();

            Assert(returnedToMenu, "调试死亡按钮没有触发返回主菜单信号");
            Assert(
                !GameManager.IsPlayerCardTypeRemoved(CardType.FireKill),
                "调试死亡返回主菜单后仍保留行动牌移除状态");

            GD.Print($"DEBUG_BATTLE_GAME_OVER_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"DEBUG_BATTLE_GAME_OVER_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task NextFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static object? Invoke(object instance, string methodName, params object?[] arguments)
    {
        var method = instance.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(instance.GetType().Name, methodName);
        return method.Invoke(instance, arguments);
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
