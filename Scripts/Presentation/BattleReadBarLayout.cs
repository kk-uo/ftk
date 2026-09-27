//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/BattleReadBarLayout.cs
//
// 模块：Presentation
//
// 职责：
// 1. 统一战斗读条在 Battle Stage 中的锚点与边距。
// 2. 保证读条始终位于出牌区中央上方。
// 3. 为后续新增读条提供同一套布局入口。
//
// 不负责：
// × 控制读条进度。
// × 决定读条何时显示或隐藏。
// × 修改出牌区布局。
//
// 主要依赖：
// Godot Control
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// 战斗读条的统一布局规则。
///
/// 读条挂在与 Battle Stage 同尺寸的表现层中，因此锚定父节点底部，
/// 就能稳定停在出牌区上沿，而不受战场高度或卡牌数量变化影响。
/// </summary>
public static class BattleReadBarLayout
{
    /// <summary>
    /// 读条底边与出牌区上沿之间的固定间距。
    /// </summary>
    public const float BottomGap = 24.0f;

    /// <summary>
    /// 将读条固定到父表现层的底部中央。
    /// </summary>
    /// <param name="control">需要定位的读条根控件。</param>
    /// <param name="size">读条的固定显示尺寸。</param>
    public static void PlaceAboveActionArea(Control control, Vector2 size)
    {
        control.AnchorLeft = 0.5f;
        control.AnchorRight = 0.5f;
        control.AnchorTop = 1.0f;
        control.AnchorBottom = 1.0f;
        control.OffsetLeft = -size.X * 0.5f;
        control.OffsetRight = size.X * 0.5f;
        control.OffsetTop = -BottomGap - size.Y;
        control.OffsetBottom = -BottomGap;
        control.GrowHorizontal = Control.GrowDirection.Both;
        control.GrowVertical = Control.GrowDirection.Begin;
    }
}
