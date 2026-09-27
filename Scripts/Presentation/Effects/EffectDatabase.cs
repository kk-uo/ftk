//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/Effects/EffectDatabase.cs
//
// 模块：Presentation System / Effect System
//
// 为什么存在：
// 效果会被卡牌、技能、装备、事件表现共同复用。集中注册可以保证新增效果时
// 只维护这一份表现库，不反向修改战斗逻辑，也不需要改动 EffectPlayer。
//
// 职责：
// 1. 统一注册所有 EffectDefinition（单个效果）和 EffectPreset（效果组合）。
// 2. 为 EffectPlayer 提供按 ID 查询的入口。
// 3. 防止业务代码直接散落特效资源路径或组合逻辑。
//
// 不负责：
// × 实例化效果节点（由 EffectPlayer 负责）。
// × 判断效果触发条件（由 Battle/PresentationManager 负责）。
// × 战斗规则、伤害计算。
//
// 主要依赖：
// EffectDefinition / EffectPreset
//////////////////////////////////////////////////////////

using System.Collections.Generic;

/// <summary>
/// 效果与效果组合数据库。
///
/// 当前只注册了一个"默认挥砍效果"（slash_default），作为整个 Effect System 的
/// 第一条验证链路；火杀/雷杀/冰杀/桃/酒/Buff/Debuff/Boss 技能等留给后续按需注册，
/// 注册方式完全一样，不需要修改这个类或 EffectPlayer。
/// </summary>
public static class EffectDatabase
{
    /// <summary>默认挥砍效果 ID，配合 <see cref="EffectPlayer"/> 验证整条播放链路。</summary>
    public const string SlashDefaultEffectId = "slash_default";
    public const string FireSlashEffectId = "fire_slash";
    public const string ThunderSlashEffectId = "thunder_slash";

    private const string FireSlashSpriteId = "effect_fire_slash";
    private const string ThunderSlashSpriteId = "effect_thunder_slash";

    /// <summary>
    /// 默认受击闪烁效果 ID：迁移自原 <c>EffectPresenter.PresentHitEffect</c>，
    /// 是 <see cref="PresentationManager.PlayHit"/> 现在统一调用 EffectPlayer 时使用的效果。
    /// </summary>
    public const string HitFlashDefaultEffectId = "hit_flash_default";

    /// <summary>
    /// 【战场崩坏】环境伤害的统一表现效果 ID：BattleManager 在第48回合起、每次
    /// OnTurnEnd 真实伤害结算后，对每个受到伤害的存活单位调用一次。当前复用
    /// FlashTarget 闪烁（和受击闪烁视觉一致，只是数值来源不同），只是先占住一个
    /// 独立的 EffectId——以后要加屏幕震动/背景警报/扫描线，只需要在这里给这条
    /// 定义补充字段或扩展 EffectType，不需要改 BattleManager 的调用点。
    /// </summary>
    public const string BattlefieldCollapseDamageEffectId = "battlefield_collapse_damage";

    private static readonly Dictionary<string, EffectDefinition> Definitions = new();
    private static readonly Dictionary<string, EffectPreset> Presets = new();

    static EffectDatabase()
    {
        SpriteDatabase.Register(FireSlashSpriteId, "res://Assets/Effects/Fire/effect_fire_slash.png");
        SpriteDatabase.Register(ThunderSlashSpriteId, "res://Assets/Effects/Thunder/effect_thunder_slash.png");

        // 先只注册一个默认 Slash 效果用于验证架构；SpriteId 暂时留空（项目里还没有专门
        // 的斩击特效贴图），EffectPlayer 会在贴图缺失时静默跳过，不影响其它已完成表现。
        // 以后有真实斩击贴图时，只需要在这里补上 spriteId，不需要改 EffectPlayer。
        Register(new EffectDefinition(
            effectId: SlashDefaultEffectId,
            effectType: EffectType.Slash,
            duration: 0.12f,
            fadeOut: 0.10f,
            notes: "Effect System 验证链路：默认挥砍效果，尚未接入真实贴图。"));

        Register(new EffectDefinition(
            effectId: FireSlashEffectId,
            effectType: EffectType.Fire,
            spriteId: FireSlashSpriteId,
            duration: 0.48f,
            scale: 0.48f,
            fadeIn: 0.04f,
            fadeOut: 0.18f,
            zIndex: 22,
            notes: "火杀复用当前武器挥砍，并在同一战场位置叠加低像素火焰弧。"));

        Register(new EffectDefinition(
            effectId: ThunderSlashEffectId,
            effectType: EffectType.Thunder,
            spriteId: ThunderSlashSpriteId,
            duration: 0.48f,
            scale: 0.48f,
            fadeIn: 0.04f,
            fadeOut: 0.18f,
            zIndex: 22,
            notes: "雷杀复用当前武器挥砍，并在同一战场位置叠加低像素雷电弧。"));

        // 默认受击闪烁：flashTarget = true，不需要 SpriteId，直接把目标自身的 Modulate
        // 调亮再还原，数值（0.1 秒 / (1.6,1.6,1.6,1)）和原 EffectPresenter 完全一致，
        // 保证从 EffectPresenter 迁移到 EffectPlayer 之后视觉效果不变。
        Register(new EffectDefinition(
            effectId: HitFlashDefaultEffectId,
            effectType: EffectType.Custom,
            fadeOut: 0.1f,
            flashTarget: true,
            notes: "默认受击闪烁效果，迁移自原 EffectPresenter.PresentHitEffect，PresentationManager.PlayHit 使用。"));

        // 【战场崩坏】环境伤害：暂时复用闪烁表现，只是单独占一个 EffectId 作为
        // 以后接入屏幕震动/背景警报/扫描线的扩展点，不影响当前视觉。
        Register(new EffectDefinition(
            effectId: BattlefieldCollapseDamageEffectId,
            effectType: EffectType.Custom,
            fadeOut: 0.15f,
            flashTarget: true,
            notes: "【战场崩坏】环境真实伤害的统一表现效果，当前复用闪烁，预留后续扩展（震动/警报/扫描线）。"));
    }

    /// <summary>
    /// 注册一份效果定义。
    /// </summary>
    public static void Register(EffectDefinition definition)
    {
        Definitions[definition.EffectId] = definition;
    }

    /// <summary>
    /// 按 ID 查询效果定义；未注册时返回 null，调用方应静默跳过，不影响战斗结算。
    /// </summary>
    public static EffectDefinition? Get(string id)
    {
        return Definitions.TryGetValue(id, out var definition) ? definition : null;
    }

    /// <summary>
    /// 返回当前已注册的全部效果定义。
    /// </summary>
    public static IReadOnlyCollection<EffectDefinition> GetAll()
    {
        return Definitions.Values;
    }

    /// <summary>
    /// 注册一份效果组合定义（例如"火杀 = 挥砍 + 火焰 + 受击 + 震屏"）。
    /// </summary>
    public static void RegisterPreset(EffectPreset preset)
    {
        Presets[preset.PresetId] = preset;
    }

    /// <summary>
    /// 按 ID 查询效果组合定义；未注册时返回 null。
    /// </summary>
    public static EffectPreset? GetPreset(string id)
    {
        return Presets.TryGetValue(id, out var preset) ? preset : null;
    }
}
