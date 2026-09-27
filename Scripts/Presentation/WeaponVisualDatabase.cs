//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/WeaponVisualDatabase.cs
//
// 模块：Presentation System
//
// 为什么存在：
// 武器视觉会随着装备、美术资源和动画系统变化。
// 统一数据库可以让 Presenter 查询视觉定义，而不是让 Battle 或 Equipment 直接管理资源。
//
// 职责：
// 1. 注册武器视觉定义。
// 2. 为装备到武器表现的映射提供统一入口。
// 3. 避免 Battle、Inventory 或 Equipment 直接管理武器图片路径。
//
// 不负责：
// × 装备随机池。
// × 装备效果执行。
// × 武器节点实例化。
//
// 主要依赖：
// WeaponVisualDefinition
//////////////////////////////////////////////////////////

using System.Collections.Generic;

/// <summary>
/// 武器视觉数据库。
///
/// 当前为空注册表，后续新增武器图片和动作时从这里集中维护。
/// </summary>
public static class WeaponVisualDatabase
{
    private static readonly Dictionary<string, WeaponVisualDefinition> Definitions = new();

    /// <summary>
    /// 注册武器视觉定义。
    /// </summary>
    public static void Register(WeaponVisualDefinition definition)
    {
        Definitions[definition.Id] = definition;
    }

    /// <summary>
    /// 按 ID 查询武器视觉定义。
    /// </summary>
    public static WeaponVisualDefinition? Get(string id)
    {
        return Definitions.TryGetValue(id, out var definition) ? definition : null;
    }

    /// <summary>
    /// 返回当前已注册的全部武器视觉定义。
    /// </summary>
    public static IReadOnlyCollection<WeaponVisualDefinition> GetAll()
    {
        return Definitions.Values;
    }
}
