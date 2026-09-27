//////////////////////////////////////////////////////////
// 文件：Scripts/TutorialIntegratedProgress.cs
//
// 模块：Tutorial System
//
// 为什么存在：
// 整合式首次引导教程（角色选择→教学地图→教学战斗→奖励→教学商店→背包装备→
// 教学事件→正式第一章）的完成/跳过状态持久化，写法与 TutorialProgress.cs
// （战斗/战斗外教程完成标记）完全一致——同一个 user://settings.cfg，独立
// section，避免污染其它模块的键名。另外承载"本次教学奖励/购买/装备是否已
// 发放"的一次性标记，防止同一App会话内因为中途返回/重进而重复发放。
//
// 不负责：
// × 教程步骤本身的宏观顺序推进（由 IntegratedTutorialFlow 负责）。
// × 战斗内单步教学（由 TutorialManager/TutorialDatabase 负责）。
//////////////////////////////////////////////////////////

using Godot;

public static class TutorialIntegratedProgress
{
    private const string ConfigPath = "user://settings.cfg";
    private const string Section = "tutorial_integrated";
    private const string CompletedKey = "completed";
    private const string SkippedKey = "skipped";
    private const string FirstEntryRecommendationShownKey = "first_entry_recommendation_shown";

    public static bool IsCompleted { get; private set; }
    public static bool IsSkipped { get; private set; }
    /// <summary>Whether the initial Tutorial recommendation has been displayed.</summary>
    public static bool HasShownFirstEntryRecommendation { get; private set; }

    // 以下四个是"本次教学流程内的一次性发放标记"——只需要在当前App会话内防重复
    // （同一次 IntegratedTutorialFlow 生命周期内不会被重复触发），不持久化到磁盘：
    // 项目本身没有Run级存档系统，教学奖励和玩家当前Run状态一样，只在内存里有意义，
    // 重启App后本来就会从头开始一局新的教学流程，不存在"重启后还要防重复"的场景。
    public static bool RewardGranted { get; set; }
    public static bool ShopPurchaseDone { get; set; }
    public static bool EquipDone { get; set; }
    public static bool EventDone { get; set; }

    /// <summary>
    /// 应在游戏启动时调用一次（与 TutorialProgress.Initialize() 同一处调用）。
    /// </summary>
    public static void Initialize()
    {
        var config = new ConfigFile();
        if (config.Load(ConfigPath) != Error.Ok)
        {
            IsCompleted = false;
            IsSkipped = false;
            HasShownFirstEntryRecommendation = false;
            GD.Print("[TutorialIntegratedProgress] Initialized: completed=False, skipped=False (no config)");
            return;
        }

        IsCompleted = config.GetValue(Section, CompletedKey, Variant.From(false)).AsBool();
        IsSkipped = config.GetValue(Section, SkippedKey, Variant.From(false)).AsBool();
        HasShownFirstEntryRecommendation = config.GetValue(Section, FirstEntryRecommendationShownKey, Variant.From(false)).AsBool();
        GD.Print($"[TutorialIntegratedProgress] Initialized: completed={IsCompleted}, skipped={IsSkipped}, recommended={HasShownFirstEntryRecommendation}");
    }

    public static void MarkCompleted()
    {
        IsCompleted = true;
        var config = new ConfigFile();
        config.Load(ConfigPath);
        config.SetValue(Section, CompletedKey, true);
        config.Save(ConfigPath);
        GD.Print("[TutorialIntegratedProgress] Integrated tutorial marked completed.");
    }

    public static void MarkSkipped()
    {
        IsSkipped = true;
        var config = new ConfigFile();
        config.Load(ConfigPath);
        config.SetValue(Section, SkippedKey, true);
        config.Save(ConfigPath);
        GD.Print("[TutorialIntegratedProgress] Integrated tutorial marked skipped.");
    }

    /// <summary>
    /// Consumes the one-time first-launch recommendation. It is separate from
    /// tutorial completion, so dismissing it never changes tutorial progress.
    /// </summary>
    public static bool TryMarkFirstEntryRecommendationShown()
    {
        if (HasShownFirstEntryRecommendation)
        {
            return false;
        }

        HasShownFirstEntryRecommendation = true;
        var config = new ConfigFile();
        config.Load(ConfigPath);
        config.SetValue(Section, FirstEntryRecommendationShownKey, true);
        config.Save(ConfigPath);
        GD.Print("[TutorialIntegratedProgress] First-entry tutorial recommendation shown.");
        return true;
    }

    /// <summary>
    /// 重置整合教程的完成/跳过状态（供主菜单"重新体验基础教程"使用），
    /// 同时清空本次一次性发放标记，允许下一次整合教程重新走完整流程。
    /// </summary>
    public static void ResetForReplay()
    {
        IsCompleted = false;
        IsSkipped = false;
        RewardGranted = false;
        ShopPurchaseDone = false;
        EquipDone = false;
        EventDone = false;

        var config = new ConfigFile();
        config.Load(ConfigPath);
        config.SetValue(Section, CompletedKey, false);
        config.SetValue(Section, SkippedKey, false);
        config.Save(ConfigPath);
        GD.Print("[TutorialIntegratedProgress] Reset for replay.");
    }
}
