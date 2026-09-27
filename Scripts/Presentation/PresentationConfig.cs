//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/PresentationConfig.cs
//
// 模块：Presentation System
//
// 为什么存在：
// 表现参数会被动画、浮字、震屏、Tooltip 等多个系统共享。
// 集中配置可以避免不同 Presenter 各自写死数值，方便未来统一调参。
//
// 职责：
// 1. 集中保存表现层调参项。
// 2. 避免动画、浮字、震屏、Tooltip 等表现参数散落在业务代码中。
// 3. 为后续配置文件、Debug 面板或美术调参入口预留统一对象。
//
// 不负责：
// × 播放动画。
// × 持久化配置。
// × 修改战斗规则。
//
// 主要依赖：
// C# Runtime
//////////////////////////////////////////////////////////

/// <summary>
/// 表现层配置。
///
/// 当前只提供默认参数容器，不绑定任何真实资源。
/// 后续动画系统接入时应优先读取该配置，而不是在 Presenter 中写死数值。
/// </summary>
public sealed class PresentationConfig
{
    /// <summary>
    /// 攻击动作持续时间，默认值 0.25 秒，推荐范围 0.15 到 0.45 秒。
    ///
    /// 调大后攻击动作更有重量感，但会降低战斗节奏。
    /// 调小后出手更干脆，但过低会让命中反馈显得突兀。
    /// </summary>
    public float AttackDuration { get; set; } = 0.25f;

    /// <summary>
    /// 攻击者向目标方向移动的表现距离，默认值 48 像素，推荐范围 24 到 96 像素。
    ///
    /// 调大后冲刺感更强，但容易和 UI 或其它角色重叠。
    /// 调小后画面更稳定，但攻击动作的空间感会减弱。
    /// </summary>
    public float AttackDistance { get; set; } = 48f;

    /// <summary>
    /// 命中停顿时间，默认值 0.08 秒，推荐范围 0.03 到 0.15 秒。
    ///
    /// 调大后打击感更强，但连续命中时可能拖慢结算。
    /// 调小后战斗更流畅，但重击反馈会变弱。
    /// </summary>
    public float HitPause { get; set; } = 0.08f;

    /// <summary>
    /// 屏幕震动基础强度，默认值 8 像素，推荐范围 0 到 18 像素。
    ///
    /// 调大后冲击感更明显，但频繁震动会影响阅读。
    /// 调小后画面更安静，适合普通攻击或 UI 较密集场景。
    /// </summary>
    public float CameraShake { get; set; } = 8f;

    /// <summary>
    /// 浮动文字持续时间，默认值 1.0 秒，推荐范围 0.6 到 1.4 秒。
    ///
    /// 调大后玩家更容易读清数值，但大量浮字会堆叠。
    /// 调小后界面更清爽，但重要反馈可能来不及阅读。
    /// </summary>
    public float PopupDuration { get; set; } = 1.0f;

    /// <summary>
    /// 浮动文字基础缩放，默认值 1.0，推荐范围 0.8 到 1.4。
    ///
    /// 调大后伤害、治疗数字更醒目，但可能遮挡卡牌或角色。
    /// 调小后画面更克制，但关键反馈的辨识度会下降。
    /// </summary>
    public float PopupScale { get; set; } = 1.0f;

    /// <summary>
    /// 武器或特效拖尾长度，默认值 24 像素，推荐范围 0 到 64 像素。
    ///
    /// 调大后速度感更强，适合斩击和突刺。
    /// 调小后动作更干净，适合 UI 密集或低强度反馈。
    /// </summary>
    public float TrailLength { get; set; } = 24f;

    /// <summary>
    /// 武器相对角色挂点右边缘的额外起始偏移距离，默认值 18 像素，推荐范围 0 到 48 像素。
    ///
    /// 调大后武器起始位置更远离角色手部。
    /// 调小后武器更贴近角色，适合小型武器或图标式表现。
    /// </summary>
    public float WeaponOffset { get; set; } = 18f;

    /// <summary>
    /// 武器精灵的统一缩放（X、Y 保持一致，不拉伸），默认值 0.9，推荐范围 0.5 到 1.5。
    ///
    /// 源图尺寸不同时应优先调这个值，而不是分别修改 X/Y 缩放。
    /// </summary>
    public float WeaponScale { get; set; } = 18.0f;

    /// <summary>
    /// 挥砍时武器沿 X 方向"向前推进"的距离，默认值 20 像素，推荐范围 10 到 40 像素。
    ///
    /// 这是一次向前劈砍的小幅推进，不是横向飞出；调大后推进感更明显，调小后动作更收敛。
    /// </summary>
    public float WeaponSwingDistance { get; set; } = 20f;

    /// <summary>
    /// 挥砍动画时长，默认值 0.30 秒，推荐范围 0.2 到 0.4 秒。
    ///
    /// 调大后动作更从容、更有劈砍的重量感，调小后出手更干脆。
    /// </summary>
    public float WeaponSwingDuration { get; set; } = 0.30f;

    /// <summary>
    /// 挥砍起始旋转角度（度），默认值 -90°（剑基本竖直，位于角色右手附近）。
    /// </summary>
    public float WeaponStartRotation { get; set; } = -90f;

    /// <summary>
    /// 挥砍结束旋转角度（度），默认值 30°（由右上向左下劈砍后的角度）。
    /// </summary>
    public float WeaponEndRotation { get; set; } = 30f;

    /// <summary>
    /// 挥砍结束后、淡出前的停留时间，默认值 0.08 秒，推荐范围 0 到 0.2 秒。
    ///
    /// 让劈砍到位后的姿势短暂停留，避免直接消失显得突兀。
    /// </summary>
    public float WeaponHoldDuration { get; set; } = 0.08f;

    /// <summary>
    /// 武器和 Trail 淡出并删除所用的时间，默认值 0.12 秒，推荐范围 0.05 到 0.25 秒。
    /// </summary>
    public float WeaponFadeDuration { get; set; } = 0.12f;

    /// <summary>
    /// 角色表现移动距离，默认值 36 像素，推荐范围 12 到 72 像素。
    ///
    /// 调大后角色动作幅度更明显，但会增加布局碰撞风险。
    /// 调小后角色更稳定，适合静态立绘或小屏幕布局。
    /// </summary>
    public float CharacterMoveDistance { get; set; } = 36f;

    /// <summary>
    /// Tooltip 淡入淡出时间，默认值 0.12 秒，推荐范围 0 到 0.25 秒。
    ///
    /// 调大后提示出现更柔和，但连续悬停会显得迟滞。
    /// 调小后反馈更直接，0 表示立即显示或隐藏。
    /// </summary>
    public float TooltipFadeTime { get; set; } = 0.12f;

    /// <summary>
    /// 创建一份默认表现层配置。
    ///
    /// 使用方法而不是公开静态可变实例，是为了避免不同战斗或界面共享同一份调参状态。
    /// </summary>
    public static PresentationConfig CreateDefault()
    {
        return new PresentationConfig();
    }
}
