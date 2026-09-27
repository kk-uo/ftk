//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/Effects/EffectDefinition.cs
//
// 模块：Presentation System / Effect System
//
// 为什么存在：
// “怎么动”（旋转、位移、时长——AnimationDefinition 已经负责）和“播放什么”
// （贴图、粒子、音效、屏幕反馈——是什么、要不要震屏、要不要闪白）是两件不同的事。
// EffectDefinition 只描述后者，让同一个 EffectType 可以配出很多不同的具体效果，
// 而不需要改任何播放代码。
//
// 职责：
// 1. 描述一个效果播放时需要的全部数据：资源引用（贴图/粒子/Shader/音效）、
//    屏幕反馈（震屏/顿帧/闪屏）、以及基础的显示参数（时长/缩放/旋转/偏移/淡入淡出/层级）。
// 2. 让 EffectPlayer 只读取数据、按 EffectType 分支播放，不需要为每个效果写专属代码。
// 3. 让新增效果（火杀/雷杀/冰杀/桃/酒/Buff/Debuff/Boss 技能）变成
//    “新增一条 EffectDefinition”，而不是修改 EffectPlayer。
//
// 不负责：
// × 播放效果（由 EffectPlayer 负责）。
// × 决定效果何时触发（由 Battle/PresentationManager 负责）。
// × 战斗规则、伤害计算。
//
// 主要依赖：
// Godot.Vector2 / EffectType
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// 一个效果的完整参数集合。
///
/// 新增效果只需要新增一份 <see cref="EffectDefinition"/> 并注册到
/// <see cref="EffectDatabase"/>，不需要修改 <see cref="EffectPlayer"/> 的播放逻辑
/// （前提是该效果所属的 <see cref="EffectType"/> 已经有对应的播放分支；
/// 全新大类才需要在 EffectPlayer 里补一个分支，见 README）。
/// </summary>
public sealed class EffectDefinition
{
    /// <summary>
    /// 创建一份效果参数定义。
    ///
    /// 除 <paramref name="effectId"/> 和 <paramref name="effectType"/> 外全部提供默认值，
    /// 方便只关心少数几个参数的效果（例如只想加一次震屏，不需要贴图）。
    /// </summary>
    public EffectDefinition(
        string effectId,
        EffectType effectType,
        string spriteId = "",
        string particleId = "",
        string shaderId = "",
        string audioId = "",
        float cameraShake = 0f,
        float hitStop = 0f,
        bool flashScreen = false,
        float duration = 0f,
        float scale = 1f,
        float rotation = 0f,
        Vector2 offset = default,
        bool followTarget = false,
        bool loop = false,
        float fadeIn = 0f,
        float fadeOut = 0f,
        int layer = 60,
        int zIndex = 0,
        string notes = "",
        RenderLayer renderLayer = RenderLayer.SkillEffect,
        bool flashTarget = false)
    {
        EffectId = effectId;
        EffectType = effectType;
        SpriteId = spriteId;
        ParticleId = particleId;
        ShaderId = shaderId;
        AudioId = audioId;
        CameraShake = cameraShake;
        HitStop = hitStop;
        FlashScreen = flashScreen;
        Duration = duration;
        Scale = scale;
        Rotation = rotation;
        Offset = offset;
        FollowTarget = followTarget;
        Loop = loop;
        FadeIn = fadeIn;
        FadeOut = fadeOut;
        Layer = layer;
        ZIndex = zIndex;
        Notes = notes;
        RenderLayer = renderLayer;
        FlashTarget = flashTarget;
    }

    /// <summary>
    /// 效果稳定 ID，例如 "slash_default"、"fire_default"。
    ///
    /// 逻辑层和 Presenter 都应通过该 ID 引用效果，不直接引用资源路径。
    /// </summary>
    public string EffectId { get; }

    /// <summary>
    /// 效果大类，决定 EffectPlayer 用哪一段播放逻辑处理该效果。
    /// </summary>
    public EffectType EffectType { get; }

    /// <summary>
    /// 关联的图片资源 ID，应由 SpriteDatabase 解析为真实资源路径。
    ///
    /// 留空表示该效果暂时没有对应贴图（例如纯震屏/纯音效效果），EffectPlayer 应静默跳过。
    /// </summary>
    public string SpriteId { get; }

    /// <summary>
    /// 关联的粒子资源 ID（预留）。
    ///
    /// 当前 EffectPlayer 尚未接入真实粒子系统，该字段只保存引用，方便以后扩展。
    /// </summary>
    public string ParticleId { get; }

    /// <summary>
    /// 关联的 Shader 资源 ID（预留）。
    ///
    /// 当前 EffectPlayer 尚未接入真实 Shader 效果，该字段只保存引用，方便以后扩展。
    /// </summary>
    public string ShaderId { get; }

