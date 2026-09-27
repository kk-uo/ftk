//////////////////////////////////////////////////////////
// 文件：Scripts/InitialEventRandomizer.cs
//
// 模块：Event System
//
// 职责：
// 1. 承载地图事件、事件选项与事件奖励相关代码。
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

using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Event System 的公开类：InitialEventManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class InitialEventManager
{
    private static readonly Random Rng = new();

    /// <summary>
    /// Event System 的公开入口：Pick3。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static List<InitialEventDefinition> Pick3()
    {
        var pool = new List<InitialEventDefinition>(InitialEventPool.AllEntries);
        var result = new List<InitialEventDefinition>(3);
        var usedTags = new HashSet<string>();

        while (result.Count < 3 && pool.Count > 0)
        {
            var idx = Rng.Next(pool.Count);
            var candidate = pool[idx];
            pool.RemoveAt(idx);

            if (candidate.Tags.Any(usedTags.Contains))
                continue;

            foreach (var tag in candidate.Tags)
                usedTags.Add(tag);

            result.Add(candidate);
        }

        return result;
    }

    /// <summary>
    /// 为【改命】重抽一个初始事件选项。排除当前画面已有Id，并以另外两个选项的全部标签作为约束，
    /// 保持初始三选一“相同字母不可同时出现”的规则；多标签事件会同时占用所有标签。
    /// </summary>
    public static InitialEventDefinition? PickReplacement(
        InitialEventDefinition current,
        IReadOnlyList<InitialEventDefinition> allShown)
    {
        var excludedIds = allShown.Select(entry => entry.Id).ToHashSet(StringComparer.Ordinal);
        var occupiedTags = allShown
            .Where(entry => !ReferenceEquals(entry, current))
            .SelectMany(entry => entry.Tags)
            .ToHashSet(StringComparer.Ordinal);
        var candidates = InitialEventPool.AllEntries
            .Where(entry => !excludedIds.Contains(entry.Id))
            .Where(entry => !entry.Tags.Any(occupiedTags.Contains))
            .ToList();

        return candidates.Count == 0 ? null : candidates[Rng.Next(candidates.Count)];
    }

    /// <summary>
    /// Event System 的公开入口：RunTest。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string RunTest()
    {
        var pass = 0;
        var fail = 0;

        for (var i = 0; i < 1000; i++)
        {
            var picks = Pick3();
            if (picks.Count != 3) { fail++; continue; }

            var seen = new HashSet<string>();
            var ok = true;
            foreach (var tag in picks.SelectMany(entry => entry.Tags))
            {
                if (!seen.Add(tag)) { ok = false; break; }
            }

            if (ok) pass++; else fail++;
        }

        return $"InitialEventManager.RunTest: 1000次随机 → pass={pass}, fail={fail}";
    }
}
