//////////////////////////////////////////////////////////
// 文件：Scripts/Presentation/UIResourceDatabase.cs
//
// 模块：Presentation / UI Skin
//
// 职责：
// 1. 集中加载商店、事件、战斗日志与通用 UI 切片。
// 2. 为运行时创建的 Control 提供统一九宫格与滚动条样式。
// 3. 在资源缺失时提供可读的后备样式，避免表现资源阻断界面功能。
//
// 不负责：
// × 创建界面布局。
// × 处理购买、事件或日志业务逻辑。
// × 保存任何运行时游戏状态。
//////////////////////////////////////////////////////////

using Godot;

/// <summary>
/// 固定尺寸装饰角的位置语义。
/// </summary>
public enum UIDecorationCorner
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}

/// <summary>
/// 非战斗专属界面的统一视觉资源入口。
///
/// UI 控制器只声明需要的视觉语义，不直接依赖纹理路径；以后替换整套皮肤时，
/// 只需更新本类映射和对应图片，不需要触碰商店、事件或日志逻辑。
/// </summary>
public static class UIResourceDatabase
{
    private const string Root = "res://Assets/UI";

    private static readonly Texture2D? CommonPanel = Load("Common/common_panel.png");
    private static readonly Texture2D? CommonHeader = Load("Common/common_header.png");
    private static readonly Texture2D? CommonCard = Load("Common/common_card_background.png");
    private static readonly Texture2D? CommonPrice = Load("Common/common_price_panel.png");
    private static readonly Texture2D? PrimaryPanel = Load("Panel/panel_primary.png");
    private static readonly Texture2D? SecondaryPanel = Load("Panel/panel_secondary.png");
    private static readonly Texture2D? DescriptionPanel = Load("Panel/panel_description.png");
    private static readonly Texture2D? ShopFrame = Load("Shop/shop_frame.png");
    private static readonly Texture2D? ShopItemNormal = Load("Shop/item_panel_normal.png");
    private static readonly Texture2D? ShopItemHover = Load("Shop/item_panel_hover.png");
    private static readonly Texture2D? ShopItemSelected = Load("Shop/item_panel_selected.png");
    private static readonly Texture2D? ShopItemDisabled = Load("Shop/item_panel_disabled.png");
    private static readonly Texture2D? ShopGold = Load("Shop/gold_panel.png");
    private static readonly Texture2D? EventFrame = Load("Event/event_frame.png");
    private static readonly Texture2D? EventDescription = Load("Event/description_panel.png");
    private static readonly Texture2D? EventOptionNormal = Load("Event/option_normal.png");
    private static readonly Texture2D? EventOptionHover = Load("Event/option_hover.png");
    private static readonly Texture2D? EventOptionPressed = Load("Event/option_pressed.png");
    private static readonly Texture2D? EventOptionDisabled = Load("Event/option_disabled.png");
    private static readonly Texture2D? EventOptionCompleted = Load("Event/option_completed.png");
    private static readonly Texture2D? BattleLogFrame = Load("BattleLog/battlelog_frame.png");
    private static readonly Texture2D? LogEntryPanel = Load("BattleLog/log_entry_panel.png");
    private static readonly Texture2D? CategoryNormal = Load("BattleLog/category_normal.png");
    private static readonly Texture2D? CategorySelected = Load("BattleLog/category_selected.png");
    private static readonly Texture2D? ButtonNormal = Load("Button/common_button_normal.png");
    private static readonly Texture2D? ButtonHover = Load("Button/common_button_hover.png");
    private static readonly Texture2D? ButtonPressed = Load("Button/common_button_pressed.png");
    private static readonly Texture2D? ButtonDisabled = Load("Button/common_button_disabled.png");
    private static readonly Texture2D? ScrollTrack = Load("Scroll/scroll_track.png");
    private static readonly Texture2D? ScrollGrabber = Load("Scroll/scroll_grabber.png");
    private static readonly Texture2D? ScrollGrabberHover = Load("Scroll/scroll_grabber_hover.png");
    private static readonly Texture2D? ScrollGrabberPressed = Load("Scroll/scroll_grabber_pressed.png");
    private static readonly Texture2D? DividerTile = Load("Decoration/divider_tile.png");
    private static readonly Texture2D? CornerTopLeft = Load("Decoration/corner_top_left.png");
    private static readonly Texture2D? CornerTopRight = Load("Decoration/corner_top_right.png");
    private static readonly Texture2D? CornerBottomLeft = Load("Decoration/corner_bottom_left.png");
    private static readonly Texture2D? CornerBottomRight = Load("Decoration/corner_bottom_right.png");

    /// <summary>
    /// 创建商店主框架九宫格样式。
    /// </summary>
    public static StyleBox CreateShopFrameStyle(float contentMargin = 18f)
        => CreateTextureStyle(ShopFrame, 22f, 22f, contentMargin);