    /// <summary>
    /// 关联的音效资源 ID（预留）。
    ///
    /// 当前 EffectPlayer 尚未播放真实音频，该字段只保存引用，方便以后扩展。
    /// </summary>
    public string AudioId { get; }

    /// <summary>
    /// 震屏强度（预留）。0 表示不震屏。
    ///
    /// 当前 EffectPlayer 尚未实现真实镜头震动（需要引用具体 Camera 节点），
    /// 该字段只保存数据，避免以后接入时还要再扩展 EffectDefinition。
    /// </summary>
    public float CameraShake { get; }

    /// <summary>
    /// 顿帧时长（秒，预留）。0 表示不顿帧。
    ///
    /// 当前 EffectPlayer 尚未实现真实顿帧（需要控制 Engine.TimeScale 或战斗时序），
    /// 该字段只保存数据。
    /// </summary>
    public float HitStop { get; }

    /// <summary>
    /// 是否触发全屏闪白（预留）。
    ///
    /// 当前 EffectPlayer 尚未实现真实闪屏节点，该字段只保存数据。
    /// </summary>
    public bool FlashScreen { get; }

    /// <summary>
    /// 效果持续时间（秒）：贴图类效果从出现到开始淡出之间的停留时间。
    /// </summary>
    public float Duration { get; }

    /// <summary>
    /// 贴图统一缩放（等比例，不拉伸）。
    /// </summary>
    public float Scale { get; }

    /// <summary>
    /// 贴图静态旋转角度（度）。效果本身不描述“怎么转”的过程（那是 AnimationDefinition
    /// 的职责），只描述出现时的固定朝向。
    /// </summary>
    public float Rotation { get; }

    /// <summary>
    /// 相对锚点（通常是角色卡片或目标节点）的位置偏移。
    /// </summary>
    public Vector2 Offset { get; }

    /// <summary>
    /// 是否跟随锚点：true 时挂在锚点下面随锚点一起移动；
    /// false 时使用独立 CanvasLayer + GlobalPosition 定位一次，之后不再跟随锚点。
    /// </summary>
    public bool FollowTarget { get; }

    /// <summary>
    /// 是否循环播放。当前 EffectPlayer 对贴图类效果支持循环淡入淡出，
    /// 其余大类的循环语义留给以后接入时再实现。
    /// </summary>
    public bool Loop { get; }

    /// <summary>
    /// 淡入时长（秒），0 表示一出现就是完全不透明。
    /// </summary>
    public float FadeIn { get; }

    /// <summary>
    /// 淡出时长（秒），0 表示不淡出、Duration 结束后直接销毁。
    /// </summary>
    public float FadeOut { get; }

    /// <summary>
    /// 独立 CanvasLayer 的 Layer 值（FollowTarget = false 时使用）。
    ///
    /// 保留字段，向后兼容；新代码应该改用 <see cref="RenderLayer"/>——
    /// EffectPlayer 现在通过 RenderLayerManager 按 RenderLayer 取得共享 CanvasLayer，
    /// 不再读取这个原始整数。
    /// </summary>
    public int Layer { get; }

    /// <summary>
    /// 效果节点自身的 ZIndex。
    /// </summary>
    public int ZIndex { get; }

    /// <summary>
    /// 备注，方便以后维护时快速理解这条效果定义的用途，不参与任何播放逻辑。
    /// </summary>
    public string Notes { get; }

    /// <summary>
    /// 该效果所在的统一 Render Layer，默认 <see cref="global::RenderLayer.SkillEffect"/>。
    ///
    /// EffectPlayer 通过 RenderLayerManager.GetOrCreateCanvasLayer 读取这个字段决定
    /// 挂载哪个 CanvasLayer；火焰/雷电/治疗/Buff/护盾等效果都可以自行指定层级
    /// （例如护盾效果可能想显示在角色前面但在 HUD 后面）。
    /// </summary>
    public RenderLayer RenderLayer { get; }

    /// <summary>
    /// 是否是"目标闪烁"类效果：直接把锚点自身的 Modulate 调亮再还原，
    /// 而不是生成一个新的 Sprite2D。默认 false（走贴图类播放流程）。
    ///
    /// 迁移自原 <c>EffectPresenter.PresentHitEffect</c> 的受击闪烁——是 EffectPlayer
    /// 里唯一不需要 <see cref="SpriteId"/> 就能有视觉效果的模式，专门覆盖
    /// "闪烁一下已经存在的节点"这种不生成新贴图节点的场景。
    /// </summary>
    public bool FlashTarget { get; }
}
