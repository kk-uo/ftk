//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/SkillTriggerVisualEffect.cs
//
// 模块：Presentation System
//
// 职责：
// 1. 定义技能触发大字的可扩展视觉特效接口。
// 2. 向特效提供只读表现请求与统一动画时长。
// 3. 隔离技能规则和具体视觉实现。
//
// 不负责：
// × 判断技能是否触发。
// × 修改技能或战斗状态。
// × 管理技能提示播放队列。
//
// 主要依赖：
// SkillTriggerPresentationRequest
// Godot Control
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// 一次技能大字播放时提供给附加视觉特效的只读上下文。
///
/// 后续新增粒子、角色专属纹章或音效同步时，只需实现
/// <see cref="ISkillTriggerVisualEffect"/>，无需修改技能与 Trigger 代码。
/// </summary>
public sealed record SkillTriggerVisualEffectContext(
    SkillTriggerPresentationRequest Request,
    Vector2 DisplaySize,
    bool Simplified,
    float EnterDuration,
    float HoldDuration,
    float ExitDuration);

/// <summary>
/// 技能触发大字的附加表现接口。
///
/// 实现只允许操作自身创建的表现节点，不应读取或写入战斗数据。
/// </summary>
public interface ISkillTriggerVisualEffect
{
    /// <summary>
    /// 将特效节点挂载到技能提示的专用 Overlay。
    /// </summary>
    void Attach(Control host);

    /// <summary>
    /// 播放一次与技能提示同步的视觉特效。
    /// </summary>
    void Play(SkillTriggerVisualEffectContext context);

    /// <summary>
    /// 停止动画并恢复透明状态。
    /// </summary>
    void Stop();

    /// <summary>
    /// 从技能提示 Overlay 移除自身节点。
    /// </summary>
    void Detach();
}
