//////////////////////////////////////////////////////////
// 文件：Scripts/TooltipManager.cs
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

// Unified floating tooltip manager. No autoload needed — panel is lazily added to scene root.
// Show() cancels any pending hide, so adjacent hover areas never flicker.
// Hide() is 1-frame deferred: if Show() fires in the same frame the hide is cancelled.
// HideImmediate() is synchronous — use for scene transitions and battle end.
/// <summary>
/// Core System 的公开类：TooltipManager。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public static class TooltipManager
{
    // Tooltip 正文字号上调后，同步略增基础宽度，避免短提示被不必要地折成多行。
    public const float DefaultTextWidth = 132f;
    public const float BattleUnitTextWidth = 620f;
    public const float RichTextMinWidth = 220f;
    public const float RichTextMaxWidth = 470f;

    private static TooltipPanel? _panel;
    internal static bool ShouldHide;

    /// <summary>
    /// Core System 的公开入口：Show。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void Show(string text, Control source, float minTextWidth = DefaultTextWidth)
    {
        if (string.IsNullOrWhiteSpace(text)) { HideImmediate(); return; }
        ShouldHide = false;
        EnsurePanel(source);
        _panel?.ShowTooltip(text, minTextWidth);
    }

    /// <summary>
    /// 显示统一的富文本 Tooltip（标题/副标题/品质颜色/图片/描述/额外说明）。
    ///
    /// 装备、技能、Buff、事件、角色都应该共用这一个入口，不要各自再写一套
    /// Tooltip 弹窗——具体显示哪些行由 <see cref="TooltipContent"/> 决定，
    /// 缺省的字段（例如没有图片、没有额外说明）会自动隐藏对应的行。
    /// </summary>
    public static void ShowRich(TooltipContent content, Control source)
    {
        ShouldHide = false;
        EnsurePanel(source);
        _panel?.ShowRichTooltip(content);
    }

    /// <summary>
    /// Core System 的公开入口：Hide。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void Hide()
    {
        ShouldHide = true;
        _panel?.RequestHide();
    }

    /// <summary>
    /// Core System 的公开入口：HideImmediate。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void HideImmediate()
    {
        ShouldHide = true;
        _panel?.ForceHide();
    }

    /// <summary>
    /// Core System 的公开入口：Refresh。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public static void Refresh(string text, Control source, float minTextWidth = DefaultTextWidth) => Show(text, source, minTextWidth);

    private static void EnsurePanel(Control source)
    {
        if (_panel != null && GodotObject.IsInstanceValid(_panel) && _panel.IsInsideTree())
            return;
        if (_panel != null && GodotObject.IsInstanceValid(_panel))
            _panel.QueueFree();
        _panel = new TooltipPanel();
        source.GetTree().Root.AddChild(_panel);
    }
}

/// <summary>
/// Core System 的公开类：TooltipPanel。
///
/// 用于表达该模块对外可见的核心概念，并保持具体实现与调用方解耦。
/// </summary>
public partial class TooltipPanel : CanvasLayer
{
    // 统一信息展示框字号。标题、副标题、正文、补充说明均在原基础上增加 2px。
    private const int TitleFontSize = 22;
    private const int SubtitleFontSize = 18;
    private const int BodyFontSize = 20;
    private const int ExtraNoteFontSize = 17;

    private PanelContainer? _container;
    private TextureRect? _iconRect;
    private Label? _titleLabel;
    private Label? _subtitleLabel;
    private Label? _label;
    private Label? _extraLabel;

    /// <summary>
    /// Core System 的公开入口：_Ready。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Ready()
    {
        Layer = 200;

        _container = new PanelContainer
        {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        _container.AddThemeStyleboxOverride("panel", BattleUiSkin.CreateTooltipStyle(10));

        // 统一布局：左侧可选图标，右侧标题/副标题/描述/额外说明依次排列。
        // 简单文本模式（ShowTooltip）只使用 _label 这一行，其余行保持隐藏。
        var root = new HBoxContainer();
        root.AddThemeConstantOverride("separation", 10);
        _container.AddChild(root);

        _iconRect = new TextureRect
        {
            CustomMinimumSize = new Vector2(48, 48),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            Visible = false
        };
        root.AddChild(_iconRect);

        var textColumn = new VBoxContainer();
        textColumn.AddThemeConstantOverride("separation", 2);
        root.AddChild(textColumn);

        _titleLabel = new Label
        {
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(120, 0)
        };
        _titleLabel.AddThemeFontSizeOverride("font_size", TitleFontSize);
        textColumn.AddChild(_titleLabel);

        _subtitleLabel = new Label
        {
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(120, 0)
        };
        _subtitleLabel.AddThemeFontSizeOverride("font_size", SubtitleFontSize);
        _subtitleLabel.AddThemeColorOverride("font_color", new Color(0.72f, 0.74f, 0.80f));
        textColumn.AddChild(_subtitleLabel);

        _label = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(120, 0)
        };
        _label.AddThemeFontSizeOverride("font_size", BodyFontSize);
        _label.AddThemeColorOverride("font_color", new Color(0.92f, 0.93f, 0.96f));
        textColumn.AddChild(_label);

        _extraLabel = new Label
        {
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(120, 0)
        };
        _extraLabel.AddThemeFontSizeOverride("font_size", ExtraNoteFontSize);
        _extraLabel.AddThemeColorOverride("font_color", new Color(0.68f, 0.70f, 0.76f));
        textColumn.AddChild(_extraLabel);

        AddChild(_container);
    }

