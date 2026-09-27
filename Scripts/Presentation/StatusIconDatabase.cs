//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/StatusIconDatabase.cs
//
// 模块：Presentation System
//
// 职责：
// 1. 统一管理所有 Buff / Debuff 状态图标：StatusId → PNG 贴图。
// 2. 提供统一的图标尺寸配置（普通 / Hover / Boss），所有显示位置共用。
// 3. 提供统一的图标控件工厂：有贴图时显示贴图，没有时回退到原文字徽标。
//
// 不负责：
// × 状态本身的规则/数值（那是 RunBuffManager / BattleUnitUiFormatter 的职责）。
// × Tooltip 文案（名称/描述/持续时间照旧走 BattleUnitUiFormatter）。
//
// 新增状态图标流程（无需修改任何 UI 代码）：
// 1. 把 PNG 放进 Assets/UI/StatusIcons/（英文小写命名，如 bleed.png）。
// 2. 在下方静态构造函数里 Register("状态Id", "bleed") 一行。
// 完成——玩家状态卡 / 敌人状态条 / Boss 状态条 / Tooltip 全部自动生效。
//////////////////////////////////////////////////////////

using Godot;
using System.Collections.Generic;

/// <summary>
/// 状态图标的显示尺寸档位：所有显示位置从这里取尺寸，禁止各处自行写死。
/// </summary>
public enum StatusIconVariant
{
    /// <summary>普通状态徽标（玩家状态卡 / 敌人状态条）。</summary>
    Normal,
    /// <summary>Hover 悬停面板中的状态图标。</summary>
    Hover,
    /// <summary>Boss 状态条上的状态图标。</summary>
    Boss
}

/// <summary>
/// Buff / Debuff 图标数据库：StatusId → 贴图，以及统一的尺寸配置与控件工厂。
/// </summary>
public static class StatusIconDatabase
{
    /// <summary>状态图标资源根目录：往这里放 PNG（英文小写命名）。</summary>
    public const string RootPath = "res://Assets/UI/StatusIcons";

    // ————————————————————————————————————————
    // 统一尺寸配置：只允许在这里改，所有显示位置共用。
    // ————————————————————————————————————————
    public static int NormalSize { get; set; } = 32;
    public static int HoverSize { get; set; } = 36;
    public static int BossSize { get; set; } = 40;

    private static readonly Dictionary<string, string> IconFileByStatusId = new();
    private static readonly Dictionary<string, Texture2D?> TextureCache = new();

    static StatusIconDatabase()
    {
        // 参考图第一排
        Register(RunBuffIds.Curse, "curse");                    // 诅咒
        Register(RunBuffIds.ShaQiChenShen, "fury");             // 煞气缠身
        Register(RunBuffIds.Darkness, "darkness");              // 黑暗
        Register(RunBuffIds.BloodMoonDarkness, "darkness");     // 血月黑暗（暂共用黑暗图标）
        Register("ferocity", "brutality");                      // 凶残
        Register("jigu", "excitement");                         // 击鼓（亢奋）
        Register("stun", "dizzy");                              // 眩晕
        // 参考图第二排
        Register("plague", "plague");                           // 瘟疫
        Register(RunBuffIds.AncestorBlessing, "blessing");      // 先祖的赐福
        Register("war_drum", "war_fervor");                     // 战鼓（战争兴奋）
        Register("war_drum_pending", "war_fervor");             // 战鼓待激活（共用图标）
        Register("frozen", "frozen");                           // 冰冻
    }

    /// <summary>
    /// 注册一个状态图标：statusId 是 BattleUnitStatusEntry.Id（含 RunBuffIds），
    /// fileName 是 Assets/UI/StatusIcons 下不带扩展名的 PNG 文件名。
    /// </summary>
    public static void Register(string statusId, string fileName)
    {
        IconFileByStatusId[statusId] = fileName;
        TextureCache.Remove(statusId);
    }

    /// <summary>
    /// 解析状态图标贴图；未注册或文件缺失时返回 null（调用方回退到文字徽标）。
    /// </summary>
    public static Texture2D? TryGetIconTexture(string statusId)
    {
        if (TextureCache.TryGetValue(statusId, out var cached))
        {
            return cached;
        }

        Texture2D? texture = null;
        if (IconFileByStatusId.TryGetValue(statusId, out var fileName))
        {
            var path = $"{RootPath}/{fileName}.png";
            if (ResourceLoader.Exists(path))
            {
                texture = GD.Load<Texture2D>(path);
            }
        }

        TextureCache[statusId] = texture;
        return texture;
    }

    /// <summary>
    /// 取某个档位的像素尺寸。
    /// </summary>
    public static int GetPixelSize(StatusIconVariant variant)
    {
        return variant switch
        {
            StatusIconVariant.Hover => HoverSize,
            StatusIconVariant.Boss => BossSize,
            _ => NormalSize
        };
    }

    /// <summary>
    /// 统一的状态图标控件工厂：
    /// - 已注册贴图 → TextureRect（原图统一缩放到档位尺寸）+ 右下角叠加层数标签；
    /// - 未注册 → 原有的文字徽标（IconText + ×层数），显示效果与旧版完全一致。
    /// mouseFilter 由调用方决定（需要 Hover Tooltip 的传 Stop，纯展示传 Ignore）。
    /// </summary>
    public static Control CreateIconControl(
        BattleUnitStatusEntry status,
        StatusIconVariant variant,
        Control.MouseFilterEnum mouseFilter = Control.MouseFilterEnum.Ignore)
    {
        var size = GetPixelSize(variant);
        var texture = TryGetIconTexture(status.Id);

        if (texture == null)
        {
            return CreateFallbackTextBadge(status, size, mouseFilter);
        }

        var root = new Control
        {
            CustomMinimumSize = new Vector2(size, size),
            MouseFilter = mouseFilter
        };

        var iconRect = new TextureRect
        {
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        iconRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(iconRect);

        if (status.Stackable && status.StackCount > 1)
        {
            var stackLabel = new Label
            {
                Text = $"×{status.StackCount}",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            stackLabel.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            stackLabel.AddThemeFontSizeOverride("font_size", System.Math.Max(10, size * 2 / 5));
            stackLabel.AddThemeColorOverride("font_color", Colors.White);
            stackLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
            stackLabel.AddThemeConstantOverride("outline_size", 3);
            root.AddChild(stackLabel);
        }

        return root;
    }

    private static Control CreateFallbackTextBadge(
        BattleUnitStatusEntry status,
        int size,
        Control.MouseFilterEnum mouseFilter)
    {
        var label = new Label
        {
            Text = status.Stackable && status.StackCount > 1
                ? $"{status.IconText}×{status.StackCount}"
                : status.IconText,
            MouseFilter = mouseFilter,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            CustomMinimumSize = new Vector2(size, size),
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis
        };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 3);
        return label;
    }
}
