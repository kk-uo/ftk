//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/AnimationDefinition.cs
//
// 模块：Presentation System
//
// 为什么存在：
// 挥砍角度、位移、缩放、淡入淡出、Trail 参数如果散落在各个 Presenter 里，
// 以后每新增一个动画（火杀/雷杀/技能/Boss）都要重复写一遍 Tween 代码。
// AnimationDefinition 把这些数值收敛成一份数据，Presenter 只负责“读数据 + 播放”。
//
// 职责：
// 1. 描述一个武器/角色挥动动画所需的全部可调参数。
// 2. 让 WeaponPresenter、CharacterPresenter 等表现层只读取数据，不写死数值。
// 3. 让新增动画变成“新增一条 AnimationDefinition”，而不是修改播放代码。
//
// 不负责：
// × 播放动画（由 Presenter 负责）。
// × 决定何时触发动画（由 Battle/PresentationManager 负责）。
// × 计算伤害或战斗结果。
//
// 主要依赖：
// Godot.Vector2
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// 一个动画的完整参数集合。
///
/// 每个字段都对应 Presenter 播放动画时需要的一个具体数值，
/// 新增动画只需要新增一份 <see cref="AnimationDefinition"/> 并注册到
/// <see cref="AnimationDatabase"/>，不需要修改任何 Presenter 代码。
/// </summary>
public sealed class AnimationDefinition
{
    /// <summary>
    /// 创建一份动画参数定义。
    ///
    /// 除 <paramref name="animationId"/> 和 <paramref name="duration"/> 外全部提供默认值，
    /// 方便只关心少数几个参数的新动画（例如只改角度、不改 Trail）。
    /// </summary>
    public AnimationDefinition(
        string animationId,
        float duration,
        float startRotation = 0f,
        float endRotation = 0f,
        Vector2 startOffset = default,
        Vector2 endOffset = default,
        float scale = 1f,
        float fadeInTime = 0f,
        float fadeOutTime = 0f,
        float holdDuration = 0f,
        bool trailEnabled = false,
        float trailFadeTime = 0f,
        float trailScale = 1f,
        float trailAlpha = 1f,
        bool weaponVisible = true,
        bool playOnTop = true,
        bool loop = false,
        string animationCurve = "",
        RenderLayer renderLayer = RenderLayer.Weapon,
        Vector2 startScale = default,
        Vector2 endScale = default)
    {
        AnimationId = animationId;
        Duration = duration;
        StartRotation = startRotation;
        EndRotation = endRotation;
        StartOffset = startOffset;
        EndOffset = endOffset;
        Scale = scale;
        FadeInTime = fadeInTime;
        FadeOutTime = fadeOutTime;
        HoldDuration = holdDuration;
        TrailEnabled = trailEnabled;
        TrailFadeTime = trailFadeTime;
        TrailScale = trailScale;
        TrailAlpha = trailAlpha;
        WeaponVisible = weaponVisible;
        PlayOnTop = playOnTop;
        Loop = loop;
        AnimationCurve = animationCurve;
        RenderLayer = renderLayer;
        StartScale = startScale == Vector2.Zero ? new Vector2(scale, scale) : startScale;
        EndScale = endScale == Vector2.Zero ? new Vector2(scale, scale) : endScale;
    }

    /// <summary>
    /// 动画稳定 ID，例如 "weapon_default_attack"、"weapon_fire_attack"。
    ///
    /// Presenter 通过该 ID 从 <see cref="AnimationDatabase"/> 查询参数，
    /// 不应该在代码里拼接或猜测 ID 规则之外的内容。
    /// </summary>
    public string AnimationId { get; }

    /// <summary>
    /// 挥动主体阶段的时长（秒），即旋转 + 位移那一段的持续时间。
    /// </summary>
    public float Duration { get; }

    /// <summary>
    /// 起始旋转角度（度）。
    /// </summary>
    public float StartRotation { get; }

    /// <summary>
    /// 结束旋转角度（度）。
    /// </summary>
    public float EndRotation { get; }

