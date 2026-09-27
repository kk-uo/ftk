//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/CombatVisualProfile/CombatVisualProfileDatabase.cs
//
// 模块：Presentation System / Combat Visual Profile System
//
// 为什么存在：
// 攻击/防御表现如果按装备类型写 if/switch 判断，每新增一件装备都要改代码。
// 这个数据库把"装备 Id → AttackVisualProfile / DefenseVisualProfile"做成
// 一份纯查询表，新增装备表现只需要注册一条数据。
//
// 职责：
// 1. 按装备 Id 注册/查询 AttackVisualProfile、DefenseVisualProfile。
// 2. 提供 DefaultAttackVisualProfile/DefaultDefenseVisualProfile，
//    装备没有配置专属 Profile 时自动使用，查询方法本身就是"回退到默认"，
//    调用方不需要写任何 if (profile == null) 之类的特殊判断。
//
// 不负责：
// × 修改 EquipmentDefinition/EquipmentDatabase（只用装备已有的 Id 作为查询 key，
//   不在装备数据类上新增任何字段）。
// × 播放表现（由 WeaponPresenter/EffectPlayer 等负责）。
// × 装备属性、伤害、战斗效果。
//
// 主要依赖：
// AttackVisualProfile / DefenseVisualProfile
//////////////////////////////////////////////////////////

using System.Collections.Generic;

/// <summary>
/// 战斗表现 Profile 数据库：装备 Id → AttackVisualProfile / DefenseVisualProfile。
///
/// 查询方法 <see cref="GetAttackProfile"/>/<see cref="GetDefenseProfile"/> 本身就包含
/// "没有配置时回退默认"的逻辑，调用方永远能拿到一份可用的 Profile，
/// 不需要在调用处写任何空值判断。
/// </summary>
public static class CombatVisualProfileDatabase
{
    private static readonly Dictionary<string, AttackVisualProfile> AttackProfiles = new();
    private static readonly Dictionary<string, DefenseVisualProfile> DefenseProfiles = new();

    /// <summary>
    /// 默认攻击表现：复用 Weapon Presentation（Phase 2）和 Effect System 已经注册好的
    /// 默认武器/Trail/挥砍动画/挥砍特效 Id（"weapon_default"/"weapon_default_trail"/
    /// "weapon_default_attack"/EffectDatabase.SlashDefaultEffectId），
    /// 保证默认 Profile 从第一天起就是一条完整可用的链路，而不是一堆空字符串。
    /// </summary>
    public static readonly AttackVisualProfile DefaultAttackVisualProfile = new(
        profileId: "attack_default",
        weaponSpriteId: "weapon_default",
        trailSpriteId: "weapon_default_trail",
        attackAnimationId: "weapon_default_attack",
        attackEffectId: EffectDatabase.SlashDefaultEffectId);

    /// <summary>
    /// 默认防御表现：目前项目里还没有专门的格挡动画/特效资源，所有字段留空，
    /// GetDefenseProfile 的调用方（未来的防御 Presenter）应该在字段为空时静默跳过，
    /// 不报错、不影响战斗结算。
    /// </summary>
    public static readonly DefenseVisualProfile DefaultDefenseVisualProfile = new(
        profileId: "defense_default");

    /// <summary>
    /// 注册（或覆盖）某件装备的攻击表现 Profile。
    /// </summary>
    public static void RegisterAttackProfile(string equipmentId, AttackVisualProfile profile)
    {
        AttackProfiles[equipmentId] = profile;
    }

    /// <summary>
    /// 查询某件装备的攻击表现 Profile；没有注册过时自动返回
    /// <see cref="DefaultAttackVisualProfile"/>，调用方不需要判空。
    /// </summary>
    public static AttackVisualProfile GetAttackProfile(string equipmentId)
    {
        return AttackProfiles.TryGetValue(equipmentId, out var profile) ? profile : DefaultAttackVisualProfile;
    }

    /// <summary>
    /// 注册（或覆盖）某件装备的防御表现 Profile。
    /// </summary>
    public static void RegisterDefenseProfile(string equipmentId, DefenseVisualProfile profile)
    {
        DefenseProfiles[equipmentId] = profile;
    }

    /// <summary>
    /// 查询某件装备的防御表现 Profile；没有注册过时自动返回
    /// <see cref="DefaultDefenseVisualProfile"/>，调用方不需要判空。
    /// </summary>
    public static DefenseVisualProfile GetDefenseProfile(string equipmentId)
    {
        return DefenseProfiles.TryGetValue(equipmentId, out var profile) ? profile : DefaultDefenseVisualProfile;
    }
}
