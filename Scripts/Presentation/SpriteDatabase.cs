//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/SpriteDatabase.cs
//
// 模块：Presentation System
//
// 为什么存在：
// 图片资源路径如果散落在业务代码中，后续替换美术和做缓存会非常困难。
// SpriteDatabase 提供未来统一加载与缓存的入口。
//
// 职责：
// 1. 统一登记图片资源 ID 与路径。
// 2. 为未来 Sprite 加载、缓存和替换提供唯一入口。
// 3. 防止业务代码直接 Load 图片路径。
//
// 不负责：
// × 实际加载 Godot Texture。
// × 管理动画。
// × 决定图片何时显示。
//
// 主要依赖：
// C# Runtime
//////////////////////////////////////////////////////////

using System.Collections.Generic;

/// <summary>
/// 图片资源数据库。
///
/// 当前只保存 ID 到资源路径的映射，后续可以扩展为缓存 Godot 纹理资源。
/// </summary>
public static class SpriteDatabase
{
    private static readonly Dictionary<string, string> SpritePaths = new();

    /// <summary>
    /// 注册图片资源路径。
    /// </summary>
    public static void Register(string id, string path)
    {
        SpritePaths[id] = path;
    }

    /// <summary>
    /// 按 ID 查询图片资源路径。
    /// </summary>
    public static string? GetPath(string id)
    {
        return SpritePaths.TryGetValue(id, out var path) ? path : null;
    }
}
