//////////////////////////////////////////////////////////
// 文件：Scripts/FirstTimeHintManager.cs
//
// 模块：Tutorial System
//
// 为什么存在：
// 项目里"只弹一次"的提示此前全是手写复制的Toast代码（MainFlow.cs 的
// ShowTryMetaTutorialHint / OnInitialEventQualityUpgradeTriggered，各自独立
// 的bool字段+独立的Tween淡出代码），没有一个通用组件。基础教程刻意不塞入
// 高级系统（技能/Buff/Debuff/濒死/粮草/临时生命/特殊路线/阵营命运/载具商店/
// 装备交换），这些内容改为"玩家第一次真正遇到时"才弹一条简短提示——本类是
// 承载这14个（以及未来更多）首次提示的通用框架：状态持久化 + 只显示一次 +
// 可关闭 + 不阻塞游戏。
//
// 不负责：
// × 判断"什么时候算第一次遇到"（由各系统自己的调用点决定，本类只负责
//   "还没显示过就显示，显示过就永远不再显示"这一件事）。
// × 提示内容本身的教学设计（由 Localization 文案决定）。
//////////////////////////////////////////////////////////

using Godot;

public static class FirstTimeHintManager
{
    private const string ConfigPath = "user://settings.cfg";
    private const string Section = "first_time_hints";

    /// <summary>
    /// 14个首次提示的稳定id（用作 user://settings.cfg 里的key，也用作
    /// Localization key 后缀 first_time_hint.{id}.title/.desc）。
    /// </summary>
    public static class HintIds
    {
        public const string FirstChip = "first_chip";
        public const string FirstElite = "first_elite";
        public const string FirstBossSighting = "first_boss_sighting";
        public const string FirstFreeExplore = "first_free_explore";
        public const string FirstSkillTrigger = "first_skill_trigger";
        public const string FirstBuff = "first_buff";
        public const string FirstDebuff = "first_debuff";
        public const string FirstNearDeath = "first_near_death";
        public const string FirstForageSpent = "first_forage_spent";
        public const string FirstTempHp = "first_temp_hp";
        public const string FirstSpecialRoute = "first_special_route";
        public const string FirstFactionFate = "first_faction_fate";
        public const string FirstVehicleShop = "first_vehicle_shop";
        public const string FirstEquipmentSwap = "first_equipment_swap";
    }

    /// <summary>
    /// 全部14个首次提示id，供"规则手册/提示记录"界面枚举展示已解锁的提示
    /// （对应用户§11"可以在规则手册中重新查看"，本项目没有独立的规则手册
    /// 系统，复用 TutorialSelectController 的卡片式界面承载这个列表）。
    /// </summary>
    public static readonly string[] AllHintIds =
    {
        HintIds.FirstChip,
        HintIds.FirstElite,
        HintIds.FirstBossSighting,
        HintIds.FirstFreeExplore,
        HintIds.FirstSkillTrigger,
        HintIds.FirstBuff,
        HintIds.FirstDebuff,
        HintIds.FirstNearDeath,
        HintIds.FirstForageSpent,
        HintIds.FirstTempHp,
        HintIds.FirstSpecialRoute,
        HintIds.FirstFactionFate,
        HintIds.FirstVehicleShop,
        HintIds.FirstEquipmentSwap
    };

    /// <summary>
    /// 已展示过的提示id集合，游戏启动时从 user://settings.cfg 载入一次。
    /// </summary>
    private static readonly System.Collections.Generic.HashSet<string> SeenHintIds = new();

    public static void Initialize()
    {
        SeenHintIds.Clear();
        var config = new ConfigFile();
        if (config.Load(ConfigPath) != Error.Ok || !config.HasSection(Section)) return;

        foreach (var key in config.GetSectionKeys(Section))
        {
            if (config.GetValue(Section, key, Variant.From(false)).AsBool())
            {
                SeenHintIds.Add(key);
            }
        }
    }

    public static bool HasSeen(string hintId) => SeenHintIds.Contains(hintId);

    private static void MarkSeen(string hintId)
    {
        if (!SeenHintIds.Add(hintId)) return;
        var config = new ConfigFile();
        config.Load(ConfigPath);
        config.SetValue(Section, hintId, true);
        config.Save(ConfigPath);
    }

    /// <summary>
    /// 尝试展示一条首次提示：已经看过就什么都不做，返回false；否则在
    /// <paramref name="host"/>（任意常驻/长生命周期的 CanvasItem，通常是
    /// MainFlow 自身）上弹一条可关闭、~3.5秒自动淡出的左上角Toast，
    /// 并立即持久化"已看过"标记（不等玩家关闭，避免关闭前中途退出又刷一次）。
    /// </summary>
    public static bool TryShow(string hintId, Node host)
    {
        if (HasSeen(hintId)) return false;
        MarkSeen(hintId);

        var titleKey = $"first_time_hint.{hintId}.title";
        var descKey = $"first_time_hint.{hintId}.desc";

        var toast = new PanelContainer
        {
            AnchorLeft = 0f, AnchorRight = 0f, AnchorTop = 0f, AnchorBottom = 0f,
            OffsetLeft = 28f, OffsetRight = 680f, OffsetTop = 92f, OffsetBottom = 214f,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 650
        };
        toast.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.11f, 0.94f),
            BorderColor = new Color(1f, 0.85f, 0.3f),
            BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8
        });

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 18);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_right", 18);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        toast.AddChild(margin);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 6);
        margin.AddChild(vbox);

        var titleRow = new HBoxContainer();
        vbox.AddChild(titleRow);

        var titleLabel = new Label
        {
            Text = Localization.Get(titleKey),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 22);
        titleLabel.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        titleRow.AddChild(titleLabel);

        var closeButton = new Button { Text = "×", CustomMinimumSize = new Vector2(32, 32) };
        closeButton.AddThemeFontSizeOverride("font_size", 18);
        titleRow.AddChild(closeButton);

        var descLabel = new Label
        {
            Text = Localization.Get(descKey),
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        descLabel.AddThemeFontSizeOverride("font_size", 19);
        descLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.88f, 0.9f));
        vbox.AddChild(descLabel);

        host.AddChild(toast);

        var tween = toast.CreateTween();
        tween.TweenInterval(3.5);
        tween.TweenProperty(toast, "modulate:a", 0f, 0.45)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(toast.QueueFree));

        closeButton.Pressed += () =>
        {
            tween.Kill();
            toast.QueueFree();
        };

        return true;
    }

    public static void ResetAll()
    {
        SeenHintIds.Clear();
        var config = new ConfigFile();
        config.Load(ConfigPath);
        if (config.HasSection(Section))
        {
            foreach (var key in config.GetSectionKeys(Section))
            {
                config.SetValue(Section, key, false);
            }
        }
        config.Save(ConfigPath);
    }
}
