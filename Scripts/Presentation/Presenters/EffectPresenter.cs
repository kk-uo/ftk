//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/Presenters/EffectPresenter.cs
//
// 模块：Presentation System
//
// 为什么存在：
// 攻击、治疗、护盾、Buff 等特效只表达视觉反馈，
// 不应该和伤害公式、Buff 层数或技能触发条件混在一起。
//
// 职责：
// 1. 定义攻击、治疗、护盾、Buff、Debuff 等特效表现接口。
// 2. 隔离特效资源选择和实例化。
// 3. 让 Damage、Skill、Equipment 只提交表现语义。
//
// 不负责：
// × 伤害倍率。
// × Buff 层数变化。
// × 特效资源加载策略。
//
// 主要依赖：
// PresentationEvent
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// 特效表现接口。
///
/// 该接口只描述要播放哪类表现，不决定效果是否命中或造成多少数值。
/// </summary>
public interface IEffectPresenter
{
    /// <summary>
    /// 播放攻击特效。
    /// </summary>
    void PresentAttackEffect(PresentationEvent presentationEvent);

    /// <summary>
    /// 播放命中特效。
    /// </summary>
    void PresentHitEffect(PresentationEvent presentationEvent);

    /// <summary>
    /// 播放治疗特效。
    /// </summary>
    void PresentHealEffect(PresentationEvent presentationEvent);

    /// <summary>
    /// 播放 Buff 或 Debuff 特效。
    /// </summary>
    void PresentStatusEffect(PresentationEvent presentationEvent);
}

/// <summary>
/// Phase 2 第一版具体实现：只处理"目标短暂提亮再恢复"这种最简单的受击闪烁。
///
/// 不涉及具体元素特效贴图、Buff/Debuff 表现——这些留给后续版本按需扩展，
/// 本版本刻意保持最小实现。
/// </summary>
public sealed class EffectPresenter : IEffectPresenter
{
    private const float HitFlashDurationSeconds = 0.1f;
    private static readonly Color HitFlashColor = new(1.6f, 1.6f, 1.6f, 1f);

    /// <summary>
    /// 攻击特效（挥砍轨迹、命中光效等）留给后续版本实现。
    /// </summary>
    public void PresentAttackEffect(PresentationEvent presentationEvent)
    {
    }

    /// <summary>
    /// 播放一次受击闪烁：把 <see cref="PresentationEvent.Payload"/> 指定目标的 Modulate
    /// 瞬间调亮，再用 Tween 还原为正常颜色。
    ///
    /// Payload 必须是一个有效的 CanvasItem（通常是角色状态卡片）；缺失时静默跳过，
    /// 不影响伤害结算。
    /// </summary>
    public void PresentHitEffect(PresentationEvent presentationEvent)
    {
        if (presentationEvent.Payload is not CanvasItem target || !GodotObject.IsInstanceValid(target))
        {
            return;
        }

        var originalModulate = target.Modulate;
        target.Modulate = HitFlashColor;

        var tween = target.GetTree()?.CreateTween();
        if (tween == null)
        {
            target.Modulate = originalModulate;
            return;
        }

        tween.TweenProperty(target, "modulate", originalModulate, HitFlashDurationSeconds);
    }

    /// <summary>
    /// 治疗特效留给后续版本实现。
    /// </summary>
    public void PresentHealEffect(PresentationEvent presentationEvent)
    {
    }

    /// <summary>
    /// Buff/Debuff 特效留给后续版本实现。
    /// </summary>
    public void PresentStatusEffect(PresentationEvent presentationEvent)
    {
    }
}
