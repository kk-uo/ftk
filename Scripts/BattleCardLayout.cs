//////////////////////////////////////////////////////////
// 文件：Scripts/BattleCardLayout.cs
//
// 模块：Card System
//
// 职责：
// 1. 承载卡牌定义、卡牌 UI 与卡牌规则相关代码。
// 2. 为其它模块提供清晰、稳定的调用边界。
// 3. 保持本文件内的状态变化可追踪、可调试。
//
// 不负责：
// × 处理无关模块的业务规则。
// × 绕过既有 Manager 或 Trigger 流程直接改写跨系统状态。
// × 在数据定义层混入表现层细节。
//
// 主要依赖：
// Godot / C# Runtime
// 项目内对应 Manager、Database 与 Trigger 系统
//////////////////////////////////////////////////////////

using Godot;

// Owns all container-level and card-level layout decisions for the battle HUD.
// BattleManager calls these helpers; it never sets CustomMinimumSize/SizeFlags itself.
// To change battle card sizing: edit the constants here and rebuild.
/// <summary>
/// Card System 的公开类：BattleCardLayout。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class BattleCardLayout
{
    // ── Design constants ─────────────────────────────────────────────────────
    public const int CardHeight = 190;
    public const int MinCardWidth = 180;
    public const int FriendlyCardHeight = 169;
    public const int FriendlyCardWidth = 356;
    // rightColWidth = cardWidth - margins(36) - avatar(78) - sep(14) = cardWidth - 128
    // badgeSlot = 26px icon + 4px sep = 30px; reserve 26px for overflow "…" badge
    private const int BadgeSlotWidth = 30;
    private const int BadgeReservedForOverflow = 26;
    private const int RightColOffset = 128;
    // ────────────────────────────────────────────────────────────────────────

    // ── Enemy card widths by count ───────────────────────────────────────────

    /// <summary>
    /// Card System 的公开入口：EnemyWidthForCount。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int EnemyWidthForCount(int count) => 360;

    /// <summary>
    /// Card System 的公开入口：EnemySeparationForCount。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int EnemySeparationForCount(int count) => count switch
    {
        1 => 28,
        2 => 24,
        3 => 18,
        4 => 14,
        _ => 10
    };

    /// <summary>
    /// Card System 的公开入口：MaxBadgesForWidth。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int MaxBadgesForWidth(int cardWidth)
        => Mathf.Clamp((cardWidth - RightColOffset - BadgeReservedForOverflow) / BadgeSlotWidth, 1, 6);

    // ── Container configuration ──────────────────────────────────────────────

    /// <summary>
    /// Card System 的公开入口：ConfigureEnemyContainer。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void ConfigureEnemyContainer(BoxContainer container, int count)
    {
        container.Alignment = count <= 1
            ? BoxContainer.AlignmentMode.End
            : BoxContainer.AlignmentMode.Begin;
        container.AddThemeConstantOverride("separation", 12);
    }

    // ── Card configuration ───────────────────────────────────────────────────

    /// <summary>
    /// Card System 的公开入口：ConfigureEnemyCard。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void ConfigureEnemyCard(CharacterStatusCard card, int count)
    {
        var width = EnemyWidthForCount(count);
        card.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        card.CustomMinimumSize = new Vector2(width, CardHeight);
        card.MaxStatusBadges = MaxBadgesForWidth(width);
    }

    /// <summary>
    /// Card System 的公开入口：ConfigureFriendlyCard。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void ConfigureFriendlyCard(CharacterStatusCard card, int count)
    {
        // 玩家状态 UI 的外层锚点是固定 HUD，不允许由 HBoxContainer 根据卡片内容重新定位。
        // 因此玩家卡必须拥有稳定宽高；HP 数字、费用、Buff 数量变化只能影响卡片内部，
        // 不能再通过 ExpandFill/最小宽度反向拉伸血条，导致受击刷新后越界。
        card.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        card.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        card.CustomMinimumSize = new Vector2(FriendlyCardWidth, count == 1 ? FriendlyCardHeight : CardHeight);
        card.Size = card.CustomMinimumSize;
        card.MaxStatusBadges = count == 1 ? 4 : 6;
    }
}