    /// <summary>
    /// Core System 的公开入口：ShowTooltip。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ShowTooltip(string text, float minTextWidth = TooltipManager.DefaultTextWidth)
    {
        if (_label == null || _container == null) return;

        // 简单文本模式：只显示描述这一行，隐藏图标/标题/副标题/额外说明。
        if (_iconRect != null) _iconRect.Visible = false;
        if (_titleLabel != null) _titleLabel.Visible = false;
        if (_subtitleLabel != null) _subtitleLabel.Visible = false;
        if (_extraLabel != null) _extraLabel.Visible = false;

        ApplyTextWidth(minTextWidth);
        _label.Text = text;
        _label.AddThemeColorOverride("font_color", new Color(0.92f, 0.93f, 0.96f));
        ResetTooltipSize();
        _container.Visible = true;
        PositionNearMouse();
    }

    /// <summary>
    /// 显示富文本 Tooltip：按 <see cref="TooltipContent"/> 里各字段是否有值，
    /// 决定显示哪些行——装备/技能/Buff/事件/角色共用这一个方法，不需要各自实现。
    /// </summary>
    public void ShowRichTooltip(TooltipContent content)
    {
        if (_label == null || _container == null || _iconRect == null
            || _titleLabel == null || _subtitleLabel == null || _extraLabel == null)
        {
            return;
        }

        ApplyTextWidth(CalculateRichTextWidth(content));
        if (content.Icon != null)
        {
            _iconRect.Texture = content.Icon;
            _iconRect.Visible = true;
        }
        else
        {
            _iconRect.Visible = false;
        }

        _titleLabel.Visible = !string.IsNullOrEmpty(content.Title);
        _titleLabel.Text = content.Title;
        _titleLabel.AddThemeColorOverride("font_color", content.TitleColor ?? new Color(0.95f, 0.95f, 0.97f));

        _subtitleLabel.Visible = !string.IsNullOrEmpty(content.Subtitle);
        _subtitleLabel.Text = content.Subtitle;

        _label.Text = content.Description;
        _label.AddThemeColorOverride("font_color", new Color(0.92f, 0.93f, 0.96f));

        _extraLabel.Visible = !string.IsNullOrEmpty(content.ExtraNote);
        _extraLabel.Text = content.ExtraNote;

        ResetTooltipSize();
        _container.Visible = true;
        PositionNearMouse();
    }

    private void ApplyTextWidth(float minTextWidth)
    {
        var width = Mathf.Max(TooltipManager.DefaultTextWidth, minTextWidth);
        if (_titleLabel != null) _titleLabel.CustomMinimumSize = new Vector2(width, 0);
        if (_subtitleLabel != null) _subtitleLabel.CustomMinimumSize = new Vector2(width, 0);
        if (_label != null) _label.CustomMinimumSize = new Vector2(width, 0);
        if (_extraLabel != null) _extraLabel.CustomMinimumSize = new Vector2(width, 0);
    }

    private static float CalculateRichTextWidth(TooltipContent content)
    {
        var longestLineUnits = EstimateLongestLineUnits(content.Title);
        longestLineUnits = Mathf.Max(longestLineUnits, EstimateLongestLineUnits(content.Subtitle));
        longestLineUnits = Mathf.Max(longestLineUnits, EstimateLongestLineUnits(content.Description));
        longestLineUnits = Mathf.Max(longestLineUnits, EstimateLongestLineUnits(content.ExtraNote));

        var totalUnits = EstimateTextUnits(content.Title)
            + EstimateTextUnits(content.Subtitle)
            + EstimateTextUnits(content.Description)
            + EstimateTextUnits(content.ExtraNote);

        // 正文从18px增至20px：按同一比例估算宽度，尽量维持原有换行密度。
        var width = Mathf.Clamp(longestLineUnits * 20f + 24f, TooltipManager.RichTextMinWidth, TooltipManager.RichTextMaxWidth);
        if (totalUnits > 72f)
        {
            width = Mathf.Max(width, 320f);
        }
        if (totalUnits > 120f)
        {
            width = Mathf.Max(width, 380f);
        }

        return Mathf.Min(width, TooltipManager.RichTextMaxWidth);
    }

