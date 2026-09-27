using Godot;
using System;

public partial class SettingsLayoutRegression : Node
{
    public override async void _Ready()
    {
        try
        {
            Localization.Initialize();
            var menu = new MainMenuController();
            AddChild(menu);
            menu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            SettingsPanelController? settings = null;
            foreach (var child in menu.GetChildren())
                if (child is SettingsPanelController found) settings = found;
            if (settings == null) throw new Exception("设置面板未挂载");
            settings.Show();
            foreach (var size in new[] { new Vector2(2560, 1440), new Vector2(1920, 1200) })
            {
                menu.Size = size;
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                var panel = settings.FindChild("SettingsContent", true, false) as PanelContainer;
                if (panel == null) throw new Exception("找不到设置内容面板");
                var center = panel.GetGlobalRect().GetCenter();
                if (center.DistanceTo(menu.GetGlobalRect().GetCenter()) > 1f)
                    throw new Exception($"设置未居中：{size}, {center}");
                var style = panel.GetThemeStylebox("panel") as StyleBoxFlat;
                if (style == null || style.BgColor.A != 1f)
                    throw new Exception("设置背景不完全不透明");
                GD.Print($"SETTINGS_LAYOUT_PASS size={size} center={center} alpha={style.BgColor.A}");
            }
            menu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            if (DisplayServer.GetName() != "headless")
            {
                await ToSignal(GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = GetViewport().GetTexture().GetImage();
                image.SavePng("/tmp/ftk-settings-preview.png");
            }
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }
}
