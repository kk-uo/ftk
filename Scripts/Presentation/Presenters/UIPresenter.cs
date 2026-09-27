//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/Presenters/UIPresenter.cs
//
// 模块：Presentation System
//
// 为什么存在：
// 浮字、Buff 提示、屏幕震动属于玩家反馈，不属于战斗规则。
// 独立 UI Presenter 可以避免 Damage、Reward、Battle 直接操作界面节点。
//
// 职责：
// 1. 定义 UI 反馈表现接口。
// 2. 管理 Floating Text、Buff 提示、短提示、屏幕震动等表现入口。
// 3. 避免 Battle、Damage、Reward 直接操作具体 UI 节点。
//
// 不负责：
// × 计算 UI 文案。
// × 修改玩家状态。
// × 判断奖励是否合法。
//
// 主要依赖：
// PresentationEvent
//////////////////////////////////////////////////////////

/// <summary>
/// UI 表现接口。
///
/// 所有浮字、提示、Buff 图标反馈和震屏请求都应走该接口或 PresentationManager。
/// </summary>
public interface IUIPresenter
{
    /// <summary>
    /// 播放浮动文字。
    /// </summary>
    void PresentPopup(PresentationEvent presentationEvent);

    /// <summary>
    /// 播放 Buff 或状态提示。
    /// </summary>
    void PresentStatusHint(PresentationEvent presentationEvent);

    /// <summary>
    /// 播放屏幕震动。
    /// </summary>
    void PresentCameraShake(PresentationEvent presentationEvent);
}
