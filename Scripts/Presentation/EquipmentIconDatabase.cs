//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/EquipmentIconDatabase.cs
//
// 模块：Presentation System / UI Icon System
//
// 为什么存在：
// UI 需要"读一个 Id 就能拿到装备图标"的能力，但装备逻辑数据
// （EquipmentDefinition/EquipmentDatabase）不应该被迫增加表现层字段——
// 图片路径、图标 ID 这些完全是表现层的事。这个类把"装备 Id → IconSpriteId"
// 的映射独立出来，UI 只需要传装备的既有 Id，不需要装备逻辑知道图标存在。
//
// 职责：
// 1. 为每件已注册的装备（读取自既有 EquipmentDatabase.GetAllEquipments，
//    只读、不修改）按约定自动派生一个 IconSpriteId（"equipment_icon_<Id>"）。
// 2. 允许以后按需覆盖某件装备的 IconSpriteId（Register），不需要遵循自动约定。
// 3. 提供 TryGetIconTexture(equipmentId)：IconSpriteId → SpriteDatabase → Texture2D
//    这条完整查询链路的唯一入口，UI 不需要自己拼图片路径。
//
// 不负责：
// × 装备属性、掉落、战斗效果（完全不读写 EquipmentDefinition 的任何字段）。
// × 加载资源缓存策略（和其它 Presentation 数据库一样，直接 GD.Load）。
// × 图片缺失时的兜底显示（那是 Scripts/UI/IconLibrary.cs 的职责）。
//
// 主要依赖：
// SpriteDatabase / EquipmentDatabase（只读查询 GetAllEquipments）
//////////////////////////////////////////////////////////

using System.Collections.Generic;
using Godot;

/// <summary>
/// 装备图标数据库：装备 Id → IconSpriteId → SpriteDatabase → Texture2D。
///
/// 不在 EquipmentDefinition 上新增任何字段——装备逻辑数据和表现层图标
/// 完全解耦，通过装备已有的 Id 关联。
/// </summary>
public static class EquipmentIconDatabase
{
    private const string IconSpriteIdPrefix = "equipment_icon_";
    private const string IconResourceDirectory = "res://Assets/UI/Icons/Equipment/";

    private static readonly Dictionary<string, string> EquipmentIdToIconSpriteId = new();

    static EquipmentIconDatabase()
    {
        // 按约定自动为每件已注册装备派生一条 IconSpriteId："equipment_icon_<装备Id>"。
        // 只读取 EquipmentDatabase.GetAllEquipments() 枚举已有装备，不修改它，
        // 也不需要每新增一件装备就手动登记一次图标映射。
        foreach (var definition in EquipmentDatabase.GetAllEquipments())
        {
            if (!EquipmentIdToIconSpriteId.ContainsKey(definition.Id))
            {
                var spriteId = IconSpriteIdPrefix + definition.Id;
                EquipmentIdToIconSpriteId[definition.Id] = spriteId;

                // 装备逻辑只提供稳定 Id；图标路径完全由表现层按约定生成。
                // 这使新增图标不必触碰背包、商店或场景布局代码。
                SpriteDatabase.Register(spriteId, IconResourceDirectory + definition.Id + ".png");
            }
        }
    }

    /// <summary>
    /// 覆盖（或补充）某件装备的 IconSpriteId，不遵循自动约定时使用。
    /// </summary>
    public static void Register(string equipmentId, string iconSpriteId)
    {
        EquipmentIdToIconSpriteId[equipmentId] = iconSpriteId;
    }

    /// <summary>
    /// 查询某件装备对应的 IconSpriteId；理论上所有已注册装备都有（自动派生的）值。
    /// </summary>
    public static string? GetIconSpriteId(string equipmentId)
    {
        return EquipmentIdToIconSpriteId.TryGetValue(equipmentId, out var spriteId) ? spriteId : null;
    }

    /// <summary>
    /// 完整解析链路：装备 Id → IconSpriteId → SpriteDatabase → Texture2D。
    ///
    /// 贴图尚未注册/资源不存在时返回 null——调用方（通常是 Scripts/UI/IconLibrary.cs）
    /// 应该回退到 DefaultIcon，这里不做任何兜底，也不报错。
    /// </summary>
    public static Texture2D? TryGetIconTexture(string equipmentId)
    {
        var spriteId = GetIconSpriteId(equipmentId);
        if (string.IsNullOrEmpty(spriteId))
        {
            return null;
        }

        var path = SpriteDatabase.GetPath(spriteId);
        if (string.IsNullOrEmpty(path) || !ResourceLoader.Exists(path))
        {
            return null;
        }

        return GD.Load<Texture2D>(path);
    }
}
