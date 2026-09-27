//////////////////////////////////////////////////////////
// 文件：Scripts/InitialEventEntry.cs
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

/// <summary>
/// Event System 的公开类：InitialEventDefinition。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public sealed class InitialEventDefinition
{
    public string Id = string.Empty;
    // 同一字母标签不能与自己同时出现在初始事件三选一中。
    // Tag 保留为首个标签，AdditionalTags 用于 "(a)(e)" 这类多重互斥条件。
    public string? Tag;
    public List<string> AdditionalTags = new();
    public IEnumerable<string> Tags
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Tag))
            {
                yield return Tag;
            }

            foreach (var tag in AdditionalTags)
            {
                if (!string.IsNullOrWhiteSpace(tag) && !string.Equals(tag, Tag, StringComparison.Ordinal))
                {
                    yield return tag;
                }
            }
        }
    }
    public int Weight = 1;           // reserved
    public string Title = string.Empty;         // reserved
    public string Description = string.Empty;   // fallback (zh)
    public string EffectDetail = string.Empty;  // fallback (zh)
    public string DescriptionKey = string.Empty;
    public string EffectDetailKey = string.Empty;

    public string DisplayDescription => Localization.GetOrFallback(DescriptionKey, Description);
    public string DisplayEffectDetail => Localization.GetOrFallback(EffectDetailKey, EffectDetail);

    public Action Apply = () => { };
}
