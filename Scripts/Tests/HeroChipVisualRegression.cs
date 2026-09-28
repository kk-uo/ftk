using Godot;
using System;
using System.Linq;

public partial class HeroChipVisualRegression : Node
{
    public override async void _Ready()
    {
        try
        {
            Localization.Initialize();
            Localization.SetLanguage("en_US");
            var select = new CharacterSelectController();
            AddChild(select);
            select.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            await ToSignal(GetTree().CreateTimer(.5), SceneTreeTimer.SignalName.Timeout);
            var chips = select.FindChildren("*", "Button", true, false).OfType<HeroChipButton>().ToArray();
            if (chips.Length != CharacterDatabase.GetAllCharacters().Count) throw new Exception("芯片数量错误");
            var lockedSeen = false;
            foreach (var chip in chips)
            {
                if (!chip.Unlocked) lockedSeen = true;
                else if (lockedSeen) throw new Exception("已解锁芯片排在未解锁之后");
            }
            var first = chips.First(c => c.Unlocked);
            first.EmitSignal(BaseButton.SignalName.Pressed);
            await ToSignal(GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
            if (DisplayServer.GetName() != "headless")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = GetViewport().GetTexture().GetImage();
                image.SavePng(ProjectSettings.GlobalizePath("res://Docs/Images/character-selection.png"));
            }
            GD.Print($"HERO_CHIPS_PASS count={chips.Length} unlocked={chips.Count(c=>c.Unlocked)}");
            GetTree().Quit();
        }
        catch(Exception exception) { GD.PushError(exception.ToString()); GetTree().Quit(1); }
    }
}