    /// <summary>
    /// 创建事件主框架九宫格样式。
    /// </summary>
    public static StyleBox CreateEventFrameStyle(float contentMargin = 18f)
        => CreateTextureStyle(EventFrame, 22f, 22f, contentMargin);

    /// <summary>
    /// 创建战斗日志主框架九宫格样式。
    /// </summary>
    public static StyleBox CreateBattleLogFrameStyle(float contentMargin = 14f)
        => CreateTextureStyle(BattleLogFrame, 22f, 22f, contentMargin);

    /// <summary>
    /// 创建通用标题栏九宫格样式。
    /// </summary>
    public static StyleBox CreateHeaderStyle(float contentMargin = 10f)
        => CreateTextureStyle(CommonHeader, 18f, 14f, contentMargin);

    /// <summary>
    /// 创建说明区域九宫格样式。
    /// </summary>
    public static StyleBox CreateDescriptionStyle(float contentMargin = 14f, bool eventStyle = false)
        => CreateTextureStyle(eventStyle ? EventDescription : DescriptionPanel, 18f, 16f, contentMargin);

    /// <summary>
    /// 创建通用信息面板九宫格样式。
    /// </summary>
    public static StyleBox CreateCommonPanelStyle(float contentMargin = 12f)
        => CreateTextureStyle(CommonPanel, 20f, 20f, contentMargin);

    /// <summary>
    /// 创建通用主面板九宫格样式。
    /// </summary>
    public static StyleBox CreatePrimaryPanelStyle(float contentMargin = 14f)
        => CreateTextureStyle(PrimaryPanel, 20f, 20f, contentMargin);

    /// <summary>
    /// 创建通用次级面板九宫格样式。
    /// </summary>
    public static StyleBox CreateSecondaryPanelStyle(float contentMargin = 12f)
        => CreateTextureStyle(SecondaryPanel, 18f, 18f, contentMargin);

    /// <summary>
    /// 创建通用卡片九宫格样式。
    /// </summary>
    public static StyleBox CreateCardStyle(float contentMargin = 10f)
        => CreateTextureStyle(CommonCard, 16f, 16f, contentMargin);

    /// <summary>
    /// 创建金币区域九宫格样式。
    /// </summary>
    public static StyleBox CreateGoldPanelStyle(float contentMargin = 8f)
        => CreateTextureStyle(ShopGold, 16f, 12f, contentMargin);

    /// <summary>
    /// 创建通用价格区域九宫格样式。
    /// </summary>
    public static StyleBox CreatePricePanelStyle(float contentMargin = 8f)
        => CreateTextureStyle(CommonPrice, 14f, 10f, contentMargin);

    /// <summary>
    /// 根据商品交互状态返回统一的商品卡片样式。
    /// </summary>
    public static StyleBox CreateShopItemStyle(bool hovered, bool selected = false, bool disabled = false)
    {
        var texture = disabled
            ? ShopItemDisabled
            : selected
                ? ShopItemSelected
                : hovered
                    ? ShopItemHover
                    : ShopItemNormal;
        return CreateTextureStyle(texture, 18f, 18f, 12f);
    }

    /// <summary>
    /// 创建日志条目样式；分类颜色仅做轻微色调提示，不改变日志文字颜色。
    /// </summary>
    public static StyleBox CreateLogEntryStyle(Color? tint = null)
        => CreateTextureStyle(LogEntryPanel, 16f, 14f, 8f, tint ?? Colors.White);

    /// <summary>
    /// 创建横向平铺分隔线。纹理只沿水平方向重复，不随容器宽度拉伸变形。
    /// </summary>
    public static TextureRect CreateDivider()
    {
        return new TextureRect
        {
            Texture = DividerTile,
            CustomMinimumSize = new Vector2(0f, 4f),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Tile,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest
        };
    }

    /// <summary>
    /// 返回固定尺寸角标纹理。角标不参与 NinePatch 或 Tile，避免轮廓变形。
    /// </summary>
    public static Texture2D? GetCornerTexture(UIDecorationCorner corner)
        => corner switch
        {
            UIDecorationCorner.TopLeft => CornerTopLeft,
            UIDecorationCorner.TopRight => CornerTopRight,
            UIDecorationCorner.BottomLeft => CornerBottomLeft,
            UIDecorationCorner.BottomRight => CornerBottomRight,
            _ => null
        };

    /// <summary>
    /// 应用项目通用按钮的 Normal、Hover、Pressed、Disabled 四态。
    /// </summary>
    public static void ApplyCommonButton(Button button)
    {
        ApplyButtonTextures(button, ButtonNormal, ButtonHover, ButtonPressed, ButtonDisabled);
    }

