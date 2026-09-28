using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public partial class InitialFateVisualRegression : Node
{
    public override async void _Ready()
    {
        try
        {
            Localization.Initialize();
            Localization.SetLanguage("en_US");
            var screen = new InitialEventController();
            AddChild(screen);
            screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            var optionsField = typeof(InitialEventController).GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var options = (List<InitialEventDefinition>)optionsField.GetValue(screen)!;
            var ids = options.Select(o => o.Id).ToArray();
            typeof(InitialEventController).GetMethod("RefreshLanguage", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(screen, null);
            await ToSignal(GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
            var buttons = screen.FindChildren("*", "Button", true, false).OfType<FateSelectButton>().ToArray();
            if (buttons.Length != 3) throw new Exception("Expected three fate choices");
            if (!ids.SequenceEqual(((List<InitialEventDefinition>)optionsField.GetValue(screen)!).Select(o => o.Id)))
                throw new Exception("Refreshing presentation rerolled choices");
            var background = screen.GetChildren().OfType<TextureRect>().Single();
            if (background.Texture == null) throw new Exception("Missing background");
            var row = screen.GetChildren().OfType<HBoxContainer>().Single();
            if (row.GetGlobalRect().GetCenter().DistanceTo(screen.GetGlobalRect().GetCenter()) > 1f)
                throw new Exception("Fate choices are not centered in the viewport");
            foreach (var button in buttons)
                if (!screen.GetGlobalRect().Encloses(button.GetGlobalRect())) throw new Exception("Choice button clipped");
            if (DisplayServer.GetName() != "headless")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = GetViewport().GetTexture().GetImage();
                image.SavePng(ProjectSettings.GlobalizePath("res://Docs/Images/initial-fate.png"));
            }
            GD.Print("INITIAL_FATE_VISUAL_PASS");
            GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError(exception.ToString()); GetTree().Quit(1); }
    }
}
