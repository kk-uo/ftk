//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/AudioDatabase.cs
//
// 模块：Presentation System
//
// 为什么存在：
// 音效属于表现反馈，不应由 Damage、Battle 或 Reward 直接加载。
// AudioDatabase 为未来音频缓存、音量分组和音频总线提供统一入口。
//
// 职责：
// 1. 统一登记音效资源 ID 与路径。
// 2. 为未来音效播放、混音、音量分组预留入口。
// 3. 防止业务代码直接 Load 音频资源。
//
// 不负责：
// × 播放音效。
// × 管理音量设置。
// × 判断音效触发条件。
//
// 主要依赖：
// C# Runtime
//////////////////////////////////////////////////////////

using System.Collections.Generic;

/// <summary>
/// 音效资源数据库。
///
/// 当前只保存资源路径，后续可以扩展为 AudioStream 缓存或音频总线配置。
/// </summary>
public static class AudioDatabase
{
    private static readonly Dictionary<string, string> AudioPaths = new();

    /// <summary>
    /// 注册音效资源路径。
    /// </summary>
    public static void Register(string id, string path)
    {
        AudioPaths[id] = path;
    }

    /// <summary>
    /// 按 ID 查询音效资源路径。
    /// </summary>
    public static string? GetPath(string id)
    {
        return AudioPaths.TryGetValue(id, out var path) ? path : null;
    }
}
