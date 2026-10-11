using Godot;
using System.Collections.Generic;

/// <summary>
/// 第四章深渊敌人的立绘适配器。
/// 生成原稿不直接参与战场布局，而是先归并到统一的低分辨率逻辑画布，
/// 以保证多敌人编队下的比例与像素密度一致。
/// </summary>
public static class ChapterFourPortraitTextures
{
    public const string Scout = "res://Assets/Enemies/BattleModels/Generated/ch4_scout_abyss_pixel_v1.png";
    public const string GiantMaw = "res://Assets/Enemies/BattleModels/Generated/ch4_giant_maw_abyss_pixel_v1.png";
    public const string AbyssSymbiote = "res://Assets/Enemies/BattleModels/Generated/ch4_abyss_symbiote_pixel_v1.png";
    public const string PhantomTentacle = "res://Assets/Bosses/BattlePortraits/Generated/ch4_phantom_tentacle_pixel_v1.png";

    private static readonly Dictionary<string, Texture2D> Cache = new();

    public static bool IsAbyssPortrait(string path) =>
        path == Scout || path == GiantMaw || path == AbyssSymbiote || path == PhantomTentacle;

    public static Texture2D? Load(string path)
    {
        if (Cache.TryGetValue(path, out var cached))
            return cached;

        var source = GD.Load<Texture2D>(path);
        if (source == null)
            return null;

        using var image = source.GetImage();
        if (image.IsCompressed())
            image.Decompress();
        image.Convert(Image.Format.Rgba8);

        var isBoss = path == PhantomTentacle;
        var width = isBoss ? 256 : 128;
        var height = isBoss ? 192 : 160;
        // 先等比归并到一半逻辑分辨率、再放大，避免高精原稿在缩放后变成细碎插画。
        var lowWidth = width / 2;
        var lowHeight = height / 2;
        var scale = Mathf.Min((float)lowWidth / image.GetWidth(), (float)lowHeight / image.GetHeight());
        image.Resize(Mathf.Max(1, Mathf.RoundToInt(image.GetWidth() * scale)),
            Mathf.Max(1, Mathf.RoundToInt(image.GetHeight() * scale)), Image.Interpolation.Nearest);
        using var lowCanvas = Image.CreateEmpty(lowWidth, lowHeight, false, Image.Format.Rgba8);
        lowCanvas.Fill(Colors.Transparent);
        lowCanvas.BlitRect(image, new Rect2I(Vector2I.Zero, image.GetSize()),
            new Vector2I((lowWidth - image.GetWidth()) / 2, (lowHeight - image.GetHeight()) / 2));
        lowCanvas.Resize(width, height, Image.Interpolation.Nearest);

        var result = ImageTexture.CreateFromImage(lowCanvas);
        Cache[path] = result;
        return result;
    }
}
