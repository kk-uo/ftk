using Godot;
using System.Collections.Generic;

/// <summary>第一章重制 Boss 直接使用生成原稿，统一为 256×192 像素逻辑画布。
/// 避免新原稿与旧 Runtime 副本不同步；不改动战斗布局或敌人定义。</summary>
public static class BossPortraitTextures
{
    public const string Healer = "res://Assets/Bosses/BattlePortraits/Generated/boss_healer_pixel_v2.png";
    // 用户指定的 v13：吕布前列持戟、貂蝉后方进行数据供能，武器已包含在主体中。
    public const string Traitor = "res://Assets/Bosses/BattlePortraits/Generated/boss_traitor_supplied_pixel_v13.png";
    public const string Performer = "res://Assets/Bosses/BattlePortraits/Generated/crazy_performer_robot_v3.png";
    public const string Halberd = "res://Assets/Bosses/BattlePortraits/Generated/traitor_halberd_v3.png";
    public static bool TraitorHasEmbeddedHalberd => true;
    private static readonly Dictionary<string, Texture2D> Cache = new();

    public static bool IsBossPortrait(string path) => path == Healer || path == Traitor || path == Performer;

    public static Texture2D? Load(string path)
    {
        if (Cache.TryGetValue(path, out var cached)) return cached;
        var source = GD.Load<Texture2D>(path);
        if (source == null) return null;
        using var image = source.GetImage();
        if (image.IsCompressed()) image.Decompress();
        image.Convert(Image.Format.Rgba8);
        // 背叛者先归并到 128×96 的逻辑像素画布，再用 Nearest 放大到战斗画布。
        // 这避免高精原画仅缩小后仍显得过于平滑、细碎。
        if (path == Traitor)
            image.Resize(128, 96, Image.Interpolation.Nearest);
        var width = path == Halberd ? 64 : 256;
        const int height = 192;
        var scale = Mathf.Min((float)width / image.GetWidth(), (float)height / image.GetHeight());
        image.Resize(Mathf.Max(1, Mathf.RoundToInt(image.GetWidth() * scale)),
            Mathf.Max(1, Mathf.RoundToInt(image.GetHeight() * scale)), Image.Interpolation.Nearest);
        using var canvas = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        canvas.Fill(Colors.Transparent);
        canvas.BlitRect(image, new Rect2I(Vector2I.Zero, image.GetSize()),
            new Vector2I((width - image.GetWidth()) / 2, height - image.GetHeight()));
        var result = ImageTexture.CreateFromImage(canvas);
        Cache[path] = result;
        return result;
    }
}