    private static float EstimateLongestLineUnits(string text)
    {
        var longest = 0f;
        var current = 0f;
        foreach (var ch in text)
        {
            if (ch == '\n')
            {
                longest = Mathf.Max(longest, current);
                current = 0f;
                continue;
            }

            current += EstimateCharUnits(ch);
        }

        return Mathf.Max(longest, current);
    }

    private static float EstimateTextUnits(string text)
    {
        var units = 0f;
        foreach (var ch in text)
        {
            if (ch != '\n')
            {
                units += EstimateCharUnits(ch);
            }
        }

        return units;
    }

    private static float EstimateCharUnits(char ch)
        => ch <= 0x007F ? 0.55f : 1f;

    private void ResetTooltipSize()
    {
        if (_container == null)
        {
            return;
        }

        _container.Size = Vector2.Zero;
        _container.QueueSort();
    }

    /// <summary>
    /// Core System 的公开入口：RequestHide。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void RequestHide()
    {
        Callable.From(DeferredHide).CallDeferred();
    }

    /// <summary>
    /// Core System 的公开入口：ForceHide。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public void ForceHide()
    {
        if (_container != null) _container.Visible = false;
    }

    private void DeferredHide()
    {
        if (TooltipManager.ShouldHide)
            ForceHide();
    }

    /// <summary>
    /// Core System 的公开入口：_Process。
    ///
    /// 调用方应通过该入口完成对应业务步骤，不应依赖本方法内部的临时状态或执行细节。
    /// </summary>
    public override void _Process(double delta)
    {
        if (_container?.Visible == true)
            PositionNearMouse();
    }

    private void PositionNearMouse()
    {
        if (_container == null) return;
        var viewport = GetViewport();
        if (viewport == null) return;

        var mouse = viewport.GetMousePosition();
        var size = _container.Size;
        var viewSize = viewport.GetVisibleRect().Size;

        const float gap = 14f;
        const float edge = 6f;
        var x = mouse.X + gap;
        var y = mouse.Y + gap;

        if (x + size.X > viewSize.X - edge) x = mouse.X - size.X - gap;
        if (y + size.Y > viewSize.Y - edge) y = mouse.Y - size.Y - gap;
        if (x < edge) x = edge;
        if (y < edge) y = edge;

        _container.SetPosition(new Vector2(x, y));
    }
}

/// <summary>
/// 统一 Tooltip 的内容数据：装备、技能、Buff、事件、角色都应该构造这一个类型，
/// 交给 <see cref="TooltipManager.ShowRich"/> 显示，不要各自维护一套 Tooltip 弹窗。
///
/// 除 Description 外全部可选——留空/为 null 的字段，TooltipPanel 会自动隐藏对应的行，
/// 而不是显示一片空白。
/// </summary>
public sealed class TooltipContent
{
    /// <summary>
    /// 创建一份 Tooltip 内容。
    /// </summary>
    public TooltipContent(
        string description,
        string title = "",
        string subtitle = "",
        Color? titleColor = null,
        Texture2D? icon = null,
        string extraNote = "")
    {
        Description = description;
        Title = title;
        Subtitle = subtitle;
        TitleColor = titleColor;
        Icon = icon;
        ExtraNote = extraNote;
    }

    /// <summary>标题，例如装备/技能/Buff 名称。留空则不显示标题行。</summary>
    public string Title { get; }

    /// <summary>副标题，例如装备类型文本。留空则不显示副标题行。</summary>
    public string Subtitle { get; }

    /// <summary>
    /// 标题颜色，通常用来表示品质（普通/稀有/史诗/传说）。
    /// 为 null 时使用默认的浅色文字。
    /// </summary>
    public Color? TitleColor { get; }

    /// <summary>
    /// 图片，通常来自 IconLibrary/XxxIconDatabase 解析后的 Texture2D。
    /// 为 null 则不显示图标（调用方一般应该已经用 IconLibrary 兜底到 DefaultIcon，
    /// 而不是把"没有图"传进来）。
    /// </summary>
    public Texture2D? Icon { get; }

    /// <summary>主描述文本，全部 Tooltip 共有的一行，应通过 Localization 取得。</summary>
    public string Description { get; }

    /// <summary>额外说明，例如出售价格、装备来源、Buff 剩余回合数。留空则不显示。</summary>
    public string ExtraNote { get; }
}
