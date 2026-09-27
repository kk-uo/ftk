//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/CombatVisualProfile/AttackVisualProfile.cs
//
// 模块：Presentation System / Combat Visual Profile System
//
// 为什么存在：
// 一次完整的攻击表现（武器贴图、Trail、动画、特效、音效、命中反馈、
// 预留的震屏/闪屏）如果散落在 WeaponPresenter 或者每件装备各自的代码里，
// 以后每新增一把武器都要改播放代码。AttackVisualProfile 把这些全部收敛成
// 一份数据，武器/装备只需要"关联一份 Profile"，不需要修改任何播放逻辑。
//
// 职责：
// 1. 描述一次攻击表现需要的全部资源 Id（武器贴图、Trail、动画、特效、音效）。
// 2. 预留 CameraShakeId/ScreenFlashId，供以后接入镜头/全屏反馈时使用。
// 3. 让 CombatVisualProfileDatabase 可以按装备 Id 查询到这份数据，
//    不需要在 EquipmentDefinition 上新增任何字段。
//
// 不负责：
// × 播放表现（由 WeaponPresenter/EffectPlayer/未来的 AudioPlayer 负责）。
// × 装备属性、伤害、战斗效果（完全不涉及）。
// × 决定攻击何时发生（由 Battle 负责）。
//
// 主要依赖：
// 无（纯数据，字段值分别对应 SpriteDatabase/AnimationDatabase/EffectDatabase/
// AudioDatabase 里注册的 Id）
//////////////////////////////////////////////////////////

/// <summary>
/// 一次攻击表现的完整参数集合。
///
/// 除 <see cref="ProfileId"/> 外全部是可选的资源 Id 字符串——留空表示这一步
/// 表现暂时没有对应资源，播放方（WeaponPresenter/EffectPlayer 等）应该静默跳过，
/// 不报错、不影响战斗结算。
/// </summary>
public sealed class AttackVisualProfile
{
    /// <summary>
    /// 创建一份攻击表现 Profile。
    /// </summary>
    public AttackVisualProfile(
        string profileId,
        string weaponSpriteId = "",
        string trailSpriteId = "",
        string attackAnimationId = "",
        string attackEffectId = "",
        string attackAudioId = "",
        string hitEffectId = "",
        string hitAudioId = "",
        string cameraShakeId = "",
        string screenFlashId = "")
    {
        ProfileId = profileId;
        WeaponSpriteId = weaponSpriteId;
        TrailSpriteId = trailSpriteId;
        AttackAnimationId = attackAnimationId;
        AttackEffectId = attackEffectId;
        AttackAudioId = attackAudioId;
        HitEffectId = hitEffectId;
        HitAudioId = hitAudioId;
        CameraShakeId = cameraShakeId;
        ScreenFlashId = screenFlashId;
    }

    /// <summary>Profile 稳定 Id，例如 "attack_default"、"attack_qinggangjian"。</summary>
    public string ProfileId { get; }

    /// <summary>武器贴图资源 Id，交给 SpriteDatabase 解析。</summary>
    public string WeaponSpriteId { get; }

    /// <summary>武器挥砍 Trail 贴图资源 Id，交给 SpriteDatabase 解析。</summary>
    public string TrailSpriteId { get; }

    /// <summary>挥砍动画参数 Id，交给 AnimationDatabase 解析。</summary>
    public string AttackAnimationId { get; }

    /// <summary>攻击特效 Id（挥砍/斩击类），交给 EffectDatabase 解析。</summary>
    public string AttackEffectId { get; }

    /// <summary>攻击音效 Id（预留，交给未来的 AudioDatabase/AudioPlayer 解析）。</summary>
    public string AttackAudioId { get; }

    /// <summary>命中特效 Id，交给 EffectDatabase 解析。</summary>
    public string HitEffectId { get; }

    /// <summary>命中音效 Id（预留，交给未来的 AudioDatabase/AudioPlayer 解析）。</summary>
    public string HitAudioId { get; }

    /// <summary>震屏 Id（预留，当前没有 Presenter 读取该字段）。</summary>
    public string CameraShakeId { get; }

    /// <summary>全屏闪光 Id（预留，当前没有 Presenter 读取该字段）。</summary>
    public string ScreenFlashId { get; }
}
