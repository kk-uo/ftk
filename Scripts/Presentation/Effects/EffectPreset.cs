//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/Effects/EffectPreset.cs
//
// 模块：Presentation System / Effect System
//
// 为什么存在：
// 一个技能通常不是只播放一个效果：火杀可能是 挥砍 + 火焰 + 受击 + 震屏 的组合，
// Boss 技能可能是 雷电 + 闪屏 + 顿帧 + 震屏 的组合。EffectPreset 把“一组效果
// 应该一起播放”这件事也变成数据，避免以后在 Battle 或 Presenter 里手写
// “先播 A 再播 B 再播 C”的组合代码。
//
// 职责：
// 1. 用一个稳定 ID 描述一组要同时播放的 EffectId。
// 2. 让 EffectPlayer.PlayPreset 只读取这份组合列表并依次调用 Play，不需要
//    为每一种技能组合写专属播放代码。
//
// 不负责：
// × 播放效果本身（由 EffectPlayer 负责）。
// × 决定技能触发条件（由 Battle/Skill 系统负责）。
//
// 主要依赖：
// System.Collections.Generic
//////////////////////////////////////////////////////////

using System.Collections.Generic;

/// <summary>
/// 一组效果的组合定义。
///
/// 例如火杀 = ["slash_default", "fire_default", "hit_default", "camera_shake_default"]；
/// 新增一个技能组合只需要注册一条 <see cref="EffectPreset"/>，不需要修改 EffectPlayer。
/// </summary>
public sealed class EffectPreset
{
    /// <summary>
    /// 创建一份效果组合定义。
    /// </summary>
    public EffectPreset(string presetId, IReadOnlyList<string> effectIds)
    {
        PresetId = presetId;
        EffectIds = effectIds;
    }

    /// <summary>
    /// 组合稳定 ID，例如 "fire_kill_combo"。
    /// </summary>
    public string PresetId { get; }

    /// <summary>
    /// 该组合按顺序包含的 EffectId 列表。
    ///
    /// EffectPlayer.PlayPreset 会依次对每个 ID 调用 Play，播放顺序即列表顺序；
    /// 各效果之间目前没有延迟或依赖关系，如果以后需要“先后播放、中间等待”，
    /// 应该在 EffectPreset 或调用方扩展时间信息，而不是让 EffectPlayer 猜测。
    /// </summary>
    public IReadOnlyList<string> EffectIds { get; }
}