    /// <summary>
    /// 起始位置相对锚点（角色卡片）右边缘的偏移量。
    ///
    /// Presenter 仍然需要用锚点自身的 Size 计算基础挂点，该偏移只是在此之上的额外调整。
    /// </summary>
    public Vector2 StartOffset { get; }

    /// <summary>
    /// 结束位置相对锚点右边缘的偏移量，与 <see cref="StartOffset"/> 同一参照系。
    /// </summary>
    public Vector2 EndOffset { get; }

    /// <summary>
    /// 武器精灵的统一缩放（等比例，不拉伸）。
    /// </summary>
    public float Scale { get; }

    /// <summary>
    /// 动作起始时的二维缩放。X/Y 不同时可模拟武器在纵深方向上的透视压缩；
    /// 未显式配置时自动使用 <see cref="Scale"/> 的等比例值。
    /// </summary>
    public Vector2 StartScale { get; }

    /// <summary>
    /// 动作结束时的二维缩放。与 <see cref="StartScale"/> 配合，可以在纯 2D 场景中
    /// 表现武器由远及近的劈落感，而不需要引入真正的 3D 节点。
    /// </summary>
    public Vector2 EndScale { get; }

    /// <summary>
    /// 淡入时长（秒）。默认动画一开始就是完全不透明，该值为 0 表示不做淡入。
    /// </summary>
    public float FadeInTime { get; }

    /// <summary>
    /// 淡出时长（秒），动画播放完毕、停留结束后，武器（和 Trail）淡出所用的时间。
    /// </summary>
    public float FadeOutTime { get; }

    /// <summary>
    /// 挥动结束、淡出开始前的停留时间（秒），让最终姿势短暂停留，避免直接消失显得突兀。
    /// </summary>
    public float HoldDuration { get; }

    /// <summary>
    /// 是否启用 Trail 表现。
    ///
    /// 为 false 时 Presenter 不应该创建 Trail 节点，即使武器视觉定义里配置了 TrailSpriteId。
    /// </summary>
    public bool TrailEnabled { get; }

    /// <summary>
    /// Trail 淡出所用时间（秒），通常与 <see cref="FadeOutTime"/> 一致，也可以单独调整。
    /// </summary>
    public float TrailFadeTime { get; }

    /// <summary>
    /// Trail 相对武器的统一缩放比例（Trail 是武器的子节点，最终显示缩放 = 武器 Scale × 该值）。
    /// </summary>
    public float TrailScale { get; }

    /// <summary>
    /// Trail 起始透明度（0~1）。
    /// </summary>
    public float TrailAlpha { get; }

    /// <summary>
    /// 该动画播放时武器精灵本体是否可见。
    ///
    /// 预留给未来“只播放 Trail/特效、不显示武器本体”的动画类型。
    /// </summary>
    public bool WeaponVisible { get; }

    /// <summary>
    /// 是否渲染在最上层（对应更高的 ZIndex，在所在 RenderLayer 内部的相对排序）。
    /// </summary>
    public bool PlayOnTop { get; }

    /// <summary>
    /// 是否循环播放。当前 Presenter 实现只处理一次性动画，
    /// 该字段为未来循环型表现（例如持续燃烧特效）预留。
    /// </summary>
    public bool Loop { get; }

    /// <summary>
    /// 动画缓动曲线 ID（预留）。
    ///
    /// 当前 Presenter 未读取该字段，统一使用线性过渡；
    /// 以后接入自定义缓动时，应该在这里存曲线资源 ID，而不是新增字段。
    /// </summary>
    public string AnimationCurve { get; }

    /// <summary>
    /// 该动画期望播放在哪个统一 Render Layer，默认 <see cref="global::RenderLayer.Weapon"/>。
    ///
    /// 当前 WeaponPresenter 的 CanvasLayer 选择仍然读取
    /// WeaponVisualDefinition.WeaponLayer（武器本身的层级），这个字段是为未来
    /// 角色/Boss 演出类动画预留的入口——那些动画不一定挂在某件武器上，
    /// 需要自己决定层级时直接读取这里，不需要再新增字段。
    /// </summary>
    public RenderLayer RenderLayer { get; }
}
