//////////////////////////////////////////////////////////
// 文件：Scripts/UI/IconLibrary.cs
//
// 模块：UI System / UI Icon System
//
// 为什么存在：
// 所有 UI 图标最终都要从 SpriteDatabase 查询到 Texture2D，但查不到的时候
// （美术资源还没做、Id 拼错、以后新增内容忘了注册图标）不能让界面出现
// 报错或者一块空白——需要一个统一的"图片查不到时怎么办"的兜底点，
// 而不是每个 UI 各自处理一遍。
//
// 职责：
// 1. 提供 DefaultIcon：查不到任何图标时使用的占位图标。优先尝试
//    SpriteDatabase 里的 "ui_icon_default"（以后美术可以注册一张真正的
//    默认图标贴图）；如果连这个也没有注册，退回到程序生成的纯色占位贴图，
//    保证任何情况下都不会空白、不会报错。
// 2. 提供 ResolveEquipmentIcon(equipmentId)：装备图标解析 + 兜底一步到位，
//    UI 只需要调用这一个方法。
//
// 不负责：
// × 装备/角色/技能/Buff 的图标映射关系（那是各自的 XxxIconDatabase/
//   XxxVisualDatabase 的职责，例如 EquipmentIconDatabase）。
// × 任何游戏逻辑判断。
//
// 主要依赖：
// SpriteDatabase / EquipmentIconDatabase
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// UI 图标兜底工具：负责"图片查不到时怎么办"，本身不维护任何 Id → 图标的映射关系。
/// </summary>
public static class IconLibrary
{
    private const string DefaultIconSpriteId = "ui_icon_default";

    private static Texture2D? _proceduralDefaultIcon;

    /// <summary>
    /// 获取默认占位图标：SpriteDatabase 里如果注册了 "ui_icon_default" 就用那张贴图，
    /// 否则使用程序生成的占位纹理（纯色方块），保证永远有图可显示。
    /// </summary>
    public static Texture2D GetDefaultIcon()
    {
        var path = SpriteDatabase.GetPath(DefaultIconSpriteId);
        if (!string.IsNullOrEmpty(path) && ResourceLoader.Exists(path))
        {
            var texture = GD.Load<Texture2D>(path);
            if (texture != null)
            {
                return texture;
            }
        }

        return GetProceduralDefaultIcon();
    }

    /// <summary>
    /// 解析某件装备的图标：优先用 EquipmentIconDatabase 里的真实贴图，
    /// 查不到时回退到 <see cref="GetDefaultIcon"/>——调用方永远能拿到一个可用的 Texture2D。
    /// </summary>
    public static Texture2D ResolveEquipmentIcon(string equipmentId)
    {
        return EquipmentIconDatabase.TryGetIconTexture(equipmentId) ?? GetDefaultIcon();
    }

    private static Texture2D GetProceduralDefaultIcon()
    {
        if (_proceduralDefaultIcon != null)
        {
            return _proceduralDefaultIcon;
        }

        // 64x64 纯色方块 + 一圈稍深的边框，作为"暂无图标"的可辨识占位符，
        // 不依赖任何外部资源文件，保证 Build 出来的游戏里永远不会出现空白图标。
        var image = Image.CreateEmpty(64, 64, false, Image.Format.Rgba8);
        image.Fill(new Color(0.42f, 0.44f, 0.48f, 1f));
        for (var x = 0; x < 64; x++)
        {
            image.SetPixel(x, 0, new Color(0.65f, 0.66f, 0.70f));
            image.SetPixel(x, 63, new Color(0.65f, 0.66f, 0.70f));
        }
        for (var y = 0; y < 64; y++)
        {
            image.SetPixel(0, y, new Color(0.65f, 0.66f, 0.70f));
            image.SetPixel(63, y, new Color(0.65f, 0.66f, 0.70f));
        }

        _proceduralDefaultIcon = ImageTexture.CreateFromImage(image);
        return _proceduralDefaultIcon;
    }
}
