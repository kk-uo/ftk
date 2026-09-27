//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/Effects/EffectType.cs
//
// 模块：Presentation System / Effect System
//
// 为什么存在：
// EffectPlayer 需要知道一个 Effect 大致属于哪一类，才能决定用哪种方式播放
// （贴图挥砍、屏幕闪白、镜头震动、UI 提示……），但不应该为每一类效果各写一个
// 完全独立、互不相干的播放器。
//
// 职责：
// 1. 枚举当前 Effect System 支持（或预留）的效果大类。
// 2. 让 EffectDefinition.EffectType 有一个统一、可扩展的取值范围。
//
// 不负责：
// × 决定具体播放哪张贴图、哪个粒子、哪个音效（由 EffectDefinition 的其它字段决定）。
// × 播放效果本身（由 EffectPlayer 负责）。
//
// 主要依赖：
// 无
//////////////////////////////////////////////////////////

/// <summary>
/// 效果大类。
///
/// 只描述效果“属于哪一类”，具体表现资源和参数仍然由 <see cref="EffectDefinition"/> 决定；
/// 新增大类只需要在这里加一个枚举值，不需要改动已有效果的注册和播放逻辑。
/// </summary>
public enum EffectType
{
    /// <summary>普通斩击类效果（例如默认武器挥砍留下的视觉反馈）。</summary>
    Slash,

    /// <summary>火属性效果（预留，例如火杀）。</summary>
    Fire,

    /// <summary>雷属性效果（预留，例如雷杀）。</summary>
    Thunder,

    /// <summary>冰属性效果（预留，例如冰杀）。</summary>
    Ice,

    /// <summary>治疗效果（预留，例如桃）。</summary>
    Heal,

    /// <summary>护盾效果（预留）。</summary>
    Shield,

    /// <summary>增益效果（预留，例如酒的攻击力加成）。</summary>
    Buff,

    /// <summary>减益效果（预留）。</summary>
    Debuff,

    /// <summary>UI 相关效果（预留，例如提示浮字的附加表现）。</summary>
    UI,

    /// <summary>全屏效果（预留，例如闪屏）。</summary>
    Screen,

    /// <summary>镜头效果（预留，例如镜头震动）。</summary>
    Camera,

    /// <summary>粒子效果（预留，尚未接入具体粒子系统）。</summary>
    Particle,

    /// <summary>Shader 效果（预留，尚未接入具体 Shader 方案）。</summary>
    Shader,

    /// <summary>不属于以上任何一类的自定义效果。</summary>
    Custom
}
