//////////////////////////////////////////////////////////
// 文件：Scripts/TutorialProgress.cs
//
// 模块：Tutorial System
//
// 为什么存在：
// 战斗教程/战斗外教程 完成状态的轻量持久化——复用项目里唯一真实存在的
// user://settings.cfg 机制（与 DeveloperModeManager 的开发者模式开关同款写法），
// 不是Run级存档系统（项目里没有），只是一个跨进程重启保留的小标记文件，
// 供教程选择界面展示"已完成/未完成"。
//
// 不负责：
// × Run内状态（由 GameManager/TutorialManager 负责）。
// × 教程步骤本身的推进逻辑（由 TutorialManager 负责）。
//////////////////////////////////////////////////////////

using Godot;

public static class TutorialProgress
{
    private const string ConfigPath = "user://settings.cfg";
    private const string Section = "tutorial";
    private const string CombatKey = "combat_completed";
    private const string MetaKey = "meta_completed";

    /// <summary>
    /// 假设中的"旧版单一完成字段"键名——本项目实际上从未写过这个字段（项目里
    /// 目前没有任何Run级存档系统，此前也没有任何形式的教程完成持久化），这里
    /// 只是按需求做防御性兼容读取：若这个字段恰好存在且为true，视为战斗教程
    /// 已完成，战斗外教程（本次新增，旧版本不可能存在）仍视为未完成。
    /// </summary>
    private const string LegacyCombinedKey = "completed";

    public static bool IsCombatCompleted { get; private set; }
    public static bool IsMetaCompleted { get; private set; }

    /// <summary>
    /// 应在游戏启动时调用一次（与 DeveloperModeManager.Initialize() 同一处调用），
    /// 从 user://settings.cfg 读取两个教程模块的完成状态。
    /// </summary>
    public static void Initialize()
    {
        var config = new ConfigFile();
        if (config.Load(ConfigPath) != Error.Ok)
        {
            IsCombatCompleted = false;
            IsMetaCompleted = false;
            GD.Print("[TutorialProgress] Initialized: combat=False, meta=False (no config)");
            return;
        }

        if (config.HasSectionKey(Section, CombatKey))
        {
            IsCombatCompleted = config.GetValue(Section, CombatKey, Variant.From(false)).AsBool();
        }
        else if (config.HasSectionKey(Section, LegacyCombinedKey))
        {
            IsCombatCompleted = config.GetValue(Section, LegacyCombinedKey, Variant.From(false)).AsBool();
        }
        else
        {
            IsCombatCompleted = false;
        }

        IsMetaCompleted = config.GetValue(Section, MetaKey, Variant.From(false)).AsBool();
        GD.Print($"[TutorialProgress] Initialized: combat={IsCombatCompleted}, meta={IsMetaCompleted}");
    }

    public static void MarkCombatCompleted()
    {
        if (IsCombatCompleted) return;
        IsCombatCompleted = true;
        var config = new ConfigFile();
        config.Load(ConfigPath);
        config.SetValue(Section, CombatKey, true);
        config.Save(ConfigPath);
        GD.Print("[TutorialProgress] Combat tutorial marked completed.");
    }

    public static void MarkMetaCompleted()
    {
        if (IsMetaCompleted) return;
        IsMetaCompleted = true;
        var config = new ConfigFile();
        config.Load(ConfigPath);
        config.SetValue(Section, MetaKey, true);
        config.Save(ConfigPath);
        GD.Print("[TutorialProgress] Meta tutorial marked completed.");
    }

    public static void ResetCombat()
    {
        IsCombatCompleted = false;
        var config = new ConfigFile();
        config.Load(ConfigPath);
        config.SetValue(Section, CombatKey, false);
        config.Save(ConfigPath);
        GD.Print("[TutorialProgress] Combat tutorial progress reset.");
    }

    public static void ResetMeta()
    {
        IsMetaCompleted = false;
        var config = new ConfigFile();
        config.Load(ConfigPath);
        config.SetValue(Section, MetaKey, false);
        config.Save(ConfigPath);
        GD.Print("[TutorialProgress] Meta tutorial progress reset.");
    }

    public static void ResetAll()
    {
        ResetCombat();
        ResetMeta();
    }
}
