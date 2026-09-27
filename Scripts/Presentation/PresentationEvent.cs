//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/PresentationEvent.cs
//
// 模块：Presentation System
//
// 为什么存在：
// Battle、Event、Reward 等逻辑模块需要向表现层提交结果，
// 但不能暴露内部上下文或直接控制 Godot 节点。
//
// 职责：
// 1. 承载一次表现请求。
// 2. 解耦逻辑层与具体动画、音效、特效实现。
// 3. 为未来队列化、延迟播放、跳过动画提供统一数据入口。
//
// 不负责：
// × 执行战斗规则。
// × 查询伤害公式。
// × 直接实例化 Godot 节点或资源。
//
// 主要依赖：
// PresentationEventType
//////////////////////////////////////////////////////////

/// <summary>
/// 表现层事件。
///
/// 逻辑层只需要提交该事件，PresentationManager 再根据 Type 和附加数据
/// 转发给角色、武器、特效、UI 或交互 Presenter。
/// </summary>
public sealed class PresentationEvent
{
    /// <summary>
    /// 创建一次表现请求。
    ///
    /// 参数均保持通用字符串或数值，是为了避免表现层事件绑定战斗内部对象。
    /// 后续如需更强类型的数据，应通过新的表现层数据对象扩展，而不是暴露 BattleContext。
    /// </summary>
    public PresentationEvent(
        PresentationEventType type,
        string actorId = "",
        string targetId = "",
        string effectId = "",
        string weaponId = "",
        string text = "",
        float value = 0f,
        object? payload = null)
    {
        Type = type;
        ActorId = actorId;
        TargetId = targetId;
        EffectId = effectId;
        WeaponId = weaponId;
        Text = text;
        Value = value;
        Payload = payload;
    }

    /// <summary>
    /// 表现事件类型。
    ///
    /// PresentationManager 通过该字段判断事件应交给角色、武器、特效、交互还是 UI Presenter。
    /// </summary>
    public PresentationEventType Type { get; }

    /// <summary>
    /// 表现发起者 ID。
    ///
    /// 该字段通常对应角色、敌人、卡牌或事件来源的稳定 ID，
    /// 不要求表现层持有 BattleUnit 或其它逻辑对象引用。
    /// </summary>
    public string ActorId { get; }

    /// <summary>
    /// 表现目标 ID。
    ///
    /// 用于描述攻击、治疗、Buff、Debuff 等表现的目标，
    /// 具体目标节点由 Presenter 或 UI 层根据 ID 查找。
    /// </summary>
    public string TargetId { get; }

    /// <summary>
    /// 特效定义 ID。
    ///
    /// Presenter 应通过 EffectDatabase 查询该 ID，
    /// 而不是让逻辑层传入具体图片、动画或音效路径。
    /// </summary>
    public string EffectId { get; }

    /// <summary>
    /// 武器视觉定义 ID。
    ///
    /// 用于把装备或角色武器映射到表现资源，
    /// Battle 不应根据该字段计算任何战斗效果。
    /// </summary>
    public string WeaponId { get; }

    /// <summary>
    /// UI 提示文本。
    ///
    /// 该字段主要服务 Floating Text 或短提示，
    /// 正式玩家可见文本仍应由调用方提前完成本地化。
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// 通用表现数值。
    ///
    /// 可用于伤害数字、治疗数字、震屏强度等展示参数，
    /// 不应被表现层重新解释为规则计算输入。
    /// </summary>
    public float Value { get; }

    /// <summary>
    /// 可选扩展数据。
    ///
    /// 仅用于框架过渡或未来强类型表现数据接入。
    /// 不应长期传递 BattleContext、Player 等逻辑层内部对象。
    /// </summary>
    public object? Payload { get; }
}
