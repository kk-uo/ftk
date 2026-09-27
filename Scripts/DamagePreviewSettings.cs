//////////////////////////////////////////////////////////
// 文件：Scripts/DamagePreviewSettings.cs
//
// 模块：Core System
//
// 职责：
// 1. 承载"手牌预估伤害显示"功能的开关状态与持久化。
//
// 不负责：
// × 计算伤害（见 DamagePreviewService）。
// × 渲染 UI。
//
// 主要依赖：
// Godot / C# Runtime，user://settings.cfg
//////////////////////////////////////////////////////////

using Godot;
using System;

/// <summary>
/// Core System 的公开类：DamagePreviewSettings。
/// </summary>
public static class DamagePreviewSettings
{
    private const string ConfigPath = "user://settings.cfg";
    private const string ConfigSection = "damage_preview";
    private const string ConfigKey = "enabled";

    public static bool IsEnabled { get; private set; } = true;

    public static event Action? EnabledChanged;

    /// <summary>
    /// Core System 的公开入口：Initialize。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void Initialize()
    {
        var config = new ConfigFile();
        IsEnabled = config.Load(ConfigPath) != Error.Ok
            || config.GetValue(ConfigSection, ConfigKey, Variant.From(true)).AsBool();
        GD.Print($"[DamagePreviewSettings] Initialized: {IsEnabled}");
    }

    /// <summary>
    /// Core System 的公开入口：SetEnabled。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void SetEnabled(bool enabled)
    {
        if (IsEnabled == enabled) return;
        IsEnabled = enabled;
        var config = new ConfigFile();
        config.Load(ConfigPath);
        config.SetValue(ConfigSection, ConfigKey, enabled);
        config.Save(ConfigPath);
        GD.Print($"[DamagePreviewSettings] Changed to: {enabled}");
        EnabledChanged?.Invoke();
    }
}