    /// <summary>
    /// 应用事件选项按钮四态；选项可继续由现有逻辑控制是否可用。
    /// </summary>
    public static void ApplyEventOptionButton(Button button)
    {
        ApplyButtonTextures(button, EventOptionNormal, EventOptionHover, EventOptionPressed, EventOptionDisabled, 18f, 14f);
        button.AddThemeColorOverride("font_hover_color", new Color(0.84f, 1f, 1f));
    }

    /// <summary>
    /// 将事件选项切换为已完成视觉状态，不改变按钮自身业务状态。
    /// </summary>
    public static void ApplyCompletedEventOption(Button button)
    {
        var completed = CreateTextureStyle(EventOptionCompleted, 18f, 14f, 8f);
        button.AddThemeStyleboxOverride("normal", completed);
        button.AddThemeStyleboxOverride("hover", completed);
        button.AddThemeStyleboxOverride("pressed", completed);
    }

    /// <summary>
    /// 应用战斗日志分类按钮样式，Toggle 的按下状态使用高亮纹理。
    /// </summary>
    public static void ApplyBattleLogCategoryButton(Button button)
    {
        ApplyButtonTextures(button, CategoryNormal, CategorySelected, CategorySelected, ButtonDisabled, 14f, 12f);
    }

    /// <summary>
    /// 将统一滚动条皮肤应用到指定 ScrollContainer。
    /// </summary>
    public static void ApplyScrollBar(ScrollContainer scrollContainer)
    {
        ApplyScrollBarControl(scrollContainer.GetVScrollBar());
        ApplyScrollBarControl(scrollContainer.GetHScrollBar());
    }

    /// <summary>
    /// 递归处理界面中的所有滚动容器。
    ///
    /// 动态界面在完成节点创建后调用一次即可，避免 Shop、Inventory、Developer、
    /// Codex 和 BattleLog 各自维护不同的滚动条资源。
    /// </summary>
    public static void ApplyScrollBars(Node root)
    {
        if (root is ScrollContainer scrollContainer)
        {
            ApplyScrollBar(scrollContainer);
        }

        foreach (var child in root.GetChildren())
        {
            ApplyScrollBars(child);
        }
    }

    private static void ApplyScrollBarControl(ScrollBar scrollBar)
    {
        scrollBar.CustomMinimumSize = scrollBar is VScrollBar
            ? new Vector2(18f, 0f)
            : new Vector2(0f, 18f);
        scrollBar.AddThemeStyleboxOverride("scroll", CreateTextureStyle(ScrollTrack, 5f, 5f, 0f));
        scrollBar.AddThemeStyleboxOverride("grabber", CreateTextureStyle(ScrollGrabber, 5f, 5f, 0f));
        scrollBar.AddThemeStyleboxOverride("grabber_highlight", CreateTextureStyle(ScrollGrabberHover, 5f, 5f, 0f));
        scrollBar.AddThemeStyleboxOverride("grabber_pressed", CreateTextureStyle(ScrollGrabberPressed, 5f, 5f, 0f));
    }

    private static void ApplyButtonTextures(
        Button button,
        Texture2D? normal,
        Texture2D? hover,
        Texture2D? pressed,
        Texture2D? disabled,
        float horizontalMargin = 12f,
        float verticalMargin = 10f)
    {
        button.AddThemeStyleboxOverride("normal", CreateTextureStyle(normal, horizontalMargin, verticalMargin, 8f));
        button.AddThemeStyleboxOverride("hover", CreateTextureStyle(hover, horizontalMargin, verticalMargin, 8f));
        button.AddThemeStyleboxOverride("pressed", CreateTextureStyle(pressed, horizontalMargin, verticalMargin, 8f));
        button.AddThemeStyleboxOverride("disabled", CreateTextureStyle(disabled, horizontalMargin, verticalMargin, 8f));
        button.AddThemeStyleboxOverride("focus", CreateTextureStyle(hover, horizontalMargin, verticalMargin, 8f));
        button.AddThemeColorOverride("font_color", new Color(0.78f, 0.88f, 0.90f));
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", new Color(0.92f, 0.84f, 0.94f));
        button.AddThemeColorOverride("font_disabled_color", new Color(0.40f, 0.44f, 0.48f));
    }

    private static Texture2D? Load(string relativePath)
        => GD.Load<Texture2D>($"{Root}/{relativePath}");

    private static StyleBox CreateTextureStyle(
        Texture2D? texture,
        float horizontalTextureMargin,
        float verticalTextureMargin,
        float contentMargin,
        Color? tint = null)
    {
        if (texture == null)
        {
            return new StyleBoxFlat
            {
                BgColor = new Color(0.015f, 0.025f, 0.035f, 0.96f),
                BorderColor = new Color(0.10f, 0.66f, 0.70f, 0.78f),
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
            ModulateColor = tint ?? Colors.White,
            DrawCenter = true
        };
    }
}
