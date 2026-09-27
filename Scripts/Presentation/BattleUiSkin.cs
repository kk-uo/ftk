//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/BattleUiSkin.cs
//
// 模块：Presentation / Battle UI
//
// 职责：
// 1. 集中加载 Battle Skin 切片资源。
// 2. 为动态创建的按钮、面板、血条和 Tooltip 提供统一 StyleBox。
// 3. 保证表现层替换不侵入战斗逻辑与数据结构。
//
// 不负责：
// × 创建 Battle UI 布局。
// × 修改生命、费用或战斗状态。
// × 管理 Buff、卡牌或目标选择逻辑。
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// Battle UI 的统一 Skin 工厂。
///
/// 场景中静态节点与运行时动态节点都从这里取得同一组纹理样式，避免两套 UI
/// 在视觉上再次分叉。资源缺失时返回可读的深色后备样式，不影响战斗运行。
/// </summary>
public static class BattleUiSkin
{
    private const string Root = "res://Assets/UI/BattleSkin";

    private static readonly Texture2D? PanelTexture = LoadTexture("Panel/panel_frame.png");
    private static readonly Texture2D? HeaderTexture = LoadTexture("Header/header_frame.png");
    private static readonly Texture2D? TooltipTexture = LoadTexture("Tooltip/tooltip_frame.png");
    private static readonly Texture2D? HpBackgroundTexture = LoadTexture("HPBar/hp_background.png");
    private static readonly Texture2D? HpFillTexture = LoadTexture("HPBar/hp_fill.png");
    private static readonly Texture2D? ButtonNormalTexture = LoadTexture("Button/button_normal.png");
    private static readonly Texture2D? ButtonHoverTexture = LoadTexture("Button/button_hover.png");
    private static readonly Texture2D? ButtonPressedTexture = LoadTexture("Button/button_pressed.png");
    private static readonly Texture2D? ButtonDisabledTexture = LoadTexture("Button/button_disabled.png");
    private static readonly Texture2D? IconFrameTexture = LoadTexture("IconBackground/icon_frame.png");
    private static readonly Texture2D? ManaCircleTexture = LoadTexture("ManaCircle/mana_circle_frame.png");

    /// <summary>
    /// 将参考图按钮的 Normal、Hover、Pressed、Disabled 四态应用到按钮。
    /// </summary>
    public static void ApplyButton(Button button)
    {
        button.AddThemeStyleboxOverride("normal", CreateButtonStyle(ButtonNormalTexture));
        button.AddThemeStyleboxOverride("hover", CreateButtonStyle(ButtonHoverTexture));
        button.AddThemeStyleboxOverride("pressed", CreateButtonStyle(ButtonPressedTexture));
        button.AddThemeStyleboxOverride("disabled", CreateButtonStyle(ButtonDisabledTexture));
        button.AddThemeStyleboxOverride("focus", CreateButtonStyle(ButtonHoverTexture));
        button.AddThemeColorOverride("font_color", new Color(0.86f, 0.94f, 0.96f));
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", new Color(0.92f, 0.88f, 0.95f));
        button.AddThemeColorOverride("font_disabled_color", new Color(0.48f, 0.52f, 0.56f));
    }

    /// <summary>
    /// 创建普通信息面板的九宫格样式。
    /// </summary>
    public static StyleBox CreatePanelStyle(float contentMargin = 14f, Color? tint = null)
        => CreateTextureStyle(
            PanelTexture,
            28f,
            26f,
            contentMargin,
            tint ?? Colors.White,
            new Color(0.035f, 0.055f, 0.07f, 0.96f));

    /// <summary>
    /// 创建顶部导航栏样式。
    /// </summary>
    public static StyleBox CreateHeaderStyle(float contentMargin = 10f)
        => CreateTextureStyle(
            HeaderTexture,
            36f,
            22f,
            contentMargin,
            Colors.White,
            new Color(0.025f, 0.045f, 0.06f, 0.94f));

    /// <summary>
    /// 创建 Tooltip、技能说明与战斗详情共用的九宫格样式。
    /// </summary>
    public static StyleBox CreateTooltipStyle(float contentMargin = 14f)
        => CreateTextureStyle(
            TooltipTexture,
            30f,
            28f,
            contentMargin,
            Colors.White,
            new Color(0.025f, 0.045f, 0.06f, 0.98f));

    /// <summary>
    /// 创建血条底图。高亮时只改变外框色调，不改变血量数据或填充比例。
    /// </summary>
    public static StyleBox CreateHpBackgroundStyle(Color? highlight = null, float highlightAlpha = 0f)
    {
        var tint = Colors.White;
        if (highlight.HasValue && highlightAlpha > 0.01f)
        {
            tint = Colors.White.Lerp(highlight.Value, Mathf.Clamp(highlightAlpha, 0f, 0.82f));
        }

        return CreateTextureStyle(
            HpBackgroundTexture,
            10f,
            10f,
            0f,
            tint,
            new Color(0.08f, 0.025f, 0.035f, 0.96f));
    }

