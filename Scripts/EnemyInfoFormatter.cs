//////////////////////////////////////////////////////////
// 文件：Scripts/EnemyInfoFormatter.cs
//
// 模块：Enemy System
//
// 职责：
// 1. 承载敌人定义、敌人实例与敌方 AI相关代码。
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

using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Enemy System 的公开类：EnemyInfoFormatter。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class EnemyInfoFormatter
{
    /// <summary>
    /// Enemy System 的公开入口：GetFactionText。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetFactionText(EnemyDefinition definition)
    {
        if (definition.Tags.Contains(EnemyTag.Shu))
        {
            return Localization.Get("faction.shu");
        }

        if (definition.Tags.Contains(EnemyTag.Wei))
        {
            return Localization.Get("faction.wei");
        }

        if (definition.Tags.Contains(EnemyTag.Wu))
        {
            return Localization.Get("faction.wu");
        }

        if (definition.Tags.Contains(EnemyTag.Qun))
        {
            return Localization.Get("faction.qun");
        }

        return Localization.Get("faction.unknown");
    }

    /// <summary>
    /// Enemy System 的公开入口：GetDangerReason。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetDangerReason(string loreId)
    {
        return loreId switch
        {
            "giant_rolling_stone" => Localization.Get("enemy.danger.giant_rolling_stone"),
            "giant_rolling_log" => Localization.Get("enemy.danger.giant_rolling_log"),
            "mihuan_xiao_shou" => Localization.Get("enemy.danger.mihuan_xiao_shou"),
            "heavy_armor_guard" => Localization.Get("enemy.danger.heavy_armor_guard"),
            "gunner" => Localization.Get("enemy.danger.gunner"),
            "wo_long_collective" => Localization.Get("enemy.danger.wo_long_collective"),
            "cold_guard" => Localization.Get("enemy.danger.cold_guard"),
            "boss_tyrant" => Localization.Get("enemy.danger.boss_tyrant"),
            "giant_pus_sac" => Localization.Get("enemy.danger.giant_pus_sac"),
            "huang_yi_zhi_zhu" => Localization.Get("enemy.danger.huang_yi_zhi_zhu"),
            "yellow_turban_devotee" => Localization.Get("enemy.danger.yellow_turban_devotee"),
            "giant_mech_rat" => Localization.Get("enemy.danger.giant_mech_rat"),
            "rat_king" => Localization.Get("enemy.danger.rat_king"),
            "corpse_collector" => Localization.Get("enemy.danger.corpse_collector"),
            _ => string.Empty
        };
    }

    /// <summary>
    /// Enemy System 的公开入口：BuildSkillSummary。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string BuildSkillSummary(EnemyInstance enemy)
    {
        if (enemy.RuntimeSkillIds.Count == 0)
        {
            return Localization.Get("battle.hover.none");
        }

        var lines = new List<string>();
        foreach (var skillId in enemy.RuntimeSkillIds)
        {
            var skill = SkillDatabase.GetSkill(skillId);
            if (skill == null)
            {
                continue;
            }

            lines.Add($"{Localization.GetName(skill)}：{Localization.GetDescription(skill)}");
        }

        return lines.Count == 0 ? Localization.Get("battle.hover.none") : string.Join("\n", lines);
    }

    /// <summary>
    /// 读取敌人的技能展示数据。
    ///
    /// 敌人信息面板只需要稳定的名称与描述列表，
    /// 因此这里隐藏 RuntimeSkillIds 到 SkillDatabase 的查询细节。
    /// </summary>
    public static List<(string Name, string Description)> GetSkills(EnemyInstance enemy)
    {
        var result = new List<(string, string)>();
        foreach (var skillId in enemy.RuntimeSkillIds)
        {
            var skill = SkillDatabase.GetSkill(skillId);
            if (skill != null)
            {
                result.Add((Localization.GetName(skill), Localization.GetDescription(skill)));
            }
        }

        return result;
    }

    /// <summary>
    /// Enemy System 的公开入口：BuildEquipmentSummary。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string BuildEquipmentSummary(EnemyInstance enemy)
    {
        if (enemy.Equipments.Count == 0)
        {
            return Localization.Get("battle.hover.none");
        }

        var lines = new List<string>();
        foreach (var group in enemy.Equipments.GroupBy(equipment => equipment.Id))
        {
            var equipment = group.First();
            var effectText = equipment.Effects.Count == 0
                ? Localization.GetDescription(equipment)
                : Localization.GetEquipmentEffectDescriptions(equipment, "\n");
            var countSuffix = group.Count() > 1 ? $"*{group.Count()}" : string.Empty;
            lines.Add($"{Localization.GetName(equipment)}{countSuffix}：{effectText}");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// 读取敌人的装备展示数据。
    ///
    /// 该方法只生成 Tooltip 和详情面板所需文本，
    /// 不执行装备效果，也不改变敌人装备列表。
    /// </summary>
    public static List<(string Name, string Description)> GetEquipments(EnemyInstance enemy)
    {
        var result = new List<(string, string)>();
        foreach (var group in enemy.Equipments.GroupBy(equipment => equipment.Id))
        {
            var equipment = group.First();
            var desc = equipment.Effects.Count == 0
                ? Localization.GetDescription(equipment)
                : Localization.GetEquipmentEffectDescriptions(equipment, "\n");
            var countSuffix = group.Count() > 1 ? $"*{group.Count()}" : string.Empty;
            result.Add(($"{Localization.GetName(equipment)}{countSuffix}", desc));
        }

        return result;
    }
}
