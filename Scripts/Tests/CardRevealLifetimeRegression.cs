//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/CardRevealLifetimeRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证战斗出牌结果不再按固定时间自动消失。
// 2. 验证下一轮行动正式确认后清除上一轮结果。
// 3. 验证清除旧结果后仍能正常显示本轮新结果。
//
// 不负责：
// × 验证卡牌伤害规则。
// × 验证结果文字的美术样式。
//
// 主要依赖：
// Battle.tscn
// BattleManager
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>
/// 战斗出牌结果生命周期的 Headless 回归入口。
/// </summary>
public partial class CardRevealLifetimeRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 启动战斗场景并验证“持续显示→下一次出招替换”的完整生命周期。
    /// </summary>
    public override async void _Ready()
    {
        try
        {
            Localization.Initialize();
            var battleScene = GD.Load<PackedScene>("res://Scenes/Battle.tscn");
            var battle = battleScene.Instantiate<BattleManager>();
            battle.ConfigureDebugMode(CharacterIds.ZhaoYun);
            AddChild(battle);
            await NextFrame();
            await NextFrame();

            var previousAction = BattleAction.FromCard(Card.Kill(), 1);
            Invoke(
                battle,
                "ShowCardRevealPopup",
                previousAction,
                new List<EnemyActionEntry>());

            var previousNodes = GetRevealNodes(battle);
            Assert(previousNodes.Count == 1, "测试用上一轮结果没有创建");
            var previousNode = previousNodes[0];

            await ToSignal(GetTree().CreateTimer(1.9), SceneTreeTimer.SignalName.Timeout);
            Assert(
                GodotObject.IsInstanceValid(previousNode) && previousNode.Visible,
                "战斗结果仍按旧的固定时间自动消失");
            Assert(previousNode.Modulate.A > 0.99f, "战斗结果在等待下一轮出招时错误淡出");

            var runId = GetPrivateField<int>(battle, "_battleRunId");
            var resolveTask = (Task?)Invoke(
                battle,
                "ResolvePlayerAction",
                BattleAction.FromCard(Card.Fee(), 1),
                null,
                runId);
            Assert(resolveTask != null, "无法执行下一轮测试行动");
            await resolveTask!;
            await NextFrame();

            Assert(
                !GodotObject.IsInstanceValid(previousNode) || !previousNode.Visible,
                "下一轮正式出招后上一轮结果仍未消失");
            var currentNodes = GetRevealNodes(battle);
            Assert(currentNodes.Count > 0, "清除上一轮结果后没有显示本轮新结果");
            Assert(!currentNodes.Contains(previousNode), "新旧两轮结果错误复用了同一个显示节点");

            GD.Print($"CARD_REVEAL_LIFETIME_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"CARD_REVEAL_LIFETIME_TEST_FAIL {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task NextFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static List<Control> GetRevealNodes(BattleManager battle)
    {
        return GetPrivateField<List<Control>>(battle, "_activeCardRevealNodes");
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingFieldException(instance.GetType().Name, fieldName);
        return (T)field.GetValue(instance)!;
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
