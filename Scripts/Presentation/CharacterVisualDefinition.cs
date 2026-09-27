//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/CharacterVisualDefinition.cs
//
// 模块：Presentation System
//
// 为什么存在：
// CharacterData 只应该描述角色规则数据，不能承载头像、立绘、动作等表现资源。
// 该定义为角色视觉资源提供独立映射。
//
// 职责：
// 1. 描述角色头像、立绘和动作资源。
// 2. 将角色 ID 与表现资源解耦。
// 3. 为未来角色换皮、Boss 形态、特殊演出预留统一定义。
//
// 不负责：
// × 角色属性。
// × 角色技能。
// × 角色选择流程。
//
// 主要依赖：
// C# Runtime
//////////////////////////////////////////////////////////

/// <summary>
/// 角色视觉定义。
///
/// 角色逻辑数据仍由 CharacterData 管理，
/// 该类只描述表现层需要的头像、立绘和动作引用。
/// </summary>
public sealed class CharacterVisualDefinition
{
    /// <summary>
    /// 创建角色视觉定义。
    /// </summary>
    public CharacterVisualDefinition(
        string id,
        string portraitSpriteId = "",
        string battleSpriteId = "",
        string idleAnimationId = "",
        string attackAnimationId = "",
        string defenseAnimationId = "",
        string hitAnimationId = "",
        string deathAnimationId = "",
        string victoryAnimationId = "",
        string skillAnimationId = "",
        string mapSpriteId = "",
        RenderLayer characterLayer = RenderLayer.Character)
    {
        Id = id;
        PortraitSpriteId = portraitSpriteId;
        BattleSpriteId = battleSpriteId;
        IdleAnimationId = idleAnimationId;
        AttackAnimationId = attackAnimationId;
        DefenseAnimationId = defenseAnimationId;
        HitAnimationId = hitAnimationId;
        DeathAnimationId = deathAnimationId;
        VictoryAnimationId = victoryAnimationId;
        SkillAnimationId = skillAnimationId;
        MapSpriteId = mapSpriteId;
        CharacterLayer = characterLayer;
    }

    /// <summary>
    /// 角色视觉稳定 ID。
    ///
    /// 通常对应角色 ID 或敌人视觉 ID，但不直接引用 CharacterData。
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 头像图片资源 ID。
    ///
    /// 用于角色选择、状态面板、Tooltip 或事件展示。
    /// </summary>
    public string PortraitSpriteId { get; }

    /// <summary>
    /// 战斗立绘或战斗形象图片资源 ID。
    ///
    /// CharacterPresenter 可通过该 ID 决定战斗中显示的基础视觉。
    /// </summary>
    public string BattleSpriteId { get; }

    /// <summary>
    /// 待机动作动画 ID。
    /// </summary>
    public string IdleAnimationId { get; }

    /// <summary>
    /// 攻击动作动画 ID。
    ///
    /// 用于角色本体攻击动作，不包含武器或命中特效。
    /// </summary>
    public string AttackAnimationId { get; }

    /// <summary>
    /// 防御/格挡动作动画 ID。
    /// </summary>
    public string DefenseAnimationId { get; }

    /// <summary>
    /// 受击动作动画 ID。
    ///
    /// 用于角色被命中或受到影响时的表现反馈。
    /// </summary>
    public string HitAnimationId { get; }

    /// <summary>
    /// 死亡动作动画 ID（预留，当前没有 Presenter 读取该字段）。
    ///
    /// 用于角色战败、消失或倒下表现，不负责胜负判定。
    /// </summary>
    public string DeathAnimationId { get; }

    /// <summary>
    /// 胜利动作动画 ID（预留，当前没有 Presenter 读取该字段）。
    /// </summary>
    public string VictoryAnimationId { get; }

    /// <summary>
    /// 技能动作动画 ID（预留，当前没有 Presenter 读取该字段）。
    /// </summary>
    public string SkillAnimationId { get; }

    /// <summary>
    /// 地图探索角色精灵资源 ID。
    ///
    /// 该字段只服务地图探索表现，不影响角色战斗立绘、头像、技能或数值。
    /// </summary>
    public string MapSpriteId { get; }

    /// <summary>
    /// 该角色所在的统一 Render Layer，默认 <see cref="global::RenderLayer.Character"/>。
    ///
    /// 以后 Boss 可以指定更高的层级（例如 <see cref="global::RenderLayer.SkillEffect"/>
    /// 之上）来盖住普通角色/特效，不需要写死判断"如果是 Boss 就……"。
    /// </summary>
    public RenderLayer CharacterLayer { get; }
}
