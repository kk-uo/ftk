using Godot;
using System.Collections.Generic;

/// <summary>将已批准的高清像素原稿按 Nearest 映射到精英逻辑画布，避免直接加载原稿撑爆战场。</summary>
public static class ElitePortraitTextures
{
    public const string Collector = "res://Assets/Enemies/BattleModels/Generated/corpse_collector_v2.png";
    public const string Pursuer = "res://Assets/Enemies/BattleModels/Generated/pursuer.png";
    private static readonly Dictionary<string, Texture2D> Cache = new();

    public static bool IsElitePortrait(string path) => path == Collector || path == Pursuer;

    public static Texture2D? Load(string path)
    {
        if (Cache.TryGetValue(path, out var cached)) return cached;
        var source = GD.Load<Texture2D>(path);
        if (source == null) return null;
        using var image = source.GetImage();
        if (image.IsCompressed()) image.Decompress();
        image.Convert(Image.Format.Rgba8);
        const int width = 128;
        const int height = 160;
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
