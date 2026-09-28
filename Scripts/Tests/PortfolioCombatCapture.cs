using Godot;
using System;

/// <summary>
/// Captures the real battle scene for the repository portfolio.
/// This scene intentionally uses the standard BattleManager setup rather than a mock UI.
/// </summary>
public partial class PortfolioCombatCapture : Node
{
    public override async void _Ready()
    {
        try
        {
            Localization.Initialize();
            Localization.SetLanguage("en_US");

            var battleScene = GD.Load<PackedScene>("res://Scenes/Battle.tscn")
                ?? throw new InvalidOperationException("Battle scene could not be loaded.");
            var battle = battleScene.Instantiate<BattleManager>();
            // Debug setup supplies the ordinary gate-guard encounter used by the
            // live battle scene; the capture itself shows the normal combat HUD.
            battle.ConfigureDebugMode(CharacterIds.ZhaoYun);
            AddChild(battle);
            battle.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            await ToSignal(GetTree().CreateTimer(1.0), SceneTreeTimer.SignalName.Timeout);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

            using var image = GetViewport().GetTexture().GetImage();
            var outputPath = ProjectSettings.GlobalizePath("res://Docs/Images/combat-system.png");
            var result = image.SavePng(outputPath);
            if (result != Error.Ok)
                throw new InvalidOperationException($"Unable to save combat screenshot: {result}");

            GD.Print($"PORTFOLIO_COMBAT_CAPTURE_PASS path={outputPath}");
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"PORTFOLIO_COMBAT_CAPTURE_FAIL {exception}");
            GetTree().Quit(1);
        }
    }
}
