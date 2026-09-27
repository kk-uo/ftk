//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/WeaponVisualDefinition.cs
//
// 模块：Presentation System
//
// 为什么存在：
// 装备数据不应该直接引用图片、动画或特效资源。
// 该定义把武器表现资源独立出来，让装备逻辑保持纯规则数据。
//
// 职责：
// 1. 描述武器视觉资源。
// 2. 将装备 ID 与武器图片、动画、特效关联起来。
// 3. 让 Battle 不需要知道具体使用哪把武器的视觉资源。
//
// 不负责：
// × 装备属性。
// × 装备获取方式。
// × 装备战斗触发。
//
// 主要依赖：
// C# Runtime
//////////////////////////////////////////////////////////

/// <summary>
/// 武器视觉定义。
///
/// 该定义只面向表现层，逻辑层仍然使用 EquipmentDefinition 判断规则。
/// </summary>
public sealed class WeaponVisualDefinition
{
    /// <summary>
    /// 创建武器视觉定义。
    /// </summary>
    public WeaponVisualDefinition(
        string id,
        string spriteId = "",
        string animationId = "",
        string effectId = "",
        string trailSpriteId = "",
        RenderLayer weaponLayer = RenderLayer.Weapon,
        RenderLayer trailLayer = RenderLayer.WeaponTrail)
    {
        Id = id;
        SpriteId = spriteId;
        AnimationId = animationId;
        EffectId = effectId;
        TrailSpriteId = trailSpriteId;
        WeaponLayer = weaponLayer;
        TrailLayer = trailLayer;
    }

    /// <summary>
    /// 武器视觉稳定 ID。
    ///
    /// 通常可以与装备 ID 对齐，但表现层不依赖装备定义对象。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 武器图片资源 ID。
    ///
    /// WeaponPresenter 应通过 SpriteDatabase 查询该资源。
    /// </summary>
    public string SpriteId { get; }

    /// <summary>
    /// 武器动作动画 ID。
    ///
    /// 用于挥动、蓄力、收刀等武器层动画。
    /// </summary>
    public string AnimationId { get; }

    /// <summary>
    /// 武器默认特效 ID。
    ///
    /// 用于把武器表现与斩击、火焰、电击等特效关联起来。
    /// </summary>
    public string EffectId { get; }

    /// <summary>
    /// 武器挥砍拖尾（Trail）图片资源 ID。
    ///
    /// WeaponPresenter 应通过 SpriteDatabase 查询该资源；不同武器可以拥有不同的拖尾，
    /// 留空表示该武器暂不使用拖尾表现。
    /// </summary>
    public string TrailSpriteId { get; }

    /// <summary>
    /// 武器本体所在的 Render Layer，默认 <see cref="RenderLayer.Weapon"/>。
    ///
    /// 不同武器可以拥有不同的显示层级（例如某把武器需要盖住某些特效），
    /// WeaponPresenter 应该读取这个字段决定挂载哪个 CanvasLayer，不再写死数字。
    /// </summary>
    public RenderLayer WeaponLayer { get; }

    /// <summary>
    /// Trail 所在的 Render Layer，默认 <see cref="RenderLayer.WeaponTrail"/>。
    /// </summary>
    public RenderLayer TrailLayer { get; }
}
