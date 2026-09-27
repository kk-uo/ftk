//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/SkillTriggerPresentationRequest.cs
//
// 模块：Presentation System
//
// 职责：
// 1. 描述一次已经由规则层确认生效的角色技能表现请求。
// 2. 为 Trigger 与 Battle UI 提供稳定的强类型边界。
// 3. 携带同一结算链去重所需的标识。
//
// 不负责：
// × 判断技能是否满足触发条件。
// × 修改战斗数值。
// × 播放具体 UI 动画。
//
// 主要依赖：
// BattleTeam
// TriggerTiming
// EffectPriority
//////////////////////////////////////////////////////////

/// <summary>
/// 一次已经确认产生实际效果的角色技能表现请求。
///
/// 规则层只负责创建请求；表现层只读取请求，不反向推断战斗结果。
/// </summary>
public sealed record SkillTriggerPresentationRequest(
    BattleTeam Side,
    string ActorId,
    string SkillId,
    string LocalizedSkillName,
    TriggerTiming Timing,
    EffectPriority? Priority,
    long ResolutionChainId,
    string Variant = "");

/// <summary>
/// 角色技能大字表现的可访问性等级。
/// </summary>
public enum SkillTriggerPresentationMode
{
    Full,
    Simplified,
    Off
}
