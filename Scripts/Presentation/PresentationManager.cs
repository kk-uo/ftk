//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/PresentationManager.cs
//
// 模块：Presentation System
//
// 为什么存在：
// 项目需要一个唯一入口接收所有表现请求。
// 这样 Battle 只发送 PresentationEvent，不需要知道动画、音效、特效如何实现。
//
// 职责：
// 1. 作为项目唯一表现层调度中心。
// 2. 接收 PresentationEvent，并在未来分发给具体 Presenter。
// 3. 为攻击、受击、格挡、治疗、Buff、死亡、胜利、特效、武器、浮字和震屏预留统一入口。
//
// 不负责：
// × 计算伤害。
// × 决定卡牌是否合法。
// × 修改 Trigger、Reward、Inventory、Shop、Event 状态。
// × 在当前阶段播放真实动画。
// × 设置任何具体的 ZIndex/CanvasLayer 数字——本类自身不实例化节点，
//   实际创建节点的 Presenter（WeaponPresenter/EffectPlayer 等）都通过
//   RenderLayerManager 按 Render Layer System（Scripts/Presentation/RenderLayer/）
//   统一决定层级，不在各自代码里写死数字。
//
// 主要依赖：
// PresentationEvent
// PresentationConfig
// Presenters
// RenderLayerManager（由具体 Presenter 使用，PresentationManager 本身不直接依赖）
//////////////////////////////////////////////////////////

/// <summary>
/// 表现层调度中心。
///
/// 逻辑层未来只提交表现事件或调用这些高层表现入口，
/// 具体动画、音效、特效和 UI 反馈由 Presenter 实现。
/// 当前类只建立边界，不播放任何真实动画。
/// </summary>
public static class PresentationManager
{
    // Phase 2 第一版具体实现：只有武器和命中表现真正接了对应 Presenter，
    // 其余高层入口仍保持 Phase 1 的空实现，等到对应表现被实际需要时再接入。
    //
    // 命中表现（PlayHit）第一轮架构整理后统一改走 EffectPlayer（Effect System 的
    // 唯一播放入口），不再实例化 EffectPresenter——避免"一部分特效走 EffectPresenter，
    // 一部分走 EffectPlayer"。EffectPresenter.cs 本身作为文件保留（未删除，属于
    // 下一轮整理的清理范围），但从这里开始不再被调用。
    private static readonly IWeaponPresenter WeaponPresenterInstance = new WeaponPresenter();

    /// <summary>
    /// 当前表现层配置。
    ///
    /// 所有 Presenter 后续都应从这里读取动画时长、震屏强度和浮字时间等参数，
    /// 避免在具体表现实现中写死数值。
    /// </summary>
    public static PresentationConfig Config { get; private set; } = PresentationConfig.CreateDefault();

    /// <summary>
    /// 替换表现层配置。
    ///
    /// 该入口用于 Debug 调参或未来读取配置文件，不应影响战斗规则。
    /// </summary>
    public static void Configure(PresentationConfig config)
    {
        Config = config;
    }

    /// <summary>
    /// 接收一次表现层事件。
    ///
    /// 当前版本只保留事件入口，不做真实分发。
    /// 后续接入动画系统时应在这里根据事件类型转发到对应 Presenter。
    /// </summary>
    public static void Publish(PresentationEvent presentationEvent)
    {
    }

    /// <summary>
    /// 播放攻击表现。
    ///
    /// 只表示“攻击表现请求”，不代表攻击一定造成伤害。
    /// </summary>
    public static void PlayAttack(PresentationEvent presentationEvent)
    {
    }

    /// <summary>
    /// 播放受击表现。
    ///
    /// 只负责表现命中反馈，最终生命变化必须来自 Damage 系统。统一通过
    /// EffectPlayer 播放 <see cref="EffectDatabase.HitFlashDefaultEffectId"/>
    /// （效果内容和原来 EffectPresenter 的受击闪烁完全一致，只是播放入口改了）。
    /// </summary>
    public static void PlayHit(PresentationEvent presentationEvent)
    {
        EffectPlayer.Play(EffectDatabase.HitFlashDefaultEffectId, presentationEvent.Payload as Godot.Control);
    }

    /// <summary>
    /// 播放格挡或防御表现。
    ///
    /// 是否成功格挡由逻辑层决定，表现层只消费结果。
    /// </summary>
    public static void PlayBlock(PresentationEvent presentationEvent)
    {
        EffectPlayer.PlayBlock(presentationEvent.Payload as Godot.Control);
    }

    /// <summary>
    /// 播放治疗表现。
    ///
    /// 治疗数值已由逻辑层结算，表现层不重新计算。
    /// </summary>
    public static void PlayHeal(PresentationEvent presentationEvent)
    {
    }

    /// <summary>
    /// 播放 Buff 表现。
    ///
    /// Buff 生命周期仍由 RunBuff、BattleBuff 或 Trigger 系统维护。
    /// </summary>
    public static void PlayBuff(PresentationEvent presentationEvent)
    {
    }

    /// <summary>
    /// 播放死亡表现。
    ///
    /// 胜负判定已经由 Battle 处理，表现层只负责最终反馈。
    /// </summary>
    public static void PlayDeath(PresentationEvent presentationEvent)
    {
    }

    /// <summary>
    /// 播放胜利表现。
    ///
    /// 奖励发放不应该写在该入口中。
    /// </summary>
    public static void PlayVictory(PresentationEvent presentationEvent)
    {
    }

    /// <summary>
    /// 播放通用特效表现。
    ///
    /// 具体特效资源应由 EffectDatabase 决定。
    /// </summary>
    public static void PlayEffect(PresentationEvent presentationEvent)
    {
        if (presentationEvent.EffectId == ArrowBarrageEffectVisual.EffectId
            && presentationEvent.Payload is ArrowBarragePresentationRequest arrowBarrageRequest)
        {
            ArrowBarrageEffectVisual.Play(arrowBarrageRequest);
        }
    }

    /// <summary>
    /// 播放武器表现。
    ///
    /// 武器视觉资源应由 WeaponVisualDatabase 决定，Battle 不关心具体武器节点。
    /// </summary>
    public static void PlayWeapon(PresentationEvent presentationEvent)
    {
        WeaponPresenterInstance.PresentWeaponAttack(presentationEvent);
    }

    /// <summary>
    /// 播放 UI 浮字或提示。
    ///
    /// 该入口用于伤害数字、治疗数字、资源变化和短提示。
    /// </summary>
    public static void PlayPopup(PresentationEvent presentationEvent)
    {
    }

    /// <summary>
    /// 播放镜头震动表现。
    ///
    /// 震动强度和时间应来自 PresentationConfig 或事件参数。
    /// </summary>
    public static void PlayCameraShake(PresentationEvent presentationEvent)
    {
    }
}
