//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/PresentationEventType.cs
//
// 模块：Presentation System
//
// 为什么存在：
// 表现层需要一套不依赖 Battle、Damage、Trigger 的事件语言。
// 该枚举把“规则结果”转换成“表现请求”，避免逻辑层直接调用动画函数。
//
// 职责：
// 1. 定义表现层事件类型。
// 2. 为 Battle、Event、UI 等逻辑模块提供稳定的表现请求语义。
// 3. 让表现层可以在不理解战斗计算细节的前提下播放反馈。
//
// 不负责：
// × 计算伤害。
// × 推进战斗阶段。
// × 修改玩家、敌人、奖励或背包状态。
// × 绑定具体动画资源。
//
// 主要依赖：
// C# Runtime
// PresentationEvent
//////////////////////////////////////////////////////////

/// <summary>
/// 表现层事件类型。
///
/// 这些类型描述“发生了什么需要表现”，不描述“规则为什么成立”。
/// 具体动画、音效、特效由 PresentationManager 和 Presenter 决定。
/// </summary>
public enum PresentationEventType
{
    None,
    BattleStart,
    BattleEnd,
    CardPlayed,
    AttackResolved,
    HitResolved,
    BlockResolved,
    HealResolved,
    DamageResolved,
    BuffApplied,
    DebuffApplied,
    DeathResolved,
    VictoryResolved,
    EffectRequested,
    WeaponRequested,
    PopupRequested,
    CameraShakeRequested
}