    /// <summary>
    /// 创建血条填充九宫格样式。
    /// </summary>
    public static StyleBox CreateHpFillStyle(bool disabled = false)
        => CreateTextureStyle(
            HpFillTexture,
            10f,
            10f,
            0f,
            disabled ? new Color(0.48f, 0.48f, 0.50f) : Colors.White,
            disabled ? new Color(0.34f, 0.34f, 0.36f) : new Color(0.90f, 0.16f, 0.18f));

    /// <summary>
    /// 敌方生命条使用不圆角的硬边像素框，与玩家的平滑红条明确区分。
    /// </summary>
    public static StyleBoxFlat CreateEnemyHpBackgroundStyle(Color? highlight = null, float highlightAlpha = 0f)
    {
        var border = new Color(0.53f, 0.08f, 0.18f, 0.98f);
        if (highlight.HasValue && highlightAlpha > 0.01f)
            border = border.Lerp(highlight.Value, Mathf.Clamp(highlightAlpha, 0f, 0.85f));
        return new StyleBoxFlat
        {
            BgColor = new Color(0.035f, 0.025f, 0.045f, 0.98f),
            BorderColor = border,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0
        };
    }

    /// <summary>敌方像素生命填充；超量生命保留金色提示。</summary>
    public static StyleBoxFlat CreateEnemyHpFillStyle(bool overflowing = false)
    {
        return new StyleBoxFlat
        {
            BgColor = overflowing ? new Color(1.00f, 0.76f, 0.12f, 0.98f) : new Color(0.88f, 0.08f, 0.16f, 0.98f),
            BorderColor = overflowing ? new Color(1.00f, 0.94f, 0.48f, 0.96f) : new Color(1.00f, 0.32f, 0.38f, 0.96f),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 0,
            CornerRadiusTopRight = 0,
            CornerRadiusBottomLeft = 0,
            CornerRadiusBottomRight = 0
        };
    }

    /// <summary>
    /// 创建不替换图标内容、只提供科技外框的图标底板。
    /// </summary>
    public static TextureRect CreateIconFrame(float size)
    {
        return new TextureRect
        {
            Texture = IconFrameTexture,
            CustomMinimumSize = new Vector2(size, size),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
    }

    /// <summary>
    /// 创建费用圆环九宫格样式；选中目标时允许沿用现有高亮颜色。
    /// </summary>
    public static StyleBox CreateManaCircleStyle(Color? highlight = null, float highlightAlpha = 0f)
    {
        var tint = Colors.White;
        if (highlight.HasValue && highlightAlpha > 0.01f)
        {
            tint = Colors.White.Lerp(highlight.Value, Mathf.Clamp(highlightAlpha, 0f, 0.85f));
        }

        return CreateTextureStyle(
            ManaCircleTexture,
            18f,
            18f,
            0f,
            tint,
            new Color(0.04f, 0.08f, 0.10f, 0.92f));
    }

    private static Texture2D? LoadTexture(string relativePath)
        => GD.Load<Texture2D>($"{Root}/{relativePath}");

    private static StyleBox CreateButtonStyle(Texture2D? texture)
        => CreateTextureStyle(
            texture,
            18f,
            12f,
            8f,
            Colors.White,
            new Color(0.035f, 0.065f, 0.08f, 0.96f));

    private static StyleBox CreateTextureStyle(
        Texture2D? texture,
        float horizontalTextureMargin,
        float verticalTextureMargin,
        float contentMargin,
        Color tint,
        Color fallbackColor)
    {
        if (texture == null)
        {
            return new StyleBoxFlat
            {
                BgColor = fallbackColor,
                BorderColor = new Color(0.10f, 0.72f, 0.74f, 0.76f),
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
                ContentMarginBottom = contentMargin,
                ContentMarginLeft = contentMargin,
                ContentMarginRight = contentMargin,
                ContentMarginTop = contentMargin
            };
        }

        return new StyleBoxTexture
        {
            Texture = texture,
            TextureMarginBottom = verticalTextureMargin,
            TextureMarginLeft = horizontalTextureMargin,
            TextureMarginRight = horizontalTextureMargin,
            TextureMarginTop = verticalTextureMargin,
            ContentMarginBottom = contentMargin,
            ContentMarginLeft = contentMargin,
            ContentMarginRight = contentMargin,
            ContentMarginTop = contentMargin,
            ModulateColor = tint,
            DrawCenter = true
        };
    }
}
