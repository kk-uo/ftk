//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/AnimationDatabase.cs
//
// 模块：Presentation System
//
// 为什么存在：
// 动画方案未来可能从 Tween 扩展到 AnimationPlayer、Shader 或 Spine；
// 挥砍角度、位移、缩放、淡入淡出、Trail 等具体参数也不应该写死在 Presenter 里。
// AnimationDatabase 用稳定 ID 隔离业务代码和具体动画资源/参数。
//
// 职责：
// 1. 统一登记动画资源 ID 与路径（GetPath，供未来接入 AnimationPlayer/Spine 资源用）。
// 2. 统一登记动画参数定义（GetAnimation，供 Tween 类 Presenter 直接读取播放）。
// 3. 为未来 Tween、AnimationPlayer、Spine 或其它动画方案预留统一入口。
// 4. 防止业务代码直接绑定动画资源路径或写死 Tween 数值。
//
// 不负责：
// × 播放动画（由 WeaponPresenter/CharacterPresenter 等负责）。
// × 决定动画何时触发（由 Battle/PresentationManager 负责）。
// × 修改战斗逻辑。
//
// 主要依赖：
// AnimationDefinition
//////////////////////////////////////////////////////////

using System.Collections.Generic;

/// <summary>
/// 动画资源与参数数据库。
///
/// 提供两套并行的注册表：
/// - <see cref="AnimationPaths"/>：ID → 资源路径，供未来接入真正的动画资源文件使用。
/// - <see cref="Definitions"/>：ID → <see cref="AnimationDefinition"/>，供当前 Tween 类
///   Presenter（例如 WeaponPresenter）直接读取旋转、位移、缩放、淡入淡出等参数。
/// 两套注册表互不影响，新增动画时按需要注册其中一种或两种都注册。
/// </summary>
public static class AnimationDatabase
{
    private static readonly Dictionary<string, string> AnimationPaths = new();
    private static readonly Dictionary<string, AnimationDefinition> Definitions = new();

    /// <summary>
    /// 注册动画资源路径。
    /// </summary>
    public static void Register(string id, string path)
    {
        AnimationPaths[id] = path;
    }

    /// <summary>
    /// 按 ID 查询动画资源路径。
    /// </summary>
    public static string? GetPath(string id)
    {
        return AnimationPaths.TryGetValue(id, out var path) ? path : null;
    }

    /// <summary>
    /// 注册一份动画参数定义。
    ///
    /// 新增动画（普通杀/火杀/雷杀/桃/酒/技能/Boss 等）只需要调用该方法注册一条新的
    /// <see cref="AnimationDefinition"/>，不需要修改任何 Presenter 播放代码。
    /// </summary>
    public static void RegisterAnimation(AnimationDefinition definition)
    {
        Definitions[definition.AnimationId] = definition;
    }

    /// <summary>
    /// 按 ID 查询动画参数定义；未注册时返回 null，调用方应静默跳过，不影响战斗结算。
    /// </summary>
    public static AnimationDefinition? GetAnimation(string id)
    {
        return Definitions.TryGetValue(id, out var definition) ? definition : null;
    }
}
