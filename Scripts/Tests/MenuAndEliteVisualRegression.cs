using Godot;
using System;
using System.Collections;
using System.Reflection;

public partial class MenuAndEliteVisualRegression : Node
{
    public override async void _Ready()
    {
        try
        {
            Localization.Initialize();
            var menu = new MainMenuController();
            AddChild(menu);
            menu.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            await ToSignal(GetTree().CreateTimer(.6), SceneTreeTimer.SignalName.Timeout);
            var filter = menu.GetNode<MainMenuCrtFilter>("BackgroundCrtFilter");
            if (filter.MouseFilter != Control.MouseFilterEnum.Ignore || filter.Size != menu.Size)
                throw new Exception("背景滤镜布局/输入不正确");
            await Capture("/tmp/ftk-crt-preview.png");
            menu.Hide();
            var board = new ColorRect { Color = new Color(.06f, .075f, .09f) };
            AddChild(board);
            board.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var mappings = (IDictionary)typeof(BattleManager).GetField("TemporaryEnemyStageVisualsByEnemyId",
                BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            var paths = new[] { ElitePortraitTextures.Collector, ElitePortraitTextures.Pursuer };
            var ids = new[] { "corpse_collector", "hunter" };
            for (var i = 0; i < paths.Length; i++)
            {
                var entry = mappings[ids[i]]!;
                if ((string)entry.GetType().GetProperty("TexturePath")!.GetValue(entry)! != paths[i])
                    throw new Exception("战斗立绘映射仍然指向旧资源");
                var texture = ElitePortraitTextures.Load(paths[i])!;
                using var image = texture.GetImage();
                if (texture.GetSize() != new Vector2(128, 160) || image.GetPixel(0, 0).A != 0)
                    throw new Exception("精英画布尺寸/透明通道错误");
                board.AddChild(new Sprite2D
                {
                    Texture = texture, TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                    Position = new Vector2(850 + i * 800, 720), Scale = new Vector2(4, 4)
                });
                GD.Print($"ELITE_VISUAL_PASS id={ids[i]} size={texture.GetSize()} path={paths[i]}");
            }
            await Capture("/tmp/ftk-elites-preview.png");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError(exception.ToString());
            GetTree().Quit(1);
        }
    }

    private async System.Threading.Tasks.Task Capture(string path)
    {
        if (DisplayServer.GetName() == "headless") return;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        image.SavePng(path);
    }
}
