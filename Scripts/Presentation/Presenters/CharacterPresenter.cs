//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/Presenters/CharacterPresenter.cs
//
// 模块：Presentation System
//
// 为什么存在：
// 角色头像、立绘、动作、受击和死亡演出会随美术方案变化。
// 独立 Presenter 可以让 Battle 不直接依赖角色节点结构。
//
// 职责：
// 1. 定义角色表现接口。
// 2. 隔离角色立绘、头像、动作、受击和死亡表现。
// 3. 让 Battle 只提交表现事件，不关心角色节点如何播放。
//
// 不负责：
// × 角色属性计算。
// × 技能触发。
// × 胜负判定。
//
// 主要依赖：
// PresentationEvent
//////////////////////////////////////////////////////////

/// <summary>
/// 角色表现接口。
///
/// 后续具体实现可以使用 Sprite、AnimatedSprite、Spine 或 3D 节点，
/// 但调用方只依赖该接口描述的表现能力。
/// </summary>
public interface ICharacterPresenter
{
    /// <summary>
    /// 播放角色进入战斗表现。
    /// </summary>
    void PresentEnter(PresentationEvent presentationEvent);

    /// <summary>
    /// 播放角色攻击动作表现。
    /// </summary>
    void PresentAttack(PresentationEvent presentationEvent);

    /// <summary>
    /// 播放角色受击表现。
    /// </summary>
    void PresentHit(PresentationEvent presentationEvent);

    /// <summary>
    /// 播放角色治疗或恢复表现。
    /// </summary>
    void PresentHeal(PresentationEvent presentationEvent);

    /// <summary>
    /// 播放角色死亡表现。
    /// </summary>
    void PresentDeath(PresentationEvent presentationEvent);
}
