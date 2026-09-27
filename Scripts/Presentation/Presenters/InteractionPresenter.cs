//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/Presenters/InteractionPresenter.cs
//
// 模块：Presentation System
//
// 为什么存在：
// 某些演出需要同时协调角色、武器、特效和 UI。
// 交互 Presenter 负责这种组合编排，避免 Battle 同时操作多个表现模块。
//
// 职责：
// 1. 定义双方交互表现接口。
// 2. 表达攻击者、目标、卡牌、特效之间的组合演出。
// 3. 为后续队列化、连击、打断、HitPause 预留统一入口。
//
// 不负责：
// × 判断攻击是否合法。
// × 决定双方卡牌克制关系。
// × 修改战斗阶段。
//
// 主要依赖：
// PresentationEvent
//////////////////////////////////////////////////////////

/// <summary>
/// 交互表现接口。
///
/// 当一个表现需要同时协调角色、武器、特效和 UI 时，
/// 应由 InteractionPresenter 编排，而不是让 Battle 直接操作多个 Presenter。
/// </summary>
public interface IInteractionPresenter
{
    /// <summary>
    /// 播放攻击交互表现。
    /// </summary>
    void PresentAttackInteraction(PresentationEvent presentationEvent);

    /// <summary>
    /// 播放格挡交互表现。
    /// </summary>
    void PresentBlockInteraction(PresentationEvent presentationEvent);

    /// <summary>
    /// 播放死亡或胜利交互表现。
    /// </summary>
    void PresentFinishInteraction(PresentationEvent presentationEvent);
}
