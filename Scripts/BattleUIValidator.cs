//////////////////////////////////////////////////////////
// 文件：Scripts/BattleUIValidator.cs
//
// 模块：Core System
//
// 职责：
// 1. 承载核心数据结构、通用规则与跨模块协作相关代码。
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
using System.Collections.Generic;

// Validates CharacterStatusCard instances after battle setup.
// Called once per battle start. Logs GD.PrintErr for any layout or data anomaly.
// No exceptions thrown — errors are non-fatal to allow inspection in-game.
/// <summary>
/// Core System 的公开类：BattleUIValidator。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class BattleUIValidator
{
    /// <summary>
    /// Core System 的公开入口：Validate。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void Validate(
        IReadOnlyList<CharacterStatusCard> friendlyCards,
        IReadOnlyList<CharacterStatusCard> enemyCards)
    {
        ValidateCardList("Player", friendlyCards, expectedMinCount: 1);
        ValidateCardList("Enemy", enemyCards, expectedMinCount: 1);
    }

    private static void ValidateCardList(
        string label,
        IReadOnlyList<CharacterStatusCard> cards,
        int expectedMinCount)
    {
        if (cards.Count < expectedMinCount)
        {
            GD.PrintErr($"[BattleUIValidator] {label}: expected ≥{expectedMinCount} card(s), found {cards.Count}");
            return;
        }

        for (var i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            var prefix = $"[BattleUIValidator] {label}[{i}]";

            if (!GodotObject.IsInstanceValid(card))
            {
                GD.PrintErr($"{prefix}: card node is invalid (freed or null)");
                continue;
            }

            var w = card.CustomMinimumSize.X;
            var h = card.CustomMinimumSize.Y;

            if (w < BattleCardLayout.MinCardWidth)
                GD.PrintErr($"{prefix}: CustomMinimumSize.X={w} is below MinCardWidth={BattleCardLayout.MinCardWidth} — card may collapse");

            if (h < BattleCardLayout.CardHeight)
                GD.PrintErr($"{prefix}: CustomMinimumSize.Y={h} is below CardHeight={BattleCardLayout.CardHeight}");

            if (card.SizeFlagsHorizontal == Control.SizeFlags.ExpandFill
                && w == 0)
                GD.PrintErr($"{prefix}: SizeFlagsHorizontal=ExpandFill with MinWidth=0 inside a ShrinkBegin container — layout will collapse to 0px");
        }

        GD.Print($"[BattleUIValidator] {label}: {cards.Count} card(s) validated OK");
    }
}
