//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/WeaponAnimationController.cs
//
// 模块：Presentation System
//
// 为什么存在：
// 武器动画资源以后会持续增加，如果每新增一把武器都修改 Presenter，
// 表现层会很快退化成大量硬编码。本控制器只按约定目录读取资源，
// 让“放入图片”与“播放动画”解耦。
//
// 职责：
// 1. 按 Weapons/<WeaponId>/ 目录自动读取 weapon.png。
// 2. 按 slash_01.png、slash_02.png ... 自动读取攻击特效帧。
// 3. 在资源缺失时回退到初始默认武器目录。
//
// 不负责：
// × 判断战斗中装备了哪把武器。
// × 计算伤害。
// × 决定动画播放位置。
//
// 主要依赖：
// Godot ResourceLoader
//////////////////////////////////////////////////////////

using Godot;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// 单把武器从目录中读取出来的表现资源。
///
/// 它只描述图片资源本身，不包含伤害、装备效果或战斗规则。
/// </summary>
public sealed class WeaponAnimationResource
{
    /// <summary>
    /// 创建一份武器表现资源快照。
    /// </summary>
    public WeaponAnimationResource(string weaponId, string folderPath, Texture2D weaponTexture, IReadOnlyList<Texture2D> slashFrames)
    {
        WeaponId = weaponId;
        FolderPath = folderPath;
        WeaponTexture = weaponTexture;
        SlashFrames = slashFrames;
    }

    /// <summary>
    /// 武器资源目录名称，同时作为表现层的武器资源 Id。
    /// </summary>
    public string WeaponId { get; }

    /// <summary>
    /// 资源目录路径，例如 res://Assets/Weapons/RustSword。
    /// </summary>
    public string FolderPath { get; }

    /// <summary>
    /// 武器本体图片，来自 weapon.png。
    /// </summary>
    public Texture2D WeaponTexture { get; }

    /// <summary>
    /// 攻击特效帧，来自 slash_01.png、slash_02.png 等顺序文件。
    /// </summary>
    public IReadOnlyList<Texture2D> SlashFrames { get; }
}

/// <summary>
/// 武器动画资源读取器。
///
/// 以后新增武器时，只要在 Assets/Weapons 下新增同名目录并放入约定文件，
/// Presenter 就能自动读取，不需要为每把武器写一段专用代码。
/// </summary>
public static class WeaponAnimationController
{
    /// <summary>
    /// 武器动画资源根目录。
    /// </summary>
    public const string RootPath = "res://Assets/Weapons";

    /// <summary>
    /// 初始默认武器目录。旧代码传入空武器 Id 或 weapon_default 时都会回退到这里。
    /// </summary>
    public const string DefaultWeaponId = "RustSteelSword";

    private const string LegacyDefaultWeaponId = "weapon_default";
    private const int MaxSlashFrames = 24;

    private static readonly Dictionary<string, WeaponAnimationResource?> CachedResources = new();

    /// <summary>
    /// 读取指定武器目录。
    ///
    /// 如果目录不存在或资源尚未导入，会自动回退到 <see cref="DefaultWeaponId"/>。
    /// </summary>
    public static WeaponAnimationResource? Load(string requestedWeaponId)
    {
        var weaponId = ResolveWeaponId(requestedWeaponId);
        if (CachedResources.TryGetValue(weaponId, out var cached))
        {
            return cached;
        }

        var resource = TryLoadFolder(weaponId);
        if (resource == null && weaponId != DefaultWeaponId)
        {
            resource = TryLoadFolder(DefaultWeaponId);
        }

        CachedResources[weaponId] = resource;
        return resource;
    }

    private static string ResolveWeaponId(string requestedWeaponId)
    {
        if (string.IsNullOrWhiteSpace(requestedWeaponId) || requestedWeaponId == LegacyDefaultWeaponId)
        {
            return DefaultWeaponId;
        }

        var sanitized = SanitizeFolderName(requestedWeaponId);
        return string.IsNullOrEmpty(sanitized) ? DefaultWeaponId : sanitized;
    }

    private static string SanitizeFolderName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsLetterOrDigit(character) || character == '_' || character == '-')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static WeaponAnimationResource? TryLoadFolder(string weaponId)
    {
        var folderPath = $"{RootPath}/{weaponId}";
        var weaponPath = $"{folderPath}/weapon.png";
        if (!ResourceLoader.Exists(weaponPath))
        {
            return null;
        }

        var weaponTexture = GD.Load<Texture2D>(weaponPath);
        if (weaponTexture == null)
        {
            return null;
        }

        var slashFrames = new List<Texture2D>();
        for (var frameIndex = 1; frameIndex <= MaxSlashFrames; frameIndex++)
        {
            var framePath = $"{folderPath}/slash_{frameIndex:00}.png";
            if (!ResourceLoader.Exists(framePath))
            {
                break;
            }

            var frameTexture = GD.Load<Texture2D>(framePath);
            if (frameTexture != null)
            {
                slashFrames.Add(frameTexture);
            }
        }

        return new WeaponAnimationResource(weaponId, folderPath, weaponTexture, slashFrames);
    }
}
