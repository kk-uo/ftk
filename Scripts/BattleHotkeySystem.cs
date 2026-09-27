//////////////////////////////////////////////////////////
// 文件：Scripts/BattleHotkeySystem.cs
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

/// <summary>
/// Core System 的公开类：BattleHotkeySystem。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class BattleHotkeySystem
{
    /// <summary>
    /// 出牌栏固定槽位数量。第11槽用于容纳更多招式，但没有对应数字键，只支持鼠标点击。
    /// </summary>
    public const int ActionSlotCount = 11;

    public const int MaxActionHotkeys = 10;

    private static readonly Key[] ActionKeys =
    {
        Key.Key1,
        Key.Key2,
        Key.Key3,
        Key.Key4,
        Key.Key5,
        Key.Key6,
        Key.Key7,
        Key.Key8,
        Key.Key9,
        Key.Key0
    };

    private static readonly Key[] KeypadActionKeys =
    {
        Key.Kp1,
        Key.Kp2,
        Key.Kp3,
        Key.Kp4,
        Key.Kp5,
        Key.Kp6,
        Key.Kp7,
        Key.Kp8,
        Key.Kp9,
        Key.Kp0
    };

    private static readonly string[] DisplayTexts =
    {
        "①",
        "②",
        "③",
        "④",
        "⑤",
        "⑥",
        "⑦",
        "⑧",
        "⑨",
        "⑩"
    };

    /// <summary>
    /// Core System 的公开入口：GetActionIndexForKey。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetActionIndexForKey(Key key)
    {
        for (var i = 0; i < ActionKeys.Length; i++)
        {
            if (key == ActionKeys[i] || key == KeypadActionKeys[i])
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Core System 的公开入口：GetActionIndexForKeyEvent。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetActionIndexForKeyEvent(InputEventKey keyEvent)
        => GetActionIndexForKeyPair(keyEvent.Keycode, keyEvent.PhysicalKeycode);

    /// <summary>
    /// Core System 的公开入口：GetActionIndexForKeyPair。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetActionIndexForKeyPair(Key keycode, Key physicalKeycode)
    {
        var index = GetActionIndexForKey(keycode);
        if (index >= 0)
        {
            return index;
        }

        return GetActionIndexForKey(physicalKeycode);
    }

    /// <summary>
    /// Core System 的公开入口：GetKeyForActionIndex。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static Key GetKeyForActionIndex(int index)
        => index >= 0 && index < ActionKeys.Length ? ActionKeys[index] : Key.None;

    /// <summary>
    /// Core System 的公开入口：GetDisplayTextForActionIndex。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetDisplayTextForActionIndex(int index)
        => index >= 0 && index < DisplayTexts.Length ? DisplayTexts[index] : string.Empty;

    /// <summary>
    /// Core System 的公开入口：GetKeyNameForActionIndex。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static string GetKeyNameForActionIndex(int index)
        => index == 9 ? "0" : index >= 0 && index < 9 ? (index + 1).ToString() : string.Empty;

    /// <summary>
    /// Core System 的公开入口：GetTargetIndexAfterSwitch。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static int GetTargetIndexAfterSwitch(int currentIndex, int targetCount, int direction)
    {
        if (targetCount <= 0)
        {
            return -1;
        }

        if (currentIndex < 0 || currentIndex >= targetCount)
        {
            return 0;
        }

        return System.Math.Clamp(currentIndex + direction, 0, targetCount - 1);
    }
}
