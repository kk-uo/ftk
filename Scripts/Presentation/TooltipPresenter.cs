//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/TooltipPresenter.cs
//
// 模块：Presentation System
//
// 职责：
// 1. 统一 Battle 中战斗单位 Hover 的 Tooltip 内容。
// 2. 将名称、生命、费用、Buff、装备、技能、简介集中到一个入口。
// 3. 复用 TooltipManager 的跟随鼠标与屏幕边界避让能力。
//
// 不负责：
// × 修改任何战斗数据。
// × 处理目标选择或高亮。
// × 给生命条、费用、Buff 等子控件分别注册 Hover。
//////////////////////////////////////////////////////////

using Godot;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Battle 战斗单位 Hover 的统一 Tooltip Presenter。
///
/// 以后 Tooltip 新增字段时只改这里，不在各个 UI 控件中分散实现。
/// </summary>
public static class TooltipPresenter
{
    /// <summary>
    /// 显示战斗单位 Hover Tooltip。
    /// </summary>
    public static void ShowUnit(BattleUnit unit, BattleContext? context, Control source)
    {
        TooltipManager.Show(BuildUnitTooltipText(unit, context), source, TooltipManager.BattleUnitTextWidth);
    }

    /// <summary>
    /// 显示敌人 Hover Tooltip。
    /// </summary>
    public static void ShowEnemy(BattleUnit unit, BattleContext? context, Control source)
    {
        ShowUnit(unit, context, source);
    }

    /// <summary>
    /// 显示一组敌人的 Hover Tooltip。
    ///
    /// 主要用于共享血池或多主体 Boss：UI 上只有一个顶部 Boss 状态条，
    /// 但玩家仍需要一次性查看所有 Boss 单位的技能、装备与 Buff。
    /// </summary>
    public static void ShowEnemies(IReadOnlyList<EnemyInstance> enemies, BattleContext? context, Control source)
    {
        if (enemies.Count == 0)
        {
            Hide();
            return;
        }

        if (enemies.Count == 1)
        {
            ShowEnemy(enemies[0], context, source);
            return;
        }

        var builder = new StringBuilder();
        for (var i = 0; i < enemies.Count; i++)
        {
            if (i > 0)
            {
                builder.AppendLine();
                builder.AppendLine("────────────────");
            }

            builder.Append(BuildUnitTooltipText(enemies[i], context));
            builder.AppendLine();
        }

        TooltipManager.Show(builder.ToString().TrimEnd(), source, TooltipManager.BattleUnitTextWidth);
    }

    /// <summary>
    /// 隐藏当前 Tooltip。
    /// </summary>
    public static void Hide()
    {
        TooltipManager.Hide();
    }

    private static string BuildUnitTooltipText(BattleUnit unit, BattleContext? context)
    {
        var builder = new StringBuilder();
        builder.AppendLine(unit.Name);
        builder.AppendLine(Localization.GetFmt("battle.enemy_status.hp_tooltip_fmt", unit.CurrentHP, unit.MaxHP));
        builder.AppendLine(Localization.GetFmt("battle.enemy_status.mana_tooltip_fmt", BattleRules.FormatMana(unit.Resource)));

        AppendStatuses(builder, unit, context);
        AppendEquipments(builder, unit);
        AppendSkills(builder, unit);
        AppendIntro(builder, unit);

        return builder.ToString().TrimEnd();
    }

    private static void AppendStatuses(StringBuilder builder, BattleUnit unit, BattleContext? context)
    {
        builder.AppendLine();
        builder.AppendLine(Localization.Get("battle.hover.tab_status"));
        var statuses = BattleUnitUiFormatter.GetStatuses(unit, context);
        if (statuses.Count == 0)
        {
            builder.AppendLine(Localization.Get("battle.hover.none"));
            return;
        }

        foreach (var status in statuses)
        {
            var stackText = status.Stackable && status.StackCount > 1 ? $" ×{status.StackCount}" : string.Empty;
            builder.AppendLine($"{status.IconText} {status.Name}{stackText} - {status.Description}");
        }
    }

    private static void AppendEquipments(StringBuilder builder, BattleUnit unit)
    {
        builder.AppendLine();
        builder.AppendLine(Localization.Get("battle.hover.tab_equip"));
        var equipments = BattleUnitUiFormatter.GetEquipments(unit);
        if (equipments.Count == 0)
        {
            builder.AppendLine(Localization.Get("battle.hover.none"));
            return;
        }

        foreach (var equipment in equipments)
        {
            builder.AppendLine($"{equipment.Name} [{equipment.Rarity}] - {equipment.Description}");
        }
    }

    private static void AppendSkills(StringBuilder builder, BattleUnit unit)
    {
        builder.AppendLine();
        builder.AppendLine(Localization.Get("battle.hover.tab_skills"));
        var skills = BattleUnitUiFormatter.GetSkills(unit);
        if (skills.Count == 0)
        {
            builder.AppendLine(Localization.Get("battle.hover.none"));
            return;
        }

        foreach (var skill in skills)
        {
            builder.AppendLine($"{skill.Name} [{skill.Rarity}] - {skill.Description}");
        }
    }

    private static void AppendIntro(StringBuilder builder, BattleUnit unit)
    {
        var intro = BattleUnitUiFormatter.GetDangerReason(unit);
        if (string.IsNullOrWhiteSpace(intro))
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine(Localization.Get("battle.hover.danger"));
        builder.AppendLine(intro);
    }
}
