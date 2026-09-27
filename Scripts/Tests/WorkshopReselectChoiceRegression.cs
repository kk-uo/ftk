//////////////////////////////////////////////////////////
// 文件：Scripts/Tests/WorkshopReselectChoiceRegression.cs
//
// 模块：Regression Tests
//
// 职责：
// 1. 验证工坊重选使用覆盖式 ChoicePanel，不销毁当前流程页面。
// 2. 验证点击候选后弹层关闭，并获得一件非原装备的同品质替换装备。
//
// 不负责：
// × 验证 ChoicePanel 的美术布局。
// × 验证装备效果的战斗结算。
//
// 主要依赖：
// MainFlow
// FactionFateManager
// InventoryManager
//////////////////////////////////////////////////////////

using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// 工坊重选模态选择流程的 Headless 回归入口。
/// </summary>
public partial class WorkshopReselectChoiceRegression : Node
{
    private int _assertionCount;

    /// <summary>
    /// 创建真实 MainFlow，并验证工坊重选点击后的完整页面恢复流程。
    /// </summary>
    public override void _Ready()
    {
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        try
        {
            var mainScene = GD.Load<PackedScene>("res://Scenes/Main.tscn");
            var main = mainScene.Instantiate<MainFlow>();
            AddChild(main);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            var sceneHost = main.GetNode<Control>("SceneHost");
            Assert(sceneHost.GetChildCount() == 1, "MainFlow没有创建初始页面");
            var originalPage = sceneHost.GetChild(0);

            GameManager.BeginNewRun();
            FactionFateManager.DebugForceFate(FactionFateIds.WuEquipmentReselect);
            GameManager.AddEquipment(EquipmentIds.RustSword);

            var choicePanel = main.GetNodeOrNull<ChoicePanel>("FactionFateChoicePanel");
            Assert(choicePanel != null, "获得合法装备后没有显示工坊重选弹层");
            Assert(sceneHost.GetChildCount() == 1 && sceneHost.GetChild(0) == originalPage,
                "工坊重选错误替换或销毁了当前流程页面");
            Assert(!InventoryManager.GetAllOwned().Any(item => item.Definition.Id == EquipmentIds.RustSword),
                "工坊重选弹层显示后原装备没有被销毁");

            var optionButton = choicePanel!
                .FindChildren("*", nameof(Button), true, false)
                .OfType<Button>()
                .FirstOrDefault();
            Assert(optionButton != null, "工坊重选没有可点击的候选按钮");
            optionButton!.EmitSignal(Button.SignalName.Pressed);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

            Assert(main.GetNodeOrNull<ChoicePanel>("FactionFateChoicePanel") == null,
                "点击工坊重选候选后弹层没有关闭");
            Assert(sceneHost.GetChildCount() == 1 && sceneHost.GetChild(0) == originalPage,
                "工坊重选完成后没有保留原流程页面");
            Assert(InventoryManager.GetAllOwned().Count == 1, "工坊重选完成后背包装备数量不为1");
            Assert(InventoryManager.GetAllOwned()[0].Definition.Id != EquipmentIds.RustSword,
                "工坊重选错误返还了原装备");

            GD.Print($"WORKSHOP_RESELECT_CHOICE_TEST_PASS assertions={_assertionCount}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"WORKSHOP_RESELECT_CHOICE_TEST_FAIL {exception}");
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
