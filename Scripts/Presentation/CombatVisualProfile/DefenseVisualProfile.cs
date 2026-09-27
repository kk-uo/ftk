//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/CombatVisualProfile/DefenseVisualProfile.cs
//
// 模块：Presentation System / Combat Visual Profile System
//
// 为什么存在：
// 防御表现（格挡动作、格挡特效、格挡音效、格挡姿势、预留的护盾贴图/反击特效）
// 和攻击表现是同一类问题：不应该写死在某个 Presenter 或装备代码里，
// 而应该是"关联一份 Profile"就能生效的数据。
//
// 职责：
// 1. 描述一次防御表现需要的全部资源 Id。
// 2. 预留 ShieldSpriteId/CounterEffectId，供以后接入护盾/反击表现时使用。
//
// 不负责：
// × 播放表现、判断是否格挡成功（由 Battle 负责结算，Presenter 负责播放）。
// × 装备属性、伤害、战斗效果。
//
// 主要依赖：
// 无（纯数据）
//////////////////////////////////////////////////////////

/// <summary>
/// 一次防御表现的完整参数集合，字段留空表示暂无对应资源，播放方应静默跳过。
/// </summary>
public sealed class DefenseVisualProfile
{
    /// <summary>
    /// 创建一份防御表现 Profile。
    /// </summary>
    public DefenseVisualProfile(
        string profileId,
        string defenseAnimationId = "",
        string defenseEffectId = "",
        string defenseAudioId = "",
        string blockPoseId = "",
        string shieldSpriteId = "",
        string counterEffectId = "")
    {
        ProfileId = profileId;
        DefenseAnimationId = defenseAnimationId;
        DefenseEffectId = defenseEffectId;
        DefenseAudioId = defenseAudioId;
        BlockPoseId = blockPoseId;
        ShieldSpriteId = shieldSpriteId;
        CounterEffectId = counterEffectId;
    }

    /// <summary>Profile 稳定 Id，例如 "defense_default"。</summary>
    public string ProfileId { get; }

    /// <summary>格挡动作动画参数 Id，交给 AnimationDatabase 解析。</summary>
    public string DefenseAnimationId { get; }

    /// <summary>格挡特效 Id，交给 EffectDatabase 解析。</summary>
    public string DefenseEffectId { get; }

    /// <summary>格挡音效 Id（预留，交给未来的 AudioDatabase/AudioPlayer 解析）。</summary>
    public string DefenseAudioId { get; }

    /// <summary>格挡姿势 Id（预留，供角色表现层决定摆出哪种防御姿势）。</summary>
    public string BlockPoseId { get; }

    /// <summary>护盾贴图资源 Id（预留，交给 SpriteDatabase 解析）。</summary>
    public string ShieldSpriteId { get; }

    /// <summary>反击特效 Id（预留，交给 EffectDatabase 解析）。</summary>
    public string CounterEffectId { get; }
}
