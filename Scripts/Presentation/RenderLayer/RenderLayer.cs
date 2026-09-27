//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/RenderLayer/RenderLayer.cs
//
// 模块：Presentation System / Render Layer System
//
// 为什么存在：
// 以前每个 Presenter（WeaponPresenter/EffectPlayer/MapNodeVisual）各自
// 写死一个 CanvasLayer.Layer 数字或 ZIndex 常量，互相之间的层级关系全凭
// 巧合对上。RenderLayer 用一个统一的枚举描述"这个东西应该显示在哪一层"，
// 数值本身就是最终的渲染顺序（Godot 的 CanvasLayer.Layer / Node2D.ZIndex
// 数值越大越靠前），所有 Presenter 改成读取这个枚举，不再各自定义数字。
//
// 职责：
// 1. 定义项目里全部已知的渲染层级，从背景到调试信息，从底到顶排列。
// 2. 层级本身的排列已经保证："普通特效（SkillEffect/HitEffect）" 低于
//    "HUD/Tooltip/Modal" 这类常规 UI，但 "ScreenEffect/FullscreenEffect/Fade"
//    这类全屏演出层级高于所有 UI，允许 Boss 技能、过场动画盖住 HUD——
//    UI 不再是"永远最高层"。
//
// 不负责：
// × 决定某个具体资源应该用哪一层（由各自的 Definition/Database 决定）。
// × 播放任何表现。
//
// 主要依赖：
// 无
//////////////////////////////////////////////////////////

/// <summary>
/// 统一渲染层级。数值代表最终渲染顺序（越大越靠前/越靠上），
/// 所有 Presentation 相关的图片、动画、特效、UI 都应该通过这个枚举决定层级，
/// 不要在各自的代码里写死 ZIndex 或 CanvasLayer.Layer 数字。
/// </summary>
public enum RenderLayer
{
    /// <summary>背景（例如战斗/地图的固定背景图）。</summary>
    Background = 0,

    /// <summary>地面/地形装饰。</summary>
    Ground = 10,

    /// <summary>角色本体（立绘、战斗形象）。</summary>
    Character = 20,

    /// <summary>武器本体。</summary>
    Weapon = 30,

    /// <summary>武器挥砍拖尾。</summary>
    WeaponTrail = 40,

    /// <summary>技能/元素特效（斩击、火焰、雷电……）。</summary>
    SkillEffect = 50,

    /// <summary>命中反馈特效。</summary>
    HitEffect = 60,

    /// <summary>世界空间里的浮动文字/图标（伤害数字、"E 进入"提示等）。</summary>
    WorldPopup = 70,

    /// <summary>世界空间里的交互式 UI（地图节点标签等）。</summary>
    WorldUI = 80,

    /// <summary>常规屏幕 HUD（资源、Buff 面板、按钮等）。</summary>
    HUD = 90,

    /// <summary>Tooltip 悬浮提示。</summary>
    Tooltip = 100,

    /// <summary>模态弹窗（确认框、调试窗口等）。</summary>
    Modal = 110,

    /// <summary>局部全屏反馈（例如震屏时的边缘暗角）。</summary>
    ScreenEffect = 120,

    /// <summary>
    /// 完整全屏演出（Boss 大招、剧情/过场动画）——刻意高于 HUD/Tooltip/Modal，
    /// 允许这类演出盖住所有常规 UI。
    /// </summary>
    FullscreenEffect = 130,

    /// <summary>转场淡入淡出，永远在最上层（Fade 之上只有 Debug）。</summary>
    Fade = 140,

    /// <summary>调试信息，永远最上层，方便任何时候都能看到调试内容。</summary>
    Debug = 150
}
