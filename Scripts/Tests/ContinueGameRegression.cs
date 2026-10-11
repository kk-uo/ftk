using Godot;
using System;
using System.Reflection;

public partial class ContinueGameRegression : Node
{
    public override async void _Ready()
    {
        MainFlow? flow = null;
        try
        {
            Localization.Initialize();
            var host = new Control();
            AddChild(host);
            var saved = new Control();
            saved.SetMeta("regression_hp", 23);
            host.AddChild(saved);
            var statusLayer = new CanvasLayer { Name = "PlayerStatusLayer" };
            saved.AddChild(statusLayer);
            var nestedLayer = new CanvasLayer { Visible = false };
            statusLayer.AddChild(nestedLayer);
            // 不初始化整个游戏，直接验证场景暂存/菜单生命周期，避免改动玩家存档。
            flow = new MainFlow();
            Set(flow, "_sceneHost", host);
            Call(flow, "HideSuspendedScreenPresentation", saved);
            saved.Hide();
            saved.ProcessMode = ProcessModeEnum.Disabled;
            Check(!statusLayer.Visible && !nestedLayer.Visible, "暂存后独立角色/演出层仍然可见");
            Set(flow, "_suspendedRunScreen", saved);
            var menu = new MainMenuController();
            menu.SetContinueAvailable(true);
            menu.ContinueGameRequested += () => Call(flow, "ResumeSuspendedRun");
            Call(flow, "SwitchTo", menu);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var button = (Button)Get(menu, "_continueButton")!;
            Check(!button.Disabled, "挂载前传入的继续状态丢失");
            menu.SetContinueAvailable(false);
            Check(button.Disabled, "禁用状态没有更新");
            menu.SetContinueAvailable(true);
            Call(flow, "SwitchTo", new Control());
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Check(IsInstanceValid(saved) && saved.GetParent() == host, "浏览菜单释放了暂存场景");
            var returnMenu = new MainMenuController();
            returnMenu.SetContinueAvailable(true);
            returnMenu.ContinueGameRequested += () => Call(flow, "ResumeSuspendedRun");
            Call(flow, "SwitchTo", returnMenu);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            ((Button)Get(returnMenu, "_continueButton")!).EmitSignal(BaseButton.SignalName.Pressed);
            Check(saved.Visible && saved.ProcessMode == ProcessModeEnum.Inherit, "没有恢复场景显示和处理");
            Check(statusLayer.Visible && !nestedLayer.Visible, "没有按原可见性恢复独立图层");
            Check(saved.GetMeta("regression_hp").AsInt32() == 23, "恢复后状态改变");
            Check(Get(flow, "_suspendedRunScreen") == null, "暂存引用未清除");
            Check(host.GetChildCount() == 1, "恢复后菜单未移除");
            // 对真实战斗场景也验证，避免测试夹具遗漏后来增加的独立图层。
            using var battleScene = GD.Load<PackedScene>("res://Scenes/Battle.tscn");
            var battle = battleScene.Instantiate<BattleManager>();
            var playerLayer = battle.GetNode<CanvasLayer>("PlayerStatusLayer");
            var skillLayer = battle.GetNode<CanvasLayer>("SkillTriggerPresentationLayer");
            skillLayer.Hide();
            Call(flow, "HideSuspendedScreenPresentation", battle);
            Check(!playerLayer.Visible && !skillLayer.Visible, "真实战斗场景图层未隐藏");
            Call(flow, "RestoreSuspendedScreenPresentation");
            Check(playerLayer.Visible && !skillLayer.Visible, "真实战斗场景图层恢复状态错误");
            battle.Free();
            GD.Print("CONTINUE_GAME_REGRESSION_PASS");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
        finally { flow?.Free(); }
    }
    private static object? Get(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
