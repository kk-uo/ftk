using Godot;
using System;
using System.Collections;
using System.Reflection;

public partial class ChapterOneBossPortraitRegression : Node
{
    public override async void _Ready()
    {
        try
        {
            var board = new ColorRect { Color = new Color(.06f, .075f, .09f) };
            AddChild(board);
            board.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var mappings = (IDictionary)typeof(BattleManager).GetField("TemporaryEnemyStageVisualsByEnemyId",
                BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            var loader = typeof(BattleManager).GetMethod("LoadEnemyStageTexture", BindingFlags.NonPublic | BindingFlags.Static)!;
            var ids = new[] { "boss_healer", "boss_traitor", "crazy_performer" };
            var paths = new[] { BossPortraitTextures.Healer, BossPortraitTextures.Traitor, BossPortraitTextures.Performer };
            var names = new[] { "医者", "背叛者", "失疯卖艺人" };
            TraitorWeaponVisual? weapon = null;
            Sprite2D? traitorBody = null;
            for (var i = 0; i < ids.Length; i++)
            {
                var entry = mappings[ids[i]]!;
                if ((string)entry.GetType().GetProperty("TexturePath")!.GetValue(entry)! != paths[i])
                    throw new Exception("Boss still uses old portrait: " + ids[i]);
                var texture = (Texture2D?)loader.Invoke(null, new object[] { paths[i] })
                    ?? throw new Exception("Missing portrait: " + paths[i]);
                using var image = texture.GetImage();
                if (texture.GetSize() != new Vector2(256, 192) || image.GetPixel(0, 0).A != 0 || image.GetUsedRect().Size.X < 30)
                    throw new Exception("Invalid size, transparency or empty sprite: " + ids[i]);
                if (BossPortraitTextures.Load(paths[i]) != texture) throw new Exception("Texture cache not reused");
                var body = new Sprite2D
                {
                    Texture = texture, TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
                    Centered = false,
                    Position = new Vector2(76 + i * 820, 432), Scale = new Vector2(3, 3)
                };
                board.AddChild(body);
                if (ids[i] == "boss_traitor" && !BossPortraitTextures.TraitorHasEmbeddedHalberd)
                {
                    traitorBody = body;
                    weapon = new TraitorWeaponVisual();
                    body.AddChild(weapon);
                }
                var label = new Label { Text = names[i], Position = new Vector2(160 + i * 820, 1050),
                    Size = new Vector2(600, 80), HorizontalAlignment = HorizontalAlignment.Center };
                label.AddThemeFontOverride("font", GD.Load<Font>("res://Assets/Fonts/NotoSerifCJKsc-Black.otf"));
                label.AddThemeFontSizeOverride("font_size", 40);
                board.AddChild(label);
                GD.Print($"BOSS_PORTRAIT_PASS id={ids[i]} size={texture.GetSize()}");
            }
            if (DisplayServer.GetName() != "headless")
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var screenshot = GetViewport().GetTexture().GetImage();
                screenshot.SavePng("/tmp/ftk-chapter-one-bosses.png");
            }
            if (!BossPortraitTextures.TraitorHasEmbeddedHalberd)
            {
                var halberd = BossPortraitTextures.Load(BossPortraitTextures.Halberd);
                if (halberd == null || halberd.GetSize() != new Vector2(64, 192))
                    throw new Exception("Missing independent weapon texture");
                weapon!.PlayAttack();
                await ToSignal(GetTree().CreateTimer(.17), SceneTreeTimer.SignalName.Timeout);
                if (!weapon.IsSwinging || Mathf.IsEqualApprox(weapon.RotationDegrees, TraitorWeaponVisual.RestAngle)
                    || traitorBody!.Rotation != 0)
                    throw new Exception("Weapon did not swing independently of the carried character");
                weapon.PlayAttack();
                await ToSignal(GetTree().CreateTimer(.55), SceneTreeTimer.SignalName.Timeout);
                if (weapon.IsSwinging || !Mathf.IsEqualApprox(weapon.RotationDegrees, TraitorWeaponVisual.RestAngle))
                    throw new Exception("Weapon did not recover to its grip pose");
            }
            GetTree().Quit();
        }
        catch (Exception exception) { GD.PushError(exception.ToString()); GetTree().Quit(1); }
    }
}
